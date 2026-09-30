"""
Generates low poly placeholder models for the rig, at real world size, and exports
each one as an FBX that Unity imports directly.

    blender -b --factory-startup --python tools/blender/generate_rig_parts.py
    blender -b --factory-startup --python tools/blender/generate_rig_parts.py -- --out <folder> --preview preview.png

Or open Blender, go to the Scripting tab, open this file and press Run Script.

Parts: gpu_card, psu, motherboard, riser, frame. Every part is centred on its origin,
so Unity can drop it exactly where the scene builder's placeholder boxes used to go.

Names matter to Unity:
    Fan0, Fan1   separate objects with their origin at the fan centre, so they can spin
    LED...       every object whose name starts with LED glows when the card is busy:
                 LED (the light bar on the top edge) and LED_Ring0, LED_Ring1 (fan rings)

Axes: built with Blender's X across the card's thickness, Y along its length and Z up.
The exporter settings below turn that into Unity's X, Z and Y, so a card stands upright
in Unity with no rotation needed.

These are placeholders with real proportions, not final art. They are deliberately
cheap: a few hundred to a couple of thousand triangles each, for a Quest 2.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

# ---------------------------------------------------------------------------- args

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    here = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
    out = os.path.normpath(os.path.join(here, "..", "..", "Assets", "Art", "Models"))
    preview = None
    i = 0
    while i < len(argv):
        if argv[i] == "--out":
            out = argv[i + 1]; i += 2
        elif argv[i] == "--preview":
            preview = argv[i + 1]; i += 2
        else:
            i += 1
    return out, preview

# ------------------------------------------------------------------------ materials

MATERIALS = {}

def material(name, color, metallic=0.0, roughness=0.5):
    """One shared material per name, so every part using it batches together in Unity."""
    if name in MATERIALS:
        return MATERIALS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    mat.diffuse_color = (*color, 1.0)      # what the FBX exporter writes as the base colour
    MATERIALS[name] = mat
    return mat

def define_materials():
    material("Shroud", (0.08, 0.08, 0.09), metallic=0.2, roughness=0.45)
    material("ShroudAccent", (0.55, 0.56, 0.58), metallic=0.9, roughness=0.3)
    material("FanBlack", (0.03, 0.03, 0.03), roughness=0.6)
    material("PCB", (0.03, 0.12, 0.06), roughness=0.55)
    material("Gold", (0.83, 0.64, 0.25), metallic=1.0, roughness=0.3)
    material("Steel", (0.62, 0.63, 0.65), metallic=1.0, roughness=0.35)
    material("Aluminium", (0.78, 0.79, 0.8), metallic=1.0, roughness=0.4)
    material("FrameBlack", (0.03, 0.03, 0.035), metallic=0.8, roughness=0.35)   # anodised
    material("BlackPlastic", (0.05, 0.05, 0.05), roughness=0.7)
    material("PSUBody", (0.1, 0.1, 0.11), metallic=0.6, roughness=0.4)
    material("Label", (0.85, 0.85, 0.82), roughness=0.8)
    # Unity drives the glow at runtime. Exported with no emission so it's dark when idle.
    material("LED", (0.9, 0.9, 0.9), roughness=0.3)

# --------------------------------------------------------------------- mesh builder

class MeshBuilder:
    """Accumulates boxes and cylinders into one mesh, each face tagged with a material."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.materials = []

    def _mat_index(self, mat_name):
        if mat_name not in self.materials:
            self.materials.append(mat_name)
        return self.materials.index(mat_name)

    def _tag(self, faces, mat_name, smooth_sides=False):
        idx = self._mat_index(mat_name)
        for f in faces:
            f.material_index = idx
            f.smooth = smooth_sides and len(f.verts) == 4

    def box(self, size, center=(0, 0, 0), mat="Shroud", bevel=0.0, rotation=None):
        before = set(self.bm.faces)
        m = Matrix.Translation(Vector(center))
        if rotation is not None:
            m = m @ rotation
        m = m @ Matrix.Diagonal((*size, 1.0))
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m, calc_uvs=True)
        if bevel > 0.0:
            verts = res["verts"]
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=verts + edges, offset=bevel, offset_type='OFFSET',
                            segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
        self._tag(set(self.bm.faces) - before, mat)

    def cylinder(self, radius, depth, center=(0, 0, 0), axis="X", mat="FanBlack", segments=24):
        before = set(self.bm.faces)
        rot = {"X": Matrix.Rotation(math.radians(90), 4, "Y"),
               "Y": Matrix.Rotation(math.radians(90), 4, "X"),
               "Z": Matrix.Identity(4)}[axis]
        m = Matrix.Translation(Vector(center)) @ rot
        bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                              radius1=radius, radius2=radius, depth=depth, matrix=m, calc_uvs=True)
        self._tag(set(self.bm.faces) - before, mat, smooth_sides=True)

    def ring(self, outer, inner, depth, center=(0, 0, 0), mat="FanBlack", segments=32):
        """A flat ring around the X axis: a fan's outer frame."""
        before = set(self.bm.faces)
        cx, cy, cz = center
        outer_ring, inner_ring = [], []
        for side in (-0.5, 0.5):
            o, n = [], []
            for i in range(segments):
                a = 2 * math.pi * i / segments
                o.append(self.bm.verts.new((cx + side * depth, cy + outer * math.cos(a), cz + outer * math.sin(a))))
                n.append(self.bm.verts.new((cx + side * depth, cy + inner * math.cos(a), cz + inner * math.sin(a))))
            outer_ring.append(o); inner_ring.append(n)
        for i in range(segments):
            j = (i + 1) % segments
            for quad in (
                (outer_ring[0][i], outer_ring[0][j], outer_ring[1][j], outer_ring[1][i]),   # outside
                (inner_ring[0][j], inner_ring[0][i], inner_ring[1][i], inner_ring[1][j]),   # inside
                (outer_ring[0][j], outer_ring[0][i], inner_ring[0][i], inner_ring[0][j]),   # back face
                (outer_ring[1][i], outer_ring[1][j], inner_ring[1][j], inner_ring[1][i]),   # front face
            ):
                self.bm.faces.new(quad)
        self._tag(set(self.bm.faces) - before, mat)

    def to_object(self, parent=None, location=(0, 0, 0)):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mat_name in self.materials:
            mesh.materials.append(MATERIALS[mat_name])
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        if parent is not None:
            obj.parent = parent
        return obj

