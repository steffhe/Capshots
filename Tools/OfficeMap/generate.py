#!/usr/bin/env python3
"""Builds the Office map from layout.py and writes it as a list of boxes for OfficeMap.cs.

    python3 Tools/OfficeMap/generate.py            # writes Assets/Resources/Maps/Office.txt
    python3 Tools/OfficeMap/generate.py --preview  # also draws a top-down preview PNG

Only the Python standard library is needed (the preview also needs Pillow).

Output format, one item per line (world units are metres, y up, the office floor at y = 0):
    m <name> <r> <g> <b> <a> <texture> <tile> <smoothness> <kind>   a material
    b <material> <cx> <cy> <cz> <sx> <sy> <sz> <flags>              a box
    s <x> <y> <z> <yaw>                                             a spawn point
    l <x> <y> <z> <yaw> <height> <r> <g> <b> <text...>              a sign
    c <x> <y> <z> <pitch> <yaw>                                     the map camera
    p <cx> <cy> <cz> <sx> <sy> <sz> <frost cy> <frost sy>          a glass pane that shatters
Box flags: '-' for none, else any of n (no collider), g (collider on the Glass layer),
s (casts no shadow), o (collider only, not drawn). A material kind is opaque, glass or glow.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import layout as L  # noqa: E402

S = L.SCALE
CX = (96 + 1347) / 2.0
CY = (55 + 1090) / 2.0

H = 3.2            # ceiling height
WALL = 0.12        # partition wall thickness
GLASS_T = 0.1      # glass wall (frame) thickness
FRAME_INSET = 0.003  # keeps glass wall frames off the faces of the walls they meet
EXT_T = 0.3        # outside wall thickness
DOOR_W = 1.5
DOOR_H = 2.5
GARDEN_Y = -3.75   # the courtyard is one floor down
GROUND_Y = -7.5    # street level, two floors down

MATERIALS = [
    # name, r, g, b, a, texture, tile (m), smoothness, kind
    ("wood", 0.66, 0.48, 0.32, 1, "wood", 2.4, 0.45, "opaque"),
    ("carpet", 0.40, 0.42, 0.46, 1, "carpet", 1.2, 0.05, "opaque"),
    ("carpet_blue", 0.27, 0.33, 0.50, 1, "carpet", 1.2, 0.05, "opaque"),
    ("vinyl", 0.78, 0.78, 0.76, 1, "tile", 0.6, 0.3, "opaque"),
    ("rubber", 0.20, 0.21, 0.23, 1, "carpet", 1.0, 0.1, "opaque"),
    ("wall", 0.88, 0.88, 0.86, 1, "none", 1, 0.1, "opaque"),
    ("ext", 0.72, 0.71, 0.69, 1, "concrete", 3, 0.1, "opaque"),
    ("ceiling", 0.93, 0.93, 0.92, 1, "tile", 1.2, 0.0, "opaque"),
    ("frame", 0.24, 0.25, 0.27, 1, "none", 1, 0.55, "opaque"),
    ("glass", 0.78, 0.90, 0.94, 0.14, "none", 1, 0.95, "glass"),
    ("frost", 0.93, 0.95, 0.96, 0.7, "none", 1, 0.5, "glass"),
    ("window", 0.60, 0.74, 0.84, 0.22, "none", 1, 0.95, "glass"),
    ("facade_glass", 0.20, 0.27, 0.33, 1, "none", 1, 0.9, "opaque"),
    ("light", 1.0, 0.97, 0.90, 1, "none", 1, 0.0, "glow"),
    ("desk", 0.86, 0.80, 0.70, 1, "none", 1, 0.35, "opaque"),
    ("metal", 0.32, 0.33, 0.35, 1, "none", 1, 0.6, "opaque"),
    ("divider", 0.38, 0.52, 0.44, 1, "carpet", 0.5, 0.05, "opaque"),
    ("monitor", 0.07, 0.07, 0.08, 1, "none", 1, 0.7, "opaque"),
    ("screen", 0.30, 0.45, 0.70, 1, "none", 1, 0.0, "glow"),
    ("chair", 0.14, 0.15, 0.17, 1, "none", 1, 0.25, "opaque"),
    ("sofa", 0.30, 0.44, 0.40, 1, "carpet", 0.5, 0.05, "opaque"),
    ("sofa_red", 0.62, 0.28, 0.24, 1, "carpet", 0.5, 0.05, "opaque"),
    ("table", 0.93, 0.93, 0.91, 1, "none", 1, 0.4, "opaque"),
    ("counter", 0.96, 0.96, 0.95, 1, "none", 1, 0.5, "opaque"),
    ("door", 0.50, 0.37, 0.26, 1, "wood", 1.5, 0.35, "opaque"),
    ("exit", 0.15, 0.85, 0.35, 1, "none", 1, 0.0, "glow"),
    ("pink", 0.86, 0.66, 0.80, 1, "none", 1, 0.1, "opaque"),
    ("yellow", 0.93, 0.86, 0.45, 1, "none", 1, 0.1, "opaque"),
    ("blue", 0.42, 0.52, 0.80, 1, "none", 1, 0.1, "opaque"),
    ("green", 0.45, 0.85, 0.45, 1, "none", 1, 0.1, "opaque"),
    ("teal", 0.36, 0.66, 0.58, 1, "none", 1, 0.1, "opaque"),
    ("orange", 0.88, 0.58, 0.36, 1, "none", 1, 0.2, "opaque"),
    ("plant", 0.24, 0.50, 0.24, 1, "carpet", 0.4, 0.1, "opaque"),
    ("pot", 0.85, 0.84, 0.80, 1, "none", 1, 0.3, "opaque"),
    ("grass", 0.34, 0.55, 0.26, 1, "grass", 2.5, 0.0, "opaque"),
    ("path", 0.64, 0.62, 0.58, 1, "tile", 1.0, 0.1, "opaque"),
    ("trunk", 0.40, 0.29, 0.20, 1, "wood", 1, 0.1, "opaque"),
    ("leaves", 0.27, 0.50, 0.22, 1, "grass", 1.5, 0.0, "opaque"),
    ("leaves2", 0.38, 0.58, 0.26, 1, "grass", 1.5, 0.0, "opaque"),
    ("ground", 0.40, 0.50, 0.33, 1, "grass", 4, 0.0, "opaque"),
    ("asphalt", 0.30, 0.30, 0.31, 1, "concrete", 4, 0.1, "opaque"),
    ("mirror", 0.70, 0.76, 0.80, 1, "none", 1, 0.98, "opaque"),
    ("printer", 0.82, 0.82, 0.80, 1, "none", 1, 0.3, "opaque"),
]

out = []
panes = []  # (x0, x1, y0, y1, z0, z1, frost y0, frost y1): breakable glass
boxes = []  # (mat, x0, x1, y0, y1, z0, z1, flags), kept for checks and the preview


def wx(px):
    return (px - CX) * S


def wz(py):
    return -(py - CY) * S


def wrect(r):
    """Plan rect -> world (x0, x1, z0, z1)."""
    x1, y1, x2, y2 = r
    return (wx(x1), wx(x2), wz(y2), wz(y1))


def box(mat, x0, x1, y0, y1, z0, z1, flags="-"):
    if x1 < x0:
        x0, x1 = x1, x0
    if y1 < y0:
        y0, y1 = y1, y0
    if z1 < z0:
        z0, z1 = z1, z0
    if x1 - x0 < 1e-3 or y1 - y0 < 1e-3 or z1 - z0 < 1e-3:
        return
    boxes.append((mat, x0, x1, y0, y1, z0, z1, flags))


def cbox(mat, cx, cy, cz, sx, sy, sz, flags="-"):
    box(mat, cx - sx / 2, cx + sx / 2, cy - sy / 2, cy + sy / 2, cz - sz / 2, cz + sz / 2, flags)


def rbox(mat, r, y0, y1, flags="-"):
    x0, x1, z0, z1 = r
    box(mat, x0, x1, y0, y1, z0, z1, flags)


def wbox(mat, axis, line, t0, t1, thick, y0, y1, flags="-"):
    """A box along a wall: 'axis' is the direction the wall runs (x or z)."""
    if axis == "x":
        box(mat, t0, t1, y0, y1, line - thick / 2, line + thick / 2, flags)
    else:
        box(mat, line - thick / 2, line + thick / 2, y0, y1, t0, t1, flags)


labels = []


def label(x, y, z, yaw, height, text, color=(0.12, 0.13, 0.15)):
    """A sign: dark text on a white plate. The text faces away from its yaw direction."""
    labels.append((x, y, z, yaw, height, color, text))
    width = max(0.6, len(text) * height * 0.62 + 0.25)
    tall = height * 1.6
    fx, fz = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))  # into the wall
    px, pz = x + fx * 0.015, z + fz * 0.015
    if abs(fz) > 0.5:
        cbox("counter", px, y, pz, width, tall, 0.02, "ns")
    else:
        cbox("counter", px, y, pz, 0.02, tall, width, "ns")


def wall_point(axis, line, t):
    return (t, line) if axis == "x" else (line, t)


def yaw_facing(axis, outsign):
    """Yaw for a sign readable from the side the outward normal points to."""
    nx, nz = (0, outsign) if axis == "x" else (outsign, 0)
    # A sign's text is read looking along its forward axis, so forward points into the wall
    return math.degrees(math.atan2(-nx, -nz)) % 360


# ---------------------------------------------------------------------------------------------
# Rooms

class Room:
    def __init__(self, kind, label_text, name, rects):
        self.kind = kind
        self.label = label_text
        self.name = name
        self.rects = rects  # plan rects
        self.doors = []     # (axis, line, outsign, t) in world coords


rooms = []
for lab, r in L.OFFICES:
    rooms.append(Room("office", lab, "", [r]))
for lab, name, r in L.MEETING:
    rooms.append(Room("meeting", lab, name, [r]))
for lab, name, r in L.QUIET:
    rooms.append(Room("quiet", lab, name, [r]))
for r in L.PRINT:
    rooms.append(Room("print", "Print", "", [r]))
rooms.append(Room("gym", "Trimrommet", "", list(L.GYM)))

cores = [(kind, sign, r) for kind, sign, r in L.CORES]


def in_rect(px, py, r):
    return r[0] <= px <= r[2] and r[1] <= py <= r[3]


def in_slab(px, py):
    return any(in_rect(px, py, r) for r in L.SLABS) and not (
        L.GARDEN[0] < px < L.GARDEN[2] and L.GARDEN[1] < py < L.GARDEN[3])


def classify(px, py, me):
    """What's on the far side of a wall at this plan point."""
    if not in_slab(px, py):
        return ("ext", None)
    for room in rooms:
        if any(in_rect(px, py, r) for r in room.rects):
            if room is me:
                return ("self", None)
            return ("room", room)
    for core in cores:
        if in_rect(px, py, core[2]):
            return ("core", core)
    return ("hall", None)


