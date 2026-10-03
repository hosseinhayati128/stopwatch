#!/usr/bin/env bash
set -euo pipefail

# Cross-platform packaging script for Stopwatch Overlay on Linux
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$REPO_ROOT/dist"
CONFIG="${1:-Release}"

echo "========================================"
echo " Stopwatch Overlay Linux Packager"
echo " Config: $CONFIG"
echo "========================================"

mkdir -p "$OUTPUT_DIR"
LINUX_OUT="$OUTPUT_DIR/linux-x64"
rm -rf "$LINUX_OUT"
mkdir -p "$LINUX_OUT"

echo "Building Linux self-contained binary..."
dotnet publish "$REPO_ROOT/StopwatchOverlay.Desktop/StopwatchOverlay.Desktop.csproj" \
    -c "$CONFIG" \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "$LINUX_OUT"

# Copy desktop integration files
PACKAGING_DIR="$REPO_ROOT/packaging/linux"
if [[ -d "$PACKAGING_DIR" ]]; then
    cp "$PACKAGING_DIR/stopwatch-overlay.desktop" "$LINUX_OUT/"
    cp "$PACKAGING_DIR/install.sh" "$LINUX_OUT/"
    cp "$PACKAGING_DIR/uninstall.sh" "$LINUX_OUT/"
    chmod +x "$LINUX_OUT/install.sh" "$LINUX_OUT/uninstall.sh"
fi

LOGO_PNG="$REPO_ROOT/StopwatchOverlay/project-logo-24.png"
if [[ -f "$LOGO_PNG" ]]; then
    cp "$LOGO_PNG" "$LINUX_OUT/stopwatch-overlay.png"
fi

if [[ -f "$LINUX_OUT/StopwatchOverlay.Desktop" ]]; then
    cp "$LINUX_OUT/StopwatchOverlay.Desktop" "$LINUX_OUT/stopwatch-overlay"
    chmod +x "$LINUX_OUT/stopwatch-overlay"
fi

# Create tar.gz archive
ARCHIVE_NAME="stopwatch-overlay-linux-x64.tar.gz"
echo "Creating archive $ARCHIVE_NAME..."
(cd "$OUTPUT_DIR" && tar -czf "$ARCHIVE_NAME" -C "$OUTPUT_DIR" linux-x64)

echo "========================================"
echo " Packaging complete!"
echo " Output archive: $OUTPUT_DIR/$ARCHIVE_NAME"
echo "========================================"
