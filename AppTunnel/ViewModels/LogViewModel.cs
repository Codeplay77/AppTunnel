using System.Collections.ObjectModel;
using System.Windows.Threading;
using AppTunnel.Servicos;

namespace AppTunnel.ViewModels;

internal sealed class LogViewModel : ObservableObject
{
    private readonly Telemetria _telemetria;
    private long _ultimoIdVisto;

    public ObservableCollection<EntradaLog> Linhas { get; } = new();

    public LogViewModel(Telemetria telemetria)
    {
        _telemetria = telemetria;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => Atualizar();
        timer.Start();
    }

    private void Atualizar()
    {
        foreach (var entrada in _telemetria.Entradas.Where(e => e.Id > _ultimoIdVisto))
            Linhas.Add(entrada);
        if (Linhas.Count > 0)
            _ultimoIdVisto = Linhas[^1].Id;
    }
}
