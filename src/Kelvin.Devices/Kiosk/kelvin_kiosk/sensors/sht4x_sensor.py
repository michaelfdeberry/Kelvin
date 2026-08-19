from __future__ import annotations

from sensirion_driver_adapters.i2c_adapter.i2c_channel import I2cChannel
from sensirion_i2c_driver import CrcCalculator, I2cConnection, LinuxI2cTransceiver
from sensirion_i2c_sht4x.device import Sht4xDevice

from ..models import SensorReading
from .base import SensorReader

_I2C_ADDRESS = 0x44


class Sht4xSensorReader(SensorReader):
    def __init__(self, i2c_port: str) -> None:
        self._transceiver = LinuxI2cTransceiver(i2c_port)
        channel = I2cChannel(
            I2cConnection(self._transceiver),
            slave_address=_I2C_ADDRESS,
            crc=CrcCalculator(8, 0x31, 0xFF, 0x0),
        )
        self._sensor = Sht4xDevice(channel)

    def read(self) -> SensorReading:
        temperature, humidity = self._sensor.measure_high_precision()
        return SensorReading(
            temperature_c=float(temperature.value),
            humidity_percentage=float(humidity.value),
        )