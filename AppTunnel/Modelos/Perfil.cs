namespace AppTunnel.Modelos;

internal sealed class Perfil
{
    public int Id { get; set; }
    public required string Nome { get; init; }
    public required string Exe { get; init; }
    public string Args { get; init; } = "";
    public required string Dir { get; init; }
    public int ProxyPrincipal { get; init; }
    public List<int> Reservas { get; init; } = new();

    // IP único, CIDR ("x.x.x.x/n") ou faixa ("x.x.x.x-y.y.y.y") por item —
    // ver FaixaIp.TentarAnalisar. Tráfego pra esses destinos ignora o proxy.
    public List<string> IpsPassthrough { get; init; } = new();
}
