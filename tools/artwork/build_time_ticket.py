"""Build the controller atlas from Spleen's spleen-16x32.bdf (Pillow required).

Source: https://github.com/fcambus/spleen/tree/2046e7cfca6a8ac12ae8c2c8731b8451c3001fbe
Usage: python tools/artwork/build_time_ticket.py path/to/spleen-16x32.bdf
"""

from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageOps


ASSETS = Path(__file__).resolve().parents[2] / "StopwatchOverlay.Desktop/Assets/Nika/ControllerDigits"
PAPER, INK = "#efc383", "#422e22"
SHADOW, FOLD, LIGHT = "#775036", "#ba8052", "#ffe4ab"
HEIGHT, SLOT_WIDTH = 37, 24


def clock_glyphs(bdf):
    glyphs = {}
    for entry in bdf.split("STARTCHAR ")[1:]:
        lines = entry.splitlines()
        code = int(next(line.split()[1] for line in lines if line.startswith("ENCODING ")))
        if code not in map(ord, "0123456789:.-"):
            continue
        assert "BBX 16 32 0 -6" in lines
        rows = lines[lines.index("BITMAP") + 1:lines.index("ENDCHAR")]
        assert len(rows) == 32
        glyphs[chr(code)] = Image.frombytes("1", (16, 32), bytes.fromhex("".join(rows)))
    assert len(glyphs) == 13
    return glyphs


def paper(width):
    tile = Image.new("RGBA", (width, HEIGHT))
    draw = ImageDraw.Draw(tile)
    draw.rectangle((0, 2, width - 1, 34), fill=SHADOW)
    draw.rectangle((0, 3, width - 1, 32), fill=PAPER)
    draw.line((0, 3, width - 1, 3), fill=LIGHT)
    draw.line((0, 5, width - 1, 5), fill=FOLD)
    draw.line((0, 30, width - 1, 30), fill=FOLD)
    return tile


def end_cap():
    cap = paper(9)
    draw = ImageDraw.Draw(cap)
    for y in range(2, 35):
        distance = abs(y - 18)
        inset = max(0, 4 - distance, 4 - (y - 2), 4 - (34 - y))
        if inset:
            draw.line((0, y, inset - 1, y), fill=(0, 0, 0, 0))
        draw.point((inset, y), fill=SHADOW)
    for y in range(8, 29, 4):
        draw.line((6, y, 6, y + 1), fill=FOLD)
    return cap


def build(bdf_path):
    glyphs = clock_glyphs(bdf_path.read_text(encoding="ascii"))
    atlas = Image.new("RGBA", (16 * SLOT_WIDTH, HEIGHT))
    # Slot order is shared with ControllerDigitAtlas. Keep punctuation and blank
    # separators equally wide so blinking doesn't move the digits or ticket ends.
    for index, char in enumerate("0123456789:. <>-"):
        if char in "<>":
            tile = end_cap()
            if char == ">":
                tile = ImageOps.mirror(tile)
        else:
            tile = paper(19 if char.isdigit() else 16 if char == "-" else 8)
            if char != " ":
                mask = glyphs[char]
                left, _, right, _ = mask.getbbox()
                mask = mask.crop((left, 0, right, 32))
                assert mask.width <= tile.width
                tile.paste(INK, ((tile.width - mask.width) // 2, 2,
                                  (tile.width - mask.width) // 2 + mask.width, 34), mask)
        atlas.paste(tile, (index * SLOT_WIDTH, 0))
    # The other faces also use the controller's 96 x 148 atlas slots.
    atlas.resize((atlas.width * 4, HEIGHT * 4), Image.Resampling.NEAREST).save(ASSETS / "time-ticket.png", optimize=True)


if __name__ == "__main__":
    build(Path(sys.argv[1]))
