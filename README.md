# PCmonitor para Windows

Monitor local e leve de CPU, memoria e disco, escrito em C# para o .NET Framework.
Executa sem janela e registra amostras a cada 30 segundos em arquivos CSV diarios.
Permite marcar as fases `antes` e `depois` para comparar o computador apos mudancas.

## Requisitos

Windows com .NET Framework 4.x, WMI e contadores de desempenho disponiveis.
O script de compilacao usa o compilador Framework64 incluido no Windows de 64 bits.
Nao usa bibliotecas externas, servicos de nuvem nem envia registros pela rede.

## Compilar e instalar

No Windows PowerShell, na pasta do projeto:

```powershell
.\Compilar.ps1
.\Instalar.ps1
```

Se a politica local impedir scripts, revise os fontes e siga a politica do seu
computador. Nao e necessario desativar o Microsoft Defender ou suas protecoes.
O binario compilado localmente nao possui assinatura digital comercial.

O instalador copia o executavel para `%LOCALAPPDATA%\PCmonitor\bin`, cria atalhos
em `%LOCALAPPDATA%\PCmonitor` e configura a tarefa `PCmonitor` no login do usuario,
sem atraso programado. Nao inicia antes do login. Se nao houver permissao para
criar a tarefa, usa um atalho na pasta Inicializar e informa essa limitacao.
Nao ha garantia de tempo de inicio: a carga do Windows tambem influencia.

## Usar

Abra `%LOCALAPPDATA%\PCmonitor` no Explorador:

- **Iniciar**: inicia a coleta; uma segunda instancia nao duplica os registros.
- **Parar**: encerra a coleta; nao desativa a inicializacao no proximo login.
- **Marcar DEPOIS**: registra a mudanca de fase, mantida apos reiniciar.
- **Marcar ANTES**: retorna a fase inicial.
- **Abrir registros**: abre a pasta `data`.
- **Ver status**: mostra o horario da ultima coleta; confira se e recente.

Tambem e possivel executar `PCmonitor.exe` com `--run`, `--stop`, `--before` ou
`--after`. Os comandos de controle nao abrem uma janela.

## Registros

Os dados ficam em `%LOCALAPPDATA%\PCmonitor\data`, separados do codigo-fonte.
CSV em UTF-8, separador ponto e virgula, ponto decimal e horario com fuso.
Os dados sao mantidos localmente e nao sao apagados automaticamente.

| Arquivo | Conteudo |
| --- | --- |
| resources-AAAA-MM-DD.csv | CPU, RAM disponivel/usada, memoria comprometida, limite e paginas/s |
| disks-AAAA-MM-DD.csv | Atividade, fila atual, leitura/escrita por segundo e transferencias/s |
| volumes-AAAA-MM-DD.csv | Capacidade e espaco livre dos volumes locais fixos |
| processes-AAAA-MM-DD.csv | Uniao dos 10 processos com maior CPU e 10 com maior RAM |
| session-*.txt | Sistema, processador, primeiro disco, inicio e ultimo boot |
| markers.txt | Horarios das mudancas entre antes e depois |
| events.txt | Inicio e parada do monitor |
| errors-*.txt | Falhas da coleta, se houver |
| status.txt | Horario da ultima coleta concluida |

## Interpretacao e comparacao

Colete um periodo de uso normal antes da mudanca e repita tarefas semelhantes
apos ela. Anote o inicio e fim de instalacoes e exclua esse intervalo da analise.
Marque DEPOIS ao terminar a alteracao. Compare periodos de duracao semelhante,
com os mesmos programas e condicoes de uso.

As leituras WMI possuem suas proprias janelas de atualizacao; nao representam
necessariamente uma media de 30 segundos. A coleta pode perder picos curtos.
CPU por processo e calculada entre amostras e normalizada pelo total de CPUs
logicas. A primeira leitura de CPU de cada processo fica vazia. Processos
protegidos ou que encerraram durante a coleta podem nao aparecer.
RAM por processo e working set: paginas compartilhadas impedem somar esses valores
para calcular a memoria total usada. Paginas/s inclui arquivos mapeados e nao
indica, isoladamente, falta de RAM. Atividade de disco usa `100 - PercentIdleTime`,
limitada a 0..100; a fila e uma leitura instantanea. `_Total` agrega os discos.
Campos indisponiveis ficam vazios; veja tambem os erros. Correlacao entre fases
nao demonstra que uma unica alteracao causou a mudanca de desempenho.

O monitor tambem consome recursos. Uma medicao isolada deste aplicativo nao e
uma garantia de consumo para outros computadores.

## Privacidade

Os registros incluem nomes de processos, horarios e informacoes do sistema.
Nao coleta comandos, conteudo de arquivos, teclas, historico de navegacao ou rede.
Revise os registros antes de compartilha-los. Este repositorio exclui dados,
atalhos locais e executaveis compilados por meio do `.gitignore`.

## Desinstalar

Use **Parar**, desabilite/remova a tarefa `PCmonitor` no Agendador de Tarefas e
remova, se existir, o atalho `PCmonitor` da pasta Inicializar (`shell:startup`).
Depois apague `%LOCALAPPDATA%\PCmonitor` se tambem quiser remover os registros.
Remover apenas os fontes nao para a instalacao ativa.

## Licenca

MIT. Consulte [LICENSE](LICENSE).
## Assinatura local opcional

Depois de compilar, execute `./Assinar.ps1` para incluir uma assinatura Authenticode
com certificado local autoassinado (RSA 3072, SHA-256, validade de um ano).
O certificado e criado em `CurrentUser\My`, com chave privada nao exportavel.
Somente o certificado publico e exportado para `bin/PCmonitor-local.cer`.
O script nao instala raizes confiaveis nem altera protecoes do Windows.

Uma assinatura autoassinada pode aparecer como `UnknownError` ou `NotTrusted`,
com mensagem de raiz nao confiavel. Ela nao equivale a um certificado comercial
e nao garante que Defender, SmartScreen, Acesso controlado a pastas ou outros
antivirus aceitem o programa. O monitor nao precisa de regra de firewall.
Recompilar o executavel remove a assinatura; assine novamente antes de instalar.
Nunca publique chaves privadas ou arquivos PFX.