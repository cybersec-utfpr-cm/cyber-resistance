# Revisao temporaria do projeto Cyber Resistance

Data da revisao: 2026-06-17  
Escopo: leitura geral do projeto Godot/C#, dados JSON, cenas principais, scripts de Docker/terminal, documentacao e estado do repositorio.

## Resumo executivo

O projeto compila em C# (`dotnet build` passou sem warnings/erros) e a estrutura geral esta compreensivel: Godot 4.4.1 com C#, managers em autoload, dados em JSON, UI de quest/quiz/livros/exame, computador in-game com terminal telnet e missoes que podem subir containers Docker.

O ponto mais urgente nao e compilacao, e runtime/fluxo: ha risco forte de managers duplicados entre autoload e cena principal, sinais conectados duas vezes ou para metodos inexistentes, e progresso de missoes acoplado a UI/interacoes especificas. Esses problemas podem aparecer como bugs estranhos durante o teste manual: missao pulando etapa, singleton apontando para o node errado, container tentando iniciar mais de uma vez, console cheio de erros de signal, ou partes da missao que param de avancar se a UI nao existir.

## Verificacoes executadas

- `dotnet build`: passou.
  - Resultado: `Build succeeded. 0 Warning(s), 0 Error(s).`
- Validacao JSON com `python3 -m json.tool`:
  - `Data/quests.json`: valido.
  - `Data/quizzes.json`: valido.
  - `Data/questions.json`: valido.
  - `Data/books.json`: valido.
  - `Data/rewards.json`: valido.
  - `Scripts/NPCs/Dialogues/npc_tutor_hubner.json`: valido.
- `dotnet csharpier check Scripts`: falhou para 44 arquivos.
  - Nao e bug funcional imediato, mas mostra que o codigo esta fora do padrao do formatter ja configurado.
- Smoke test Godot/headless: nao executado.
  - O ambiente atual nao tem `godot`, `godot4` nem `Godot_v4.4.1-stable_mono_linux_x86_64` no PATH.
- Docker runtime: nao executei containers.
  - O pedido foi revisao geral; evitei alterar estado externo de Docker alem de ler os scripts.

## Estado do repositorio

`git status --short` no momento da revisao:

```text
 M Scripts/Computer/Terminal/DockerManager.cs
 M install-docker-machines.sh
?? .config/
```

Observacoes:

- `Scripts/Computer/Terminal/DockerManager.cs` foi alterado para remover um container de missao parado em vez de apenas dar `docker start`. Isso ajuda quando a imagem foi reconstruida, mas tambem descarta estado anterior do container.
- `install-docker-machines.sh` foi alterado para limpar `player_machine`, `scenario1`, `cr_scenario1` e a rede `cr_mission_sudo_with_less`; agora ele cria a imagem `scenario1`, mas nao cria o container do cenario. Isso esta mais alinhado com `MissionInfrastructureManager`, que cria `cr_scenario1` sob demanda.
- `.config/dotnet-tools.json` define `csharpier` 1.3.0 e esta untracked. Se o formatter for padrao do projeto, esse arquivo deveria ser versionado.

## Achados de alta prioridade

### 1. Managers duplicados entre autoload e cena principal

Evidencias:

- `project.godot:20-28` registra `GameManager`, `DialogueManager`, `BookManager`, `QuizManager`, `QuestManager`, `MissionInfrastructureManager`, `NPCManager`, `InventoryManager` e `RewardManager` como autoload.
- `Scenes/Core/game.tscn:10-22` tambem instancia `GameManager` e `NPCManager` como nodes da cena principal.
- `Scripts/Core/GameManager.cs:15-18` usa `/root/Game/WorldContainer` e `/root/Game/UIContainer`.

Impacto:

- Pode haver dois `GameManager` e dois `NPCManager` vivos.
- `Instance` pode ser sobrescrito pelo ultimo node que executar `_Ready`.
- O autoload `GameManager` pode tentar procurar `/root/Game/...` antes da cena principal estar pronta.
- Docker e quest log podem ser inicializados duas vezes ou em ordem errada.
- Bugs podem ser intermitentes, porque dependem da ordem de inicializacao do Godot.

Recomendacao:

- Escolher uma unica estrategia:
  - Opção mais simples: manter `GameManager` e `NPCManager` como nodes da cena `Game` e remover ambos do `[autoload]`.
  - Ou transformar ambos em autoload de verdade e remover os nodes duplicados da cena, mas nesse caso `GameManager` deve resolver `WorldContainer`/`UIContainer` de forma segura depois que a cena principal existir.
