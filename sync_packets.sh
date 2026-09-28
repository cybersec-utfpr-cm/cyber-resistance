#!/usr/bin/env bash
# sync_packets.sh
# Copia shared/packets/ -> scripts/packets/ de cada projeto Godot do repo.
# Equivalente Linux/macOS do sync_packets.ps1. Mesma semantica:
#   - Pula source que nao existe.
#   - Pula target cujo project.godot ancestor nao foi encontrado.
#   - Sobrescreve arquivos existentes. NAO espelha (renomear em shared/ deixa o
#     antigo no target; remova manualmente se necessario).
#
# Uso (no diretorio raiz do repo):
#     ./sync_packets.sh
# Se nao tiver permissao de execucao: chmod +x sync_packets.sh

set -euo pipefail

# Diretorio do script (raiz do repo).
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Cores ANSI; '' se TERM nao suportar.
if [[ -t 1 ]]; then
    green=$'\033[32m'; yellow=$'\033[33m'; cyan=$'\033[36m'; reset=$'\033[0m'
else
    green=''; yellow=''; cyan=''; reset=''
fi

# Sobe pelos pais de $1 ate achar um project.godot; ecoa o caminho ou nada.
find_project_root() {
    local cur
    cur="$(dirname "$1")"
    while [[ -n "$cur" && "$cur" != "/" && "${#cur}" -ge "${#repo_root}" ]]; do
        if [[ -f "$cur/project.godot" ]]; then
            echo "$cur"
            return 0
        fi
        cur="$(dirname "$cur")"
    done
    return 1
}

# Cada sync: "source_rel|target_label1=target_rel1|target_label2=target_rel2|..."
syncs=(
    "shared/packets|server=server/scripts/packets|professor=professor/scripts/packets|client=client/prototype/scripts/packets"
    "shared/data|professor=professor/data|client=client/prototype/data"
)

for entry in "${syncs[@]}"; do
    IFS='|' read -r -a parts <<< "$entry"
    source_rel="${parts[0]}"
    source_abs="$repo_root/$source_rel"

    if [[ ! -d "$source_abs" ]]; then
        printf '  %s[skip src] %s nao existe%s\n' "$yellow" "$source_rel" "$reset"
        continue
    fi

    for ((i = 1; i < ${#parts[@]}; i++)); do
        target_spec="${parts[i]}"
        target_name="${target_spec%%=*}"
        target_rel="${target_spec#*=}"
        target_abs="$repo_root/$target_rel"

        if ! project_root="$(find_project_root "$target_abs")"; then
            printf '  %s[skip]   %s (%s): project.godot nao encontrado em %s%s\n' \
                "$yellow" "$target_name" "$source_rel" "$target_rel" "$reset"
            continue
        fi

        mkdir -p "$target_abs"
        cp -R "$source_abs"/. "$target_abs"/
        printf '  %s[ok]     %s (%s) -> %s%s\n' \
            "$green" "$target_name" "$source_rel" "$target_rel" "$reset"
    done
done

printf '\n%ssync concluido.%s\n' "$cyan" "$reset"
