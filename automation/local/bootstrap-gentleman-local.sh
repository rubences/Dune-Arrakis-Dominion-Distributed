#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
EXTERNAL_DIR="$REPO_ROOT/automation/external"
TOOLS_BIN="$REPO_ROOT/automation/tools/bin"
TOOLS_SKILLS="$REPO_ROOT/automation/tools/skills"

repos=(
  "engram|https://github.com/Gentleman-Programming/engram"
  "gentle-ai|https://github.com/Gentleman-Programming/gentle-ai"
  "gentleman-guardian-angel|https://github.com/Gentleman-Programming/gentleman-guardian-angel"
  "Gentleman-Skills|https://github.com/Gentleman-Programming/Gentleman-Skills"
)

mkdir -p "$EXTERNAL_DIR" "$TOOLS_BIN" "$TOOLS_SKILLS"

for item in "${repos[@]}"; do
  name="${item%%|*}"
  url="${item##*|}"
  target="$EXTERNAL_DIR/$name"

  if [[ -d "$target/.git" ]]; then
    echo "[sync] $name"
    git -C "$target" pull --ff-only
  else
    echo "[clone] $name"
    git clone "$url" "$target"
  fi

  origin="$(git -C "$target" remote get-url origin)"
  if [[ "$origin" != "$url" ]]; then
    echo "Origin mismatch for $name: expected $url got $origin" >&2
    exit 1
  fi

done

echo "[build] engram"
go -C "$EXTERNAL_DIR/engram" build -o "$TOOLS_BIN/engram" ./cmd/engram

echo "[build] gentle-ai"
go -C "$EXTERNAL_DIR/gentle-ai" build -o "$TOOLS_BIN/gentle-ai" ./cmd/gentle-ai

echo "[link] gga wrapper"
cat > "$TOOLS_BIN/gga" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_GGA="$SCRIPT_DIR/../../external/gentleman-guardian-angel/bin/gga"
exec "$REPO_GGA" "$@"
EOF
chmod +x "$TOOLS_BIN/engram" "$TOOLS_BIN/gentle-ai" "$TOOLS_BIN/gga"

echo "[skills] link curated/community"
ln -sfn "$EXTERNAL_DIR/Gentleman-Skills/curated" "$TOOLS_SKILLS/curated"
ln -sfn "$EXTERNAL_DIR/Gentleman-Skills/community" "$TOOLS_SKILLS/community"

echo "Done. Add this to your shell for this repo:"
echo "  export PATH=\"$TOOLS_BIN:\$PATH\""
echo "Versions:"
"$TOOLS_BIN/engram" version || true
"$TOOLS_BIN/gentle-ai" --version || true
"$TOOLS_BIN/gga" version || true
