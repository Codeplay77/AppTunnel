using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using AppTunnel.Interop;
using AppTunnel.Modelos;

namespace AppTunnel.Views;

public partial class NovoPerfilWindow : Window
{
    private sealed record ItemProxy(int Indice, ProxyCfg Proxy)
    {
        public string Rotulo => $"{Indice}: {Proxy.Host}:{Proxy.Porta}";
    }

    internal Perfil? Resultado { get; private set; }

    internal NovoPerfilWindow(IReadOnlyList<ProxyCfg> proxiesDisponiveis)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Dwm.AtivarTituloEscuro(new WindowInteropHelper(this).Handle);

        var itens = proxiesDisponiveis.Select((p, i) => new ItemProxy(i, p)).ToList();
        CmbProxyPrincipal.ItemsSource = itens;
        LstReservas.ItemsSource = itens;
        if (itens.Count > 0) CmbProxyPrincipal.SelectedIndex = 0;
    }

    private void ProcurarExe_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog { Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*" };
        if (dialogo.ShowDialog(this) == true)
        {
            TxtExe.Text = dialogo.FileName;
            if (string.IsNullOrWhiteSpace(TxtDir.Text))
                TxtDir.Text = Path.GetDirectoryName(dialogo.FileName) ?? "";
        }
    }

    private void ProcurarDir_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog();
        if (dialogo.ShowDialog(this) == true)
            TxtDir.Text = dialogo.FolderName;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNome.Text) || string.IsNullOrWhiteSpace(TxtExe.Text))
        {
            MessageBox.Show(this, "Nome e executável são obrigatórios.", "AppTunnel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (CmbProxyPrincipal.SelectedItem is not ItemProxy principal)
        {
            MessageBox.Show(this, "Selecione um proxy principal.", "AppTunnel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var reservas = LstReservas.SelectedItems.Cast<ItemProxy>()
            .Where(i => i.Indice != principal.Indice)
            .Select(i => i.Indice)
            .ToList();

        Resultado = new Perfil
        {
            Nome = TxtNome.Text.Trim(),
            Exe = TxtExe.Text.Trim(),
            Args = TxtArgs.Text.Trim(),
            Dir = string.IsNullOrWhiteSpace(TxtDir.Text) ? (Path.GetDirectoryName(TxtExe.Text) ?? ".") : TxtDir.Text.Trim(),
            ProxyPrincipal = principal.Indice,
            Reservas = reservas,
        };
        DialogResult = true;
    }
}
