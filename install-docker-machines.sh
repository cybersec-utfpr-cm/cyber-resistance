#!/usr/bin/env bash

# Máquina do player
docker build -t player_machine -f Dockerfiles/Dockerfile.player Dockerfiles/
docker create --name player_machine -p 5000:23 player_machine

# Máquina do cenário 1
docker build -t scenario1 -f Scenarios/Scenario1/Dockerfile Scenarios/Scenario1/
docker create --name scenario1 -p 5001:22 scenario1
