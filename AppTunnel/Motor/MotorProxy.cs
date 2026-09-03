using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Timer = System.Threading.Timer;
using AppTunnel.Interop;
using AppTunnel.Modelos;
using AppTunnel.Servicos;

namespace AppTunnel.Motor;

internal sealed class MotorProxy : IDisposable
{
    // FIN/RST de uma conexão fechada ainda podem estar em trânsito quando o
    // evento CLOSE chega; a varredura só remove o mapeamento depois dessa
    // janela, para a CamadaRede continuar NATeando esses pacotes finais.
    private const long GracaFechamentoMs = 3000;

    private readonly ConcurrentDictionary<uint, Instancia> _instancias = new();
    private readonly ConcurrentDictionary<ushort, Conexao> _conexoes = new();

    private readonly RelaySocks5 _relay;
    private readonly CamadaSocket _camadaSocket;
    private readonly CamadaRede _camadaRede;
    private readonly Telemetria _telemetria;
    private readonly Timer _varredura;

    public MotorProxy(Telemetria telemetria)
    {
        _telemetria = telemetria;

        _relay = new RelaySocks5(_instancias, _conexoes, _telemetria);
        _relay.Iniciar();

        // Handle da CamadaSocket só abre quando houver PID alvo — ver
        // AtualizarFiltro em Lancar/Encerrar.
        _camadaSocket = new CamadaSocket(_instancias, _conexoes, _telemetria);
        _camadaRede = new CamadaRede(_instancias, _conexoes, _telemetria);

        _varredura = new Timer(_ => Varrer(), null, 1000, 1000);
    }

    public IEnumerable<Instancia> Instancias => _instancias.Values;
    public IEnumerable<Conexao> Conexoes => _conexoes.Values;

    // CREATE_SUSPENDED -> registra vínculo -> ResumeThread. Nenhum pacote do
    // processo pode preceder o registro em _instancias (invariante 1).
    public Instancia Lancar(string caminhoExe, string argumentos, string diretorio, string nome, IReadOnlyList<ProxyCfg> proxies, int proxyInicial = 0, IReadOnlyList<FaixaIp>? faixasPassthrough = null)
    {
        if (proxies.Count == 0)
            throw new ArgumentException("Instância sem proxy configurado", nameof(proxies));

        var startupInfo = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>() };
        var linhaComando = string.IsNullOrEmpty(argumentos)
            ? $"\"{caminhoExe}\""
            : $"\"{caminhoExe}\" {argumentos}";

        if (!Kernel32.CreateProcess(null, linhaComando, IntPtr.Zero, IntPtr.Zero, false,
                Kernel32.CREATE_SUSPENDED, IntPtr.Zero, diretorio, ref startupInfo, out var pi))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcess falhou");
        }

        var instancia = new Instancia
        {
            Pid = (uint)pi.dwProcessId,
            Nome = nome,
            HandleProcesso = pi.hProcess,
            Proxies = proxies,
            Indice = proxyInicial,
            FaixasPassthrough = faixasPassthrough ?? Array.Empty<FaixaIp>(),
        };

        _instancias[instancia.Pid] = instancia;
        _camadaSocket.AtualizarFiltro(); // inclui o novo PID antes do processo poder rodar

        Kernel32.ResumeThread(pi.hThread);
        Kernel32.CloseHandle(pi.hThread);

        return instancia;
    }

    public void Encerrar(uint pid)
    {
        if (_instancias.TryRemove(pid, out var instancia))
        {
            instancia.Viva = false;
            Kernel32.TerminateProcess(instancia.HandleProcesso, 0);
            Kernel32.CloseHandle(instancia.HandleProcesso);
            _camadaSocket.AtualizarFiltro(); // tira o PID do filtro — evita pegar reuso de PID por outro processo
            _telemetria.Registrar(NivelLog.Info, $"Instância '{instancia.Nome}' (PID={pid}) encerrada");
        }
    }

    private void Varrer()
    {
        var agora = Environment.TickCount64;
        foreach (var (porta, conexao) in _conexoes)
        {
            if (conexao.Estado == "fechada" && agora - conexao.Marca > GracaFechamentoMs)
                _conexoes.TryRemove(porta, out _);
        }

        // Invariante 4: só avança o proxy quando a instância não tem conexão
        // ativa — nunca troca de IP no meio de uma sessão (char/map server).
        foreach (var instancia in _instancias.Values)
        {
            if (instancia.PendenteFailover && instancia.ConexoesAtivas == 0)
            {
                instancia.Indice = (instancia.Indice + 1) % instancia.Proxies.Count;
                instancia.PendenteFailover = false;
                _telemetria.Registrar(NivelLog.Aviso,
                    $"Instância '{instancia.Nome}' (PID={instancia.Pid}) trocou de proxy: {instancia.ProxyAtual.Host}:{instancia.ProxyAtual.Porta}");
            }
        }
    }

    public void Dispose()
    {
        _varredura.Dispose();
        _relay.Parar();
        _camadaSocket.Parar();
        _camadaRede.Parar();

        foreach (var instancia in _instancias.Values)
        {
            Kernel32.TerminateProcess(instancia.HandleProcesso, 0);
            Kernel32.CloseHandle(instancia.HandleProcesso);
        }
    }
}
