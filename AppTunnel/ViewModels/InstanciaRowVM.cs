namespace AppTunnel.ViewModels;

internal sealed class InstanciaRowVM : ObservableObject
{
    public uint Pid { get; }
    public string Nome { get; }

    private string _proxyAtual = "";
    public string ProxyAtual { get => _proxyAtual; set => Set(ref _proxyAtual, value); }

    private int _conexoesAtivas;
    public int ConexoesAtivas { get => _conexoesAtivas; set => Set(ref _conexoesAtivas, value); }

    private int _falhas;
    public int Falhas { get => _falhas; set => Set(ref _falhas, value); }

    public InstanciaRowVM(uint pid, string nome)
    {
        Pid = pid;
        Nome = nome;
    }
}
