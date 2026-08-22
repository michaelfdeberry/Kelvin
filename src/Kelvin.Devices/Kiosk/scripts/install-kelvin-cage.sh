#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
CAGE_VERSION="0.2.0"
WORK_DIR="$(mktemp -d)"

cleanup() {
  rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

curl -fsSL "https://github.com/cage-kiosk/cage/archive/refs/tags/v${CAGE_VERSION}.tar.gz" \
  -o "${WORK_DIR}/cage.tar.gz"
tar -xzf "${WORK_DIR}/cage.tar.gz" -C "${WORK_DIR}"
python3 "${SCRIPT_DIR}/patch-cage-cursor.py" "${WORK_DIR}/cage-${CAGE_VERSION}/seat.c"

meson setup "${WORK_DIR}/build" "${WORK_DIR}/cage-${CAGE_VERSION}" \
  --buildtype=release \
  -Dman-pages=disabled
meson compile -C "${WORK_DIR}/build"
sudo install -m 0755 "${WORK_DIR}/build/cage" /usr/local/bin/kelvin-cage