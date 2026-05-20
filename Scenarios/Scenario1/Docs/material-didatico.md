# Material Didático — Cenário 1: Sudo with Less

## 1. Visão geral do cenário

Este material explica os conceitos necessários para compreender o primeiro cenário de pentest do projeto **Cyber Resistance**, chamado **“Sudo with Less”**.

O cenário simula uma situação de **escalonamento de privilégio local em Linux**. O jogador acessa uma máquina Linux como um usuário comum chamado `bob` e precisa identificar uma configuração insegura que permite executar o programa `less` com privilégios administrativos. A partir disso, é possível usar uma funcionalidade do próprio `less` para abrir um shell como `root`.

Em termos didáticos, o cenário trabalha os seguintes temas:

- permissões e usuários no Linux;
- diferença entre usuário comum e superusuário;
- uso do `sudo`;
- configuração do arquivo `sudoers`;
- enumeração local de privilégios;
- programas interativos em Linux;
- uso do `less`;
- shell escape;
- escalonamento de privilégio;
- impacto e correção de permissões inseguras.

> **Aviso de uso ético:** este cenário deve ser utilizado apenas em ambiente controlado, autorizado e isolado, como o container Docker fornecido pelo projeto. As técnicas estudadas aqui são apresentadas com finalidade educacional.

---

## 2. Conceitos fundamentais de Linux

### 2.1 Usuários no Linux

Sistemas Linux são multiusuário. Isso significa que diferentes contas podem existir no mesmo sistema, cada uma com permissões próprias.

Exemplos de usuários comuns:

```bash
bob
alice
joao
```

Cada usuário possui um identificador chamado **UID** (*User ID*). O usuário administrativo mais importante do sistema é o `root`, que normalmente possui UID `0`.

Para verificar o usuário atual, pode-se usar:

```bash
whoami
```

Para verificar informações mais detalhadas do usuário atual:

```bash
id
```

Exemplo de saída:

```bash
uid=1000(bob) gid=1000(bob) groups=1000(bob)
```

Essa saída indica que o usuário atual é `bob`, com UID `1000`. Em geral, usuários comuns possuem UID diferente de `0`.

---

### 2.2 O usuário `root`

O `root` é o superusuário do Linux. Ele possui privilégios administrativos e pode realizar ações críticas, como:

- instalar e remover pacotes;
- criar e remover usuários;
- alterar arquivos do sistema;
- modificar permissões;
- iniciar e parar serviços;
- acessar arquivos restritos;
- alterar configurações de segurança.

Em um sistema Linux, obter acesso como `root` geralmente significa ter controle total sobre aquela máquina.

Por isso, em pentest, uma das etapas mais importantes após obter acesso inicial a uma máquina é tentar descobrir se existe algum caminho legítimo ou mal configurado que permita elevar privilégios de usuário comum para `root`.

---

### 2.3 Shell

Um **shell** é um interpretador de comandos. Ele recebe comandos digitados pelo usuário e solicita ao sistema operacional que execute essas ações.

Exemplos de shells comuns:

```bash
/bin/sh
/bin/bash
/bin/zsh
```

Quando o jogador acessa o cenário via SSH, ele recebe um shell como o usuário `bob`. O objetivo do cenário é transformar esse shell comum em um shell privilegiado, executando comandos como `root`.

Para verificar se o shell atual está com privilégio de root, pode-se usar:

```bash
whoami
```

Se a resposta for:

```bash
root
```

então o jogador conseguiu escalonar privilégios.

---

## 3. Conceitos de permissões no Linux

### 3.1 Permissões de arquivos

Arquivos e diretórios no Linux possuem permissões. Elas controlam quem pode ler, escrever ou executar determinado recurso.

Para listar permissões, usa-se:

```bash
ls -l
```

Exemplo:

```bash
-rw-r--r-- 1 root root 1234 mai 20 10:00 arquivo.txt
```

A primeira parte indica as permissões:

```text
-rw-r--r--
```

Uma forma simplificada de ler isso é:

```text
dono     grupo    outros
rw-      r--      r--
```

Onde:

- `r` significa leitura (*read*);
- `w` significa escrita (*write*);
- `x` significa execução (*execute*);
- `-` significa ausência daquela permissão.

