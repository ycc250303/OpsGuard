#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "==> Deploy OpsGuard App (backend / CLI)"
"$ROOT/deploy/deploy-app.sh"

echo
echo "==> Deploy OpsGuard Web (UI)"
"$ROOT/deploy/deploy-web.sh"

echo
echo "==> All deploy steps finished"
