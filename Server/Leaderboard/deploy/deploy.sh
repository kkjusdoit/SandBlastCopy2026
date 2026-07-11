#!/usr/bin/env bash
set -euo pipefail

release_id="${1:-$(date +%Y%m%d%H%M%S)}"
source_dir="$(cd "$(dirname "$0")/.." && pwd)"
remote="${FLOWSAND_SSH_HOST:-aliyun}"
remote_release="/srv/sandrussia/releases/$release_id"

rsync -az --delete \
  --exclude node_modules \
  --exclude data \
  --exclude .env \
  "$source_dir/" "$remote:$remote_release/"

ssh "$remote" "RELEASE='$remote_release' bash -s" <<'REMOTE'
set -euo pipefail
id flowsand >/dev/null 2>&1 || useradd --system --home /srv/sandrussia --shell /usr/sbin/nologin flowsand
mkdir -p /srv/sandrussia/shared/backups
chown -R flowsand:flowsand /srv/sandrussia/shared "$RELEASE"
cd "$RELEASE"
npm ci --omit=dev
chmod +x deploy/backup-leaderboard.sh
ln -sfn "$RELEASE" /srv/sandrussia/current
install -m 0644 deploy/flowsand-leaderboard.service /etc/systemd/system/flowsand-leaderboard.service
install -m 0644 deploy/flowsand-leaderboard-backup.service /etc/systemd/system/flowsand-leaderboard-backup.service
install -m 0644 deploy/flowsand-leaderboard-backup.timer /etc/systemd/system/flowsand-leaderboard-backup.timer
install -m 0644 deploy/nginx-leaderboard.conf /etc/nginx/sites-available/flowsand-leaderboard
ln -sfn /etc/nginx/sites-available/flowsand-leaderboard /etc/nginx/sites-enabled/flowsand-leaderboard
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl daemon-reload
systemctl enable --now flowsand-leaderboard flowsand-leaderboard-backup.timer
systemctl reload nginx
systemctl is-active flowsand-leaderboard nginx
REMOTE
