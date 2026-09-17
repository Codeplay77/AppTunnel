using System.IO;
using System.IO.Pipes;

namespace AppTunnel.Servicos;

// Garante uma única instância do AppTunnel.exe dona do MotorProxy por sessão
// do Windows. Motivo: CamadaRede abre um handle WinDivert na camada NETWORK
// com prioridade fixa (0) e filtro não exclusivo (casa todo TCP outbound da
// máquina) — a própria doc do WinDivert diz que um pacote só é entregue a UM
// handle por nível de prioridade nesse caso. Duas instâncias concorrentes
// brigam pelo mesmo tráfego e uma delas simplesmente para de tunelar, então
// só a primeira ("hub") pode abrir o MotorProxy; as demais só repassam o
// pedido por esse pipe e encerram.
internal static class HubComunicacao
{
    private const string NomePipe = "AppTunnel.Hub";
    private const int TimeoutConexaoMs = 300;
    private const int MaxInstanciasPipe = 32;

    // Tenta repassar o pedido (lançar perfil e/ou mostrar janela) para um hub
    // já em execução. Retorna true se conseguiu falar com um hub — quem
    // chamou deve encerrar o processo em seguida. Retorna false quando não
    // há hub ouvindo, e este processo deve virar o hub.
    public static bool TentarDelegar(int? perfilId, bool mostrarJanela)
    {
        try
        {
            using var cliente = new NamedPipeClientStream(".", NomePipe, PipeDirection.Out);
            cliente.Connect(TimeoutConexaoMs);
            using var escritor = new StreamWriter(cliente) { AutoFlush = true };
            if (perfilId.HasValue)
                escritor.WriteLine($"LANCAR:{perfilId.Value}");
            if (mostrarJanela)
                escritor.WriteLine("MOSTRAR");
            return true;
        }
        catch (Exception)
        {
            // Qualquer falha aqui (pipe inexistente, timeout, ocupado demais)
            // significa "não há hub alcançável" — o chamador vira o hub.
            return false;
        }
    }

    // Só deve ser chamado pelo processo que virou o hub. Roda em background
    // pela vida inteira do processo; cada conexão é um lançamento (-silent ou
    // GUI) que manda 0-2 linhas e fecha.
    public static void IniciarServidor(Action<int> aoLancar, Action aoMostrar)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                NamedPipeServerStream servidor;
                try
                {
                    servidor = new NamedPipeServerStream(NomePipe, PipeDirection.In, MaxInstanciasPipe);
                    await servidor.WaitForConnectionAsync();
                }
                catch
                {
                    return; // pipe encerrado junto com o processo (Dispose do hub)
                }
                _ = AtenderClienteAsync(servidor, aoLancar, aoMostrar);
            }
        });
    }

    private static async Task AtenderClienteAsync(NamedPipeServerStream servidor, Action<int> aoLancar, Action aoMostrar)
    {
        using var _ = servidor;
        try
        {
            using var leitor = new StreamReader(servidor);
            string? linha;
            while ((linha = await leitor.ReadLineAsync()) != null)
            {
                if (linha.StartsWith("LANCAR:", StringComparison.Ordinal) && int.TryParse(linha["LANCAR:".Length..], out var id))
                    aoLancar(id);
                else if (linha == "MOSTRAR")
                    aoMostrar();
            }
        }
        catch
        {
            // cliente desconectou no meio da leitura — ignora, era um pedido só
        }
    }
}
