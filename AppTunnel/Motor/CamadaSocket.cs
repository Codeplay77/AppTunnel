using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using AppTunnel.Interop;
using AppTunnel.Modelos;
using AppTunnel.Servicos;

namespace AppTunnel.Motor;

// Filtro sempre restrito por processId — sem PID no filtro, a camada SOCKET
// retém bind/connect/close de todo processo do sistema até a nossa thread
// consumir, travando a rede da máquina inteira. Flags SNIFF | RECV_ONLY:
// RECV_ONLY é obrigatória nessa camada; sem SNIFF a operação capturada fica
// retida sem forma de liberar (RECV_ONLY desativa WinDivertSend).
internal sealed class CamadaSocket
{
    private const long IntervaloHeartbeatMs = 5000;

    private readonly ConcurrentDictionary<uint, Instancia> _instancias;
    private readonly ConcurrentDictionary<ushort, Conexao> _conexoes;
    private readonly Telemetria _telemetria;
    private readonly object _trava = new();

    private IntPtr _handle = IntPtr.Zero;
    private Thread? _thread;
    private volatile bool _executando;

    public CamadaSocket(ConcurrentDictionary<uint, Instancia> instancias, ConcurrentDictionary<ushort, Conexao> conexoes, Telemetria telemetria)
    {
        _instancias = instancias;
        _conexoes = conexoes;
        _telemetria = telemetria;
        // Sem handle aqui: sem PID alvo ainda não há o que filtrar.
    }

    // Reabre o handle com o filtro restrito aos PIDs atuais de _instancias.
    // Chamar depois de registrar uma nova Instancia (antes do ResumeThread)
    // e depois de remover uma em Encerrar, para não deixar uma porta
    // reciclada presa a um filtro antigo.
    public void AtualizarFiltro()
    {
        lock (_trava)
        {
            FecharHandleAtual();

            var pids = _instancias.Keys.ToArray();
            if (pids.Length == 0)
                return;

            var condicaoPid = string.Join(" or ", pids.Select(p => $"processId == {p}"));
            var filtro = $"({condicaoPid}) and (event == CONNECT or event == CLOSE)";

            _handle = WinDivert.WinDivertOpen(filtro, CamadaWinDivert.Socket, 0, FlagWinDivert.Sniff | FlagWinDivert.RecvOnly);
            if (_handle == new IntPtr(-1))
            {
                var erro = Marshal.GetLastWin32Error();
                _telemetria.Registrar(NivelLog.Erro, $"CamadaSocket: WinDivertOpen falhou, Win32Error={erro}");
                throw new Win32Exception(erro, "WinDivertOpen (SOCKET) falhou");
            }
            _telemetria.Registrar(NivelLog.Info, $"CamadaSocket: filtro atualizado, {pids.Length} PID(s) alvo");

            _executando = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "CamadaSocket" };
            _thread.Start();
        }
    }

    private void FecharHandleAtual()
    {
        if (_thread == null) return;

        _executando = false;
        WinDivert.WinDivertShutdown(_handle, ComoWinDivertShutdown.Recv);
        _thread.Join();
        WinDivert.WinDivertClose(_handle);
        _thread = null;
        _handle = IntPtr.Zero;
    }

    private void Loop()
    {
        var handle = _handle;
        var endereco = new byte[WinDivertAddress.Tamanho];

        long eventos = 0, doAlvo = 0, falhasRecv = 0;
        var ultimoHeartbeat = Environment.TickCount64;
        var ultimoErroLogado = 0;

        while (_executando)
        {
            // Camada SOCKET não carrega pacote/dado.
            if (!WinDivert.WinDivertRecv(handle, null, 0, out _, endereco))
            {
                if (!_executando) break;

                falhasRecv++;
                var erro = Marshal.GetLastWin32Error();
                if (erro != ultimoErroLogado)
                {
                    _telemetria.Registrar(NivelLog.Aviso, $"CamadaSocket: WinDivertRecv falhou, Win32Error={erro}");
                    ultimoErroLogado = erro;
                }
                continue;
            }
            ultimoErroLogado = 0;
            eventos++;

            try
            {
                var pid = WinDivertAddress.ProcessId(endereco);
                if (!_instancias.TryGetValue(pid, out var instancia) || !instancia.Viva)
                    continue;

                doAlvo++;
                var portaLocal = WinDivertAddress.PortaLocal(endereco);

                if (WinDivertAddress.Evento(endereco) == EventoWinDivert.SocketClose)
                {
                    // Mapeamento não é removido aqui: FIN/RST ainda podem chegar
                    // na CamadaRede para essa porta. MotorProxy.Varrer remove
                    // depois de uma janela de graça.
                    if (_conexoes.TryGetValue(portaLocal, out var fechada) && fechada.Pid == pid)
                    {
                        fechada.Estado = "fechada";
                        fechada.Marca = Environment.TickCount64;
                    }
                    continue;
                }

                // CONNECT de TCP/IPv4. IpDestino/PortaDestino não vêm daqui — a
                // CamadaRede preenche a partir do próprio pacote SYN.
                if (WinDivertAddress.Evento(endereco) == EventoWinDivert.SocketConnect &&
                    WinDivertAddress.Protocolo(endereco) == 6 && !WinDivertAddress.IPv6(endereco))
                {
                    _conexoes[portaLocal] = new Conexao { Pid = pid, PortaLocal = portaLocal };
                    _telemetria.Registrar(NivelLog.Info, $"CamadaSocket: CONNECT PID={pid} portaLocal={portaLocal}");
                }
            }
            catch (Exception ex)
            {
                _telemetria.Registrar(NivelLog.Erro, $"CamadaSocket: exceção processando evento: {ex.Message}");
            }

            var agora = Environment.TickCount64;
            if (agora - ultimoHeartbeat > IntervaloHeartbeatMs)
            {
                _telemetria.Registrar(NivelLog.Info,
                    $"CamadaSocket: {eventos} eventos, {doAlvo} de PID alvo, {falhasRecv} falhas recv (últimos {IntervaloHeartbeatMs / 1000}s)");
                eventos = doAlvo = falhasRecv = 0;
                ultimoHeartbeat = agora;
            }
        }
    }

    public void Parar()
    {
        lock (_trava)
            FecharHandleAtual();
    }
}
