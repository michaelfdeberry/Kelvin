from __future__ import annotations

import socket
import uuid


def _format_mac(mac_value: int) -> str:
    return ":".join(f"{(mac_value >> shift) & 0xFF:02x}" for shift in range(40, -1, -8))


def get_mac_address(interface_name: str | None = None) -> str:
    if interface_name:
        try:
            with open(f"/sys/class/net/{interface_name}/address", "r", encoding="utf-8") as handle:
                return handle.read().strip().lower()
        except OSError:
            pass

    mac_value = uuid.getnode()
    if (mac_value >> 40) % 2:
        hostname = socket.gethostname()
        return f"kiosk-{hostname}".lower()

    return _format_mac(mac_value)