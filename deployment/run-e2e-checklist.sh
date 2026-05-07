#!/usr/bin/env bash
set -euo pipefail

SIM_URL="${SIM_URL:-http://localhost:5200}"
PERSIST_URL="${PERSIST_URL:-http://localhost:5100}"
SAVE_NAME="${SAVE_NAME:-poc-e2e-$(date +%s)}"
AUTO_START="${AUTO_START:-true}"
WAIT_SECONDS="${WAIT_SECONDS:-60}"

log(){ echo "[$(date +%H:%M:%S)] $*"; }
require(){ command -v "$1" >/dev/null 2>&1 || { echo "Missing dependency: $1"; exit 1; }; }
require curl

wait_for_health() {
  local url="$1" name="$2"
  local deadline=$((SECONDS + WAIT_SECONDS))
  until curl -fsS "$url" >/dev/null 2>&1; do
    if (( SECONDS >= deadline )); then
      echo "Timeout esperando $name en $url"
      return 1
    fi
    sleep 2
  done
}

start_services_if_needed() {
  if curl -fsS "$SIM_URL/api/simulation/health" >/dev/null 2>&1 && curl -fsS "$PERSIST_URL/api/gamestate/health" >/dev/null 2>&1; then
    return 0
  fi

  if [[ "$AUTO_START" != "true" ]]; then
    echo "Servicios no disponibles y AUTO_START=false"
    return 1
  fi

  if command -v docker >/dev/null 2>&1; then
    log "Servicios no detectados. Intentando levantar stack con docker compose..."
    docker compose -f deployment/docker-compose.full-stack.yml up -d
  elif command -v dotnet >/dev/null 2>&1; then
    log "Servicios no detectados. Intentando levantar servicios con dotnet..."
    dotnet run --project src/DuneArrakis.PersistenceService/DuneArrakis.PersistenceService.csproj --urls=http://0.0.0.0:5100 >/tmp/persistence.log 2>&1 &
    dotnet run --project src/DuneArrakis.SimulationService/DuneArrakis.SimulationService.csproj --urls=http://0.0.0.0:5200 >/tmp/simulation.log 2>&1 &
  else
    echo "No hay docker ni dotnet disponibles para autostart."
    return 1
  fi

  wait_for_health "$SIM_URL/api/simulation/health" "SimulationService"
  wait_for_health "$PERSIST_URL/api/gamestate/health" "PersistenceService"
}

json_post(){
  local url="$1" body="$2"
  curl -sS -X POST "$url" -H 'Content-Type: application/json' -d "$body"
}

log "Preflight: comprobando/levantando servicios"
start_services_if_needed

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