PROBE = 2.5  # plan pixels


def sides(r):
    """The four sides of a plan rect: (side, axis, fixed plan coord, a, b, outward plan vector)."""
    x1, y1, x2, y2 = r
    return [
        ("n", "x", y1, x1, x2, (0, -1)),
        ("s", "x", y2, x1, x2, (0, 1)),
        ("w", "z", x1, y1, y2, (-1, 0)),
        ("e", "z", x2, y1, y2, (1, 0)),
    ]


def runs_of(r, me):
    """Splits each side of a rect into runs by what's beyond it."""
    result = []
    for side, axis, fixed, a, b, (ox, oy) in sides(r):
        cur = None
        start = a
        t = a
        while t < b:
            mid = t + 0.5
            if axis == "x":
                c = classify(mid, fixed + oy * PROBE, me)
            else:
                c = classify(fixed + ox * PROBE, mid, me)
            key = (c[0], id(c[1]) if c[1] is not None else None)
            if cur is None:
                cur = (key, c)
            elif key != cur[0]:
                result.append((side, axis, fixed, start, t, (ox, oy), cur[1]))
                cur = (key, c)
                start = t
            t += 1
        result.append((side, axis, fixed, start, b, (ox, oy), cur[1]))
    return result


def world_run(axis, fixed, a, b, outv):
    """Plan run -> world (axis, line, t0, t1, outsign)."""
    if axis == "x":
        line = wz(fixed)
        outsign = -outv[1]  # plan y down = world -z
        t0, t1 = wx(a), wx(b)
    else:
        line = wx(fixed)
        outsign = outv[0]
        t0, t1 = sorted((wz(a), wz(b)))
    return axis, line, t0, t1, outsign


