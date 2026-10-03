#!/usr/bin/env bash
set -euo pipefail

# Stopwatch Overlay Linux Uninstallation Script
BIN_NAME="stopwatch-overlay"

if [[ $EUID -eq 0 ]]; then
    PREFIX="/usr/local"
else
    PREFIX="$HOME/.local"
fi

rm -f "$PREFIX/bin/$BIN_NAME"
rm -f "$PREFIX/share/applications/stopwatch-overlay.desktop"
rm -f "$PREFIX/share/icons/hicolor/256x256/apps/stopwatch-overlay.png"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$PREFIX/share/applications" || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q "$PREFIX/share/icons/hicolor" || true
fi

echo "✓ Uninstalled $BIN_NAME from $PREFIX"
