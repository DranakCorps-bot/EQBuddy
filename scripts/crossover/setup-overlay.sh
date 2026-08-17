#!/bin/zsh
# EQBuddy — CrossOver/Wine overlay setup for macOS.
#
# Teaches the Mac Wine driver (winemac.drv) two opt-in tricks so the EQBuddy
# Windows build, run inside CrossOver, can float over a FULLSCREEN EverQuest
# Legends window — which stock Wine cannot do (a fullscreen game sits at a
# window level no ordinary topmost window can beat). See
# docs/CrossOver-macOS-overlay.md for the why.
#
# It rebuilds ONLY winemac.so from CrossOver's own published source (LGPL),
# adds a tiny patch that gates two new behaviors behind registry knobs, and
# drops it in. Everything is opt-in and reversible; nothing here affects a
# Windows install or any other Wine app that doesn't set the knobs.
#
# Usage:   scripts/crossover/setup-overlay.sh          # patch the driver
#          scripts/crossover/setup-overlay.sh --revert # restore the original
#
# Requires: CrossOver in /Applications, Xcode command-line tools, and Homebrew
# bison (>= 3.0). The build takes a few minutes the first time.
set -e

CX_APP="/Applications/CrossOver.app"
WINE_UNIX="$CX_APP/Contents/SharedSupport/CrossOver/lib/wine/x86_64-unix"
SHIPPED="$WINE_UNIX/winemac.so"
BACKUP="$SHIPPED.eqbuddy-orig"
HERE="$(cd "$(dirname "$0")" && pwd)"
PATCH="$HERE/winemac-overlay.patch"
WORK="${TMPDIR:-/tmp}/eqbuddy-winemac-build"

fail() { echo "‼️  $1" >&2; exit 1; }

[ -d "$CX_APP" ] || fail "CrossOver not found at $CX_APP."
[ -f "$SHIPPED" ] || fail "winemac.so not found — is this an Apple-silicon CrossOver 26+ bottle?"

install_so() {  # $1 = path to a built winemac.so
    [ -f "$BACKUP" ] || cp "$SHIPPED" "$BACKUP"   # keep the very first original
    cp "$1" "$SHIPPED"
    codesign --sign - --force "$SHIPPED"          # ad-hoc; wineloader has disable-library-validation
    codesign -v "$SHIPPED"
    echo "✅ Patched winemac.so installed. Restart EQBuddy (and EverQuest) to load it."
}

if [ "$1" = "--revert" ]; then
    [ -f "$BACKUP" ] || fail "No backup found ($BACKUP) — nothing to revert."
    cp "$BACKUP" "$SHIPPED" && codesign --sign - --force "$SHIPPED"
    echo "✅ Restored the original winemac.so."
    exit 0
fi

if nm "$SHIPPED" 2>/dev/null | grep -q topmost_float_over_fullscreen; then
    echo "winemac.so already carries the overlay patch — nothing to do."
    exit 0
fi

CXVER="$(defaults read "$CX_APP/Contents/Info" CFBundleShortVersionString)"
echo "CrossOver $CXVER detected."

# CodeWeavers names the source tarball with a three-part version (crossover-sources-26.3.0),
# but the bundle's CFBundleShortVersionString drops a trailing zero ("26.3"). Pad it back or
# the download 404s — which looks exactly like a network problem and isn't one.
source_version() {
    case "$1" in
        *.*.*) echo "$1" ;;
        *.*)   echo "$1.0" ;;
        *)     echo "$1.0.0" ;;
    esac
}
CXSRCVER="$(source_version "$CXVER")"

# There is deliberately NO prebuilt fast path. This script replaces a library inside
# your CrossOver installation, and EQBuddy is not going to ask you to trust a binary you
# cannot read for that — especially not one shipped by a log-reading widget. Everything
# it installs is built here, on your machine, from CodeWeavers' own published source plus
# the patch beside this script, both of which you can review before running it.
command -v /opt/homebrew/opt/bison/bin/bison >/dev/null || \
    fail "Homebrew bison not found. Run: brew install bison"
xcode-select -p >/dev/null 2>&1 || fail "Xcode command-line tools not found. Run: xcode-select --install"

