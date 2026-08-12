from __future__ import annotations

import subprocess

from .config import KioskConfig


def launch_browser(config: KioskConfig) -> subprocess.Popen[str] | None:
    if not config.browser_enabled:
        return None

    command = [config.browser_command, *config.chromium_args, config.ui_url]
    return subprocess.Popen(command)