#!/usr/bin/env python3
"""Builds the Office map from layout.py and writes it as a list of boxes for OfficeMap.cs.

    python3 Tools/OfficeMap/generate.py            # writes Assets/Resources/Maps/Office.txt
    python3 Tools/OfficeMap/generate.py --preview  # also draws a top-down preview PNG

Only the Python standard library is needed (the preview also needs Pillow).

Output format, one item per line (world units are metres, y up, the office floor at y = 0):
    m <name> <r> <g> <b> <a> <texture> <tile> <smoothness> <kind>   a material
    b <material> <cx> <cy> <cz> <sx> <sy> <sz> <flags>              a box
    s <x> <y> <z> <yaw> <team>                                      a spawn point (team -1: any)
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
GARDEN_Y = 0.0     # the courtyard garden is on the same level as the floor
FLOOR_H = 3.75     # storey height of the floors above, round the courtyard
FLOORS_ABOVE = 3
GROUND_Y = -7.5    # street level, two floors down

MATERIALS = [
    # name, r, g, b, a, texture, tile (m), smoothness, kind
    ("wood", 0.80, 0.62, 0.43, 1, "wood", 2.4, 0.5, "opaque"),          # oak floor
    ("carpet", 0.58, 0.59, 0.60, 1, "checker", 1.0, 0.05, "opaque"),    # grey squares
    ("carpet_blue", 0.30, 0.31, 0.34, 1, "carpet", 1.2, 0.05, "opaque"),  # meeting rooms: dark grey
    ("vinyl", 0.78, 0.78, 0.76, 1, "tile", 0.6, 0.3, "opaque"),
    ("rubber", 0.20, 0.21, 0.23, 1, "carpet", 1.0, 0.1, "opaque"),
    ("wall", 0.90, 0.90, 0.89, 1, "none", 1, 0.1, "opaque"),
    ("grey_wall", 0.42, 0.44, 0.47, 1, "none", 1, 0.1, "opaque"),
    ("ext", 0.72, 0.71, 0.69, 1, "concrete", 3, 0.1, "opaque"),
    ("ceiling", 0.93, 0.93, 0.92, 1, "tile", 0.6, 0.0, "opaque"),
    ("frame", 0.10, 0.10, 0.11, 1, "none", 1, 0.55, "opaque"),
    ("glass", 0.80, 0.90, 0.94, 0.12, "none", 1, 0.95, "glass"),
    ("frost", 0.94, 0.96, 0.97, 0.72, "mountains", 6, 0.5, "glass"),
    ("window", 0.60, 0.74, 0.84, 0.2, "none", 1, 0.95, "glass"),
    ("dark_glass", 0.16, 0.20, 0.24, 1, "none", 1, 0.92, "opaque"),
    ("terracotta", 0.80, 0.50, 0.33, 1, "panels", 2.4, 0.25, "opaque"),
    ("facade_dark", 0.17, 0.18, 0.19, 1, "none", 1, 0.3, "opaque"),
    ("light", 1.0, 0.97, 0.90, 1, "none", 1, 0.0, "glow"),
    ("desk", 0.93, 0.93, 0.91, 1, "none", 1, 0.35, "opaque"),
    ("metal", 0.10, 0.10, 0.11, 1, "none", 1, 0.5, "opaque"),
    ("chrome", 0.55, 0.56, 0.58, 1, "none", 1, 0.9, "opaque"),
    ("divider", 0.60, 0.62, 0.58, 1, "carpet", 0.5, 0.05, "opaque"),
    ("monitor", 0.05, 0.05, 0.06, 1, "none", 1, 0.7, "opaque"),
    ("screen", 0.30, 0.45, 0.70, 1, "none", 1, 0.0, "glow"),
    ("chair", 0.10, 0.10, 0.11, 1, "none", 1, 0.25, "opaque"),
    ("sofa", 0.33, 0.34, 0.37, 1, "carpet", 0.5, 0.05, "opaque"),
    ("sofa_red", 0.10, 0.10, 0.11, 1, "carpet", 0.5, 0.05, "opaque"),  # black lounge chairs
    ("walnut", 0.36, 0.24, 0.16, 1, "wood", 1.2, 0.4, "opaque"),
    ("copper", 0.72, 0.42, 0.22, 1, "none", 1, 0.7, "opaque"),
    ("marble", 0.93, 0.93, 0.92, 1, "concrete", 1.0, 0.8, "opaque"),
    ("table", 0.12, 0.12, 0.13, 1, "none", 1, 0.45, "opaque"),
    ("counter", 0.13, 0.13, 0.14, 1, "none", 1, 0.5, "opaque"),
    ("tile_black", 0.10, 0.10, 0.11, 1, "tile", 0.3, 0.6, "opaque"),
    ("rug", 0.52, 0.45, 0.35, 1, "carpet", 0.5, 0.0, "opaque"),
    ("rug_black", 0.09, 0.09, 0.10, 1, "carpet", 0.5, 0.0, "opaque"),
    ("door", 0.50, 0.37, 0.26, 1, "wood", 1.5, 0.35, "opaque"),
    ("exit", 0.15, 0.85, 0.35, 1, "none", 1, 0.0, "glow"),
    ("green_light", 0.25, 0.95, 0.55, 1, "none", 1, 0.0, "glow"),
    ("red", 0.78, 0.10, 0.10, 1, "none", 1, 0.4, "opaque"),
    ("teal", 0.36, 0.66, 0.58, 1, "none", 1, 0.1, "opaque"),
    ("orange", 0.92, 0.55, 0.20, 1, "none", 1, 0.2, "opaque"),
    ("plant", 0.20, 0.42, 0.20, 1, "grass", 0.6, 0.1, "opaque"),
    ("pot", 0.42, 0.33, 0.22, 1, "none", 1, 0.5, "opaque"),
    ("gravel", 0.66, 0.60, 0.52, 1, "gravel", 1.2, 0.05, "opaque"),
    ("paver", 0.36, 0.37, 0.38, 1, "boards", 1.2, 0.15, "opaque"),
    ("soil", 0.33, 0.22, 0.15, 1, "gravel", 1.0, 0.0, "opaque"),
    ("corten", 0.52, 0.26, 0.12, 1, "concrete", 1.0, 0.2, "opaque"),
    ("shrub", 0.19, 0.31, 0.17, 1, "grass", 1.0, 0.0, "opaque"),
    ("shrub2", 0.26, 0.37, 0.19, 1, "grass", 1.0, 0.0, "opaque"),
    ("conifer", 0.13, 0.25, 0.15, 1, "grass", 1.0, 0.0, "opaque"),
    ("twig", 0.30, 0.24, 0.20, 1, "none", 1, 0.1, "opaque"),
    ("weathered", 0.58, 0.58, 0.55, 1, "boards", 1.8, 0.1, "opaque"),
    ("ground", 0.30, 0.38, 0.24, 1, "grass", 4, 0.0, "opaque"),
    ("asphalt", 0.27, 0.27, 0.28, 1, "concrete", 4, 0.1, "opaque"),
    ("pavement", 0.62, 0.62, 0.60, 1, "tile", 1.2, 0.1, "opaque"),
    ("mirror", 0.70, 0.76, 0.80, 1, "none", 1, 0.98, "opaque"),
    ("printer", 0.84, 0.84, 0.82, 1, "none", 1, 0.3, "opaque"),
    ("whiteboard", 0.97, 0.97, 0.97, 1, "none", 1, 0.8, "opaque"),
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


def label(x, y, z, yaw, height, text, color=(0.12, 0.13, 0.15), plate=True):
    """A sign: dark text on a white plate (or just the letters, plate=False). The text faces away
    from its yaw direction."""
    labels.append((x, y, z, yaw, height, color, text))
    if not plate:
        return
    width = max(0.6, len(text) * height * 0.62 + 0.25)
    tall = height * 1.6
    fx, fz = math.sin(math.radians(yaw)), math.cos(math.radians(yaw))  # into the wall
    px, pz = x + fx * 0.015, z + fz * 0.015
    if abs(fz) > 0.5:
        cbox("desk", px, y, pz, width, tall, 0.02, "ns")
    else:
        cbox("desk", px, y, pz, 0.02, tall, width, "ns")


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
    # The film: a mountain range with a row of orange rings below it (drawn by its texture), from
    # 0.6 to 2.8 m. Below and above it the glass is clear, so you see feet and heads.
    panes.append(b + ((0.6, 2.8) if frosted else (0.0, 0.0)))


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
    # Not onto a sliver of corridor too narrow to walk along (at least 1 m deep)
    deep = [r for r in usable if r[5] >= 13]
    usable = deep or usable
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
        # Offices, quiet rooms and the gym have the frosted film; meeting rooms and print rooms are
        # clear, so you can see (and shoot) right through them
        glass_wall(waxis, line, outsign, t0, t1, ds, frosted=room.kind in ("office", "quiet", "gym"))
    if not room.doors:
        print("warning: room %s has no door" % room.label, file=sys.stderr)


# ---------------------------------------------------------------------------------------------
# Floors, ceilings, outside walls, the building below and the garden

for r in L.SLABS:
    rbox("wood", wrect(r), -0.4, 0.0)
    # It casts shadows: it is the roof, and without it the sun and sky light every room in the lightmaps
    rbox("ceiling", wrect(r), H, H + 0.3)
    # The floors below
    rbox("ext", wrect(r), GROUND_Y, -0.4, "n")
rbox("ext", wrect(L.GARDEN), GROUND_Y, -0.4, "n")

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
    """Outside walls along a closed outline (clockwise in plan coords): yields each straight run."""
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


def facade(axis, face, outsign, t0, t1, y_base, floors, seed, lit=True):
    """Terracotta panels with dark window frames standing proud of them, a dark band at each floor
    and vertical louvres on some windows, as on the real building. face: the wall's outer face."""
    top = y_base + floors * FLOOR_H
    wbox("terracotta", axis, face - outsign * 0.15, t0, t1, 0.3, y_base, top, "n")
    for f in range(floors):
        y0 = y_base + f * FLOOR_H
        wbox("facade_dark", axis, face + outsign * 0.02, t0, t1, 0.06, y0, y0 + 0.25, "ns")
        pitch = 3.0
        t = t0 + 1.2 + (pitch / 2) * ((f + seed) % 2)
        k = 0
        while t + 1.4 < t1 - 0.6:
            w = 1.3 if (k + f + seed) % 3 else 2.2
            fc = face + outsign * 0.05
            wbox("facade_dark", axis, fc, t, t + w, 0.1, y0 + 0.7, y0 + 3.3, "ns")
            wbox("dark_glass", axis, fc + outsign * 0.04, t + 0.08, t + w - 0.08, 0.04, y0 + 0.78, y0 + 3.22, "ns")
            if lit and (k * 7 + f * 3 + seed) % 5 == 0:
                # A lit room behind
                wbox("light", axis, fc + outsign * 0.065, t + 0.2, t + w - 0.2, 0.01, y0 + 2.9, y0 + 3.08, "ns")
            if (k + 2 * f + seed) % 4 == 1:
                for i in range(5):
                    ft = t + 0.1 + i * (w - 0.2) / 4
                    wbox("facade_dark", axis, fc + outsign * 0.16, ft - 0.03, ft + 0.03, 0.22, y0 + 0.75, y0 + 3.25, "ns")
            t += w + pitch - 1.3
            k += 1
    wbox("facade_dark", axis, face, t0, t1, 0.36, top, top + 0.3, "ns")


