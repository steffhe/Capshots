#!/usr/bin/env python3
"""Generates the tiling normal maps for the hats (Assets/Textures/Hats) and the gun
(Assets/Textures/Gun).

    python3 Tools/SurfaceTextures/generate.py

Each map is drawn as a height pattern that wraps seamlessly, then turned into a tangent-space
normal map (OpenGL style: green is up, as Unity expects). Only the Python standard library is
needed. The hat materials that use them are in Assets/Resources/Hats; the gun's is
Assets/Materials/Gun Metal.mat.
"""
import math
import os
import random
import struct
import zlib

SIZE = 256
TEXTURES = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Textures")


def wrap(v):
    return v % SIZE


def periodic_noise(rng, cells):
    """Smooth value noise that wraps at the tile's edges, cells x cells across."""
    grid = [[rng.random() for _ in range(cells)] for _ in range(cells)]

    def sample(x, y):
        fx, fy = x / SIZE * cells, y / SIZE * cells
        x0, y0 = int(math.floor(fx)), int(math.floor(fy))
        tx, ty = fx - x0, fy - y0
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        x0, y0 = x0 % cells, y0 % cells
        x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
        a = grid[y0][x0] + (grid[y0][x1] - grid[y0][x0]) * tx
        b = grid[y1][x0] + (grid[y1][x1] - grid[y1][x0]) * tx
        return a + (b - a) * ty

    return sample


def twill(rng):
    """Cotton twill: fine diagonal ribs, with a faint cross-weave."""
    grain = periodic_noise(rng, 32)
    ribs, weave = 24, 48
    return lambda x, y: (0.5 + 0.5 * math.sin(2 * math.pi * (x + y) / SIZE * ribs)
                         + 0.35 * math.sin(2 * math.pi * (x - y) / SIZE * weave)
                         + 0.25 * grain(x, y))


def knit(rng):
    """Knitting: columns of V-shaped stitches."""
    columns, rows = 8, 12
    grain = periodic_noise(rng, 16)

    def height(x, y):
        u = (x / SIZE * columns) % 1.0
        v = (y / SIZE * rows) % 1.0
        lean = (v - 0.5) * 0.35
        left = math.exp(-((u - (0.27 + lean)) / 0.18) ** 2)
        right = math.exp(-((u - (0.73 - lean)) / 0.18) ** 2)
        bulge = math.sin(math.pi * v)
        return (left + right) * bulge + 0.15 * grain(x, y)

    return height


def felt(rng):
    """Felt: a soft, fine, irregular grain."""
    coarse, fine = periodic_noise(rng, 16), periodic_noise(rng, 64)
    return lambda x, y: 0.6 * coarse(x, y) + 0.4 * fine(x, y)


def satin(rng):
    """Satin: very fine lines across, barely there."""
    grain = periodic_noise(rng, 64)
    return lambda x, y: 0.5 + 0.5 * math.sin(2 * math.pi * y / SIZE * 64) + 0.3 * grain(x, y)


def brushed(rng):
    """Brushed metal: long streaks along the tile."""
    rows = [rng.random() for _ in range(SIZE)]
    streak = periodic_noise(rng, 8)

    def height(x, y):
        # Each row's streak, softened with its neighbours, drifting slowly along it
        r = (rows[y % SIZE] * 0.5 + rows[(y + 1) % SIZE] * 0.25 + rows[(y - 1) % SIZE] * 0.25)
        return r * 0.7 + 0.3 * streak(x, y)

    return height


def machined(rng):
    """Machined metal: very fine tool marks along the part, with the odd faint scratch across."""
    rows = [rng.random() for _ in range(SIZE)]
    scratches = [(rng.uniform(0, SIZE), rng.uniform(0, SIZE), rng.uniform(-0.4, 0.4)) for _ in range(6)]

    def height(x, y):
        h = rows[y % SIZE] * 0.6 + rows[(y + 1) % SIZE] * 0.2 + rows[(y - 1) % SIZE] * 0.2
        for sx, sy, slope in scratches:
            # A thin line across, wrapping round the tile
            d = abs(((y - sy - (x - sx) * slope) + SIZE / 2) % SIZE - SIZE / 2)
            if d < 1.0:
                h -= 0.6 * (1.0 - d)
        return h

    return height


def hammered(rng):
    """Hammered metal: overlapping shallow round dents."""
    dents = [(rng.uniform(0, SIZE), rng.uniform(0, SIZE), rng.uniform(16, 30), rng.uniform(0.6, 1.0))
             for _ in range(45)]

    def height(x, y):
        h = 0.0
        for cx, cy, radius, depth in dents:
            dx = (x - cx + SIZE / 2) % SIZE - SIZE / 2
            dy = (y - cy + SIZE / 2) % SIZE - SIZE / 2
            d2 = (dx * dx + dy * dy) / (radius * radius)
            if d2 < 1.0:
                h = min(h, -depth * (1.0 - d2) ** 2)
        return h

    return height


# name: (folder in Assets/Textures, pattern, strength): strength scales the slopes, so how bumpy
# the surface looks
MAPS = {
    "HatTwill_Normal": ("Hats", twill, 0.7),
    "HatKnit_Normal": ("Hats", knit, 2.0),
    "HatFelt_Normal": ("Hats", felt, 2.5),
    "HatSatin_Normal": ("Hats", satin, 0.6),
    "HatBrushed_Normal": ("Hats", brushed, 0.8),
    "HatHammered_Normal": ("Hats", hammered, 3.2),
    "GunMachined_Normal": ("Gun", machined, 0.5),
}


def normal_map(height, strength):
    h = [[height(x, y) for x in range(SIZE)] for y in range(SIZE)]
    pixels = bytearray()
    for y in range(SIZE):
        row = bytearray([0])  # PNG filter: none
        for x in range(SIZE):
            # Image rows run down, texture v runs up: up the texture is the row above
            dx = (h[y][wrap(x + 1)] - h[y][wrap(x - 1)]) * 0.5
            dv = (h[wrap(y - 1)][x] - h[wrap(y + 1)][x]) * 0.5
            nx, ny, nz = -dx * strength, -dv * strength, 1.0
            length = math.sqrt(nx * nx + ny * ny + nz * nz)
            for c in (nx, ny, nz):
                row.append(max(0, min(255, int(round((c / length * 0.5 + 0.5) * 255)))))
        pixels += row
    return bytes(pixels)


def write_png(path, rgb):
    def chunk(kind, data):
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(rgb, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def main():
    for i, (name, (folder, pattern, strength)) in enumerate(MAPS.items()):
        rng = random.Random(1000 + i)
        out = os.path.join(TEXTURES, folder)
        os.makedirs(out, exist_ok=True)
        path = os.path.join(out, name + ".png")
        write_png(path, normal_map(pattern(rng), strength))
        print("wrote", os.path.relpath(path))


if __name__ == "__main__":
    main()
