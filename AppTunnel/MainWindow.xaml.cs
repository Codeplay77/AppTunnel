using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;
using System.Windows.Interop;
using AppTunnel.Interop;

namespace AppTunnel;

public partial class MainWindow : Window
{
    private WinForms.NotifyIcon? _trayIcon;
    private bool _fechamentoForcado;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Dwm.AtivarTituloEscuro(new WindowInteropHelper(this).Handle);
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (App.ModoSilencioso || _fechamentoForcado)
        {
            e.Cancel = false;
            return;
        }

        e.Cancel = true;
        OcultarNaBandeja();
    }

    private void OcultarNaBandeja()
    {
        if (_trayIcon == null)
            InicializarTrayIcon();

        Hide();
        _trayIcon!.Visible = true;
        _trayIcon.ShowBalloonTip(2500, "AppTunnel", "Aplicação minimizada para a bandeja do sistema.", WinForms.ToolTipIcon.Info);
    }

    private void RestaurarJanela()
    {
        if (_trayIcon != null)
            _trayIcon.Visible = false;

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void InicializarTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();

        var itemRestaurar = new WinForms.ToolStripMenuItem("Restaurar janela");
        itemRestaurar.Click += (_, _) => RestoreWindow();

        var itemSair = new WinForms.ToolStripMenuItem("Sair");
        itemSair.Click += (_, _) =>
        {
            // marca antes do Shutdown pois ele dispara o Closing desta janela,
            // e sem a flag ele cairia no mesmo fluxo de "ocultar na bandeja"
            _fechamentoForcado = true;
            System.Windows.Application.Current.Shutdown();
        };

        menu.Items.Add(itemRestaurar);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(itemSair);

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = App.ObterIconeFavicon(),
            Text = "AppTunnel",
            ContextMenuStrip = menu,
            Visible = false
        };

        _trayIcon.DoubleClick += (_, _) => RestoreWindow();
    }

    private void RestoreWindow()
    {
        if (IsVisible)
        {
            WindowState = WindowState.Normal;
            Activate();
        }
        else
        {
            RestaurarJanela();
        }
    }
}
