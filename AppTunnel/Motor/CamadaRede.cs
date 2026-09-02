using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using AppTunnel.Interop;
using AppTunnel.Modelos;
using AppTunnel.Servicos;

namespace AppTunnel.Motor;

// Reescreve endereço/porta direto nos bytes crus do header IP/TCP — nunca
// via WINDIVERT_ADDRESS, que só carrega IfIdx/SubIfIdx nesta camada.
// Redireciona pro IP real de saída da máquina, não pra 127.0.0.1: o strong
// host model do Windows rejeita pacote outbound reescrito pra loopback e
// reinjetado via WinDivertSend (ver PROJETO.md §9). RelaySocks5 escuta em
// IPAddress.Any por causa disso.
internal sealed class CamadaRede
{
    // WINDIVERT_MTU_MAX = 40 + 0xFFFF, não 0xFFFF: com LSO/TSO ligado
    // (padrão na maioria das placas), pacotes de saída chegam na WinDivert
    // maiores que o MTU físico. Buffer pequeno demais trunca e descarta o
    // pacote sem reenviar — e o filtro casa TCP outbound de qualquer
    // processo da máquina, não só o nosso.
    private const int TamanhoMaximoPacote = 40 + 0xFFFF;
    private const long IntervaloHeartbeatMs = 5000;

    private readonly IntPtr _handle;
    private readonly Thread _thread;
    private readonly ConcurrentDictionary<ushort, Conexao> _conexoes;
    private readonly Telemetria _telemetria;
    private readonly byte[] _enderecoLocal; // IPv4 desta máquina, 4 bytes, ordem de rede
    private volatile bool _executando;

    public CamadaRede(ConcurrentDictionary<ushort, Conexao> conexoes, Telemetria telemetria)
    {
        _conexoes = conexoes;
        _telemetria = telemetria;
        _enderecoLocal = DescobrirEnderecoLocal();
        var enderecoTexto = string.Join('.', _enderecoLocal);

        var filtro =
            "ip and tcp and outbound and " +
            $"(ip.DstAddr != {enderecoTexto} or (ip.SrcAddr == {enderecoTexto} and tcp.SrcPort == {RelaySocks5.Porta}))";

        _handle = WinDivert.WinDivertOpen(filtro, CamadaWinDivert.Rede, 0, 0);
        if (_handle == new IntPtr(-1))
        {
            var erro = Marshal.GetLastWin32Error();
            _telemetria.Registrar(NivelLog.Erro, $"CamadaRede: WinDivertOpen falhou, Win32Error={erro}");
            throw new Win32Exception(erro, "WinDivertOpen (NETWORK) falhou");
        }
        _telemetria.Registrar(NivelLog.Info, $"CamadaRede aberta (buffer={TamanhoMaximoPacote} bytes, IP local={enderecoTexto})");

        _executando = true;
        _thread = new Thread(Loop) { IsBackground = true, Name = "CamadaRede" };
        _thread.Start();
    }

