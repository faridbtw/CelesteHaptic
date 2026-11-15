#!/bin/bash
cd "$(dirname "$0")"
./tools/run.command uninstall "${1:-$HOME/Library/Application Support/Steam/steamapps/common/Celeste}"
result=$?
if [[ -t 0 ]]; then read -r -p 'Press Return to close. '; fi
exit "$result"
