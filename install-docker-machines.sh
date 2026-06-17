#!/usr/bin/env bash

set -e

echo "Instalando as maquinas Docker do Cyber Resistance..."
echo "Modo de instalacao limpa: containers e redes antigas serao removidos."

echo
echo "Removendo containers antigos, se existirem..."

for container in player_machine scenario1 cr_scenario1; do
    if docker container inspect "$container" > /dev/null 2>&1; then
        echo "Removendo container antigo '$container'..."
        docker rm -f "$container"
    else
        echo "Container '$container' nao encontrado. Nada a remover."
    fi
done

echo
echo "Removendo rede antiga da missao, se existir..."

if docker network inspect cr_mission_sudo_with_less > /dev/null 2>&1; then
    docker network rm cr_mission_sudo_with_less
else
    echo "Rede 'cr_mission_sudo_with_less' nao encontrada. Nada a remover."
fi

echo
echo "Criando imagem da maquina do jogador..."
docker build -t player_machine -f Dockerfiles/Dockerfile.player Dockerfiles/

echo "Criando container da maquina do jogador..."
docker create \
--name player_machine \
-p 127.0.0.1:5000:23 \
player_machine

echo
echo "Criando imagem do cenario 1..."
docker build -t scenario1 -f Scenarios/Scenario1/Dockerfile Scenarios/Scenario1/

echo
echo "Maquinas Docker instaladas com sucesso."
echo
echo "Containers preparados:"
echo "  - player_machine -> telnet localhost 5000"
echo
echo "Imagens preparadas:"
echo "  - scenario1"
echo
echo "Observacao:"
echo "  - O script cria apenas a maquina do jogador."
echo "  - O container cr_scenario1 sera criado pelo jogo quando a missao sudo_with_less iniciar."