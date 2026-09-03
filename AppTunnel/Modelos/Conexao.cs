namespace AppTunnel.Modelos;

internal sealed class Conexao
{
    public required uint Pid { get; init; }
    public required ushort PortaLocal { get; init; }

    // Preenchidos pela CamadaRede a partir do pacote SYN real, não pelo
    // evento CONNECT da CamadaSocket (nem sempre confiável — ver CamadaRede).
    // Host order.
    public uint IpDestino;
    public ushort PortaDestino;

    // true quando IpDestino cai numa faixa de passthrough do perfil — a
    // CamadaRede para de reescrever os pacotes desta conexão.
    public bool Passthrough;

    public readonly long CriadaEm = Environment.TickCount64;
    public long Enviados;
    public long Recebidos;
    public long Marca = Environment.TickCount64;
    public string Estado = "novo"; // novo|conectando|ativa|fechada|erro
}
