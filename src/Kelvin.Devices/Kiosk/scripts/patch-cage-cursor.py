#!/usr/bin/env python3
import sys
from pathlib import Path


CAPABILITY_CURSOR = """\
\t/* Hide cursor if the seat doesn't have pointer capability. */
\tif ((caps & WL_SEAT_CAPABILITY_POINTER) == 0) {
\t\twlr_cursor_unset_image(seat->cursor);
\t} else {
\t\twlr_cursor_set_xcursor(seat->cursor, seat->xcursor_manager, DEFAULT_XCURSOR);
\t}
"""

CLIENT_CURSOR = """\
\tstruct wlr_seat_pointer_request_set_cursor_event *event = data;
\tstruct wlr_surface *focused_surface = event->seat_client->seat->pointer_state.focused_surface;
\tbool has_focused = focused_surface != NULL && focused_surface->resource != NULL;
\tstruct wl_client *focused_client = NULL;
\tif (has_focused) {
\t\tfocused_client = wl_resource_get_client(focused_surface->resource);
\t}

\t/* This can be sent by any client, so we check to make sure
\t * this one actually has pointer focus first. */
\tif (focused_client == event->seat_client->client) {
\t\twlr_cursor_set_surface(seat->cursor, event->surface, event->hotspot_x, event->hotspot_y);
\t}
"""


def replace_once(source: str, old: str, new: str) -> str:
    if source.count(old) != 1:
        raise RuntimeError("Expected Cage 0.2.0 cursor block was not found exactly once")
    return source.replace(old, new)


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(f"Usage: {sys.argv[0]} CAGE_SEAT_SOURCE")

    source_path = Path(sys.argv[1])
    source = source_path.read_text(encoding="utf-8")
    source = replace_once(source, CAPABILITY_CURSOR, "\twlr_cursor_unset_image(seat->cursor);\n")
    source = replace_once(source, CLIENT_CURSOR, "\t(void)data;\n\twlr_cursor_unset_image(seat->cursor);\n")
    source_path.write_text(source, encoding="utf-8")


if __name__ == "__main__":
    main()