for k, (axis, line, t0, t1, outsign) in enumerate(outline_walls(L.OUTLINE, H, False)):
    # Floor-to-ceiling windows with black frames
    wbox("ext", axis, line, t0, t1, EXT_T, 0.0, 0.25)
    wbox("window", axis, line, t0, t1, 0.04, 0.25, 2.95, "gs")
    wbox("ext", axis, line, t0, t1, EXT_T, 2.95, H + 0.4)
    n = max(1, int(round((t1 - t0) / 1.8)))
    for i in range(1, n):
        t = t0 + (t1 - t0) * i / n
        wbox("frame", axis, line, t - 0.03, t + 0.03, 0.14, 0.25, 2.95, "n")
    wbox("frame", axis, line, t0, t1, 0.1, 2.9, 2.98, "n")
    # Outside: the terracotta facade, on the floors below and the one above
    face = line + outsign * EXT_T / 2
    facade(axis, face + outsign * 0.15, outsign, t0, t1, GROUND_Y, 2, k, lit=False)
    wbox("terracotta", axis, face + outsign * 0.07, t0, t1, 0.14, 2.95, H + 0.4, "n")
    facade(axis, face + outsign * 0.15, outsign, t0, t1, H + 0.4, 1, k + 1, lit=False)
    wbox("ext", axis, line, t0, t1, EXT_T, -0.4, 0.0, "n")


