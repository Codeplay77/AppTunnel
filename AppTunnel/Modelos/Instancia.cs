namespace AppTunnel.Modelos;

internal sealed class Instancia
{
    public required uint Pid { get; init; }
    public required string Nome { get; init; }
    public required IntPtr HandleProcesso { get; init; }
    public required IReadOnlyList<ProxyCfg> Proxies { get; init; }

    public int Indice; // Proxies[Indice] = proxy ativo
    public int ConexoesAtivas;
    public int Falhas;
    public volatile bool Viva = true;

    // Setado por RelaySocks5 quando uma conexão termina em erro. Consumido
    // pela varredura do MotorProxy, que só avança Indice com ConexoesAtivas == 0
    // (invariante 4: não trocar de IP no meio de uma sessão).
    public volatile bool PendenteFailover;

    public ProxyCfg ProxyAtual => Proxies[Indice];
}
