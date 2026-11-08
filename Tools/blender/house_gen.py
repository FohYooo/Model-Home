"""Procedural ground floor of the "Lot 14" show home (plan Task 5).

Run:
  Blender --background --factory-startup --python Tools/blender/house_gen.py -- \
      [--out Assets/ModelHome/Models/House/ModelHome_House.fbx] [--no-preview]

Blender metres: X right, Y into the house (front wall at Y=0), Z up. Walls 0.12 thick, ceiling 2.6.
Anchor empties face along their -Y axis (same convention as the figures' front).
"""
import math
import pathlib
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import common as C  # noqa: E402

WALL_T = 0.12
CEIL = 2.6
DOOR_H = 2.05
TILE = {"M_Floor_Wood": 2.0, "M_Wall_Plaster": 2.0, "M_Ceiling": 2.0, "M_Fabric_Sofa": 0.6, "M_Rug": 1.5,
        "M_Cabinet_Wood": 1.0, "M_Counter_Marble": 1.5, "M_Ground_Dirt": 4.0}
PREVIEW_COL = {"M_Floor_Wood": "#8A6A4E", "M_Wall_Plaster": "#E9E4DA", "M_Ceiling": "#EDEAE4",
               "M_Fabric_Sofa": "#9AA3A8", "M_Rug": "#C9C2B5", "M_Cabinet_Wood": "#7A5C42",
               "M_Counter_Marble": "#E6E3DE", "M_Ground_Dirt": "#4A3F33", "M_Door_Paint": "#F1EFEA",
               "M_Skirting_Paint": "#F4F2EE", "M_Metal": "#8C8F93", "M_Glass": "#B8C8D0",
               "M_Lampshade": "#F3E9D2", "M_Paper": "#F2F0EA", "M_Photo_Family": "#888888",
               "M_Tape": "#E8C320", "M_Ink": "#141414"}

# wall lines: (axis, fixed coord, start, end, [(o_start, o_end, bottom, top), ...], interior_sides)
#   axis "x": runs along X at y = fixed; axis "y": runs along Y at x = fixed
#   interior_sides: which faces get skirting: +1 (positive normal side), -1, or both
WALLS = [
    ("x", 0.0, 0.0, 12.0, [(1.0, 2.0, 0.0, DOOR_H), (4.5, 6.5, 0.9, 2.1), (9.5, 11.0, 0.9, 2.1)], (1,)),
    ("x", 10.0, 0.0, 12.0, [(1.0, 2.0, 0.0, DOOR_H), (9.4, 10.6, 1.05, 1.95)], (-1,)),
    ("y", 0.0, 0.0, 10.0, [(7.5, 8.5, 1.1, 1.9)], (1,)),
    ("y", 12.0, 0.0, 10.0, [], (-1,)),
    ("y", 3.0, 0.0, 10.0, [(1.2, 2.8, 0.0, 2.1), (6.0, 7.0, 0.0, DOOR_H)], (1, -1)),
    ("x", 4.0, 0.0, 3.0, [(1.0, 2.0, 0.0, DOOR_H)], (1, -1)),
    ("x", 5.0, 3.0, 12.0, [(4.0, 7.0, 0.0, 2.1)], (1, -1)),
    ("y", 8.0, 0.0, 5.0, [(1.5, 2.4, 0.0, DOOR_H)], (1, -1)),
]
WINDOWS = [  # (axis, fixed, start, end, sill, top, latch_name or None)
    ("x", 0.0, 4.5, 6.5, 0.9, 2.1, None),
    ("x", 0.0, 9.5, 11.0, 0.9, 2.1, "INT_StudyWindow"),
    ("x", 10.0, 9.4, 10.6, 1.05, 1.95, "INT_KitchenWindow"),
    ("y", 0.0, 7.5, 8.5, 1.1, 1.9, "INT_UtilityWindow"),
]
DOORS = [  # (axis, fixed, start, end, open_deg, hinge_at_start, lock_name, interior_sign)
    ("x", 0.0, 1.0, 2.0, 0, True, "INT_FrontDoor", 1),
    ("x", 10.0, 1.0, 2.0, 0, True, "INT_BackDoor", -1),
    ("x", 4.0, 1.0, 2.0, 80, True, None, 1),
    ("y", 3.0, 6.0, 7.0, -80, False, None, 1),
    ("y", 8.0, 1.5, 2.4, 80, True, None, 1),
]
ANCHORS = {  # name -> (position, facing)
    "Anchor_PlayerSpawn": ((1.8, 2.4, 0.0), (1, 0)),
    "Anchor_Dad_Armchair": ((7.3, 3.8, 0.0), (-1, -0.5)),
    "Anchor_Dad_LivingDoorway": ((5.5, 4.6, 0.0), (0, -1)),
    "Anchor_Mom_Counter": ((9.8, 9.3, 0.0), (0, 1)),
    "Anchor_Mom_FacingHall": ((9.8, 9.3, 0.0), (-1, 0)),
    "Anchor_Mom_BackHallDoor": ((3.6, 6.5, 0.0), (1, 0)),
    "Anchor_Daughter_Chair": ((5.5, 8.15, 0.0), (0, -1)),
    "Anchor_Baby_Bassinet": ((7.2, 8.9, 0.55), (0, -1)),
}
SEAT_ARMCHAIR = 0.42   # figure_gen Dad_Seated is built for this seat height
SEAT_DINING = 0.45     # figure_gen Daughter_Seated is built for this seat height