# ---------------------------------------------------------------------------------------------
# The courtyard garden: on the floor's level, walled in glass that breaks (with doors), the
# terracotta facade of the floors above round it, and a gravel garden like the real one

gx0, gx1, gz0, gz1 = wrect(L.GARDEN)
CDOOR_W, CDOOR_H, CGH = 2.0, 2.6, 3.0


def court_wall(axis, line, t0, t1, doors):
    """Tall black-framed panes, each breakable, with doors and a dark band over them"""
    cuts = sorted((d - CDOOR_W / 2, d + CDOOR_W / 2) for d in doors)
    pieces, cur = [], t0
    for c0, c1 in cuts:
        pieces.append((cur, c0))
        cur = c1
    pieces.append((cur, t1))
    for p0, p1 in pieces:
        if p1 - p0 < 0.3:
            continue
        wbox("frame", axis, line, p0, p1, 0.14, 0.0, 0.1)
        n = max(1, int(math.ceil((p1 - p0) / 1.6)))
        posts = []
        for i in range(n + 1):
            t = p0 + (p1 - p0) * i / n
            t = min(max(t, p0 + 0.04), p1 - 0.04)
            posts.append(t)
            wbox("frame", axis, line, t - 0.04, t + 0.04, 0.16, 0.0, CGH)
        for a, b in zip(posts, posts[1:]):
            pane(axis, line, a + 0.04, b - 0.04, 0.1, CGH, False)
    for c0, c1 in cuts:
        pane(axis, line, c0, c1, CDOOR_H, CGH, False)
        wbox("frame", axis, line, c0, c1, 0.16, CDOOR_H - 0.08, CDOOR_H)
        if axis == "x":
            box("rug_black", c0, c1, 0.012, 0.022, line - 1.0, line + 1.0, "ns")
        else:
            box("rug_black", line - 1.0, line + 1.0, 0.012, 0.022, c0, c1, "ns")
    wbox("facade_dark", axis, line, t0, t1, 0.3, CGH, H + 0.4)


def door_t(side, p):
    return wx(p) if side in ("n", "s") else wz(p)


ci = 0.12  # the glass stands this far into the courtyard
for side, axis, line, t0, t1, outsign, seed in (
        ("n", "x", gz1 - ci, gx0, gx1, -1, 0), ("s", "x", gz0 + ci, gx0, gx1, 1, 1),
        ("w", "z", gx0 + ci, gz0, gz1, 1, 2), ("e", "z", gx1 - ci, gz0, gz1, -1, 3)):
    court_wall(axis, line, t0, t1, [door_t(s, p) for s, p in L.GARDEN_DOORS if s == side])
    # The floors above
    edge = line - outsign * ci
    lo, hi = (t0 - 0.3, t1 + 0.3) if axis == "x" else (t0, t1)
    facade(axis, edge + outsign * 0.3, outsign, lo, hi, H + 0.4, FLOORS_ABOVE, seed)
    wbox("terracotta", axis, edge + outsign * 0.15, lo, hi, 0.3, H, H + 0.4, "n")

# Ground: gravel, with dark paving paths
box("gravel", gx0, gx1, -0.02, 0.01, gz0, gz1, "s")
for r in L.GARDEN_PATHS:
    x0, x1, z0, z1 = wrect(r)
    box("paver", max(x0, gx0 + 0.2), min(x1, gx1 - 0.2), 0.01, 0.03, max(z0, gz0 + 0.2), min(z1, gz1 - 0.2), "ns")


