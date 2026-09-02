namespace AppTunnel.Interop;

internal static class WinDivertAddress
{
    public const int Tamanho = 80;

    public static CamadaWinDivert Camada(byte[] endereco) => (CamadaWinDivert)endereco[8];
    public static EventoWinDivert Evento(byte[] endereco) => (EventoWinDivert)endereco[9];

    public static bool Outbound(byte[] endereco) => (endereco[10] & 0x02) != 0;
    public static bool Loopback(byte[] endereco) => (endereco[10] & 0x04) != 0;
    public static bool IPv6(byte[] endereco) => (endereco[10] & 0x10) != 0;

    public static uint ProcessId(byte[] endereco) => BitConverter.ToUInt32(endereco, 32);

    // Socket.LocalAddr/RemoteAddr são UINT32[4] (endereço IPv4-mapeado em IPv6);
    // o valor IPv4 vive na última palavra do array, índice 3.
    public static uint EnderecoLocalV4(byte[] endereco) => BitConverter.ToUInt32(endereco, 48);
    public static uint EnderecoRemotoV4(byte[] endereco) => BitConverter.ToUInt32(endereco, 64);

    public static ushort PortaLocal(byte[] endereco) => BitConverter.ToUInt16(endereco, 68);
    public static ushort PortaRemota(byte[] endereco) => BitConverter.ToUInt16(endereco, 70);

    public static byte Protocolo(byte[] endereco) => endereco[72];
}
