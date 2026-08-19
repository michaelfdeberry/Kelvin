from __future__ import annotations

from ..config import KioskConfig
from .base import SensorReader


def create_sensor_reader(config: KioskConfig) -> SensorReader:
    if config.sensor_type == "sht4x":
        from .sht4x_sensor import Sht4xSensorReader

        return Sht4xSensorReader(config.i2c_port)

    if config.sensor_type == "scd4x":
        from .scd4x_sensor import Scd4xSensorReader

        return Scd4xSensorReader(config.i2c_port)

    if config.sensor_type == "mock":
        from .mock_sensor import MockSensorReader

        return MockSensorReader()

    raise ValueError(f"Unsupported KELVIN_SENSOR_TYPE: {config.sensor_type}")