from __future__ import annotations

import board
import busio

from adafruit_sht4x import SHT4x

from ..models import SensorReading
from .base import SensorReader


class Sht4xSensorReader(SensorReader):
    def __init__(self) -> None:
        i2c = busio.I2C(board.SCL, board.SDA)
        self._sensor = SHT4x(i2c)

    def read(self) -> SensorReading:
        temperature, humidity = self._sensor.measurements
        return SensorReading(
            temperature_c=float(temperature),
            humidity_percentage=float(humidity),
        )