def shrub(x, z, size, height, mat):
    """A rounded evergreen bush: a solid core (cover) with softer lumps round it"""
    cbox(mat, x, 0.12 + height * 0.42, z, size * 0.8, height * 0.84, size * 0.7)
    cbox(mat, x, 0.12 + height * 0.35, z, size, height * 0.55, size * 0.5, "n")
    cbox(mat, x, 0.12 + height * 0.35, z, size * 0.55, height * 0.55, size * 0.9, "n")
    cbox(mat, x + size * 0.1, 0.12 + height * 0.82, z - size * 0.08, size * 0.5, height * 0.36, size * 0.45, "n")


def conifer(x, z, height):
    """A clipped yew: stacked layers narrowing to the top"""
    cbox("twig", x, 0.3, z, 0.18, 0.6, 0.18, "n")
    layers = 6
    for i in range(layers):
        w = 1.4 * (1 - i / (layers + 0.5)) + 0.25
        y0 = 0.35 + i * (height - 0.35) / layers
        cbox("conifer", x, y0 + (height - 0.35) / layers / 2 + 0.05, z, w, (height - 0.35) / layers + 0.1, w,
             "-" if i < 3 else "n")


def bare_tree(x, z, height):
    """A young tree in winter: a thin trunk and bare branches (no collider above the trunk)"""
    cbox("twig", x, height * 0.35, z, 0.14, height * 0.7, 0.14)
    for dx, dz, h, l in ((0.5, 0.0, 0.55, 0.3), (-0.45, 0.2, 0.6, 0.35), (0.1, -0.5, 0.65, 0.3),
                         (-0.2, 0.45, 0.75, 0.25), (0.6, 0.5, 0.8, 0.2), (-0.6, -0.4, 0.8, 0.25)):
        y = height * h
        if abs(dx) > abs(dz):
            cbox("twig", x + dx / 2, y, z + dz * 0.2, abs(dx), 0.05, 0.05, "ns")
        else:
            cbox("twig", x + dx * 0.2, y, z + dz / 2, 0.05, 0.05, abs(dz), "ns")
        cbox("twig", x + dx, y + height * l / 2, z + dz, 0.05, height * l, 0.05, "ns")
        cbox("twig", x + dx * 1.3, y + height * l * 0.8, z + dz * 1.3, 0.035, height * l * 0.5, 0.035, "ns")


def bed(r, planted):
    x0, x1, z0, z1 = wrect(r)
    e = 0.06
    box("soil", x0, x1, 0.0, 0.12, z0, z1, "n")
    for a0, a1, b0, b1 in ((x0, x1, z0, z0 + e), (x0, x1, z1 - e, z1), (x0, x0 + e, z0, z1), (x1 - e, x1, z0, z1)):
        box("corten", a0, a1, 0.0, 0.25, b0, b1, "n")
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    w, d = x1 - x0, z1 - z0
    if "conifer" in planted:
        conifer(x0 + 1.2, z1 - 1.2, 3.0)
    if "tree" in planted:
        bare_tree(cx + 0.4, cz - 0.3, 4.8)
    if "shrubs" in planted:
        shrub(x1 - 1.0, z0 + 1.0, 1.6, 1.2, "shrub")
        if w > 5.5 or d > 5.5:
            shrub(x0 + 1.1, z0 + 1.1, 1.5, 1.0, "shrub2")
        if "conifer" not in planted:
            shrub(x0 + 1.2, z1 - 1.2, 1.8, 1.3, "shrub2")
    if "grass" in planted:
        for dx, dz in ((0, 0), (0.35, 0.2), (-0.3, 0.25), (0.1, -0.35)):
            cbox("rug", cx + dx, 0.32, cz + dz, 0.3, 0.45, 0.3, "n")


for r, planted in L.GARDEN_BEDS:
    bed(r, planted)


def garden_house(r):
    """The wooden garden house with a glass roof. Its long sides run north-south and its ends are
    open, so it's a way through the middle of the garden. The west side is half glass (breakable),
    the east side boards; the roof is glass in strips stepping up to the ridge."""
    x0, x1, z0, z1 = wrect(r)
    eave, ridge, t = 2.9, 4.3, 0.14
    zm = (z0 + z1) / 2
    # West side: boards on the north half, glass panes on the south half
    box("weathered", x0, x0 + t, 0.0, eave, zm, z1)
    span = zm - z0
    n = 3
    for i in range(n):
        a, b = z0 + span * i / n, z0 + span * (i + 1) / n
        wbox("frame", "z", x0 + t / 2, a, a + 0.08, t, 0.0, eave)
        pane("z", x0 + t / 2, a + 0.08, b, 0.1, eave - 0.1, False)
    wbox("frame", "z", x0 + t / 2, z0, zm, t, 0.0, 0.1)
    wbox("frame", "z", x0 + t / 2, z0, zm, t, eave - 0.1, eave)
    # East side: boards
    box("weathered", x1 - t, x1, 0.0, eave, z0, z1)
    # Ends: corner posts and a gable of boards over the opening
    for ez in (z0, z1 - t):
        for ex in (x0, x1 - 0.5):
            box("weathered", ex, ex + 0.5, 0.0, eave, ez, ez + t)
        box("weathered", x0, x1, 2.5, eave, ez, ez + t)
        steps = 10
        for i in range(steps):
            y = eave + (ridge - eave) * i / steps
            inset = (x1 - x0) / 2 * i / steps
            box("weathered", x0 + inset, x1 - inset, y, y + (ridge - eave) / steps + 0.002, ez, ez + t, "n")
    # The glass roof in strips stepping up to the ridge from both eaves, on rafters
    steps = 10
    half = (x1 - x0) / 2 + 0.15
    xm = (x0 + x1) / 2
    for i in range(steps):
        y = eave + (ridge - eave) * i / steps
        for sgn in (-1, 1):
            a = xm + sgn * half * (1 - i / steps)
            b = xm + sgn * half * (1 - (i + 1) / steps)
            box("window", a, b + sgn * 0.01, y, y + 0.03, z0 - 0.1, z1 + 0.1, "s")
            box("-", a, b, y, y + (ridge - eave) / steps, z0 - 0.1, z1 + 0.1, "o")
    # Tie beams across, under the glass
    for rz in frange(z0 + 1.0, z1 - 0.5, 2.0):
        box("weathered", x0 + 0.1, x1 - 0.1, eave - 0.12, eave, rz - 0.05, rz + 0.05, "ns")
    box("weathered", xm - 0.07, xm + 0.07, ridge - 0.05, ridge + 0.08, z0 - 0.1, z1 + 0.1, "n")
    # Inside: a long table and a bench (low cover)
    box("walnut", xm - 0.2, xm + 0.85, 0.72, 0.78, z0 + 1.4, z1 - 1.4)
    box("metal", xm + 0.3, xm + 0.36, 0.0, 0.72, z0 + 1.6, z1 - 1.6, "n")
    box("walnut", xm - 0.9, xm - 0.55, 0.42, 0.47, z0 + 1.6, z1 - 1.6, "n")
    box("-", xm - 0.9, xm - 0.55, 0.0, 0.47, z0 + 1.6, z1 - 1.6, "o")


