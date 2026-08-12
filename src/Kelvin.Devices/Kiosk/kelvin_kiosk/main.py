from __future__ import annotations

import argparse
import logging
import sys
import time

from .browser import launch_browser
from .change_detection import ChangeDetector
from .config import load_config
from .identity import get_mac_address
from .signalr_client import ReadingsHubClient
from .sensors import create_sensor_reader


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Kelvin Raspberry Pi kiosk service")
    parser.add_argument("--once", action="store_true", help="Read and submit one sample, then exit")
    parser.add_argument("--skip-browser", action="store_true", help="Do not launch Chromium")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    config = load_config()
    sensor = create_sensor_reader(config)
    change_detector = ChangeDetector(config)
    mac_address = get_mac_address(config.mac_interface)
    hub_client = ReadingsHubClient(config.hub_url)

    browser_process = None
    if not args.skip_browser:
        browser_process = launch_browser(config)

    try:
        while True:
            reading = sensor.read()
            if change_detector.should_send(reading):
                payload = reading.to_signalr_payload(mac_address)
                hub_client.submit_reading(payload)
                logging.info("Submitted reading for %s: %s", mac_address, payload)
            else:
                logging.info("Skipped reading for %s because thresholds were not met.", mac_address)

            if args.once:
                break

            time.sleep(config.poll_interval_seconds)
    except KeyboardInterrupt:
        logging.info("Stopping kiosk service.")
    finally:
        hub_client.stop()
        if browser_process is not None and browser_process.poll() is None:
            browser_process.terminate()

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))