echo "Building winemac.so from CrossOver $CXVER source (a few minutes)…"
mkdir -p "$WORK"
TARBALL="$WORK/crossover-sources-$CXSRCVER.tar.gz"
SRC="$WORK/src-$CXVER"
if [ ! -d "$SRC/sources/wine" ]; then
    # --remove-on-error: without it a 404 or a dropped connection leaves a stub behind,
    # and the next run takes the [ -f ] cache hit and tries to untar an error page.
    [ -f "$TARBALL" ] || curl -fL --progress-bar --remove-on-error \
        "https://media.codeweavers.com/pub/crossover/source/crossover-sources-$CXSRCVER.tar.gz" -o "$TARBALL" \
        || fail "Could not download CrossOver $CXSRCVER source. Check the version / your connection."
    mkdir -p "$SRC" && tar -xzf "$TARBALL" -C "$SRC"
fi
WINESRC="$SRC/sources/wine"
[ -f "$WINESRC/configure" ] || fail "Unexpected source layout — no $WINESRC/configure."

# "Already applied" and "offsets moved, nothing applied" are the SAME patch(1) exit code,
# and only the first is benign — so judge by the state of the tree, not by the status. Every
# file the patch touches must carry the new symbol: a PARTIAL application must not pass.
patch_is_applied() {
    [ "$(grep -l topmost_float_over_fullscreen \
        "$WINESRC/dlls/winemac.drv/macdrv_cocoa.h" \
        "$WINESRC/dlls/winemac.drv/macdrv_main.c" \
        "$WINESRC/dlls/winemac.drv/cocoa_window.m" 2>/dev/null | wc -l)" -eq 3 ]
}

echo "Applying the overlay patch…"
if patch_is_applied; then
    echo "  (already applied — continuing)"
else
    # Checked afterwards, so don't let a nonzero status abort under `set -e`. Any .rej
    # files a real failure leaves behind are the diagnostic, so they are not cleaned up.
    ( cd "$WINESRC" && patch -p1 --forward --no-backup-if-mismatch < "$PATCH" ) || true
    patch_is_applied || fail "The overlay patch does not apply to CrossOver $CXVER source
    (see the .rej files under $WINESRC/dlls/winemac.drv). winemac.drv has probably moved
    on — $PATCH needs refreshing against this version."
fi

BUILD="$WORK/build-$CXVER"
mkdir -p "$BUILD"
if [ ! -f "$BUILD/Makefile" ]; then
    echo "Configuring (winemac.drv only)…"
    ( cd "$BUILD" && CC="clang -arch x86_64" CXX="clang++ -arch x86_64" \
        "$WINESRC/configure" --host=x86_64-apple-darwin --without-mingw \
        --enable-archs=none --disable-tests \
        --without-alsa --without-capi --without-cups --without-dbus --without-ffmpeg \
        --without-fontconfig --without-freetype --without-gphoto --without-gnutls \
        --without-gssapi --without-gstreamer --without-hwloc --without-inotify --without-krb5 \
        --without-netapi --without-opencl --without-oss --without-pcap --without-pcsclite \
        --without-pulse --without-sane --without-sdl --without-udev --without-unwind \
        --without-usb --without-v4l2 --without-vulkan --without-wayland >/dev/null ) \
        || fail "configure failed — see $BUILD/config.log."
fi

echo "Building winemac.so…"
MOLTENVK="$CX_APP/Contents/SharedSupport/CrossOver/lib64/libMoltenVK.dylib"
# The doubled backslashes are load-bearing. CFLAGS reaches the compiler via make, which
# runs each recipe through /bin/sh — so one level of quoting is eaten there. Passing a
# plain \" leaves clang with -DSONAME_LIBVULKAN=libMoltenVK.dylib, and win32u/vulkan.c
# fails to build on "use of undeclared identifier 'libMoltenVK'". \\\" survives the shell
# and gives the preprocessor the string literal it needs.
( cd "$BUILD" && PATH="/opt/homebrew/opt/bison/bin:$PATH" \
    make dlls/winemac.drv/winemac.so -j"$(sysctl -n hw.ncpu)" \
    "CFLAGS=-g -O2 -U_FORTIFY_SOURCE -D_FORTIFY_SOURCE=0 -DSONAME_LIBVULKAN=\\\"$(basename "$MOLTENVK")\\\"" ) \
    || fail "build failed."

BUILT="$BUILD/dlls/winemac.drv/winemac.so"
[ -f "$BUILT" ] || fail "build produced no winemac.so."
nm "$BUILT" | grep -q topmost_float_over_fullscreen || fail "built winemac.so is missing the patch symbol."
install_so "$BUILT"
