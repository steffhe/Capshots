"""Side and top views of the gun as GunDesign (Assets/Scripts/Core/GunDesign.cs) lays it out.

A Python mirror of GunDesign's numbers, so the layout can be checked without Unity: the machined
parts in their finishes, the glowing parts in a sample player colour, the sights from the prefabs,
and (dashed) the old Body block the floating pieces and hand-orbs are fitted round. Keep it in step
with GunDesign.cs when the layout changes.
Run: python3 Tools/WeaponDesign/layout_preview.py  (writes docs/weapon-redesign/layout.png)
"""
import math, os
from PIL import Image, ImageDraw, ImageFont

X, CY, TOP = 0.444, 0.288, 0.397
CORE = (X, 0.275, 1.25)
TUCK = (X, 0.29, 1.40)
COL = {"graphite": (38, 40, 45), "gunmetal": (70, 75, 86), "steel": (140, 146, 154), "rubber": (20, 20, 22), "glow": (255, 150, 40)}

def P(name, shape, fin, pos, size, rot=(0, 0, 0), float_=False):
    return dict(name=name, shape=shape, fin=fin, pos=pos, size=size, rot=rot, float=float_)

def hidden(name, shape, fin):
    return P(name, shape, fin, TUCK, (0, 0, 0))

def rotx(v, deg):
    a = math.radians(deg); x, y, z = v
    return (x, y * math.cos(a) - z * math.sin(a), y * math.sin(a) + z * math.cos(a))

def shared():
    g = []
    g.append(P("Receiver Rear", "chamfer", "graphite", (X, CY, 1.065), (0.14, 0.218, 0.21)))
    g.append(P("Receiver Front", "chamfer", "graphite", (X, CY, 1.395), (0.14, 0.218, 0.13)))
    g.append(P("Receiver Spine", "box", "graphite", (X, 0.373, 1.25), (0.12, 0.048, 0.16)))
    g.append(P("Receiver Keel", "box", "graphite", (X, 0.19, 1.25), (0.12, 0.022, 0.16)))
    g.append(P("Spine Plate", "box", "steel", (X, 0.394, 1.065), (0.06, 0.008, 0.17)))
    g.append(P("Core", "cyl", "glow", CORE, (0.085, 0.085, 0.15)))
    g.append(P("Core Clamp Rear", "ring", "gunmetal", (X, 0.275, 1.178), (0.118, 0.118, 0.03)))
    g.append(P("Core Clamp Front", "ring", "gunmetal", (X, 0.275, 1.322), (0.118, 0.118, 0.03)))
    g.append(P("Core Ring A", "ring", "glow", (X, 0.275, 1.218), (0.13, 0.13, 0.014), float_=True))
    g.append(P("Core Ring B", "ring", "glow", (X, 0.275, 1.282), (0.13, 0.13, 0.014), float_=True))
    g.append(P("Rear Housing", "chamfer", "graphite", (X, 0.30, 0.88), (0.12, 0.19, 0.16)))
    g.append(P("Rear Cap", "chamfer", "gunmetal", (X, 0.30, 0.785), (0.13, 0.15, 0.03)))
    g.append(P("Rear Glow", "cyl", "glow", (X, 0.30, 0.768), (0.05, 0.05, 0.006)))
    grip = (X, 0.05, 1.085)
    g.append(P("Grip", "chamfer", "rubber", grip, (0.105, 0.27, 0.115), rot=(14, 0, 0)))
    off = rotx((0, -0.01, -0.0585), 14)
    g.append(P("Grip Glow", "box", "glow", (X, grip[1] + off[1], grip[2] + off[2]), (0.02, 0.16, 0.006), rot=(14, 0, 0)))
    g.append(P("Trigger Guard", "box", "gunmetal", (X, 0.135, 1.215), (0.03, 0.012, 0.14)))
    g.append(P("Guard Post", "box", "gunmetal", (X, 0.16, 1.28), (0.03, 0.04, 0.012)))
    g.append(P("Trigger", "box", "steel", (X, 0.158, 1.21), (0.014, 0.04, 0.012), rot=(10, 0, 0)))
    return g

