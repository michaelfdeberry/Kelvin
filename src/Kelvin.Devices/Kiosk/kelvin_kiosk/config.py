from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

def _to_float(value: str | None, default: float) -> float:
    if value is None or value.strip() == "":
        return default

    return float(value)


def _to_int(value: str | None, default: int) -> int:
    if value is None or value.strip() == "":
        return default
    return int(value)

@dataclass(frozen=True)
class KioskConfig:
    server_url: str
    ui_url: str
    poll_interval_seconds: int
    heartbeat_seconds: int
    failure_backoff_seconds: int
    threshold_temperature_c: float
    threshold_humidity_percent: float
    threshold_co2_ppm: int
    log_level: str
    mac_interface: str | None
    i2c_port: str

    @property
    def sensor_packets_url(self) -> str:
        return f"{self.server_url.rstrip('/')}" + "/api/sensors/packets"


def load_config() -> KioskConfig:
    env_path = Path(__file__).resolve().parents[1] / ".env"
    load_dotenv(env_path, override=False)

    return KioskConfig(
        server_url=os.getenv("KELVIN_SERVER_URL", "http://localhost:5209"),
        ui_url=os.getenv("KELVIN_UI_URL", "http://localhost:5209"),
        poll_interval_seconds=_to_int(os.getenv("KELVIN_POLL_INTERVAL_SECONDS"), 30),
        heartbeat_seconds=_to_int(os.getenv("KELVIN_HEARTBEAT_SECONDS"), 300),
        failure_backoff_seconds=_to_int(os.getenv("KELVIN_FAILURE_BACKOFF_SECONDS"), 10),
        threshold_temperature_c=_to_float(os.getenv("KELVIN_THRESHOLD_TEMPERATURE_C"), 0.5),
        threshold_humidity_percent=_to_float(os.getenv("KELVIN_THRESHOLD_HUMIDITY_PERCENT"), 1.0),
        threshold_co2_ppm=_to_int(os.getenv("KELVIN_THRESHOLD_CO2_PPM"), 75),
        log_level=os.getenv("KELVIN_LOG_LEVEL", "INFO").strip().upper(),
        mac_interface=os.getenv("KELVIN_MAC_INTERFACE") or None,
        i2c_port=os.getenv("KELVIN_I2C_PORT", "/dev/i2c-1"),
    )