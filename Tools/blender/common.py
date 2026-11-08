"""Shared helpers for the Model Home Blender generators (run headless, Blender 5.2)."""
import math
import pathlib
import sys

import bpy
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets" / "ModelHome"
OUT = ROOT / "Tools" / "blender" / "out"


def script_args():
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def link(obj, parent=None):
    bpy.context.scene.collection.objects.link(obj)
    if parent is not None:
        obj.parent = parent
    return obj


def new_mesh_object(name, mesh, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, mesh)
    obj.location = location
    return link(obj, parent)


def new_empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    obj.empty_display_size = 0.1
    obj.location = location
    return link(obj, parent)


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(srgb_to_linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4))


def material(name, color="#CCCCCC", roughness=0.5, metallic=0.0, alpha=1.0, image=None):
    """Get or create a Principled material. Only the NAME matters to Unity (remap key);
    the values make the Blender previews representative."""
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except AttributeError:
        pass
    rgb = hex_rgb(color) if isinstance(color, str) else color
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        for attr, val in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
            try:
                setattr(m, attr, val)
            except (AttributeError, TypeError):
                pass
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(str(image), check_existing=True)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    m.diffuse_color = (*rgb, alpha)
    return m


def bake_modifiers(obj):
    """Apply all modifiers by evaluating the object; keeps the object, replaces its mesh."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    old = obj.data
    obj.modifiers.clear()
    obj.data = me
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def shade_smooth(me):
    for p in me.polygons:
        p.use_smooth = True


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def export_fbx(root, path):
    path = pathlib.Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(False)
    for o in [root, *root.children_recursive]:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=False, add_leaf_bones=False,
        mesh_smooth_type="FACE", path_mode="STRIP", use_custom_props=False, bake_anim=False)


# ---------- preview rendering ----------

def _set_engine(scene):
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = eng
            return eng
        except TypeError:
            continue
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    return "CYCLES"


def _world(scene, color=(0.035, 0.035, 0.04)):
    w = bpy.data.worlds.get("PreviewWorld") or bpy.data.worlds.new("PreviewWorld")
    try:
        w.use_nodes = True
    except AttributeError:
        pass
    bg = next((n for n in w.node_tree.nodes if n.type == "BACKGROUND"), None)
    if bg is not None:
        bg.inputs["Color"].default_value = (*color, 1)
        bg.inputs["Strength"].default_value = 1.0
    scene.world = w


def look_at(obj, target):
    d = Vector(target) - obj.location
    obj.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def studio_render(path, cam_loc, target, lens=50.0, res=(768, 768), key_energy=300.0, dark=False):
    """Render the current scene from cam_loc toward target with a 3-light studio rig."""
    scene = bpy.context.scene
    _set_engine(scene)
    _world(scene, (0.01, 0.01, 0.012) if dark else (0.035, 0.035, 0.04))
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    try:
        scene.view_settings.view_transform = "AgX"
    except TypeError:
        pass
    temp = []
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = lens
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    link(cam)
    cam.location = cam_loc
    look_at(cam, target)
    scene.camera = cam
    temp.append(cam)
    tgt = Vector(target)
    fwd = (tgt - Vector(cam_loc)).normalized()
    side = fwd.cross(Vector((0, 0, 1))).normalized()
    for name, offset, energy, size in (
            ("Key", -side * 1.6 + Vector((0, 0, 1.2)) - fwd * 1.2, key_energy, 1.2),
            ("Fill", side * 2.0 - fwd * 1.0 + Vector((0, 0, 0.3)), key_energy * 0.25, 2.0),
            ("Rim", fwd * 2.0 + Vector((0, 0, 1.5)), key_energy * 0.6, 1.0)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy = energy
        ld.size = size
        lo = bpy.data.objects.new(name, ld)
        link(lo)
        lo.location = tgt + offset
        look_at(lo, tgt)
        temp.append(lo)
    scene.render.filepath = str(path)
    pathlib.Path(path).parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.render.render(write_still=True)
    for o in temp:
        data = o.data
        bpy.data.objects.remove(o)
        if isinstance(data, bpy.types.Camera):
            bpy.data.cameras.remove(data)
        else:
            bpy.data.lights.remove(data)


def deg(x):
    return math.radians(x)