def front(form):
    g = []
    sf, sw, sb, st = {"tight": (1.643, 0.13, 0.193, TOP), "orbit": (2.376, 0.13, 0.203, TOP),
                      "fan": (2.025, 0.20, 0.158, 0.418), "rail": (2.1265, 0.12, 0.213, TOP)}[form]
    g.append(P("Shroud", "chamfer", "graphite", (X, (st + sb) / 2, (1.46 + sf) / 2), (sw, st - sb, sf - 1.46)))
    vents, vh = {"orbit": ([1.56, 1.68, 1.80, 1.92], 0.10), "fan": ([1.60, 1.70, 1.80, 1.90], 0.14),
                 "rail": ([1.60, 1.72, 1.84, 1.96], 0.07)}.get(form, (None, 0))
    for i in range(4):
        if vents: g.append(P(f"Vent {i+1}", "box", "glow", (X, (st + sb) / 2, vents[i]), (sw + 0.006, vh, 0.014)))
    ly = 0.235 if form == "rail" else 0.215
    lx = max(0.07, sw / 2) + 0.0035
    g.append(P("Energy Line", "box", "glow", (X + lx, ly, (1.34 + sf - 0.03) / 2), (0.006, 0.012, sf - 0.03 - 1.34)))
    rx = max(0.07, sw / 2) + 0.0055
    g.append(P("Rail", "box", "steel", (X + rx, 0.365, (0.97 + sf - 0.04) / 2), (0.011, 0.014, sf - 0.04 - 0.97)))
    m, ms, mx = {"orbit": ((0.29, 2.155), (0.016, 0.13, 0.35), 0.081), "fan": ((0.36, 1.675), (0.016, 0.08, 0.35), 0.114),
                 "rail": ((0.31, 0.91), (0.014, 0.11, 0.18), 0.078), "tight": ((0.30, 0.98), (0.014, 0.12, 0.15), 0.078)}[form]
    g.append(P("Side Module", "chamfer", "gunmetal", (X + mx, m[0], m[1]), ms, float_=True))
    g.append(P("Side Glow", "box", "glow", (X + mx + ms[0] / 2 + 0.0015, m[0], m[1]), (0.003, 0.02, ms[2] * 0.8), float_=True))
    if form == "orbit":
        g += [P("Spindle", "cyl", "steel", (X, CY, 2.69), (0.035, 0.035, 0.62)), P("Bore Glow", "cyl", "glow", (X, CY, 3.0), (0.03, 0.03, 0.01)),
              P("Muzzle", "ring", "gunmetal", (X, CY, 3.03), (0.17, 0.17, 0.025), float_=True), P("Muzzle Glow", "ring", "glow", (X, CY, 3.03), (0.15, 0.15, 0.03), float_=True)]
    elif form == "fan":
        g += [P("Spindle", "cyl", "steel", (X, CY, 2.045), (0.12, 0.12, 0.04)), P("Bore Glow", "cyl", "glow", (X, CY, 2.067), (0.09, 0.09, 0.006)),
              P("Muzzle", "ring", "gunmetal", (X, CY, 2.02), (0.17, 0.17, 0.02), float_=True), P("Muzzle Glow", "ring", "glow", (X, CY, 2.02), (0.15, 0.15, 0.024), float_=True)]
    elif form == "rail":
        g += [P("Spindle", "cyl", "steel", (X, 0.34, 2.66), (0.03, 0.03, 1.50)), P("Bore Glow", "cyl", "glow", (X, 0.34, 3.42), (0.03, 0.03, 0.01)),
              P("Muzzle", "ring", "gunmetal", (X, 0.34, 3.45), (0.10, 0.085, 0.03), float_=True), P("Muzzle Glow", "ring", "glow", (X, 0.34, 3.45), (0.088, 0.074, 0.034), float_=True)]
    else:
        g += [P("Spindle", "cyl", "steel", (X, 0.31, 1.66), (0.04, 0.04, 0.05)), P("Bore Glow", "cyl", "glow", (X, 0.31, 1.686), (0.026, 0.026, 0.006)),
              P("Muzzle", "ring", "gunmetal", (X, 0.31, 1.652), (0.06, 0.06, 0.012), float_=True), P("Muzzle Glow", "ring", "glow", (X, 0.31, 1.652), (0.05, 0.05, 0.016), float_=True)]
    if form == "orbit": g.append(P("Foregrip", "chamfer", "rubber", (X, 0.10, 1.857), (0.085, 0.20, 0.09), rot=(8, 0, 0)))
    if form == "fan": g.append(P("Foregrip", "chamfer", "rubber", (X, 0.135, 1.68), (0.15, 0.06, 0.38)))
    if form == "rail":
        g.append(P("Foregrip", "chamfer", "rubber", (X, 0.12, 1.60), (0.08, 0.17, 0.09), rot=(10, 0, 0)))
        g.append(P("Scope Mount", "box", "gunmetal", (X, 0.4095, 1.05), (0.05, 0.025, 0.04)))
        g.append(P("Scope Mount", "box", "gunmetal", (X, 0.4095, 1.52), (0.05, 0.025, 0.04)))
    if form in ("rail", "orbit"):
        full = form == "rail"; y = 0.27 if full else 0.29; back = 0.47 if full else 0.635; fr = 0.745
        size = (0.10, 0.20 if full else 0.12, fr - back)
        g += [P("Stock", "chamfer", "graphite", (X, y, (fr + back) / 2), size, float_=True),
              P("Stock Pad", "chamfer", "rubber", (X, y, back - 0.015), (0.11, size[1] + 0.01, 0.03), float_=True),
              P("Stock Glow", "box", "glow", (X, y, (fr + back) / 2), (0.104, 0.012, size[2] * 0.75), float_=True),
              P("Stock Rod", "cyl", "steel", (X, 0.30, 0.7575), (0.022, 0.022, 0.035))]
    return g