def frange(a, b, step):
    v = a
    while v < b - 1e-9:
        yield v
        v += step


garden_house(L.GARDEN_HOUSE)
x0, x1, z0, z1 = wrect(L.GARDEN_VENT)
for i in range(7):
    inset = 0.03 * (i % 2)
    box("ext", x0 + inset, x1 - inset, i * 0.13, i * 0.13 + 0.1, z0 + inset, z1 - inset, "-" if i == 0 else "n")
box("-", x0, x1, 0.0, 0.94, z0, z1, "o")
box("ext", x0 - 0.02, x1 + 0.02, 0.92, 0.96, z0 - 0.02, z1 + 0.02, "n")
for r in L.GARDEN_BENCHES:
    x0, x1, z0, z1 = wrect(r)
    box("walnut", x0, x1, 0.4, 0.46, z0, z1)
    box("corten", x0 + 0.2, x0 + 0.3, 0.0, 0.4, z0 + 0.03, z1 - 0.03, "n")
    box("corten", x1 - 0.3, x1 - 0.2, 0.0, 0.4, z0 + 0.03, z1 - 0.03, "n")
    box("-", x0, x1, 0.0, 0.4, z0, z1, "o")
for px, py in L.GARDEN_LIGHTS:
    x, z = wx(px), wz(py)
    cbox("chrome", x, 0.45, z, 0.14, 0.9, 0.14, "n")
    cbox("light", x, 0.84, z, 0.15, 0.08, 0.15, "ns")

# The street two floors down, with hedges and bare trees
box("ground", -300, 300, GROUND_Y - 0.5, GROUND_Y, -300, 300, "n")
bx0, bx1, bz0, bz1 = wx(96), wx(1347), wz(1090), wz(55)
box("asphalt", bx0 - 14, bx1 + 14, GROUND_Y, GROUND_Y + 0.02, bz0 - 14, bz1 + 14, "n")
box("pavement", bx0 - 4, bx1 + 4, GROUND_Y + 0.02, GROUND_Y + 0.12, bz0 - 4, bz1 + 4, "n")


def street_tree(x, z, height):
    cbox("twig", x, GROUND_Y + height * 0.3, z, 0.25, height * 0.6, 0.25, "n")
    for dx, dz, h in ((1.1, 0.2, 0.55), (-1.0, 0.5, 0.6), (0.3, -1.1, 0.65), (-0.4, 1.0, 0.75), (0.8, 0.9, 0.8), (-0.9, -0.8, 0.7)):
        y = GROUND_Y + height * h
        cbox("twig", x + dx / 2, y, z + dz / 2, abs(dx) + 0.06, 0.07, abs(dz) + 0.06, "ns")
        cbox("twig", x + dx, y + height * 0.18, z + dz, 0.07, height * 0.36, 0.07, "ns")


for axis, line, t0, t1, outsign in outline_walls(L.OUTLINE, H, False):
    face = line + outsign * 2.6
    for t in frange(t0 + 2.0, t1 - 2.0, 7.0):
        if axis == "x":
            box("shrub", t, t + 4.4, GROUND_Y, GROUND_Y + 1.1, face - 0.6, face + 0.6, "n")
            street_tree(t + 5.6, face + outsign * 1.2, 8.0)
        else:
            box("shrub", face - 0.6, face + 0.6, GROUND_Y, GROUND_Y + 1.1, t, t + 4.4, "n")
            street_tree(face + outsign * 1.2, t + 5.6, 8.0)

# ---------------------------------------------------------------------------------------------
# The closed blocks

CORE_ACCENT = {"stair": "copper", "lift": "copper", "wc": "grey_wall", "store": "grey_wall", "shaft": "ext"}
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
    wbox("grey_wall" if kind != "lift" else "chrome", waxis, face, t - 0.5, t + 0.5, 0.06, 0.0, 2.2)
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
            # Grey felt screens between the desks: cover up to 1.35 m
            cbox("divider", x, 1.05, z, cols * 1.4, 0.6, 0.04)
        else:
            cbox("divider", x, 1.05, z, 0.04, 0.6, cols * 1.4)


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


