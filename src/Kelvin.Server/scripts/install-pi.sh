#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -eq 0 ]]; then
  echo "Run this script as the target gateway user, not as root."
  exit 1
fi

TARGET_USER="$(id -un)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="$(cd "${PROJECT_DIR}/.." && pwd)"
CLIENT_DIR="${SRC_DIR}/Kelvin.Client"
INSTALL_ROOT="/opt/kelvin/Kelvin.Server"
PUBLISH_DIR="${PROJECT_DIR}/publish/pi"
SERVICE_SOURCE="${PROJECT_DIR}/systemd/kelvin-server.service"
SERVICE_TARGET="/etc/systemd/system/kelvin-server.service"
NGINX_SOURCE="${PROJECT_DIR}/nginx/kelvin-server.conf"
NGINX_TARGET="/etc/nginx/sites-available/kelvin-server.conf"
NGINX_ENABLED_TARGET="/etc/nginx/sites-enabled/kelvin-server.conf"
NODE_MAJOR=22
PNPM_VERSION="11.17.0"

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Required command not found: $1"
    exit 1
  fi
}

ensure_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    return
  fi

  echo "dotnet not found. Installing .NET SDK 10.0 to ${HOME}/.dotnet"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "${HOME}/.dotnet"
  export DOTNET_ROOT="${HOME}/.dotnet"
  export PATH="${DOTNET_ROOT}:${PATH}"
}

ensure_node() {
  if command -v node >/dev/null 2>&1; then
    local major
    major="$(node -p 'process.versions.node.split(".")[0]')"
    if [[ "${major}" -ge "${NODE_MAJOR}" ]]; then
      return
    fi
    echo "Node.js $(node -v) is too old (pnpm ${PNPM_VERSION} requires >= v${NODE_MAJOR}). Upgrading."
  else
    echo "node not found. Installing Node.js ${NODE_MAJOR}.x"
  fi

  curl -fsSL "https://deb.nodesource.com/setup_${NODE_MAJOR}.x" -o /tmp/nodesource_setup.sh
  sudo -E bash /tmp/nodesource_setup.sh
  # distro npm conflicts with the npm bundled in the NodeSource nodejs package
  sudo apt-get remove -y npm || true
  sudo apt-get install -y nodejs
  hash -r
}

ensure_pnpm() {
  if command -v pnpm >/dev/null 2>&1 && pnpm --version >/dev/null 2>&1; then
    return
  fi

  echo "Installing pnpm ${PNPM_VERSION} globally."
  sudo npm install -g "pnpm@${PNPM_VERSION}"
  hash -r
}

echo "Installing Kelvin.Server dependencies from ${PROJECT_DIR}"

sudo apt-get update
sudo apt-get install -y curl ca-certificates gnupg sqlite3 nginx gpiod libgpiod3

ensure_dotnet
ensure_node
ensure_pnpm

require_command dotnet
require_command pnpm

pushd "${CLIENT_DIR}" >/dev/null
pnpm install --frozen-lockfile
pnpm build
popd >/dev/null

mkdir -p "${PUBLISH_DIR}"
dotnet publish "${PROJECT_DIR}/Kelvin.Server.csproj" -c Release -o "${PUBLISH_DIR}"

sudo mkdir -p "${INSTALL_ROOT}/app"
sudo cp -R "${PUBLISH_DIR}/." "${INSTALL_ROOT}/app/"
sudo cp "${PROJECT_DIR}/scripts/start-server.sh" "${INSTALL_ROOT}/start-server.sh"
sudo chmod +x "${INSTALL_ROOT}/start-server.sh"
sudo sed "s/^User=.*/User=${TARGET_USER}/" "${SERVICE_SOURCE}" | sudo tee "${SERVICE_TARGET}" >/dev/null
sudo cp "${NGINX_SOURCE}" "${NGINX_TARGET}"
sudo ln -sf "${NGINX_TARGET}" "${NGINX_ENABLED_TARGET}"
if [[ -e "/etc/nginx/sites-enabled/default" ]]; then
  sudo rm -f "/etc/nginx/sites-enabled/default"
fi

sudo nginx -t

sudo systemctl daemon-reload
sudo systemctl enable kelvin-server.service
sudo systemctl enable nginx
sudo systemctl restart nginx

echo "Installation complete. Start the service with:"
echo "  sudo systemctl start kelvin-server.service"
echo "  sudo systemctl status kelvin-server.service"
echo "Kelvin will be available through nginx on port 80."