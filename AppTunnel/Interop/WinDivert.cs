using System.Runtime.InteropServices;

namespace AppTunnel.Interop;

// Underlying type default (int) casa com o enum C WINDIVERT_LAYER, que é
// passado por valor em WinDivertOpen — não reduzir para byte.
internal enum CamadaWinDivert
{
    Rede = 0,
    RedeEncaminhada = 1,
    Fluxo = 2,
    Socket = 3,
    Reflexo = 4,
}

// Usado só para leitura do byte Event do WINDIVERT_ADDRESS (offset 9), nunca
// passado por P/Invoke — o tamanho do underlying type aqui é irrelevante.
internal enum EventoWinDivert
{
    PacoteRede = 0,
    FluxoEstabelecido = 1,
    FluxoRemovido = 2,
    SocketBind = 3,
    SocketConnect = 4,
    SocketListen = 5,
    SocketAccept = 6,
    SocketClose = 7,
    ReflexoAbriu = 8,
    ReflexoFechou = 9,
}

internal static class FlagWinDivert
{
    public const ulong Sniff = 0x0001;
    public const ulong Drop = 0x0002;
    public const ulong RecvOnly = 0x0004;
    public const ulong SendOnly = 0x0008;
    public const ulong NoInstall = 0x0010;
    public const ulong Fragments = 0x0020;
}

internal enum ComoWinDivertShutdown
{
    Recv = 0x1,
    Send = 0x2,
    Both = 0x3,
}

internal static class WinDivert
{
    private const string Dll = "WinDivert.dll";

    [DllImport(Dll, SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr WinDivertOpen(string filtro, CamadaWinDivert camada, short prioridade, ulong flags);

    // pacote e endereco são buffers fixos: WinDivertRecv preenche até o tamanho informado.
    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertRecv(IntPtr handle, byte[]? pacote, uint tamanhoPacote, out uint tamanhoRecebido, byte[] endereco);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertSend(IntPtr handle, byte[]? pacote, uint tamanhoPacote, out uint tamanhoEnviado, byte[] endereco);

    // Desbloqueia um WinDivertRecv pendente em outra thread (fila esvazia e o
    // Recv retorna com ERROR_NO_DATA) — fechar o handle direto enquanto há
    // I/O síncrono pendente nele não é seguro. Chamar antes do Close.
    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertShutdown(IntPtr handle, ComoWinDivertShutdown como);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertClose(IntPtr handle);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WinDivertHelperCalcChecksums(byte[] pacote, uint tamanhoPacote, byte[]? endereco, ulong flags);
}
