using System.Windows;
using System.Windows.Controls;
using AppTunnel.Modelos;
using AppTunnel.ViewModels;

namespace AppTunnel.Views;

public partial class InstanciasView : UserControl
{
    public InstanciasView()
    {
        InitializeComponent();
    }

    private void NovoPerfil_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InstanciasViewModel vm) return;

        var dialogo = new NovoPerfilWindow(vm.ProxiesDisponiveis) { Owner = Window.GetWindow(this) };
        if (dialogo.ShowDialog() == true && dialogo.Resultado != null)
            vm.AdicionarPerfil(dialogo.Resultado);
    }

    private void EditarPerfil_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InstanciasViewModel vm) return;
        if (vm.PerfilSelecionado is not Perfil perfilAtual)
        {
            MessageBox.Show(Window.GetWindow(this), "Selecione um perfil para editar.", "AppTunnel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialogo = new NovoPerfilWindow(vm.ProxiesDisponiveis, perfilAtual) { Owner = Window.GetWindow(this) };
        if (dialogo.ShowDialog() == true && dialogo.Resultado != null)
            vm.AtualizarPerfil(perfilAtual, dialogo.Resultado);
    }

    private void RemoverPerfil_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not InstanciasViewModel vm) return;
        if (vm.PerfilSelecionado is not Perfil perfilAtual)
        {
            MessageBox.Show(Window.GetWindow(this), "Selecione um perfil para remover.", "AppTunnel", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var resposta = MessageBox.Show(Window.GetWindow(this), $"Remover o perfil \"{perfilAtual.Nome}\"?",
            "AppTunnel", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (resposta == MessageBoxResult.Yes)
            vm.RemoverPerfil(perfilAtual);
    }
}
