from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv


def _to_bool(value: str | None, default: bool) -> bool:
    if value is None:
        return default

    return value.strip().lower() in {"1", "true", "yes", "on"}


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
    sensor_type: str
    poll_interval_seconds: int
    heartbeat_seconds: int
    threshold_temperature_c: float
    threshold_humidity_percent: float
    threshold_co2_ppm: int
    browser_enabled: bool
    browser_command: str
    chromium_args: tuple[str, ...]
    mac_interface: str | None
    dht11_pin: str

    @property
    def hub_url(self) -> str:
        return f"{self.server_url.rstrip('/')}" + "/hubs/readings"


def load_config() -> KioskConfig:
    env_path = Path(__file__).resolve().parents[1] / ".env"
    load_dotenv(env_path, override=False)

    chromium_args = os.getenv("KELVIN_CHROMIUM_ARGS", "").split()

    return KioskConfig(
        server_url=os.getenv("KELVIN_SERVER_URL", "http://localhost:5194"),
        ui_url=os.getenv("KELVIN_UI_URL", "http://localhost:5173"),
        sensor_type=os.getenv("KELVIN_SENSOR_TYPE", "mock").strip().lower(),
        poll_interval_seconds=_to_int(os.getenv("KELVIN_POLL_INTERVAL_SECONDS"), 30),
        heartbeat_seconds=_to_int(os.getenv("KELVIN_HEARTBEAT_SECONDS"), 300),
        threshold_temperature_c=_to_float(os.getenv("KELVIN_THRESHOLD_TEMPERATURE_C"), 0.5),
        threshold_humidity_percent=_to_float(os.getenv("KELVIN_THRESHOLD_HUMIDITY_PERCENT"), 1.0),
        threshold_co2_ppm=_to_int(os.getenv("KELVIN_THRESHOLD_CO2_PPM"), 75),
        browser_enabled=_to_bool(os.getenv("KELVIN_BROWSER_ENABLED"), True),
        browser_command=os.getenv("KELVIN_BROWSER_COMMAND", "chromium-browser"),
        chromium_args=tuple(chromium_args),
        mac_interface=os.getenv("KELVIN_MAC_INTERFACE") or None,
        dht11_pin=os.getenv("KELVIN_DHT11_PIN", "D4"),
    )