---

### 3.2 Arquivos importantes para usuários

Dois arquivos importantes para compreender usuários no Linux são:

```bash
/etc/passwd
/etc/shadow
```

O arquivo `/etc/passwd` contém informações básicas sobre usuários do sistema. Normalmente, usuários comuns conseguem lê-lo.

O arquivo `/etc/shadow` contém hashes de senhas e informações sensíveis de autenticação. Normalmente, apenas o `root` consegue acessá-lo diretamente.

Isso ajuda o aluno a entender a diferença prática entre um usuário comum e um usuário privilegiado.

---

## 4. O que é `sudo`

### 4.1 Ideia geral

O comando `sudo` permite que um usuário execute comandos com privilégios de outro usuário, normalmente o `root`.

Exemplo:

```bash
sudo apt update
```

Nesse caso, um usuário comum solicita permissão para executar `apt update` com privilégios administrativos.

O `sudo` é uma ferramenta essencial para administração segura de sistemas, porque evita que usuários precisem trabalhar o tempo todo logados diretamente como `root`.

---

### 4.2 Por que `sudo` existe?

Sem `sudo`, haveria duas alternativas ruins:

1. dar a senha do `root` para várias pessoas;
2. deixar usuários comuns sem possibilidade de realizar tarefas administrativas legítimas.

O `sudo` resolve isso permitindo que o administrador diga exatamente:

- qual usuário pode executar comandos administrativos;
- em qual máquina;
- como qual usuário;
- quais comandos;
- se precisa ou não de senha.

---

### 4.3 O comando `sudo -l`

Durante enumeração local, um dos comandos mais importantes é:

```bash
sudo -l
```

Esse comando lista quais comandos o usuário atual tem permissão para executar via `sudo`.

No cenário, o jogador deve usá-lo para descobrir que o usuário `bob` pode executar o `less` como root sem senha.

Esse é o momento principal de enumeração do laboratório: antes de explorar, o jogador precisa observar a configuração do ambiente.

---

## 5. O arquivo `sudoers`

### 5.1 O que é o `sudoers`

O arquivo `sudoers` define as regras de permissão do `sudo`.

Em muitos sistemas, o arquivo principal fica em:

```bash
/etc/sudoers
```

Também é comum existirem regras adicionais em:

```bash
/etc/sudoers.d/
```

No cenário, existe um arquivo específico para o usuário `bob`:

```bash
/etc/sudoers.d/bob
```

A regra vulnerável é:

```sudoers
bob ALL=(ALL) NOPASSWD: /usr/bin/less
```

---

### 5.2 Entendendo a regra vulnerável

A linha:

```sudoers
bob ALL=(ALL) NOPASSWD: /usr/bin/less
```

pode ser dividida em partes:

```text
bob        ALL        (ALL)        NOPASSWD:        /usr/bin/less
```

#### `bob`

Indica a qual usuário a regra se aplica.

Neste caso, a regra vale para o usuário `bob`.

#### `ALL`

Indica em quais hosts/máquinas a regra vale.

Neste cenário, `ALL` significa que a regra vale para qualquer host dentro do contexto da configuração.

#### `(ALL)`

Indica como qual usuário o comando pode ser executado.

Como está `(ALL)`, o usuário `bob` pode executar o comando como qualquer usuário, inclusive `root`.

#### `NOPASSWD:`

Indica que o usuário não precisa informar senha para executar aquele comando via `sudo`.

Isso facilita o cenário para fins didáticos e também representa uma prática perigosa quando aplicada de forma descuidada.

#### `/usr/bin/less`

Indica o comando permitido.

Aparentemente, permitir apenas o `less` parece pouco perigoso. O problema é que `less` é um programa interativo e permite executar comandos externos.

---

### 5.3 Por que essa regra é perigosa?

A regra não permite diretamente:

```bash
sudo /bin/bash
```

nem:

```bash
sudo /bin/sh
```

Porém, ela permite:

```bash
sudo less arquivo
```

E o `less`, por ser interativo, possui uma funcionalidade que permite chamar comandos do shell.

Portanto, embora o administrador tenha permitido apenas o `less`, na prática ele deu ao usuário um caminho indireto para abrir um shell privilegiado.

