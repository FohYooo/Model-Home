"""Contract test for the generated figure FBX files (plan Task 4).

Run: Blender --background --factory-startup --python Tools/blender/verify_figures.py
Exits 1 and lists every failure if any figure breaks the Blender -> Unity contract.
"""
import pathlib
import sys

import bpy
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
import os
FIG_DIR = pathlib.Path(os.environ["FIG_DIR"]) if os.environ.get("FIG_DIR") else ROOT / "Assets" / "ModelHome" / "Models" / "Figures"

VARIANTS = ["Dad_Seated", "Dad_Standing", "Mom_Standing", "Daughter_Seated", "Daughter_Standing", "Baby_Lying"]
# standing variants: crown height; lying: body length. Seated variants have no height target.
SIZE_TARGET = {"Dad_Standing": 1.83, "Mom_Standing": 1.68, "Daughter_Standing": 1.32, "Baby_Lying": 0.62}
PARENT = {"Body": "ROOT", "Clothes": "ROOT", "Head": "ROOT",
          "Eye_L": "Head", "Eye_R": "Head", "Lid_L": "Head", "Lid_R": "Head", "Hair": "Head"}
KEYS = {"Body": {"Breathe", "NeckLong"}, "Clothes": {"Breathe", "NeckLong"}, "Head": {"SmileWide"},
        "Lid_L": {"Blink"}, "Lid_R": {"Blink"}}
TRI_BUDGET = {"Body": 30_000, "Head": 12_000}


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def find(name, objs):
    for o in objs:
        if o.name == name or o.name.split(".")[0] == name:
            return o
    return None


def check_variant(variant):
    fails = []
    path = FIG_DIR / f"Figure_{variant}.fbx"
    if not path.exists():
        return [f"{variant}: missing {path.name}"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    objs = list(bpy.context.scene.objects)
    root = find(f"Figure_{variant}", objs)
    if root is None:
        return [f"{variant}: no root named Figure_{variant} (got {[o.name for o in objs]})"]

    for child, parent in PARENT.items():
        if variant.startswith("Baby") and child == "Hair":
            continue
        o = find(child, objs)
        if o is None:
            fails.append(f"{variant}: missing {child}")
            continue
        want = root if parent == "ROOT" else find(parent, objs)
        if o.parent != want:
            fails.append(f"{variant}: {child} parent is {o.parent and o.parent.name}, want {want and want.name}")

    for name, keys in KEYS.items():
        o = find(name, objs)
        if o is None or o.type != "MESH":
            continue
        blocks = o.data.shape_keys.key_blocks if o.data.shape_keys else []
        have = {k.name for k in blocks}
        missing = keys - have
        if missing:
            fails.append(f"{variant}: {name} missing shape keys {sorted(missing)}")
        for k in blocks:
            if k.name in keys and abs(k.value) > 1e-6:
                fails.append(f"{variant}: {name} key {k.name} exported at weight {k.value:.2f}, want 0")

    for name, budget in TRI_BUDGET.items():
        o = find(name, objs)
        if o is not None and o.type == "MESH" and tris(o) > budget:
            fails.append(f"{variant}: {name} has {tris(o)} tris > {budget}")

    eye_l, eye_r = find("Eye_L", objs), find("Eye_R", objs)
    if eye_l and eye_r:
        cl, cr = eye_l.matrix_world.translation, eye_r.matrix_world.translation
        local_l = root.matrix_world.inverted() @ cl
        local_r = root.matrix_world.inverted() @ cr
        # symmetric about the figure's midline: the midline is the head's local X = 0
        head = find("Head", objs)
        hl = head.matrix_world.inverted() @ cl
        hr = head.matrix_world.inverted() @ cr
        if abs(hl.x + hr.x) > 0.002 or abs(hl.z - hr.z) > 0.002:
            fails.append(f"{variant}: eyes not symmetric in head space ({hl.x:.4f}, {hr.x:.4f})")
        for eye in (eye_l, eye_r):
            front = iris_direction(eye, head)
            if front is None:
                fails.append(f"{variant}: {eye.name} has no iris material faces")
            elif front.y > -0.8:
                fails.append(f"{variant}: {eye.name} iris faces {tuple(round(c, 2) for c in front)}, want head -Y")
        del local_l, local_r

    target = SIZE_TARGET.get(variant)
    body = find("Body", objs)
    if target and body is not None:
        # real vertices in world space (the importer restores Blender Z-up); rotated bound-box
        # corners would overestimate the extent
        pts = [o.matrix_world @ v.co
               for o in objs if o.type == "MESH" and o.name.split(".")[0] in ("Body", "Head", "Hair", "Clothes")
               for v in o.data.vertices]
        if variant == "Baby_Lying":
            size = max(max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3))
            tol = 0.05
        else:
            size = max(p.z for p in pts) - min(p.z for p in pts)
            tol = 0.03
        if abs(size - target) > target * tol:
            fails.append(f"{variant}: size {size:.3f} m, want {target} +-{int(tol * 100)}%")
    return fails


def iris_direction(eye, head):
    me = eye.data
    iris_slots = {i for i, m in enumerate(me.materials) if m and m.name.split(".")[0].startswith("M_Iris_")}
    if not iris_slots:
        return None
    acc, n = Vector(), 0
    for p in me.polygons:
        if p.material_index in iris_slots:
            acc += p.center
            n += 1
    if not n:
        return None
    world = eye.matrix_world @ (acc / n)
    to_iris = head.matrix_world.inverted().to_3x3() @ (world - eye.matrix_world.translation)
    return to_iris.normalized()


def main():
    fails = []
    for v in VARIANTS:
        fails += check_variant(v)
    if fails:
        print(f"VERIFY FAIL ({len(fails)})")
        for f in fails:
            print("  -", f)
        sys.exit(1)
    print(f"VERIFY PASS ({len(VARIANTS)} figures)")


main()
