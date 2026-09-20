#!/usr/bin/env bash
set -euo pipefail

pkg_dir="build/StandaloneWindows64"
while (( $# )); do
  case "$1" in
    --pkg-dir)
      [[ $# -ge 2 && -n "$2" ]] || { echo "--pkg-dir requires a directory" >&2; exit 2; }
      pkg_dir="$2"; shift 2 ;;
    -h|--help)
      echo "Usage: bash scripts/release-smoke.sh [--pkg-dir DIRECTORY]"
      echo "Checks Windows Mono player files, without launching the game."
      exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

for file in \
  ShipSim159.exe \
  UnityPlayer.dll \
  MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll \
  ShipSim159_Data/boot.config \
  ShipSim159_Data/globalgamemanagers \
  ShipSim159_Data/Managed/ShipSimulator.Runtime.dll; do
  [[ -f "$pkg_dir/$file" && -s "$pkg_dir/$file" ]] || {
    echo "error: missing or empty Windows build file: $pkg_dir/$file" >&2
    exit 1
  }
done
echo "RELEASE_SMOKE|PASS: $pkg_dir"
