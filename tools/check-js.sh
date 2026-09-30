#!/usr/bin/env bash
# Syntax-checks the client runtime with Node when available (Playwright E2E is the real gate).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
JS="$ROOT/src/NetOpenEditor/Assets/netopeneditor.js"
if command -v node >/dev/null 2>&1; then
  node --check "$JS" && echo "[netopeneditor] JS syntax OK"
else
  echo "[netopeneditor] node not found; skipping syntax check (E2E tests cover the runtime)"
fi