def ring_light(x, z, y, r):
    """A black ring lamp hanging from the ceiling, glowing underneath"""
    n = 40
    for i in range(n):
        a = 2 * math.pi * i / n
        px, pz = x + r * math.cos(a), z + r * math.sin(a)
        cbox("metal", px, y + 0.05, pz, 0.15, 0.1, 0.15, "ns")
        cbox("light", px, y - 0.005, pz, 0.11, 0.012, 0.11, "ns")
    for a in (0.6, 2.7, 4.8):
        cbox("chrome", x + r * math.cos(a), (y + H) / 2, z + r * math.sin(a), 0.01, H - y, 0.01, "ns")


def wingback(x, z, back):
    """A black wingback chair on copper legs, its back towards 'back' (world n, s, e or w)"""
    bx, bz = {"e": (1, 0), "w": (-1, 0), "n": (0, 1), "s": (0, -1)}[back]
    cbox("sofa_red", x, 0.42, z, 0.75, 0.14, 0.75, "n")
    cbox("sofa_red", x + bx * 0.32, 0.85, z + bz * 0.32, 0.14 if bx else 0.8, 0.95, 0.8 if bx else 0.14, "n")
    for sgn in (-1, 1):
        if bx:
            cbox("sofa_red", x + bx * 0.18, 0.95, z + sgn * 0.36, 0.3, 0.45, 0.1, "n")
        else:
            cbox("sofa_red", x + sgn * 0.36, 0.95, z + bz * 0.18, 0.1, 0.45, 0.3, "n")
    for lx, lz in ((-0.3, -0.3), (0.3, -0.3), (-0.3, 0.3), (0.3, 0.3)):
        cbox("copper", x + lx, 0.17, z + lz, 0.03, 0.34, 0.03, "n")
    cbox("-", x, 0.65, z, 0.8, 1.3, 0.8, "o")


def round_rug(x, z, r, mat):
    for i, (w, d) in enumerate(((2 * r, 1.1 * r), (1.1 * r, 2 * r), (1.75 * r, 1.45 * r), (1.45 * r, 1.75 * r))):
        cbox(mat, x, 0.014 + i * 0.002, z, w, 0.004, d, "ns")


def hall_item(kind, where, opts):
    if kind == "plant":
        plant(wx(where[0]), wz(where[1]))
        return
    if kind == "rings":
        x, z = wx(where[0]), wz(where[1])
        ring_light(x, z, 2.75, 1.1)
        ring_light(x + 0.25, z - 0.2, 2.5, 0.7)
        return
    if kind == "wingbacks":
        # Three black wingback chairs round a small table on a round black rug, under pendants
        x, z = wx(where[0]), wz(where[1])
        round_rug(x, z, 1.4, "rug_black")
        for w, d in ((0.8, 0.5), (0.5, 0.8), (0.7, 0.7)):
            cbox("table", x, 0.48, z, w, 0.03, d, "n")
        cbox("metal", x, 0.24, z, 0.05, 0.48, 0.05, "n")
        cbox("-", x, 0.25, z, 0.7, 0.5, 0.7, "o")
        cbox("orange", x, 0.53, z, 0.22, 0.07, 0.22, "n")
        for dx, dz, back in ((-1.0, 0.0, "w"), (1.0, 0.0, "e"), (0.0, 1.0, "n")):
            wingback(x + dx, z + dz, back)
        for dx, dz in ((0.3, 0.2), (-0.3, -0.2)):
            cbox("chrome", x + dx, (2.4 + H) / 2, z + dz, 0.01, H - 2.4, 0.01, "ns")
            cbox("chrome", x + dx, 2.4, z + dz, 0.3, 0.3, 0.3, "ns")
            cbox("light", x + dx, 2.24, z + dz, 0.18, 0.02, 0.18, "ns")
        return
    if kind == "plinth":
        x, z = wx(where[0]), wz(where[1])
        cbox("marble", x, 0.4, z, 0.6, 0.8, 0.6)
        cbox("dark_glass", x, 1.0, z, 0.25, 0.4, 0.25, "n")
        cbox("red", x, 1.45, z, 0.35, 0.3, 0.2, "n")
        return
    if kind == "slats":
        # Black floor-to-ceiling slats with gaps: you can see and shoot through, not walk through
        x0, x1, z0, z1 = wrect(where)
        along_x = x1 - x0 > z1 - z0
        a, b = (x0, x1) if along_x else (z0, z1)
        t = a + 0.06
        while t < b - 0.02:
            if along_x:
                box("metal", t - 0.025, t + 0.025, 0.0, H, (z0 + z1) / 2 - 0.04, (z0 + z1) / 2 + 0.04)
            else:
                box("metal", (x0 + x1) / 2 - 0.04, (x0 + x1) / 2 + 0.04, 0.0, H, t - 0.025, t + 0.025)
            t += 0.14
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
        box("walnut", x0 - 0.01, x1 + 0.01, 0.9, 0.94, z0 - 0.01, z1 + 0.01, "n")
        if opts.get("coffee"):
            cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
            cbox("chrome", cx, 1.16, cz, 0.38, 0.44, 0.38, "n")
            cbox("monitor", cx, 1.25, cz, 0.39, 0.06, 0.39, "ns")
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
    # The room's number or letter in copper over the door, its name on a small plate beside it
    sx, sz = wall_point(axis, line + outsign * 0.06, t)
    yaw = yaw_facing(axis, outsign)
    label(sx, 2.76, sz, yaw, 0.36, room.label, (0.72, 0.42, 0.22), plate=False)
    if room.name:
        # Beside the door, on the glass at eye height
        right = -1 if yaw in (0.0, 270.0) else 1
        nx, nz = wall_point(axis, line + outsign * 0.06, t + right * (DOOR_W / 2 + 0.2 + 0.045 * len(room.name)))
        label(nx, 1.6, nz, yaw, 0.13, room.name)

