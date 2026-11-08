"""Check the fetched/generated texture payloads (plan Task 3, Python side).

Run: python3 -I Tools/textures/test_textures.py
"""
import pathlib
import sys

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
PH = ROOT / "Assets" / "ModelHome" / "Textures" / "PolyHaven"
EYES = ROOT / "Assets" / "ModelHome" / "Textures" / "Eyes"

IDS = ["laminate_floor_02", "plaster_grey_04", "ceiling_interior", "fabric_pattern_05",
       "dirty_carpet", "kitchen_wood", "marble_01", "dirt", "denim_fabric", "fabric_pattern_07"]
IRIS = ["GreyBlue", "Brown", "BlueGrey", "DarkGrey"]


def main():
    fails = []
    for i in IDS:
        d = PH / i
        for suffix in ("diff_2k.jpg", "nor_gl_2k.jpg", "rough_2k.jpg", "mask_2k.png"):
            if not (d / f"{i}_{suffix}").exists():
                fails.append(f"{i}: missing {suffix}")
        mask, rough = d / f"{i}_mask_2k.png", d / f"{i}_rough_2k.jpg"
        if mask.exists() and rough.exists():
            m = np.asarray(Image.open(mask))
            r = np.asarray(Image.open(rough).convert("L")).astype(int)
            if m.ndim != 3 or m.shape[2] != 4:
                fails.append(f"{i}: mask is not RGBA")
            else:
                if m[..., 0].max() != 0:
                    fails.append(f"{i}: mask R (metallic) must be 0")
                if np.abs(m[..., 3].astype(int) - (255 - r)).max() > 1:
                    fails.append(f"{i}: mask A != 255 - roughness")
    for c in IRIS:
        p = EYES / f"iris_{c}.png"
        if not p.exists():
            fails.append(f"iris_{c}.png missing")
            continue
        a = np.asarray(Image.open(p).convert("RGB")).astype(int)
        if a.shape[:2] != (1024, 1024):
            fails.append(f"iris_{c}: size {a.shape[:2]}, want 1024x1024")
        centre = a[500:524, 500:524].mean()
        ring = a[512, 512 + 200:512 + 300].mean()
        if not centre < 40:
            fails.append(f"iris_{c}: pupil not dark (mean {centre:.0f})")
        if not ring > centre + 20:
            fails.append(f"iris_{c}: iris not brighter than pupil")
    if fails:
        print(f"FAIL ({len(fails)})")
        for f in fails:
            print("  -", f)
        sys.exit(1)
    print(f"PASS ({len(IDS)} texture sets, {len(IRIS)} irises)")


if __name__ == "__main__":
    main()
