"""Render the framed 'family photo' from the generated figures (plan Task 4, Step 6).

Run: Blender --background --factory-startup --python Tools/blender/photo_render.py
Writes Assets/ModelHome/Textures/Photos/photo_family.png (1024x768).
"""
import math
import pathlib
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import common as C  # noqa: E402

FIG = C.ASSETS / "Models" / "Figures"
OUT = C.ASSETS / "Textures" / "Photos" / "photo_family.png"
CAST = [("Dad_Standing", -0.42, 0.10), ("Daughter_Standing", 0.0, -0.25), ("Mom_Standing", 0.40, 0.08)]
PORTRAIT_SMILE = 0.22  # a held, slightly-too-wide catalogue smile


def main():
    C.reset_scene()
    for variant, x, y in CAST:
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(FIG / f"Figure_{variant}.fbx"))
        new = set(bpy.context.scene.objects) - before
        root = next(o for o in new if o.name.startswith(f"Figure_{variant}"))
        root.location = (x, y, 0)
        root.rotation_euler = (0, 0, math.radians(-6 * (1 if x > 0 else -1) if x else 0))
        for o in new:
            if o.type == "MESH" and o.data.shape_keys and "SmileWide" in o.data.shape_keys.key_blocks:
                o.data.shape_keys.key_blocks["SmileWide"].value = PORTRAIT_SMILE
            if o.name.startswith("Prop"):
                o.hide_render = True  # Mom's mug stays out of the portrait
    backdrop = bpy.data.meshes.new("Backdrop")
    backdrop.from_pydata([(-4, 1.2, 0), (4, 1.2, 0), (4, 1.2, 4), (-4, 1.2, 4)], [], [(0, 1, 2, 3)])
    backdrop.materials.append(C.material("Backdrop", "#6E7275", roughness=0.9))
    C.new_mesh_object("Backdrop", backdrop)
    floor = bpy.data.meshes.new("PhotoFloor")
    floor.from_pydata([(-4, -4, 0), (4, -4, 0), (4, 1.2, 0), (-4, 1.2, 0)], [], [(0, 1, 2, 3)])
    floor.materials.append(C.material("Backdrop", "#6E7275", roughness=0.9))
    C.new_mesh_object("PhotoFloor", floor)
    target = Vector((0, 0, 1.25))
    C.studio_render(OUT, (0.15, -4.6, 1.45), target, lens=70, res=(1024, 768), key_energy=500)
    print(f"wrote {OUT}")


main()
