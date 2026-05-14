#!/usr/bin/env bash

set -e

echo "Instalando as maquinas Docker do Cyber Resistance..."

echo
echo "Criando imagem da maquina do jogador..."
docker build -t player_machine -f Dockerfiles/Dockerfile.player Dockerfiles/

echo "Criando container da maquina do jogador..."
if docker container inspect player_machine > /dev/null 2>&1; then
    echo "O container 'player_machine' ja existe. Pulando criacao."
else
    docker create \
    --name player_machine \
    -p 127.0.0.1:5000:23 \
    player_machine
fi

echo
echo "Criando imagem do cenario 1..."
docker build -t scenario1 -f Scenarios/Scenario1/Dockerfile Scenarios/Scenario1/

echo "Criando container do cenario 1..."
if docker container inspect scenario1 > /dev/null 2>&1; then
    echo "O container 'scenario1' ja existe. Pulando criacao."
else
    docker create \
    --name scenario1 \
    -p 127.0.0.1:5001:22 \
    scenario1
fi

echo
echo "Máquinas Docker instaladas com sucesso."
echo
echo "Containers preparados:"
echo "  - player_machine -> telnet localhost 5000"
echo "  - scenario1      -> ssh bob@localhost -p 5001"
echo
echo "Observação: este script apenas instala/prepara as máquinas."
echo "O jogo sera responsavel por iniciar e parar os containers quando necessario."