Esse é um exemplo clássico de erro de configuração: permitir com `sudo` um programa que parece seguro, mas que possui recursos capazes de executar outros comandos.

---

## 6. Programas interativos e risco de segurança

### 6.1 O que são programas interativos?

Programas interativos são aqueles que não apenas executam uma tarefa e terminam. Eles abrem uma interface de interação com o usuário.

Exemplos:

```bash
less
more
vim
nano
man
find
```

Alguns desses programas permitem:

- abrir arquivos;
- editar conteúdo;
- chamar comandos externos;
- executar um shell;
- interagir com variáveis de ambiente;
- carregar plugins ou scripts;
- acessar outros arquivos do sistema.

Por isso, permitir programas interativos via `sudo` pode ser perigoso.

---

### 6.2 O princípio do menor privilégio

Um princípio fundamental de segurança é o **princípio do menor privilégio**.

Ele afirma que um usuário, processo ou serviço deve ter apenas as permissões estritamente necessárias para cumprir sua função.

No caso do cenário, o usuário `bob` não deveria ter permissão para executar um programa interativo como `less` com privilégios de root.

Mesmo que a intenção fosse permitir que `bob` lesse algum arquivo específico, a regra foi ampla demais, pois permite executar o binário `less` como root sobre qualquer arquivo.

---

## 7. O que é o `less`

### 7.1 Função normal do `less`

O `less` é um visualizador de texto em terminal. Ele permite abrir e navegar por arquivos de texto.

Exemplo:

```bash
less /etc/hosts
```

Dentro do `less`, o usuário pode navegar pelo conteúdo do arquivo usando teclas como:

```text
setas
Page Up
Page Down
/
q
```

A tecla `q` sai do programa.

---

### 7.2 Por que o `less` é usado em administração?

Administradores usam `less` para visualizar arquivos grandes ou logs sem abrir um editor de texto completo.

Exemplos:

```bash
less /var/log/syslog
less /etc/ssh/sshd_config
less /etc/passwd
```

A vantagem é que o `less` permite ler arquivos de forma confortável sem carregar tudo de uma vez na tela.

---

### 7.3 O recurso perigoso: executar comandos

Dentro do `less`, é possível usar o caractere `!` para executar comandos do shell.

Exemplo dentro do `less`:

```bash
!/bin/sh
```

Isso solicita ao `less` que abra um shell.

Se o `less` estiver rodando como usuário comum, o shell aberto será comum.

Mas se o `less` foi iniciado com `sudo`, ele estará rodando com privilégios elevados. Assim, o shell chamado a partir dele também poderá herdar esses privilégios.

Esse é o ponto central do cenário.

---

## 8. Shell escape

### 8.1 Definição

**Shell escape** é uma técnica em que o usuário “escapa” de dentro de um programa para executar comandos em um shell.

No contexto deste cenário, o jogador não executa diretamente:

```bash
sudo /bin/sh
```

Ele executa:

```bash
sudo less /etc/hosts
```

E, de dentro do `less`, chama:

```bash
!/bin/sh
```

Isso cria um caminho indireto:

```text
bob → sudo less como root → comando interno do less → /bin/sh como root
```

---

### 8.2 Por que isso funciona?

O `sudo` inicia o processo `less` com privilégios de root.

Depois disso, quando o `less` chama outro programa, como `/bin/sh`, esse novo processo pode herdar o contexto privilegiado do processo que o iniciou.

Assim, o problema não está em uma falha de memória, buffer overflow ou exploração complexa. O problema está em uma permissão administrativa mal configurada.

Essa é uma lição importante: nem toda vulnerabilidade depende de código quebrado. Muitas vulnerabilidades surgem de configurações inseguras.

---

## 9. Escalonamento de privilégio

### 9.1 O que é escalonamento de privilégio?

Escalonamento de privilégio é o processo de obter mais permissões do que se tinha originalmente.

Existem dois tipos comuns:

- **vertical:** quando um usuário comum obtém privilégios administrativos;
- **horizontal:** quando um usuário acessa permissões de outro usuário no mesmo nível de privilégio.

Este cenário demonstra escalonamento vertical:

```text
bob → root
```

---

### 9.2 Escalonamento local

