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

echo "Installing Kelvin kiosk dependencies from ${PROJECT_DIR}"

sudo apt-get update
sudo apt-get install -y python3-venv python3-pip chromium-browser

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

sudo sed "s/^User=.*/User=${TARGET_USER}/" "${SERVICE_SOURCE}" | sudo tee "${SERVICE_TARGET}" >/dev/null
sudo systemctl daemon-reload
sudo systemctl enable kelvin-kiosk.service

echo "Installation complete. Review ${PROJECT_DIR}/.env, then run:"
echo "  sudo systemctl start kelvin-kiosk.service"
echo "  sudo systemctl status kelvin-kiosk.service"