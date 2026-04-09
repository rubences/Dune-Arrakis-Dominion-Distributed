#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TOOLS_BIN="$REPO_ROOT/automation/tools/bin"

export PATH="$TOOLS_BIN:$PATH"

echo "Gentleman local tools enabled for this shell."
echo "PATH prefix: $TOOLS_BIN"