- Padronizar todos os managers para usar `_EnterTree` ou `_Ready` consistentemente e limpar `Instance` em `_ExitTree` quando aplicavel.

### 2. Fluxo de missoes depende da UI de quest log

Evidencias:

- `Scripts/Interfaces/QuestLogUi.cs:54-72` inicia `wifi_hacking`, `sudo_with_less` e `university_exam` dentro de `OnQuestCompleted`.

Impacto:

- Se `QuestLogUi` falhar, nao for instanciada, for removida, ou virar outra UI, a campanha para de avancar.
- Regra de progressao fica escondida em uma UI, dificultando teste e manutencao.

Recomendacao:

- Mover cadeia de missoes para um servico de dominio, por exemplo `QuestProgressionManager`, ou para o proprio `QuestManager`.
- A UI deve observar estado, nao decidir qual missao vem depois.

### 3. Tutorial pode pular etapas pela estante

Evidencias:

- `Scripts/Interactions/Bookshelf.cs:39-46` muda tutorial de stage 1 para 2.
- Logo em seguida, `Scripts/Interactions/Bookshelf.cs:61-65` ve stage 2 e muda para 3 na mesma interacao.
- O dialogo do tutor tambem ja muda tutorial para stage 2 em `Scripts/NPCs/Dialogues/npc_tutor_hubner.json`.

Impacto:

- O jogador pode potencialmente abrir a estante logo no inicio e pular a conversa com o tutor.
- A missao "Fale com o tutor" perde valor pratico.

Recomendacao:

- Remover o avancar de stage 1 na estante.
- Deixar o tutor ser o unico responsavel por stage 1 -> 2.
- A estante deve avancar stage 2 -> 3 apenas depois de uma leitura real ou ao menos apos abrir um capitulo relevante, nao apenas ao abrir a UI.

### 4. Sinais conectados em cena e tambem no codigo

Evidencias:

- `Scripts/Interactions/Bookshelf.cs:12-13` conecta `BodyEntered/BodyExited`; a cena tambem conecta em `Scenes/Interactions/bookshelf.tscn:31-32`.
- `Scripts/Interactions/DoorArea.cs:12-13`; cena tambem em `Scenes/Interactions/door_area.tscn:17-18`.
- `Scripts/Interactions/ExamTerminal.cs:10-11`; cena tambem em `Scenes/Interactions/exam_terminal.tscn:17-18`.
- `Scripts/NPCs/NPCTutorHubner.cs:12-13`; cena tambem em `Scenes/NPCs/npc_tutor_hubner.tscn:159-160`.
- `Scenes/Interactions/computer_access.tscn:15-16` conecta para `_on_body_entered` e `_on_body_exited`, mas `Scripts/Interactions/ComputerAccess.cs` define `OnBodyEntered`/`OnBodyExited` e tambem conecta no `_Ready`.

Impacto:

- Eventos podem disparar duas vezes.
- No `DoorArea`, NPC entrando na porta pode chamar `OnReachedDoor` duas vezes; hoje existe guard com `IsChangingScene`, mas isso e fragil.
- `ComputerAccess` deve gerar erro de metodo inexistente no console, mesmo que a conexao feita no codigo funcione.

Recomendacao:

- Padronizar: ou conexoes pelo Inspector/cena, ou conexoes no `_Ready`.
- Para este projeto, eu manteria conexoes no codigo para areas dinamicas e removeria as entradas duplicadas/invalidas dos `.tscn`.

## Achados funcionais e de gameplay

### Wi-Fi

- O fluxo real parece ser Desktop -> Settings -> WiFi:
  - `Scenes/Computer/computer.tscn:317` conecta `Screens/Settings/WifiButton` para `_on_wifi_button_pressed`.
  - `Scripts/Computer/Screens/Settings.cs` chama `ShowScreen("WiFi")`.
- `Scripts/Computer/Desktop.cs:15-17` tem metodo `_on_wifi_button_pressed` que abre `Settings`, nao `WiFi`, mas nao parece conectado na cena atual.
- `Scenes/Computer/wi_fi_screen.tscn` existe separado, mas nao tem script `WiFiScreen.cs` nem node `Anagram`; se alguem usar essa cena, ela nao funciona como a tela embutida em `computer.tscn`.

Recomendacao:

- Apagar ou completar `Scenes/Computer/wi_fi_screen.tscn`.
- Remover metodo morto de `Desktop.cs` ou conectar intencionalmente.
- Garantir no teste manual que a missao Wi-Fi avanca somente pelo caminho esperado.

