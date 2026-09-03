using System.IO;
using System.Linq;
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
        var perfis = raiz?.Perfis ?? new List<Perfil>();

        for (var indice = 0; indice < perfis.Count; indice++)
        {
            if (perfis[indice].Id <= 0)
                perfis[indice].Id = indice + 1;
        }

        return perfis;
    }

    public static void Salvar(string caminho, IReadOnlyList<Perfil> perfis)
    {
        var diretorio = Path.GetDirectoryName(caminho);
        if (!string.IsNullOrEmpty(diretorio)) Directory.CreateDirectory(diretorio);

        var lista = perfis.ToList();
        var proximoId = ProximoId(lista);
        foreach (var perfil in lista)
        {
            // preserva o ID definido no diálogo (inclusive editado manualmente) —
            // só atribui um novo quando o perfil ainda não tem um
            if (perfil.Id <= 0)
                perfil.Id = proximoId++;
        }

        var raiz = new Raiz { Perfis = lista };
        File.WriteAllText(caminho, JsonSerializer.Serialize(raiz, Opcoes));
    }

    public static int ProximoId(IReadOnlyList<Perfil> perfis) => perfis.Count > 0 ? perfis.Max(p => p.Id) + 1 : 1;
}
