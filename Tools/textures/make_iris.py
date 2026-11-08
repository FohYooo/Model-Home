"""Generate procedural iris textures (plan Task 3).

Run: python3 -I Tools/textures/make_iris.py
Writes Assets/ModelHome/Textures/Eyes/iris_<Colour>.png (1024x1024). The texture maps onto the
eyeball's iris cap with planar UVs centred on the pupil: radius 0.5 (UV) = the cap edge.
"""
import pathlib

import numpy as np
from PIL import Image, ImageFilter

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "ModelHome" / "Textures" / "Eyes"
SIZE = 1024
PUPIL = 0.18   # radius as a fraction of the half-size
IRIS_EDGE = 0.92

COLOURS = {  # inner (collarette) colour, outer colour
    "GreyBlue": ((150, 160, 160), (88, 112, 135)),
    "Brown": ((120, 82, 45), (70, 45, 28)),
    "BlueGrey": ((165, 180, 190), (95, 120, 140)),
    "DarkGrey": ((80, 82, 85), (45, 47, 50)),
}


def iris(inner, outer, seed):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:SIZE, 0:SIZE]
    u = (x - SIZE / 2 + 0.5) / (SIZE / 2)
    v = (y - SIZE / 2 + 0.5) / (SIZE / 2)
    r = np.sqrt(u * u + v * v)
    a = np.arctan2(v, u)

    # radial fibres: sum of angular sinusoids with random phase, modulated by radius
    fib = np.zeros_like(r)
    for k in rng.integers(40, 260, 24):
        fib += np.sin(k * a + rng.uniform(0, 2 * np.pi) + 6 * r * rng.uniform(-1, 1))
    fib = (fib - fib.min()) / (fib.max() - fib.min())

    t = np.clip((r - PUPIL) / (IRIS_EDGE - PUPIL), 0, 1)
    inner_c = np.array(inner, float)
    outer_c = np.array(outer, float)
    col = inner_c * (1 - t[..., None]) + outer_c * t[..., None]
    col *= (0.75 + 0.5 * fib[..., None] * (0.6 + 0.4 * t[..., None]))

    collarette = np.exp(-((r - (PUPIL + 0.12)) / 0.03) ** 2)
    col *= 1 + 0.25 * collarette[..., None]

    limbal = np.clip((r - (IRIS_EDGE - 0.12)) / 0.12, 0, 1) ** 1.5
    col *= 1 - 0.75 * limbal[..., None]

    pupil = np.clip((PUPIL + 0.01 - r) / 0.02, 0, 1)
    col = col * (1 - pupil[..., None]) + np.array([8, 8, 9], float) * pupil[..., None]

    sclera = np.clip((r - IRIS_EDGE) / 0.03, 0, 1)
    col = col * (1 - sclera[..., None]) + np.array([235, 230, 222], float) * sclera[..., None]

    img = Image.fromarray(np.clip(col, 0, 255).astype(np.uint8), "RGB")
    return img.filter(ImageFilter.GaussianBlur(0.8))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for i, (name, (inner, outer)) in enumerate(COLOURS.items()):
        iris(inner, outer, 40 + i).save(OUT / f"iris_{name}.png")
    print(f"wrote {len(COLOURS)} irises to {OUT}")


if __name__ == "__main__":
    main()
