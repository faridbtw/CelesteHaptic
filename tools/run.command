#!/bin/bash
set -euo pipefail
base="$(cd "$(dirname "$0")/.." && pwd)"
action="${1:-status}"
game="${2:-$HOME/Library/Application Support/Steam/steamapps/common/Celeste}"
if [[ "$(uname -m)" == arm64 ]]; then arch=arm64; else arch=x64; fi
"$base/dist/installer-$arch/Installer" "$action" "$game" "$base/dist/payload"
