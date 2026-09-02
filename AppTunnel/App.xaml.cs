using System.IO;
using System.Windows;
using AppTunnel.Motor;
using AppTunnel.Servicos;
using AppTunnel.ViewModels;

namespace AppTunnel;

public partial class App : Application
{
    private MotorProxy? _motor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
            // Falha aqui é sempre fatal pro motor (WinDivert sem admin, porta
            // 34567 ocupada, etc.) — mostra o motivo em vez de estourar o
            // diálogo padrão de exceção não tratada, e encerra limpo.
            MessageBox.Show(ex.Message, "AppTunnel não conseguiu iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var gerenciador = new GerenciadorInstancias(_motor, proxies, telemetria);

        var vm = new MainViewModel(
            new InstanciasViewModel(_motor, gerenciador, perfis, proxies, caminhoPerfis),
            new ConexoesViewModel(_motor),
            new ProxiesViewModel(_motor, proxies, caminhoProxies, telemetria),
            new LogViewModel(telemetria));

        new MainWindow { DataContext = vm }.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _motor?.Dispose();
        base.OnExit(e);
    }
}
