using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using AppTunnel.Modelos;
using AppTunnel.Servicos;

namespace AppTunnel.Motor;

// TcpListener em IPAddress.Any:34567 — a CamadaRede redireciona pro IP real
// de saída da máquina, não pra 127.0.0.1, então o listener precisa aceitar
// em qualquer interface. Isso expõe a porta à LAN se não houver firewall
// bloqueando entrada nela.
internal sealed class RelaySocks5
{
    public const ushort Porta = 34567;

    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<uint, Instancia> _instancias;
    private readonly ConcurrentDictionary<ushort, Conexao> _conexoes;
    private readonly Telemetria _telemetria;
    private readonly CancellationTokenSource _cts = new();

    public RelaySocks5(ConcurrentDictionary<uint, Instancia> instancias, ConcurrentDictionary<ushort, Conexao> conexoes, Telemetria telemetria)
    {
        _instancias = instancias;
        _conexoes = conexoes;
        _telemetria = telemetria;
        _listener = new TcpListener(IPAddress.Any, Porta);
    }

    public void Iniciar()
    {
        try
        {
            _listener.Start();
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            throw new InvalidOperationException(
                $"Porta {Porta} já está em uso. Provavelmente outra instância do AppTunnel.exe ainda está rodando — " +
                "feche-a (Gerenciador de Tarefas) antes de abrir uma nova.", ex);
        }
        _ = AceitarLoopAsync(_cts.Token);
    }

    public void Parar()
    {
        _cts.Cancel();
        _listener.Stop();
    }

    private async Task AceitarLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient cliente;
            try
            {
                cliente = await _listener.AcceptTcpClientAsync(ct);
            }
            catch (Exception) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Erro no accept costuma ser fatal (listener quebrado), não
                // transitório — encerra o loop em vez de girar a CPU tentando
                // de novo sem pausa.
                _telemetria.Registrar(NivelLog.Erro, $"RelaySocks5: AcceptTcpClientAsync falhou, loop de aceite encerrado: {ex.Message}");
                return;
            }
            _ = AtenderAsync(cliente, ct);
        }
    }

    private async Task AtenderAsync(TcpClient cliente, CancellationToken ct)
    {
        using var _ = cliente;
        cliente.NoDelay = true;

        var portaLocal = (ushort)((IPEndPoint)cliente.Client.RemoteEndPoint!).Port;
        if (!_conexoes.TryGetValue(portaLocal, out var conexao) ||
            !_instancias.TryGetValue(conexao.Pid, out var instancia))
        {
            _telemetria.Registrar(NivelLog.Aviso,
                $"RelaySocks5: conexão aceita em porta={portaLocal} sem mapeamento em _conexoes — encerrando (fail-closed)");
            return; // fail-closed: sem mapeamento, encerra
        }

        _telemetria.Registrar(NivelLog.Info,
            $"RelaySocks5: conexão aceita porta={portaLocal} PID={conexao.Pid} destino={FormatarIp(conexao.IpDestino)}:{conexao.PortaDestino}");

        conexao.Estado = "conectando";
        Interlocked.Increment(ref instancia.ConexoesAtivas);
        try
        {
            var proxy = instancia.ProxyAtual;
            using var upstream = new TcpClient();
            await upstream.ConnectAsync(proxy.Host, proxy.Porta, ct);
            upstream.NoDelay = true;

            using var fluxoUpstream = upstream.GetStream();
            await ClienteSocks5.ConectarAsync(fluxoUpstream, proxy, conexao.IpDestino, conexao.PortaDestino, ct);

            conexao.Estado = "ativa";
            using var fluxoCliente = cliente.GetStream();

            var t1 = BombearAsync(fluxoCliente, fluxoUpstream, conexao, paraUpstream: true, ct);
            var t2 = BombearAsync(fluxoUpstream, fluxoCliente, conexao, paraUpstream: false, ct);
            await Task.WhenAny(t1, t2);
        }
        catch (Exception ex)
        {
            conexao.Estado = "erro";
            Interlocked.Increment(ref instancia.Falhas);
            _telemetria.Registrar(NivelLog.Erro,
                $"PID={conexao.Pid} porta={portaLocal} destino={conexao.IpDestino:X8}:{conexao.PortaDestino} via {instancia.ProxyAtual.Host}:{instancia.ProxyAtual.Porta} falhou: {ex.Message}");
        }
        finally
        {
            Interlocked.Decrement(ref instancia.ConexoesAtivas);
            if (conexao.Estado == "erro")
                instancia.PendenteFailover = true;
            conexao.Estado = "fechada";
            conexao.Marca = Environment.TickCount64;
        }
    }

    private static string FormatarIp(uint v) => $"{(byte)(v >> 24)}.{(byte)(v >> 16)}.{(byte)(v >> 8)}.{(byte)v}";

    private static async Task BombearAsync(NetworkStream origem, NetworkStream destino, Conexao conexao, bool paraUpstream, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        try
        {
            int n;
            while ((n = await origem.ReadAsync(buffer, ct)) > 0)
            {
                await destino.WriteAsync(buffer.AsMemory(0, n), ct);
                if (paraUpstream) Interlocked.Add(ref conexao.Enviados, n);
                else Interlocked.Add(ref conexao.Recebidos, n);
                conexao.Marca = Environment.TickCount64;
            }
        }
        catch
        {
            // Uma ponta fechou ou deu erro de rede; a outra tarefa do
            // bombeamento encerra o par via Task.WhenAny em AtenderAsync.
        }
    }
}