def empty(name):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'PLAIN_AXES'
    e.empty_display_size = 0.05
    bpy.context.collection.objects.link(e)
    return e

# ---------------------------------------------------------------------------- parts
# Real sizes in metres. A dual fan GTX 1070 is roughly 267 x 111 x 40 mm.

def build_fan(name, radius, parent, location):
    """Built around its own origin so Unity can spin it about its centre."""
    fan = MeshBuilder(name)
    fan.ring(outer=radius, inner=radius - 0.004, depth=0.006, mat="FanBlack")
    fan.cylinder(radius=radius * 0.32, depth=0.008, mat="ShroudAccent", segments=20)
    blades = 9
    for k in range(blades):
        a = 2 * math.pi * k / blades
        r_mid = radius * 0.64
        rot = Matrix.Rotation(a, 4, "X") @ Matrix.Rotation(math.radians(25), 4, "Z")
        center = (0.0, r_mid * math.cos(a + math.pi / 2), r_mid * math.sin(a + math.pi / 2))
        fan.box((0.0012, radius * 0.34, radius * 0.62), center=center, mat="FanBlack", rotation=rot)
    return fan.to_object(parent=parent, location=location)

def build_gpu_card():
    root = empty("GPU_Card")
    L, H, T = 0.267, 0.111, 0.040

    body = MeshBuilder("Body")
    body.box((0.030, L, H - 0.004), center=(-0.002, 0, 0.002), mat="Shroud", bevel=0.004)
    body.box((0.0016, L - 0.004, H - 0.006), center=(0.0152, 0, 0), mat="PCB")
    body.box((0.0018, L - 0.010, H - 0.012), center=(0.0175, 0.003, 0.0), mat="ShroudAccent")   # backplate
    body.box((0.020, 0.0012, 0.120), center=(0.006, -L / 2 - 0.0006, 0.004), mat="Steel")       # IO bracket
    body.box((0.0016, 0.090, 0.008), center=(0.0152, -0.040, -H / 2 - 0.004), mat="Gold")       # PCIe fingers
    body.box((0.012, 0.022, 0.008), center=(0.008, 0.095, H / 2 + 0.004), mat="BlackPlastic")    # 8 pin power
    body.to_object(parent=root)

    # Light bar across the top edge, where a real card has its glowing logo. It's the
    # face the viewer looks down on, so it has to be wide enough to read from the bench.
    # Stops short of the 8 pin connector at the far end.
    led = MeshBuilder("LED")
    led.box((0.024, 0.160, 0.003), center=(-0.002, -0.020, H / 2 + 0.0015), mat="LED")
    led.to_object(parent=root)

    fan_r = 0.041
    for i, y in enumerate((-0.063, 0.063)):
        build_fan("Fan%d" % i, fan_r, root, (-0.0185, y, 0.0))
        # A lit ring just outside each fan's frame. Separate from the fan so it doesn't
        # spin, and so Unity can find it by name.
        ring = MeshBuilder("LED_Ring%d" % i)
        ring.ring(outer=fan_r + 0.004, inner=fan_r + 0.0005, depth=0.004, mat="LED", segments=40)
        ring.to_object(parent=root, location=(-0.0185, y, 0.0))
    return root, (T, L, H)

