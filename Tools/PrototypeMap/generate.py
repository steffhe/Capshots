#!/usr/bin/env python3
"""Generates the Prototype map: Assets/Resources/Maps/Prototype.txt (and preview.png with --preview).

An open arena in the vein of Proving Ground: a flat grid floor, three clear lanes split by two long
walls, a tower in the middle with a tunnel through it, a raised base deck at each end, and a few
blocks, crates and low walls for cover. Everything is boxes, in the same format as the Office map,
so the same builder (OfficeMap) makes it when the scene loads.

The south half is laid out here, and placed again turned 180 degrees about the middle for the north
half, so both teams get the same cover at the same distances. Orange is the south team's side,
blue the north's.

Standard library only. Run from the repository root:
    python3 Tools/PrototypeMap/generate.py --preview
"""
import math
import os
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Maps", "Prototype.txt")
PREVIEW = os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview.png")

# The player, for the checks: a 2 m capsule of radius 0.5 that steps up 0.3 m
PLAYER_RADIUS, PLAYER_HEIGHT, STEP = 0.5, 2.0, 0.3
# How high above the floor a spawn point sits (as on the other maps)
SPAWN_LIFT = 1.2

HALF = 40.0          # the playable square: -40..40 on x (west-east) and z (south-north)
WALL_HEIGHT = 6.0    # the outer walls
LANE_WALL = 4.5      # tall enough that nobody jumps over (a jump reaches about 3 m)
LOW_WALL = 1.2       # cover to crouch behind and shoot over
DECK = 2.5           # the base decks and the side-lane perches
TOWER = 3.4          # the tower's top
PARAPET = 1.0

# name: (r, g, b, a, texture, metres per repeat, smoothness, kind)
MATERIALS = {
    "floor": (0.43, 0.45, 0.48, 1, "tile", 2.0, 0.15, "opaque"),
    "edge": (0.3, 0.32, 0.35, 1, "tile", 2.0, 0.1, "opaque"),
    "wall": (0.8, 0.81, 0.83, 1, "tile", 1.0, 0.1, "opaque"),
    "centre": (0.9, 0.9, 0.88, 1, "tile", 1.0, 0.15, "opaque"),
    "crate": (0.56, 0.58, 0.61, 1, "tile", 0.5, 0.1, "opaque"),
    "trim": (0.2, 0.21, 0.23, 1, "none", 1.0, 0.4, "opaque"),
    "orange": (0.95, 0.52, 0.2, 1, "tile", 1.0, 0.15, "opaque"),
    "blue": (0.24, 0.5, 0.9, 1, "tile", 1.0, 0.15, "opaque"),
    "glow_orange": (1.0, 0.55, 0.2, 1, "none", 1.0, 0.0, "glow"),
    "glow_blue": (0.3, 0.6, 1.0, 1, "none", 1.0, 0.0, "glow"),
    "glow_white": (0.95, 0.97, 1.0, 1, "none", 1.0, 0.0, "glow"),
}
# What a material becomes on the other side of the map
SWAP = {"orange": "blue", "blue": "orange", "glow_orange": "glow_blue", "glow_blue": "glow_orange"}


class Box:
    def __init__(self, material, x0, x1, y0, y1, z0, z1, collider=True, shadows=True):
        self.material = material
        self.x0, self.x1 = min(x0, x1), max(x0, x1)
        self.y0, self.y1 = min(y0, y1), max(y0, y1)
        self.z0, self.z1 = min(z0, z1), max(z0, z1)
        self.collider = collider
        self.shadows = shadows

    def turned(self):
        """The same box turned 180 degrees about the middle of the map (for the other side)"""
        return Box(SWAP.get(self.material, self.material), -self.x1, -self.x0, self.y0, self.y1, -self.z1, -self.z0, self.collider, self.shadows)

    def line(self):
        cx, cy, cz = (self.x0 + self.x1) / 2, (self.y0 + self.y1) / 2, (self.z0 + self.z1) / 2
        sx, sy, sz = self.x1 - self.x0, self.y1 - self.y0, self.z1 - self.z0
        flags = ("" if self.collider else "n") + ("" if self.shadows else "s")
        return "b %s %s %s %s %s %s %s %s" % (self.material, f(cx), f(cy), f(cz), f(sx), f(sy), f(sz), flags or "-")


