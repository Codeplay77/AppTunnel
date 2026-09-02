using AppTunnel.Modelos;

namespace AppTunnel.ViewModels;

internal sealed class ProxyRowVM : ObservableObject
{
    public ProxyCfg Proxy { get; }
    public string HostPorta => $"{Proxy.Host}:{Proxy.Porta}";

    private string _status = "não testado";
    public string Status { get => _status; set => Set(ref _status, value); }

    private long _latenciaMs = -1;
    public long LatenciaMs { get => _latenciaMs; set => Set(ref _latenciaMs, value); }

    private string _emUsoPor = "";
    public string EmUsoPor { get => _emUsoPor; set => Set(ref _emUsoPor, value); }

    public ProxyRowVM(ProxyCfg proxy)
    {
        Proxy = proxy;
    }
}