def build_psu():
    root = empty("PSU")
    W, D, H = 0.150, 0.160, 0.086          # ATX width, a longer high wattage depth, height
    body = MeshBuilder("Body")
    body.box((W, D, H), mat="PSUBody", bevel=0.003)
    body.box((W * 0.6, 0.0015, H * 0.5), center=(0, -D / 2 - 0.0008, 0), mat="Label")           # side label
    body.box((0.050, 0.030, 0.030), center=(0.03, D / 2 + 0.012, -0.015), mat="BlackPlastic")    # cable exit
    body.to_object(parent=root)
    grille = MeshBuilder("Fan0")
    grille.ring(outer=0.058, inner=0.054, depth=0.004, mat="Steel", segments=40)
    grille.cylinder(radius=0.012, depth=0.004, mat="Steel", segments=16)
    for k in range(6):
        a = math.pi * k / 6
        grille.box((0.002, 0.108, 0.002), center=(0, 0, 0), mat="Steel",
                   rotation=Matrix.Rotation(a, 4, "X"))
    fan_obj = grille.to_object(parent=root, location=(0, 0, H / 2 + 0.002))
    fan_obj.rotation_euler = (0, math.radians(-90), 0)   # grille faces up
    return root, (W, D, H)

def build_motherboard():
    root = empty("Motherboard")
    W, D, T = 0.305, 0.244, 0.0016
    body = MeshBuilder("Body")
    body.box((W, D, T), mat="PCB")
    body.box((0.045, 0.045, 0.006), center=(0.02, 0.05, 0.004), mat="Steel")                     # CPU socket
    for i in range(4):                                                                          # RAM slots
        body.box((0.006, 0.133, 0.009), center=(0.085 + 0.011 * i, 0.03, 0.0053), mat="BlackPlastic")
    for i in range(6):                                                                          # PCIe slots
        body.box((0.089, 0.0075, 0.011), center=(-0.06, -0.02 - 0.02 * i, 0.0063), mat="BlackPlastic")
    body.box((0.040, 0.030, 0.020), center=(-0.10, 0.07, 0.011), mat="ShroudAccent")             # VRM heatsink
    body.box((0.030, 0.030, 0.012), center=(0.05, -0.08, 0.007), mat="ShroudAccent")             # chipset
    body.to_object(parent=root)
    return root, (W, D, T)

def build_riser():
    root = empty("Riser")
    W, D, T = 0.040, 0.100, 0.0016
    body = MeshBuilder("Body")
    body.box((W, D, T), mat="PCB")
    body.box((0.0075, 0.089, 0.011), center=(0, 0, 0.0063), mat="BlackPlastic")                 # x16 slot
    body.box((0.014, 0.008, 0.007), center=(0.012, -0.04, 0.0043), mat="Steel")                  # USB socket
    body.to_object(parent=root)
    return root, (W, D, T)