def pane(axis, line, t0, t1, y0, y1, frosted):
    """A breakable glass pane, with a frosted band across it if frosted."""
    if t1 - t0 < 0.05:
        return
    if axis == "x":
        b = (t0, t1, y0, y1, line - 0.015, line + 0.015)
    else:
        b = (line - 0.015, line + 0.015, y0, y1, t0, t1)
    panes.append(b + ((0.95, 1.75) if frosted else (0.0, 0.0)))


def glass_wall(axis, edge, outsign, t0, t1, doors, frosted=True):
    """A glass front wall on the inside of a room edge, with door openings. Each pane between two
    mullions is a separate pane that can be shot to pieces."""
    line = edge - outsign * GLASS_T / 2
    cuts = sorted((d - DOOR_W / 2, d + DOOR_W / 2) for d in doors)

    def frame(a, b, y0, y1, flags="-"):
        # The frame is a hair thinner than the wall line and stops a hair short of the run's ends.
        # Otherwise its faces land exactly on the faces of the partition walls it butts into, and
        # the two flicker through each other (z-fighting) as the camera moves.
        wbox("frame", axis, line, max(a, t0 + FRAME_INSET), min(b, t1 - FRAME_INSET),
             GLASS_T - 2 * FRAME_INSET, y0, y1, flags)

    pieces = []
    cur = t0
    for c0, c1 in cuts:
        pieces.append((cur, c0))
        cur = c1
    pieces.append((cur, t1))
    for p0, p1 in pieces:
        if p1 - p0 < 0.3:
            if p1 - p0 > 0.001:
                frame(p0, p1, 0.0, 3.0)
            continue
        frame(p0, p1, 0.0, 0.08)
        # Mullions, at most 1.5 m apart, and posts at both ends, with a pane between each pair
        n = max(1, int(math.ceil((p1 - p0) / 1.5)))
        posts = []
        for i in range(n + 1):
            t = p0 + (p1 - p0) * i / n
            t = min(max(t, p0 + 0.03), p1 - 0.03)
            posts.append(t)
            frame(t - 0.03, t + 0.03, 0.0, 3.0, "n")
        for a, b in zip(posts, posts[1:]):
            pane(axis, line, a + 0.03, b - 0.03, 0.08, 3.0, frosted)
    # Head rail along the whole run, transoms above the doors
    frame(t0, t1, 3.0, H)
    for c0, c1 in cuts:
        pane(axis, line, c0, c1, DOOR_H, 3.0, False)
        frame(c0, c1, DOOR_H - 0.06, DOOR_H)


def partition(axis, edge, outsign, t0, t1):
    line = edge - outsign * WALL / 2
    wbox("wall", axis, line, t0, t1, WALL, 0, H)


def choose_doors(room, hall_runs):
    """Door positions: on the longest corridor-facing run (two on long ones)."""
    usable = [r for r in hall_runs if r[3] - r[2] >= DOOR_W + 0.15]
    if not usable:
        return []
    axis, line, t0, t1, outsign = max(usable, key=lambda r: (round(r[3] - r[2], 1), r[5]))[:5]
    length = t1 - t0
    if length > 13:
        ts = [t0 + length * 0.25, t0 + length * 0.75]
    else:
        ts = [(t0 + t1) / 2]
    return [(axis, line, outsign, t) for t in ts]


def depth_beyond(axis, fixed, a, b, outv, me):
    """How far (plan px) the corridor reaches beyond the middle of a run, up to 60."""
    mid = (a + b) / 2
    for d in range(3, 60):
        if axis == "x":
            c = classify(mid, fixed + outv[1] * d, me)
        else:
            c = classify(fixed + outv[0] * d, mid, me)
        if c[0] != "hall":
            return d
    return 60


room_signs = []
for room in rooms:
    hall_runs = []
    for r in room.rects:
        for side, axis, fixed, a, b, outv, c in runs_of(r, room):
            kind = c[0]
            waxis, line, t0, t1, outsign = world_run(axis, fixed, a, b, outv)
            if kind == "hall":
                depth = depth_beyond(axis, fixed, a, b, outv, room)
                hall_runs.append((waxis, line, t0, t1, outsign, min(depth, 20)))
            elif kind == "room":
                partition(waxis, line, outsign, t0, t1)
            # ext: the outside wall is there; core: the block is there; self: same room
    room.doors = choose_doors(room, hall_runs)
    for waxis, line, t0, t1, outsign, _ in hall_runs:
        ds = [d[3] for d in room.doors if d[0] == waxis and abs(d[1] - line) < 1e-6 and t0 <= d[3] <= t1]
        glass_wall(waxis, line, outsign, t0, t1, ds, frosted=room.kind != "print")
    if not room.doors:
        print("warning: room %s has no door" % room.label, file=sys.stderr)


# ---------------------------------------------------------------------------------------------
# Floors, ceilings, outside walls, the building below and the garden

for r in L.SLABS:
    rbox("wood", wrect(r), -0.4, 0.0)
    rbox("ceiling", wrect(r), H, H + 0.3, "s")
    # The floors below
    rbox("ext", wrect(r), GROUND_Y, -0.4, "n")

ROOM_FLOOR = {"office": "carpet", "meeting": "carpet_blue", "quiet": "carpet_blue", "print": "vinyl", "gym": "rubber"}
for room in rooms:
    for r in room.rects:
        x0, x1, z0, z1 = wrect(r)
        box(ROOM_FLOOR[room.kind], x0, x1, 0.0, 0.012, z0, z1, "ns")

