"""Parametric display-figure generator for Model Home (plan Task 4).

Run:
  Blender --background --factory-startup --python Tools/blender/figure_gen.py -- \
      --variant all --out Assets/ModelHome/Models/Figures [--no-preview]

Figure space: origin on the floor, figure FRONT = -Y, up = +Z, figure's left = +X.
Object/blend-shape names are the contract with Unity (see the plan's Global Constraints).
"""
import math
import pathlib
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import common as C  # noqa: E402

# ---------------------------------------------------------------- figure table

FIGURES = {
    "Dad": dict(height=1.83, head_ratio=7.5, build=0.46, iris="GreyBlue", hair="short",
                skin="#E2C8B6", hair_col="#3B2E26", top="M_Cloth_Shirt", sleeve=0.92,
                bottom="M_Cloth_Trousers", female=False),
    "Mom": dict(height=1.68, head_ratio=7.5, build=0.38, iris="Brown", hair="bun",
                skin="#E8D3C4", hair_col="#8C7355", top="M_Cloth_Knit", sleeve=0.92,
                bottom="M_Cloth_Trousers", female=True),
    "Daughter": dict(height=1.32, head_ratio=6.0, build=0.30, iris="BlueGrey", hair="bob",
                     skin="#E8D3C4", hair_col="#6B5440", top="M_Cloth_Tee", sleeve=0.45,
                     bottom=None, pinafore="M_Cloth_Denim", female=True, child=True),
    "Baby": dict(height=0.62, head_ratio=4.0, build=0.17, iris="DarkGrey", hair=None,
                 skin="#F0DCD0", hair_col=None, top="M_Cloth_Tee", sleeve=0.9,
                 bottom=None, onesie=True, female=False, baby=True),
}
VARIANTS = {  # variant -> (figure, pose, seat height)
    "Dad_Seated": ("Dad", "seated_lap", 0.42),
    "Dad_Standing": ("Dad", "standing", None),
    "Mom_Standing": ("Mom", "standing_mug", None),
    "Daughter_Seated": ("Daughter", "seated_table", 0.45),
    "Daughter_Standing": ("Daughter", "standing", None),
    "Baby_Lying": ("Baby", "lying", None),
}
CLOTH = {"M_Cloth_Shirt": "#AFC4D9", "M_Cloth_Knit": "#E8E0D0", "M_Cloth_Trousers": "#3A3D42",
         "M_Cloth_Tee": "#F2F2F0", "M_Cloth_Denim": "#4A6386", "M_Cloth_Blanket": "#F4F1EA"}

# reference standing skeleton (1.75 m adult), torso chain only; limbs come from lengths
REF_TORSO = {"pelvis": (0.0, 0.000, 0.95), "spine": (0.0, 0.005, 1.10), "chest": (0.0, 0.000, 1.28),
             "upper": (0.0, 0.010, 1.40), "neck": (0.0, 0.020, 1.47), "head_base": (0.0, 0.025, 1.53)}
REF_HEAD_BASE = 1.53
NECK_LIFT = 0.08          # NeckLong = 100 lifts the neck top by this much (contract)

# head geometry at scale 1 (head-local: origin = head_base, front -Y, up +Z)
HEAD_C = Vector((0.0, -0.010, 0.100))
HEAD_AXES = (0.075, 0.095, 0.112)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- skeleton / pose

def two_bone_ik(a, target, l1, l2, pole):
    d = target - a
    dist = min(max(d.length, 1e-4), (l1 + l2) * 0.995)
    n = d.normalized()
    cos_a = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
    sin_a = math.sqrt(max(0.0, 1 - cos_a * cos_a))
    p = pole - n * pole.dot(n)
    p = p.normalized() if p.length > 1e-6 else Vector((0, 1, 0))
    elbow = a + n * (l1 * cos_a) + p * (l1 * sin_a)
    return elbow, a + n * dist


def proportions(fig):
    f = FIGURES[fig]
    s_head = (f["height"] / f["head_ratio"]) / 0.22
    hb = f["height"] - 0.212 * s_head
    kh = hb / REF_HEAD_BASE
    kw = f["build"] / 0.40
    return f, s_head, kh, kw


