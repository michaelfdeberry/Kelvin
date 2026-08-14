from __future__ import annotations

import time

import board
import busio

from adafruit_scd4x import SCD4X

from ..models import SensorReading
from .base import SensorReader


class Scd4xSensorReader(SensorReader):
    def __init__(self) -> None:
        i2c = busio.I2C(board.SCL, board.SDA)
        self._sensor = SCD4X(i2c)
        self._sensor.start_periodic_measurement()

    def read(self) -> SensorReading:
        timeout_at = time.monotonic() + 5.0
        while not self._sensor.data_ready:
            if time.monotonic() >= timeout_at:
                raise TimeoutError("Timed out waiting for SCD4X data.")
            time.sleep(0.25)

        return SensorReading(
            temperature_c=float(self._sensor.temperature),
            humidity_percentage=float(self._sensor.relative_humidity),
            co2_level_ppm=int(self._sensor.CO2),
        )