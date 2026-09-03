using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;
using WpfOpenFolderDialog = Microsoft.Win32.OpenFolderDialog;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using AppTunnel.Interop;
using AppTunnel.Modelos;
using AppTunnel.Servicos;

namespace AppTunnel.Views;

public partial class NovoPerfilWindow : Window
{
    private sealed record ItemProxy(int Indice, ProxyCfg Proxy)
    {
        public string Rotulo => $"{Indice}: {Proxy.Host}:{Proxy.Porta}";
    }

    internal Perfil? Resultado { get; private set; }

    private readonly IReadOnlyList<Perfil> _perfisExistentes;
    private readonly int? _idOriginal;

    internal NovoPerfilWindow(IReadOnlyList<ProxyCfg> proxiesDisponiveis, IReadOnlyList<Perfil> perfisExistentes, Perfil? perfilExistente = null)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Dwm.AtivarTituloEscuro(new WindowInteropHelper(this).Handle);

        _perfisExistentes = perfisExistentes;
        _idOriginal = perfilExistente?.Id;

        var itens = proxiesDisponiveis.Select((p, i) => new ItemProxy(i, p)).ToList();
        CmbProxyPrincipal.ItemsSource = itens;
        LstReservas.ItemsSource = itens;

        if (perfilExistente != null)
        {
            Title = "Editar perfil";
            TxtId.Text = perfilExistente.Id.ToString();
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
        else
        {
            TxtId.Text = Perfis.ProximoId(perfisExistentes).ToString();
            if (itens.Count > 0)
                CmbProxyPrincipal.SelectedIndex = 0;
        }
    }

    private void ProcurarExe_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new WpfOpenFileDialog { Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*" };
        if (dialogo.ShowDialog(this) == true)
        {
            TxtExe.Text = dialogo.FileName;
            if (string.IsNullOrWhiteSpace(TxtDir.Text))
                TxtDir.Text = Path.GetDirectoryName(dialogo.FileName) ?? "";
        }
    }

    private void ProcurarDir_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new WpfOpenFolderDialog();
        if (dialogo.ShowDialog(this) == true)
            TxtDir.Text = dialogo.FolderName;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(TxtId.Text.Trim(), out var idInformado) || idInformado <= 0)
        {
            WpfMessageBox.Show(this, "Informe um ID de perfil válido (número inteiro maior que zero).", "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }
        if (idInformado != _idOriginal && _perfisExistentes.Any(p => p.Id == idInformado))
        {
            WpfMessageBox.Show(this, $"Já existe um perfil com o ID {idInformado}.", "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(TxtNome.Text) || string.IsNullOrWhiteSpace(TxtExe.Text))
        {
            WpfMessageBox.Show(this, "Nome e executável são obrigatórios.", "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }
        if (CmbProxyPrincipal.SelectedItem is not ItemProxy principal)
        {
            WpfMessageBox.Show(this, "Selecione um proxy principal.", "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
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
                WpfMessageBox.Show(this, $"IP de passthrough inválido: \"{item}\".\nUse um IP (1.2.3.4), CIDR (1.2.3.0/24) ou faixa (1.2.3.4-1.2.3.10), com porta opcional (ex.: 1.2.3.4:80 ou 1.2.3.4:80-443).",
                    "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
                return;
            }
            ipsPassthrough.Add(item);
        }

        Resultado = new Perfil
        {
            Id = idInformado,
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
