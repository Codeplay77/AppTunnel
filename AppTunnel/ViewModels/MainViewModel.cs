namespace AppTunnel.ViewModels;

internal sealed class MainViewModel
{
    public InstanciasViewModel Instancias { get; }
    public ConexoesViewModel Conexoes { get; }
    public ProxiesViewModel Proxies { get; }
    public LogViewModel Log { get; }

    public MainViewModel(InstanciasViewModel instancias, ConexoesViewModel conexoes, ProxiesViewModel proxies, LogViewModel log)
    {
        Instancias = instancias;
        Conexoes = conexoes;
        Proxies = proxies;
        Log = log;
    }
}
