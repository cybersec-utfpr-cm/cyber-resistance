#!/usr/bin/env bash
# =============================================================================
# Coletor de metricas no servidor central - TCC Cyber Resistance.
# Versao SEM sudo (Linux Mint do laboratorio, sem privilegios elevados).
#
# Inicia o Godot ANTES de rodar este script (pgrep precisa achar o processo).
#
# Uso:
#   chmod +x eval_scripts/collect_server.sh
#   eval_scripts/collect_server.sh [porta_lobby]   # default 42069
#
# Para interromper: Ctrl+C. Os arquivos sao fechados corretamente.
#
# Saidas em eval_results/server_<timestamp>/ :
#   meta.txt        - timestamps de inicio/fim, porta, hostname
#   cpu_mem.csv     - CPU%/RSS do(s) processo(s) Godot, 1Hz (via ps)
#   net_iface.csv   - bytes e pacotes por interface, 1Hz (via /proc/net/dev)
#
# LIMITACOES (versao sem sudo):
#   - Banda eh POR INTERFACE, nao por processo. Como na sessao de teste so
#     o Godot trafega volume significativo, a banda da interface aproxima a
#     do processo.
#   - Sem captura de pacotes (tshark precisa de CAP_NET_RAW). RTT/perda
#     saem do log interno do Godot em ~/.local/share/godot/app_userdata/<projeto>/
#     enet_stats_*.csv.
# =============================================================================

set -u

PORT="${1:-42069}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SESS="server_$(date +%Y%m%d_%H%M%S)"
OUT="$ROOT/eval_results/$SESS"
mkdir -p "$OUT"

echo "[*] sessao: $SESS"
echo "[*] saida:  $OUT"
echo "[*] porta:  $PORT (registrada em meta.txt)"

{
    echo "start_unix=$(date +%s)"
    echo "start_iso=$(date -Iseconds)"
    echo "port=$PORT"
    echo "host=$(hostname)"
} > "$OUT/meta.txt"

# -- CPU/memoria 1Hz --
# pgrep -f casa tanto o binario exportado ("Cyber Resistance...") quanto o editor.
{
    echo "timestamp,pid,cpu_pct,rss_kb,cmd"
    while true; do
        ts=$(date +%s)
        pgrep -f "Resistance|[Gg]odot" | while read -r pid; do
            ps -p "$pid" -o pid=,pcpu=,rss=,comm= 2>/dev/null \
                | awk -v t="$ts" 'NF{print t","$1","$2","$3","$4}'
        done
        sleep 1
    done
} > "$OUT/cpu_mem.csv" 2>/dev/null &
PID_CM=$!

# -- Banda/pacotes por interface 1Hz (delta de /proc/net/dev) --
# Formato de /proc/net/dev (linhas a partir da 3a):
#   "  eth0: <rx_bytes> <rx_packets> ... <tx_bytes> <tx_packets> ..."
# Pegamos rx_bytes ($1), rx_packets ($2), tx_bytes ($9), tx_packets ($10)
# e gravamos o delta entre amostras consecutivas (= bytes/s e pacotes/s).
{
    echo "timestamp,iface,rx_bps,rx_pps,tx_bps,tx_pps"
    declare -A prev_rx_b prev_rx_p prev_tx_b prev_tx_p
    while true; do
        ts=$(date +%s)
        while IFS= read -r line; do
            [[ "$line" =~ ^[[:space:]]*([^:]+):[[:space:]]+(.+)$ ]] || continue
            iface="${BASH_REMATCH[1]// /}"
            [[ "$iface" == "lo" ]] && continue
            vals="${BASH_REMATCH[2]}"
            read -r rx_b rx_p _ _ _ _ _ _ tx_b tx_p _ <<< "$vals"
            if [[ -n "${prev_rx_b[$iface]:-}" ]]; then
                printf "%d,%s,%d,%d,%d,%d\n" "$ts" "$iface" \
                    "$((rx_b - prev_rx_b[$iface]))" \
                    "$((rx_p - prev_rx_p[$iface]))" \
                    "$((tx_b - prev_tx_b[$iface]))" \
                    "$((tx_p - prev_tx_p[$iface]))"
            fi
            prev_rx_b[$iface]=$rx_b
            prev_rx_p[$iface]=$rx_p
            prev_tx_b[$iface]=$tx_b
            prev_tx_p[$iface]=$tx_p
        done < /proc/net/dev
        sleep 1
    done
} > "$OUT/net_iface.csv" 2>/dev/null &
PID_NI=$!

cleanup() {
    echo
    echo "[*] parando coleta..."
    kill -TERM "$PID_CM" "$PID_NI" 2>/dev/null
    wait 2>/dev/null
    {
        echo "end_unix=$(date +%s)"
        echo "end_iso=$(date -Iseconds)"
    } >> "$OUT/meta.txt"
    echo "[*] OK. Arquivos em: $OUT"
    exit 0
}
trap cleanup INT TERM

echo "[*] coletando (Ctrl+C para parar)..."
wait
