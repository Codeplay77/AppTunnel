using System.IO;
using System.Net.Sockets;
using System.Text;
using AppTunnel.Modelos;

namespace AppTunnel.Motor;

internal static class ClienteSocks5
{
    // Negociação de método + autenticação, sem enviar CONNECT.
    public static async Task NegociarAsync(NetworkStream fluxo, ProxyCfg proxy, CancellationToken ct)
    {
        var usaAuth = !string.IsNullOrEmpty(proxy.Usuario);

        var negociacao = usaAuth ? new byte[] { 0x05, 0x01, 0x02 } : new byte[] { 0x05, 0x01, 0x00 };
        await fluxo.WriteAsync(negociacao, ct);

        var respostaMetodo = new byte[2];
        await LerExatoAsync(fluxo, respostaMetodo, ct);
        if (respostaMetodo[0] != 0x05)
            throw new IOException("Proxy SOCKS5 respondeu versão inválida na negociação");
        if (respostaMetodo[1] == 0xFF)
            throw new IOException("Proxy SOCKS5 recusou todos os métodos de autenticação");

        if (respostaMetodo[1] == 0x02)
        {
            if (!usaAuth)
                throw new IOException("Proxy exigiu usuário/senha e nenhuma credencial foi configurada");

            var usuario = Encoding.ASCII.GetBytes(proxy.Usuario!);
            var senha = Encoding.ASCII.GetBytes(proxy.Senha ?? "");
            var auth = new byte[3 + usuario.Length + senha.Length];
            auth[0] = 0x01;
            auth[1] = (byte)usuario.Length;
            usuario.CopyTo(auth, 2);
            auth[2 + usuario.Length] = (byte)senha.Length;
            senha.CopyTo(auth, 3 + usuario.Length);
            await fluxo.WriteAsync(auth, ct);

            var respostaAuth = new byte[2];
            await LerExatoAsync(fluxo, respostaAuth, ct);
            if (respostaAuth[0] != 0x01 || respostaAuth[1] != 0x00)
                throw new IOException("Autenticação SOCKS5 falhou");
        }
        else if (respostaMetodo[1] != 0x00)
        {
            throw new IOException($"Método SOCKS5 não suportado: 0x{respostaMetodo[1]:X2}");
        }
    }

    // Negocia e envia CONNECT para ipDestino:portaDestino (host order).
    public static async Task ConectarAsync(NetworkStream fluxo, ProxyCfg proxy, uint ipDestino, ushort portaDestino, CancellationToken ct)
    {
        await NegociarAsync(fluxo, proxy, ct);

        var pedido = new byte[10];
        pedido[0] = 0x05;
        pedido[1] = 0x01; // CMD = CONNECT
        pedido[2] = 0x00; // RSV
        pedido[3] = 0x01; // ATYP = IPv4
        pedido[4] = (byte)(ipDestino >> 24);
        pedido[5] = (byte)(ipDestino >> 16);
        pedido[6] = (byte)(ipDestino >> 8);
        pedido[7] = (byte)ipDestino;
        pedido[8] = (byte)(portaDestino >> 8);
        pedido[9] = (byte)portaDestino;
        await fluxo.WriteAsync(pedido, ct);

        var resposta = new byte[10]; // ATYP=01 -> resposta fixa de 10 bytes
        await LerExatoAsync(fluxo, resposta, ct);
        if (resposta[0] != 0x05)
            throw new IOException("Proxy SOCKS5 respondeu versão inválida no CONNECT");
        if (resposta[1] != 0x00)
            throw new IOException($"Proxy SOCKS5 recusou CONNECT, REP=0x{resposta[1]:X2}");
    }

    private static async Task LerExatoAsync(NetworkStream fluxo, byte[] buffer, CancellationToken ct)
    {
        var lidos = 0;
        while (lidos < buffer.Length)
        {
            var n = await fluxo.ReadAsync(buffer.AsMemory(lidos), ct);
            if (n == 0) throw new IOException("Proxy SOCKS5 fechou a conexão durante o handshake");
            lidos += n;
        }
    }
}
