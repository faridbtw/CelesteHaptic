#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
if pgrep -x Celeste >/dev/null; then
  echo 'Quit Celeste before running the standalone controller test.'
  exit 1
fi
mkdir -p test-logs
cd test-logs
../dist/payload/HapticsBridge --test --transport bluetooth
if [[ -t 0 ]]; then read -r -p 'Press Return to close. '; fi
