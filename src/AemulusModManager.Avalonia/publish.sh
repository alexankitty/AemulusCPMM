#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="AemulusModManager.Avalonia.csproj"
OUT_DIR="$SCRIPT_DIR/publish/output"
TARGETS=()

usage() {
    echo "Usage: $0 [--all | --windows | --linux | --appimage]"
    echo ""
    echo "  --all          Build all targets (default if none specified)"
    echo "  --windows      Build Windows x64 zip"
    echo "  --linux        Build Linux x64 tar.gz"
    echo "  --appimage     Build Linux x86_64 AppImage"
    echo "  --dependencies Build project dependencies"
    exit 1
}

# Parse arguments
if [[ $# -eq 0 ]]; then
    TARGETS=(windows linux appimage)
else
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --all)          TARGETS=(windows linux appimage); shift ;;
            --windows)      TARGETS+=(windows); shift ;;
            --linux)        TARGETS+=(linux); shift ;;
            --appimage)     TARGETS+=(appimage); shift ;;
            --dependencies) TARGETS+=(dependencies); shift ;;
            -h|--help)      usage ;;
            *)              echo "Unknown option: $1"; usage ;;
        esac
    done
fi

mkdir -p "$OUT_DIR"

build_windows() {
    echo "==> Publishing Windows x64..."
    dotnet publish "$SCRIPT_DIR/$PROJECT" \
        -c Release \
        -r win-x64 \
        -p:PublishSingleFile=true \
        -p:SelfContained=true \
        -o "$SCRIPT_DIR/publish/win-x64"

    echo "==> Creating Windows archive..."
    (cd "$SCRIPT_DIR/publish/win-x64" && zip -qr "$OUT_DIR/AemulusPackageManager-win-x64.zip" .)
    echo "    -> $OUT_DIR/AemulusPackageManager-win-x64.zip"
}

build_linux() {
    echo "==> Publishing Linux x64..."
    dotnet publish "$SCRIPT_DIR/$PROJECT" \
        -c Release \
        -r linux-x64 \
        -p:PublishSingleFile=true \
        -p:SelfContained=true \
        -o "$SCRIPT_DIR/publish/linux-x64"

    echo "==> Creating Linux archive..."
    tar -czf "$OUT_DIR/AemulusPackageManager-linux-x64.tar.gz" -C "$SCRIPT_DIR/publish/linux-x64" .
    echo "    -> $OUT_DIR/AemulusPackageManager-linux-x64.tar.gz"
}

build_appimage() {
    # Check dependencies
    for cmd in convert wget; do
        if ! command -v "$cmd" &>/dev/null; then
            echo "Error: '$cmd' is required but not found."
            [[ "$cmd" == "convert" ]] && echo "  Install with: sudo apt install imagemagick"
            [[ "$cmd" == "wget" ]]    && echo "  Install with: sudo apt install wget"
            exit 1
        fi
    done

    echo "==> Publishing Linux x64 (non-single-file for AppImage)..."
    dotnet publish "$SCRIPT_DIR/$PROJECT" \
        -c Release \
        -r linux-x64 \
        -p:PublishSingleFile=false \
        -p:SelfContained=true \
        -o "$SCRIPT_DIR/publish/linux-x64-appimage"

    echo "==> Building AppImage..."
    local APPDIR="$SCRIPT_DIR/publish/AppDir"
    rm -rf "$APPDIR"
    mkdir -p "$APPDIR/usr/bin"
    mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"

    # Copy published output
    cp -r "$SCRIPT_DIR/publish/linux-x64-appimage/"* "$APPDIR/usr/bin/"

    # Convert .ico to .png for AppImage icon (use largest frame)
    convert "$SCRIPT_DIR/Assets/Aemulus.ico[7]" "$APPDIR/AemulusPackageManager.png"
    cp "$APPDIR/AemulusPackageManager.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/AemulusPackageManager.png"

    # Generate desktop file
    cat > "$APPDIR/AemulusPackageManager.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Aemulus Package Manager
Comment=Manage modding packages for Persona games
Exec=AemulusPackageManager
Icon=AemulusPackageManager
Categories=Game;Utility;
Terminal=false
StartupWMClass=AemulusPackageManager
EOF

    # Generate AppRun
    cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/AemulusPackageManager" "$@"
EOF
    chmod +x "$APPDIR/AppRun"
    chmod +x "$APPDIR/usr/bin/AemulusPackageManager"

    # Download appimagetool if not cached
    local TOOL="$SCRIPT_DIR/publish/appimagetool"
    if [[ ! -x "$TOOL" ]]; then
        echo "    Downloading appimagetool..."
        wget -q https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage -O "$TOOL"
        chmod +x "$TOOL"
    fi

    ARCH=x86_64 "$TOOL" "$APPDIR" "$OUT_DIR/AemulusPackageManager-x86_64.AppImage"
    echo "    -> $OUT_DIR/AemulusPackageManager-x86_64.AppImage"
}

build_dependencies() {
    echo "==> Building dependencies..."
    git clone --depth 1 https://github.com/tge-was-taken/Atlus-Script-Tools.git atlus-tools
          dotnet publish atlus-tools/Source/AtlusScriptCompiler/AtlusScriptCompiler.csproj \
            -r linux-x64 \
            -c Release \
            -o Dependencies/AtlusScriptCompiler-linux --self-contained true /p:PublishSingleFile=true
          chmod +x Dependencies/AtlusScriptCompiler-linux/AtlusScriptCompiler
          dotnet publish atlus-tools/Source/AtlusScriptCompiler/AtlusScriptCompiler.csproj \
            -r win-x64 \
            -c Release \
            -o Dependencies/AtlusScriptCompiler-windows --self-contained true /p:PublishSingleFile=true
          chmod +x Dependencies/AtlusScriptCompiler-windows/AtlusScriptCompiler.exe
          rm -rf atlus-tools
}

for target in "${TARGETS[@]}"; do
    case "$target" in
        windows)  build_windows  ;;
        linux)    build_linux    ;;
        appimage) build_appimage ;;
        dependencies) build_dependencies ;;
    esac
done

echo ""
echo "==> Done! Outputs in $OUT_DIR/"
ls -lh "$OUT_DIR/"

