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
//
// Quem é o hub é decidido por um Mutex nomeado — atômico e instantâneo. Uma
// versão anterior decidia isso tentando conectar no pipe com timeout curto
// e virava hub quando a conexão falhava; isso tinha uma corrida real: em
// builds onefile self-contained o cold start (extração dos nativos do
// runtime empacotado, primeira instalação do driver WinDivert) é bem mais
// lento que num build normal, e duas instâncias lançadas quase juntas podiam
// achar as duas que "não tem hub" antes de qualquer uma abrir o pipe —
// resultando em dois hubs concorrentes.
internal static class HubComunicacao
{
    private const string NomeMutex = "AppTunnel.HubMutex";
    private const string NomePipe = "AppTunnel.Hub";
    // Generoso de propósito: o hub pode demorar entre vencer o mutex e abrir
    // o pipe (driver WinDivert, cold start onefile) — o cliente já sabe que
    // existe um hub (via mutex), então vale esperar em vez de desistir cedo.
    private const int TimeoutConexaoMs = 15000;

    private static Mutex? _mutexHub;

    // Decide atomicamente, sem nenhum IPC, se este processo é o hub. Deve ser
    // a primeira coisa chamada no startup, antes de qualquer inicialização
    // lenta — a janela de corrida do design antigo era exatamente esse
    // intervalo. Nunca libera o mutex: ele marca "hub vivo" até o processo
    // encerrar, quando o Windows o libera sozinho.
    public static bool TornarSeHub()
    {
        _mutexHub = new Mutex(initiallyOwned: true, NomeMutex, out var criadoAgora);
        return criadoAgora;
    }

    // Só deve ser chamado quando TornarSeHub() já disse que existe um hub.
    // Repassa o pedido (lançar perfil e/ou mostrar janela) esperando o hub
    // abrir o pipe. Retorna false só se o hub morreu antes disso (ex.:
    // WinDivertOpen falhou e ele já mostrou o próprio erro) — nesse caso não
    // há o que fazer além de encerrar.
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
                    servidor = new NamedPipeServerStream(NomePipe, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances);
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
