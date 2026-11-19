# Cafeteria — patch v4 (YSort e colisões)

Este patch substitui a cena interna da cafeteria e seu controlador de YSort.
Ele também inclui novamente os dois PNGs v3, portanto pode ser aplicado sobre a
versão anterior sem copiar arquivos manualmente.

## Aplicar no Linux/macOS

```bash
cd /caminho/para/cyber-resistance-project-restart
unzip -o /caminho/para/cyber-resistance-cafeteria-ysort-colisoes-v4-patch.zip -d .
dotnet build
godot --path . --editor
```

Para iniciar o jogo diretamente:

```bash
cd /caminho/para/cyber-resistance-project-restart
godot --path .
```

Se a instalação .NET do Godot usar outro executável, troque `godot` por
`godot-mono` ou pelo caminho do Godot 4 .NET.

## Aplicar no Windows PowerShell

```powershell
Set-Location "C:\caminho\para\cyber-resistance-project-restart"
Expand-Archive -Path "C:\caminho\para\cyber-resistance-cafeteria-ysort-colisoes-v4-patch.zip" -DestinationPath . -Force
dotnet build
& "C:\caminho\para\Godot_v4.x-stable_mono_win64.exe" --path . --editor
```

## Alterações principais

- Corrige a profundidade da vitrine de doces separando máquina, vitrine,
  bancada do caixa e frente de madeira em quatro faixas de YSort.
- Adiciona 42 polígonos de colisão: 16 para paredes/arquitetura e 26 para
  mesas, balcões, cozinha, escritório, plantas e demais móveis.
- Mantém livres a entrada, a passagem lateral do balcão, o corredor de trabalho
  até a cozinha e a porta do escritório.
- Reposiciona pontos internos que ficariam sobre móveis sólidos.
