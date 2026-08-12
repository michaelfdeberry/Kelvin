from __future__ import annotations

from ..models import SensorReading
from .base import SensorReader


class Dht11SensorReader(SensorReader):
    def __init__(self, pin_name: str) -> None:
        import adafruit_dht
        import board

        pin = getattr(board, pin_name)
        self._sensor = adafruit_dht.DHT11(pin)

    def read(self) -> SensorReading:
        temperature = self._sensor.temperature
        humidity = self._sensor.humidity
        if temperature is None or humidity is None:
            raise RuntimeError("DHT11 returned an incomplete reading.")

        return SensorReading(
            temperature_c=float(temperature),
            humidity_percentage=float(humidity),
        )