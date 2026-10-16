#!/usr/bin/env bash
# Runs every ImageShot extension test. Exits non-zero if any test fails.
#
#   CHROME_PATH=/path/to/chrome ./run-all.sh        (default: newest /opt/pw-browsers Chromium, else Playwright's own)
#   TEST_PORT=8765                                   (port for the local test site)
set -u

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
EXT="$(dirname "$HERE")"
PORT="${TEST_PORT:-8765}"
export TEST_SITE="http://127.0.0.1:${PORT}"

if [[ -z "${CHROME_PATH:-}" ]]; then
  CHROME_PATH="$(ls -d /opt/pw-browsers/chromium-*/chrome-linux*/chrome 2>/dev/null | sort -V | tail -n 1 || true)"
fi
if [[ -n "${CHROME_PATH}" ]]; then export CHROME_PATH; else unset CHROME_PATH; fi

# Playwright: tests/node_modules (npm install) or whatever NODE_PATH points at.
if [[ -d "${HERE}/node_modules" ]]; then
  export NODE_PATH="${HERE}/node_modules${NODE_PATH:+:${NODE_PATH}}"
fi

for cmd in node python3 xvfb-run; do
  command -v "$cmd" >/dev/null 2>&1 || { echo "missing required command: $cmd" >&2; exit 2; }
done

python3 -m http.server "$PORT" --bind 127.0.0.1 --directory "${HERE}/site" >/dev/null 2>&1 &
SERVER_PID=$!
trap 'kill "$SERVER_PID" 2>/dev/null' EXIT

for _ in $(seq 1 50); do
  python3 - "$PORT" <<'EOF' && break
import socket, sys
s = socket.socket()
s.settimeout(0.2)
sys.exit(0 if s.connect_ex(("127.0.0.1", int(sys.argv[1]))) == 0 else 1)
EOF
  sleep 0.1
done
if ! kill -0 "$SERVER_PID" 2>/dev/null; then
  echo "could not start the test site on port ${PORT} (already in use?)" >&2
  exit 2
fi

failed=()

echo "=== node --check"
while IFS= read -r -d '' js; do
  node --check "$js" || failed+=("node --check ${js#"${EXT}/"}")
done < <(find "$EXT" -name '*.js' -not -path '*/node_modules/*' -print0)

echo "=== static-check.js"
node "${HERE}/static-check.js" || failed+=("static-check.js")

for t in run.js crop.js draw.js region.js esc.js tabs.js locale.js; do
  echo
  echo "=== ${t}"
  if timeout 300 xvfb-run -a -s "-screen 0 1280x900x24" node "${HERE}/${t}"; then
    echo "PASS ${t}"
  else
    echo "FAIL ${t}"
    failed+=("$t")
  fi
done

echo
if (( ${#failed[@]} )); then
  echo "FAILED: ${failed[*]}"
  exit 1
fi
echo "ALL TESTS PASSED"
