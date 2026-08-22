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

            handler = logging.StreamHandler()
            self._connection = (
                HubConnectionBuilder()
                # signalrcore's hand-rolled WebSocket client corrupts frame parsing when the
                # HTTP 101 response and first frame arrive in the same TCP read (common over
                # a local reverse proxy), surfacing as UnicodeDecodeError - long polling avoids
                # that raw frame parser entirely.
                .with_url(self._hub_url)
                .configure_logging(logging.DEBUG, socket_trace=True, handler=handler)
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
            logging.info("Connecting to SignalR hub at %s", self._hub_url)
            self._connection.start()

    def stop(self) -> None:
        with self._lock:
            if self._connection is None:
                return

            self._connection.stop()
            self._connection = None

    def reset(self) -> None:
        logging.warning("Resetting SignalR hub connection.")
        self.stop()

    def submit_reading(self, payload: dict[str, object]) -> None:
        try:
            self.start()

            with self._lock:
                if self._connection is None:
                    raise RuntimeError("SignalR connection was not established.")

                self._connection.send("SubmitReading", [payload])
        except Exception:
            self.reset()
            raise