def build_frame():
    """Open air mining frame: base rectangle, four posts, top rails, and a card bar."""
    root = empty("Frame")
    W, D, H = 0.72, 0.40, 0.30
    t = 0.020                                                    # square aluminium tube
    body = MeshBuilder("Body")
    zb, zt = -H / 2 + t / 2, H / 2 - t / 2
    xs = (-W / 2 + t / 2, W / 2 - t / 2)
    ys = (-D / 2 + t / 2, D / 2 - t / 2)
    # Pieces butt up against each other rather than overlapping. Overlapping boxes put two
    # faces in the same place, and the renderer flickers between them.
    for x in xs:
        for y in ys:
            body.box((t, t, H), center=(x, y, 0), mat="FrameBlack")                 # posts, full height
    for y in ys:
        body.box((W - 2 * t, t, t), center=(0, y, zb), mat="FrameBlack")            # long rails, between posts
        body.box((W - 2 * t, t, t), center=(0, y, zt), mat="FrameBlack")
    for x in xs:
        body.box((t, D - 2 * t, t), center=(x, 0, zb), mat="FrameBlack")            # short rails, between posts
    body.box((W - 2 * t, t, t), center=(0, 0.06, -0.04), mat="FrameBlack")     # bar the cards rest on
    body.to_object(parent=root)
    return root, (W, D, H)

# --------------------------------------------------------------------------- export

def select_tree(root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root

def export_fbx(root, path):
    select_tree(root)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={'EMPTY', 'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z',
        axis_up='Y',
        bake_space_transform=True,
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
    )

def triangle_count(root):
    tris = 0
    for obj in [root] + list(root.children_recursive):
        if obj.type == 'MESH':
            tris += sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return tris

# ---------------------------------------------------------------------------- preview

def render_preview(roots, path):
    """A contact sheet of every part, using CPU Cycles so it works with no GPU."""
    gap = 0.10
    layout = []
    for root, size in roots:
        if root.name == "GPU_Card":
            # Turn the card so its fans face the camera. Preview only: already exported.
            root.rotation_euler = (0, 0, math.radians(90))
            footprint = size[1]
        else:
            footprint = size[0]
        layout.append((root, footprint, size[2]))
    total = sum(f for _, f, _ in layout) + gap * (len(layout) - 1)
    x = -total / 2
    for root, footprint, height in layout:
        root.location = (x + footprint / 2, 0, height / 2)
        x += footprint + gap

    floor = bpy.data.meshes.new("Floor")
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=4.0); bm.to_mesh(floor); bm.free()
    floor_obj = bpy.data.objects.new("Floor", floor)
    floor_obj.data.materials.append(material("PreviewFloor", (0.18, 0.19, 0.21), roughness=0.9))
    bpy.context.collection.objects.link(floor_obj)

    def aim(obj, target):
        obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()

    target = (0, 0, 0.08)
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    bpy.context.collection.objects.link(cam)
    cam.data.lens = 35
    cam.location = (0, -total * 1.15, total * 0.45)
    aim(cam, target)
    bpy.context.scene.camera = cam

    for loc, energy in (((1.2, -1.6, 1.8), 120), ((-1.6, -1.0, 1.2), 50), ((0, 1.6, 1.6), 60)):
        light = bpy.data.objects.new("Light", bpy.data.lights.new("Light", 'AREA'))
        light.data.energy = energy
        light.data.size = 1.2
        light.location = loc
        bpy.context.collection.objects.link(light)
        aim(light, target)

    world = bpy.data.worlds.new("World"); world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.62, 0.66, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.08
    bpy.context.scene.world = world

    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 48
    scene.cycles.use_denoising = False     # not every Blender build ships the denoiser
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 520
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

# ------------------------------------------------------------------------------ main

def main():
    out_dir, preview = parse_args()
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1.0
    define_materials()

    built = []
    for file_name, builder in (("gpu_card", build_gpu_card), ("psu", build_psu),
                               ("motherboard", build_motherboard), ("riser", build_riser),
                               ("frame", build_frame)):
        root, size = builder()
        path = os.path.join(out_dir, file_name + ".fbx")
        export_fbx(root, path)
        print(f"[rig parts] {file_name:12s} {triangle_count(root):5d} tris  "
              f"{size[0]*1000:.0f} x {size[1]*1000:.0f} x {size[2]*1000:.0f} mm  -> {path}")
        built.append((root, size))

    if preview:
        render_preview(built, os.path.abspath(preview))
        print(f"[rig parts] preview -> {os.path.abspath(preview)}")

main()