O cenário é de **escalonamento local**, porque o jogador já possui acesso ao sistema como `bob`.

Isso é diferente de uma exploração remota inicial. Aqui, o objetivo não é invadir o SSH nem quebrar senha. O acesso inicial já foi concedido pelo laboratório.

O foco está em descobrir o que pode ser feito dentro do sistema após obter uma sessão de usuário comum.

---

## 10. Enumeração local

### 10.1 O que é enumeração?

Enumeração é o processo de coletar informações sobre o ambiente.

Em pentest, enumeração é uma etapa essencial. Antes de explorar algo, o analista precisa observar:

- qual usuário está usando;
- quais permissões possui;
- quais comandos pode executar;
- quais serviços estão rodando;
- qual sistema operacional está sendo usado;
- quais arquivos e diretórios são acessíveis;
- quais configurações parecem inseguras.

---

### 10.2 Comandos úteis para este cenário

Verificar usuário atual:

```bash
whoami
```

Verificar UID, GID e grupos:

```bash
id
```

Verificar diretório atual:

```bash
pwd
```

Listar arquivos:

```bash
ls -la
```

Verificar sistema:

```bash
uname -a
```

Listar permissões sudo:

```bash
sudo -l
```

Encontrar caminho de um binário:

```bash
which less
```

Verificar se está como root após a exploração:

```bash
whoami
id
```

---

## 11. Fluxo conceitual do ataque no cenário

O cenário pode ser entendido como a sequência abaixo:

```text
1. O jogador entra no sistema como bob.
2. O jogador enumera suas permissões.
3. O comando sudo -l revela permissão especial para /usr/bin/less.
4. O jogador executa o less via sudo.
5. O less abre com privilégios de root.
6. Dentro do less, o jogador executa um shell escape.
7. Um shell é aberto como root.
8. O jogador confirma o privilégio com whoami ou id.
```

Representação simplificada:

```text
Usuário comum
    |
    | sudo -l
    v
Descoberta de permissão insegura
    |
    | sudo less /etc/hosts
    v
Programa interativo executando como root
    |
    | !/bin/sh
    v
Shell root
```

---

## 12. O papel do Docker no cenário

### 12.1 Por que usar Docker?

Docker permite criar ambientes isolados e reproduzíveis.

Neste cenário, o Docker é usado para criar uma máquina Linux vulnerável sem comprometer o sistema real do jogador.

A imagem do cenário instala as dependências necessárias, cria o usuário `bob`, configura o SSH e adiciona a regra vulnerável no `sudoers`.

---

### 12.2 Container como ambiente de laboratório

O container funciona como uma máquina de laboratório.

Isso traz vantagens importantes:

- o ambiente pode ser recriado facilmente;
- todos os jogadores recebem o mesmo cenário;
- erros podem ser corrigidos reiniciando o container;
- a vulnerabilidade fica isolada;
- o sistema real do usuário não precisa ser alterado.

---

### 12.3 SSH no cenário

O SSH é usado para simular acesso remoto à máquina.

O jogador se conecta com:

```bash
ssh bob@localhost -p 2222
```

Nesse caso:

- `bob` é o usuário;
- `localhost` é a máquina local;
- `-p 2222` indica a porta mapeada no host;
- dentro do container, o serviço SSH escuta na porta 22.

Essa estrutura simula um cenário realista: o jogador acessa uma máquina como usuário comum e, a partir dali, realiza enumeração e escalonamento local.

---

## 13. Impacto da vulnerabilidade

A má configuração permite que um usuário comum obtenha um shell como root.

Com isso, em um ambiente real, um atacante poderia:

- ler arquivos restritos;
- modificar configurações do sistema;
- criar novos usuários;
- alterar senhas;
- instalar ferramentas;
- desativar logs;
- manter persistência;
- comprometer outros serviços;
- destruir ou alterar dados.

No laboratório, o objetivo não é causar dano, mas compreender por que uma configuração aparentemente pequena pode ter impacto total no sistema.

---

## 14. Como corrigir ou evitar essa falha

### 14.1 Não permitir programas interativos perigosos via sudo

A melhor correção é não permitir que usuários comuns executem programas interativos como root, especialmente quando esses programas possuem recursos de shell escape.

Evite regras como:

