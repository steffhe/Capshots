"""Concept sheet for the modular weapon redesign (Stage 1 proposal, not game data).

Draws side profiles of the four gun forms built from ONE shared set of parts, and the five
stages of a transformation, so the design can be reviewed before any Unity work. Every form has
every part; a part a form doesn't use is collapsed into the receiver (size 0), which is how the
in-game morph will move every part continuously instead of swapping models.

Gun space: z forward (right in the drawings), y up, metres, receiver centred near z = 0.
Run: python3 Tools/WeaponDesign/concept.py  (writes docs/weapon-redesign/*.png)
"""
import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "weapon-redesign")

# Material palette
MAT = {
    "graphite": (38, 40, 45),
    "gunmetal": (70, 75, 84),
    "steel":    (138, 144, 152),
    "rubber":   (22, 22, 24),
    "core":     (70, 232, 255),
    "blue":     (24, 74, 165),
}
OUTLINE = (12, 13, 15)
BG = (226, 229, 233)

# kind: box | chamfer (bevelled box) | cyl (side view: rounded ends) | grip (raked) | seg (segmented cylinder) | vent (box with slots)
SHARED = [
    # name, kind, material, z0, z1, y0, y1
    ("stock", "chamfer", "graphite"),
    ("stock_pad", "box", "rubber"),
    ("rear_housing", "chamfer", "graphite", -0.40, -0.23, 0.00, 0.16),
    ("rear_cap", "box", "gunmetal", -0.43, -0.40, 0.02, 0.14),
    ("grip", "grip", "rubber", -0.19, -0.08, -0.24, -0.03),
    ("trigger_guard", "box", "gunmetal", -0.09, 0.03, -0.13, -0.115),
    ("guard_post", "box", "gunmetal", 0.01, 0.03, -0.13, -0.04),
    ("under", "box", "rubber"),
    ("barrel", "cyl", "steel"),
    ("shroud", "vent", "graphite"),
    ("muzzle", "chamfer", "gunmetal"),
    ("receiver", "chamfer", "graphite", -0.24, 0.31, -0.04, 0.17),
    ("rail_top", "box", "steel"),
    ("core_window", "box", "blue", -0.07, 0.15, 0.025, 0.115),
    ("core", "cyl", "core", -0.04, 0.12, 0.045, 0.095),
    ("clamp_rear", "box", "gunmetal", -0.07, -0.045, 0.02, 0.12),
    ("clamp_front", "box", "gunmetal", 0.125, 0.15, 0.02, 0.12),
    ("side_module", "chamfer", "gunmetal"),
    ("scope", "cyl", "graphite"),
    ("scope_mount", "box", "gunmetal"),
]

COLLAPSED = (0.20, 0.20, 0.06, 0.06)  # tucked into the receiver's front

FORMS = {
    "PISTOL": {
        "note": "Compact L. Slide panels recoil; steady core, quick blip per shot.",
        "rail_top": (-0.18, 0.30, 0.17, 0.188),
        "side_module": (0.16, 0.34, 0.07, 0.15),
        "shroud": (0.30, 0.47, 0.00, 0.16),
        "barrel": (0.40, 0.52, 0.06, 0.10),
        "muzzle": (0.46, 0.50, 0.02, 0.14),
        "stock": (-0.43, -0.43, 0.06, 0.06), "stock_pad": (-0.43, -0.43, 0.06, 0.06),
    },
    "MACHINE GUN": {
        "note": "Long and level. Rotary barrel spins up; vented heat-sink modules; core ramps with sustained fire.",
        "rail_top": (-0.18, 0.60, 0.17, 0.188),
        "side_module": (0.16, 0.46, 0.02, 0.16),
        "shroud": (0.30, 0.64, -0.01, 0.16),
        "barrel": (0.55, 0.93, 0.035, 0.115),
        "muzzle": (0.89, 0.95, 0.01, 0.14),
        "under": (0.38, 0.48, -0.19, -0.01),
        "stock": (-0.62, -0.43, 0.02, 0.12), "stock_pad": (-0.64, -0.62, 0.02, 0.12),
    },
    "SHOTGUN": {
        "note": "Short and tall. Wide claw muzzle blooms open; pump racks; core dims after a blast and recharges.",
        "rail_top": (-0.18, 0.56, 0.17, 0.188),
        "side_module": (0.16, 0.44, 0.10, 0.21),
        "shroud": (0.30, 0.60, -0.05, 0.20),
        "barrel": (0.42, 0.66, 0.02, 0.14),
        "muzzle": (0.60, 0.71, -0.06, 0.22),
        "under": (0.33, 0.56, -0.13, -0.04),
        "stock": (-0.43, -0.43, 0.06, 0.06), "stock_pad": (-0.43, -0.43, 0.06, 0.06),
    },
    "SNIPER RIFLE": {
        "note": "Longest, with scope hump and stock. Modules fold back to a cheek rest; charge runs down the barrel segments.",
        "rail_top": (-0.18, 0.40, 0.17, 0.188),
        "side_module": (-0.34, -0.10, 0.05, 0.15),
        "shroud": (0.30, 0.52, 0.02, 0.15),
        "barrel": (0.46, 1.25, 0.055, 0.105),
        "muzzle": (1.22, 1.33, 0.03, 0.13),
        "under": (0.24, 0.37, -0.10, -0.04),
        "scope": (-0.10, 0.32, 0.215, 0.315),
        "scope_mount": (0.02, 0.18, 0.188, 0.215),
        "stock": (-0.84, -0.43, -0.07, 0.14), "stock_pad": (-0.87, -0.84, -0.07, 0.14),
    },
}
ORDER = ["PISTOL", "MACHINE GUN", "SHOTGUN", "SNIPER RIFLE"]


