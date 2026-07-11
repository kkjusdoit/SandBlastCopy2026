#!/usr/bin/env bash
set -euo pipefail

shared_dir=/srv/sandrussia/shared
backup_dir="$shared_dir/backups"
database="$shared_dir/leaderboard.sqlite"

mkdir -p "$backup_dir"
if [[ -f "$database" ]]; then
  sqlite3 "$database" ".backup '$backup_dir/leaderboard-$(date +%Y%m%d-%H%M%S).sqlite'"
fi
find "$backup_dir" -type f -name 'leaderboard-*.sqlite' -mtime +14 -delete
