#!/usr/bin/env python3
"""Draws the app icons of Pixel Tuner Studio and Pixel Tuner Stage and writes them into the heads.

Both share the pink ring. Studio has the pixel grid it edits, Stage the play sign of a
player. Run from anywhere after changing the drawing:  python3 scripts/icons/make-icons.py
Needs Pillow.
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
RING = (232, 62, 140, 255)
BLUE = (42, 157, 244, 255)
DARK = (27, 29, 34, 255)
SCALE = 4  # draw large and scale down for smooth edges


def ring(draw: ImageDraw.ImageDraw, size: int) -> tuple[int, int, int, int]:
    """Draws the ring and returns the box of the inner disc."""
    outer = size * 0.375
    inner = size * 0.27
    c = size / 2
    draw.ellipse((c - outer, c - outer, c + outer, c + outer), fill=RING)
    return (round(c - inner), round(c - inner), round(c + inner), round(c + inner))


def studio(size: int, background=None) -> Image.Image:
    big = size * SCALE
    image = Image.new("RGBA", (big, big), background or (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    box = ring(draw, big)
    grid = Image.new("RGBA", (box[2] - box[0], box[3] - box[1]), DARK)
    cells = 5
    step = grid.width / cells
    grid_draw = ImageDraw.Draw(grid)
    for y in range(cells):
        for x in range(cells):
            if (x + y) % 2 == 0:
                grid_draw.rectangle((x * step, y * step, (x + 1) * step, (y + 1) * step), fill=BLUE)
    mask = Image.new("L", grid.size, 0)
    ImageDraw.Draw(mask).ellipse((0, 0, grid.width - 1, grid.height - 1), fill=255)
    image.paste(grid, box[:2], mask)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def stage(size: int, background=None) -> Image.Image:
    big = size * SCALE
    image = Image.new("RGBA", (big, big), background or (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    left, top, right, bottom = ring(draw, big)
    draw.ellipse((left, top, right, bottom), fill=DARK)
    # Play sign, nudged right so it looks centred.
    r = (right - left) / 2
    cx, cy = (left + right) / 2 + r * 0.12, (top + bottom) / 2
    h = r * 0.62
    draw.polygon([(cx - h * 0.85, cy - h), (cx - h * 0.85, cy + h), (cx + h, cy)], fill=BLUE)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def write(image: Image.Image, relative: str, **kwargs) -> None:
    path = ROOT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, **kwargs)
    print("wrote", relative)


def main() -> None:
    # Studio: window icon (shared UI) and the Windows .exe icon.
    write(studio(256), "src/PixelTuner.UI/Assets/studio_icon.png", optimize=True)
    write(studio(256), "src/Heads/PixelTuner.Studio.Desktop/app-icon.ico",
          sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    # Stage on Android: launcher icon with transparency.
    write(stage(512), "src/Heads/PixelTuner.Stage.Android/Resources/drawable/icon.png", optimize=True)
    # Stage on iOS: one 1024 icon; the App Store wants it opaque, so the ring sits on dark.
    write(stage(1024, background=DARK).convert("RGB"),
          "src/Heads/PixelTuner.Stage.iOS/Assets.xcassets/AppIcon.appiconset/icon-1024.png", optimize=True)


if __name__ == "__main__":
    main()