```sudoers
usuario ALL=(ALL) NOPASSWD: /usr/bin/less
usuario ALL=(ALL) NOPASSWD: /usr/bin/vim
usuario ALL=(ALL) NOPASSWD: /usr/bin/nano
usuario ALL=(ALL) NOPASSWD: /usr/bin/man
```

---

### 14.2 Restringir comandos de forma específica

Se o objetivo for permitir que um usuário leia um arquivo específico, é melhor criar uma solução mais restrita.

Por exemplo, em vez de permitir `less` livremente como root, pode-se pensar em alternativas como:

- ajustar permissões de leitura do arquivo específico;
- criar um grupo com permissão limitada;
- criar um script controlado e muito bem validado;
- evitar uso de programas interativos;
- registrar ações administrativas.

---

### 14.3 Evitar `NOPASSWD` sem necessidade

O `NOPASSWD` pode ser útil em automações, mas deve ser usado com muito cuidado.

Em ambientes reais, exigir senha pode não impedir todos os ataques, mas adiciona uma barreira importante, especialmente quando a sessão do usuário foi comprometida.

---

### 14.4 Usar `visudo`

O arquivo `sudoers` deve ser editado com:

```bash
visudo
```

O `visudo` valida a sintaxe antes de salvar, reduzindo o risco de quebrar a configuração do `sudo`.

Para arquivos dentro de `/etc/sudoers.d/`, pode-se usar:

```bash
visudo -f /etc/sudoers.d/nome-do-arquivo
```

---

### 14.5 Considerar modos restritivos

O `less` possui um modo mais restritivo por meio da variável `LESSSECURE=1`, que desativa alguns recursos perigosos, incluindo execução de shell. Porém, para segurança, a melhor abordagem ainda é evitar conceder execução privilegiada a programas interativos quando isso não for estritamente necessário.

---

## 15. Termos importantes

### `root`

Usuário administrativo máximo em sistemas Linux.

### `sudo`

Ferramenta que permite executar comandos com privilégios de outro usuário, normalmente `root`.

### `sudoers`

Arquivo ou conjunto de arquivos que define as permissões do `sudo`.

### `NOPASSWD`

Opção do `sudoers` que permite executar comandos via `sudo` sem solicitar senha.

### `less`

Visualizador de arquivos em terminal.

### Shell

Interpretador de comandos, como `/bin/sh` ou `/bin/bash`.

### Shell escape

Técnica de sair de um programa interativo para executar comandos em um shell.

### Escalonamento de privilégio

Processo de obter permissões maiores do que as originalmente concedidas.

### Enumeração

Processo de coletar informações sobre o sistema para identificar possíveis caminhos de exploração.

---

## 16. Perguntas para fixação

1. Qual é a diferença entre um usuário comum e o usuário `root`?
2. Para que serve o comando `sudo`?
3. O que o comando `sudo -l` mostra?
4. O que significa `NOPASSWD` em uma regra do `sudoers`?
5. Por que permitir `/usr/bin/less` via `sudo` pode ser perigoso?
6. O que é um shell escape?
7. Por que o shell aberto pelo `less` pode herdar privilégios de root?
8. Qual princípio de segurança foi violado nesse cenário?
9. Como essa vulnerabilidade poderia ser evitada?
10. Por que Docker é útil para laboratórios de pentest?

---

## 17. Resumo final

Este cenário ensina que configurações incorretas de `sudo` podem levar a comprometimento total do sistema.

O erro principal é permitir que o usuário `bob` execute o programa interativo `less` como `root` sem senha. Embora `less` seja apenas um visualizador de arquivos, ele possui recursos internos capazes de executar comandos do shell.

Assim, o jogador aprende que:

- enumeração é essencial;
- `sudo -l` pode revelar caminhos de escalação;
- programas interativos podem ser perigosos quando executados como root;
- shell escape é uma técnica simples, mas poderosa;
- o princípio do menor privilégio deve ser aplicado com rigor;
- uma regra pequena no `sudoers` pode ter impacto crítico.

O aprendizado principal do laboratório é que segurança não depende apenas de impedir exploits complexos. Muitas vezes, o problema está em permissões excessivas, configurações descuidadas e falta de compreensão sobre o comportamento dos programas permitidos.