    // Connect() num socket UDP não transmite nada — só pede pro SO resolver
    // a rota/IP de origem pra esse destino.
    private static byte[] DescobrirEnderecoLocal()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect("8.8.8.8", 65530);
        return ((IPEndPoint)socket.LocalEndPoint!).Address.GetAddressBytes();
    }

    private void Loop()
    {
        var pacote = new byte[TamanhoMaximoPacote];
        var endereco = new byte[WinDivertAddress.Tamanho];

        long recebidos = 0, reescritos = 0, falhasRecv = 0, falhasSend = 0;
        var ultimoHeartbeat = Environment.TickCount64;
        var ultimoErroRecvLogado = 0;
        var ultimoErroSendLogado = 0;

        while (_executando)
        {
            if (!WinDivert.WinDivertRecv(_handle, pacote, (uint)pacote.Length, out var tamanho, endereco))
            {
                if (!_executando) break;

                falhasRecv++;
                var erro = Marshal.GetLastWin32Error();
                if (erro != ultimoErroRecvLogado)
                {
                    _telemetria.Registrar(NivelLog.Aviso, $"CamadaRede: WinDivertRecv falhou, Win32Error={erro}");
                    ultimoErroRecvLogado = erro;
                }
                continue;
            }
            ultimoErroRecvLogado = 0;
            recebidos++;

            try
            {
                if (Reescrever(pacote, tamanho))
                {
                    reescritos++;
                    WinDivert.WinDivertHelperCalcChecksums(pacote, tamanho, endereco, 0);
                }
            }
            catch (Exception ex)
            {
                // Nunca deixar a exceção pular o WinDivertSend abaixo: o filtro
                // casa tráfego de qualquer processo da máquina, não só o nosso.
                _telemetria.Registrar(NivelLog.Erro, $"CamadaRede: exceção reescrevendo pacote ({tamanho} bytes): {ex.Message}");
            }

            if (!WinDivert.WinDivertSend(_handle, pacote, tamanho, out _, endereco))
            {
                falhasSend++;
                var erro = Marshal.GetLastWin32Error();
                if (erro != ultimoErroSendLogado)
                {
                    _telemetria.Registrar(NivelLog.Erro, $"CamadaRede: WinDivertSend falhou, Win32Error={erro}, tamanho={tamanho}");
                    ultimoErroSendLogado = erro;
                }
            }
            else
            {
                ultimoErroSendLogado = 0;
            }

            var agora = Environment.TickCount64;
            if (agora - ultimoHeartbeat > IntervaloHeartbeatMs)
            {
                _telemetria.Registrar(NivelLog.Info,
                    $"CamadaRede: {recebidos} recebidos, {reescritos} reescritos, {falhasRecv} falhas recv, {falhasSend} falhas send (últimos {IntervaloHeartbeatMs / 1000}s)");
                recebidos = reescritos = falhasRecv = falhasSend = 0;
                ultimoHeartbeat = agora;
            }
        }
    }

    // Fail-closed só vale pra portas com conexão rastreada; tráfego alheio
    // casa o filtro mas não tem entrada em _conexoes e passa intocado.
    private bool Reescrever(byte[] pacote, uint tamanho)
    {
        var ihl = (pacote[0] & 0x0F) * 4;
        if (tamanho < ihl + 20) return false; // menor que IP+TCP mínimo

        var offsetTcp = ihl;
        var dstEhLocal = pacote[16] == _enderecoLocal[0] && pacote[17] == _enderecoLocal[1] &&
                          pacote[18] == _enderecoLocal[2] && pacote[19] == _enderecoLocal[3];

        if (!dstEhLocal)
        {
            // Perna cliente -> destino real. Chave: TCP SrcPort = porta local do cliente.
            var portaLocal = (ushort)((pacote[offsetTcp] << 8) | pacote[offsetTcp + 1]);
            if (!_conexoes.TryGetValue(portaLocal, out var conexao)) return false;

            // Fonte confiável do destino real: o próprio pacote (SYN, sempre o
            // primeiro visto nesta porta), não o evento CONNECT da CamadaSocket
            // — RemoteAddr vem zerado ali para conexões assíncronas (ConnectEx).
            // Só grava na primeira vez; os pacotes seguintes já vão pro IP local
            // e não carregam mais o destino real.
            if (conexao.IpDestino == 0)
            {
                conexao.IpDestino = (uint)((pacote[16] << 24) | (pacote[17] << 16) | (pacote[18] << 8) | pacote[19]);
                conexao.PortaDestino = (ushort)((pacote[offsetTcp + 2] << 8) | pacote[offsetTcp + 3]);
            }

            pacote[16] = _enderecoLocal[0]; pacote[17] = _enderecoLocal[1];
            pacote[18] = _enderecoLocal[2]; pacote[19] = _enderecoLocal[3];
            pacote[offsetTcp + 2] = (byte)(RelaySocks5.Porta >> 8);
            pacote[offsetTcp + 3] = unchecked((byte)RelaySocks5.Porta);
            return true;
        }
        else
        {
            // Perna RelaySocks5 -> cliente (só chega aqui com SrcPort==34567,
            // garantido pelo filtro). Chave: TCP DstPort = porta local do cliente.
            var portaLocal = (ushort)((pacote[offsetTcp + 2] << 8) | pacote[offsetTcp + 3]);
            if (!_conexoes.TryGetValue(portaLocal, out var conexao)) return false;

            pacote[12] = (byte)(conexao.IpDestino >> 24);
            pacote[13] = (byte)(conexao.IpDestino >> 16);
            pacote[14] = (byte)(conexao.IpDestino >> 8);
            pacote[15] = (byte)conexao.IpDestino;
            pacote[offsetTcp] = (byte)(conexao.PortaDestino >> 8);
            pacote[offsetTcp + 1] = (byte)conexao.PortaDestino;
            return true;
        }
    }

    public void Parar()
    {
        _executando = false;
        WinDivert.WinDivertShutdown(_handle, ComoWinDivertShutdown.Recv);
        _thread.Join();
        WinDivert.WinDivertClose(_handle);
    }
}