def skeleton(fig, pose, seat_h):
    f, s_head, kh, kw = proportions(fig)
    L = dict(thigh=0.43 * kh, shin=0.42 * kh, foot=0.16 * kh, upper=0.29 * kh, fore=0.25 * kh, hand=0.18 * kh)
    hip_x = 0.09 * kw * (1.08 if f.get("female") and not f.get("child") else 1.0)
    sh_x = f["build"] * 0.45
    rel = {k: Vector((0, v[1] * kh, (v[2] - 0.95) * kh)) for k, v in REF_TORSO.items()}
    thigh_r = 0.076 * (0.6 * kw + 0.4 * kh) * (1.3 if f.get("baby") else 1.0)

    if pose.startswith("seated"):
        hips_z = seat_h + thigh_r * 0.85
        pelvis = Vector((0, 0.03, hips_z + 0.03 * kh))
        lean = -8.0 if pose == "seated_lap" else 4.0
    else:
        pelvis = Vector((0, 0, 0.95 * kh))
        lean = 0.0
    R = Matrix.Rotation(math.radians(lean), 3, "X")
    J = {k: pelvis + R @ v for k, v in rel.items()}

    for side, sx in (("L", 1), ("R", -1)):
        J[f"clav_{side}"] = pelvis + R @ Vector((sh_x * 0.45 * sx, 0.012 * kh, 0.475 * kh))
        J[f"shoulder_{side}"] = pelvis + R @ Vector((sh_x * sx, 0.010 * kh, 0.48 * kh))
        hip = pelvis + Vector((hip_x * sx, 0, -0.03 * kh))
        J[f"hip_{side}"] = hip

        # legs
        if pose.startswith("seated"):
            knee = hip + Vector((0.025 * sx, -L["thigh"] * 0.98, 0.0))
            ankle_h = 0.075 * kh
            drop = knee.z - ankle_h
            if drop >= L["shin"]:
                ankle = knee + Vector((0, 0.02, -L["shin"]))
            else:
                ankle = knee + Vector((0, -math.sqrt(L["shin"] ** 2 - drop ** 2), -drop))
        else:
            knee = hip + Vector((0.006 * sx, -0.012 * kh, -L["thigh"]))
            ankle = knee + Vector((0.0, 0.02 * kh, -L["shin"]))
        J[f"knee_{side}"] = knee
        J[f"ankle_{side}"] = ankle
        toe_drop = min(ankle.z - 0.022 * kh, L["foot"] * 0.55)
        J[f"toe_{side}"] = ankle + Vector((0.012 * sx, -math.sqrt(max(L["foot"] ** 2 - toe_drop ** 2, 1e-6)), -toe_drop))
        J[f"heel_{side}"] = ankle + Vector((0, 0.035 * kh, -min(0.045 * kh, ankle.z - 0.02 * kh)))

        # arms
        sh = J[f"shoulder_{side}"]
        flat_hand = False
        if pose == "standing":
            target = sh + Vector((0.04 * sx, 0.035 * kh, -(L["upper"] + L["fore"]) * 0.965))
            pole = Vector((0.3 * sx, 1.0, 0.0))
        elif pose == "standing_mug" and side == "R":
            target = J["chest"] + Vector((-0.07 * kw, -0.26 * kh, -0.07 * kh))
            pole = Vector((-1.0, 0.4, -1.0))
        elif pose == "standing_mug":  # left hand resting on the counter top (0.92 m)
            target = Vector((0.20, -0.33, 0.955))
            pole = Vector((1.0, 1.0, -0.3))
            flat_hand = True
        elif pose == "seated_lap":
            target = Vector((0.13 * sx, J[f"hip_{side}"].y - L["thigh"] * 0.5, J[f"hip_{side}"].z + thigh_r + 0.03))
            pole = Vector((0.5 * sx, 1.0, -0.3))
            flat_hand = True
        elif pose == "seated_table":  # forearms on the dining table (top 0.74 m)
            target = Vector((0.10 * sx, -0.32, 0.775))
            pole = Vector((0.7 * sx, 0.3, -1.0))
            flat_hand = True
        else:  # lying (built upright): hands folded on the chest
            target = J["chest"] + Vector((0.05 * sx, -0.09 * kh, -0.03 * kh))
            pole = Vector((1.0 * sx, 0.3, -0.6))
        elbow, wrist = two_bone_ik(sh, target, L["upper"], L["fore"], pole)
        J[f"elbow_{side}"] = elbow
        J[f"wrist_{side}"] = wrist
        fdir = (wrist - elbow).normalized()
        if flat_hand:
            fdir = Vector((fdir.x, fdir.y, min(fdir.z, 0.0) * 0.3)).normalized()
        J[f"hand_{side}"] = wrist + fdir * L["hand"] * 0.5
        J[f"tip_{side}"] = wrist + fdir * L["hand"]
    return J, dict(kh=kh, kw=kw, s_head=s_head, L=L)


# bones: (a, b, [(fraction, (rx, ry)), ...] including endpoints at 0 and 1), label
def bone_table(f, kh, kw):
    fem = f.get("female") and not f.get("child")
    tw = kw  # torso width scale
    td = 0.5 * (kw + kh)
    limb = 0.6 * kw + 0.4 * kh
    if f.get("baby"):
        limb *= 1.35
        td *= 1.25
        tw *= 1.15
    T = lambda rx, ry: (rx * tw, ry * td)  # noqa: E731
    Lm = lambda rx, ry: (rx * limb, ry * limb)  # noqa: E731
    bones = [
        ("pelvis", "spine", [(0, T(0.165 if fem else 0.155, 0.11 if fem else 0.105)), (1, T(0.115 if fem else 0.128, 0.087 if fem else 0.092))], "torso"),
        ("spine", "chest", [(0.5, T(0.13 if fem else 0.138, 0.095)), (1, T(0.135 if fem else 0.148, 0.108 if fem else 0.102))], "torso"),
        ("chest", "upper", [(1, T(0.13 if fem else 0.142, 0.085))], "torso"),
        ("upper", "neck", [(1, Lm(0.052, 0.054))], "neck"),
        ("neck", "head_base", [(1, Lm(0.048, 0.05))], "neck"),
    ]
    for s in ("L", "R"):
        bones += [
            ("upper", f"clav_{s}", [(1, Lm(0.06, 0.058))], "torso"),
            (f"clav_{s}", f"shoulder_{s}", [(1, Lm(0.056, 0.056))], "torso"),
            (f"shoulder_{s}", f"elbow_{s}", [(0.4, Lm(0.047, 0.049)), (1, Lm(0.036, 0.036))], f"upperarm_{s}"),
            (f"elbow_{s}", f"wrist_{s}", [(0.3, Lm(0.039, 0.036)), (1, Lm(0.027, 0.021))], f"forearm_{s}"),
            (f"wrist_{s}", f"hand_{s}", [(1, Lm(0.041, 0.016))], f"hand_{s}"),
            (f"hand_{s}", f"tip_{s}", [(1, Lm(0.03, 0.011))], f"hand_{s}"),
            ("pelvis", f"hip_{s}", [(1, Lm(0.088, 0.09))], "pelvis"),
            (f"hip_{s}", f"knee_{s}", [(0.35, Lm(0.077, 0.081)), (1, Lm(0.051, 0.054))], f"thigh_{s}"),
            (f"knee_{s}", f"ankle_{s}", [(0.3, Lm(0.054, 0.058)), (1, Lm(0.031, 0.034))], f"shin_{s}"),
            (f"ankle_{s}", f"toe_{s}", [(0.5, Lm(0.04, 0.026)), (1, Lm(0.033, 0.017))], f"foot_{s}"),
            (f"ankle_{s}", f"heel_{s}", [(1, Lm(0.03, 0.03))], f"foot_{s}"),
        ]
    return bones


