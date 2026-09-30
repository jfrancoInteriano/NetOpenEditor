#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SUFFIX="${1:-}"
OUT="$ROOT/artifacts"

export DOTNET_CLI_DISABLE_NODE_REUSE=1
export MSBUILDDISABLENODEREUSE=1

echo "[netopeneditor] Shutting down stale build servers..."
dotnet build-server shutdown

"$ROOT/tools/check-js.sh"

echo "[netopeneditor] Running tests..."
dotnet test "$ROOT/NetOpenEditor.slnx" -c Release

rm -rf "$OUT"

if [ -n "$SUFFIX" ]; then
  echo "[netopeneditor] Packing <VersionPrefix>-$SUFFIX..."
  dotnet pack "$ROOT/src/NetOpenEditor/NetOpenEditor.csproj" -c Release -o "$OUT" --version-suffix "$SUFFIX"
else
  echo "[netopeneditor] Packing release version..."
  dotnet pack "$ROOT/src/NetOpenEditor/NetOpenEditor.csproj" -c Release -o "$OUT"
fi

echo "[netopeneditor] Packages in $OUT:"
ls -1 "$OUT"
