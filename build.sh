#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
game="${CELESTE_GAME:-$HOME/Library/Application Support/Steam/steamapps/common/Celeste}"
resources="$game/Celeste.app/Contents/Resources"
dotnet_exe="${DOTNET_EXE:-dotnet}"
mkdir -p dist/payload
csc -nologo -target:library -optimize+ -out:dist/payload/CelesteHaptic.dll \
  -reference:"$resources/Celeste.exe" -reference:"$resources/FNA.dll" \
  src/Adapter/Synth.cs src/Adapter/Hooks.cs
xcrun clang -std=c11 -O2 -Wall -Wextra -Werror -arch arm64 -arch x86_64 \
  -mmacosx-version-min=11.0 -framework AudioUnit -framework CoreAudio \
  -framework CoreFoundation -framework IOKit -lz \
  src/Bridge/HapticsBridge.c src/Bridge/BluetoothHaptics.c -o dist/payload/HapticsBridge
codesign --force --sign - dist/payload/HapticsBridge
for arch in arm64 x64; do
  "$dotnet_exe" publish src/Installer/Installer.csproj -c Release -r "osx-$arch" --self-contained true -o "dist/installer-$arch"
  codesign --force --sign - "dist/installer-$arch/Installer"
done