def build_body(name, J, f, kh, kw):
    bones = bone_table(f, kh, kw)
    verts, radii, edges = [], [], []
    index = {}

    def vid(joint, r):
        if joint not in index:
            index[joint] = len(verts)
            verts.append(J[joint].copy())
            radii.append(r)
        return index[joint]

    vid("pelvis", bones[0][2][0][1])
    for a, b, samples, _ in bones:
        ia = index[a] if a in index else vid(a, samples[0][1])
        prev = ia
        for frac, r in samples:
            if frac == 0:
                radii[ia] = r
                continue
            if frac == 1:
                cur = vid(b, r)
            else:
                verts.append(J[a].lerp(J[b], frac))
                radii.append(r)
                cur = len(verts) - 1
            edges.append((prev, cur))
            prev = cur

    me = bpy.data.meshes.new(name + "_skel")
    me.from_pydata([tuple(v) for v in verts], edges, [])
    obj = C.new_mesh_object(name, me)
    skin = obj.modifiers.new("Skin", "SKIN")
    skin.branch_smoothing = 0.75
    sv = obj.data.skin_vertices[0].data
    for i, r in enumerate(radii):
        sv[i].radius = r
    sv[0].use_root = True
    sub = obj.modifiers.new("Sub", "SUBSURF")
    sub.levels = sub.render_levels = 2
    C.bake_modifiers(obj)

    voxel = 0.0045 if f.get("baby") else 0.0065
    rm = obj.modifiers.new("Remesh", "REMESH")
    rm.mode = "VOXEL"
    rm.voxel_size = voxel
    sm = obj.modifiers.new("Smooth", "CORRECTIVE_SMOOTH")
    sm.smooth_type = "LENGTH_WEIGHTED"
    sm.use_only_smooth = True
    sm.factor = 0.5
    sm.iterations = 10
    C.bake_modifiers(obj)
    tris = C.tri_count(obj.data)
    if tris > 28_000:
        dec = obj.modifiers.new("Dec", "DECIMATE")
        dec.ratio = 28_000 / tris
        C.bake_modifiers(obj)
    C.shade_smooth(obj.data)
    return obj, bones


# ---------------------------------------------------------------- labels / regions

def segment_labels(points, J, bones):
    """Nearest bone label + fraction along it for every point (numpy, vectorised)."""
    P = np.asarray(points)
    best_d = np.full(len(P), np.inf)
    best_l = np.empty(len(P), dtype=object)
    best_t = np.zeros(len(P))
    for a, b, _, label in bones:
        A = np.array(J[a])
        B = np.array(J[b])
        AB = B - A
        denom = max(float(AB @ AB), 1e-9)
        t = np.clip(((P - A) @ AB) / denom, 0, 1)
        Q = A + t[:, None] * AB
        d = np.linalg.norm(P - Q, axis=1)
        m = d < best_d
        best_d[m], best_l[m], best_t[m] = d[m], label, t[m]
    return best_l, best_t


def clothing_planes(J, f, kh):
    """Cut planes (point, normal) whose zero sets are the garment edges; used both to
    bisect the clothes mesh (clean straight edges) and to decide each face's garment."""
    neck_axis = (J["head_base"] - J["neck"]).normalized()
    planes = {"neckline": (J["neck"] - neck_axis * 0.004 * kh,
                           (neck_axis + Vector((0, -0.25, 0))).normalized()),
              # the neckline only applies within this radius of the neck axis, so the infinite cut
              # plane does not strip cloth from the shoulder tops (which sit near neck height)
              "collar": (J["neck"], neck_axis, 0.095 * kh)}
    long_sleeve = f["sleeve"] > 0.5
    for s in ("L", "R"):
        if long_sleeve:
            a, b = J[f"elbow_{s}"], J[f"wrist_{s}"]
        else:
            a, b = J[f"shoulder_{s}"], J[f"elbow_{s}"]
        planes[f"sleeve_{s}"] = (a.lerp(b, f["sleeve"]), (b - a).normalized())
        k, an = J[f"knee_{s}"], J[f"ankle_{s}"]
        planes[f"shoe_{s}"] = (k.lerp(an, 0.92), (an - k).normalized())
        h = J[f"hip_{s}"]
        planes[f"skirt_{s}"] = (h.lerp(k, 0.5), (k - h).normalized())
    planes["waist"] = (Vector((0, 0, J["spine"].z - 0.04 * kh)), Vector((0, 0, 1)))
    planes["chest"] = (Vector((0, 0, J["chest"].z - 0.02 * kh)), Vector((0, 0, 1)))
    return planes


