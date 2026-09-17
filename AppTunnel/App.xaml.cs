using System.Drawing;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;
using AppTunnel.Motor;
using AppTunnel.Modelos;
using AppTunnel.Servicos;
using AppTunnel.ViewModels;

namespace AppTunnel;

public partial class App : System.Windows.Application
{
    private MotorProxy? _motor;
    private NotifyIcon? _notificacaoSilenciosa;
    private MainWindow? _janela;
    private bool _encerraSemInstancias;
    public static bool ModoSilencioso { get; private set; }
    public static int? PerfilSolicitadoId { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ParseArgumentos(e.Args);

        if (HubComunicacao.TentarDelegar(PerfilSolicitadoId, mostrarJanela: !ModoSilencioso))
        {
            Shutdown();
            return;
        }

        _encerraSemInstancias = ModoSilencioso;

        var telemetria = new Telemetria();

        // Ancorado em AppContext.BaseDirectory (pasta do próprio .exe), não no
        // diretório de trabalho do processo — esse muda dependendo de como o
        // app é lançado (debugger da IDE, atalho, clique duplo), e um caminho
        // relativo faz o app ler/gravar config em pastas diferentes conforme
        // o lançamento, parecendo perder perfis/proxies salvos.
        var pastaConfig = Path.Combine(AppContext.BaseDirectory, "config");
        var caminhoProxies = Path.Combine(pastaConfig, "proxies.txt");
        var caminhoPerfis = Path.Combine(pastaConfig, "perfis.json");
        Directory.CreateDirectory(pastaConfig);

        // proxies é uma List<ProxyCfg> única, compartilhada por referência entre
        // GerenciadorInstancias e ProxiesViewModel: quando a UI salva a lista
        // editada, muta essa mesma instância (Clear+AddRange) em vez de trocar
        // a referência — assim quem já a segurava (perfis apontam pra ela por
        // índice) enxerga a atualização sem precisar de evento nenhum.
        var proxies = RepositorioProxies.Carregar(caminhoProxies);
        var perfis = Perfis.Carregar(caminhoPerfis);

        try
        {
            _motor = new MotorProxy(telemetria);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "AppTunnel não conseguiu iniciar", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var gerenciador = new GerenciadorInstancias(_motor, proxies, telemetria);

        var vm = new MainViewModel(
            new InstanciasViewModel(_motor, gerenciador, perfis, proxies, caminhoPerfis),
            new ConexoesViewModel(_motor),
            new ProxiesViewModel(_motor, proxies, caminhoProxies, telemetria),
            new LogViewModel(telemetria));

        _janela = new MainWindow { DataContext = vm };

        HubComunicacao.IniciarServidor(
            aoLancar: id => Dispatcher.Invoke(() => LancarPerfilPorId(id, perfis, gerenciador)),
            aoMostrar: () => Dispatcher.Invoke(MostrarJanela));

        if (PerfilSolicitadoId != null)
            LancarPerfilPorId(PerfilSolicitadoId.Value, perfis, gerenciador);

        if (!ModoSilencioso)
        {
            MostrarJanela();
            return;
        }

        _janela.ShowInTaskbar = false;
        _janela.WindowStartupLocation = WindowStartupLocation.Manual;
        _janela.Left = -32000;
        _janela.Top = -32000;
        _janela.Show();
        _janela.Hide();
        ExibirNotificacaoSilenciosa();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _motor?.Dispose();
        _notificacaoSilenciosa?.Dispose();
        base.OnExit(e);
    }

    private static void ParseArgumentos(string[] args)
    {
        if (args.Any(a => a.Equals("-silent", StringComparison.OrdinalIgnoreCase) || a.Equals("/silent", StringComparison.OrdinalIgnoreCase)))
            ModoSilencioso = true;

        foreach (var arg in args)
        {
            if (arg.StartsWith("-profile=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("/profile=", StringComparison.OrdinalIgnoreCase))
            {
                var valor = arg[(arg.IndexOf('=') + 1)..].Trim();
                if (int.TryParse(valor, out var perfilId))
                    PerfilSolicitadoId = perfilId;
            }
        }
    }

    private void ExibirNotificacaoSilenciosa()
    {
        _notificacaoSilenciosa = new WinForms.NotifyIcon
        {
            Visible = true,
            Icon = ObterIconeFavicon(),
            Text = "AppTunnel"
        };

        _notificacaoSilenciosa.ShowBalloonTip(5000, "AppTunnel", "Execução silenciosa ativada.", WinForms.ToolTipIcon.Info);
        _notificacaoSilenciosa.Visible = false;
    }

    private void LancarPerfilPorId(int perfilId, IReadOnlyList<Perfil> perfis, GerenciadorInstancias gerenciador)
    {
        var perfil = perfis.FirstOrDefault(p => p.Id == perfilId);
        if (perfil == null) return;

        try
        {
            var instancia = gerenciador.Lancar(perfil);
            MonitorarSaidaInstancia(instancia.Pid);
        }
        catch { /* falha já registrada na Telemetria */ }
    }

    private void MonitorarSaidaInstancia(uint pid)
    {
        try
        {
            var processo = System.Diagnostics.Process.GetProcessById((int)pid);
            processo.EnableRaisingEvents = true;
            processo.Exited += (_, _) => Dispatcher.Invoke(() =>
            {
                _motor?.Encerrar(pid);
                if (_encerraSemInstancias && _motor?.Instancias.Any() != true)
                    Shutdown();
            });
        }
        catch
        {
            // processo pode já ter encerrado antes de conseguirmos monitorá-lo
        }
    }

   private void MostrarJanela()
    {
        if (_janela == null) return;

        _encerraSemInstancias = false;
        if (_janela.Left <= -10000)
        {
            var area = SystemParameters.WorkArea;
            _janela.WindowStartupLocation = WindowStartupLocation.Manual;
            _janela.Left = area.Left + (area.Width - _janela.Width) / 2;
            _janela.Top = area.Top + (area.Height - _janela.Height) / 2;
        }

        _janela.ShowInTaskbar = true;
        _janela.Show();
        if (_janela.WindowState == WindowState.Minimized)
            _janela.WindowState = WindowState.Normal;
        _janela.Activate();
    }

    internal static Icon ObterIconeFavicon()
    {
        try
        {
            var caminhoIcone = Path.Combine(AppContext.BaseDirectory, "favicon.ico");
            if (File.Exists(caminhoIcone))
                return new Icon(caminhoIcone);
        }
        catch { }

        return SystemIcons.Application;
    }
}
