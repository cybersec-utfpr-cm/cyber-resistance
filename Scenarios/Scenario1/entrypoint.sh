#!/bin/bash

# Iniciar o serviço SSH
service ssh start

# Manter o container em execução com shell interativo
exec /bin/bash