def in_collar(p, planes):
    """Bare skin at the neck: above the neckline plane AND near the neck axis."""
    co, axis, radius = planes["collar"]
    d = Vector(p) - co
    radial = (d - axis * d.dot(axis)).length
    return radial < radius and above(p, planes["neckline"])


def above(p, plane):
    co, no = plane
    return (Vector(p) - co).dot(no) > 0


def garment_for(p, label, f, planes):
    """Material for a clothes face centred at p with nearest-bone label, or None (bare skin)."""
    side = label[-1] if label[-2:] in ("_L", "_R") else None
    base = label[:-2] if side else label
    if f.get("onesie"):  # baby: footed onesie up to the neckline, hands bare
        if base == "hand":
            return None
        if base in ("neck", "torso"):
            return None if in_collar(p, planes) else f["top"]
        return f["top"]
    if base in ("hand",):
        return None
    if base in ("neck", "torso"):
        if in_collar(p, planes):
            return None
        if f.get("pinafore"):
            return f["pinafore"] if not above(p, planes["chest"]) else f["top"]
        return f["bottom"] if not above(p, planes["waist"]) else f["top"]
    if base == "upperarm":
        return f["top"] if (f["sleeve"] > 0.5 or not above(p, planes[f"sleeve_{side}"])) else None
    if base == "forearm":
        return f["top"] if (f["sleeve"] > 0.5 and not above(p, planes[f"sleeve_{side}"])) else None
    if base == "pelvis":
        return f.get("pinafore") or f["bottom"]
    if base == "thigh":
        if f.get("pinafore"):
            return f["pinafore"] if not above(p, planes[f"skirt_{side}"]) else None
        return f["bottom"]
    if base == "shin":
        if above(p, planes[f"shoe_{side}"]):
            return "M_Cloth_Trousers"  # shoes
        return f["bottom"]
    if base == "foot":
        return "M_Cloth_Trousers"  # shoes
    return None


def build_clothes(body, J, bones, f, kh):
    me = body.data.copy()
    me.materials.clear()
    obj = C.new_mesh_object("Clothes", me)
    planes = clothing_planes(J, f, kh)
    bm = bmesh.new()
    bm.from_mesh(me)
    for plane in planes.values():
        if len(plane) != 2:
            continue
        co, no = plane
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-5,
                               plane_co=co, plane_no=no, clear_inner=False, clear_outer=False)
    bm.faces.ensure_lookup_table()
    centres = [fc.calc_center_median() for fc in bm.faces]
    labels, _ = segment_labels([tuple(c) for c in centres], J, bones)
    gmat = [garment_for(c, labels[i], f, planes) for i, c in enumerate(centres)]
    mats = sorted({m for m in gmat if m})
    for m in mats:
        me.materials.append(C.material(m, CLOTH.get(m, "#888888"), roughness=0.85))
    kill = []
    for i, fc in enumerate(bm.faces):
        if gmat[i] is None:
            kill.append(fc)
        else:
            fc.material_index = mats.index(gmat[i])
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.normal_update()
    rng = np.random.default_rng(7)
    phase = rng.uniform(0, 6.28, 3)
    for v in bm.verts:
        p = v.co
        fold = 0.0015 * (math.sin(p.z * 38 + phase[0]) * math.sin(p.x * 21 + phase[1]) + math.sin(p.y * 29 + phase[2]))
        v.co = p + v.normal * (0.0055 + fold)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    sol = obj.modifiers.new("Thick", "SOLIDIFY")
    sol.thickness = 0.003
    sol.offset = -1.0
    C.bake_modifiers(obj)
    C.shade_smooth(obj.data)
    return obj


def box_uvs(me, tile=0.4):
    uv = me.uv_layers.new(name="UVMap") if not me.uv_layers else me.uv_layers[0]
    for poly in me.polygons:
        n = poly.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        a, b = [(1, 2), (0, 2), (0, 1)][ax]
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (p[a] / tile, p[b] / tile)


# ---------------------------------------------------------------- shape keys (body/clothes)

def add_body_keys(obj, J, bones, kh):
    me = obj.data
    co = np.array([v.co for v in me.vertices])
    labels, _ = segment_labels(co, J, bones)
    obj.shape_key_add(name="Basis")
    # Breathe: torso vertices pushed out from the spine axis around chest / upper belly
    A, B = np.array(J["pelvis"]), np.array(J["upper"])
    AB = B - A
    t = np.clip(((co - A) @ AB) / (AB @ AB), 0, 1)
    radial = co - (A + t[:, None] * AB)
    radial /= np.maximum(np.linalg.norm(radial, axis=1), 1e-6)[:, None]
    centre = np.array(J["chest"].lerp(J["spine"], 0.35))
    sigma = 0.14 * kh
    w = np.exp(-np.sum((co - centre) ** 2, axis=1) / (2 * sigma * sigma))
    torso = np.array([lab in ("torso",) for lab in labels])
    d_breathe = radial * (0.012 * kh * w * torso)[:, None]
    k = obj.shape_key_add(name="Breathe")
    k.data.foreach_set("co", (co + d_breathe).ravel())
    k.value = 0.0  # Blender 5 defaults new keys to 1.0
    # NeckLong: neck vertices lifted along the neck axis, 0 at the neck base -> NECK_LIFT at head_base
    N0, N1 = np.array(J["neck"]), np.array(J["head_base"])
    ax = N1 - N0
    ln = np.linalg.norm(ax)
    ax_n = ax / ln
    tn = np.clip(((co - N0) @ ax_n) / ln, 0, 1)
    dist_axis = np.linalg.norm((co - N0) - np.outer((co - N0) @ ax_n, ax_n), axis=1)
    near = (dist_axis < 0.12 * kh) & (((co - N0) @ ax_n) > -0.01)
    neckish = np.array([lab in ("neck", "torso") for lab in labels]) & near
    d_neck = np.outer(tn * neckish, ax_n) * NECK_LIFT
    k = obj.shape_key_add(name="NeckLong")
    k.data.foreach_set("co", (co + d_neck).ravel())
    k.value = 0.0  # Blender 5 defaults new keys to 1.0


