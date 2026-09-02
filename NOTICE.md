# Avisos de terceiros

O código do AppTunnel em si está sob licença MIT (ver `LICENSE`). Este projeto
distribui, sem modificação, dois binários de terceiros que têm licença própria:

## WinDivert

`nativo/WinDivert.dll` e `nativo/WinDivert64.sys` são da
[WinDivert](https://reqrypt.org/windivert.html) (Basil, reqrypt.org), licenciada sob
[LGPL v3](https://www.gnu.org/licenses/lgpl-3.0.html) (ou GPL v2, à escolha do
usuário — dual-licenciada). O AppTunnel usa a WinDivert via P/Invoke, linkando
dinamicamente contra `WinDivert.dll` em runtime — não linka estaticamente, não
modifica nem redistribui o código-fonte da WinDivert. O texto completo da
licença está em `WinDivert-2.2.2-A/LICENSE`.
