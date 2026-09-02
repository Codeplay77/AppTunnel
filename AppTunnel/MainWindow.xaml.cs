using System.Windows;
using System.Windows.Interop;
using AppTunnel.Interop;

namespace AppTunnel;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Dwm.AtivarTituloEscuro(new WindowInteropHelper(this).Handle);
    }
}
