"""The HUD's gun form icons (Assets/Textures/{gun,machinegun,shotgun,sniper}.png), drawn from the
gun's layout (GunDesign, mirrored in layout_preview.py) and its floating pieces (GunSatellites).

White silhouettes on transparent, 400 x 200, muzzle to the left and the grip near the right edge,
like the icons they replace (the HUD tints them). The floating parts stand a little apart, and the
core shows in its window, so the icons read as the same machine as the gun in your hands.
Run: python3 Tools/WeaponDesign/icons.py
"""
import os, sys
from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from layout_preview import shared, front, satellites, corners  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
FILES = {"tight": "gun.png", "orbit": "machinegun.png", "fan": "shotgun.png", "rail": "sniper.png"}
W, H, SS = 400, 200, 4          # size, and supersampling for smooth edges
WHITE = (247, 248, 255, 255)
CLEAR = (0, 0, 0, 0)
GAP = 0.012                     # the gap round floating parts, in metres


def outline(p):
    pts = corners(p)
    return [pts[0], pts[1], pts[3], pts[2]]


def draw(form):
    parts = [p for p in shared() + front(form) if min(p["size"]) > 0]
    sats = satellites(form)
    # Extents, to fit the icon
    zs, ys = [], []
    for p in parts:
        for z, y in outline(p):
            zs.append(z); ys.append(y)
    if form == "rail":
        ys.append(0.5075)
    for z, y, l, h in sats:
        zs += [z - l / 2, z + l / 2]; ys += [y - h / 2, y + h / 2]
    zmin, zmax, ymin, ymax = min(zs), max(zs), min(ys), max(ys)
    scale = min(372 / (zmax - zmin), 176 / (ymax - ymin), 235)
    right = W - 14                  # the back of the gun near the right edge
    bottom = H - 10 - (176 - (ymax - ymin) * scale) / 2

    def to(z, y):
        # Mirrored: the muzzle (high z) to the left
        return ((right - (z - zmin) * scale) * SS, (bottom - (y - ymin) * scale) * SS)

    img = Image.new("RGBA", (W * SS, H * SS), CLEAR)
    d = ImageDraw.Draw(img)

    def box(z, y, l, h, fill):
        a, b = to(z + l / 2, y + h / 2), to(z - l / 2, y - h / 2)
        d.rectangle([a, b], fill=fill)

    # The solid parts first, then the floating ones with a clear gap round them
    solid = [p for p in parts if not p["float"] and p["fin"] != "glow"]
    # Floating parts that stick out of the outline (the side modules float off the sides, so not them)
    floating = [p for p in parts if p["float"] and p["fin"] != "glow" and p["name"] != "Side Module"]
    for p in solid:
        d.polygon([to(z, y) for z, y in outline(p)], fill=WHITE)
    # The core's window: open, with the core in it
    box(1.25, 0.275, 0.12, 0.13, CLEAR)
    box(1.25, 0.275, 0.09, 0.07, WHITE)
    # The sniper's scope (part of the prefab, not the layout)
    if form == "rail":
        box(1.29, 0.4625, 0.618, 0.09, WHITE)
    for p in floating:
        pts = outline(p)
        zs_ = [q[0] for q in pts]; ys_ = [q[1] for q in pts]
        box((min(zs_) + max(zs_)) / 2, (min(ys_) + max(ys_)) / 2, max(zs_) - min(zs_) + 2 * GAP, max(ys_) - min(ys_) + 2 * GAP, CLEAR)
        d.polygon([to(z, y) for z, y in pts], fill=WHITE)
    # The floating pieces (GunSatellites), in segments where they're segmented
    for z, y, l, h in sats:
        if form == "rail" or (form == "tight" and l < 0.05):
            box(z, y, l, h, WHITE)
        elif form == "orbit":
            # The rotary barrel: slats with gaps between
            for k in range(3):
                box(z, y + (k - 1) * h / 3, l, h / 3 - GAP, WHITE)
        elif form == "fan" and l < 0.3:
            # The claw: petals flaring open
            for k in range(3):
                zz = z - l / 2 + l * (k + 0.5) / 3
                spread = h * (0.6 + 0.4 * k / 2)
                box(zz, y + spread / 2 - 0.02, l / 3 - GAP, 0.035, WHITE)
                box(zz, y - spread / 2 + 0.02, l / 3 - GAP, 0.035, WHITE)
    return img.resize((W, H), Image.LANCZOS)


def main():
    for form, name in FILES.items():
        path = os.path.join(ROOT, "Assets", "Textures", name)
        draw(form).save(path)
        print("wrote", os.path.relpath(path, ROOT))


if __name__ == "__main__":
    main()
