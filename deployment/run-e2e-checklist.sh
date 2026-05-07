#!/usr/bin/env bash
set -euo pipefail

SIM_URL="${SIM_URL:-http://localhost:5200}"
PERSIST_URL="${PERSIST_URL:-http://localhost:5100}"
SAVE_NAME="${SAVE_NAME:-poc-e2e-$(date +%s)}"

log(){ echo "[$(date +%H:%M:%S)] $*"; }

require(){ command -v "$1" >/dev/null 2>&1 || { echo "Missing dependency: $1"; exit 1; }; }
require curl
require python3

json_post(){
  local url="$1" body="$2"
  curl -sS -X POST "$url" -H 'Content-Type: application/json' -d "$body"
}

log "1) Health checks"
curl -fsS "$SIM_URL/api/simulation/health" >/tmp/sim_health.json
curl -fsS "$PERSIST_URL/api/gamestate/health" >/tmp/persist_health.json

log "2) Create game"
GAME_STATE=$(curl -fsS -X POST "$SIM_URL/api/simulation/new-game?scenarioType=0&saveName=$SAVE_NAME")

echo "$GAME_STATE" >/tmp/game_state.json

log "3) Process month"
PROCESS_RESULT=$(json_post "$SIM_URL/api/simulation/process-month" "$GAME_STATE")
echo "$PROCESS_RESULT" >/tmp/process_result.json

log "4) Persistence save/list/load"
json_post "$PERSIST_URL/api/gamestate/save" "$GAME_STATE" >/tmp/save_result.json
curl -fsS "$PERSIST_URL/api/gamestate/list" >/tmp/list_result.json
curl -fsS "$PERSIST_URL/api/gamestate/load/$SAVE_NAME" >/tmp/load_result.json || true

log "5) CrewAI health/input checks"
curl -sS "$SIM_URL/api/simulation/ai/health" >/tmp/ai_health.json || true
curl -sS "$SIM_URL/api/simulation/ai/inputs" >/tmp/ai_inputs.json || true

log "Checklist E2E finalizado. Artefactos en /tmp/*.json"