def pose(form):
    """Every part's (z0, z1, y0, y1) in this form"""
    f = FORMS[form]
    out = {}
    for p in SHARED:
        name = p[0]
        if len(p) > 3:
            out[name] = p[3:7]
        else:
            out[name] = f.get(name, COLLAPSED)
    if form != "SNIPER RIFLE":
        out["barrel_kind"] = "cyl"
    return out


def lerp(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def draw_part(d, kind, mat, rect, scale, origin, glow=1.0, name=""):
    z0, z1, y0, y1 = rect
    if abs(z1 - z0) < 1e-4 or abs(y1 - y0) < 1e-4:
        return
    ox, oy = origin
    x0, x1 = ox + z0 * scale, ox + z1 * scale
    ya, yb = oy - y1 * scale, oy - y0 * scale
    col = MAT[mat]
    if mat in ("core", "blue"):
        col = tuple(int(c * (0.45 + 0.55 * glow)) for c in col)
    if kind == "grip":
        rake = 0.06 * scale
        d.polygon([(x0, ya), (x1, ya), (x1 - rake, yb), (x0 - rake, yb)], fill=col, outline=OUTLINE)
        for i in range(1, 5):  # rubber texture lines
            yy = ya + (yb - ya) * i / 5
            off = rake * i / 5
            d.line([(x0 - off + 4, yy), (x1 - off - 4, yy)], fill=(40, 40, 44), width=1)
        return
    if kind == "chamfer":
        c = min(0.25 * (yb - ya), 0.25 * (x1 - x0), 0.03 * scale)
        d.polygon([(x0 + c, ya), (x1 - c, ya), (x1, ya + c), (x1, yb - c), (x1 - c, yb), (x0 + c, yb), (x0, yb - c), (x0, ya + c)], fill=col, outline=OUTLINE)
        return
    if kind == "cyl":
        r = (yb - ya) / 2
        d.rounded_rectangle([x0, ya, x1, yb], radius=max(1, min(r, (x1 - x0) / 2)), fill=col, outline=OUTLINE)
        if mat == "steel":
            d.line([(x0 + r, ya + (yb - ya) * 0.3), (x1 - r, ya + (yb - ya) * 0.3)], fill=(190, 196, 204), width=2)
        return
    if kind == "seg":
        n = 7
        gap = 0.012 * scale
        w = ((x1 - x0) - gap * (n - 1)) / n
        for i in range(n):
            a = x0 + i * (w + gap)
            d.rounded_rectangle([a, ya, a + w, yb], radius=3, fill=col, outline=OUTLINE)
            d.line([(a + 3, (ya + yb) / 2), (a + w - 3, (ya + yb) / 2)], fill=tuple(int(c * glow) for c in MAT["core"]), width=2)
        return
    d.rectangle([x0, ya, x1, yb], fill=col, outline=OUTLINE)
    if kind == "vent" and (x1 - x0) > 40:
        n = int((x1 - x0) / 26)
        for i in range(1, n):
            xx = x0 + (x1 - x0) * i / n
            d.line([(xx, ya + (yb - ya) * 0.25), (xx, yb - (yb - ya) * 0.25)], fill=(20, 21, 24), width=3)


def draw_gun(d, poses, scale, origin, glow=1.0, sniper=False, channel=True):
    for p in SHARED:
        name, kind, mat = p[0], p[1], p[2]
        if name == "barrel" and sniper:
            kind = "seg"
        draw_part(d, kind, mat, poses[name], scale, origin, glow, name)
    # The one energy channel: a thin line from the core towards the front assembly
    if channel:
        ox, oy = origin
        z1 = poses["shroud"][1] - 0.02
        y = oy - 0.07 * scale
        d.line([(ox + 0.15 * scale, y), (ox + max(0.16, z1) * scale, y)], fill=tuple(int(c * (0.4 + 0.6 * glow)) for c in MAT["blue"]), width=3)


def font(size):
    for path in ["/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"]:
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def forms_sheet():
    scale = 520
    w, row = 1400, 380
    img = Image.new("RGB", (w, row * 4 + 170), BG)
    d = ImageDraw.Draw(img)
    d.text((40, 30), "CAPSHOTS ENERGY GUN - ONE MACHINE, FOUR FORMS (side profiles, concept)", fill=(20, 20, 24), font=font(26))
    d.text((40, 68), "Receiver, grip, rear housing and cyan core are identical in every form. Only the front assembly, rails, side modules, under-module, scope and stock move.", fill=(60, 60, 66), font=font(15))
    for i, f in enumerate(ORDER):
        top = 120 + i * row
        origin = (560, top + 250)
        d.text((40, top + 10), f, fill=(20, 20, 24), font=font(22))
        d.text((40, top + 40), FORMS[f]["note"], fill=(70, 70, 78), font=font(15))
        # Faint outline of the shared core so it reads across rows
        d.line([(origin[0] - 0.43 * scale, top + 370), (origin[0] + 0.31 * scale, top + 370)], fill=(150, 200, 220), width=3)
        d.text((origin[0] + 0.33 * scale, top + 362), "shared receiver / grip / core", fill=(90, 140, 160), font=font(12))
        draw_gun(d, pose(f), scale, origin, 1.0, sniper=(f == "SNIPER RIFLE"))
    # Legend
    x = 40
    y = 120 + row * 4 + 20
    for name, col in [("Matte graphite", MAT["graphite"]), ("Gunmetal", MAT["gunmetal"]), ("Brushed steel", MAT["steel"]), ("Dark rubber", MAT["rubber"]), ("Emissive cyan core", MAT["core"]), ("Deep blue energy", MAT["blue"])]:
        d.rectangle([x, y, x + 22, y + 22], fill=col, outline=OUTLINE)
        d.text((x + 30, y + 2), name, fill=(40, 40, 46), font=font(14))
        x += 210
    img.save(os.path.join(OUT, "forms.png"))


def stages_sheet():
    # Machine gun -> sniper rifle over the five stages; t is the share of the 0.5 s morph
    stages = [
        ("1 ENERGY ACTIVATION", 0.00, 0.08, "Core flares, channel lights"),
        ("2 MECHANICAL RELEASE", 0.08, 0.16, "Clamps unlock, modules pop 4 mm"),
        ("3 RECONFIGURATION", 0.16, 0.38, "Front assembly slides on rails"),
        ("4 MECHANICAL LOCK", 0.38, 0.44, "Parts seat with a small overshoot"),
        ("5 ENERGY STABILISATION", 0.44, 0.50, "Core settles to the new form"),
    ]
    a, b = pose("MACHINE GUN"), pose("SNIPER RIFLE")
    scale = 230
    cw, h = 520, 330
    img = Image.new("RGB", (cw * 5 + 40, h + 110), BG)
    d = ImageDraw.Draw(img)
    d.text((30, 20), "TRANSFORMATION: MACHINE GUN -> SNIPER RIFLE, same 0.5 s as now (stages overlap slightly in game)", fill=(20, 20, 24), font=font(22))
    for i, (title, t0, t1, note) in enumerate(stages):
        tm = (t0 + t1) / 2 / 0.5
        move = min(1.0, max(0.0, (tm * 0.5 - 0.16) / 0.22))
        move = move * move * (3 - 2 * move)
        poses = {k: lerp(a[k], b[k], move) for k in a if isinstance(a[k], tuple)}
        # Release: side modules and clamps lift a touch
        lift = 0.012 if 0.08 <= tm * 0.5 < 0.40 else 0.0
        z0, z1, y0, y1 = poses["side_module"]
        poses["side_module"] = (z0, z1, y0 + lift, y1 + lift)
        for c in ("clamp_rear", "clamp_front"):
            z0, z1, y0, y1 = poses[c]
            poses[c] = (z0, z1, y0 - lift, y1 + lift)
        glow = {0: 1.6, 1: 0.9, 2: 0.55, 3: 0.75, 4: 1.0}[i]
        ox = 30 + i * cw
        d.text((ox, 70), title, fill=(20, 20, 24), font=font(16))
        d.text((ox, 94), note, fill=(70, 70, 78), font=font(13))
        draw_gun(d, poses, scale, (ox + 0.9 * scale, 300), min(glow, 1.0), sniper=move > 0.5)
        if glow > 1.0:  # activation flare
            cx, cy = ox + 0.9 * scale + 0.04 * scale, 300 - 0.07 * scale
            d.ellipse([cx - 26, cy - 26, cx + 26, cy + 26], outline=MAT["core"], width=3)
    img.save(os.path.join(OUT, "transformation.png"))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    forms_sheet()
    stages_sheet()
    print("wrote", os.path.abspath(OUT))
