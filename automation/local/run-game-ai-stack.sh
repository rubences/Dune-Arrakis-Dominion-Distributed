#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOLS_BIN="$REPO_ROOT/automation/tools/bin"
OUTPUT_DIR="$REPO_ROOT/automation/output"
REPORT_FILE="$OUTPUT_DIR/local-game-ai-stack-report.txt"

AGENT="opencode"
CREWAI_MODE="local"
STATE_FILE=""
WAIT_REMOTE="false"
SKIP_BOOTSTRAP="false"
SKIP_GGA="false"
SKIP_CREWAI="false"
AUTO_STAGE_SIMULATION="false"
GGA_ONLY="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --agent)
      AGENT="${2:-opencode}"
      shift 2
      ;;
    --crewai-mode)
      CREWAI_MODE="${2:-local}"
      shift 2
      ;;
    --state-file)
      STATE_FILE="${2:-}"
      shift 2
      ;;
    --wait-remote)
      WAIT_REMOTE="true"
      shift
      ;;
    --skip-bootstrap)
      SKIP_BOOTSTRAP="true"
      shift
      ;;
    --skip-gga)
      SKIP_GGA="true"
      shift
      ;;
    --skip-crewai)
      SKIP_CREWAI="true"
      shift
      ;;
    --auto-stage-simulation)
      AUTO_STAGE_SIMULATION="true"
      shift
      ;;
    --gga-only)
      GGA_ONLY="true"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

mkdir -p "$OUTPUT_DIR"

steps=()
pending=()
errors=()

log_step() {
  steps+=("$1")
  echo "[step] $1"
}

log_pending() {
  pending+=("$1")
  echo "[pending] $1"
}

log_error() {
  errors+=("$1")
  echo "[error] $1" >&2
}

run_or_pending() {
  local title="$1"
  shift
  log_step "$title"
  if ! "$@"; then
    log_pending "$title failed. Continue manually."
  fi
}

if [[ "$SKIP_BOOTSTRAP" != "true" ]]; then
  log_step "Sync + build local Gentleman tools"
  bash "$REPO_ROOT/automation/local/bootstrap-gentleman-local.sh"
fi

export PATH="$TOOLS_BIN:$PATH"

if [[ ! -x "$TOOLS_BIN/engram" || ! -x "$TOOLS_BIN/gentle-ai" || ! -x "$TOOLS_BIN/gga" ]]; then
  log_error "Local tools not found in automation/tools/bin. Run bootstrap first."
fi

if [[ "$GGA_ONLY" != "true" ]]; then
  run_or_pending "Engram setup for $AGENT" "$TOOLS_BIN/engram" setup "$AGENT"
  run_or_pending "Gentle preset for $AGENT" "$TOOLS_BIN/gentle-ai" install --agent "$AGENT" --preset full-gentleman
fi

if [[ "$SKIP_CREWAI" != "true" && "$GGA_ONLY" != "true" ]]; then
  PYTHON_CMD=""
  if [[ -x "$REPO_ROOT/.venv/bin/python" ]]; then
    PYTHON_CMD="$REPO_ROOT/.venv/bin/python"
  elif command -v python3 >/dev/null 2>&1; then
    PYTHON_CMD="$(command -v python3)"
  elif command -v python >/dev/null 2>&1; then
    PYTHON_CMD="$(command -v python)"
  fi

  if [[ -z "$PYTHON_CMD" ]]; then
    log_pending "CrewAI step skipped: Python interpreter not found."
  else
    log_step "Run CrewAI monthly decision automation ($CREWAI_MODE)"
    crew_cmd=(
      "$PYTHON_CMD"
      "$REPO_ROOT/automation/crewai_monthly_decision_automation.py"
      --mode "$CREWAI_MODE"
      --output-file "$REPO_ROOT/automation/output/monthly_actions.json"
    )

    if [[ -n "$STATE_FILE" ]]; then
      crew_cmd+=(--state-file "$STATE_FILE")
    fi

    if [[ "$CREWAI_MODE" == "remote" && "$WAIT_REMOTE" == "true" ]]; then
      crew_cmd+=(--wait)
    fi

    if ! "${crew_cmd[@]}"; then
      log_pending "CrewAI monthly decision automation failed. Verify tokens/dependencies and retry."
    fi
  fi
fi

if [[ "$SKIP_GGA" != "true" ]]; then
  if [[ "$AUTO_STAGE_SIMULATION" == "true" ]]; then
    log_step "Auto-stage simulation and test files for GGA"
    git -C "$REPO_ROOT" add -- "src/DuneArrakis.SimulationService" "tests/DuneArrakis.Tests"
  fi

  log_step "Run GGA review for staged simulation changes"

  staged_simulation_count=$(git -C "$REPO_ROOT" diff --cached --name-only -- "src/DuneArrakis.SimulationService/**" "tests/DuneArrakis.Tests/**" | wc -l | tr -d ' ')

  if [[ "$staged_simulation_count" == "0" ]]; then
    log_pending "No staged simulation/test changes found for GGA. Stage files and rerun."
  else
    if [[ ! -f "$REPO_ROOT/.gga" ]]; then
      run_or_pending "Initialize GGA config" "$TOOLS_BIN/gga" init
    fi

    if ! (cd "$REPO_ROOT" && "$TOOLS_BIN/gga" run); then
      log_pending "GGA review failed. Verify provider configuration in .gga and agent CLI availability."
    fi
  fi
fi

{
  echo "RESUMEN"
  echo "- agent: $AGENT"
  echo "- crewai_mode: $CREWAI_MODE"
  echo "- steps_executed: ${#steps[@]}"
  echo
  echo "PASOS"
  for s in "${steps[@]}"; do
    echo "- $s"
  done
  echo
  echo "PENDIENTES"
  if [[ ${#pending[@]} -eq 0 ]]; then
    echo "- none"
  else
    for p in "${pending[@]}"; do
      echo "- $p"
    done
  fi
  echo
  echo "ERRORES"
  if [[ ${#errors[@]} -eq 0 ]]; then
    echo "- none"
  else
    for e in "${errors[@]}"; do
      echo "- $e"
    done
  fi
  echo
  echo "VERSIONES"
  "$TOOLS_BIN/engram" version 2>/dev/null || true
  "$TOOLS_BIN/gentle-ai" --version 2>/dev/null || true
  "$TOOLS_BIN/gga" version 2>/dev/null || true
} > "$REPORT_FILE"

echo "Report generated at: $REPORT_FILE"

if [[ ${#errors[@]} -gt 0 ]]; then
  exit 1
fi

exit 0
