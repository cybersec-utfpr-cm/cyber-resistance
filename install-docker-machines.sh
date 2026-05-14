#!/usr/bin/env bash

set -e

echo "Instalando as maquinas Docker do Cyber Resistance..."
echo "Modo de instalacao limpa: containers existentes serao removidos."

echo
echo "Removendo containers antigos, se existirem..."

if docker container inspect player_machine > /dev/null 2>&1; then
    echo "Removendo container antigo 'player_machine'..."
    docker rm -f player_machine
else
    echo "Container 'player_machine' nao encontrado. Nada a remover."
fi

if docker container inspect scenario1 > /dev/null 2>&1; then
    echo "Removendo container antigo 'scenario1'..."
    docker rm -f scenario1
else
    echo "Container 'scenario1' nao encontrado. Nada a remover."
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

echo "Criando container do cenario 1..."
docker create \
--name scenario1 \
-p 127.0.0.1:5001:22 \
scenario1

echo
echo "Maquinas Docker instaladas com sucesso."
echo
echo "Containers preparados:"
echo "  - player_machine -> telnet localhost 5000"
echo "  - scenario1      -> ssh bob@localhost -p 5001"
echo
echo "Observacao: este script apenas instala/prepara as maquinas."
echo "O jogo sera responsavel por iniciar e parar os containers quando necessario."