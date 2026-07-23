#!/usr/bin/env bash
# Stops whatever ./scripts/dev-up.sh started.
# Usage: ./scripts/dev-down.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PID_FILE="${SCRIPT_DIR}/.dev-pids"

if [[ ! -s "$PID_FILE" ]]; then
  echo "Nothing to stop (no ${PID_FILE})."
  exit 0
fi

echo "=== Stopping energy-tracker local dev stack ==="

# Some services (notably `npx ... swa start`) spawn grandchild processes
# (e.g. SWA CLI's msha/server.js) that outlive their immediate parent if
# only the tracked PID is killed — recurse through the whole tree.
kill_tree() {
  local pid="$1" sig="$2"
  local child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    kill_tree "$child" "$sig"
  done
  kill "-$sig" "$pid" 2>/dev/null || true
}

while IFS='=' read -r name pid; do
  [[ -z "$name" ]] && continue
  if kill -0 "$pid" 2>/dev/null; then
    kill_tree "$pid" TERM
    for _ in 1 2 3 4 5; do
      kill -0 "$pid" 2>/dev/null || break
      sleep 1
    done
    if kill -0 "$pid" 2>/dev/null; then
      kill_tree "$pid" KILL
      echo "  $name (pid $pid) force-killed"
    else
      echo "  $name (pid $pid) stopped"
    fi
  else
    echo "  $name (pid $pid) already stopped"
  fi
done < "$PID_FILE"

rm -f "$PID_FILE"
echo "Done."