def f(v):
    s = ("%.3f" % v).rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


half = []     # the south half, placed again turned round for the north
whole = []    # things placed once (the floor and the outer walls)
spawns = []   # (x, y, z, yaw) for the south team; turned round for the north


def box(material, x0, x1, y0, y1, z0, z1, **kw):
    half.append(Box(material, x0, x1, y0, y1, z0, z1, **kw))


def stairs(material, x0, x1, z0, z1, top, going, base=0.0):
    """A flight of steps over x0..x1, z0..z1 rising from base to top in the direction going
    ('+x', '-x', '+z' or '-z'), each step at most 0.25 m up so players and bots walk it"""
    rise = top - base
    count = max(1, int(math.ceil(rise / 0.25 - 1e-6)))
    for i in range(count):
        t0, t1 = i / count, (i + 1) / count
        h = base + rise * t1
        if going == "+x":
            box(material, x0 + (x1 - x0) * t0, x0 + (x1 - x0) * t1, 0, h, z0, z1)
        elif going == "-x":
            box(material, x1 - (x1 - x0) * t1, x1 - (x1 - x0) * t0, 0, h, z0, z1)
        elif going == "+z":
            box(material, x0, x1, 0, h, z0 + (z1 - z0) * t0, z0 + (z1 - z0) * t1)
        else:
            box(material, x0, x1, 0, h, z1 - (z1 - z0) * t1, z1 - (z1 - z0) * t0)


def glow_strip(material, x0, x1, y, z0, z1):
    """A thin glowing strip (no collider) along an edge"""
    box(material, x0, x1, y - 0.06, y + 0.06, z0, z1, collider=False, shadows=False)


