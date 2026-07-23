#!/usr/bin/env bash
# Starts the local dev stack: Azurite + func start + Vite + SWA CLI emulator.
# Idempotent — re-running while services are already up skips them.
# Usage: ./scripts/dev-up.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
PID_FILE="${SCRIPT_DIR}/.dev-pids"
LOG_DIR="${SCRIPT_DIR}/.dev-logs"
AZURITE_DATA_DIR="${SCRIPT_DIR}/.azurite-data"

mkdir -p "$LOG_DIR" "$AZURITE_DATA_DIR"
touch "$PID_FILE"

on_exit() {
  local code=$?
  if [[ "$code" -ne 0 ]]; then
    echo ""
    echo "Some services may have started before this failure. Run ./scripts/dev-down.sh to clean up."
  fi
}
trap on_exit EXIT

safe_tail() {
  tail -n 10 "$1" 2>/dev/null || echo "(no log output captured)"
}

is_running() {
  local name="$1"
  local pid
  pid=$(grep "^${name}=" "$PID_FILE" 2>/dev/null | cut -d= -f2 || true)
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    echo "$pid"
    return 0
  fi
  return 1
}

record_pid() {
  local name="$1" pid="$2"
  grep -v "^${name}=" "$PID_FILE" > "${PID_FILE}.tmp" 2>/dev/null || true
  mv "${PID_FILE}.tmp" "$PID_FILE"
  echo "${name}=${pid}" >> "$PID_FILE"
}

start_bg() {
  local name="$1" workdir="$2" logfile="$3"
  shift 3
  ( cd "$workdir" && exec "$@" > "$logfile" 2>&1 ) &
  local pid=$!
  record_pid "$name" "$pid"
  echo "$pid"
}

wait_for_port() {
  local port="$1" timeout="$2" waited=0
  while ! nc -z localhost "$port" 2>/dev/null; do
    sleep 1
    waited=$((waited + 1))
    [[ "$waited" -ge "$timeout" ]] && return 1
  done
  return 0
}

# Recursively kills a process and all its descendants — some tools (e.g.
# `npx ... swa start`) spawn grandchildren that outlive their parent if only
# the top-level PID is signaled.
kill_tree() {
  local pid="$1" sig="$2"
  local child
  for child in $(pgrep -P "$pid" 2>/dev/null || true); do
    kill_tree "$child" "$sig"
  done
  kill "-$sig" "$pid" 2>/dev/null || true
}

echo "=== energy-tracker local dev stack ==="

# --- Azurite (local Blob/Queue/Table storage emulator) ---
if ! command -v azurite &>/dev/null; then
  echo "ERROR: azurite not found. Install it: npm install -g azurite"
  exit 1
fi

if pid=$(is_running azurite); then
  echo "Azurite already running (pid $pid)"
else
  echo "Starting Azurite..."
  start_bg azurite "$ROOT_DIR" "$LOG_DIR/azurite.log" \
    azurite --silent --location "$AZURITE_DATA_DIR" --debug "$AZURITE_DATA_DIR/debug.log" >/dev/null
  if ! wait_for_port 10000 15 || ! wait_for_port 10001 5 || ! wait_for_port 10002 5; then
    echo "ERROR: Azurite did not become ready (blob/queue/table) within timeout. Last log lines:"
    safe_tail "$LOG_DIR/azurite.log"
    exit 1
  fi
  echo "  Azurite ready"
fi

# --- Azure Functions host ---
if pid=$(is_running func); then
  echo "Functions host already running (pid $pid)"
else
  if ! command -v func &>/dev/null; then
    echo "ERROR: func (Azure Functions Core Tools) not found."
    echo "Install it: https://learn.microsoft.com/azure/azure-functions/functions-run-local"
    exit 1
  fi
  if [[ ! -f "$ROOT_DIR/api/local.settings.json" ]]; then
    echo "ERROR: api/local.settings.json not found."
    echo "Copy api/local.settings.json.example to api/local.settings.json and fill in the values"
    echo "(see infra/deploy.sh's post-deploy instructions for where each value comes from)."
    exit 1
  fi
  echo "Starting Functions host (func start)..."
  start_bg func "$ROOT_DIR/api" "$LOG_DIR/func.log" func start >/dev/null
  if ! wait_for_port 7071 45; then
    echo "ERROR: func start did not become ready within 45s. Last log lines:"
    safe_tail "$LOG_DIR/func.log"
    echo ""
    echo "A SQL Server firewall block is a common cause — that needs manual Azure-side"
    echo "intervention (ask Ralf), this script cannot work around it."
    exit 1
  fi
  echo "  Functions host ready"
fi

# --- Vite dev server ---
if pid=$(is_running vite); then
  echo "Vite dev server already running (pid $pid)"
else
  echo "Starting Vite dev server..."
  start_bg vite "$ROOT_DIR/client" "$LOG_DIR/vite.log" npm run dev >/dev/null
  if ! wait_for_port 5173 15; then
    echo "ERROR: Vite dev server did not become ready within 15s. Last log lines:"
    safe_tail "$LOG_DIR/vite.log"
    exit 1
  fi
  echo "  Vite dev server ready"
fi

# --- SWA CLI (auth emulator + reverse proxy) ---
if pid=$(is_running swa); then
  echo "SWA CLI already running (pid $pid)"
else
  if [[ ! -f "$ROOT_DIR/client/public/staticwebapp.config.json" ]]; then
    echo "ERROR: client/public/staticwebapp.config.json not found — SWA CLI needs it."
    exit 1
  fi

  if lsof -i :4280 -t >/dev/null 2>&1; then
    stale_pid=$(lsof -i :4280 -t | head -n 1)
    stale_cmd=$(ps -p "$stale_pid" -o command= 2>/dev/null || echo "")
    echo "Port 4280 is already in use by pid $stale_pid: $stale_cmd"
    if [[ "$stale_cmd" == *swa* || "$stale_cmd" == *static-web-apps-cli* ]]; then
      if [[ -t 0 ]]; then
        read -r -p "This looks like a stale SWA CLI process. Kill it and continue? [y/N] " confirm
        if [[ "$confirm" =~ ^[Yy]$ ]]; then
          kill_tree "$stale_pid" TERM
          sleep 1
          if lsof -i :4280 -t >/dev/null 2>&1; then
            echo "Port 4280 still occupied after kill — free it manually and re-run."
            exit 1
          fi
        else
          echo "Not killing. Free port 4280 manually and re-run this script."
          exit 1
        fi
      else
        echo "Non-interactive shell — not killing automatically. Free port 4280 manually and re-run."
        exit 1
      fi
    else
      echo "This does not look like a SWA CLI process — not killing automatically."
      echo "Free port 4280 manually and re-run this script."
      exit 1
    fi
  fi
  echo "Starting SWA CLI emulator..."
  start_bg swa "$ROOT_DIR" "$LOG_DIR/swa.log" \
    npx -y @azure/static-web-apps-cli@^2.0.0 start http://localhost:5173 \
    --api-devserver-url http://localhost:7071 \
    --swa-config-location client/public >/dev/null
  if ! wait_for_port 4280 30; then
    echo "ERROR: SWA CLI did not become ready within 30s. Last log lines:"
    safe_tail "$LOG_DIR/swa.log"
    exit 1
  fi
  echo "  SWA CLI ready"
fi

echo ""
echo "=== Stack is up ==="
echo "Open: http://localhost:4280"
echo "Logs: $LOG_DIR"
echo "Stop: ./scripts/dev-down.sh"
