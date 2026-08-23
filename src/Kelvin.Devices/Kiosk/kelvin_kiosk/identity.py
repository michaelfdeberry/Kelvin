from __future__ import annotations

import socket
import uuid


def _format_mac(mac_value: int) -> str:
    return ":".join(f"{(mac_value >> shift) & 0xFF:02x}" for shift in range(40, -1, -8))


def get_mac_address(interface_name: str | None = None) -> str:
    # Mirror start-browser.sh's fallback order so the reported MAC always matches the ?mac= on the kiosk URL.
    for interface in (interface_name, "wlan0"):
        if not interface:
            continue
        try:
            with open(f"/sys/class/net/{interface}/address", "r", encoding="utf-8") as handle:
                return handle.read().strip().lower()
        except OSError:
            continue

    mac_value = uuid.getnode()
    if (mac_value >> 40) % 2:
        hostname = socket.gethostname()
        return f"kiosk-{hostname}".lower()

    return _format_mac(mac_value)