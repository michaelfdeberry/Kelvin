from __future__ import annotations

import time

from sensirion_driver_adapters.i2c_adapter.i2c_channel import I2cChannel
from sensirion_i2c_driver import CrcCalculator, I2cConnection, LinuxI2cTransceiver
from sensirion_i2c_scd4x.device import Scd4xDevice

from .models import SensorReading

_I2C_ADDRESS = 0x62

class Scd4xSensorReader:
    def __init__(self, i2c_port: str) -> None:
        self._transceiver = LinuxI2cTransceiver(i2c_port)
        channel = I2cChannel(
            I2cConnection(self._transceiver),
            slave_address=_I2C_ADDRESS,
            crc=CrcCalculator(8, 0x31, 0xFF, 0x0),
        )
        self._sensor = Scd4xDevice(channel)
        self._sensor.wake_up()
        self._sensor.stop_periodic_measurement()
        self._sensor.reinit()
        self._sensor.start_periodic_measurement()

    def read(self) -> SensorReading:
        timeout_at = time.monotonic() + 5.0
        while not self._sensor.get_data_ready_status():
            if time.monotonic() >= timeout_at:
                raise TimeoutError("Timed out waiting for SCD4X data.")
            time.sleep(0.25)

        co2_concentration, temperature, relative_humidity = self._sensor.read_measurement()
        
        return SensorReading(
            temperature_c=float(temperature.value),
            humidity_percentage=float(relative_humidity.value),
            co2_level_ppm=int(co2_concentration.value),
        )