# ---------------------------------------------------------------------------------------------
# Spawns and the map camera

SPAWN_LIFT = 1.2
spawns = []
for px, py, facing, team in L.SPAWNS:
    spawns.append((wx(px), SPAWN_LIFT, wz(py), facing % 360, team))

cam = L.MAP_CAMERA
camera = (wx(cam[0]), cam[2], wz(cam[1]), cam[3], cam[4])


def solids(glass_blocks=True):
    out = []
    for mat, x0, x1, y0, y1, z0, z1, flags in boxes:
        if "n" in flags and "o" not in flags:
            continue
        out.append((x0, x1, y0, y1, z0, z1, "g" in flags))
    if glass_blocks:
        out += [p[:6] + (True,) for p in panes]
    return out


CELL = 0.25
GX0, GZ0 = wx(96) - 1, wz(1090) - 1
NX, NZ = int((wx(1347) + 1 - GX0) / CELL), int((wz(55) + 1 - GZ0) / CELL)


def walk_grid():
    """Where a player can stand: each cell's floor height, and whether the player's body (0.45 m
    round, 2 m tall, stepping up 0.3 m) fits there"""
    solid = solids()
    surf = [[-9.0] * NZ for _ in range(NX)]
    for x0, x1, y0, y1, z0, z1, g in solid:
        if y1 > 2.0 or y1 < -0.05 or g:
            continue
        for i in range(max(0, int((x0 - GX0) / CELL)), min(NX, int(math.ceil((x1 - GX0) / CELL)))):
            cx = GX0 + (i + 0.5) * CELL
            if x0 <= cx <= x1:
                for j in range(max(0, int((z0 - GZ0) / CELL)), min(NZ, int(math.ceil((z1 - GZ0) / CELL)))):
                    cz = GZ0 + (j + 0.5) * CELL
                    if z0 <= cz <= z1 and y1 > surf[i][j]:
                        surf[i][j] = y1
    clear = [[surf[i][j] > -1 for j in range(NZ)] for i in range(NX)]
    r = 0.45
    for x0, x1, y0, y1, z0, z1, g in solid:
        for i in range(max(0, int((x0 - r - GX0) / CELL)), min(NX, int(math.ceil((x1 + r - GX0) / CELL)))):
            cx = GX0 + (i + 0.5) * CELL
            for j in range(max(0, int((z0 - r - GZ0) / CELL)), min(NZ, int(math.ceil((z1 + r - GZ0) / CELL)))):
                if not clear[i][j]:
                    continue
                cz = GZ0 + (j + 0.5) * CELL
                dx, dz = max(x0 - cx, 0, cx - x1), max(z0 - cz, 0, cz - z1)
                if dx * dx + dz * dz <= r * r and y0 < surf[i][j] + 1.95 and y1 > surf[i][j] + 0.31:
                    clear[i][j] = False
    return surf, clear


def cell_of(x, z):
    return int((x - GX0) / CELL), int((z - GZ0) / CELL)


def reach(surf, clear, starts):
    seen = [[False] * NZ for _ in range(NX)]
    dist = {}
    queue = []
    for x, z in starts:
        i, j = cell_of(x, z)
        if clear[i][j]:
            seen[i][j] = True
            dist[(i, j)] = 0
            queue.append((i, j))
    k = 0
    while k < len(queue):
        i, j = queue[k]
        k += 1
        for a, b in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if 0 <= a < NX and 0 <= b < NZ and not seen[a][b] and clear[a][b] and surf[a][b] - surf[i][j] <= 0.3:
                seen[a][b] = True
                dist[(a, b)] = dist[(i, j)] + 1
                queue.append((a, b))
    return seen, dist


def ray_blocked(p, q, solid):
    for x0, x1, y0, y1, z0, z1, g in solid:
        t0, t1 = 0.0, 1.0
        for a, b, lo, hi in ((p[0], q[0], x0, x1), (p[1], q[1], y0, y1), (p[2], q[2], z0, z1)):
            d = b - a
            if abs(d) < 1e-9:
                if a < lo or a > hi:
                    t0 = 2.0
                    break
                continue
            u0, u1 = sorted(((lo - a) / d, (hi - a) / d))
            t0, t1 = max(t0, u0), min(t1, u1)
            if t0 > t1:
                break
        if t0 <= t1:
            return True
    return False


