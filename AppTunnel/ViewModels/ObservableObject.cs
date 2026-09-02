using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AppTunnel.ViewModels;

internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T campo, T valor, [CallerMemberName] string? nome = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
        campo = valor;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
        return true;
    }
}
