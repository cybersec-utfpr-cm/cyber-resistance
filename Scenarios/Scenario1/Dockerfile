FROM ubuntu:22.04

# Instalar dependências do cenário
RUN apt-get update && \
    apt-get install -y \
    sudo \
    less \
    openssh-server \
    && rm -rf /var/lib/apt/lists/*

# Diretório necessário para o sshd iniciar corretamente
RUN mkdir -p /run/sshd

# Criar usuário 'bob' e definir senha
RUN useradd -m -s /bin/bash bob && \
    echo 'bob:password' | chpasswd

# Configurar sudoers para permitir less como root sem senha
COPY sudoers.bob /etc/sudoers.d/bob
RUN chmod 0440 /etc/sudoers.d/bob

# Configurar SSH
RUN echo 'PasswordAuthentication yes' >> /etc/ssh/sshd_config && \
    echo 'PermitRootLogin no' >> /etc/ssh/sshd_config

# Arquivo final do cenário, acessível apenas após obter root
RUN printf '%s\n' \
    'Você encontrou a prova perdida de Sistemas Operacionais...' \
    '' \
    'Mas calma: a verdadeira prova era entender permissões, processos e privilégios até chegar aqui.' \
    '' \
    'Agora volte e estude. :)' \
    > /root/prova-sistemas-operacionais.txt && \
    chmod 600 /root/prova-sistemas-operacionais.txt

EXPOSE 22

CMD ["/usr/sbin/sshd", "-D"]