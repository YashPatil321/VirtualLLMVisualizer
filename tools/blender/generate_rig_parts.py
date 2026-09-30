"""
Generates low poly placeholder models for the rig, at real world size, and exports
each one as an FBX that Unity imports directly.

    blender -b --factory-startup --python tools/blender/generate_rig_parts.py
    blender -b --factory-startup --python tools/blender/generate_rig_parts.py -- --out <folder> --preview preview.png

Or open Blender, go to the Scripting tab, open this file and press Run Script.

Parts: gpu_card, psu, motherboard, cpu_cooler, ram_stick, riser, frame, pcie_cables,
modelled on Rig 2. Every part is centred on its origin, so Unity can drop it exactly
where the scene builder's placeholder boxes used to go.

Names matter to Unity:
    Fan0, Fan1   separate objects with their origin at the fan centre, so they can spin
    LED...       every object whose name starts with LED glows when the card is busy:
                 the light bar and EVGA lettering on the card's top edge
    Front        an empty on the side that should face the viewer

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
    # Colours taken from photos of Rig 2: black EVGA cards, a matte black aluminium
    # frame, black Antec supplies with gold lettering, black sleeved cables.
    material("Shroud", (0.035, 0.035, 0.04), metallic=0.2, roughness=0.5)
    material("ShroudGrey", (0.22, 0.23, 0.25), metallic=0.5, roughness=0.4)
    material("Backplate", (0.03, 0.03, 0.035), metallic=0.6, roughness=0.45)
    material("Fins", (0.3, 0.31, 0.33), metallic=0.9, roughness=0.5)
    material("FanBlack", (0.03, 0.03, 0.03), roughness=0.6)
    material("PCB", (0.03, 0.12, 0.06), roughness=0.55)
    material("Gold", (0.83, 0.64, 0.25), metallic=1.0, roughness=0.3)
    material("Steel", (0.62, 0.63, 0.65), metallic=1.0, roughness=0.35)
    material("Aluminium", (0.78, 0.79, 0.8), metallic=1.0, roughness=0.4)
    material("Copper", (0.72, 0.42, 0.26), metallic=1.0, roughness=0.35)
    material("FrameBlack", (0.025, 0.025, 0.028), metallic=0.3, roughness=0.6)   # matte anodised
    material("BlackPlastic", (0.05, 0.05, 0.05), roughness=0.7)
    material("PSUBody", (0.03, 0.03, 0.033), metallic=0.4, roughness=0.5)
    material("AntecGold", (0.95, 0.72, 0.18), metallic=0.3, roughness=0.4)
    material("Label", (0.85, 0.85, 0.82), roughness=0.8)
    material("DummyWhite", (0.88, 0.88, 0.86), roughness=0.6)
    material("UsbBlue", (0.1, 0.3, 0.85), roughness=0.5)
    material("CableBlack", (0.025, 0.025, 0.028), roughness=0.85)
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

    def text(self, body, size, center, mat, rotation=None, depth=0.0006):
        """Raised lettering, turned into mesh. Lies in XY reading along +X unless rotated."""
        before = set(self.bm.faces)
        cu = bpy.data.curves.new("lettering", type="FONT")
        cu.body = body
        cu.size = size
        cu.extrude = depth / 2
        cu.resolution_u = 2                 # coarse curves: this is read from a metre away
        cu.align_x = 'CENTER'
        cu.align_y = 'CENTER'
        obj = bpy.data.objects.new("lettering", cu)
        bpy.context.collection.objects.link(obj)
        mesh = obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh().copy()
        bpy.data.objects.remove(obj)
        bpy.data.curves.remove(cu)
        m = Matrix.Translation(Vector(center))
        if rotation is not None:
            m = m @ rotation
        mesh.transform(m)
        self.bm.from_mesh(mesh)             # appends to what is already here
        bpy.data.meshes.remove(mesh)
        self._tag(set(self.bm.faces) - before, mat)

    def tube(self, points, radius, mat, sides=6):
        """A round tube through the points: a cable. Open ended, since the ends are buried."""
        before = set(self.bm.faces)
        rings = []
        for i, p in enumerate(points):
            p = Vector(p)
            nxt = Vector(points[min(i + 1, len(points) - 1)])
            prv = Vector(points[max(i - 1, 0)])
            t = (nxt - prv).normalized()
            ref = Vector((1, 0, 0)) if abs(t.x) < 0.9 else Vector((0, 1, 0))
            n = t.cross(ref).normalized()
            b = t.cross(n).normalized()
            ring = []
            for k in range(sides):
                a = 2 * math.pi * k / sides
                ring.append(self.bm.verts.new(p + radius * (math.cos(a) * n + math.sin(a) * b)))
            rings.append(ring)
        for r0, r1 in zip(rings, rings[1:]):
            for k in range(sides):
                j = (k + 1) % sides
                self.bm.faces.new((r0[k], r0[j], r1[j], r1[k]))
        self._tag(set(self.bm.faces) - before, mat, smooth_sides=True)

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
# Real sizes in metres, modelled on Rig 2: eight EVGA GTX 1070 SC (ACX 3.0) cards on
# USB risers in a two level black aluminium frame, with two Antec HCP 1300 Platinum
# supplies at the ends of the lower level.
#
# Every part that has a front puts an empty named "Front" on that side. The scene
# builder turns each part so its Front faces the viewer, so the models come out the
# right way round whatever the FBX axis conversion does.

# Card row, shared with Assets/Data/RigLayout.asset (cardSpacing, firstCardOffset) and
# the scene builder. Change them together.
CARD_COUNT = 8
CARD_SPACING = 0.075
CARD_L, CARD_H, CARD_T = 0.267, 0.111, 0.040
CARD_CONNECTOR = (0.006, 0.095)        # 8 pin: x across the card, y along it from its centre

def front_marker(root, location):
    marker = empty("Front")
    marker.parent = root
    marker.location = location
    return marker

def build_fan(name, radius, parent, location):
    """Built around its own origin so Unity can spin it about its centre."""
    fan = MeshBuilder(name)
    fan.ring(outer=radius, inner=radius - 0.004, depth=0.006, mat="FanBlack")
    fan.cylinder(radius=radius * 0.3, depth=0.008, mat="ShroudGrey", segments=20)
    blades = 11                                                  # ACX 3.0 fans have 11
    for k in range(blades):
        a = 2 * math.pi * k / blades
        r_mid = radius * 0.64
        rot = Matrix.Rotation(a, 4, "X") @ Matrix.Rotation(math.radians(28), 4, "Z")
        center = (0.0, r_mid * math.cos(a + math.pi / 2), r_mid * math.sin(a + math.pi / 2))
        fan.box((0.0012, radius * 0.3, radius * 0.62), center=center, mat="FanBlack", rotation=rot)
    return fan.to_object(parent=parent, location=location)

def build_gpu_card():
    """EVGA GTX 1070 SC, ACX 3.0: black shroud, two fans, black backplate, lit logo."""
    root = empty("GPU_Card")
    L, H, T = CARD_L, CARD_H, CARD_T
    top = H / 2

    body = MeshBuilder("Body")
    body.box((0.026, L, H - 0.004), center=(-0.004, 0, 0.0), mat="Shroud", bevel=0.004)
    # Grey accent strips across the fan face, between and outside the fans.
    for y in (-0.118, 0.0, 0.118):
        body.box((0.0012, 0.010, H - 0.02), center=(-0.0176, y, 0.0), mat="ShroudGrey")
    # Fin stack showing along the top edge at the back, with three nickel heat pipes.
    body.box((0.020, 0.140, 0.003), center=(-0.004, 0.055, top - 0.0005), mat="Fins")
    for x in (-0.010, -0.004, 0.002):
        body.cylinder(radius=0.003, depth=0.050, center=(x, 0.075, top + 0.002), axis="Y", mat="Steel", segments=8)
    body.box((0.0016, L - 0.004, H - 0.006), center=(0.0098, 0, 0), mat="PCB")
    body.box((0.0018, L - 0.010, H - 0.010), center=(0.0116, 0.003, 0.0), mat="Backplate")
    body.text("EVGA", 0.022, center=(0.0128, 0.03, 0.0), mat="ShroudGrey",
              rotation=Matrix.Rotation(math.radians(90), 4, "Y") @ Matrix.Rotation(math.radians(90), 4, "Z"))
    body.box((0.0016, 0.090, 0.008), center=(0.0098, -0.040, -top - 0.004), mat="Gold")         # PCIe fingers

    # I/O bracket at the front, with DVI, HDMI and three DisplayPorts, and the white
    # dummy plug every card in the rig has in its HDMI port.
    yb = -L / 2 - 0.0006
    body.box((0.020, 0.0012, 0.120), center=(0.006, yb, -0.004), mat="Steel")
    body.box((0.010, 0.0012, 0.012), center=(0.006, yb, -top - 0.012), mat="Steel")               # screw tab
    body.box((0.009, 0.004, 0.030), center=(0.004, yb - 0.002, 0.030), mat="BlackPlastic")        # DVI
    body.box((0.006, 0.004, 0.014), center=(0.004, yb - 0.002, 0.004), mat="BlackPlastic")        # HDMI
    for z in (-0.014, -0.030, -0.046):
        body.box((0.006, 0.004, 0.012), center=(0.004, yb - 0.002, z), mat="BlackPlastic")       # DP
    body.box((0.009, 0.022, 0.016), center=(0.004, yb - 0.013, 0.004), mat="DummyWhite")         # dummy plug

    cx, cy = CARD_CONNECTOR
    body.box((0.010, 0.020, 0.008), center=(cx, cy, top + 0.004), mat="BlackPlastic")             # 8 pin socket
    body.to_object(parent=root)

    # The lit logo along the top edge: the GeForce light bar at the front and the EVGA
    # lettering behind it. Both glow with the card's load in Unity.
    led = MeshBuilder("LED")
    led.box((0.005, 0.070, 0.0015), center=(-0.004, -0.085, top + 0.0005), mat="LED")
    led.text("EVGA", 0.016, center=(-0.004, -0.018, top + 0.0005), mat="LED",
             rotation=Matrix.Rotation(math.radians(90), 4, "Z"), depth=0.001)
    led.to_object(parent=root)

    fan_r = 0.043
    for i, y in enumerate((-0.060, 0.060)):
        build_fan("Fan%d" % i, fan_r, root, (-0.0185, y, 0.0))
    front_marker(root, (0, -L / 2, 0))
    return root, (T, L, H)

def build_psu():
    """Antec HCP 1300 Platinum, lying flat, lettered side to the front."""
    root = empty("PSU")
    W, D, H = 0.200, 0.150, 0.086           # long side along the frame, 150 deep, 86 tall
    body = MeshBuilder("Body")
    body.box((W, D, H), mat="PSUBody", bevel=0.003)
    face = Matrix.Rotation(math.radians(90), 4, "X")            # text standing on the -Y face
    yf = -D / 2 - 0.0003
    body.text("Antec", 0.040, center=(-0.030, yf, 0.010), mat="AntecGold", rotation=face, depth=0.0008)
    body.text("1300", 0.026, center=(0.060, yf, 0.016), mat="Label", rotation=face, depth=0.0008)
    body.text("HCP Platinum", 0.011, center=(0.050, yf, -0.018), mat="Label", rotation=face, depth=0.0006)
    # Modular sockets on one end, the mains socket and switch on the other.
    for k in range(3):
        for j in range(2):
            body.box((0.002, 0.018, 0.010), center=(W / 2 + 0.001, -0.045 + 0.03 * k, -0.018 + 0.022 * j), mat="BlackPlastic")
    body.box((0.002, 0.030, 0.022), center=(-W / 2 - 0.001, 0.030, 0.0), mat="BlackPlastic")
    body.to_object(parent=root)

    grille = MeshBuilder("Grille")
    grille.ring(outer=0.066, inner=0.062, depth=0.003, mat="Steel", segments=40)
    grille.ring(outer=0.042, inner=0.039, depth=0.003, mat="Steel", segments=32)
    grille.cylinder(radius=0.016, depth=0.003, mat="Steel", segments=16)
    for k in range(4):
        grille.box((0.002, 0.128, 0.002), center=(0, 0, 0), mat="Steel",
                   rotation=Matrix.Rotation(math.pi * k / 4, 4, "X"))
    grille_obj = grille.to_object(parent=root, location=(0, 0.0, H / 2 + 0.0015))
    grille_obj.rotation_euler = (0, math.radians(-90), 0)       # faces up
    front_marker(root, (0, -D / 2, 0))
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
    body.box((0.040, 0.030, 0.020), center=(-0.10, 0.07, 0.011), mat="Fins")                     # VRM heatsink
    body.box((0.030, 0.030, 0.012), center=(0.05, -0.08, 0.007), mat="Fins")                     # chipset
    body.box((0.012, 0.052, 0.012), center=(0.145, 0.03, 0.0068), mat="BlackPlastic")            # 24 pin
    body.box((0.160, 0.008, 0.040), center=(-0.06, D / 2 - 0.004, 0.021), mat="Steel")           # I/O shield
    body.to_object(parent=root)
    return root, (W, D, T)

def build_cpu_cooler():
    """Intel stock cooler: a round aluminium fin stack round a copper core, fan on top."""
    root = empty("CPU_Cooler")
    R, H = 0.045, 0.045
    body = MeshBuilder("Body")
    body.cylinder(radius=R * 0.92, depth=0.030, center=(0, 0, -H / 2 + 0.015), axis="Z", mat="Aluminium", segments=24)
    body.cylinder(radius=0.012, depth=0.031, center=(0, 0, -H / 2 + 0.015), axis="Z", mat="Copper", segments=12)
    body.cylinder(radius=R, depth=0.004, center=(0, 0, -H / 2 + 0.032), axis="Z", mat="BlackPlastic", segments=24)
    body.to_object(parent=root)
    fan = build_fan("Fan0", R - 0.002, root, (0, 0, H / 2 - 0.005))
    fan.rotation_euler = (0, math.radians(-90), 0)               # spins about the vertical
    return root, (2 * R, 2 * R, H)

def build_ram():
    root = empty("RAM")
    L, H, T = 0.133, 0.031, 0.007
    body = MeshBuilder("Body")
    body.box((0.001, L, H), mat="PCB")
    body.box((T, L - 0.004, H - 0.004), center=(0, 0, 0.002), mat="BlackPlastic", bevel=0.001)   # heat spreader
    body.box((0.0012, L - 0.01, 0.003), center=(0, 0, -H / 2 + 0.0015), mat="Gold")
    body.to_object(parent=root)
    return root, (T, L, H)

def build_riser():
    """USB riser board: x16 slot, blue USB 3 socket, 6 pin power, a few capacitors."""
    root = empty("Riser")
    W, D, T = 0.040, 0.100, 0.0016
    body = MeshBuilder("Body")
    body.box((W, D, T), mat="PCB")
    body.box((0.0075, 0.089, 0.011), center=(0, 0, 0.0063), mat="BlackPlastic")                 # x16 slot
    body.box((0.013, 0.014, 0.007), center=(0.012, 0.040, 0.0043), mat="UsbBlue")                # USB 3
    body.box((0.010, 0.014, 0.010), center=(-0.012, 0.040, 0.0058), mat="BlackPlastic")          # 6 pin
    for k in range(3):
        body.cylinder(radius=0.003, depth=0.008, center=(-0.013, -0.02 + 0.012 * k, 0.0048), axis="Z", mat="Steel", segments=8)
    body.to_object(parent=root)
    return root, (W, D, T)

# Frame, in its own centred space. The cards stand on the upper level with their
# bottoms CARD_TIER above the frame's feet; the motherboard and supplies sit on the
# lower level.
FRAME_W, FRAME_D, FRAME_H, FRAME_TUBE = 0.80, 0.40, 0.30, 0.020
CARD_TIER = 0.18
RISER_Y = -0.085                        # the riser rail, under the cards' PCIe fingers

def build_frame():
    """Two level open air frame in black square aluminium tube."""
    root = empty("Frame")
    W, D, H, t = FRAME_W, FRAME_D, FRAME_H, FRAME_TUBE
    body = MeshBuilder("Body")
    base = -H / 2
    zb, zt = base + t / 2, H / 2 - t / 2
    card_bottom = base + CARD_TIER
    z_tier = card_bottom - 0.013 - t / 2          # rail tops meet the riser boards' undersides
    z_bar = card_bottom + CARD_H - 0.02           # the bar the card brackets screw to
    xs = (-W / 2 + t / 2, W / 2 - t / 2)
    ys = (-D / 2 + t / 2, D / 2 - t / 2)
    # Pieces butt up against each other rather than overlapping. Overlapping boxes put two
    # faces in the same place, and the renderer flickers between them.
    for x in xs:
        for y in ys:
            body.box((t, t, H), center=(x, y, 0), mat="FrameBlack")                     # posts
    for z in (zb, z_tier):
        for y in ys:
            body.box((W - 2 * t, t, t), center=(0, y, z), mat="FrameBlack")             # long rails
        for x in xs:
            body.box((t, D - 2 * t, t), center=(x, 0, z), mat="FrameBlack")             # end rails
    for y in (-0.10, 0.10):
        body.box((W - 2 * t, t, t), center=(0, y, zb), mat="FrameBlack")                # lower level supports
    body.box((W - 2 * t, t, t), center=(0, RISER_Y, z_tier), mat="FrameBlack")          # riser rail
    body.box((W - 2 * t, t, t), center=(0, ys[0], z_bar), mat="FrameBlack")             # bracket bar
    for x in xs:
        body.box((t, D - 2 * t, t), center=(x, 0, zt), mat="FrameBlack")                # top end rails
    body.to_object(parent=root)
    front_marker(root, (0, -D / 2, 0))
    return root, (W, D, H)

def card_row_x(i):
    return (i - (CARD_COUNT - 1) / 2) * CARD_SPACING

def bezier(p0, p1, p2, p3, steps):
    pts = []
    for k in range(steps + 1):
        u = k / steps
        a, b, c, d = (1 - u) ** 3, 3 * u * (1 - u) ** 2, 3 * u * u * (1 - u), u ** 3
        pts.append(tuple(a * p0[j] + b * p1[j] + c * p2[j] + d * p3[j] for j in range(3)))
    return pts

def build_pcie_cables():
    """
    The sleeved PCIe power cables: a pair from each card's 8 pin, arching up and back
    over the frame's rear rail. One part, so the "PCIe power" step lays them all on at
    once. Built around the middle of the card row, card centres at y 0 z 0, then moved
    so the part is centred on its origin like every other part; the builder uses the
    printed offset to put it back.
    """
    root = empty("PCIe_Cables")
    cables = MeshBuilder("Body")
    cx, cy = CARD_CONNECTOR
    z0 = CARD_H / 2 + 0.008
    for i in range(CARD_COUNT):
        x = card_row_x(i) + cx
        lift = 0.07 + 0.012 * ((i * 5) % 3)                       # not every loop the same
        for dx, extra in ((-0.0045, 0.0), (0.0045, 0.012)):
            p0 = (x + dx, cy, z0)
            p1 = (x + dx, cy, z0 + lift + extra)
            p2 = (x + dx * 2, 0.23 + extra, z0 + lift * 0.9 + extra)
            p3 = (x + dx * 3, 0.26 + extra, z0 - 0.05)
            cables.tube(bezier(p0, p1, p2, p3, 12), radius=0.0042, mat="CableBlack")
    obj = cables.to_object(parent=root)
    xs = [v.co.x for v in obj.data.vertices]; ys = [v.co.y for v in obj.data.vertices]; zs = [v.co.z for v in obj.data.vertices]
    centre = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))
    size = (max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    obj.data.transform(Matrix.Translation(-centre))
    front_marker(root, (0, -size[1] / 2, 0))
    print(f"[rig parts] pcie_cables centre from card row middle: "
          f"x {centre.x:.4f}  along the card {centre.y:.4f}  up {centre.z:.4f}")
    return root, size

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
                               ("motherboard", build_motherboard), ("cpu_cooler", build_cpu_cooler),
                               ("ram_stick", build_ram), ("riser", build_riser),
                               ("frame", build_frame), ("pcie_cables", build_pcie_cables)):
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