def layout():
    # The floor and the outer walls, once
    whole.append(Box("floor", -HALF - 1, HALF + 1, -1, 0, -HALF - 1, HALF + 1))
    for s in (-1, 1):
        whole.append(Box("edge", s * HALF, s * (HALF + 1), 0, WALL_HEIGHT, -HALF - 1, HALF + 1))
        whole.append(Box("edge", -HALF, HALF, 0, WALL_HEIGHT, s * HALF, s * (HALF + 1)))
    # A glowing line along the foot of the outer walls
    for s in (-1, 1):
        whole.append(Box("glow_white", s * (HALF - 0.05), s * HALF, 0.3, 0.42, -HALF, HALF, collider=False, shadows=False))
        whole.append(Box("glow_white", -HALF, HALF, 0.3, 0.42, s * (HALF - 0.05), s * HALF, collider=False, shadows=False))

    # --- The base deck, at the back of the south end: high ground over the spawn, with steps up
    # at both ends and a parapet along its front with a gap in the middle to drop down from
    box("orange", -10, 10, 0, DECK, -HALF, -32)
    stairs("orange", -15, -10, -37.5, -34.5, DECK, "+x")
    stairs("orange", 10, 15, -37.5, -34.5, DECK, "-x")
    box("orange", -10, -2.5, DECK, DECK + PARAPET, -32.5, -32)
    box("orange", 2.5, 10, DECK, DECK + PARAPET, -32.5, -32)
    glow_strip("glow_orange", -10, 10, DECK - 0.3, -32, -31.95)

    # --- The two long walls splitting the map into three lanes, each with two gaps to cut
    # through between lanes (the wall at x 14 turned round is the wall at x -14)
    for z0, z1 in ((-24, -10), (-7, 7), (10, 24)):
        box("wall", 13.5, 14.5, 0, LANE_WALL, z0, z1)

    # --- The tower in the middle: a slab on two blocks, with a tunnel straight through from west
    # to east underneath, steps up from the south (and, turned round, from the north), and
    # parapets round its top
    box("centre", -6, 6, 2.4, TOWER, -6, 0)
    box("centre", -6, 6, 0, 2.4, -6, -1.5)
    stairs("centre", 1, 4, -12.3, -6, TOWER, "+z")
    box("centre", -6, 1, TOWER, TOWER + PARAPET, -6, -5.6)
    box("centre", 4, 6, TOWER, TOWER + PARAPET, -6, -5.6)
    box("centre", 5.6, 6, TOWER, TOWER + PARAPET, -5.6, 0)
    box("centre", -6, -5.6, TOWER, TOWER + PARAPET, -5.6, 0)
    glow_strip("glow_white", -6, 6, 2.3, -6.05, -6)
    glow_strip("glow_white", -6, 6, 2.3, -1.5, -1.45)
    # Low walls beside the tower, to cover the run between the tunnel and the lane gaps
    box("crate", 9, 9.8, 0, LOW_WALL, -4, 1)

    # --- The middle lane, south: a crate, a low wall and a block to move between
    box("crate", -9, -6, 0, 3, -18, -15)
    box("crate", -5.5, -4, 0, 1.5, -16.5, -15)
    box("crate", 2, 8, 0, LOW_WALL, -19.4, -18.6)
    box("crate", 8, 10, 0, 2, -13, -11)
    box("wall", -3, 3, 0, LOW_WALL, -26.4, -25.6)

    # --- The west lane, south: a perch overlooking the lane, steps up behind it, a parapet along
    # its front, and a low wall and a crate further up the lane
    box("orange", -38, -30, 0, DECK, -22, -15)
    stairs("orange", -36, -33, -27, -22, DECK, "+z")
    box("orange", -38, -30, DECK, DECK + PARAPET, -15.5, -15)
    glow_strip("glow_orange", -38, -30, DECK - 0.3, -15, -14.95)
    box("crate", -30, -22, 0, LOW_WALL, -6.4, -5.6)
    box("crate", -21, -19, 0, 2, -13, -11)

    # --- The east lane, south: a crate cluster, a wall that breaks the long sight line down the
    # lane, and a low wall near the middle
    box("crate", 24.5, 27.5, 0, 3, -15.5, -12.5)
    box("crate", 22.5, 24, 0, 1.5, -13.5, -12)
    box("crate", 27.5, 29, 0, 1.5, -12, -10.5)
    box("wall", 18, 24, 0, LANE_WALL, -21.5, -20.5)
    box("crate", 30, 38, 0, LOW_WALL, -4.4, -3.6)

    # --- Spawn points for the south team: across the back of the map and up on the deck. Turned
    # round, the north team's. (In free-for-all every point is open to everyone.)
    for x, z in ((-24, -35), (-17, -29), (-6, -27.5), (6, -27.5), (17, -29), (24, -35)):
        spawns.append((x, SPAWN_LIFT, z, 0))
    for x in (-5, 5):
        spawns.append((x, DECK + SPAWN_LIFT, -36, 0))


def all_boxes():
    return whole + half + [b.turned() for b in half]


def all_spawns():
    return spawns + [(-x, y, -z, (yaw + 180) % 360) for x, y, z, yaw in spawns]


# ---------------------------------------------------------------- checks

def check(boxes, points):
    problems = []
    solid = [b for b in boxes if b.collider]
    for x, y, z, yaw in points:
        feet = y - SPAWN_LIFT
        under = [b for b in solid if b.x0 <= x <= b.x1 and b.z0 <= z <= b.z1 and abs(b.y1 - feet) < 0.01]
        if not under:
            problems.append("spawn (%s, %s) isn't standing on anything" % (f(x), f(z)))
        for b in solid:
            # The player's capsule, as a box round it, from just above the feet
            if (b.x0 < x + PLAYER_RADIUS and b.x1 > x - PLAYER_RADIUS and b.z0 < z + PLAYER_RADIUS and b.z1 > z - PLAYER_RADIUS
                    and b.y0 < feet + PLAYER_HEIGHT and b.y1 > feet + 0.05):
                problems.append("spawn (%s, %s) is blocked by a box at (%s, %s)" % (f(x), f(z), f((b.x0 + b.x1) / 2), f((b.z0 + b.z1) / 2)))
        if abs(x) > HALF - 1 or abs(z) > HALF - 1:
            problems.append("spawn (%s, %s) is outside the map" % (f(x), f(z)))
    for b in boxes:
        if b.material not in MATERIALS:
            problems.append("unknown material " + b.material)
    return problems


