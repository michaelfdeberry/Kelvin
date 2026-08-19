from __future__ import annotations

import logging
import re
import subprocess
from time import monotonic

from .config import KioskConfig
from .identity import get_mac_address


def _build_ui_url(config: KioskConfig) -> str:
    # The client enters kiosk mode when it sees a valid ?mac= on the URL.
    mac_address = re.sub(r"[^0-9a-f]", "", get_mac_address(config.mac_interface).lower())
    if len(mac_address) != 12:
        return config.ui_url

    separator = "&" if "?" in config.ui_url else "?"
    return f"{config.ui_url}{separator}mac={mac_address}"


def launch_browser(config: KioskConfig) -> subprocess.Popen[str] | None:
    if not config.browser_enabled:
        return None

    command = [config.browser_command, *config.chromium_args, _build_ui_url(config)]
    logging.info("Launching browser: %s", " ".join(command))
    return subprocess.Popen(command)


def ensure_browser_running(
    config: KioskConfig,
    browser_process: subprocess.Popen[str] | None,
    last_restart_at: float,
) -> tuple[subprocess.Popen[str] | None, float]:
    if not config.browser_enabled:
        return None, last_restart_at

    if browser_process is None:
        return launch_browser(config), monotonic()

    if browser_process.poll() is None:
        return browser_process, last_restart_at

    now = monotonic()
    if (now - last_restart_at) < config.browser_restart_seconds:
        return browser_process, last_restart_at

    logging.warning("Browser exited with code %s. Restarting Chromium.", browser_process.returncode)
    return launch_browser(config), now