# ---------------------------------------------------------------- head

FACE = [  # (x, z(rel. centre), sx, sz, amplitude, mirror)
    (0.032, 0.008, 0.021, 0.0082, -0.0105, True),   # eye socket
    (0.031, 0.027, 0.026, 0.0075, 0.0050, True),    # brow ridge
    (0.000, 0.022, 0.011, 0.010, 0.0020, False),    # glabella
    (0.000, 0.002, 0.0065, 0.017, 0.0080, False),   # nose bridge
    (0.000, -0.020, 0.0080, 0.012, 0.0115, False),  # nose mid
    (0.000, -0.036, 0.0090, 0.008, 0.0165, False),  # nose tip
    (0.014, -0.040, 0.0075, 0.0065, 0.0075, True),  # nostril wing
    (0.000, -0.0495, 0.0055, 0.004, -0.0018, False),  # philtrum
    (0.000, -0.0555, 0.019, 0.0048, 0.0065, False),  # upper lip
    (0.000, -0.0615, 0.018, 0.0024, -0.0022, False),  # mouth line
    (0.000, -0.0680, 0.016, 0.0050, 0.0065, False),  # lower lip
    (0.000, -0.0785, 0.013, 0.0040, -0.0020, False),  # mentolabial groove
    (0.000, -0.0920, 0.016, 0.0120, 0.0080, False),  # chin
    (0.048, -0.010, 0.020, 0.016, 0.0065, True),    # cheekbone
    (0.044, -0.052, 0.020, 0.020, -0.0030, True),   # cheek hollow
]
MOUTH_CORNER = (0.024, -0.0615)


def face_params(f):
    if f.get("baby"):
        return dict(fs=0.82, dz=-0.016, nose=0.55, eye_r=0.0125, jaw=0.06, cranium=0.08, cheek=0.5)
    if f.get("child"):
        return dict(fs=0.93, dz=-0.007, nose=0.8, eye_r=0.012, jaw=0.12, cranium=0.06, cheek=0.6)
    return dict(fs=1.0, dz=0.0, nose=1.0, eye_r=0.012, jaw=0.19, cranium=0.045, cheek=0.7)


def head_point(n, fp, socket=True):
    """Unit-sphere direction n -> head-local point (scale 1)."""
    a, b, c = HEAD_AXES
    nx, ny, nz = n
    p = np.array([a * nx, b * ny, c * nz])
    nrm = np.array([nx / a, ny / b, nz / c])
    nrm /= np.linalg.norm(nrm)
    zr = p[2]
    # face flattening, rounder back, narrower jaw and nape
    p[1] *= 1 - 0.07 * max(-ny, 0)
    if ny > 0 and zr > -0.02:
        p[1] *= 1.05
    p[0] *= 1 - fp["jaw"] * float(smoothstep(0.035, 0.11, -zr))    # jaw / chin taper
    p[0] *= 1 + fp["cranium"] * float(smoothstep(-0.01, 0.06, zr))  # broader cranium
    front = float(smoothstep(0.15, 0.55, -ny))
    disp = 0.0
    fs, dz = fp["fs"], fp["dz"]
    for x0, z0, sx, sz, amp, mirror in FACE:
        if not socket and amp < 0 and sx > 0.015 and z0 > 0:
            continue
        if x0 in (0.0, ) and z0 < 0.0 and z0 > -0.045:
            amp *= fp["nose"]
        if x0 >= 0.044:  # cheekbones / cheek hollows
            amp *= fp["cheek"]
        zz = z0 * fs + dz
        for xx in ((x0 * fs, -x0 * fs) if mirror else (x0 * fs,)):
            disp += amp * math.exp(-((p[0] - xx) ** 2) / (2 * (sx * fs) ** 2) - ((zr - zz) ** 2) / (2 * (sz * fs) ** 2))
    disp *= front
    # ears (side features)
    side = float(smoothstep(0.55, 0.85, abs(nx)))
    ear = 0.011 * math.exp(-((p[1] - 0.008) ** 2) / (2 * 0.011 ** 2) - ((zr - (-0.004 * fs + dz)) ** 2) / (2 * 0.024 ** 2))
    disp += ear * side
    temple = -0.004 * math.exp(-((p[1] + 0.035) ** 2) / (2 * 0.02 ** 2) - ((zr - 0.03) ** 2) / (2 * 0.025 ** 2)) * side
    disp += temple
    return p + nrm * disp + np.array(HEAD_C), nrm


