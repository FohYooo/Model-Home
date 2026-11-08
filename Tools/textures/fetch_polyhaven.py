"""Download the CC0 Poly Haven texture sets used by Model Home (plan Task 3).

Run: python3 -I Tools/textures/fetch_polyhaven.py
Writes Assets/ModelHome/Textures/PolyHaven/<id>/<id>_{diff,nor_gl,rough,ao}_2k.jpg and a
URP mask map <id>_mask_2k.png (R=metallic 0, G=AO, B=0, A=smoothness=255-roughness).
Existing files are skipped, so re-runs only fill gaps. The jpg/png payloads are gitignored;
their .meta files are committed so material references survive a fresh clone + re-fetch.
"""
import json
import pathlib
import sys
import urllib.request

import numpy as np
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "ModelHome" / "Textures" / "PolyHaven"
UA = {"User-Agent": "model-home-devtool"}
IDS = ["laminate_floor_02", "plaster_grey_04", "ceiling_interior", "fabric_pattern_05",
       "dirty_carpet", "kitchen_wood", "marble_01", "dirt", "denim_fabric", "fabric_pattern_07"]
MAPS = {"Diffuse": "diff", "nor_gl": "nor_gl", "Rough": "rough", "AO": "ao"}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return r.read()


def fetch_set(asset_id):
    d = OUT / asset_id
    d.mkdir(parents=True, exist_ok=True)
    files = json.loads(get(f"https://api.polyhaven.com/files/{asset_id}"))
    for key, short in MAPS.items():
        dest = d / f"{asset_id}_{short}_2k.jpg"
        if dest.exists():
            continue
        # some fabrics ship colour variants (col_01..) instead of a single Diffuse
        candidates = [key, "col_01", "col_1"] if key == "Diffuse" else [key]
        entry = next((files[k]["2k"]["jpg"] for k in candidates
                      if files.get(k, {}).get("2k", {}).get("jpg")), None)
        if not entry:
            if key == "AO":
                continue  # optional; mask falls back to AO = 1
            raise RuntimeError(f"{asset_id}: no 2k jpg for {key}")
        url = entry["url"]
        if not url.startswith("https://dl.polyhaven.org/"):
            raise RuntimeError(f"{asset_id}: unexpected download host in {url}")
        dest.write_bytes(get(url))
        print(f"  {dest.name} ({dest.stat().st_size // 1024} KB)")
    make_mask(d, asset_id)


def make_mask(d, asset_id):
    mask = d / f"{asset_id}_mask_2k.png"
    if mask.exists():
        return
    rough = np.asarray(Image.open(d / f"{asset_id}_rough_2k.jpg").convert("L"))
    ao_path = d / f"{asset_id}_ao_2k.jpg"
    if ao_path.exists():
        ao = np.asarray(Image.open(ao_path).convert("L").resize(rough.shape[::-1]))
    else:
        ao = np.full_like(rough, 255)
    rgba = np.zeros(rough.shape + (4,), dtype=np.uint8)
    rgba[..., 1] = ao
    rgba[..., 3] = 255 - rough
    Image.fromarray(rgba, "RGBA").save(mask)


def main():
    for i in IDS:
        print(i)
        fetch_set(i)
    print("done")


if __name__ == "__main__":
    sys.exit(main())
