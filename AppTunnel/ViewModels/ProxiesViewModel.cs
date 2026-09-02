using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using AppTunnel.Modelos;
using AppTunnel.Motor;
using AppTunnel.Servicos;

namespace AppTunnel.ViewModels;

internal sealed class ProxiesViewModel : ObservableObject
{
    private static readonly TimeSpan TempoLimiteTeste = TimeSpan.FromSeconds(5);

    private readonly MotorProxy _motor;
    private readonly List<ProxyCfg> _proxies; // mesma instância compartilhada com GerenciadorInstancias
    private readonly string _caminhoArquivo;
    private readonly Telemetria _telemetria;
    private bool _testando;

    public ObservableCollection<ProxyRowVM> Linhas { get; }
    public RelayCommand ComandoTestarTodos { get; }
    public RelayCommand ComandoSalvarLista { get; }

    private string _textoLista;
    public string TextoLista { get => _textoLista; set => Set(ref _textoLista, value); }

    public ProxiesViewModel(MotorProxy motor, List<ProxyCfg> proxies, string caminhoArquivo, Telemetria telemetria)
    {
        _motor = motor;
        _proxies = proxies;
        _caminhoArquivo = caminhoArquivo;
        _telemetria = telemetria;

        Linhas = new ObservableCollection<ProxyRowVM>(proxies.Select(p => new ProxyRowVM(p)));
        _textoLista = RepositorioProxies.FormatarTexto(proxies);

        ComandoTestarTodos = new RelayCommand(async _ => await TestarTodosAsync(), _ => !_testando);
        ComandoSalvarLista = new RelayCommand(_ => SalvarLista());

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => AtualizarEmUso();
        timer.Start();
    }

    private void SalvarLista()
    {
        var novos = RepositorioProxies.ParsearTexto(TextoLista);
        RepositorioProxies.Salvar(_caminhoArquivo, novos);

        // Muta a lista compartilhada em vez de trocar a referência: perfis já
        // carregados apontam pra proxies por índice nessa mesma instância.
        _proxies.Clear();
        _proxies.AddRange(novos);

        Linhas.Clear();
        foreach (var p in novos) Linhas.Add(new ProxyRowVM(p));

        _telemetria.Registrar(NivelLog.Info, $"{novos.Count} proxies salvos em {_caminhoArquivo}");
    }

    private void AtualizarEmUso()
    {
        var emUso = _motor.Instancias.ToDictionary(i => i.ProxyAtual, i => i.Nome);
        foreach (var linha in Linhas)
            linha.EmUsoPor = emUso.TryGetValue(linha.Proxy, out var nome) ? nome : "";
    }

    private async Task TestarTodosAsync()
    {
        _testando = true;
        try
        {
            // Limita concorrência para não abrir centenas de sockets de uma vez.
            using var portao = new SemaphoreSlim(10);
            var tarefas = Linhas.Select(async linha =>
            {
                await portao.WaitAsync();
                try
                {
                    linha.Status = "testando...";
                    var cron = Stopwatch.StartNew();
                    var ok = await RepositorioProxies.TestarAsync(linha.Proxy, TempoLimiteTeste);
                    cron.Stop();
                    linha.Status = ok ? "ok" : "falhou";
                    linha.LatenciaMs = ok ? cron.ElapsedMilliseconds : -1;
                }
                finally
                {
                    portao.Release();
                }
            });
            await Task.WhenAll(tarefas);
        }
        finally
        {
            _testando = false;
        }
    }
}