def build_head(f, s, fp, skin_mat):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=192, v_segments=144, radius=1.0)
    for v in bm.verts:
        n = v.co.normalized()
        p, _ = head_point((n.x, n.y, n.z), fp)
        v.co = Vector(p) * s
    me = bpy.data.meshes.new("Head")
    bm.to_mesh(me)
    bm.free()
    head = C.new_mesh_object("Head", me)
    me.materials.append(skin_mat)
    dec = head.modifiers.new("Dec", "DECIMATE")
    dec.ratio = 11_000 / C.tri_count(me)
    dec.use_symmetry = True
    dec.symmetry_axis = "X"
    C.bake_modifiers(head)
    C.shade_smooth(head.data)
    # SmileWide: corners pulled out and up, lips stretched thin
    co = np.array([v.co for v in head.data.vertices]) / s
    head.shape_key_add(name="Basis")
    cx, cz = MOUTH_CORNER
    cx *= fp["fs"]
    cz = cz * fp["fs"] + fp["dz"] + HEAD_C.z
    d = np.zeros_like(co)
    front = co[:, 1] < HEAD_C.y - 0.03
    for sgn in (1, -1):
        w = np.exp(-((co[:, 0] - sgn * cx) ** 2) / (2 * 0.016 ** 2) - ((co[:, 2] - cz) ** 2) / (2 * 0.013 ** 2)) * front
        d[:, 0] += sgn * 0.0085 * w
        d[:, 2] += 0.0045 * w
        d[:, 1] += 0.002 * w
    lips = np.exp(-(co[:, 0] ** 2) / (2 * 0.02 ** 2) - ((co[:, 2] - cz) ** 2) / (2 * 0.009 ** 2)) * front
    d[:, 1] += 0.0028 * lips  # flatten (push back) the lips
    k = head.shape_key_add(name="SmileWide")
    k.data.foreach_set("co", ((co + d) * s).ravel())
    k.value = 0.0  # Blender 5 defaults new keys to 1.0
    return head


def surface_y(x, z, fp):
    """Front surface y (scale 1, no socket) at head-local (x, z), by marching directions."""
    best = None
    for ny in np.linspace(-1, -0.2, 160):
        for nx in np.linspace(-0.7, 0.7, 120):
            nz2 = 1 - nx * nx - ny * ny
            if nz2 < 0:
                continue
            for nz in (math.sqrt(nz2), -math.sqrt(nz2)):
                p, _ = head_point((nx, ny, nz), fp, socket=False)
                err = (p[0] - x) ** 2 + (p[2] - z) ** 2
                if best is None or err < best[0]:
                    best = (err, p[1])
    return best[1]


def build_eye(side, centre, r, iris_mat, sclera_mat, cornea_mat, head):
    face_front = Matrix.Rotation(math.radians(90), 4, "X")  # sphere pole (+Z) -> -Y (front)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=r, matrix=face_front)
    eyeball = set(bm.faces)
    r_iris = r * math.sin(math.radians(30))
    # cornea cap: sphere r_c centred dd in front, meeting the eyeball at the iris rim
    rc = r * 0.65
    dd = r * math.cos(math.radians(30)) - math.sqrt(rc * rc - r_iris * r_iris)
    bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=24, radius=rc,
                              matrix=Matrix.Translation((0, -dd, 0)) @ face_front)
    lim = math.degrees(math.asin(r_iris / rc)) + 2
    cap_c = Vector((0, -dd, 0))
    kill = []
    for fc in bm.faces:
        if fc in eyeball:
            continue
        d = fc.calc_center_median() - cap_c
        if math.degrees(math.acos(max(-1, min(1, -d.y / d.length)))) > lim:
            kill.append(fc)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        if face in eyeball:
            c = face.calc_center_median()
            ang = math.degrees(math.acos(max(-1, min(1, -c.y / c.length))))
            face.material_index = 1 if ang < 30 else 0
        else:
            face.material_index = 2
        for loop in face.loops:
            p = loop.vert.co
            loop[uv].uv = (0.5 + p.x / r_iris * 0.46, 0.5 + p.z / r_iris * 0.46) if face.material_index == 1 else (0.02, 0.5)
    me = bpy.data.meshes.new(f"Eye_{side}")
    bm.to_mesh(me)
    bm.free()
    for m in (sclera_mat, iris_mat, cornea_mat):
        me.materials.append(m)
    C.shade_smooth(me)
    return C.new_mesh_object(f"Eye_{side}", me, parent=head, location=centre)


def build_lid(side, centre, r, skin_mat, head):
    """Upper lid band (closes on Blink) + static lower lid band, both shells just outside the eyeball."""
    rl = r * 1.08
    cols, rows = 18, 8
    bands = [  # (e_from, e_open_edge, e_closed_edge)
        (78.0, 27.0, -24.0),    # upper lid: top -> lid edge
        (-76.0, -21.0, -21.0),  # lower lid: bottom -> lid edge (static)
    ]
    verts_open, verts_closed, faces = [], [], []
    for e_from, e_open, e_closed in bands:
        base = len(verts_open)
        for i in range(rows + 1):
            for j in range(cols + 1):
                th = math.radians(-80 + 160 * j / cols)
                for e_end, out in ((e_open, verts_open), (e_closed, verts_closed)):
                    e = math.radians(e_from + (e_end - e_from) * i / rows)
                    out.append((rl * math.sin(th) * math.cos(e), -rl * math.cos(th) * math.cos(e), rl * math.sin(e)))
        band_faces = []
        for i in range(rows):
            for j in range(cols):
                q = base + i * (cols + 1) + j
                band_faces.append((q, q + 1, q + cols + 2, q + cols + 1))
        # outward normals: a face's normal should point away from the eye centre
        va, vb, vc = (Vector(verts_open[k]) for k in band_faces[0][:3])
        if (vb - va).cross(vc - va).dot(va + vb + vc) < 0:
            band_faces = [tuple(reversed(fc)) for fc in band_faces]
        faces += band_faces
    me = bpy.data.meshes.new(f"Lid_{side}")
    me.from_pydata(verts_open, [], faces)
    me.materials.append(skin_mat)
    C.shade_smooth(me)
    obj = C.new_mesh_object(f"Lid_{side}", me, parent=head, location=centre)
    obj.shape_key_add(name="Basis")
    k = obj.shape_key_add(name="Blink")
    k.data.foreach_set("co", np.array(verts_closed).ravel())
    k.value = 0.0  # Blender 5 defaults new keys to 1.0
    return obj