# Ceiling lights on a grid, not over the blocks
for r in L.SLABS:
    x0, x1, z0, z1 = wrect(r)
    nx = max(1, int((x1 - x0) / 3.6))
    nz = max(1, int((z1 - z0) / 3.6))
    for i in range(nx):
        for j in range(nz):
            x = x0 + (i + 0.5) * (x1 - x0) / nx
            z = z0 + (j + 0.5) * (z1 - z0) / nz
            px, py = x / S + CX, -z / S + CY
            if any(in_rect(px, py, c[2]) for c in cores):
                continue
            cbox("light", x, H - 0.02, z, 1.2, 0.04, 0.6, "ns")


def outline_walls(points, height_top, inner):
    """Outside walls along a closed outline (clockwise in plan coords): a sill, windows and a header."""
    n = len(points)
    # Signed area tells which side is outside
    area = sum(points[i][0] * points[(i + 1) % n][1] - points[(i + 1) % n][0] * points[i][1] for i in range(n))
    cw = area > 0  # with y down, positive area is clockwise on screen
    for i in range(n):
        p0, p1 = points[i], points[(i + 1) % n]
        prev, nxt = points[i - 1], points[(i + 2) % n]
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        length = math.hypot(dx, dy)
        ux, uy = dx / length, dy / length
        # Outward normal (plan coords): for a clockwise outline on screen, left of the direction
        ox, oy = (uy, -ux) if cw else (-uy, ux)
        if inner:
            ox, oy = -ox, -oy

        def convex(a, b, c):
            cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
            return (cross > 0) == cw

        ext0 = EXT_T if convex(prev, p0, p1) != inner else 0
        ext1 = EXT_T if convex(p0, p1, nxt) != inner else 0
        if dy == 0:
            axis = "x"
            line = wz(p0[1]) + (-oy) * EXT_T / 2
            t0, t1 = sorted((wx(p0[0]), wx(p1[0])))
            outsign = -oy
            if ux > 0:
                t0 -= ext0; t1 += ext1
            else:
                t0 -= ext1; t1 += ext0
        else:
            axis = "z"
            line = wx(p0[0]) + ox * EXT_T / 2
            t0, t1 = sorted((wz(p0[1]), wz(p1[1])))
            outsign = ox
            # plan y down = world z down
            if uy > 0:
                t0 -= ext1; t1 += ext0
            else:
                t0 -= ext0; t1 += ext1
        yield axis, line, t0, t1, outsign


for axis, line, t0, t1, outsign in outline_walls(L.OUTLINE, H, False):
    wbox("ext", axis, line, t0, t1, EXT_T, 0.0, 0.75)
    wbox("window", axis, line, t0, t1, 0.04, 0.75, 2.85, "gs")
    wbox("ext", axis, line, t0, t1, EXT_T, 2.85, H + 0.4)
    n = max(1, int(round((t1 - t0) / 1.8)))
    for i in range(1, n):
        t = t0 + (t1 - t0) * i / n
        wbox("frame", axis, line, t - 0.04, t + 0.04, 0.14, 0.75, 2.85, "n")
    # Window bands on the two floors below
    face = line + outsign * (EXT_T / 2 + 0.02)
    for y0, y1 in ((-3.25, -1.0), (-7.0, -4.75)):
        wbox("facade_glass", axis, face, t0, t1, 0.04, y0, y1, "n")
    wbox("ext", axis, line, t0, t1, EXT_T, -0.4, 0.0, "n")

gx1, gy1, gx2, gy2 = L.GARDEN
garden_outline = [(gx1, gy1), (gx2, gy1), (gx2, gy2), (gx1, gy2)]
for axis, line, t0, t1, outsign in outline_walls(garden_outline, H, True):
    wbox("ext", axis, line, t0, t1, EXT_T, 0.0, 0.35)
    wbox("window", axis, line, t0, t1, 0.04, 0.35, 2.95, "gs")
    wbox("ext", axis, line, t0, t1, EXT_T, 2.95, H + 0.4)
    n = max(1, int(round((t1 - t0) / 1.8)))
    for i in range(1, n):
        t = t0 + (t1 - t0) * i / n
        wbox("frame", axis, line, t - 0.04, t + 0.04, 0.14, 0.35, 2.95, "n")
    # The courtyard walls of the floor below
    face = line + outsign * EXT_T / 2
    wbox("ext", axis, line, t0, t1, EXT_T, GARDEN_Y, 0.0, "n")
    wbox("facade_glass", axis, face + outsign * 0.02, t0, t1, 0.04, -3.25, -1.0, "n")

# Garden
g = wrect(L.GARDEN)
rbox("grass", g, GARDEN_Y - 0.3, GARDEN_Y, "n")
for r in L.GARDEN_PATHS:
    rbox("path", wrect(r), GARDEN_Y, GARDEN_Y + 0.03, "n")
for i, (px, py) in enumerate(L.GARDEN_TREES):
    x, z = wx(px), wz(py)
    big = 1.0 + 0.25 * ((i * 7) % 3)
    cbox("trunk", x, GARDEN_Y + 1.6 * big, z, 0.35, 3.2 * big, 0.35, "n")
    leaves = "leaves" if i % 2 == 0 else "leaves2"
    cbox(leaves, x, GARDEN_Y + 3.4 * big, z, 3.0 * big, 1.8 * big, 3.0 * big, "n")
    cbox(leaves, x + 0.2, GARDEN_Y + 4.7 * big, z - 0.2, 2.0 * big, 1.4 * big, 2.0 * big, "n")
for r in L.GARDEN_BENCHES:
    x0, x1, z0, z1 = wrect(r)
    box("trunk", x0, x1, GARDEN_Y + 0.4, GARDEN_Y + 0.48, z0, z1, "n")
    box("metal", x0 + 0.1, x1 - 0.1, GARDEN_Y, GARDEN_Y + 0.4, z0 + 0.1, z1 - 0.1, "n")
# Hedges along the courtyard walls
x0, x1, z0, z1 = g
for hx0, hx1, hz0, hz1 in ((x0, x1, z0, z0 + 0.8), (x0, x1, z1 - 0.8, z1), (x0, x0 + 0.8, z0, z1), (x1 - 0.8, x1, z0, z1)):
    box("leaves", hx0, hx1, GARDEN_Y, GARDEN_Y + 0.9, hz0, hz1, "n")

