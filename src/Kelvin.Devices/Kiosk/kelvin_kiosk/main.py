from __future__ import annotations

import logging
import time

from .change_detection import ChangeDetector
from .config import load_config
from .identity import get_mac_address
from .sensor_api_client import SensorApiClient
from .scd4x_sensor import Scd4xSensorReader


def main() -> int:
    config = load_config()
    logging.basicConfig(level=getattr(logging, config.log_level, logging.INFO), format="%(asctime)s %(levelname)s %(message)s")

    logging.info("Starting Kelvin kiosk against %s", config.server_url)

    sensor = Scd4xSensorReader(config.i2c_port)
    change_detector = ChangeDetector(config)
    mac_address = get_mac_address(config.mac_interface)
    api_client = SensorApiClient(config.sensor_packets_url)

    try:
        while True:
            try:
                reading = sensor.read()
                if change_detector.should_send(reading):
                    payload = reading.to_sensor_packet_payload(mac_address)
                    api_client.submit_reading(payload)
                    logging.info("Submitted reading for %s: %s", mac_address, payload)
                else:
                    logging.info("Skipped reading for %s because thresholds were not met.", mac_address)
            except KeyboardInterrupt:
                raise
            except Exception:
                logging.exception("Kiosk loop failed; retrying in %s seconds.", config.failure_backoff_seconds)
                time.sleep(config.failure_backoff_seconds)
                continue

            time.sleep(config.poll_interval_seconds)
    except KeyboardInterrupt:
        logging.info("Stopping kiosk service.")
    finally:
        api_client.close()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())