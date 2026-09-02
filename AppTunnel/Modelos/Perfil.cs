namespace AppTunnel.Modelos;

internal sealed class Perfil
{
    public required string Nome { get; init; }
    public required string Exe { get; init; }
    public string Args { get; init; } = "";
    public required string Dir { get; init; }
    public int ProxyPrincipal { get; init; }
    public List<int> Reservas { get; init; } = new();
}
