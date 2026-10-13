"""Builds the character models for Pac-Man 3D and exports them as FBX into the Unity project.

The look follows the 3D-printed tabletop maze in inspiration/InspirationPicture.png:
ghosts are chunky voxel prints with bevelled cubes, the fruit are extruded pixel art.

Run headless from the project root:
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python Blender/build_models.py

Output (Assets/Resources/Models):
    Pacman.fbx  JawUpper, JawLower        hinge at the origin, mouth opens toward -Y (Unity +Z)
    Ghost.fbx   BodyA, BodyB              two skirt frames, swapped while moving
                Eyes, Pupils, FrightFace  eyes are hidden and FrightFace shown while frightened
    Fruits.fbx  Fruit_<Name>              one per bonus fruit, from Assets/Resources/PixelArt/fruits.json

Every mesh is modelled facing -Y with +Z up. Unity remaps the materials by name at runtime,
so the colours here only matter for previewing in Blender.
"""

import json
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Models")
PIXEL_ART = os.path.join(ROOT, "Assets", "Resources", "PixelArt", "fruits.json")

# Ghost voxel grid: 12 voxels across a ghost that is 1 unit wide and 1.12 tall.
GHOST_N = 12
GHOST_VOXEL = 1.0 / GHOST_N
GHOST_RADIUS = 0.5
GHOST_HEIGHT = 1.12
GHOST_DOME_START = 0.62
SKIRT_LOBES = 8


# --------------------------------------------------------------------------- helpers

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, rgb):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        rgba = (rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0, 1.0)
        mat.diffuse_color = rgba
        bsdf = mat.node_tree.nodes.get("Principled BSDF") if mat.node_tree else None
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = rgba
    return mat


def make_object(name, bm, materials, smooth=False):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    for mat in materials:
        mesh.materials.append(mat)
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def bevelled_cube_template(bevel=0.14):
    """A unit cube with chamfered edges, as (verts, faces) to stamp out voxels with."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', profile=0.5)
    verts = [v.co.copy() for v in bm.verts]
    index = {v: i for i, v in enumerate(bm.verts)}
    faces = [[index[v] for v in f.verts] for f in bm.faces]
    bm.free()
    return verts, faces


CUBE = None


def add_voxel(bm, center, size, mat_index=0):
    global CUBE
    if CUBE is None:
        CUBE = bevelled_cube_template()
    verts, faces = CUBE
    new = [bm.verts.new(center + v * size) for v in verts]
    for f in faces:
        face = bm.faces.new([new[i] for i in f])
        face.material_index = mat_index


def export_fbx(path, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'MESH'},
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
    )
    print("exported", path)


# --------------------------------------------------------------------------- Pac-Man

def build_jaw(name, upper, yellow, mouth, eye):
    """Half of a sphere of radius 0.5, closed by a flat mouth plate on the hinge plane."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=0.5)
    # Keep one hemisphere.
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    result = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, 0), plane_no=(0, 0, 1),
                                    clear_inner=upper, clear_outer=not upper)
    cut_edges = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    for f in bm.faces:
        f.material_index = 0
        f.smooth = True
    # Close the cut with the mouth plate.
    fill = bmesh.ops.triangle_fill(bm, edges=cut_edges, use_beauty=True, use_dissolve=False)
    for f in fill["geom"]:
        if isinstance(f, bmesh.types.BMFace):
            f.material_index = 1
            f.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))

    mats = [yellow, mouth]
    if upper:
        # Two little eyes up front, so you can tell which way he is heading.
        for side in (-1, 1):
            eye_bm = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=8, radius=0.065,
                                               matrix=Matrix.Translation((side * 0.17, -0.37, 0.29)))
            for v in eye_bm["verts"]:
                for f in v.link_faces:
                    f.material_index = 2
                    f.smooth = True
        mats.append(eye)
    return make_object(name, bm, mats, smooth=True)


def build_pacman():
    reset_scene()
    yellow = material("PacYellow", (255, 222, 30))
    mouth = material("PacMouth", (90, 10, 20))
    eye = material("PacEye", (15, 15, 20))
    upper = build_jaw("JawUpper", True, yellow, mouth, eye)
    lower = build_jaw("JawLower", False, yellow, mouth, eye)
    export_fbx(os.path.join(OUT_DIR, "Pacman.fbx"), [upper, lower])


# --------------------------------------------------------------------------- ghosts

def ghost_radius_at(z):
    if z <= GHOST_DOME_START:
        return GHOST_RADIUS
    t = (z - GHOST_DOME_START) / (GHOST_HEIGHT - GHOST_DOME_START)
    return GHOST_RADIUS * math.sqrt(max(0.0, 1.0 - t * t))


def ghost_voxels(frame):
    """Voxel set (i, j, k) of a ghost. The skirt's lobes shift by half a lobe between frames."""
    layers = int(round(GHOST_HEIGHT / GHOST_VOXEL))
    voxels = set()
    for k in range(layers):
        z = (k + 0.5) * GHOST_VOXEL
        r = ghost_radius_at(z)
        for i in range(GHOST_N):
            for j in range(GHOST_N):
                x = (i + 0.5) * GHOST_VOXEL - 0.5
                y = (j + 0.5) * GHOST_VOXEL - 0.5
                d = math.hypot(x, y)
                if d > r:
                    continue
                if k < 2 and d > r - 1.6 * GHOST_VOXEL:
                    # Outer ring of the two bottom layers forms the wavy skirt.
                    lobe = (math.atan2(y, x) / (2 * math.pi) * SKIRT_LOBES + 0.5 * frame) % 1.0
                    keep = 0.22 if k == 0 else 0.4
                    if abs(lobe - 0.5) > keep:
                        continue
                elif k == 0:
                    continue  # hollow underneath
                voxels.add((i, j, k))
    return voxels


