#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
APP_DIR="${INSTALL_ROOT}/Kelvin.Server/app"

if [[ ! -f "${APP_DIR}/Kelvin.Server.dll" ]]; then
  echo "Kelvin.Server publish output not found at ${APP_DIR}. Run scripts/install-pi.sh first."
  exit 1
fi

if [[ -d "${HOME}/.dotnet" ]]; then
  export DOTNET_ROOT="${HOME}/.dotnet"
  export PATH="${DOTNET_ROOT}:${PATH}"
fi

export DOTNET_IOT_LIBGPIOD_DRIVER_VERSION=V2
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:5209}"

cd "${APP_DIR}"
exec dotnet Kelvin.Server.dll