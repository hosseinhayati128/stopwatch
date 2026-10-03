#!/usr/bin/env bash
set -euo pipefail

# Stopwatch Overlay Linux Installation Script
# Usage:
#   ./install.sh          # Installs for current user (~/.local)
#   sudo ./install.sh     # Installs system-wide (/usr/local)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_NAME="stopwatch-overlay"

if [[ $EUID -eq 0 ]]; then
    PREFIX="/usr/local"
    BIN_DIR="$PREFIX/bin"
    APP_DIR="$PREFIX/share/applications"
    ICON_DIR="$PREFIX/share/icons/hicolor"
    echo "Installing system-wide into $PREFIX..."
else
    PREFIX="$HOME/.local"
    BIN_DIR="$PREFIX/bin"
    APP_DIR="$PREFIX/share/applications"
    ICON_DIR="$PREFIX/share/icons/hicolor"
    echo "Installing for current user into $PREFIX..."
fi

mkdir -p "$BIN_DIR" "$APP_DIR" "$ICON_DIR/scalable/apps" "$ICON_DIR/256x256/apps"

# 1. Install executable
if [[ -f "$SCRIPT_DIR/$BIN_NAME" ]]; then
    cp "$SCRIPT_DIR/$BIN_NAME" "$BIN_DIR/$BIN_NAME"
elif [[ -f "$SCRIPT_DIR/StopwatchOverlay.Desktop" ]]; then
    cp "$SCRIPT_DIR/StopwatchOverlay.Desktop" "$BIN_DIR/$BIN_NAME"
else
    echo "Error: Binary not found in $SCRIPT_DIR." >&2
    exit 1
fi
chmod +x "$BIN_DIR/$BIN_NAME"
echo "✓ Installed executable to $BIN_DIR/$BIN_NAME"

# 2. Install desktop entry
sed "s|Exec=stopwatch-overlay|Exec=$BIN_DIR/$BIN_NAME|g" "$SCRIPT_DIR/stopwatch-overlay.desktop" > "$APP_DIR/stopwatch-overlay.desktop"
chmod 644 "$APP_DIR/stopwatch-overlay.desktop"
echo "✓ Installed desktop entry to $APP_DIR/stopwatch-overlay.desktop"

# 3. Install icon if available
if [[ -f "$SCRIPT_DIR/stopwatch-overlay.png" ]]; then
    cp "$SCRIPT_DIR/stopwatch-overlay.png" "$ICON_DIR/256x256/apps/stopwatch-overlay.png"
    echo "✓ Installed icon to $ICON_DIR/256x256/apps/stopwatch-overlay.png"
fi

# 4. Update desktop and icon databases if tools available
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APP_DIR" || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q "$ICON_DIR" || true
fi

echo ""
echo "Installation complete! You can run the application by typing: $BIN_NAME"
