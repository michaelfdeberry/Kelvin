#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${PROJECT_DIR}"

if [[ -f ".env" ]]; then
  set -a
  # shellcheck disable=SC1091
  source ".env"
  set +a
fi

case "${KELVIN_BROWSER_ENABLED:-true}" in
  1|true|TRUE|yes|YES|on|ON) ;;
  *) exit 0 ;;
esac

browser_command="${KELVIN_BROWSER_COMMAND:-chromium}"
ui_url="${KELVIN_UI_URL:-http://localhost:5209}"
read -r -a chromium_args <<< "${KELVIN_CHROMIUM_ARGS:---kiosk --incognito --noerrdialogs --disable-session-crashed-bubble --disable-infobars}"

while true; do
  "${browser_command}" "${chromium_args[@]}" "${ui_url}"
  sleep 5
done