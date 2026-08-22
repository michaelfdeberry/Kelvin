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
read -r -a chromium_args <<< "${KELVIN_CHROMIUM_ARGS:---kiosk --incognito --noerrdialogs --disable-session-crashed-bubble --disable-infobars --ozone-platform=wayland}"

# Wipe any leftover disk cache before every launch so a redeployed client build is never served stale on restart/reboot.
cache_dir="/tmp/kelvin-chromium-cache"
rm -rf "${cache_dir}"
mkdir -p "${cache_dir}"
chromium_args+=(--disk-cache-dir="${cache_dir}" --disk-cache-size=1 --media-cache-size=1)

resolve_mac_address() {
  local interface
  for interface in "${KELVIN_MAC_INTERFACE:-}" wlan0; do
    if [[ -n "${interface}" && -r "/sys/class/net/${interface}/address" ]]; then
      cat "/sys/class/net/${interface}/address"
      return
    fi
  done
}

# The client enters kiosk mode when it sees a valid ?mac= on the URL.
mac_address="$(resolve_mac_address | tr -d ':-' | tr '[:upper:]' '[:lower:]')"
if [[ "${mac_address}" =~ ^[0-9a-f]{12}$ ]]; then
  if [[ "${ui_url}" == *\?* ]]; then
    ui_url="${ui_url}&mac=${mac_address}"
  else
    ui_url="${ui_url}?mac=${mac_address}"
  fi
fi

# cage relaunches this script (via the display service's systemd Restart=) whenever Chromium exits.
exec "${browser_command}" "${chromium_args[@]}" "${ui_url}"