def hair_mask(q, style):
    """Smooth 0..1 coverage for a head-local (scale 1) point q, relative to the head centre."""
    zr = q.z
    ny = q.y / HEAD_AXES[1]
    nx = q.x / HEAD_AXES[0]
    front = float(smoothstep(0.30, 0.62, -ny))
    temple = float(smoothstep(0.5, 0.8, abs(nx))) * float(smoothstep(0.0, 0.3, -ny))
    if style == "short":
        zh = -0.04 + (0.074 + 0.04) * front
        zh = zh + (0.045 - zh) * temple
    elif style == "bun":
        zh = -0.06 + (0.070 + 0.06) * front
        zh = zh + (0.02 - zh) * temple
    else:  # bob with a fringe to the brows, framing the face down to the jaw
        zh = -0.105 + (0.042 + 0.105) * front
    return float(smoothstep(zh - 0.006, zh + 0.006, zr))


def build_hair(style, head, s, fp, hair_mat):
    """Offset shell of a fine head sphere: covered areas sit proud of the skin, the rest dives
    under it, so the hairline is the smooth intersection curve. Fully hidden faces are dropped."""
    if style is None:
        return None
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=160, v_segments=120, radius=1.0)
    for v in bm.verts:
        n = v.co.normalized()
        p, _ = head_point((n.x, n.y, n.z), fp)
        v.co = Vector(p) * s
    bm.normal_update()
    hc = Vector(HEAD_C) * s
    mask = {}
    for v in bm.verts:
        q = (v.co - hc) / s
        m = hair_mask(q, style)
        mask[v] = m
        off = 0.0065
        if style == "bob":
            off += 0.014 * float(smoothstep(0.02, -0.10, q.z))
        ang = math.atan2(q.x, q.y)
        groove = 0.0006 * math.sin(ang * 95 + q.z * 40)
        v.co = v.co + v.normal * ((off + groove) * m - 0.004 * (1 - m)) * s
    hidden = [fc for fc in bm.faces if all(mask[v] < 1e-3 for v in fc.verts)]
    bmesh.ops.delete(bm, geom=hidden, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    if style == "bun":
        bun_c = hc + Vector((0, 0.097, -0.012)) * s
        bmesh.ops.create_uvsphere(bm, u_segments=32, v_segments=20, radius=0.036 * s,
                                  matrix=Matrix.Translation(bun_c))
    me = bpy.data.meshes.new("Hair")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(hair_mat)
    obj = C.new_mesh_object("Hair", me, parent=head)
    C.shade_smooth(obj.data)
    return obj


# ---------------------------------------------------------------- props

def build_prop(variant, J, info, root):
    kh = info["kh"]
    if variant == "Dad_Seated":
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=(0.30, 0.24, 0.007), verts=bm.verts)
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
        for v in bm.verts:
            v.co.z += 0.012 * (v.co.x / 0.15) ** 2  # folded paper sags over the thighs
        me = bpy.data.meshes.new("Prop")
        bm.to_mesh(me)
        bm.free()
        me.materials.append(C.material("M_Newspaper", "#D9D4C7", roughness=0.9))
        lap = (J["hip_L"] + J["hip_R"]) / 2 + Vector((0, -info["L"]["thigh"] * 0.5, 0.085 * kh))
        obj = C.new_mesh_object("Prop", me, parent=root, location=lap)
        obj.rotation_euler = (math.radians(-6), 0, math.radians(3))
        return obj
    if variant == "Mom_Standing":
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=32, radius1=0.04, radius2=0.042, depth=0.095)
        # handle: a torus (major 0.022, minor 0.006) in the XZ plane, half buried in the wall
        ring, tube = 24, 10
        start = len(bm.verts)
        bm.verts.ensure_lookup_table()
        for i in range(ring):
            u = 2 * math.pi * i / ring
            for j in range(tube):
                w = 2 * math.pi * j / tube
                rr = 0.022 + 0.006 * math.cos(w)
                bm.verts.new((0.042 + rr * math.cos(u), 0.006 * math.sin(w), rr * math.sin(u)))
        bm.verts.ensure_lookup_table()
        for i in range(ring):
            for j in range(tube):
                a = start + i * tube + j
                b = start + i * tube + (j + 1) % tube
                c = start + ((i + 1) % ring) * tube + (j + 1) % tube
                d = start + ((i + 1) % ring) * tube + j
                bm.faces.new((bm.verts[a], bm.verts[b], bm.verts[c], bm.verts[d]))
        me = bpy.data.meshes.new("Prop")
        bm.to_mesh(me)
        bm.free()
        me.materials.append(C.material("M_Mug", "#E9E6DF", roughness=0.25))
        C.shade_smooth(me)
        return C.new_mesh_object("Prop", me, parent=root, location=J["hand_R"] + Vector((0.0, -0.035, 0.035)))
    if variant == "Baby_Lying":
        return None  # blanket is built after the lying transform
    return None


