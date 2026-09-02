using System.Windows;
using System.Windows.Controls;
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
}