# ---------------------------------------------------------------- preview

def write_png(path, width, height, pixels):
    raw = b"".join(b"\x00" + bytes(pixels[y * width * 3:(y + 1) * width * 3]) for y in range(height))

    def chunk(kind, data):
        c = struct.pack(">I", len(data)) + kind + data
        return c + struct.pack(">I", zlib.crc32(kind + data) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(path, "wb") as fh:
        fh.write(png)


def preview(boxes, points, path, scale=8):
    size = int((2 * HALF + 2) * scale)
    pixels = bytearray(size * size * 3)
    top = [-2.0] * (size * size)
    for b in sorted(boxes, key=lambda b: b.y1):
        if not b.collider and b.material.startswith("glow"):
            continue
        r, g, bl = MATERIALS[b.material][:3]
        # Higher is lighter, so the levels read at a glance
        shade = min(1.0, 0.75 + b.y1 * 0.06)
        for py in range(max(0, int((HALF + 1 - b.z1) * scale)), min(size, int(math.ceil((HALF + 1 - b.z0) * scale)))):
            for px in range(max(0, int((b.x0 + HALF + 1) * scale)), min(size, int(math.ceil((b.x1 + HALF + 1) * scale)))):
                i = py * size + px
                if b.y1 >= top[i]:
                    top[i] = b.y1
                    edge = px in (int((b.x0 + HALF + 1) * scale), int(math.ceil((b.x1 + HALF + 1) * scale)) - 1) or \
                        py in (int((HALF + 1 - b.z1) * scale), int(math.ceil((HALF + 1 - b.z0) * scale)) - 1)
                    k = shade * (0.7 if edge and b.y1 > 0 else 1.0)
                    pixels[i * 3:i * 3 + 3] = bytes(int(min(255, c * k * 255)) for c in (r, g, bl))
    for n, (x, y, z, yaw) in enumerate(points):
        colour = (255, 140, 40) if z < 0 else (60, 140, 255)
        cx, cy = (x + HALF + 1) * scale, (HALF + 1 - z) * scale
        for py in range(int(cy - 5), int(cy + 6)):
            for px in range(int(cx - 5), int(cx + 6)):
                d = math.hypot(px - cx, py - cy)
                if 0 <= px < size and 0 <= py < size and d <= 5:
                    pixels[(py * size + px) * 3:(py * size + px) * 3 + 3] = bytes((255, 255, 255) if d > 3.5 else colour)
    write_png(path, size, size, pixels)


def main():
    layout()
    boxes = all_boxes()
    points = all_spawns()
    problems = check(boxes, points)
    lines = ["# Generated by Tools/PrototypeMap/generate.py. Don't edit by hand."]
    for name, (r, g, b, a, tex, tile, smooth, kind) in MATERIALS.items():
        lines.append("m %s %s %s %s %s %s %s %s %s" % (name, f(r), f(g), f(b), f(a), tex, f(tile), f(smooth), kind))
    lines += [b.line() for b in boxes]
    lines += ["s %s %s %s %s" % (f(x), f(y), f(z), f(yaw)) for x, y, z, yaw in points]
    # The map view: from behind the south base, looking up the map
    lines.append("c 0 46 -62 38 0")
    with open(OUT, "w", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")
    print("Wrote %s: %d boxes, %d spawn points" % (os.path.relpath(OUT, ROOT), len(boxes), len(points)))
    if "--preview" in sys.argv:
        preview(boxes, points, PREVIEW)
        print("Wrote " + os.path.relpath(PREVIEW, ROOT))
    for p in problems:
        print("PROBLEM: " + p)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