def build_blanket(root, body, neck_x):
    xs = [(body.matrix_world @ v.co) for v in body.data.vertices]
    xmin = min(p.x for p in xs)
    zmax = max(p.z for p in xs)
    ymin, ymax = min(p.y for p in xs), max(p.y for p in xs)
    chin_x = neck_x - 0.015
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    sx, sy, sz = (chin_x - xmin) + 0.06, (ymax - ymin) + 0.10, zmax + 0.02
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    bmesh.ops.translate(bm, vec=((chin_x + xmin) / 2 - 0.03, (ymax + ymin) / 2, sz / 2), verts=bm.verts)
    me = bpy.data.meshes.new("Prop")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(C.material("M_Cloth_Blanket", CLOTH["M_Cloth_Blanket"], roughness=0.9))
    obj = C.new_mesh_object("Prop", me, parent=root)
    bev = obj.modifiers.new("Bevel", "BEVEL")
    bev.width = 0.035
    bev.segments = 4
    sub = obj.modifiers.new("Sub", "SUBSURF")
    sub.levels = 2
    C.bake_modifiers(obj)
    C.shade_smooth(obj.data)
    return obj


# ---------------------------------------------------------------- assembly

def build_variant(variant):
    C.reset_scene()
    fig, pose, seat_h = VARIANTS[variant]
    f = FIGURES[fig]
    J, info = skeleton(fig, pose, seat_h)
    kh, kw, s = info["kh"], info["kw"], info["s_head"]
    fp = face_params(f)
    skin_mat = C.material(f"M_Skin_{fig}", f["skin"], roughness=0.45)

    root = C.new_empty(f"Figure_{variant}")
    body, bones = build_body("Body", J, f, kh, kw)
    body.data.materials.append(skin_mat)
    clothes = build_clothes(body, J, bones, f, kh)
    for o in (body, clothes):
        box_uvs(o.data)
        add_body_keys(o, J, bones, kh)
        o.parent = root

    head = build_head(f, s, fp, skin_mat)
    head.location = J["head_base"]
    head.parent = root
    eye_x = 0.032 * fp["fs"] * s
    eye_z = (HEAD_C.z + 0.008 * fp["fs"] + fp["dz"]) * s
    r_eye = fp["eye_r"] * s
    y_surf = surface_y(0.032 * fp["fs"], HEAD_C.z + 0.008 * fp["fs"] + fp["dz"], fp) * s
    ey = y_surf + 0.003 * s + r_eye
    iris_mat = C.material(f"M_Iris_{f['iris']}", "#666666", roughness=0.3,
                          image=C.ASSETS / "Textures" / "Eyes" / f"iris_{f['iris']}.png")
    sclera = C.material("M_Sclera", "#EEEAE2", roughness=0.4)
    cornea = C.material("M_Cornea", "#FFFFFF", roughness=0.03, alpha=0.08)
    for side, sx in (("L", 1), ("R", -1)):
        centre = Vector((eye_x * sx, ey, eye_z))
        build_eye(side, centre, r_eye, iris_mat, sclera, cornea, head)
        build_lid(side, centre, r_eye, skin_mat, head)
    if f["hair"]:
        build_hair(f["hair"], head, s, fp, C.material(f"M_Hair_{fig}", f["hair_col"], roughness=0.3))

    build_prop(variant, J, info, root)

    if pose == "lying":
        # built upright; lay on the back (face up, head toward +X), then sit on z = 0
        M = Matrix.Rotation(math.radians(-90), 4, "Z") @ Matrix.Rotation(math.radians(-90), 4, "X")
        bpy.context.view_layer.update()  # head.matrix_world must reflect head.location first
        for o in (body, clothes):
            o.data.transform(M, shape_keys=True)
        head.matrix_world = M @ head.matrix_world
        bpy.context.view_layer.update()
        zmin = min(v.co.z for v in body.data.vertices)
        T = Matrix.Translation((0, 0, -zmin))
        for o in (body, clothes):
            o.data.transform(T, shape_keys=True)
        head.matrix_world = T @ head.matrix_world
        bpy.context.view_layer.update()
        build_blanket(root, body, (T @ M @ J["neck"]).x)
    return root


def preview(variant, root):
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in root.children_recursive if o.type == "MESH" for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    centre = (lo + hi) / 2
    size = max(hi - lo)
    eyes = [o for o in root.children_recursive if o.name.startswith("Eye_")]
    eye_mid = (eyes[0].matrix_world.translation + eyes[1].matrix_world.translation) / 2
    if variant.endswith("Lying"):
        cam_face = eye_mid + Vector((0.0, -0.12, 0.45))
        cam_full = centre + Vector((0.5, -0.9, 0.8))
    else:
        fwd = Matrix.Rotation(math.radians(-25), 3, "Z") @ Vector((0, -1, 0))
        cam_face = eye_mid + fwd * 0.55 + Vector((0, 0, 0.02))
        cam_full = centre + fwd * (size / 2 / math.tan(math.radians(19)) + 0.4) + Vector((0, 0, 0.1))
    C.studio_render(C.OUT / f"preview_{variant}.png", cam_full, centre, lens=50, res=(512, 640))
    C.studio_render(C.OUT / f"face_{variant}.png", cam_face, eye_mid + Vector((0, 0, -0.012)), lens=85, res=(512, 512), key_energy=60)


def main():
    args = C.script_args()
    variant = args[args.index("--variant") + 1] if "--variant" in args else "all"
    out = pathlib.Path(args[args.index("--out") + 1]) if "--out" in args else C.ASSETS / "Models" / "Figures"
    if not out.is_absolute():
        out = C.ROOT / out
    names = list(VARIANTS) if variant == "all" else [variant]
    for v in names:
        root = build_variant(v)
        C.export_fbx(root, out / f"Figure_{v}.fbx")
        print(f"exported Figure_{v}.fbx")
        if "--no-preview" not in args:
            preview(v, root)


if __name__ == "__main__":
    main()
