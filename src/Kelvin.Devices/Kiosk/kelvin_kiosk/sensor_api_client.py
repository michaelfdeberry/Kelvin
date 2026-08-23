from __future__ import annotations

import requests

_REQUEST_TIMEOUT_SECONDS = 10


class SensorApiClient:
    def __init__(self, sensor_packets_url: str) -> None:
        self._sensor_packets_url = sensor_packets_url
        self._session = requests.Session()

    def submit_reading(self, payload: dict[str, object]) -> None:
        response = self._session.post(self._sensor_packets_url, json=payload, timeout=_REQUEST_TIMEOUT_SECONDS)
        response.raise_for_status()

    def close(self) -> None:
        self._session.close()
