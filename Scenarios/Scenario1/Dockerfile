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