### Quiz UI

Evidencias:

- `Scripts/Interfaces/QuizUi.cs:102-108` cria um `new ButtonGroup()` para cada botao.
- `Scripts/Interfaces/QuizUi.cs:137` usa o primeiro botao pressionado encontrado.

Impacto:

- A selecao nao e exclusiva; multiplas opcoes podem ficar marcadas.
- O resultado pode considerar uma opcao antiga/errada se varias estiverem marcadas.

Recomendacao:

- Criar um unico `ButtonGroup` antes do loop, como ja foi feito corretamente em `ExamUi`.
- Trocar `int.Parse` em `Scripts/Interfaces/QuizUi.cs:156` por `int.TryParse` para evitar crash com dados ruins.

### Exame

- `Scripts/Interactions/ExamTerminal.cs:56` chama `StartExam("intro_exam", 10)`.
- `Data/quizzes.json` tem exatamente 10 perguntas hoje.

Impacto:

- Se alguem remover pergunta ou trocar o quiz, o exame fecha sem fallback.

Recomendacao:

- Tornar `quizId` e `questionCount` exportados no `ExamTerminal`.
- Se houver menos perguntas que o pedido, usar `Math.Min` ou mostrar uma mensagem clara de configuracao incompleta.

### Terminal in-game

Evidencias:

- `Scripts/Computer/Terminal/InputText.cs:74-78` da `QueueFree()` no node `Terminal` quando o usuario digita `exit` no nivel base.

Impacto:

- Como o terminal e uma tela dentro de `Screens`, destruir o node pode impedir reabrir o terminal depois pelo icone.
- `exitLevels` e uma heuristica textual; comandos como `bash`, `sh`, `sudo -s`, falhas de `ssh`/`telnet`, ou shells remotos podem deixar o contador errado.

Recomendacao:

- Ao digitar `exit` no nivel base, voltar para `Desktop` em vez de destruir a tela.
- Separar comandos locais da UI de comandos enviados ao container.
- Dar feedback quando `TerminalController.SendCommand` ignora comando por falta de conexao (`Scripts/Computer/Terminal/TerminalController.cs:108-114`).

## Docker, terminal e seguranca

### Execucao Docker via `bash -c`

Evidencias:

- `Scripts/Computer/Terminal/DockerManager.cs:158-164` monta `bash -c "{command}"`.
- Varios argumentos vem de JSON/campos: network, driver, container, image, hostname, alias.

Impacto:

- Mesmo em jogo local, isso e uma superficie de injecao de comando se algum valor configuravel contiver caracteres de shell.
- Espacos/aspas em nomes tambem quebram comando.

Recomendacao:

- Usar `ProcessStartInfo.ArgumentList` chamando `docker` diretamente, sem shell.
- Validar nomes de container/rede/imagem por allowlist simples.
- Adicionar timeout/cancelamento para comandos Docker.

### Ciclo de vida Docker

- `GameManager` inicia e para `player_machine` na entrada/saida (`Scripts/Core/GameManager.cs:43-70`).
- `MissionInfrastructureManager` cria rede e conecta `player_machine` quando a missao tem infraestrutura (`Scripts/Core/MissionInfrastructureManager.cs:74-99`).
- Ao concluir missao, remove containers de missao, mas nao remove rede nem desconecta `player_machine` (`Scripts/Core/MissionInfrastructureManager.cs:102-111`).

Recomendacao:

- Definir politica clara:
  - Player machine vive durante a sessao inteira.
  - Redes de missao sao criadas no start e removidas no complete/exit.
  - Containers de missao sao descartaveis.
- Registrar no README que `sudo_with_less` deve ser acessado de dentro da maquina do jogador, por exemplo via `ssh bob@scenario1`, se essa for a intencao.

### Telnet

- `TerminalController.ConnectWithRetry` cria `TcpClient`/login sem cancelamento ate a conexao terminar.
- `cts` so existe depois de conectar; se a tela fechar durante retries, a task pode continuar.
- `TelnetConnection` nao implementa `IDisposable`; o socket pode ficar aberto.

Recomendacao:

- Introduzir cancelamento desde o inicio da conexao.
- Dispor `TcpClient`.
- Mostrar estado de conexao na UI do terminal.

## Arquitetura e manutenibilidade

### Estado global e singletons

- Os managers usam `static Instance`, mas nem todos limpam no `_ExitTree`.
- Alguns setam `Instance` no `_EnterTree`; outros no `_Ready`.
- Com duplicidade de autoload/cena, isso fica ainda mais arriscado.

