#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/github_actions_deploy}"
HOST="${OPSGUARD_HOST:-root@111.229.81.45}"
PUBLISH_DIR="${PUBLISH_DIR:-/tmp/opsguard-publish}"
REMOTE_DIR="/opt/opsguard"
ENV_FILE="${ENV_FILE:-$ROOT/.env}"

echo "==> Publish OpsGuard.App (Release)"
dotnet publish "$ROOT/src/OpsGuard.App/OpsGuard.App.csproj" -c Release -o "$PUBLISH_DIR"

echo "==> Sync to $HOST:$REMOTE_DIR"
rsync -av "$PUBLISH_DIR/" "$HOST:$REMOTE_DIR/" \
  -e "ssh -i $SSH_KEY -o BatchMode=yes"

if [[ -f "$ENV_FILE" ]]; then
  echo "==> Copy .env to $REMOTE_DIR"
  scp -i "$SSH_KEY" "$ENV_FILE" "$HOST:$REMOTE_DIR/.env"
  ssh -i "$SSH_KEY" -o BatchMode=yes "$HOST" "chmod 600 $REMOTE_DIR/.env"
else
  echo "==> Skip .env (file not found: $ENV_FILE)"
fi

echo "==> Verify App publish on server"
ssh -i "$SSH_KEY" -o BatchMode=yes "$HOST" "test -f $REMOTE_DIR/OpsGuard.App.dll && echo OK: OpsGuard.App.dll"

echo "==> App deploy done ($REMOTE_DIR)"
