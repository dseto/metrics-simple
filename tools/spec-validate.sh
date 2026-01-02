#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="${1:-$(pwd)}"
MANIFEST="$REPO_ROOT/spec-deck-manifest.json"

if [[ ! -f "$MANIFEST" ]]; then
  echo "spec-deck-manifest.json not found at $MANIFEST" >&2
  exit 1
fi

missing=0
paths=$(python - <<'PY'
import json,sys
m=json.load(open(sys.argv[1],'r',encoding='utf-8'))
for f in m.get('files',[]):
    print(f['path'])
PY
"$MANIFEST")

while IFS= read -r p; do
  if [[ ! -f "$REPO_ROOT/$p" ]]; then
    echo "Missing: $p" >&2
    missing=1
  fi
done <<< "$paths"

if [[ $missing -ne 0 ]]; then
  exit 1
fi

echo "OK: all manifest paths exist."
