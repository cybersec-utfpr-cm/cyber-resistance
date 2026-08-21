# Audio do Cyber Resistance

Esta pasta contém a primeira trilha sonora funcional do projeto. Os arquivos
foram sintetizados especialmente para esta versão e não dependem de material
externo ou de licenças de terceiros.

## Organização

- `Music/menu_theme.ogg`: música do menu principal;
- `Music/gameplay_theme.ogg`: música do jogo;
- `Ambience/world_ambience.ogg`: ambientação das áreas externas;
- `Ambience/indoor_ambience.ogg`: ambientação do escritório e da cafeteria;
- `SFX/`: efeitos de interface, pausa, passos, portas, interações e respostas.

O `AudioManager` carrega esses caminhos diretamente. Para substituir uma faixa
sem alterar o código, exporte o novo áudio no mesmo formato e mantenha o mesmo
nome. Músicas e ambientações são repetidas automaticamente em loop.

Os volumes são separados nos barramentos `Master`, `Music`, `Ambience` e `SFX`.
As preferências do jogador são salvas em `user://audio_settings.cfg`.
