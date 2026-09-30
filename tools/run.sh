#!/usr/bin/env bash
# Runs the sample app (journal + quote demos) at http://localhost:5199
set -euo pipefail
cd "$(dirname "$0")/.."
echo "[netopeneditor] http://localhost:5199 — Ctrl+C para detener"
exec dotnet run --project samples/NetOpenEditor.Example --urls "http://localhost:${1:-5199}"
