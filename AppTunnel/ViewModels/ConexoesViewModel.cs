using System.Collections.ObjectModel;
using System.Windows.Threading;
using AppTunnel.Motor;

namespace AppTunnel.ViewModels;

internal sealed class ConexoesViewModel : ObservableObject
{
    private readonly MotorProxy _motor;
    private readonly Dictionary<ushort, ConexaoRowVM> _porPorta = new();

    public ObservableCollection<ConexaoRowVM> Linhas { get; } = new();

    public ConexoesViewModel(MotorProxy motor)
    {
        _motor = motor;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        timer.Tick += (_, _) => Atualizar();
        timer.Start();
    }

    private void Atualizar()
    {
        var instanciasPorPid = _motor.Instancias.ToDictionary(i => i.Pid);
        var vistas = new HashSet<ushort>();

        foreach (var conexao in _motor.Conexoes)
        {
            vistas.Add(conexao.PortaLocal);
            if (!_porPorta.TryGetValue(conexao.PortaLocal, out var linha))
            {
                var destino = $"{FormatarIp(conexao.IpDestino)}:{conexao.PortaDestino}";
                linha = new ConexaoRowVM(conexao.PortaLocal, conexao.Pid, destino);
                _porPorta[conexao.PortaLocal] = linha;
                Linhas.Add(linha);
            }
            linha.Estado = conexao.Estado;
            linha.Enviados = conexao.Enviados;
            linha.Recebidos = conexao.Recebidos;
            linha.Duracao = TimeSpan.FromMilliseconds(Environment.TickCount64 - conexao.CriadaEm);
            linha.ProxyUsado = instanciasPorPid.TryGetValue(conexao.Pid, out var instancia)
                ? $"{instancia.ProxyAtual.Host}:{instancia.ProxyAtual.Porta}"
                : "";
        }

        foreach (var porta in _porPorta.Keys.Except(vistas).ToList())
        {
            Linhas.Remove(_porPorta[porta]);
            _porPorta.Remove(porta);
        }
    }

    private static string FormatarIp(uint v) => $"{(byte)(v >> 24)}.{(byte)(v >> 16)}.{(byte)(v >> 8)}.{(byte)v}";
}
