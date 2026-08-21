[sphinx_cyber_resistance]: https://github.com/JSangaleti/Sphinx_Cyber_Resistance

# Instruções para Instalação e Execução do Jogo

Este arquivo descreve as instruções para instalação e execução do jogo _Cyber Resistance_ em sistemas Linux.

## 1. Instalação do ASP.NET Core Runtime 8.0

> Nota: Aqui é descrito como instalar o runtime em Debian 12. Para outras distribuições, consulte: [Install .NET Core on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux). Para outros sistemas, consulte: [Install .NET on Windows, Linux, and macOS](https://learn.microsoft.com/en-us/dotnet/core/install/).

1.1 Adicione a chave de assinatura do pacote Microsoft para a sua lista de chaves confiáveis e adicione o repositório do pacote:

```bash
wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
```

1.2 Instale o ASP.NET Core Runtime 8.0:

```bash
sudo apt-get update && \
  sudo apt-get install -y aspnetcore-runtime-8.0
```

<details><summary>Em caso de erro durante a instalação.</summary>
É possível que, durante a instalação do runtime, você se depare com algum erro como:

```
E: Unable to locate package aspnetcore-runtime-8.0
```

Nesse caso, podemos seguir as instruções apresentadas [aqui](https://stackoverflow.com/questions/77498786/unable-to-locate-package-dotnet-sdk-8-0) que podem ser resumidas em:

1. Remova outras instalações do dotnet-sdk, dotnet-runtime e dotnet-host:

    ```bash
    sudo apt-get remove dotnet-sdk-<version>
    sudo apt-get remove dotnet-runtime-<version>
    sudo apt-get remove dotnet-host-<version>
    ```

2. Execute os comandos a seguir para adicionar o repositório de origem do pacote:

    ```bash
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    ```

3. Execute os comandos a seguir para instalar o dotnet-host e o dotnet-sdk:

    ```bash
    sudo apt-get update
    sudo apt-get install -y dotnet-host aspnetcore-runtime-8.0
    ```

</details>

## 2. Instalação da Engine Godot

2.1 Acesse [esta pasta do Drive](https://drive.google.com/drive/folders/1714fKAKL5njbq_gfIZOWOJOOGB9gtZho) e faça download do aquivo `Godot_v4.4.1-stable_mono_linux_x86_64.zip`;

2.2 Descompacte o arquivo:

```bash
unzip Godot_v4.4.1-stable_mono_linux_x86_64.zip
```

## 3. Instalação da Build "Beta" do Jogo

3.1 Acesse a mesma [pasta do Drive](https://drive.google.com/drive/folders/1714fKAKL5njbq_gfIZOWOJOOGB9gtZho) e faça download do aquivo `Cyber_Resistance.zip`;

3.2 Descompacte o arquivo:

```bash
unzip Cyber_Resistance.zip
```

## 4. Instalação das Máquinas Docker

Antes de executar o jogo, é necessário preparar as máquinas Docker usadas pelos terminais e cenários do _Cyber Resistance_.

Esse processo deve ser feito apenas uma vez, ou sempre que for necessário recriar os containers do jogo.

Na raiz do repositório, execute:

```bash
chmod +x install-docker-machines.sh
./install-docker-machines.sh
```

## 5. Execução do Jogo

4.1 Execute a engine Godot:

```bash
./Godot_v4.4.1-stable_mono_linux_x86_64/Godot_v4.4.1-stable_mono_linux_x86_64
```

4.2 Importe o projeto do jogo: Clique em "Import" e seleccione o diretório extraído do arquivo `Cyber_Resistance.zip`.

- Obs.: Neste momento pode ocorrer um erro, mas basta reiniciar a engine.

4.3 Execute o jogo: Clique no projeto para selecioná-lo e clique em "Run".

## 6. Inicialização da Máquina Docker do Jogador

A máquina Docker do jogador é inicializada quando a cena principal do jogo é carregada (via `GameManager`).

- Script responsável: `Scripts/Core/GameManager.cs`;
- Nome padrão do container: `player_machine`;
- O nome pode ser alterado no Inspector do nó `GameManager` pelo campo `PlayerMachineContainerName`.

Com isso, abrir/fechar a tela de terminal não liga/desliga a máquina; o terminal apenas se conecta ao ambiente já ativo.

## 7. Áudio e menu de pausa

Durante o jogo, pressione `ESC` para pausar. O menu permite continuar, voltar ao
menu principal e controlar separadamente:

- volume geral;
- música;
- ambientação;
- efeitos sonoros.

Os volumes são preservados entre execuções. Se uma interface modal estiver
aberta, como computador, estante, quiz ou prova, o primeiro `ESC` fecha essa
interface; o `ESC` seguinte abre o menu de pausa.

Os arquivos de áudio ficam em `Assets/Audio`. Consulte o README dessa pasta para
substituir músicas ou efeitos mantendo a integração existente.


# Código-fonte: Projeto Cyber Resistance

## Normas de Padronização (NPs)

> Para qualquer um que desejar contribuir com o código do projeto, algumas normas de padronização deverão ser seguidas, a fim de manter uma organização do código e das pastas para que o projeto tenha melhor manutenção e legibilidade.


### 1. Normas gerais

- **NP1.1** Nunca utilizar acentos ou 'ç' (exceto em comentários e mensagens de *commit*);
- **NP1.2** Tudo deverá ser escrito em inglês (exceto em comentários e mensagens de *commit*).

### 2. Quanto à organização de pastas e arquivos

- **NP2.1:** Nomes de pastas e arquivos devem iniciar com letra minúscula. Se o nome tiver mais de uma palavra, não deve haver espaçamento entre as palavras e cada palavra *após a primeira* deverá ter sua inicial maiúscula.
> **Exemplo:** folderName || fileName.txt

### 3. Quanto à organização e escrita de código

- **NP3.1A (GDScript)** Nomes de variáveis e funções (inclusive sinais) devem ser escritos com letra minúscula em todas as circunstâncias, e o espaçamento entre as palavras se dá por *underline* (representado pelo caractere \'_\');
> **Exemplo:** function_name() || _signal_name() || variable_name: int = ...
 
- **NP3.1B (C#)** Nomes de variáveis devem ser escritos em letras minúsculas sem espaçamento entre as palavras, exatamente como citado para pastas e arquivos. De forma parecida deve ser feito para as funções, mas com a inicial maiúscula;
> **Exemplo:** FunctionName() || _SignalName() || int variableName = ...

- **NP3.2** Nomes de nós e seus respectivos atributos devem iniciar com letra maiúscula. Se o nome tiver mais de uma palavra, não deve haver espaçamento entre as palavras e cada palavra deverá ter sua inicial maiúscula.
> **Exemplo:** SceneName || NodeName || TileMap

### 4. Quanto aos git-commits

- **NP4.1** A mensagem deverá ser sempre uma frase breve; maiores detalhamentos sobre as alterações realizadas (cuja presença deve ser crucial em grandes modificações) deverão ser realizados na opção de descrição extendida. *[Maiores detalhes aqui!](https://www.freecodecamp.org/news/writing-good-commit-messages-a-practical-guide/)*
> **Exemplo (via linha de comando de terminal):** `git commit -m "Frase breve e resumida" -m "Descrição detalhada..."`

# [> Documentação do Projeto](https://jsangaleti.github.io/Sphinx_Cyber_Resistance/)