# The ground around the building
box("ground", -300, 300, GROUND_Y - 0.5, GROUND_Y, -300, 300, "n")
bx0, bx1, bz0, bz1 = wx(96), wx(1347), wz(1090), wz(55)
box("asphalt", bx0 - 12, bx1 + 12, GROUND_Y, GROUND_Y + 0.02, bz0 - 12, bz1 + 12, "n")

# ---------------------------------------------------------------------------------------------
# The closed blocks

CORE_ACCENT = {"stair": "pink", "lift": "pink", "wc": "yellow", "store": "wall", "shaft": "ext"}
for kind, sign, r in cores:
    rbox("wall", wrect(r), 0.0, H)
    if kind == "shaft":
        continue
    # A door and a sign on the longest side facing a corridor
    best = None
    for side, axis, fixed, a, b, outv, c in runs_of(r, None):
        if c[0] != "hall" or b - a < 18:
            continue
        if best is None or b - a > best[4] - best[3]:
            best = (side, axis, fixed, a, b, outv)
    if best is None:
        continue
    side, axis, fixed, a, b, outv = best
    waxis, line, t0, t1, outsign = world_run(axis, fixed, a, b, outv)
    t = (t0 + t1) / 2
    face = line + outsign * 0.03
    wbox("door", waxis, face, t - 0.5, t + 0.5, 0.06, 0.0, 2.2)
    wbox("frame", waxis, face, t - 0.58, t + 0.58, 0.04, 0.0, 2.28, "n")
    wbox(CORE_ACCENT[kind], waxis, line + outsign * 0.01, t - 0.9, t + 0.9, 0.04, 2.35, 3.0, "n")
    if kind == "stair":
        wbox("exit", waxis, line + outsign * 0.08, t - 0.25, t + 0.25, 0.06, 2.42, 2.6, "ns")
    if sign:
        sx, sz = wall_point(waxis, line + outsign * 0.05, t)
        label(sx, 2.75, sz, yaw_facing(waxis, outsign), 0.3, sign)
    if kind == "lift":
        sx, sz = wall_point(waxis, line + outsign * 0.05, t + (1.1 if t1 - t > 1.4 else -1.1))
        label(sx, 1.7, sz, yaw_facing(waxis, outsign), 0.4, "3. ETASJE")

# ---------------------------------------------------------------------------------------------
# Furniture

solid_rects = [wrect(c[2]) for c in cores]


def overlaps(a, b, pad=0.0):
    return a[0] < b[1] + pad and b[0] < a[1] + pad and a[2] < b[3] + pad and b[2] < a[3] + pad


def door_zones(room):
    zones = []
    for axis, line, outsign, t in room.doors:
        if axis == "x":
            z_in = line - outsign * 2.0
            zones.append((t - 1.1, t + 1.1, min(line, z_in), max(line, z_in)))
        else:
            x_in = line - outsign * 2.0
            zones.append((min(line, x_in), max(line, x_in), t - 1.1, t + 1.1))
    return zones


def desk(x, z, facing):
    """One desk with its chair and monitor. facing: +1/-1 along z (sitter looks that way), or ('x', +-1)."""
    axis, sgn = facing
    if axis == "z":
        cbox("desk", x, 0.74, z, 1.4, 0.04, 0.7, "n")
        for dx in (-0.66, 0.66):
            cbox("metal", x + dx, 0.36, z, 0.04, 0.72, 0.64, "n")
        cbox("-", x, 0.38, z, 1.4, 0.76, 0.7, "o")
        mz = z + sgn * 0.15
        cbox("monitor", x, 1.08, mz, 0.6, 0.36, 0.03, "n")
        cbox("screen", x, 1.08, mz - sgn * 0.017, 0.56, 0.32, 0.005, "ns")
        cbox("metal", x, 0.86, mz + sgn * 0.03, 0.06, 0.2, 0.04, "n")
        cz = z - sgn * 0.75
        cbox("chair", x, 0.47, cz, 0.5, 0.08, 0.5, "n")
        cbox("chair", x, 0.78, cz - sgn * 0.23, 0.48, 0.55, 0.06, "n")
        cbox("metal", x, 0.22, cz, 0.06, 0.44, 0.06, "n")
        cbox("metal", x, 0.03, cz, 0.55, 0.05, 0.55, "n")
    else:
        cbox("desk", x, 0.74, z, 0.7, 0.04, 1.4, "n")
        for dz in (-0.66, 0.66):
            cbox("metal", x, 0.36, z + dz, 0.64, 0.72, 0.04, "n")
        cbox("-", x, 0.38, z, 0.7, 0.76, 1.4, "o")
        mx = x + sgn * 0.15
        cbox("monitor", mx, 1.08, z, 0.03, 0.36, 0.6, "n")
        cbox("screen", mx - sgn * 0.017, 1.08, z, 0.005, 0.32, 0.56, "ns")
        cbox("metal", mx + sgn * 0.03, 0.86, z, 0.04, 0.2, 0.06, "n")
        cx = x - sgn * 0.75
        cbox("chair", cx, 0.47, z, 0.5, 0.08, 0.5, "n")
        cbox("chair", cx - sgn * 0.23, 0.78, z, 0.06, 0.55, 0.48, "n")
        cbox("metal", cx, 0.22, z, 0.06, 0.44, 0.06, "n")
        cbox("metal", cx, 0.03, z, 0.55, 0.05, 0.55, "n")


# Desk clusters: (desks across, desks deep). Footprint includes the chairs.
CLUSTERS = [(2, 2), (1, 2), (2, 1), (1, 1)]


def cluster_size(cols, rows, axis):
    w = cols * 1.4
    d = 0.7 * rows + 1.1 * rows  # desks plus chair space
    return (w, d) if axis == "z" else (d, w)


def place_cluster(x, z, cols, rows, axis, flip):
    w, d = cluster_size(cols, rows, "z")
    for c in range(cols):
        off = (c - (cols - 1) / 2) * 1.4
        if rows == 2:
            for sgn in (1, -1):
                pos = sgn * 0.35
                if axis == "z":
                    desk(x + off, z + pos, ("z", -sgn))
                else:
                    desk(x + pos, z + off, ("x", -sgn))
        else:
            sgn = flip
            if axis == "z":
                desk(x + off, z + sgn * 0.4, ("z", sgn))
            else:
                desk(x + sgn * 0.4, z + off, ("x", sgn))
    if rows == 2:
        if axis == "z":
            cbox("divider", x, 0.95, z, cols * 1.4, 0.4, 0.03, "n")
        else:
            cbox("divider", x, 0.95, z, 0.03, 0.4, cols * 1.4, "n")


