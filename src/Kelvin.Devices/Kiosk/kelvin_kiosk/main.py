from __future__ import annotations

import argparse
import logging
import sys
import time

from .browser import ensure_browser_running, launch_browser
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

    config = load_config()
    logging.basicConfig(level=getattr(logging, config.log_level, logging.INFO), format="%(asctime)s %(levelname)s %(message)s")

    logging.info("Starting Kelvin kiosk with sensor type '%s' against %s", config.sensor_type, config.server_url)

    sensor = create_sensor_reader(config)
    change_detector = ChangeDetector(config)
    mac_address = get_mac_address(config.mac_interface)
    hub_client = ReadingsHubClient(config.hub_url)

    browser_process = None
    last_browser_restart_at = 0.0
    if not args.skip_browser:
        browser_process = launch_browser(config)
        if browser_process is not None:
            last_browser_restart_at = time.monotonic()

    try:
        while True:
            browser_process, last_browser_restart_at = ensure_browser_running(config, browser_process, last_browser_restart_at)

            try:
                reading = sensor.read()
                if change_detector.should_send(reading):
                    payload = reading.to_signalr_payload(mac_address)
                    hub_client.submit_reading(payload)
                    logging.info("Submitted reading for %s: %s", mac_address, payload)
                else:
                    logging.info("Skipped reading for %s because thresholds were not met.", mac_address)
            except KeyboardInterrupt:
                raise
            except Exception:
                logging.exception("Kiosk loop failed; retrying in %s seconds.", config.failure_backoff_seconds)
                time.sleep(config.failure_backoff_seconds)
                continue

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