def yaw_for(facing):
    """Z rotation that turns -Y onto the (x, y) facing direction."""
    fx, fy = facing
    return math.atan2(fx, -fy)


class MeshBuilder:
    """Accumulates boxes/prisms with material slots into one bmesh, then makes an object."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def mat_index(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def box(self, lo, hi, mat, rot_z=0.0, pivot=None, bevel=0.0):
        lo, hi = Vector(lo), Vector(hi)
        size = hi - lo
        if min(size) <= 1e-6:
            return
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        bmesh.ops.scale(tmp, vec=size, verts=tmp.verts)
        if bevel > 0:
            bmesh.ops.bevel(tmp, geom=tmp.edges[:], offset=min(bevel, min(size) * 0.45), segments=2,
                            affect="EDGES", profile=0.5)
        centre = (lo + hi) / 2
        bmesh.ops.translate(tmp, vec=centre, verts=tmp.verts)
        if rot_z:
            p = Vector(pivot) if pivot is not None else centre
            bmesh.ops.rotate(tmp, cent=p, matrix=Matrix.Rotation(rot_z, 3, "Z"), verts=tmp.verts)
        self._merge(tmp, mat)

    def cylinder(self, centre, radius, depth, mat, axis="Z", segments=24, radius_top=None, caps=True):
        tmp = bmesh.new()
        bmesh.ops.create_cone(tmp, cap_ends=caps, cap_tris=False, segments=segments, radius1=radius,
                              radius2=radius if radius_top is None else radius_top, depth=depth)
        if axis == "X":
            bmesh.ops.rotate(tmp, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "Y"), verts=tmp.verts)
        elif axis == "Y":
            bmesh.ops.rotate(tmp, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, "X"), verts=tmp.verts)
        bmesh.ops.translate(tmp, vec=Vector(centre), verts=tmp.verts)
        self._merge(tmp, mat)

    def prism_x(self, x0, x1, profile_yz, mat):
        """Polygon in the YZ plane extruded from x0 to x1."""
        tmp = bmesh.new()
        a = [tmp.verts.new((x0, y, z)) for y, z in profile_yz]
        b = [tmp.verts.new((x1, y, z)) for y, z in profile_yz]
        n = len(a)
        tmp.faces.new(list(reversed(a)))
        tmp.faces.new(b)
        for i in range(n):
            j = (i + 1) % n
            tmp.faces.new((a[i], a[j], b[j], b[i]))
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        self._merge(tmp, mat)

    def _merge(self, tmp, mat):
        idx = self.mat_index(mat)
        for f in tmp.faces:
            f.material_index = idx
        me = bpy.data.meshes.new("tmp")
        tmp.to_mesh(me)
        tmp.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)

    def to_object(self, name, parent):
        me = bpy.data.meshes.new(name)
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.verify()
        for f in bm.faces:
            mat = self.mats[f.material_index]
            tile = TILE.get(mat, 1.0)
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            a, b = [(1, 2), (0, 2), (0, 1)][ax]
            for loop in f.loops:
                p = loop.vert.co
                loop[uv].uv = (p[a] / tile, p[b] / tile)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(C.material(m, PREVIEW_COL.get(m, "#999999"),
                                           roughness=0.2 if m in ("M_Glass", "M_Counter_Marble") else 0.7,
                                           metallic=1.0 if m == "M_Metal" else 0.0,
                                           alpha=0.2 if m == "M_Glass" else 1.0))
        obj = C.new_mesh_object(name, me, parent=parent)
        return obj


def wall_box(mb, axis, fixed, a0, a1, z0, z1, mat="M_Wall_Plaster", thick=WALL_T):
    h = thick / 2
    if axis == "x":
        mb.box((a0, fixed - h, z0), (a1, fixed + h, z1), mat)
    else:
        mb.box((fixed - h, a0, z0), (fixed + h, a1, z1), mat)


def build_shell(root):
    walls, skirting, frames = MeshBuilder(), MeshBuilder(), MeshBuilder()
    for axis, fixed, start, end, openings, sides in WALLS:
        cuts = sorted(openings)
        pos = start
        solid_runs = []
        for o0, o1, bot, top in cuts:
            if o0 > pos:
                solid_runs.append((pos, o0))
            if bot > 0:
                wall_box(walls, axis, fixed, o0, o1, 0, bot)
            if top < CEIL:
                wall_box(walls, axis, fixed, o0, o1, top, CEIL)
            if bot > 0:
                solid_runs.append(("sill", o0, o1))
            pos = o1
        if end > pos:
            solid_runs.append((pos, end))
        for run in solid_runs:
            if run[0] == "sill":
                a0, a1 = run[1], run[2]
            else:
                a0, a1 = run
                wall_box(walls, axis, fixed, a0, a1, 0, CEIL)
            for side in sides:  # skirting boards on interior faces
                off = side * (WALL_T / 2 + 0.008)
                if axis == "x":
                    skirting.box((a0, fixed + off - 0.008, 0), (a1, fixed + off + 0.008, 0.1), "M_Skirting_Paint")
                else:
                    skirting.box((fixed + off - 0.008, a0, 0), (fixed + off + 0.008, a1, 0.1), "M_Skirting_Paint")
        # door casings around full-height openings
        for o0, o1, bot, top in cuts:
            if bot == 0:
                for a in (o0, o1 - 0.06):
                    wall_box(frames, axis, fixed, a, a + 0.06, 0, top, "M_Skirting_Paint", WALL_T + 0.03)
                wall_box(frames, axis, fixed, o0, o1, top - 0.06, top, "M_Skirting_Paint", WALL_T + 0.03)
    walls.to_object("Walls", root)
    skirting.to_object("Skirting", root)
    frames.to_object("Casings", root)

    floor = MeshBuilder()
    floor.box((-0.06, -0.06, -0.05), (12.06, 10.06, 0.0), "M_Floor_Wood")
    floor.to_object("Floor", root)

    ceil = MeshBuilder()  # with a stairwell hole over x 0.06-1.0, y 0.3-3.94
    ceil.box((-0.06, -0.06, CEIL), (12.06, 0.3, CEIL + 0.05), "M_Ceiling")
    ceil.box((-0.06, 3.94, CEIL), (12.06, 10.06, CEIL + 0.05), "M_Ceiling")
    ceil.box((-0.06, 0.3, CEIL), (0.06, 3.94, CEIL + 0.05), "M_Ceiling")
    ceil.box((1.0, 0.3, CEIL), (12.06, 3.94, CEIL + 0.05), "M_Ceiling")
    ceil.to_object("Ceiling", root)

    ground = MeshBuilder()
    ground.box((-14, -15, -0.12), (26, 25, -0.06), "M_Ground_Dirt")
    ground.to_object("Ground", root)


def build_windows_doors(root):
    glass, wframes = MeshBuilder(), MeshBuilder()
    for axis, fixed, a0, a1, sill, top, latch in WINDOWS:
        mid = (sill + top) / 2
        wall_box(glass, axis, fixed, a0, a1, sill, top, "M_Glass", 0.01)
        for (b0, b1, z0, z1) in ((a0, a0 + 0.05, sill, top), (a1 - 0.05, a1, sill, top),
                                 (a0, a1, sill, sill + 0.05), (a0, a1, top - 0.05, top),
                                 (a0, a1, mid - 0.03, mid + 0.03)):
            wall_box(wframes, axis, fixed, b0, b1, z0, z1, "M_Skirting_Paint", 0.07)
        # interior sill board
        if axis == "x":
            inside = 1 if fixed == 0.0 else -1
            wframes.box((a0 - 0.05, fixed + inside * 0.03 - 0.0 if inside > 0 else fixed - 0.13, sill - 0.03),
                        (a1 + 0.05, fixed + 0.13 if inside > 0 else fixed - 0.03, sill), "M_Skirting_Paint")
        else:
            wframes.box((fixed + 0.03, a0 - 0.05, sill - 0.03), (fixed + 0.13, a1 + 0.05, sill), "M_Skirting_Paint")
        if latch:
            lb = MeshBuilder()
            c = (a0 + a1) / 2
            if axis == "x":
                inside = 1 if fixed == 0.0 else -1
                y = fixed + inside * 0.045
                lb.box((c - 0.05, y - 0.012, mid + 0.03), (c + 0.05, y + 0.012, mid + 0.055), "M_Metal", bevel=0.004)
            else:
                lb.box((fixed + 0.033, c - 0.05, mid + 0.03), (fixed + 0.057, c + 0.05, mid + 0.055), "M_Metal", bevel=0.004)
            lb.to_object(latch, root)
    glass.to_object("WindowGlass", root)
    wframes.to_object("WindowFrames", root)

    for axis, fixed, a0, a1, open_deg, hinge_start, lock, inside in DOORS:
        leaf = MeshBuilder()
        w = (a1 - a0) - 0.12
        hinge = a0 + 0.06 if hinge_start else a1 - 0.06
        if axis == "x":
            lo = (a0 + 0.06, fixed - 0.02, 0.01)
            hi = (a1 - 0.06, fixed + 0.02, DOOR_H - 0.06)
            pivot = (hinge, fixed, 0)
        else:
            lo = (fixed - 0.02, a0 + 0.06, 0.01)
            hi = (fixed + 0.02, a1 - 0.06, DOOR_H - 0.06)
            pivot = (fixed, hinge, 0)
        rot = math.radians(open_deg)
        leaf.box(lo, hi, "M_Door_Paint", rot_z=rot, pivot=pivot, bevel=0.004)
        # handle on the latch side
        latch_a = a1 - 0.14 if hinge_start else a0 + 0.14
        for side in (1, -1):
            if axis == "x":
                hc = (latch_a, fixed + side * 0.045, 1.0)
            else:
                hc = (fixed + side * 0.045, latch_a, 1.0)
            tmp = MeshBuilder()
            leaf.box((hc[0] - 0.012, hc[1] - 0.012, hc[2] - 0.012), (hc[0] + 0.012, hc[1] + 0.012, hc[2] + 0.012),
                     "M_Metal", rot_z=rot, pivot=pivot)
            del tmp
        leaf.to_object(f"Door_{axis}{fixed:g}_{a0:g}", root)
        if lock:
            lb = MeshBuilder()
            y = fixed + inside * 0.04
            lb.box((latch_a - 0.025, y - 0.012, 1.08), (latch_a + 0.025, y + 0.012, 1.16), "M_Metal", bevel=0.004)
            lb.to_object(lock, root)
    _ = w  # noqa: F841


def build_stairs(root):
    st = MeshBuilder()
    rise, run, n = 0.18, 0.23, 16
    for i in range(n):
        y0 = 0.3 + i * run
        st.box((0.06, y0, 0), (0.98, y0 + run, rise * (i + 1)), "M_Floor_Wood")
    # stair side wall (sloped knee wall rising to full height)
    top0, slope = 0.9, rise / run
    y_full = 0.3 + (CEIL - top0) / slope
    st.prism_x(0.98, 1.08, [(0.3, 0.0), (3.94, 0.0), (3.94, CEIL), (y_full, CEIL), (0.3, top0)], "M_Wall_Plaster")
    # dark stairwell shaft above the ceiling hole
    for lo, hi in (((0.0, 0.24, CEIL), (1.08, 0.3, 5.4)), ((0.0, 3.94, CEIL), (1.08, 4.0, 5.4)),
                   ((1.02, 0.24, CEIL), (1.08, 4.0, 5.4)), ((0.0, 0.24, 5.4), (1.08, 4.0, 5.46))):
        st.box(lo, hi, "M_Wall_Plaster")
    st.box((0.9, 0.28, 0), (1.0, 0.38, 1.05), "M_Cabinet_Wood", bevel=0.01)  # newel post
    st.to_object("Stairs", root)

    tape = MeshBuilder()
    for z0, z1 in ((0.55, 1.15), (1.15, 0.55)):
        length = math.hypot(0.92, z1 - z0)
        ang = math.atan2(z1 - z0, 0.92)
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        bmesh.ops.scale(tmp, vec=(length, 0.002, 0.05), verts=tmp.verts)
        bmesh.ops.rotate(tmp, cent=(0, 0, 0), matrix=Matrix.Rotation(-ang, 3, "Y"), verts=tmp.verts)
        bmesh.ops.translate(tmp, vec=(0.52, 0.55, (z0 + z1) / 2), verts=tmp.verts)
        tape._merge(tmp, "M_Tape")
    tape.box((0.37, 0.535, 0.79), (0.67, 0.541, 0.91), "M_Paper")  # sign card
    tape.to_object("StairTape", root)

    txt = bpy.data.curves.new("SignText", type="FONT")
    txt.body = "UPSTAIRS\nNOT STAGED"
    txt.align_x = "CENTER"
    txt.align_y = "CENTER"
    txt.size = 0.032
    txt.extrude = 0.0005
    tobj = bpy.data.objects.new("SignTextObj", txt)
    C.link(tobj)
    tobj.location = (0.52, 0.534, 0.85)
    tobj.rotation_euler = (math.pi / 2, 0, 0)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tobj.evaluated_get(dg), depsgraph=dg)
    me.transform(tobj.matrix_world)
    bpy.data.objects.remove(tobj)
    me.materials.append(C.material("M_Ink", PREVIEW_COL["M_Ink"], roughness=0.8))
    C.new_mesh_object("StairSign", me, parent=root)

    col = MeshBuilder()
    col.box((0.06, 0.6, 0), (0.98, 0.7, CEIL), "M_Glass")
    col.to_object("COL_StairsBlock", root)


def build_furniture(root):
    def obj_from(name, fill):
        mb = MeshBuilder()
        fill(mb)
        return mb.to_object(name, root)

    # --- foyer / guard post
    def guard_table(mb):
        mb.box((1.75, 2.9, 0.71), (2.65, 3.5, 0.74), "M_Door_Paint", bevel=0.006)
        for x in (1.8, 2.6):
            for y in (2.95, 3.45):
                mb.cylinder((x, y, 0.355), 0.012, 0.71, "M_Metal", segments=10)
        mb.box((2.42, 3.32, 0.74), (2.58, 3.48, 0.76), "M_Metal")         # lamp base
        mb.cylinder((2.5, 3.4, 0.93), 0.008, 0.36, "M_Metal", segments=8)  # lamp stem
        mb.cylinder((2.5, 3.4, 1.12), 0.09, 0.14, "M_Lampshade", radius_top=0.06, caps=False)
    obj_from("GuardTable", guard_table)

    def folding_chair(mb):
        mb.box((2.85, 2.6, 0.44), (3.25, 3.0, 0.47), "M_Metal", rot_z=math.radians(-20), pivot=(3.05, 2.8, 0))
        mb.box((2.85, 2.98, 0.47), (3.25, 3.0, 0.88), "M_Metal", rot_z=math.radians(-20), pivot=(3.05, 2.8, 0))
    obj_from("FoldingChair", folding_chair)

    def clipboard(mb):
        mb.box((2.0, 3.0, 0.74), (2.23, 3.32, 0.746), "M_Cabinet_Wood", rot_z=math.radians(8), pivot=(2.11, 3.16, 0))
        mb.box((2.01, 3.02, 0.746), (2.22, 3.28, 0.748), "M_Paper", rot_z=math.radians(8), pivot=(2.11, 3.16, 0))
        mb.box((2.07, 3.27, 0.746), (2.15, 3.31, 0.756), "M_Metal", rot_z=math.radians(8), pivot=(2.11, 3.16, 0))
    obj_from("INT_Clipboard", clipboard)

    # --- living room
    def sofa(mb):
        mb.box((4.0, 0.45, 0.0), (6.0, 1.35, 0.30), "M_Fabric_Sofa", bevel=0.03)
        mb.box((4.0, 0.25, 0.0), (6.0, 0.47, 0.85), "M_Fabric_Sofa", bevel=0.05)
        for x0 in (3.8, 6.0):
            mb.box((x0, 0.25, 0.0), (x0 + 0.2, 1.35, 0.62), "M_Fabric_Sofa", bevel=0.05)
        for i in range(3):
            x0 = 4.02 + i * 0.66
            mb.box((x0, 0.5, 0.30), (x0 + 0.64, 1.33, 0.45), "M_Fabric_Sofa", bevel=0.05)
            mb.box((x0 + 0.02, 0.48, 0.45), (x0 + 0.62, 0.66, 0.85), "M_Fabric_Sofa", bevel=0.06)
    obj_from("Sofa", sofa)

    def armchair(mb):
        cx, cy = 7.3, 3.8
        yaw = yaw_for((-1, -0.5))
        piv = (cx, cy, 0)
        # local: seat centred on the anchor, back behind (+Y), front toward -Y
        parts = [((cx - 0.38, cy - 0.40, 0.0), (cx + 0.38, cy + 0.42, 0.30)),          # base
                 ((cx - 0.30, cy - 0.38, 0.30), (cx + 0.30, cy + 0.32, SEAT_ARMCHAIR)),  # seat cushion
                 ((cx - 0.38, cy + 0.26, 0.0), (cx + 0.38, cy + 0.46, 1.0)),           # back
                 ((cx - 0.46, cy - 0.40, 0.0), (cx - 0.30, cy + 0.46, 0.64)),          # arm L
                 ((cx + 0.30, cy - 0.40, 0.0), (cx + 0.46, cy + 0.46, 0.64))]          # arm R
        for lo, hi in parts:
            mb.box(lo, hi, "M_Fabric_Sofa", rot_z=yaw, pivot=piv, bevel=0.04)
    obj_from("Armchair", armchair)

    def coffee_table(mb):
        mb.box((4.65, 2.0, 0.38), (5.75, 2.6, 0.42), "M_Cabinet_Wood", bevel=0.006)
        for x in (4.7, 5.66):
            for y in (2.05, 2.51):
                mb.box((x, y, 0), (x + 0.04, y + 0.04, 0.38), "M_Cabinet_Wood")
    obj_from("CoffeeTable", coffee_table)
    obj_from("Rug", lambda mb: mb.box((4.0, 1.4, 0.0), (6.4, 3.1, 0.012), "M_Rug", bevel=0.004))

    def floor_lamp(mb):
        mb.cylinder((7.6, 0.6, 0.015), 0.16, 0.03, "M_Metal")
        mb.cylinder((7.6, 0.6, 0.8), 0.012, 1.5, "M_Metal", segments=10)
        mb.cylinder((7.6, 0.6, 1.5), 0.22, 0.3, "M_Lampshade", radius_top=0.16, caps=False)
    obj_from("INT_LivingLamp", floor_lamp)

    def photo(name, centre, facing):
        mb = MeshBuilder()
        cx, cy, cz = centre
        fx, fy = facing
        if fx:  # on a wall facing +/-X
            mb.box((cx - 0.015, cy - 0.25, cz - 0.2), (cx + 0.015, cy + 0.25, cz + 0.2), "M_Cabinet_Wood", bevel=0.004)
        else:
            mb.box((cx - 0.25, cy - 0.015, cz - 0.2), (cx + 0.25, cy + 0.015, cz + 0.2), "M_Cabinet_Wood", bevel=0.004)
        obj = mb.to_object(name, root)
        # photo plane with 0..1 UVs, facing the room
        if fx:
            x = cx + fx * 0.017
            verts = [(x, cy - 0.2, cz - 0.15), (x, cy + 0.2, cz - 0.15), (x, cy + 0.2, cz + 0.15), (x, cy - 0.2, cz + 0.15)]
            if fx < 0:
                verts = [verts[1], verts[0], verts[3], verts[2]]
        else:
            y = cy + fy * 0.017
            verts = [(cx + 0.2, y, cz - 0.15), (cx - 0.2, y, cz - 0.15), (cx - 0.2, y, cz + 0.15), (cx + 0.2, y, cz + 0.15)]
            if fy > 0:
                verts = [verts[1], verts[0], verts[3], verts[2]]
        me = bpy.data.meshes.new(name + "_Photo")
        me.from_pydata(verts, [], [(0, 1, 2, 3)])
        uv = me.uv_layers.new(name="UVMap")
        for li, (u, v) in zip(range(4), ((0, 0), (1, 0), (1, 1), (0, 1))):
            uv.data[li].uv = (u, v)
        me.materials.append(C.material("M_Photo_Family", PREVIEW_COL["M_Photo_Family"],
                                       image=C.ASSETS / "Textures" / "Photos" / "photo_family.png"
                                       if (C.ASSETS / "Textures" / "Photos" / "photo_family.png").exists() else None))
        C.new_mesh_object(name + "_Photo", me, parent=root)
        return obj
    photo("PhotoFrame_Living", (3.075, 3.8, 1.55), (1, 0))
    photo("PhotoFrame_Study", (10.0, 4.925, 1.5), (0, -1))

    # --- study
    def desk(mb):
        mb.box((9.65, 0.12, 0.72), (10.85, 0.78, 0.75), "M_Cabinet_Wood", bevel=0.006)
        mb.box((9.65, 0.12, 0.0), (9.70, 0.78, 0.72), "M_Cabinet_Wood")
        mb.box((10.45, 0.12, 0.0), (10.85, 0.78, 0.72), "M_Cabinet_Wood", bevel=0.004)
    obj_from("Desk", desk)

    def desk_chair(mb):
        mb.box((10.05, 1.0, 0.44), (10.5, 1.45, 0.48), "M_Fabric_Sofa", bevel=0.02)
        mb.box((10.05, 1.42, 0.48), (10.5, 1.48, 0.95), "M_Fabric_Sofa", bevel=0.02)
        mb.cylinder((10.27, 1.22, 0.22), 0.025, 0.44, "M_Metal", segments=10)
    obj_from("DeskChair", desk_chair)

    def desk_lamp(mb):
        mb.cylinder((10.7, 0.4, 0.76), 0.07, 0.02, "M_Metal")
        mb.cylinder((10.7, 0.4, 0.95), 0.008, 0.38, "M_Metal", segments=8)
        mb.cylinder((10.7, 0.45, 1.13), 0.09, 0.12, "M_Lampshade", radius_top=0.05, caps=False)
    obj_from("INT_StudyLamp", desk_lamp)

    def bookshelf(mb):
        mb.box((11.6, 1.8, 0.0), (11.94, 3.0, 1.9), "M_Cabinet_Wood", bevel=0.004)
        for i in range(5):
            z = 0.1 + i * 0.37
            x = 11.62
            y = 1.85
            k = 0
            while y < 2.9:
                w = 0.025 + 0.02 * ((i * 7 + k * 3) % 4) / 3
                h = 0.22 + 0.06 * ((i * 5 + k) % 3) / 2
                mb.box((x, y, z + 0.02), (x + 0.24, y + w, z + 0.02 + h), "M_Paper" if (i + k) % 3 else "M_Door_Paint")
                y += w + 0.004
                k += 1
    obj_from("Bookshelf", bookshelf)

    # --- kitchen / dining
    def counter(mb):
        mb.box((8.0, 9.39, 0.0), (11.8, 9.94, 0.88), "M_Cabinet_Wood", bevel=0.005)
        mb.box((7.98, 9.36, 0.88), (11.82, 9.94, 0.92), "M_Counter_Marble", bevel=0.004)
        mb.box((9.7, 9.5, 0.80), (10.3, 9.85, 0.921), "M_Metal")  # sink
        mb.cylinder((10.0, 9.88, 1.05), 0.012, 0.26, "M_Metal", segments=10)  # tap
        for x0, x1 in ((8.0, 9.2), (10.8, 11.8)):
            mb.box((x0, 9.59, 1.45), (x1, 9.94, 2.2), "M_Cabinet_Wood", bevel=0.005)
    obj_from("KitchenCounter", counter)

    def fridge(mb):
        mb.box((11.24, 6.0, 0.0), (11.94, 6.8, 1.8), "M_Door_Paint", bevel=0.02)
        mb.box((11.20, 6.70, 0.9), (11.23, 6.74, 1.5), "M_Metal")
    obj_from("Fridge", fridge)

    def dining_table(mb):
        mb.box((4.7, 7.05, 0.70), (6.3, 7.95, 0.74), "M_Cabinet_Wood", bevel=0.006)
        for x in (4.78, 6.18):
            for y in (7.12, 7.84):
                mb.box((x, y, 0), (x + 0.05, y + 0.05, 0.70), "M_Cabinet_Wood")
    obj_from("INT_DiningTable", dining_table)

    def chair(mb, cx, cy, facing):
        yaw = yaw_for(facing)
        piv = (cx, cy, 0)
        mb.box((cx - 0.22, cy - 0.20, SEAT_DINING - 0.04), (cx + 0.22, cy + 0.24, SEAT_DINING), "M_Cabinet_Wood",
               rot_z=yaw, pivot=piv, bevel=0.005)
        mb.box((cx - 0.22, cy + 0.20, SEAT_DINING), (cx + 0.22, cy + 0.24, SEAT_DINING + 0.48), "M_Cabinet_Wood",
               rot_z=yaw, pivot=piv, bevel=0.005)
        for dx in (-0.2, 0.17):
            for dy in (-0.18, 0.2):
                mb.box((cx + dx, cy + dy, 0), (cx + dx + 0.035, cy + dy + 0.035, SEAT_DINING - 0.04), "M_Cabinet_Wood",
                       rot_z=yaw, pivot=piv)
    obj_from("DiningChairs", lambda mb: (chair(mb, 5.5, 6.82, (0, 1)), chair(mb, 5.5, 8.20, (0, -1)),
                                         chair(mb, 4.42, 7.5, (1, 0))))

    def bassinet(mb):
        x0, x1, y0, y1 = 7.08, 7.92, 8.68, 9.12
        mb.box((x0, y0, 0.50), (x1, y1, 0.53), "M_Door_Paint")                 # base
        mb.box((x0 + 0.02, y0 + 0.02, 0.53), (x1 - 0.02, y1 - 0.02, 0.55), "M_Paper")  # mattress
        for lo, hi in (((x0, y0, 0.53), (x1, y0 + 0.02, 0.78)), ((x0, y1 - 0.02, 0.53), (x1, y1, 0.78)),
                       ((x0, y0, 0.53), (x0 + 0.02, y1, 0.78)), ((x1 - 0.02, y0, 0.53), (x1, y1, 0.85))):
            mb.box(lo, hi, "M_Door_Paint", bevel=0.006)
        for x in (x0 + 0.05, x1 - 0.08):
            for y in (y0 + 0.05, y1 - 0.08):
                mb.box((x, y, 0), (x + 0.03, y + 0.03, 0.50), "M_Cabinet_Wood")
    obj_from("Bassinet", bassinet)

    def wall_clock(mb):
        mb.cylinder((3.08, 8.0, 2.0), 0.15, 0.03, "M_Metal", axis="X", segments=40)
        mb.cylinder((3.096, 8.0, 2.0), 0.135, 0.004, "M_Door_Paint", axis="X", segments=40)
        mb.box((3.099, 7.995, 2.0), (3.102, 8.005, 2.1), "M_Ink")
        mb.box((3.103, 7.995, 1.94), (3.106, 8.005, 2.0), "M_Ink", rot_z=0.0)
    obj_from("WallClock", wall_clock)

    def pendant(mb):
        mb.cylinder((5.5, 7.5, 2.37), 0.004, 0.46, "M_Ink", segments=6)
        mb.cylinder((5.5, 7.5, 2.1), 0.03, 0.08, "M_Metal", radius_top=0.2 * 0 + 0.03)
        mb.cylinder((5.5, 7.5, 2.02), 0.24, 0.14, "M_Lampshade", radius_top=0.05, caps=False)
    obj_from("KitchenPendant", pendant)

    def bench(mb):
        mb.box((0.08, 5.0, 0.42), (0.43, 6.2, 0.46), "M_Cabinet_Wood", bevel=0.005)
        for y in (5.05, 6.1):
            mb.box((0.1, y, 0), (0.41, y + 0.05, 0.42), "M_Cabinet_Wood")
    obj_from("HallBench", bench)


def build_frames(root):
    """Unlit timber house frames out in the half-built estate, seen through the windows."""
    mb = MeshBuilder()
    for ox, oy, w, d in ((4.0, -30.0, 10, 8), (-18.0, -24.0, 9, 7), (10.0, 36.0, 11, 8), (-26.0, 8.0, 8, 9)):
        for (x0, y0, x1, y1) in ((ox, oy, ox + w, oy), (ox, oy + d, ox + w, oy + d), (ox, oy, ox, oy + d), (ox + w, oy, ox + w, oy + d)):
            length = math.hypot(x1 - x0, y1 - y0)
            n = int(length / 0.6)
            for i in range(n + 1):
                t = i / max(n, 1)
                x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
                mb.box((x - 0.025, y - 0.025, 0), (x + 0.025, y + 0.025, 2.6), "M_Cabinet_Wood")
            for z in (0.0, 2.55):
                mb.box((min(x0, x1) - 0.03, min(y0, y1) - 0.03, z), (max(x0, x1) + 0.03, max(y0, y1) + 0.03, z + 0.05), "M_Cabinet_Wood")
    mb.to_object("EstateFrames", root)


def build_markers(root):
    for name, (pos, facing) in ANCHORS.items():
        e = C.new_empty(name, parent=root, location=pos)
        e.rotation_euler = (0, 0, yaw_for(facing))
    for name, pos in (("Light_FoyerLamp", (2.5, 3.4, 1.08)), ("Light_LivingLamp", (7.6, 0.6, 1.45)),
                      ("Light_StudyLamp", (10.7, 0.45, 1.10)), ("Light_KitchenPendant", (5.5, 7.5, 1.98)),
                      ("SND_Fridge", (11.6, 6.4, 1.85)), ("SND_Clock", (3.1, 8.0, 2.0))):
        C.new_empty(name, parent=root, location=pos)


def build_house():
    C.reset_scene()
    root = C.new_empty("ModelHome_House")
    build_shell(root)
    build_windows_doors(root)
    build_stairs(root)
    build_furniture(root)
    build_frames(root)
    build_markers(root)
    for o in root.children_recursive:
        if o.type == "MESH":
            for p in o.data.polygons:
                p.use_smooth = False
    return root


def preview(root):
    scene = bpy.context.scene
    lights = []
    for pos, energy in (((1.5, 2.0, 2.3), 60), ((5.5, 2.5, 2.3), 120), ((10.2, 2.5, 2.3), 80),
                        ((7.5, 7.5, 2.3), 150), ((1.5, 7.0, 2.3), 50)):
        ld = bpy.data.lights.new("P", "POINT")
        ld.energy = energy
        lo = bpy.data.objects.new("P", ld)
        C.link(lo)
        lo.location = pos
        lights.append(lo)
    ceiling = bpy.data.objects.get("Ceiling")
    ceiling.hide_render = True
    C.studio_render(C.OUT / "house_top.png", (6.0, 5.0, 16.0), (6.0, 5.0, 0.0), lens=28, res=(900, 760), key_energy=0)
    ceiling.hide_render = False
    C.studio_render(C.OUT / "house_foyer.png", (2.6, 1.0, 1.62), (6.0, 3.5, 1.0), lens=24, res=(800, 500), key_energy=0)
    C.studio_render(C.OUT / "house_kitchen.png", (5.5, 4.2, 1.62), (6.5, 9.0, 0.9), lens=24, res=(800, 500), key_energy=0)
    for lo in lights:
        bpy.data.objects.remove(lo)
    del scene


def main():
    args = C.script_args()
    out = pathlib.Path(args[args.index("--out") + 1]) if "--out" in args else \
        C.ASSETS / "Models" / "House" / "ModelHome_House.fbx"
    if not out.is_absolute():
        out = C.ROOT / out
    root = build_house()
    C.export_fbx(root, out)
    print(f"exported {out.name}")
    if "--no-preview" not in args:
        preview(root)


if __name__ == "__main__":
    main()