Recomendacao:

- Padronizar lifecycle dos singletons.
- Evitar managers duplicados.
- Quando um manager depende de outro, conectar sinais depois que todos estiverem prontos.

### JSON sem validacao robusta

- `QuestManager`, `QuizManager`, `BookManager`, `RewardManager` acessam varias chaves obrigatorias diretamente.
- Se uma chave faltar, o erro aparece em runtime e pode ser pouco amigavel.

Recomendacao:

- Criar um validador leve para `Data/*.json` e dialogos.
- Validar referencias cruzadas:
  - `quest.reward_id` existe em `rewards.json`.
  - `RewardQuestStage` aponta para quest/stage existente.
  - `correct` esta dentro do range de `options`.
  - `machines[].image/container_name/network_alias` estao preenchidos quando a missao tem infraestrutura.

### Dados de perguntas duplicados

- `Data/quizzes.json` e `Data/questions.json` modelam perguntas separadamente.
- `QuestionManager` carrega `questions.json`, mas o exame usa `QuizManager` e `quizzes.json`.

Recomendacao:

- Escolher um modelo principal.
- Se `questions.json` for banco reutilizavel, gerar quizzes por tema.
- Se `quizzes.json` for o modelo final, remover `QuestionManager`/`questions.json` ou documentar uso futuro.

### NPCs e rotina

Evidencias:

- `Scripts/NPCs/Hubner/NPCMovementAI.cs:130-132` comenta que `_Ready` nao sera chamado de novo apos reparent e que falta mecanismo para avancar rotina.
- `Scripts/NPCs/NPCManager.cs:45-49` espera `LocationManager` em loop sem timeout.
- `LocationManager.GetLocation` retorna `Vector2.Zero` se nao encontrar local.

Impacto:

- NPC pode ficar preso, ir para `(0, 0)`, ou nao avancar rotina ao trocar de cena.
- Se uma cena nao tiver `LocationManager`, o await em loop pode nunca terminar.

Recomendacao:

- Finalizar o mecanismo de rotina apos troca de cena.
- Adicionar timeout/erro claro no loop por `LocationManager`.
- Tratar location ausente como falha, nao como posicao zero valida.

## UI e cenas

- Muitas UIs usam offsets fixos e pouca responsividade (`quiz_ui.tscn`, `exam_ui.tscn`, computador).
- `QuizUi` e `BookshelfUi` instanciam controles dinamicamente, mas nao controlam bem reabertura/multiplas instancias.
- `ComputerAccess` pausa a arvore (`GetTree().Paused = true`) e `Computer.ExitComputer` despausa, mas e bom validar se todos os nodes de UI necessarios tem `ProcessMode` adequado.

Recomendacao:

- Antes de polir visual, estabilizar navegacao e ciclo de vida:
  - abrir/fechar computador repetidamente;
  - abrir terminal, voltar, reabrir;
  - abrir livro varias vezes;
  - iniciar exame e fechar;
  - sair com ESC em qualquer tela do computador.

## Documentacao

### README principal

Pontos bons:

- Explica dependencias gerais.
- Documenta Docker setup.
- Diz que a maquina do jogador e iniciada pelo `GameManager`.

Problemas:

- `README.md:7` fala em ASP.NET Core Runtime, mas para desenvolvimento Godot C# normalmente o leitor vai precisar de SDK/.NET adequado e Godot .NET.
- `README.md:93-105` tem secao 5, mas passos numerados como 4.1, 4.2, 4.3.
- O README orienta baixar build zip do Drive, mas o repo tambem e codigo-fonte; faltam instrucoes diretas para abrir este projeto local no Godot.
- As normas dizem "tudo em ingles" e "sem acentos", mas o projeto usa muito portugues em codigo, comentarios, dados e UI. Melhor atualizar a norma para refletir a realidade ou fazer uma migracao planejada.

### README do cenario 1

Evidencias:

- `Scenarios/Scenario1/README.md:49-58` mostra Dockerfile antigo com `PermitRootLogin yes`, `ENTRYPOINT` e copia de `entrypoint.sh`.
- O Dockerfile atual usa `PermitRootLogin no` e `CMD ["/usr/sbin/sshd", "-D"]`.
- `Scenarios/Scenario1/README.md:106-123` ensina mapear porta 2222 no host, mas o fluxo atual cria `cr_scenario1` pelo jogo na rede Docker, sem porta host.

Recomendacao:

