using System.IO;
using System.Text.Json;
using AppTunnel.Modelos;

namespace AppTunnel.Servicos;

internal static class Perfis
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class Raiz
    {
        public List<Perfil> Perfis { get; set; } = new();
    }

    public static List<Perfil> Carregar(string caminho)
    {
        if (!File.Exists(caminho)) return new List<Perfil>();
        var json = File.ReadAllText(caminho);
        var raiz = JsonSerializer.Deserialize<Raiz>(json, Opcoes);
        return raiz?.Perfis ?? new List<Perfil>();
    }

    public static void Salvar(string caminho, IReadOnlyList<Perfil> perfis)
    {
        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio)) Directory.CreateDirectory(diretorio);

        var raiz = new Raiz { Perfis = perfis.ToList() };
        File.WriteAllText(caminho, JsonSerializer.Serialize(raiz, Opcoes));
    }
}
