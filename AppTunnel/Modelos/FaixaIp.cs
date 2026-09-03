using System.Net;
using System.Net.Sockets;

namespace AppTunnel.Modelos;

// Faixa de IPv4 fechada [IpInicio, IpFim] + faixa de porta [PortaInicio,
// PortaFim], em host order, pra checagem rápida no laço de pacotes da
// CamadaRede. Aceita IP único, CIDR ("x.x.x.x/n") ou faixa
// ("x.x.x.x-y.y.y.y"), com porta opcional depois de ":" (porta única ou
// faixa "80-443"); sem porta, casa qualquer porta.
internal readonly struct FaixaIp
{
    public uint IpInicio { get; }
    public uint IpFim { get; }
    public ushort PortaInicio { get; }
    public ushort PortaFim { get; }

    private FaixaIp(uint ipInicio, uint ipFim, ushort portaInicio, ushort portaFim)
    {
        IpInicio = ipInicio;
        IpFim = ipFim;
        PortaInicio = portaInicio;
        PortaFim = portaFim;
    }

    public bool Contem(uint ip, ushort porta) =>
        ip >= IpInicio && ip <= IpFim && porta >= PortaInicio && porta <= PortaFim;

    public static bool TentarAnalisar(string texto, out FaixaIp faixa)
    {
        faixa = default;
        texto = texto.Trim();
        if (texto.Length == 0) return false;

        ushort portaInicio = 0, portaFim = 65535;
        var idxPorta = texto.IndexOf(':');
        if (idxPorta >= 0)
        {
            if (!TentarAnalisarPorta(texto[(idxPorta + 1)..], out portaInicio, out portaFim)) return false;
            texto = texto[..idxPorta].Trim();
        }

        if (!TentarAnalisarIp(texto, out var ipInicio, out var ipFim)) return false;

        faixa = new FaixaIp(ipInicio, ipFim, portaInicio, portaFim);
        return true;
    }

    private static bool TentarAnalisarIp(string texto, out uint inicio, out uint fim)
    {
        inicio = fim = 0;

        if (texto.Contains('/'))
        {
            var partes = texto.Split('/', 2);
            if (!TentarConverterIp(partes[0], out var rede)) return false;
            if (!int.TryParse(partes[1], out var prefixo) || prefixo < 0 || prefixo > 32) return false;

            var mascara = prefixo == 0 ? 0u : 0xFFFFFFFFu << (32 - prefixo);
            inicio = rede & mascara;
            fim = rede | ~mascara;
            return true;
        }

        if (texto.Contains('-'))
        {
            var partes = texto.Split('-', 2);
            if (!TentarConverterIp(partes[0], out var a)) return false;
            if (!TentarConverterIp(partes[1], out var b)) return false;

            inicio = Math.Min(a, b);
            fim = Math.Max(a, b);
            return true;
        }

        if (!TentarConverterIp(texto, out var unico)) return false;
        inicio = fim = unico;
        return true;
    }

    private static bool TentarAnalisarPorta(string texto, out ushort inicio, out ushort fim)
    {
        inicio = fim = 0;
        texto = texto.Trim();

        if (texto.Contains('-'))
        {
            var partes = texto.Split('-', 2);
            if (!ushort.TryParse(partes[0].Trim(), out var a)) return false;
            if (!ushort.TryParse(partes[1].Trim(), out var b)) return false;

            inicio = Math.Min(a, b);
            fim = Math.Max(a, b);
            return true;
        }

        if (!ushort.TryParse(texto, out var unica)) return false;
        inicio = fim = unica;
        return true;
    }

    private static bool TentarConverterIp(string texto, out uint valor)
    {
        valor = 0;
        if (!IPAddress.TryParse(texto.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var bytes = ip.GetAddressBytes();
        valor = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        return true;
    }
}
