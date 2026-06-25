#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/github_actions_deploy}"
HOST="${OPSGUARD_HOST:-root@111.229.81.45}"
PUBLISH_DIR="${PUBLISH_DIR:-/tmp/opsguard-web}"
REMOTE_DIR="/opt/opsguard-web"
PORT="${OPSGUARD_WEB_PORT:-5229}"

ENV_FILE="${ENV_FILE:-$ROOT/.env}"

echo "==> Publish OpsGuard.Web (Release, framework-dependent)"
dotnet publish "$ROOT/src/OpsGuard.Web/OpsGuard.Web.csproj" -c Release -o "$PUBLISH_DIR"

echo "==> Sync to $HOST:$REMOTE_DIR"
rsync -av "$PUBLISH_DIR/" "$HOST:$REMOTE_DIR/" \
  -e "ssh -i $SSH_KEY -o BatchMode=yes"

echo "==> Copy .env"
if [[ -f "$ENV_FILE" ]]; then
  scp -i "$SSH_KEY" "$ENV_FILE" "$HOST:$REMOTE_DIR/.env"
else
  echo "Skip .env (file not found: $ENV_FILE)"
fi

echo "==> Ensure ASP.NET Core runtime on server"
ssh -i "$SSH_KEY" -o BatchMode=yes "$HOST" "bash -s" <<'REMOTE'
set -euo pipefail
if ! /root/.dotnet/dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App 10'; then
  docker pull mcr.microsoft.com/dotnet/aspnet:10.0
  docker rm -f opsguard-dotnet-extract 2>/dev/null || true
  docker create --name opsguard-dotnet-extract mcr.microsoft.com/dotnet/aspnet:10.0
  mkdir -p /root/.dotnet
  docker cp opsguard-dotnet-extract:/usr/share/dotnet/. /root/.dotnet/
  docker rm opsguard-dotnet-extract
fi
REMOTE

echo "==> Install systemd unit + start service"
if ssh -i "$SSH_KEY" -o BatchMode=yes "$HOST" "test -f $REMOTE_DIR/.env"; then
  ssh -i "$SSH_KEY" -o BatchMode=yes "$HOST" "chmod 600 $REMOTE_DIR/.env"
else
  echo "ERROR: $REMOTE_DIR/.env not found on server. Create .env locally and re-run."
  exit 1
fi
scp -i "$SSH_KEY" "$ROOT/deploy/opsguard-web.service" "$HOST:/etc/systemd/system/opsguard-web.service"

ssh -i "$SSH_KEY" -o BatchMode=yes "$HOST" "bash -s" <<EOF
set -euo pipefail
sed -i "s|:5229|:$PORT|g" /etc/systemd/system/opsguard-web.service
systemctl daemon-reload
systemctl enable opsguard-web
systemctl restart opsguard-web
sleep 2
systemctl is-active opsguard-web
curl -s -o /dev/null -w "HTTP %{http_code}\n" http://127.0.0.1:$PORT/
EOF

echo "==> Done. Web UI: http://111.229.81.45:$PORT/ (建议 SSH 隧道访问)"
