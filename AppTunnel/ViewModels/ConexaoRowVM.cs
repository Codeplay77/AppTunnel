namespace AppTunnel.ViewModels;

internal sealed class ConexaoRowVM : ObservableObject
{
    public ushort PortaLocal { get; }
    public uint Pid { get; }
    public string Destino { get; }

    private string _estado = "";
    public string Estado { get => _estado; set => Set(ref _estado, value); }

    private long _enviados;
    public long Enviados { get => _enviados; set => Set(ref _enviados, value); }

    private long _recebidos;
    public long Recebidos { get => _recebidos; set => Set(ref _recebidos, value); }

    private TimeSpan _duracao;
    public TimeSpan Duracao { get => _duracao; set => Set(ref _duracao, value); }

    private string _proxyUsado = "";
    public string ProxyUsado { get => _proxyUsado; set => Set(ref _proxyUsado, value); }

    public ConexaoRowVM(ushort portaLocal, uint pid, string destino)
    {
        PortaLocal = portaLocal;
        Pid = pid;
        Destino = destino;
    }
}
