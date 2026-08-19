#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -eq 0 ]]; then
  echo "Run this script as the target kiosk user, not as root."
  exit 1
fi

TARGET_USER="$(id -un)"
TARGET_HOME="$(getent passwd "${TARGET_USER}" | cut -d: -f6)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
SERVICE_SOURCE="${PROJECT_DIR}/systemd/kelvin-kiosk.service"
SERVICE_TARGET="/etc/systemd/system/kelvin-kiosk.service"
AUTOLOGIN_SOURCE="${PROJECT_DIR}/systemd/kelvin-autologin.conf"
AUTOLOGIN_TARGET="/etc/systemd/system/getty@tty1.service.d/kelvin-autologin.conf"
DISPLAY_PROFILE_SOURCE="${PROJECT_DIR}/scripts/kelvin-display-profile.sh"
DISPLAY_PROFILE_TARGET="/etc/profile.d/kelvin-display.sh"
XINITRC_SOURCE="${PROJECT_DIR}/scripts/kelvin-xinitrc"
XINITRC_TARGET="${TARGET_HOME}/.xinitrc"
OPENBOX_AUTOSTART_SOURCE="${PROJECT_DIR}/scripts/openbox-autostart"
OPENBOX_AUTOSTART_TARGET="${TARGET_HOME}/.config/openbox/autostart"

echo "Installing Kelvin kiosk dependencies from ${PROJECT_DIR}"

sudo apt-get update
sudo apt-get install -y python3-venv python3-pip chromium-browser xserver-xorg xinit openbox

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
  -e "s|^ExecStart=.*|ExecStart=${PROJECT_DIR}/scripts/start-kiosk.sh --skip-browser|" \
  "${SERVICE_SOURCE}" | sudo tee "${SERVICE_TARGET}" >/dev/null

sudo install -d -m 755 /etc/systemd/system/getty@tty1.service.d
sudo sed "s/KELVIN_USER/${TARGET_USER}/g" "${AUTOLOGIN_SOURCE}" | sudo tee "${AUTOLOGIN_TARGET}" >/dev/null
sudo sed "s/KELVIN_USER/${TARGET_USER}/g" "${DISPLAY_PROFILE_SOURCE}" | sudo tee "${DISPLAY_PROFILE_TARGET}" >/dev/null
sudo install -d -o "${TARGET_USER}" -g "$(id -gn)" -m 755 "${TARGET_HOME}/.config/openbox"
sudo install -o "${TARGET_USER}" -g "$(id -gn)" -m 755 "${XINITRC_SOURCE}" "${XINITRC_TARGET}"
sudo sed "s|KELVIN_PROJECT_DIR|${PROJECT_DIR}|g" "${OPENBOX_AUTOSTART_SOURCE}" | sudo tee "${OPENBOX_AUTOSTART_TARGET}" >/dev/null
sudo chown "${TARGET_USER}:$(id -gn)" "${OPENBOX_AUTOSTART_TARGET}"
sudo chmod 755 "${OPENBOX_AUTOSTART_TARGET}"

sudo systemctl disable --now kelvin-x.service 2>/dev/null || true
sudo rm -f /etc/systemd/system/kelvin-x.service
sudo systemctl daemon-reload
sudo systemctl enable getty@tty1.service
sudo systemctl enable kelvin-kiosk.service

echo "Installation complete. Review ${PROJECT_DIR}/.env, then reboot to start the kiosk display:"
echo "  sudo reboot"