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

    internal NovoPerfilWindow(IReadOnlyList<ProxyCfg> proxiesDisponiveis, Perfil? perfilExistente = null)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Dwm.AtivarTituloEscuro(new WindowInteropHelper(this).Handle);

        var itens = proxiesDisponiveis.Select((p, i) => new ItemProxy(i, p)).ToList();
        CmbProxyPrincipal.ItemsSource = itens;
        LstReservas.ItemsSource = itens;

        if (perfilExistente != null)
        {
            Title = "Editar perfil";
            TxtNome.Text = perfilExistente.Nome;
            TxtExe.Text = perfilExistente.Exe;
            TxtArgs.Text = perfilExistente.Args;
            TxtDir.Text = perfilExistente.Dir;
            TxtPassthrough.Text = string.Join(Environment.NewLine, perfilExistente.IpsPassthrough);

            var principal = itens.FirstOrDefault(i => i.Indice == perfilExistente.ProxyPrincipal);
            CmbProxyPrincipal.SelectedItem = principal ?? (itens.Count > 0 ? itens[0] : null);

            foreach (var item in itens)
                if (perfilExistente.Reservas.Contains(item.Indice))
                    LstReservas.SelectedItems.Add(item);
        }
        else if (itens.Count > 0)
        {
            CmbProxyPrincipal.SelectedIndex = 0;
        }
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

        var ipsPassthrough = new List<string>();
        foreach (var linha in TxtPassthrough.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var item = linha.Trim();
            if (item.Length == 0) continue;
            if (!FaixaIp.TentarAnalisar(item, out _))
            {
                MessageBox.Show(this, $"IP de passthrough inválido: \"{item}\".\nUse um IP (1.2.3.4), CIDR (1.2.3.0/24) ou faixa (1.2.3.4-1.2.3.10), com porta opcional (ex.: 1.2.3.4:80 ou 1.2.3.4:80-443).",
                    "AppTunnel", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ipsPassthrough.Add(item);
        }

        Resultado = new Perfil
        {
            Nome = TxtNome.Text.Trim(),
            Exe = TxtExe.Text.Trim(),
            Args = TxtArgs.Text.Trim(),
            Dir = string.IsNullOrWhiteSpace(TxtDir.Text) ? (Path.GetDirectoryName(TxtExe.Text) ?? ".") : TxtDir.Text.Trim(),
            ProxyPrincipal = principal.Indice,
            Reservas = reservas,
            IpsPassthrough = ipsPassthrough,
        };
        DialogResult = true;
    }
}
