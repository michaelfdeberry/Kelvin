#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -eq 0 ]]; then
  echo "Run this script as the target kiosk user, not as root."
  exit 1
fi

TARGET_USER="$(id -un)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
SERVICE_SOURCE="${PROJECT_DIR}/systemd/kelvin-kiosk.service"
SERVICE_TARGET="/etc/systemd/system/kelvin-kiosk.service"
DISPLAY_SERVICE_SOURCE="${PROJECT_DIR}/systemd/kelvin-kiosk-display.service"
DISPLAY_SERVICE_TARGET="/etc/systemd/system/kelvin-kiosk-display.service"

echo "Installing Kelvin kiosk dependencies from ${PROJECT_DIR}"

sudo apt-get update
sudo apt-get install -y python3-venv python3-pip chromium cage i2c-tools fonts-noto-color-emoji
sudo fc-cache -f

# The scd4x sensor needs /dev/i2c-1, which Raspberry Pi OS does not expose until the I2C interface is enabled.
if command -v raspi-config >/dev/null 2>&1; then
  sudo raspi-config nonint do_i2c 0
fi

cd "${PROJECT_DIR}"

if [[ ! -d ".venv" ]]; then
  python3 -m venv .venv
fi

source .venv/bin/activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt

if [[ ! -f ".env" ]]; then
  cp .env.example .env
  echo "Created ${PROJECT_DIR}/.env from template. Update it before starting the service."
fi

sudo sed \
  -e "s/^User=.*/User=${TARGET_USER}/" \
  -e "s|^WorkingDirectory=.*|WorkingDirectory=${PROJECT_DIR}|" \
  -e "s|^ExecStart=.*|ExecStart=${PROJECT_DIR}/scripts/start-kiosk.sh|" \
  "${SERVICE_SOURCE}" | sudo tee "${SERVICE_TARGET}" >/dev/null

sudo sed \
  -e "s/^User=.*/User=${TARGET_USER}/" \
  -e "s|^WorkingDirectory=.*|WorkingDirectory=${PROJECT_DIR}|" \
  -e "s|^ExecStart=.*|ExecStart=/usr/bin/cage -- ${PROJECT_DIR}/scripts/start-browser.sh|" \
  "${DISPLAY_SERVICE_SOURCE}" | sudo tee "${DISPLAY_SERVICE_TARGET}" >/dev/null

# cage owns tty1 directly (PAMName=login + TTYPath in the unit); the console login getty would otherwise fight it for the same tty.
sudo systemctl disable --now getty@tty1.service 2>/dev/null || true
sudo systemctl daemon-reload
sudo systemctl enable kelvin-kiosk.service
sudo systemctl enable kelvin-kiosk-display.service

echo "Installation complete. Review ${PROJECT_DIR}/.env, then reboot to start the kiosk display:"
echo "  sudo reboot"