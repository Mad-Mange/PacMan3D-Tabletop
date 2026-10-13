"""Builds the tabletop props that dress the table around the maze, and exports them to the Unity project.

Run headless from the project root:
    "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python Blender/build_props.py

Output: Assets/Resources/Models/Props.fbx with one object per prop. One maze tile is one unit, about 2 cm, so
the mug is ~9 cm across. Props are modelled facing -Y with +Z up and stand on z = 0. The moving parts
(Joystick_Stick, Joystick_Button, Plant_Leaves) keep their position relative to their base, so in Unity they
can be parented under the same transform and animated about their own pivot. Unity swaps the materials by name.
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Models", "Props.fbx")


# --------------------------------------------------------------------------- helpers

def material(name, rgb):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (rgb[0] / 255.0, rgb[1] / 255.0, rgb[2] / 255.0, 1.0)
    return mat


class Part:
    """Accumulates geometry for one object, tracking which material each face uses."""

    def __init__(self, *materials):
        self.bm = bmesh.new()
        self.mats = list(materials)
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def index(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def tag(self, faces, mat, smooth_axis=None):
        i = self.index(mat)
        for f in faces:
            f.material_index = i
            if smooth_axis is None:
                f.smooth = False
            else:
                f.normal_update()
                f.smooth = abs(f.normal.dot(smooth_axis)) < 0.9

    def faces_of(self, geom):
        faces = set()
        for v in geom["verts"]:
            faces.update(v.link_faces)
        return list(faces)

    def cylinder(self, mat, r1, r2, depth, matrix, segments=40, cap=True):
        geom = bmesh.ops.create_cone(self.bm, cap_ends=cap, cap_tris=False, segments=segments,
                                     radius1=r1, radius2=r2, depth=depth, matrix=matrix)
        faces = self.faces_of(geom)
        axis = (matrix.to_3x3() @ Vector((0, 0, 1))).normalized()
        self.tag(faces, mat, axis)
        return faces

    def box(self, mat, size, center, bevel=0.0, segments=2):
        geom = bmesh.ops.create_cube(self.bm, size=1.0,
                                     matrix=Matrix.Translation(center) @ Matrix.Diagonal((*size, 1)))
        faces = self.faces_of(geom)
        if bevel > 0:
            edges = list({e for f in faces for e in f.edges})
            res = bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=segments, affect='EDGES', profile=0.5)
            faces = list(set(faces) | set(res["faces"]))
            faces = [f for f in faces if f.is_valid]
        self.tag(faces, mat)
        for f in faces:
            # Flat main faces, smooth bevels.
            f.normal_update()
            f.smooth = bevel > 0 and max(abs(f.normal.x), abs(f.normal.y), abs(f.normal.z)) < 0.99
        return faces

    def sphere(self, mat, radius, matrix, u=24, v=12):
        geom = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=radius, matrix=matrix)
        faces = self.faces_of(geom)
        self.tag(faces, mat)
        for f in faces:
            f.smooth = True
        return faces

    def torus(self, mat, major, minor, matrix, major_segments=32, minor_segments=12, arc=(0.0, 2 * math.pi)):
        """A torus in its local XZ plane (axis along local Y), optionally only an arc of it."""
        closed = abs(arc[1] - arc[0] - 2 * math.pi) < 1e-6
        rings = major_segments if closed else major_segments + 1
        verts = []
        for i in range(rings):
            a = arc[0] + (arc[1] - arc[0]) * i / major_segments
            ring = []
            for j in range(minor_segments):
                b = 2 * math.pi * j / minor_segments
                d = major + minor * math.cos(b)
                p = Vector((d * math.cos(a), minor * math.sin(b), d * math.sin(a)))
                ring.append(self.bm.verts.new(matrix @ p))
            verts.append(ring)
        faces = []
        for i in range(major_segments):
            r0, r1 = verts[i], verts[(i + 1) % rings]
            for j in range(minor_segments):
                k = (j + 1) % minor_segments
                faces.append(self.bm.faces.new((r0[j], r1[j], r1[k], r0[k])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self.tag(faces, mat)
        for f in faces:
            f.smooth = True
        return faces

    def planar_uv(self, faces, x0, x1, y0, y1):
        """Maps faces to the texture by Blender x/y so the image reads upright from the Unity camera (south)."""
        for f in faces:
            for loop in f.loops:
                co = loop.vert.co
                loop[self.uv].uv = ((x1 - co.x) / (x1 - x0), (y1 - co.y) / (y1 - y0))

    def build(self, name, location=(0, 0, 0)):
        mesh = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.mats:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(name, mesh)
        obj.location = location
        bpy.context.scene.collection.objects.link(obj)
        return obj


def T(x, y, z):
    return Matrix.Translation((x, y, z))


ROT_X90 = Matrix.Rotation(math.radians(90), 4, 'X')
ROT_Y90 = Matrix.Rotation(math.radians(90), 4, 'Y')


# --------------------------------------------------------------------------- props

def mug():
    ceramic = material("MugCeramic", (245, 238, 225))
    stripe = material("MugStripe", (40, 90, 230))
    coffee = material("Coffee", (60, 30, 15))
    p = Part(ceramic)
    R, r, H, base = 2.2, 1.95, 4.6, 0.35
    bm = p.bm

    outer = p.cylinder(ceramic, R, R, H, T(0, 0, H / 2), segments=56)
    inner = p.cylinder(ceramic, r, r, H - base, T(0, 0, base + (H - base) / 2), segments=56)
    # Open the top: drop both top caps and join the two walls with a rim.
    tops = [f for f in outer + inner if f.is_valid and f.normal.z > 0.9 and f.calc_center_median().z > H - 0.01]
    bmesh.ops.delete(bm, geom=tops, context='FACES_ONLY')
    boundary = [e for e in bm.edges if e.is_boundary]
    rim = bmesh.ops.bridge_loops(bm, edges=boundary)
    # The walls, rim and bottoms now form one closed solid: orient everything outward.
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    p.tag(rim["faces"], ceramic)
    for f in rim["faces"]:
        f.smooth = True

    p.cylinder(stripe, R + 0.02, R + 0.02, 0.55, T(0, 0, 3.55), segments=56)
    p.cylinder(coffee, r - 0.01, r - 0.01, 0.05, T(0, 0, H - 0.85), segments=56)
    # Handle: an arc of a torus standing upright on the +X side (Unity -X), ends buried in the wall.
    p.torus(ceramic, 1.05, 0.28, T(R + 0.15, 0, 2.5), arc=(-1.75, 1.75), major_segments=24)
    return p.build("Mug")


def spool():
    plastic = material("SpoolPlastic", (35, 38, 44))
    hole = material("SpoolHole", (8, 8, 10))
    label = material("SpoolLabel", (240, 240, 235))
    filament = material("Filament", (40, 160, 150))
    p = Part(plastic)
    radius = 5.0
    # Axis along X, standing on its flanges.
    axis = T(0, 0, radius) @ ROT_Y90
    for side in (-1, 1):
        p.cylinder(plastic, radius, radius, 0.35, T(side * 1.9, 0, 0) @ axis, segments=64)
        p.cylinder(hole, 0.95, 0.95, 0.38, T(side * 1.9, 0, 0) @ axis, segments=32)
        p.box(label, (0.04, 2.4, 1.3), (side * 2.09, 0, radius + 2.6))
    p.cylinder(filament, 4.2, 4.2, 3.45, axis, segments=64)
    p.cylinder(plastic, 1.5, 1.5, 3.5, axis, segments=32)
    return p.build("Spool")


def joystick():
    black = material("ArcadeBlack", (20, 20, 24))
    panel = material("ArcadePanel", (255, 200, 30))
    metal = material("Metal", (190, 190, 195))
    red = material("ArcadeRed", (230, 30, 35))
    blue = material("ArcadeBlue", (40, 90, 255))
    green = material("ArcadeGreen", (40, 200, 80))
    base = Part(black)
    base.box(black, (7.0, 4.5, 1.6), (0, 0, 0.8), bevel=0.3, segments=3)
    base.box(panel, (6.3, 3.8, 0.08), (0, 0, 1.62))
    base.cylinder(metal, 0.6, 0.6, 0.1, T(-1.6, 0, 1.68), segments=32)
    for x, y, mat in ((1.2, 0.75, blue), (2.4, 0.4, green), (2.4, -0.8, red)):
        base.cylinder(black, 0.5, 0.5, 0.12, T(x, y, 1.7), segments=32)
        base.cylinder(mat, 0.4, 0.4, 0.3, T(x, y, 1.78), segments=32)
    base.build("Joystick_Base")

    stick = Part(metal)
    stick.cylinder(metal, 0.16, 0.16, 1.9, T(0, 0, 0.95), segments=16)
    stick.sphere(red, 0.62, T(0, 0, 2.1))
    stick.build("Joystick_Stick", location=(-1.6, 0, 1.7))

    # The big start button, pressed when a game starts.
    button = Part(black)
    button.cylinder(black, 0.62, 0.62, 0.12, T(0, 0, 0.06), segments=32)
    button.cylinder(red, 0.5, 0.5, 0.35, T(0, 0, 0.22), segments=32)
    button.build("Joystick_Button", location=(1.1, -0.85, 1.66))


def game_box():
    art = material("GameBox", (255, 255, 255))
    side = material("GameBoxSide", (20, 30, 110))
    p = Part(side)
    p.box(side, (13.8, 13.8, 2.0), (0, 0, 1.0))
    lid = p.box(side, (14.2, 14.2, 0.9), (0, 0, 1.95))
    top = [f for f in lid if f.normal.z > 0.9]
    p.tag(top, art)
    p.planar_uv(top, -7.1, 7.1, -7.1, 7.1)
    return p.build("GameBox")


def notebook():
    cover = material("NotebookCover", (30, 50, 90))
    page = material("NotebookPage", (255, 255, 255))
    edge = material("PageEdge", (235, 232, 222))
    metal = material("Metal", (190, 190, 195))
    p = Part(cover)
    p.box(cover, (9.2, 12.2, 0.12), (0, 0, 0.06))
    block = p.box(edge, (9.0, 12.0, 0.3), (0, 0.05, 0.27))
    top = [f for f in block if f.normal.z > 0.9]
    p.tag(top, page)
    p.planar_uv(top, -4.5, 4.5, -5.95, 6.05)
    # Spiral binding along the far edge.
    for i in range(16):
        x = -4.2 + i * 0.56
        p.torus(metal, 0.32, 0.05, T(x, -6.0, 0.25) @ Matrix.Rotation(math.radians(90), 4, 'Z'),
                major_segments=16, minor_segments=6)
    return p.build("Notebook")


def pencil():
    yellow = material("PencilYellow", (250, 200, 30))
    wood = material("PencilWood", (225, 180, 130))
    graphite = material("Graphite", (40, 40, 45))
    metal = material("Metal", (190, 190, 195))
    eraser = material("Eraser", (240, 130, 150))
    p = Part(yellow)
    r = 0.32
    axis = T(0, 0, r) @ ROT_Y90
    p.cylinder(yellow, r, r, 8.0, axis, segments=6)
    p.cylinder(wood, r, 0.1, 1.2, T(4.6, 0, 0) @ axis, segments=6)
    p.cylinder(graphite, 0.1, 0.0, 0.35, T(5.37, 0, 0) @ axis, segments=6)
    p.cylinder(metal, r + 0.02, r + 0.02, 0.6, T(-4.3, 0, 0) @ axis, segments=16)
    p.cylinder(eraser, r - 0.01, r - 0.01, 0.6, T(-4.9, 0, 0) @ axis, segments=16)
    return p.build("Pencil")


PIPS = {
    1: [(0, 0)],
    2: [(-1, -1), (1, 1)],
    3: [(-1, -1), (0, 0), (1, 1)],
    4: [(-1, -1), (-1, 1), (1, -1), (1, 1)],
    5: [(-1, -1), (-1, 1), (0, 0), (1, -1), (1, 1)],
    6: [(-1, -1), (-1, 0), (-1, 1), (1, -1), (1, 0), (1, 1)],
}


def dice():
    white = material("DiceWhite", (250, 248, 240))
    pip = material("DicePip", (20, 20, 25))
    p = Part(white)
    s = 1.8
    p.box(white, (s, s, s), (0, 0, s / 2), bevel=0.28, segments=3)
    # Opposite faces add up to 7.
    faces = [((0, 0, 1), 1), ((0, 0, -1), 6), ((0, 1, 0), 2), ((0, -1, 0), 5), ((1, 0, 0), 3), ((-1, 0, 0), 4)]
    for normal, count in faces:
        n = Vector(normal)
        a = Vector((1, 0, 0)) if abs(n.x) < 0.5 else Vector((0, 1, 0))
        b = n.cross(a)
        for i, j in PIPS[count]:
            c = Vector((0, 0, s / 2)) + n * (s / 2 - 0.02) + a * (i * 0.46) + b * (j * 0.46)
            up = 'X' if abs(n.y) > 0.5 else 'Y'
            m = T(*c) @ n.to_track_quat('Z', up).to_matrix().to_4x4() @ Matrix.Diagonal((1, 1, 0.35, 1))
            p.sphere(pip, 0.17, m, u=12, v=6)
    return p.build("Dice")


def token():
    gold = material("TokenGold", (230, 180, 60))
    p = Part(gold)
    p.cylinder(gold, 0.75, 0.75, 0.12, T(0, 0, 0.06), segments=32)
    for z in (0.0, 0.12):
        p.cylinder(gold, 0.52, 0.52, 0.03, T(0, 0, z), segments=32)
    return p.build("Token")


def plant():
    pot = material("PotTerracotta", (190, 95, 55))
    soil = material("Soil", (55, 38, 25))
    leaf = material("LeafGreen", (60, 140, 60))
    dark = material("LeafDark", (30, 90, 45))
    p = Part(pot)
    p.cylinder(pot, 1.9, 2.5, 3.6, T(0, 0, 1.8), segments=40)
    p.torus(pot, 2.45, 0.22, T(0, 0, 3.55) @ ROT_X90, major_segments=40, minor_segments=8)
    p.cylinder(soil, 2.35, 2.35, 0.1, T(0, 0, 3.35), segments=40)
    p.build("Plant_Pot")

    # Snake-plant blades fanning out of the soil; the pivot sits on the soil so Unity can sway them.
    leaves = Part(leaf)
    blades = [(0.0, 7.5, 0), (1.3, 6.2, 1), (2.5, 8.2, 0), (3.6, 5.6, 1), (4.7, 7.0, 0), (5.6, 6.6, 1), (0.6, 5.0, 1)]
    for k, (angle, height, shade) in enumerate(blades):
        mat = dark if shade else leaf
        lean = math.radians(10 + 7 * (k % 3))
        rot = Matrix.Rotation(angle, 4, 'Z') @ Matrix.Rotation(lean, 4, 'X')
        offset = Matrix.Rotation(angle, 4, 'Z') @ Vector((0.0, -0.45, 0.0))
        rows = 10
        front, back = [], []
        for i in range(rows + 1):
            t = i / rows
            width = 0.55 * (1.0 + 0.4 * math.sin(t * math.pi * 0.8)) * (1.0 - t ** 2.2)
            curve = 0.6 * t * t
            for side in (-1, 1):
                local = Vector((side * width, -curve, t * height))
                front.append(leaves.bm.verts.new(T(*offset) @ rot @ (local - Vector((0, 0.03, 0)))))
                back.append(leaves.bm.verts.new(T(*offset) @ rot @ (local + Vector((0, 0.03, 0)))))
        faces = []
        for i in range(rows):
            a, b, c, d = i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2
            faces.append(leaves.bm.faces.new((front[a], front[b], front[c], front[d])))
            faces.append(leaves.bm.faces.new((back[d], back[c], back[b], back[a])))
        leaves.tag(faces, mat)
        for f in faces:
            f.smooth = True
    leaves.build("Plant_Leaves", location=(0, 0, 3.4))


def export(path):
    bpy.ops.object.select_all(action='SELECT')
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


if __name__ == "__main__":
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mug()
    spool()
    joystick()
    game_box()
    notebook()
    pencil()
    dice()
    token()
    plant()
    export(OUT)
