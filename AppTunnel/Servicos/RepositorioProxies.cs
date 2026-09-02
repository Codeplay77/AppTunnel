using System.IO;
using System.Linq;
using System.Net.Sockets;
using AppTunnel.Modelos;
using AppTunnel.Motor;

namespace AppTunnel.Servicos;

internal static class RepositorioProxies
{
    // Formato Webshare: host:porta:usuario:senha. Split em no máximo 4 partes —
    // a senha pode conter ':'.
    public static List<ProxyCfg> Carregar(string caminho)
    {
        return File.Exists(caminho) ? ParsearTexto(File.ReadAllText(caminho)) : new List<ProxyCfg>();
    }

    public static List<ProxyCfg> ParsearTexto(string texto)
    {
        var resultado = new List<ProxyCfg>();
        foreach (var linhaBruta in texto.Split('\n'))
        {
            var linha = linhaBruta.Trim().TrimEnd('\r');
            if (linha.Length == 0 || linha.StartsWith('#')) continue;

            var partes = linha.Split(':', 4);
            if (partes.Length < 2 || !int.TryParse(partes[1], out var porta))
                continue; // linha malformada, ignora

            var usuario = partes.Length > 2 ? partes[2] : null;
            var senha = partes.Length > 3 ? partes[3] : null;
            resultado.Add(new ProxyCfg(partes[0], porta, usuario, senha));
        }
        return resultado;
    }

    public static string FormatarTexto(IEnumerable<ProxyCfg> proxies) =>
        string.Join(Environment.NewLine, proxies.Select(FormatarLinha));

    private static string FormatarLinha(ProxyCfg p) =>
        p.Usuario != null ? $"{p.Host}:{p.Porta}:{p.Usuario}:{p.Senha}" : $"{p.Host}:{p.Porta}";

    public static void Salvar(string caminho, IEnumerable<ProxyCfg> proxies)
    {
        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio)) Directory.CreateDirectory(diretorio);
        File.WriteAllText(caminho, FormatarTexto(proxies));
    }

    // Valida na inicialização: conecta e faz só a negociação/autenticação
    // SOCKS5, sem CONNECT a um destino real.
    public static async Task<bool> TestarAsync(ProxyCfg proxy, TimeSpan tempoLimite)
    {
        using var cts = new CancellationTokenSource(tempoLimite);
        try
        {
            using var cliente = new TcpClient();
            await cliente.ConnectAsync(proxy.Host, proxy.Porta, cts.Token);
            using var fluxo = cliente.GetStream();
            await ClienteSocks5.NegociarAsync(fluxo, proxy, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
