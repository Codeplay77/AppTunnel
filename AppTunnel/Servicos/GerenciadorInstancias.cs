using AppTunnel.Modelos;
using AppTunnel.Motor;

namespace AppTunnel.Servicos;

// Resolve Perfil -> lista de ProxyCfg (principal + reservas, por índice na
// lista global) e lança via MotorProxy. Nenhuma lógica de failover aqui —
// isso é responsabilidade do MotorProxy (invariante 4: só avança o índice
// com Ativas == 0).
internal sealed class GerenciadorInstancias
{
    private readonly MotorProxy _motor;
    private readonly IReadOnlyList<ProxyCfg> _proxies;
    private readonly Telemetria _telemetria;

    public GerenciadorInstancias(MotorProxy motor, IReadOnlyList<ProxyCfg> proxies, Telemetria telemetria)
    {
        _motor = motor;
        _proxies = proxies;
        _telemetria = telemetria;
    }

    public Instancia Lancar(Perfil perfil)
    {
        var lista = new List<ProxyCfg> { ResolverProxy(perfil.ProxyPrincipal) };
        foreach (var indice in perfil.Reservas)
            lista.Add(ResolverProxy(indice));

        var instancia = _motor.Lancar(perfil.Exe, perfil.Args, perfil.Dir, perfil.Nome, lista);
        _telemetria.Registrar(NivelLog.Info, $"Instância '{perfil.Nome}' lançada, PID={instancia.Pid}, proxy={instancia.ProxyAtual.Host}:{instancia.ProxyAtual.Porta}");
        return instancia;
    }

    private ProxyCfg ResolverProxy(int indice)
    {
        if (indice < 0 || indice >= _proxies.Count)
            throw new ArgumentOutOfRangeException(nameof(indice), $"Índice de proxy {indice} fora da lista carregada ({_proxies.Count} proxies)");
        return _proxies[indice];
    }
}
