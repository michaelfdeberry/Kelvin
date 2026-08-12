from __future__ import annotations

import logging
import subprocess
from time import monotonic

from .config import KioskConfig


def launch_browser(config: KioskConfig) -> subprocess.Popen[str] | None:
    if not config.browser_enabled:
        return None

    command = [config.browser_command, *config.chromium_args, config.ui_url]
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