def surface_only(voxels):
    out = set()
    for (i, j, k) in voxels:
        for di, dj, dk in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
            if (i + di, j + dj, k + dk) not in voxels:
                out.add((i, j, k))
                break
    return out


def voxel_center(i, j, k):
    return Vector(((i + 0.5) * GHOST_VOXEL - 0.5, (j + 0.5) * GHOST_VOXEL - 0.5, (k + 0.5) * GHOST_VOXEL))


def front_surface_y(voxels, x, z):
    """Y of the front face of the frontmost voxel in the column at (x, z)."""
    i = min(GHOST_N - 1, max(0, int((x + 0.5) / GHOST_VOXEL)))
    k = int(z / GHOST_VOXEL)
    js = [j for (vi, j, vk) in voxels if vi == i and vk == k]
    if not js:
        return -GHOST_RADIUS
    return min(js) * GHOST_VOXEL - 0.5


def build_ghost_body(name, frame, mat):
    bm = bmesh.new()
    for (i, j, k) in sorted(surface_only(ghost_voxels(frame))):
        add_voxel(bm, voxel_center(i, j, k), GHOST_VOXEL * 0.98)
    return make_object(name, bm, [mat])


def build_ghost():
    reset_scene()
    body_mat = material("GhostBody", (255, 255, 255))
    white = material("EyeWhite", (245, 245, 255))
    pupil = material("Pupil", (30, 50, 200))
    face = material("FrightFace", (255, 200, 170))

    body_a = build_ghost_body("BodyA", 0, body_mat)
    body_b = build_ghost_body("BodyB", 1, body_mat)
    voxels = ghost_voxels(0)

    eye_z = 0.70
    eyes_bm = bmesh.new()
    pupils_bm = bmesh.new()
    for side in (-1, 1):
        x = side * 0.19
        front = front_surface_y(voxels, x, eye_z)
        bmesh.ops.create_uvsphere(eyes_bm, u_segments=20, v_segments=12, radius=1.0,
                                  matrix=Matrix.Translation((x, front + 0.01, eye_z)) @ Matrix.Diagonal((0.13, 0.09, 0.16, 1)))
        bmesh.ops.create_uvsphere(pupils_bm, u_segments=16, v_segments=8, radius=0.068,
                                  matrix=Matrix.Translation((x, front - 0.075, eye_z - 0.02)))
    eyes = make_object("Eyes", eyes_bm, [white], smooth=True)
    pupils = make_object("Pupils", pupils_bm, [pupil], smooth=True)

    # Frightened face: two small eyes and a zigzag mouth, stamped as voxels onto the front.
    face_bm = bmesh.new()
    s = GHOST_VOXEL * 0.8
    for side in (-1, 1):
        x = side * 0.17
        add_voxel(face_bm, Vector((x, front_surface_y(voxels, x, 0.72) - s * 0.3, 0.72)), s)
    for n in range(9):
        x = -0.32 + n * 0.08
        z = 0.40 if n % 2 == 0 else 0.47
        add_voxel(face_bm, Vector((x, front_surface_y(voxels, x, z) - s * 0.3, z)), s * 0.85)
    fright = make_object("FrightFace", face_bm, [face])

    export_fbx(os.path.join(OUT_DIR, "Ghost.fbx"), [body_a, body_b, eyes, pupils, fright])


# --------------------------------------------------------------------------- fruit

def build_fruits():
    reset_scene()
    with open(PIXEL_ART, "r", encoding="utf-8") as f:
        art = json.load(f)
    palette = {entry["key"]: entry for entry in art["palette"]}
    keys = list(palette.keys())
    mats = [material(palette[k]["name"], palette[k]["rgb"]) for k in keys]

    objects = []
    for fruit in art["fruits"]:
        rows = fruit["rows"]
        h = len(rows)
        w = max(len(r) for r in rows)
        px = 1.0 / max(w, h)
        # Centre the drawn pixels on the origin.
        cells = [(x, y, c) for y, row in enumerate(rows) for x, c in enumerate(row) if c in palette]
        min_x = min(c[0] for c in cells)
        max_x = max(c[0] for c in cells)
        min_y = min(c[1] for c in cells)
        max_y = max(c[1] for c in cells)
        cx = (min_x + max_x + 1) * 0.5
        cy = (min_y + max_y + 1) * 0.5

        bm = bmesh.new()
        for x, y, c in cells:
            for depth in (-0.5, 0.5):
                center = Vector(((x + 0.5 - cx) * px, depth * px, (cy - (y + 0.5)) * px))
                add_voxel(bm, center, px * 0.97, keys.index(c))
        objects.append(make_object("Fruit_" + fruit["name"], bm, mats))

    export_fbx(os.path.join(OUT_DIR, "Fruits.fbx"), objects)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    build_pacman()
    build_ghost()
    build_fruits()
