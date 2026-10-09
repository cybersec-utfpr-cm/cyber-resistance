#!/usr/bin/env bash
# =============================================================================
# Coleta a configuracao da maquina de teste - TCC Cyber Resistance.
#
# Como as 5 maquinas do laboratorio sao identicas, basta rodar em UMA.
# Nao precisa de sudo.
#
# Uso:
#   chmod +x eval_scripts/collect_specs.sh
#   eval_scripts/collect_specs.sh
#
# Saida: eval_results/specs_<hostname>_<timestamp>.txt
# =============================================================================

set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/eval_results"
mkdir -p "$OUT"
FILE="$OUT/specs_$(hostname)_$(date +%Y%m%d_%H%M%S).txt"

{
    echo "=== Maquina ==="
    echo "hostname:   $(hostname)"
    echo "data:       $(date -Iseconds)"
    echo

    echo "=== Sistema operacional ==="
    if [ -r /etc/os-release ]; then
        . /etc/os-release
        echo "distro:     ${PRETTY_NAME:-?}"
    fi
    echo "kernel:     $(uname -srm)"
    echo

    echo "=== CPU ==="
    lscpu 2>/dev/null \
        | grep -E "^Model name|^Architecture|^CPU\(s\):|^Thread\(s\) per core|^Core\(s\) per socket|^Socket\(s\)|^CPU max MHz|^CPU min MHz" \
        | sed 's/^/    /'
    echo

    echo "=== Memoria ==="
    free -h 2>/dev/null | awk 'NR<=2'
    echo

    echo "=== GPU ==="
    if command -v lspci >/dev/null 2>&1; then
        lspci 2>/dev/null | grep -iE "vga|3d|display" | sed 's/^/    /'
    else
        echo "    (lspci nao instalado)"
    fi
    echo

    echo "=== Interfaces de rede ==="
    for iface in $(ls /sys/class/net 2>/dev/null | grep -vE "^lo$"); do
        speed=$(cat "/sys/class/net/$iface/speed" 2>/dev/null || echo "?")
        state=$(cat "/sys/class/net/$iface/operstate" 2>/dev/null || echo "?")
        printf "    %-12s speed=%sMbps  state=%s\n" "$iface" "$speed" "$state"
    done
    echo

    echo "=== Disco (root) ==="
    df -h / 2>/dev/null | awk 'NR<=2'
} > "$FILE"

echo "Especificacoes salvas em: $FILE"
echo "---"
cat "$FILE"