- Atualizar o README do cenario para o fluxo atual:
  - imagem `scenario1`;
  - container `cr_scenario1`;
  - acesso a partir de `player_machine` via network alias `scenario1`;
  - credenciais `bob:password`.

## Limpeza recomendada

Arquivos/artefatos para revisar:

- `Scripts/Interactions/QuestManager.cs`: codigo antigo inteiro comentado. Remover se nao for mais usado.
- `Scripts/Establishments/Cafeteria.cs`: vazio.
- `Scenes/Core/quest_manager.tscn`, `quiz_manager.tscn`, `book_manager.tscn`: parecem redundantes se os managers sao autoload por script.
- `Scenes/Computer/wi_fi_screen.tscn`: incompleta/sem script.
- Imports e `using` nao usados em varios arquivos.
- Comentarios temporarios/typos: exemplos em `GameManager.cs:123`, `SpawnPoints.cs`, `NPCMovementAI.cs:130-132`.

## Formato e padrao de codigo

O projeto tem `csharpier` configurado, mas `dotnet csharpier check Scripts` acusou 44 arquivos fora do padrao.

Recomendacao:

- Depois de corrigir os bugs de runtime, rodar uma passada unica de formatter em branch separada.
- Evitar misturar formatacao massiva com mudancas funcionais no mesmo commit.
- Corrigir o fechamento/indentacao de `InventoryManager.cs`, que hoje compila mas esta muito dificil de ler (`Scripts/Core/InventoryManager.cs:40-71`).

## Roteiro sugerido de correcao

### Fase 1 - Estabilizar runtime

1. Resolver duplicidade de `GameManager` e `NPCManager`.
2. Remover sinais duplicados e conexoes invalidas dos `.tscn`.
3. Corrigir skip do tutorial na estante.
4. Corrigir comportamento de `exit` no terminal para nao destruir a tela.
5. Validar manualmente abertura/fechamento do computador e terminal.

### Fase 2 - Estabilizar missoes

1. Tirar progressao de campanha de `QuestLogUi`.
2. Criar um ponto unico para regras de "ao completar missao X, iniciar Y".
3. Validar o caminho:
   - tutorial;
   - Wi-Fi;
   - sudo with less;
   - prova;
   - recompensas.

### Fase 3 - Infra Docker/terminal

1. Refatorar `DockerManager` para nao usar `bash -c`.
2. Adicionar timeout/cancelamento.
3. Limpar rede de missao no fim.
4. Atualizar README do cenario.

### Fase 4 - Qualidade

1. Adicionar validador de JSON/cenas criticas.
2. Decidir modelo unico de perguntas.
3. Rodar CSharpier em commit separado.
4. Remover arquivos mortos.

## Checklist de teste manual

Ao testar, eu verificaria nesta ordem:

1. Abrir o jogo e observar console:
   - sem erro de `/root/Game/WorldContainer`;
   - sem erro de metodo `_on_body_entered`;
   - sem manager inicializando duas vezes.
2. Confirmar que `player_machine` inicia uma vez.
3. Tentar abrir a estante antes de falar com o tutor:
   - esperado: nao deve pular a missao de falar com o tutor.
4. Falar com tutor:
   - esperado: tutorial vai para stage 2.
5. Abrir livro/capitulo:
   - esperado: tutorial vai para stage 3 somente apos a acao esperada.
6. Abrir computador:
   - esperado: tutorial conclui, jogo pausa, ESC fecha e despausa.
7. Abrir terminal:
   - esperado: conecta ao Docker, comandos antes da conexao nao somem silenciosamente ou mostram feedback.
8. Digitar `exit` no terminal:
   - esperado: volta para desktop/tela anterior e permite reabrir terminal.
9. Caminho Wi-Fi:
   - Settings -> WiFi -> selecionar rede correta -> anagrama -> missao conclui.
10. Caminho sudo:
   - ao iniciar `sudo_with_less`, rede/container sao criados;
   - de dentro do player, `ssh bob@scenario1` funciona;
   - ao concluir, container de missao e limpo.
11. Exame:
   - quest chega no stage certo;
   - prova abre;
   - aprovacao completa quest;
   - recompensa fica coletavel uma vez.

## Conclusao

O projeto esta em bom estado de prototipo funcional: compila, os dados estao validos e a intencao de gameplay esta clara. A maior necessidade agora e reduzir comportamento implicito: um unico lugar para managers, um unico lugar para progressao de missoes, sinais conectados de forma consistente e cenas incompletas removidas ou finalizadas.

Se esses pontos forem ajustados antes de polimento visual/conteudo, os proximos testes vao ficar muito mais confiaveis.
