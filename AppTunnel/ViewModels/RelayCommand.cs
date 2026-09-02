using System.Windows.Input;

namespace AppTunnel.ViewModels;

internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _executar;
    private readonly Func<object?, bool>? _podeExecutar;

    public RelayCommand(Action<object?> executar, Func<object?, bool>? podeExecutar = null)
    {
        _executar = executar;
        _podeExecutar = podeExecutar;
    }

    public bool CanExecute(object? parametro) => _podeExecutar?.Invoke(parametro) ?? true;
    public void Execute(object? parametro) => _executar(parametro);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
