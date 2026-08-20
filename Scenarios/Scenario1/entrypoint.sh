#!/bin/bash

# Iniciar o serviço SSH
service ssh start

# Manter o container em execução sem depender de um terminal interativo
exec sleep infinity
