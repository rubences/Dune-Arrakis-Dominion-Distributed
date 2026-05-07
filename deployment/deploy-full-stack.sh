#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/deployment/docker-compose.full-stack.yml"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker no está instalado."
  exit 1
fi

if [[ "${1:-up}" == "up" ]]; then
  echo "Levantando stack local completo (Simulation + Persistence + Frontend)..."
  docker compose -f "$COMPOSE_FILE" up --build
elif [[ "${1:-}" == "down" ]]; then
  echo "Deteniendo stack local completo..."
  docker compose -f "$COMPOSE_FILE" down
else
  echo "Uso: bash deployment/deploy-full-stack.sh [up|down]"
  exit 2
fi
