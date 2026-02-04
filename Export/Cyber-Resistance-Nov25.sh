#!/bin/sh
echo -ne '\033c\033]0;Cyber-Resistance-Nov25\a'
base_path="$(dirname "$(realpath "$0")")"
"$base_path/Cyber-Resistance-Nov25.x86_64" "$@"
