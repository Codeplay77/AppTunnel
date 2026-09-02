using System.Collections.Concurrent;
using System.IO;

namespace AppTunnel.Servicos;

internal enum NivelLog { Info, Aviso, Erro }

// Id é monotônico e sobrevive ao descarte de entradas antigas — é o que a
// LogView usa para saber o que já mostrou (índice de lista não serve: desloca
// quando a fila estoura a capacidade e descarta pela frente).
internal sealed record EntradaLog(long Id, DateTime Quando, NivelLog Nivel, string Mensagem);

// Log em memória para a LogView (polling) + arquivo em disco. O arquivo
// existe porque, se a rede da máquina cair por causa do motor, o processo
// pode precisar ser morto pelo Gerenciador de Tarefas antes de dar tempo de
// abrir a LogView — sem ele o diagnóstico se perde.
internal sealed class Telemetria
{
    private const int Capacidade = 2000;
    private readonly ConcurrentQueue<EntradaLog> _entradas = new();
    private readonly string _arquivoLog;
    private readonly object _travaArquivo = new();
    private long _proximoId;

    public Telemetria(string? arquivoLog = null)
    {
        _arquivoLog = arquivoLog ?? Path.Combine("config", "roproxy.log");
    }

    public void Registrar(NivelLog nivel, string mensagem)
    {
        var entrada = new EntradaLog(Interlocked.Increment(ref _proximoId), DateTime.Now, nivel, mensagem);
        _entradas.Enqueue(entrada);
        while (_entradas.Count > Capacidade && _entradas.TryDequeue(out _)) { }

        try
        {
            lock (_travaArquivo)
            {
                File.AppendAllText(_arquivoLog,
                    $"{entrada.Quando:HH:mm:ss.fff} [{entrada.Nivel}] {entrada.Mensagem}{Environment.NewLine}");
            }
        }
        catch
        {
            // Log em arquivo é best-effort — nunca pode virar exceção nas
            // threads do motor.
        }
    }

    public IEnumerable<EntradaLog> Entradas => _entradas;
}
