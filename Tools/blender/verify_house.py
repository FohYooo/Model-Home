"""Contract test for the generated show-home FBX (plan Task 5).

Run: Blender --background --factory-startup --python Tools/blender/verify_house.py
Exits 1 and lists every failure if the house breaks the Blender -> Unity contract.
"""
import os
import pathlib
import sys

import bpy
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
HOUSE = pathlib.Path(os.environ["HOUSE_FBX"]) if os.environ.get("HOUSE_FBX") else \
    ROOT / "Assets" / "ModelHome" / "Models" / "House" / "ModelHome_House.fbx"

ANCHORS = {  # name -> (position, facing direction = the empty's -Y axis)
    "Anchor_PlayerSpawn": ((1.8, 2.4, 0.0), (1, 0, 0)),
    "Anchor_Dad_Armchair": ((7.3, 3.8, 0.0), (-1, -0.5, 0)),
    "Anchor_Dad_LivingDoorway": ((5.5, 4.6, 0.0), (0, -1, 0)),
    "Anchor_Mom_Counter": ((9.8, 9.3, 0.0), (0, 1, 0)),
    "Anchor_Mom_FacingHall": ((9.8, 9.3, 0.0), (-1, 0, 0)),
    "Anchor_Mom_BackHallDoor": ((3.6, 6.5, 0.0), (1, 0, 0)),
    "Anchor_Daughter_Chair": ((5.5, 8.15, 0.0), (0, -1, 0)),
    "Anchor_Baby_Bassinet": ((7.2, 8.9, 0.55), (0, -1, 0)),  # identity: figure lies in its own space
}
REQUIRED = [
    "Light_FoyerLamp", "Light_LivingLamp", "Light_StudyLamp", "Light_KitchenPendant",
    "INT_Clipboard", "INT_FrontDoor", "INT_BackDoor", "INT_StudyWindow", "INT_KitchenWindow",
    "INT_UtilityWindow", "INT_LivingLamp", "INT_StudyLamp", "INT_DiningTable",
    "SND_Fridge", "SND_Clock", "COL_StairsBlock", "Rug",
]
MATERIALS = {
    "M_Floor_Wood", "M_Wall_Plaster", "M_Ceiling", "M_Fabric_Sofa", "M_Rug", "M_Cabinet_Wood",
    "M_Counter_Marble", "M_Ground_Dirt", "M_Door_Paint", "M_Skirting_Paint", "M_Metal", "M_Glass",
    "M_Lampshade", "M_Paper", "M_Photo_Family", "M_Newspaper", "M_Mug", "M_Ink", "M_Tape",
}


def base(name):
    return name.split(".")[0]


def main():
    fails = []
    if not HOUSE.exists():
        print(f"VERIFY FAIL (1)\n  - missing {HOUSE}")
        sys.exit(1)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(HOUSE))
    objs = {base(o.name): o for o in bpy.context.scene.objects}

    for name, (pos, facing) in ANCHORS.items():
        o = objs.get(name)
        if o is None:
            fails.append(f"missing {name}")
            continue
        p = o.matrix_world.translation
        if (p - Vector(pos)).length > 0.02:
            fails.append(f"{name} at {tuple(round(c, 3) for c in p)}, want {pos}")
        fwd = -(o.matrix_world.to_3x3() @ Vector((0, 1, 0))).normalized()
        want = Vector(facing).normalized()
        if fwd.dot(want) < 0.99:
            fails.append(f"{name} faces {tuple(round(c, 2) for c in fwd)}, want {tuple(round(c, 2) for c in want)}")

    for name in REQUIRED:
        if name not in objs:
            fails.append(f"missing {name}")

    bad = set()
    floor_area = 0.0
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        mats = [base(m.name) for m in o.data.materials if m]
        bad |= set(mats) - MATERIALS
        mw = o.matrix_world
        for poly in o.data.polygons:
            if poly.material_index < len(mats) and mats[poly.material_index] == "M_Floor_Wood":
                n = (mw.to_3x3() @ poly.normal).normalized()
                c = mw @ poly.center
                if n.z > 0.99 and abs(c.z) < 0.02 and 0 < c.x < 12 and 0 < c.y < 10:
                    floor_area += poly.area
    if bad:
        fails.append(f"materials outside the Task 3 library: {sorted(bad)}")
    if floor_area < 110:
        fails.append(f"walkable wood floor {floor_area:.1f} m2 < 110 m2")

    if fails:
        print(f"VERIFY FAIL ({len(fails)})")
        for f in fails:
            print("  -", f)
        sys.exit(1)
    print(f"VERIFY PASS (house: {len(objs)} objects, floor {floor_area:.1f} m2)")


main()
