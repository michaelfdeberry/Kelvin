from __future__ import annotations

import logging
from threading import Lock

from signalrcore.hub_connection_builder import HubConnectionBuilder


class ReadingsHubClient:
    def __init__(self, hub_url: str) -> None:
        self._hub_url = hub_url
        self._lock = Lock()
        self._connection = None

    def start(self) -> None:
        with self._lock:
            if self._connection is not None:
                return

            self._connection = (
                HubConnectionBuilder()
                .with_url(self._hub_url)
                .configure_logging(logging.WARNING)
                .with_automatic_reconnect(
                    {
                        "type": "raw",
                        "keep_alive_interval": 10,
                        "reconnect_interval": 5,
                        "max_attempts": 0,
                    }
                )
                .build()
            )
            self._connection.start()

    def stop(self) -> None:
        with self._lock:
            if self._connection is None:
                return

            self._connection.stop()
            self._connection = None

    def submit_reading(self, payload: dict[str, object]) -> None:
        self.start()

        with self._lock:
            if self._connection is None:
                raise RuntimeError("SignalR connection was not established.")

            self._connection.send("SubmitReading", [payload])