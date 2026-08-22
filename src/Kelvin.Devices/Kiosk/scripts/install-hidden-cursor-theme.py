#!/usr/bin/env python3
import struct
import sys
from pathlib import Path


CURSOR_NAMES = (
    "default",
    "left_ptr",
    "pointer",
    "hand1",
    "hand2",
    "text",
    "xterm",
    "crosshair",
    "wait",
    "watch",
    "progress",
    "move",
    "grab",
    "grabbing",
    "not-allowed",
    "no-drop",
    "col-resize",
    "row-resize",
    "n-resize",
    "ne-resize",
    "e-resize",
    "se-resize",
    "s-resize",
    "sw-resize",
    "w-resize",
    "nw-resize",
)


def create_cursor() -> bytes:
    image_type = 0xFFFD0002
    nominal_size = 24
    image_position = 28

    file_header = struct.pack("<IIII", 0x72756358, 16, 0x00010000, 1)
    table_of_contents = struct.pack("<III", image_type, nominal_size, image_position)
    image_header = struct.pack(
        "<IIIIIIIII",
        36,
        image_type,
        nominal_size,
        1,
        1,
        1,
        0,
        0,
        0,
    )
    transparent_pixel = struct.pack("<I", 0)
    return file_header + table_of_contents + image_header + transparent_pixel


def main() -> None:
    if len(sys.argv) != 2:
        raise SystemExit(f"Usage: {sys.argv[0]} THEME_DIRECTORY")

    theme_directory = Path(sys.argv[1])
    cursor_directory = theme_directory / "cursors"
    cursor_directory.mkdir(parents=True, exist_ok=True)

    cursor_data = create_cursor()

    for name in CURSOR_NAMES:
        (cursor_directory / name).write_bytes(cursor_data)

    (theme_directory / "index.theme").write_text(
        "[Icon Theme]\nName=Kelvin Hidden Cursor\nComment=Transparent cursor for the Kelvin kiosk\n",
        encoding="ascii",
    )


if __name__ == "__main__":
    main()