def check():
    """Spawn points stand on the floor in open space; every room, the courtyard and the garden
    house can be walked into; both teams' spawns are joined; and no spawn point can see one of the
    other team's (glass doesn't count: people see through it)."""
    problems = []
    for x, y, z, _, team in spawns:
        px, py = x / S + CX, -z / S + CY
        if not in_slab(px, py):
            problems.append("spawn (%.0f, %.0f) is off the floor" % (px, py))
        for b in boxes + [("glass",) + p[:6] + ("g",) for p in panes]:
            mat, x0, x1, y0, y1, z0, z1, flags = b
            if "n" in flags and "o" not in flags:
                continue
            if x0 - 0.6 < x < x1 + 0.6 and z0 - 0.6 < z < z1 + 0.6 and y0 < 2.1 and y1 > 0.05:
                problems.append("spawn (%.0f, %.0f) is blocked by %s" % (px, py, mat))
                break
    surf, clear = walk_grid()
    seen, dist = reach(surf, clear, [(x, z) for x, y, z, _, t in spawns if t == 0])
    for x, y, z, _, team in spawns:
        i, j = cell_of(x, z)
        if not seen[i][j]:
            problems.append("spawn (%.0f, %.0f) can't be walked to from team 0's" % (x / S + CX, -z / S + CY))
    places = [(room.label, r) for room in rooms for r in room.rects] + [("the courtyard", L.GARDEN), ("the garden house", L.GARDEN_HOUSE)]
    for name, r in places:
        x0, x1, z0, z1 = wrect(r)
        i0, j0 = cell_of(x0 + 0.2, z0 + 0.2)
        i1, j1 = cell_of(x1 - 0.2, z1 - 0.2)
        if not any(seen[i][j] for i in range(i0, i1 + 1) for j in range(j0, j1 + 1)):
            problems.append("nobody can walk into " + name)
    opaque = [b for b in solids(False) if not b[6]]
    for x, y, z, _, t in spawns:
        for x2, y2, z2, _, t2 in spawns:
            if t != 0 or t2 != 1:
                continue
            for h1 in (0.6, 1.7):
                for h2 in (0.6, 1.7):
                    if not ray_blocked((x, h1, z), (x2, h2, z2), opaque):
                        problems.append("spawns (%.0f, %.0f) and (%.0f, %.0f) can see each other" % (
                            x / S + CX, -z / S + CY, x2 / S + CX, -z2 / S + CY))
                        break
                else:
                    continue
                break
    i, j = cell_of(spawns[-1][0], spawns[-1][2])
    a = [dist.get(cell_of(x, z), 0) * CELL for x, y, z, _, t in spawns if t == 1]
    print("walkable floor: %.0f m2; on foot from team 0's spawns to team 1's: %.0f-%.0f m" % (
        sum(sum(c) for c in seen) * CELL * CELL, min(a), max(a)))
    return problems


def fmt(v):
    s = "%.3f" % v
    s = s.rstrip("0").rstrip(".")
    return "0" if s in ("-0", "") else s


def write(path):
    lines = ["# Generated by Tools/OfficeMap/generate.py from layout.py. Don't edit by hand."]
    used = {b[0] for b in boxes} | {"glass", "frost"}
    for m in MATERIALS:
        name, r, gc, b, a, tex, tile, smooth, kind = m
        if name not in used:
            continue
        lines.append("m %s %s %s %s %s %s %s %s %s" % (name, fmt(r), fmt(gc), fmt(b), fmt(a), tex, fmt(tile), fmt(smooth), kind))
    for mat, x0, x1, y0, y1, z0, z1, flags in boxes:
        lines.append("b %s %s %s %s %s %s %s %s" % (
            mat, fmt((x0 + x1) / 2), fmt((y0 + y1) / 2), fmt((z0 + z1) / 2), fmt(x1 - x0), fmt(y1 - y0), fmt(z1 - z0), flags))
    for x0, x1, y0, y1, z0, z1, f0, f1 in panes:
        lines.append("p %s %s %s %s %s %s %s %s" % (
            fmt((x0 + x1) / 2), fmt((y0 + y1) / 2), fmt((z0 + z1) / 2), fmt(x1 - x0), fmt(y1 - y0), fmt(z1 - z0), fmt((f0 + f1) / 2), fmt(f1 - f0)))
    for x, y, z, yaw, team in spawns:
        lines.append("s %s %s %s %s %d" % (fmt(x), fmt(y), fmt(z), fmt(yaw), team))
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
        if by0 >= H - 0.1 or mat in ("-", "ground", "asphalt", "pavement", "light") or by1 < -0.5:
            continue
        c = colors[mat]
        if mat in ("glass", "window"):
            c = (90, 200, 230)
        d.rectangle([(bx0 - x0) * k, (z1 - bz1) * k, (bx1 - x0) * k, (z1 - bz0) * k], fill=c)
    for x, y, z, yaw, team in spawns:
        px, py = (x - x0) * k, (z1 - z) * k
        col = (60, 140, 255) if team == 0 else (235, 70, 60)  # blue and red, as in the game
        d.ellipse([px - 7, py - 7, px + 7, py + 7], fill=col, outline=(255, 255, 255), width=2)
        a = math.radians(yaw)
        d.line([px, py, px + math.sin(a) * 18, py - math.cos(a) * 18], fill=col, width=3)
    for x, y, z, yaw, height, color, text in labels:
        d.text(((x - x0) * k, (z1 - z) * k), text, fill=(0, 0, 0))
    img.save(path)


if __name__ == "__main__":
    root = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    target = os.path.join(root, "Assets", "Resources", "Maps", "Office.txt")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    problems = check()
    write(target)
    print("%d glass panes" % len(panes))
    print("%d boxes, %d desks, %d signs, %d spawns -> %s" % (len(boxes), desk_count, len(labels), len(spawns), target))
    if "--preview" in sys.argv:
        i = sys.argv.index("--preview")
        p = sys.argv[i + 1] if i + 1 < len(sys.argv) else os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview.png")
        preview(p)
        print("preview -> " + p)
    for problem in problems:
        print("PROBLEM: " + problem, file=sys.stderr)
    sys.exit(1 if problems else 0)
