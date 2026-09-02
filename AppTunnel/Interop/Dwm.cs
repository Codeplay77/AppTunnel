using System.Runtime.InteropServices;

namespace AppTunnel.Interop;

internal static class Dwm
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamanho);

    public static void AtivarTituloEscuro(IntPtr hwnd)
    {
        var ligado = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref ligado, sizeof(int));

        DefinirCor(hwnd, DWMWA_CAPTION_COLOR, 0x16, 0x23, 0x3A);   // CorPainel
        DefinirCor(hwnd, DWMWA_BORDER_COLOR, 0x2E, 0x42, 0x58);    // CorBorda
        DefinirCor(hwnd, DWMWA_TEXT_COLOR, 0xE6, 0xEA, 0xF0);      // CorTextoPrimario
    }

    // COLORREF do Win32 é 0x00BBGGRR — ordem invertida da RGB normal.
    private static void DefinirCor(IntPtr hwnd, int atributo, byte r, byte g, byte b)
    {
        var colorref = (b << 16) | (g << 8) | r;
        DwmSetWindowAttribute(hwnd, atributo, ref colorref, sizeof(int));
    }
}