def furnish_office(room):
    x0, x1, z0, z1 = wrect(room.rects[0])
    x0 += 0.25; x1 -= 0.25; z0 += 0.25; z1 -= 0.25
    zones = door_zones(room)
    aisle = 1.3
    best = None
    for cols, rows in CLUSTERS:
        for axis in ("z", "x"):
            w, d = cluster_size(cols, rows, axis)
            nx = int((x1 - x0 - 0.8 + aisle) // (w + aisle))
            nz = int((z1 - z0 - 0.8 + aisle) // (d + aisle))
            spots = []
            for i in range(nx):
                for j in range(nz):
                    cx = (x0 + x1) / 2 + (i - (nx - 1) / 2) * (w + aisle)
                    cz = (z0 + z1) / 2 + (j - (nz - 1) / 2) * (d + aisle)
                    fp = (cx - w / 2, cx + w / 2, cz - d / 2, cz + d / 2)
                    if any(overlaps(fp, zn) for zn in zones) or any(overlaps(fp, s, 0.3) for s in solid_rects):
                        continue
                    spots.append((cx, cz))
            count = len(spots) * cols * rows
            if best is None or count > best[0]:
                best = (count, cols, rows, axis, spots)
    count, cols, rows, axis, spots = best
    for k, (cx, cz) in enumerate(spots):
        place_cluster(cx, cz, cols, rows, axis, 1 if k % 2 == 0 else -1)
    # A plant in the corner furthest from the doors
    corners = [(x0 + 0.35, z0 + 0.35), (x0 + 0.35, z1 - 0.35), (x1 - 0.35, z0 + 0.35), (x1 - 0.35, z1 - 0.35)]
    dps = [wall_point(a, l, t) for a, l, s, t in room.doors] or [((x0 + x1) / 2, (z0 + z1) / 2)]
    free = [c for c in corners if not any(overlaps((c[0] - 0.3, c[0] + 0.3, c[1] - 0.3, c[1] + 0.3), s) for s in solid_rects)]
    if free:
        px, pz = max(free, key=lambda c: min(math.hypot(c[0] - d[0], c[1] - d[1]) for d in dps))
        plant(px, pz)
    return count


def plant(x, z, base=0.0):
    cbox("pot", x, base + 0.25, z, 0.45, 0.5, 0.45)
    cbox("plant", x, base + 0.85, z, 0.6, 0.7, 0.6, "n")
    cbox("plant", x, base + 1.3, z, 0.4, 0.4, 0.4, "n")


def chair_at(x, z, face_axis, sgn):
    """A chair whose sitter looks along face_axis * sgn."""
    if face_axis == "z":
        cbox("chair", x, 0.47, z, 0.48, 0.08, 0.48, "n")
        cbox("chair", x, 0.75, z - sgn * 0.22, 0.46, 0.5, 0.06, "n")
    else:
        cbox("chair", x, 0.47, z, 0.48, 0.08, 0.48, "n")
        cbox("chair", x - sgn * 0.22, 0.75, z, 0.06, 0.5, 0.46, "n")
    cbox("metal", x, 0.22, z, 0.06, 0.44, 0.06, "n")


def furnish_meeting(room):
    x0, x1, z0, z1 = wrect(room.rects[0])
    x0 += 0.2; x1 -= 0.2; z0 += 0.2; z1 -= 0.2
    w, d = x1 - x0, z1 - z0
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    long_x = w >= d
    long_len, short_len = (w, d) if long_x else (d, w)
    tl = max(1.2, long_len - 2.2)
    tw = min(1.3, max(0.8, short_len - 2.4))
    if long_x:
        rbox("table", (cx - tl / 2, cx + tl / 2, cz - tw / 2, cz + tw / 2), 0.7, 0.75)
        cbox("metal", cx, 0.35, cz, 0.12, 0.7, 0.12, "n")
        cbox("-", cx, 0.375, cz, tl, 0.75, tw, "o")
    else:
        rbox("table", (cx - tw / 2, cx + tw / 2, cz - tl / 2, cz + tl / 2), 0.7, 0.75)
        cbox("metal", cx, 0.35, cz, 0.12, 0.7, 0.12, "n")
        cbox("-", cx, 0.375, cz, tw, 0.75, tl, "o")
    n = max(1, int(tl // 0.8))
    for i in range(n):
        t = (i - (n - 1) / 2) * (tl / n)
        for sgn in (1, -1):
            off = sgn * (tw / 2 + 0.35)
            if long_x:
                chair_at(cx + t, cz + off, "z", -sgn)
            else:
                chair_at(cx + off, cz + t, "x", -sgn)
    # A screen on the short wall furthest from the door
    dps = [wall_point(a, l, t) for a, l, s, t in room.doors] or [(cx, cz)]
    dx, dz = dps[0]
    if long_x:
        sx = x0 + 0.05 if abs(dx - x0) > abs(dx - x1) else x1 - 0.05
        cbox("monitor", sx, 1.4, cz, 0.06, 0.8, 1.4, "n")
        cbox("screen", sx + (0.035 if sx < cx else -0.035), 1.4, cz, 0.01, 0.72, 1.3, "ns")
    else:
        sz = z0 + 0.05 if abs(dz - z0) > abs(dz - z1) else z1 - 0.05
        cbox("monitor", cx, 1.4, sz, 1.4, 0.8, 0.06, "n")
        cbox("screen", cx, 1.4, sz + (0.035 if sz < cz else -0.035), 1.3, 0.72, 0.01, "ns")


def furnish_quiet(room):
    x0, x1, z0, z1 = wrect(room.rects[0])
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    armchair((cx - 0.45, cx + 0.45, cz - 0.45, cz + 0.45), "s", "sofa_red")
    cbox("table", cx + 0.9, 0.5, cz, 0.45, 0.04, 0.45, "n")
    cbox("metal", cx + 0.9, 0.25, cz, 0.05, 0.5, 0.05, "n")


def furnish_print(room):
    x0, x1, z0, z1 = wrect(room.rects[0])
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    w, d = x1 - x0, z1 - z0
    dps = [wall_point(a, l, t) for a, l, s, t in room.doors] or [(cx, cz)]
    dx, dz = dps[0]
    # Printer against the wall opposite the door
    if w >= d:
        px = x0 + 0.6 if abs(dx - x0) > abs(dx - x1) else x1 - 0.6
        cbox("printer", px, 0.5, cz - 0.6, 0.9, 1.0, 0.7)
        cbox("monitor", px, 1.02, cz - 0.6, 0.5, 0.04, 0.3, "n")
        cbox("counter", px, 0.45, cz + 0.7, 0.8, 0.9, 1.0)
    else:
        pz = z0 + 0.6 if abs(dz - z0) > abs(dz - z1) else z1 - 0.6
        cbox("printer", cx - 0.6, 0.5, pz, 0.7, 1.0, 0.9)
        cbox("monitor", cx - 0.6, 1.02, pz, 0.3, 0.04, 0.5, "n")
        cbox("counter", cx + 0.7, 0.45, pz, 1.0, 0.9, 0.8)


def furnish_gym(room):
    x0, x1, z0, z1 = wrect(room.rects[0])
    cx = (x0 + x1) / 2
    # Treadmills along the north wall, a bench and a rack, a mirror on the east wall
    for i in range(3):
        tx = x0 + 1.2 + i * 1.6
        cbox("chair", tx, 0.12, z1 - 1.4, 0.8, 0.24, 1.8)
        cbox("metal", tx, 0.7, z1 - 0.6, 0.8, 1.0, 0.08, "n")
        cbox("monitor", tx, 1.2, z1 - 0.65, 0.6, 0.25, 0.1, "n")
    cbox("chair", cx, 0.25, (z0 + z1) / 2, 0.4, 0.5, 1.3)
    cbox("metal", cx, 1.0, (z0 + z1) / 2 + 0.2, 1.6, 0.04, 0.04, "n")
    for dx in (-0.6, 0.6):
        cbox("metal", cx + dx, 0.6, (z0 + z1) / 2 + 0.2, 0.06, 1.2, 0.06, "n")
    cbox("metal", x1 - 0.5, 0.8, z0 + 2.0, 0.6, 1.6, 2.4)
    cbox("orange", x1 - 0.5, 0.9, z0 + 2.0, 0.65, 0.12, 2.2, "n")
    cbox("mirror", x1 - 0.08, 1.4, (z0 + z1) / 2 + 2.5, 0.03, 2.0, 3.0, "n")
    # Mats
    box("teal", x0 + 0.6, x0 + 2.6, 0.012, 0.03, z0 + 0.8, z0 + 4.0, "n")


def armchair(r, back, mat="sofa"):
    x0, x1, z0, z1 = r
    box(mat, x0, x1, 0.0, 0.42, z0, z1)
    if back == "n":
        box(mat, x0, x1, 0.42, 0.85, z1 - 0.2, z1)
    elif back == "s":
        box(mat, x0, x1, 0.42, 0.85, z0, z0 + 0.2)
    elif back == "w":
        box(mat, x0, x0 + 0.2, 0.42, 0.85, z0, z1)
    else:
        box(mat, x1 - 0.2, x1, 0.42, 0.85, z0, z1)


def hall_item(kind, where, opts):
    if kind == "plant":
        plant(wx(where[0]), wz(where[1]))
        return
    if kind == "roundtable":
        x, z = wx(where[0]), wz(where[1])
        cbox("table", x, 0.74, z, 0.9, 0.04, 0.9)
        cbox("table", x, 0.74, z, 1.1, 0.04, 0.6, "n")
        cbox("table", x, 0.74, z, 0.6, 0.04, 1.1, "n")
        cbox("metal", x, 0.36, z, 0.1, 0.72, 0.1, "n")
        chair_at(x + 0.9, z, "x", -1)
        chair_at(x - 0.9, z, "x", 1)
        chair_at(x, z + 0.9, "z", -1)
        chair_at(x, z - 0.9, "z", 1)
        return
    r = wrect(where)
    x0, x1, z0, z1 = r
    if kind in ("sofa", "armchair"):
        armchair(r, opts.get("back", "n"), "sofa" if kind == "sofa" else "sofa_red")
    elif kind == "table":
        box("table", x0, x1, 0.4, 0.45, z0, z1)
        box("metal", x0 + 0.1, x1 - 0.1, 0.0, 0.4, z0 + 0.1, z1 - 0.1, "n")
    elif kind == "hightable":
        box("table", x0, x1, 1.02, 1.06, z0, z1)
        box("metal", (x0 + x1) / 2 - 0.05, (x0 + x1) / 2 + 0.05, 0.0, 1.02, z0 + 0.2, z1 - 0.2)
    elif kind == "counter":
        box("counter", x0, x1, 0.0, 0.9, z0, z1)
        box("table", x0 - 0.01, x1 + 0.01, 0.9, 0.94, z0 - 0.01, z1 + 0.01, "n")
        if opts.get("coffee"):
            cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
            cbox("monitor", cx, 1.16, cz, 0.38, 0.44, 0.38, "n")
            cbox("orange", cx, 1.25, cz, 0.39, 0.06, 0.39, "ns")
    elif kind == "bench":
        box("metal", x0, x1, 0.0, 0.42, z0, z1)
        n = max(1, int((max(x1 - x0, z1 - z0)) // 0.7))
        for i in range(n):
            if x1 - x0 >= z1 - z0:
                t = x0 + (i + 0.5) * (x1 - x0) / n
                cbox("chair", t, 0.47, (z0 + z1) / 2, 0.55, 0.1, z1 - z0, "n")
            else:
                t = z0 + (i + 0.5) * (z1 - z0) / n
                cbox("chair", (x0 + x1) / 2, 0.47, t, x1 - x0, 0.1, 0.55, "n")
    elif kind == "booth":
        box("orange", x0 + 0.05, x1 - 0.05, 0.0, 2.3, z0 + 0.05, z1 - 0.05)
        box("frame", x0, x1, 2.3, 2.4, z0, z1, "n")


desk_count = 0
for room in rooms:
    if room.kind == "office":
        desk_count += furnish_office(room)
    elif room.kind == "meeting":
        furnish_meeting(room)
    elif room.kind == "quiet":
        furnish_quiet(room)
    elif room.kind == "print":
        furnish_print(room)
    elif room.kind == "gym":
        furnish_gym(room)
for kind, where, opts in L.HALL_FURNITURE:
    hall_item(kind, where, opts)

# ---------------------------------------------------------------------------------------------
# Signs above the doors

for room in rooms:
    if not room.doors:
        continue
    axis, line, outsign, t = room.doors[0]
    sx, sz = wall_point(axis, line + outsign * 0.06, t)
    text = room.label if not room.name else "%s – %s" % (room.label, room.name)
    label(sx, 2.75, sz, yaw_facing(axis, outsign), 0.24 if room.name else 0.32, text)

# ---------------------------------------------------------------------------------------------
# Spawns and the map camera

spawns = []
for px, py, facing in L.SPAWNS:
    spawns.append((wx(px), 1.2, wz(py), facing % 360))

cam = L.MAP_CAMERA
camera = (wx(cam[0]), cam[2], wz(cam[1]), cam[3], cam[4])


def check():
    """Spawns must stand on the floor in open space."""
    ok = True
    for i, (x, y, z, _) in enumerate(spawns):
        px, py = x / S + CX, -z / S + CY
        if not in_slab(px, py):
            print("spawn %d is off the floor" % i, file=sys.stderr); ok = False
        for b in boxes + [("glass",) + p[:6] + ("g",) for p in panes]:
            mat, x0, x1, y0, y1, z0, z1, flags = b
            if "n" in flags and "o" not in flags:
                continue
            if x0 - 0.6 < x < x1 + 0.6 and z0 - 0.6 < z < z1 + 0.6 and y0 < 2.1 and y1 > 0.05:
                print("spawn %d (%.0f, %.0f) is blocked by %s" % (i, px, py, mat), file=sys.stderr); ok = False
                break
    return ok


def fmt(v):
    s = "%.3f" % v
    s = s.rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def write(path):
    lines = ["# Generated by Tools/OfficeMap/generate.py from layout.py. Don't edit by hand."]
    for m in MATERIALS:
        name, r, gc, b, a, tex, tile, smooth, kind = m
        lines.append("m %s %s %s %s %s %s %s %s %s" % (name, fmt(r), fmt(gc), fmt(b), fmt(a), tex, fmt(tile), fmt(smooth), kind))
    for mat, x0, x1, y0, y1, z0, z1, flags in boxes:
        lines.append("b %s %s %s %s %s %s %s %s" % (
            mat, fmt((x0 + x1) / 2), fmt((y0 + y1) / 2), fmt((z0 + z1) / 2), fmt(x1 - x0), fmt(y1 - y0), fmt(z1 - z0), flags))
    for x0, x1, y0, y1, z0, z1, f0, f1 in panes:
        lines.append("p %s %s %s %s %s %s %s %s" % (
            fmt((x0 + x1) / 2), fmt((y0 + y1) / 2), fmt((z0 + z1) / 2), fmt(x1 - x0), fmt(y1 - y0), fmt(z1 - z0), fmt((f0 + f1) / 2), fmt(f1 - f0)))
    for x, y, z, yaw in spawns:
        lines.append("s %s %s %s %s" % (fmt(x), fmt(y), fmt(z), fmt(yaw)))
    for x, y, z, yaw, height, color, text in labels:
        lines.append("l %s %s %s %s %s %s %s %s %s" % (fmt(x), fmt(y), fmt(z), fmt(yaw), fmt(height), fmt(color[0]), fmt(color[1]), fmt(color[2]), text))
    lines.append("c %s %s %s %s %s" % tuple(fmt(v) for v in camera))
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")


def preview(path):
    from PIL import Image, ImageDraw
    colors = {m[0]: tuple(int(c * 255) for c in m[1:4]) for m in MATERIALS}
    k = 10  # pixels per metre
    x0, x1, z0, z1 = wx(96) - 3, wx(1347) + 3, wz(1090) - 3, wz(55) + 3
    img = Image.new("RGB", (int((x1 - x0) * k), int((z1 - z0) * k)), (40, 60, 40))
    d = ImageDraw.Draw(img)
    for b in sorted(boxes + [("glass",) + p[:6] + ("g",) for p in panes], key=lambda b: b[4]):
        mat, bx0, bx1, by0, by1, bz0, bz1, flags = b
        if by0 >= H - 0.1 or mat in ("-", "ground", "asphalt", "light") or by1 < -0.5 and mat not in ("grass", "path", "leaves", "leaves2", "trunk"):
            continue
        c = colors[mat]
        if mat in ("glass", "window"):
            c = (90, 200, 230)
        d.rectangle([(bx0 - x0) * k, (z1 - bz1) * k, (bx1 - x0) * k, (z1 - bz0) * k], fill=c)
    for x, y, z, yaw in spawns:
        px, py = (x - x0) * k, (z1 - z) * k
        d.ellipse([px - 6, py - 6, px + 6, py + 6], fill=(255, 0, 0))
        a = math.radians(yaw)
        d.line([px, py, px + math.sin(a) * 16, py - math.cos(a) * 16], fill=(255, 0, 0), width=3)
    for x, y, z, yaw, height, color, text in labels:
        d.text(((x - x0) * k, (z1 - z) * k), text, fill=(0, 0, 0))
    img.save(path)


if __name__ == "__main__":
    root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    target = os.path.join(root, "Assets", "Resources", "Maps", "Office.txt")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    good = check()
    write(target)
    print("%d glass panes" % len(panes))
    print("%d boxes, %d desks, %d signs, %d spawns -> %s" % (len(boxes), desk_count, len(labels), len(spawns), target))
    if "--preview" in sys.argv:
        i = sys.argv.index("--preview")
        p = sys.argv[i + 1] if i + 1 < len(sys.argv) else os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview.png")
        preview(p)
        print("preview -> " + p)
    sys.exit(0 if good else 1)
