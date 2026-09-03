using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using AppTunnel.Modelos;
using AppTunnel.Motor;
using AppTunnel.Servicos;

namespace AppTunnel.ViewModels;

internal sealed class InstanciasViewModel : ObservableObject
{
    private readonly MotorProxy _motor;
    private readonly GerenciadorInstancias _gerenciador;
    private readonly IReadOnlyList<ProxyCfg> _proxies;
    private readonly string _caminhoPerfis;
    private readonly Dictionary<uint, InstanciaRowVM> _porPid = new();

    public ObservableCollection<InstanciaRowVM> Linhas { get; } = new();
    public ObservableCollection<Perfil> Perfis { get; }

    // Snapshot pro diálogo "Novo perfil" montar os combos de proxy — não
    // precisa ser ao vivo, o diálogo é aberto e fechado numa tacada só.
    public IReadOnlyList<ProxyCfg> ProxiesDisponiveis => _proxies;

    private Perfil? _perfilSelecionado;
    public Perfil? PerfilSelecionado { get => _perfilSelecionado; set => Set(ref _perfilSelecionado, value); }

    private InstanciaRowVM? _linhaSelecionada;
    public InstanciaRowVM? LinhaSelecionada { get => _linhaSelecionada; set => Set(ref _linhaSelecionada, value); }

    public RelayCommand ComandoLancar { get; }
    public RelayCommand ComandoEncerrar { get; }

    public InstanciasViewModel(MotorProxy motor, GerenciadorInstancias gerenciador, IReadOnlyList<Perfil> perfis, IReadOnlyList<ProxyCfg> proxies, string caminhoPerfis)
    {
        _motor = motor;
        _gerenciador = gerenciador;
        _proxies = proxies;
        _caminhoPerfis = caminhoPerfis;
        Perfis = new ObservableCollection<Perfil>(perfis);

        ComandoLancar = new RelayCommand(_ => Lancar(), _ => PerfilSelecionado != null);
        ComandoEncerrar = new RelayCommand(_ => Encerrar(), _ => LinhaSelecionada != null);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => Atualizar();
        timer.Start();
    }

    public void AdicionarPerfil(Perfil perfil)
    {
        Perfis.Add(perfil);
        SalvarPerfis("adicionado");
    }

    public void AtualizarPerfil(Perfil antigo, Perfil novo)
    {
        var indice = Perfis.IndexOf(antigo);
        if (indice < 0) return;

        Perfis[indice] = novo;
        if (ReferenceEquals(PerfilSelecionado, antigo)) PerfilSelecionado = novo;
        SalvarPerfis("atualizado");
    }

    public void RemoverPerfil(Perfil perfil)
    {
        if (!Perfis.Remove(perfil)) return;

        if (ReferenceEquals(PerfilSelecionado, perfil)) PerfilSelecionado = null;
        SalvarPerfis("removido");
    }

    private void SalvarPerfis(string acao)
    {
        try
        {
            AppTunnel.Servicos.Perfis.Salvar(_caminhoPerfis, Perfis.ToList());
        }
        catch (Exception ex)
        {
            // A coleção em memória já reflete a mudança mesmo que gravar em
            // disco falhe — mas isso nunca pode falhar em silêncio, senão o
            // usuário perde a alteração no próximo reinício sem saber por quê.
            System.Windows.MessageBox.Show(
                $"Perfil {acao}, mas não consegui salvar em '{_caminhoPerfis}':\n{ex.Message}",
                "AppTunnel", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
    }

    private void Lancar()
    {
        if (PerfilSelecionado == null) return;
        try { _gerenciador.Lancar(PerfilSelecionado); }
        catch { /* falha já registrada na Telemetria */ }
    }

    private void Encerrar()
    {
        if (LinhaSelecionada != null)
            _motor.Encerrar(LinhaSelecionada.Pid);
    }

    private void Atualizar()
    {
        var vistos = new HashSet<uint>();
        foreach (var instancia in _motor.Instancias)
        {
            vistos.Add(instancia.Pid);
            if (!_porPid.TryGetValue(instancia.Pid, out var linha))
            {
                linha = new InstanciaRowVM(instancia.Pid, instancia.Nome);
                _porPid[instancia.Pid] = linha;
                Linhas.Add(linha);
            }
            linha.ProxyAtual = $"{instancia.ProxyAtual.Host}:{instancia.ProxyAtual.Porta}";
            linha.ConexoesAtivas = instancia.ConexoesAtivas;
            linha.Falhas = instancia.Falhas;
        }

        // Instâncias removidas de _motor (Encerrar já as tira do dicionário) somem daqui.
        foreach (var pid in _porPid.Keys.Except(vistos).ToList())
        {
            Linhas.Remove(_porPid[pid]);
            _porPid.Remove(pid);
        }
    }
}
