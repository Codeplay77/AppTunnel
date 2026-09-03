using System.Windows;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using AppTunnel.Modelos;
using AppTunnel.ViewModels;

namespace AppTunnel.Views;

public partial class InstanciasView : WpfUserControl
{
    public InstanciasView()
    {
        InitializeComponent();
    }

    private void NovoPerfil_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InstanciasViewModel vm) return;

        var dialogo = new NovoPerfilWindow(vm.ProxiesDisponiveis, vm.Perfis) { Owner = Window.GetWindow(this) };
        if (dialogo.ShowDialog() == true && dialogo.Resultado != null)
            vm.AdicionarPerfil(dialogo.Resultado);
    }

    private void EditarPerfil_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InstanciasViewModel vm) return;
        if (vm.PerfilSelecionado is not Perfil perfilAtual)
        {
            WpfMessageBox.Show(Window.GetWindow(this), "Selecione um perfil para editar.", "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }

        var dialogo = new NovoPerfilWindow(vm.ProxiesDisponiveis, vm.Perfis, perfilAtual) { Owner = Window.GetWindow(this) };
        if (dialogo.ShowDialog() == true && dialogo.Resultado != null)
            vm.AtualizarPerfil(perfilAtual, dialogo.Resultado);
    }

    private void RemoverPerfil_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InstanciasViewModel vm) return;
        if (vm.PerfilSelecionado is not Perfil perfilAtual)
        {
            WpfMessageBox.Show(Window.GetWindow(this), "Selecione um perfil para remover.", "AppTunnel", WpfMessageBoxButton.OK, WpfMessageBoxImage.Warning);
            return;
        }

        var resposta = WpfMessageBox.Show(Window.GetWindow(this), $"Remover o perfil \"{perfilAtual.Nome}\"?",
            "AppTunnel", WpfMessageBoxButton.YesNo, WpfMessageBoxImage.Question);
        if (resposta == WpfMessageBoxResult.Yes)
            vm.RemoverPerfil(perfilAtual);
    }
}
