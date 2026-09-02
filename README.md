# AppTunnel

Proxy SOCKS5 por PID no Windows. Todo o tráfego TCP de um processo escolhido sai
por um proxy SOCKS5 específico, sem alterar o processo alvo: sem injeção de DLL,
sem hook de API, sem editar arquivo de configuração dele, sem parse do protocolo
da aplicação.

Várias instâncias podem rodar ao mesmo tempo, cada uma amarrada a um proxy
diferente — e portanto a um IP de saída diferente.

O vínculo é feito pelo **PID**, não por nome de executável nem por endereço de
destino. Isso é o que faz a ferramenta funcionar com aplicações que descobrem o
endereço do próximo servidor em runtime (dentro do payload do servidor anterior):
qualquer regra ancorada em `IP:porta` quebraria na conexão seguinte, enquanto o
vínculo por processo sobrevive porque cada `connect()` novo é observado no socket
layer, para qualquer destino.

## Como funciona

```
processo alvo (PID N)
      │  connect()
      ▼
 CamadaSocket  (WinDivert, camada SOCKET, filtro por processId)
      │  registra porta local → PID
      ▼
 CamadaRede    (WinDivert, camada NETWORK)
      │  reescreve destino do SYN → IP local da máquina:34567
      ▼
 RelaySocks5   (TcpListener)
      │  handshake SOCKS5 (RFC 1928/1929) com o proxy da instância
      ▼
 proxy SOCKS5 ──> destino real
```

| Componente | Papel |
|---|---|
| `Motor/CamadaSocket.cs` | Observa CONNECT/CLOSE apenas dos PIDs alvo e mapeia porta local → instância |
| `Motor/CamadaRede.cs` | NAT de saída: reescreve endereços entre o processo e o relay, descarta pacote sem mapeamento |
| `Motor/RelaySocks5.cs` | Aceita a conexão redirecionada e a repassa ao proxy |
| `Motor/ClienteSocks5.cs` | Handshake SOCKS5 com autenticação usuário/senha |
| `Motor/MotorProxy.cs` | Ciclo de vida: lançar processo suspenso, registrar vínculo, retomar, encerrar |

A chave de correlação entre as três partes é a **porta local TCP** — único
identificador comum às duas camadas do WinDivert e ao relay.

O processo alvo é lançado com `CREATE_SUSPENDED`, o vínculo PID→instância é
registrado, e só então a thread principal é retomada. Nenhum pacote pode
preceder o mapeamento.

## Requisitos

- Windows x64
- .NET 8 SDK (`net8.0-windows`, WPF)
- Privilégio de administrador — o primeiro `WinDivertOpen` instala o driver
- Build **x64 obrigatório**: `WinDivert64.sys` não carrega em processo x86
- Proxies SOCKS5 **estáticos**, não rotativos

## Build

```
dotnet build AppTunnel.sln -c Release
```

`WinDivert.dll` e `WinDivert64.sys` (em `nativo/`) são copiados para a pasta de
saída automaticamente. O `app.manifest` já pede elevação, então o executável
sobe com prompt de UAC.

## Configuração

O app lê e grava em uma pasta `config/` ao lado do próprio executável (não no
diretório de trabalho — assim o comportamento não muda entre lançar pela IDE,
por atalho ou por clique duplo). Ela é criada automaticamente no primeiro
arranque.

### `config/proxies.txt`

Uma linha por proxy, formato Webshare:

```
host:porta:usuario:senha
198.51.100.10:6118:meuusuario:minhasenha
198.51.100.11:6425:meuusuario:minhasenha
```

O split é feito em no máximo 4 partes, então a senha pode conter `:`. Proxies
sem autenticação podem ser informados como `host:porta`.

### `config/perfis.json`

Escrito pela própria UI; os índices apontam para linhas de `proxies.txt`.

```json
{
  "perfis": [
    {
      "nome": "instancia1",
      "exe": "C:\Caminho\App\app.exe",
      "args": "",
      "dir": "C:\Caminho\App",
      "proxyPrincipal": 0,
      "reservas": [5]
    }
  ]
}
```

`config/` está no `.gitignore` — credenciais reais nunca vão para o repositório.

## Regra de firewall (obrigatória)

Crie uma regra de **saída** no Firewall do Windows bloqueando o executável alvo
na interface real. Sem ela:

- UDP e IPv6 do processo saem direto, fora do proxy;
- existe uma janela curta entre o `connect()` ser observado e o SYN chegar na
  camada de rede em que um pacote pode escapar.

A camada SOCKET do WinDivert abre com `SNIFF | RECV_ONLY` — ela **observa**, não
retém nem bloqueia nada. Quem fecha essas lacunas na prática é a regra de
firewall. Tráfego local não é afetado por ela, então o caminho proxiado continua
funcionando normalmente.

## Interface

Quatro abas WPF, atualizadas por polling (500 ms) sobre coleções concorrentes do
motor — as threads do WinDivert nunca são bloqueadas para notificar a UI.

- **Instâncias** — nome, PID, proxy ativo, conexões ativas, bytes ↑↓, estado.
  Lançar, encerrar e trocar de proxy (permitido só com zero conexões ativas, para
  não trocar o IP de saída no meio de uma sessão).
- **Conexões** — PID, destino, porta local, estado, bytes, duração, proxy usado.
- **Proxies** — lista carregada do arquivo, latência do último teste, uso atual,
  teste em lote.
- **Log** — eventos do motor.

## Limitações conhecidas

- **Só TCP e só IPv4.** UDP e IPv6 não são roteados por proxy; são bloqueados
  pela regra de firewall.
- **DNS vaza por design.** A resolução de nomes sai pelo resolver local da
  máquina, não pelo proxy. As conexões TCP em si saem pelo proxy.
- **O relay escuta em `IPAddress.Any:34567`.** Redirecionar para `127.0.0.1` não
  funciona no Windows (o *strong host model* descarta o pacote reinjetado — ver
  WinDivert [#82](https://github.com/basil00/WinDivert/issues/82) e
  [#218](https://github.com/basil00/WinDivert/issues/218)), então a camada de
  rede usa o IP real de saída da máquina. Consequência: a porta 34567 fica
  alcançável pela LAN se não houver regra de entrada bloqueando.
- **Failover não troca IP durante a sessão.** O índice do proxy só avança quando
  a instância está com zero conexões ativas.
- Software de anti-cheat ou de proteção que detecte o WinDivert pode impedir o
  funcionamento. Nenhuma escolha de arquitetura aqui contorna isso.

## Licença

MIT — ver [LICENSE](LICENSE).

Os binários em `nativo/` são da [WinDivert](https://reqrypt.org/windivert.html),
redistribuídos sem modificação sob LGPL v3 / GPL v2 (dual). Detalhes em
[NOTICE.md](NOTICE.md).

Ferramenta de uso próprio. Use apenas em processos e redes que você tem
autorização para operar.