# The prefabs' sights (Anchor/Design/Sights, and the sniper's scope), side view: (z, y, length, height)
SIGHTS = {
    "tight": [(1.065, 0.407, 0.04, 0.021), (1.065, 0.435, 0.04, 0.034), (1.605, 0.424, 0.03, 0.055)],
    "orbit": [(0.945, 0.403, 0.04, 0.012), (0.945, 0.445, 0.03, 0.074), (2.315, 0.427, 0.03, 0.06), (2.315, 0.432, 0.04, 0.071)],
    "fan": [(1.438, 0.421, 1.075, 0.006), (1.988, 0.437, 0.026, 0.026)],
    "rail": [(1.29, 0.503, 0.618, 0.01), (1.29, 0.422, 0.618, 0.01), (1.29, 0.463, 0.618, 0.08), (1.593, 0.463, 0.01, 0.105), (0.984, 0.463, 0.01, 0.105)],
}
BODY = {"tight": (1.333, 0.62), "orbit": (1.628, 1.496), "fan": (1.45, 1.15), "rail": (1.564, 1.125)}
BODYH = {"tight": 0.218, "orbit": 0.218, "fan": 0.26, "rail": 0.218}
# The floating pieces (GunSatellites) at rest, roughly, side view
def satellites(form):
    out = []
    if form == "orbit":
        L = 1.496 * 0.42; z0 = 2.376 + 0.14 * 0.07
        out.append((z0 + L / 2, CY, L, 0.14 * 0.48 * 2 + 0.02))
    elif form == "rail":
        seg = 1.496 / 8
        for i in range(8): out.append((1.912 + seg * (i + 0.5), 0.34, seg * 0.76, 0.075 * 1.08))
    elif form == "fan":
        out.append((2.025 + 0.06, CY, 0.25, 0.30))
        out.append((1.68, 0.12, 0.40, 0.09))
    else:
        out.append((1.333 + 0.031, 0.31, 0.434, 0.12))
        out.append((1.643 + 0.031, 0.31, 0.0224, 0.109))
    return out

def corners(p):
    sx, sy, sz = [s / 2 for s in p["size"]]
    pts = []
    for dy in (-sy, sy):
        for dz in (-sz, sz):
            v = rotx((0, dy, dz), p["rot"][0])
            pts.append((p["pos"][2] + v[2], p["pos"][1] + v[1]))
    return pts

def font(n):
    path = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    return ImageFont.truetype(path, n) if os.path.exists(path) else ImageFont.load_default()

def main():
    scale = 360; w = 1400; row = 270
    forms = [("PISTOL", "tight"), ("MACHINE GUN", "orbit"), ("SHOTGUN", "fan"), ("SNIPER RIFLE", "rail")]
    img = Image.new("RGB", (w, 110 + row * 4), (222, 225, 230))
    d = ImageDraw.Draw(img, "RGBA")
    d.text((30, 20), "GUN DESIGN AS BUILT (side view, from GunDesign.cs numbers; glow in a sample player colour)", fill=(20, 20, 24), font=font(20))
    d.text((30, 52), "Solid: machined parts. Outlined orange: floating parts. Dashed: old Body block (fitting). Blue: GunSatellites' floating pieces (approx.). Grey: prefab sights.", fill=(70, 70, 78), font=font(13))
    for i, (title, form) in enumerate(forms):
        top = 100 + i * row
        ox, oy = 30 - 0.42 * scale, top + 0.55 * scale + 40
        to = lambda z, y: (ox + z * scale, oy - y * scale)
        d.text((30, top), title, fill=(20, 20, 24), font=font(18))
        bz, bl = BODY[form]; bh = BODYH[form]
        x0, y0 = to(bz - bl / 2, CY + bh / 2); x1, y1 = to(bz + bl / 2, CY - bh / 2)
        for k in range(int(x0), int(x1), 12): d.line([(k, y0), (min(k + 6, x1), y0)], fill=(120, 120, 130)); d.line([(k, y1), (min(k + 6, x1), y1)], fill=(120, 120, 130))
        for (z, y, l, h) in satellites(form):
            a = to(z - l / 2, y + h / 2); b = to(z + l / 2, y - h / 2)
            d.rectangle([a, b], fill=(60, 110, 200, 60), outline=(60, 110, 200, 160))
        parts = shared() + front(form)
        for p in parts:
            if min(p["size"]) <= 0: continue
            pts = [to(z, y) for z, y in corners(p)]
            poly = [pts[0], pts[1], pts[3], pts[2]]
            col = COL[p["fin"]]
            if p["shape"] == "ring":
                d.rectangle([min(q[0] for q in poly), min(q[1] for q in poly), max(q[0] for q in poly), max(q[1] for q in poly)], fill=col + (255,), outline=(10, 10, 12))
            else:
                d.polygon(poly, fill=col + (255,), outline=(10, 10, 12))
            if p["float"]:
                d.polygon(poly, outline=(255, 120, 0))
        for (z, y, l, h) in SIGHTS[form]:
            a = to(z - l / 2, y + h / 2); b = to(z + l / 2, y - h / 2)
            d.rectangle([a, b], outline=(90, 90, 100), fill=(150, 150, 160, 120))
    out = os.path.join(os.path.dirname(__file__), "..", "..", "docs", "weapon-redesign", "layout.png")
    img.save(out)
    print("wrote", os.path.abspath(out))

main()
