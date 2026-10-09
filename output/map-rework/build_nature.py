"""Low-poly nature kit for the RE:MAINS island map: cliff rocks, boulders, stones, pines, oaks, bushes.
Every shell is a convex hull or a closed primitive, so meshes are watertight with outward normals.
Run: /Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python build_nature.py
Blender is Z-up, metres; glTF export converts to Unity's Y-up.
"""
import bpy, bmesh, math, json, random
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path('/Users/k.shcheglov/Games/REMAINS')
OUT = ROOT / 'output/map-rework'
ASSETS = ROOT / 'Assets/Remains/MapRework/Models'
OUT.mkdir(parents=True, exist_ok=True)
ASSETS.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
rng = random.Random(20261010)

# Shared palette: same warm stone tones as the castle masonry, saturated but muted greens.
PALETTE = {
    'Rock_Shadow': (.30, .29, .26), 'Rock_Mid': (.44, .42, .37), 'Rock_Light': (.58, .55, .48), 'Rock_Moss': (.30, .36, .17),
    'Bark_Dark': (.22, .13, .07), 'Bark_Light': (.33, .21, .11),
    'Pine_Dark': (.05, .14, .07), 'Pine_Mid': (.08, .21, .09), 'Pine_Light': (.13, .29, .11),
    'Leaf_Dark': (.11, .22, .05), 'Leaf_Mid': (.18, .33, .08), 'Leaf_Light': (.29, .43, .11), 'Leaf_Autumn': (.50, .38, .09),
}
MATS = {}
for name, c in PALETTE.items():
    m = bpy.data.materials.new('Nature_' + name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*c, 1)
    bsdf.inputs['Roughness'].default_value = .92
    m.diffuse_color = (*c, 1)
    MATS[name] = m


def hull(points):
    """Convex hull of a point cloud as a bmesh-ready (verts, faces) pair."""
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    # Remove interior points that the hull left unattached.
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return bm


def ellipsoid_cloud(n, rx, ry, rz, jitter=.18, flat_bottom=None, center=(0, 0, 0)):
    pts = []
    for _ in range(n):
        u, v = rng.uniform(-1, 1), rng.uniform(0, math.tau)
        s = math.sqrt(1 - u * u)
        k = 1 + rng.uniform(-jitter, jitter)
        p = Vector((s * math.cos(v) * rx * k, s * math.sin(v) * ry * k, u * rz * k)) + Vector(center)
        if flat_bottom is not None:
            p.z = max(p.z, flat_bottom)
        pts.append(p)
    return pts


class Builder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.slots = []

    def slot(self, mat):
        if mat not in self.slots:
            self.slots.append(mat)
        return self.slots.index(mat)

    def add(self, shell, mats, weights=None, by_height=None):
        """Merge a closed shell; faces get palette materials (random or by height band)."""
        idx = [self.slot(m) for m in mats]
        tmp = bpy.data.meshes.new('tmp')
        shell.to_mesh(tmp)
        shell.free()
        zs = [v.co.z for v in tmp.vertices]
        z0, z1 = min(zs), max(zs)
        offset = len(self.bm.verts)
        vmap = [self.bm.verts.new(v.co) for v in tmp.vertices]
        self.bm.verts.ensure_lookup_table()
        for poly in tmp.polygons:
            f = self.bm.faces.new([vmap[i] for i in poly.vertices])
            if by_height:
                cz = sum(tmp.vertices[i].co.z for i in poly.vertices) / len(poly.vertices)
                t = (cz - z0) / max(z1 - z0, 1e-4)
                f.material_index = idx[by_height(t, poly.normal)]
            else:
                f.material_index = rng.choices(idx, weights=weights)[0]
        bpy.data.meshes.remove(tmp)

    def finish(self, collection):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.slots:
            mesh.materials.append(MATS[m])
        for p in mesh.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(self.name, mesh)
        collection.objects.link(obj)
        return obj


def cylinder_shell(r0, r1, h, n=6, z=0, twist=0):
    bm = bmesh.new()
    lo = [bm.verts.new((r0 * math.cos(i * math.tau / n + twist), r0 * math.sin(i * math.tau / n + twist), z)) for i in range(n)]
    hi = [bm.verts.new((r1 * math.cos(i * math.tau / n + twist + .3), r1 * math.sin(i * math.tau / n + twist + .3), z + h)) for i in range(n)]
    bm.faces.new(list(reversed(lo)))
    bm.faces.new(hi)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([lo[i], lo[j], hi[j], hi[i]])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return bm


def cone_shell(r, h, z, n=7, lean=(0, 0)):
    """Closed jittered cone: one pine tier."""
    pts = []
    for i in range(n):
        a = i * math.tau / n + rng.uniform(-.15, .15)
        rr = r * rng.uniform(.88, 1.1)
        pts.append(Vector((rr * math.cos(a), rr * math.sin(a), z + rng.uniform(-.08, .12) * h)))
    pts.append(Vector((lean[0], lean[1], z + h)))
    return hull(pts)


def rock_mats(t, normal):
    if normal.z > .72 and t > .55:
        return 3 if rng.random() < .55 else 2  # moss or light top
    if t < .25:
        return 0
    return rng.choice([0, 1, 1, 2])


ROCK_SET = ['Rock_Shadow', 'Rock_Mid', 'Rock_Light', 'Rock_Moss']


def make_cliff(i):
    """Tall stratified cliff chunk ~ 1 x 1 x 1 unit-ish, scaled in Unity to fit banks."""
    """Angular cliff chunk: 2-4 tilted blocky columns side by side, ~2 wide x 1 deep x 2 tall."""
    b = Builder(f'Cliff_Rock_{i}')
    cols = rng.randint(2, 4)
    x = -1.0
    for k in range(cols):
        w = 2.0 / cols * rng.uniform(.9, 1.25)
        d = rng.uniform(.6, 1.0)
        h = rng.uniform(1.4, 2.3)
        cx, cy = x + w / 2, rng.uniform(-.12, .12)
        tilt = rng.uniform(-.12, .12)
        pts = []
        for zz, s in ((0, 1.0), (h * rng.uniform(.45, .6), rng.uniform(.95, 1.08)), (h, rng.uniform(.6, .85))):
            for sx in (-1, 1):
                for sy in (-1, 1):
                    pts.append(Vector((cx + sx * w / 2 * s * rng.uniform(.8, 1.05) + tilt * zz, cy + sy * d / 2 * s * rng.uniform(.75, 1.05),
                                       zz + (rng.uniform(-.18, .1) if zz else 0))))
        pts.append(Vector((cx + rng.uniform(-.2, .2), cy + rng.uniform(-.1, .1), h + rng.uniform(.0, .2))))
        b.add(hull(pts), ROCK_SET, by_height=rock_mats)
        x += w * .82
    return b


def make_boulder(i):
    b = Builder(f'Boulder_{i}')
    sx, sy, sz = rng.uniform(.9, 1.2), rng.uniform(.7, 1.0), rng.uniform(.55, .8)
    b.add(hull(ellipsoid_cloud(rng.randint(14, 22), sx * .5, sy * .5, sz * .5, .22, flat_bottom=-sz * .32)), ROCK_SET, by_height=rock_mats)
    if i % 2 == 1:  # a smaller split-off chunk leaning on the main stone
        b.add(hull(ellipsoid_cloud(12, .22, .2, .18, .2, center=(sx * .45, 0, -sz * .2), flat_bottom=-sz * .32)), ROCK_SET, by_height=rock_mats)
    return b


def make_stone(i):
    b = Builder(f'Stone_{i}')
    b.add(hull(ellipsoid_cloud(9, .5, .4, .28, .25, flat_bottom=-.12)), ROCK_SET, weights=[1, 3, 2, 0])
    return b


def make_pine(i):
    b = Builder(f'Pine_{i}')
    H = 1.0  # normalised; Unity scales to 8-15 m
    trunk_h = rng.uniform(.18, .24)
    b.add(cylinder_shell(.045, .028, trunk_h + .1, 6, 0, rng.random()), ['Bark_Dark', 'Bark_Light'], weights=[2, 1])
    tiers = rng.randint(3, 4)
    z = trunk_h
    r = rng.uniform(.30, .36)
    for k in range(tiers):
        h = (H - trunk_h) / tiers * 1.45
        tone = ['Pine_Dark', 'Pine_Mid', 'Pine_Light']
        lean = (rng.uniform(-.015, .015), rng.uniform(-.015, .015))
        b.add(cone_shell(r, h, z, rng.randint(6, 8), lean), tone, by_height=lambda t, n, k=k: 2 if (n.z > .35 and t > .4) else (0 if t < .25 else 1))
        z += h * .58
        r *= .74
    return b


def make_oak(i):
    b = Builder(f'Oak_{i}')
    trunk_h = rng.uniform(.26, .32)
    b.add(cylinder_shell(.06, .04, trunk_h + .08, 6, 0, rng.random()), ['Bark_Dark', 'Bark_Light'], weights=[2, 1])
    # Two short limbs as tapered prisms leaning out.
    for k in range(2):
        a = rng.uniform(0, math.tau)
        base = Vector((0, 0, trunk_h * .75))
        tip = base + Vector((math.cos(a) * .16, math.sin(a) * .16, .16))
        pts = [base + Vector((math.cos(j * math.tau / 5) * .03, math.sin(j * math.tau / 5) * .03, 0)) for j in range(5)]
        pts += [tip + Vector((math.cos(j * math.tau / 5) * .015, math.sin(j * math.tau / 5) * .015, 0)) for j in range(5)]
        b.add(hull(pts), ['Bark_Dark', 'Bark_Light'], weights=[1, 1])
    leaves = ['Leaf_Dark', 'Leaf_Mid', 'Leaf_Light', 'Leaf_Autumn']
    autumn = .12 if i == 2 else 0
    blobs = rng.randint(4, 6)
    for k in range(blobs):
        a = k * math.tau / blobs + rng.uniform(-.3, .3)
        d = rng.uniform(.12, .2) if k else 0
        rr = rng.uniform(.17, .24) if k else .27
        c = (math.cos(a) * d, math.sin(a) * d, trunk_h + .2 + rng.uniform(-.04, .14) + (.08 if k == 0 else 0))
        b.add(hull(ellipsoid_cloud(16, rr, rr, rr * .82, .15, center=c)), leaves,
              by_height=lambda t, n: 3 if rng.random() < autumn else (2 if n.z > .45 else (0 if n.z < -.2 else 1)))
    return b


def make_bush(i):
    b = Builder(f'Bush_{i}')
    leaves = ['Leaf_Dark', 'Leaf_Mid', 'Leaf_Light']
    for k in range(rng.randint(2, 4)):
        c = (rng.uniform(-.25, .25), rng.uniform(-.25, .25), .22)
        rr = rng.uniform(.22, .32)
        b.add(hull(ellipsoid_cloud(12, rr, rr, rr * .75, .2, center=c, flat_bottom=0)), leaves,
              by_height=lambda t, n: 2 if n.z > .5 else (0 if n.z < 0 else 1))
    return b


def validate(obj):
    """Artifact report: non-manifold edges, loose geometry, degenerate faces, inward shells."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    non_manifold = sum(1 for e in bm.edges if not e.is_manifold)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    loose_v = sum(1 for v in bm.verts if not v.link_faces)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-7)
    # Each connected shell must enclose positive volume (normals outward).
    shells, seen = [], set()
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, shell = [f], []
        seen.add(f.index)
        while stack:
            g = stack.pop()
            shell.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        shells.append(shell)
    inward = 0
    for shell in shells:
        vol = 0
        for f in shell:
            vs = [v.co for v in f.verts]
            for k in range(1, len(vs) - 1):
                vol += vs[0].dot(vs[k].cross(vs[k + 1])) / 6
        if vol <= 0:
            inward += 1
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    bm.free()
    return dict(name=obj.name, triangles=tris, shells=len(shells), non_manifold_edges=non_manifold,
                boundary_edges=boundary, loose_verts=loose_v, degenerate_faces=degenerate, inward_shells=inward,
                ok=non_manifold == 0 and boundary == 0 and loose_v == 0 and degenerate == 0 and inward == 0)


col = bpy.data.collections.new('Nature')
bpy.context.scene.collection.children.link(col)
makers = [(make_cliff, 4), (make_boulder, 4), (make_stone, 3), (make_pine, 3), (make_oak, 3), (make_bush, 2)]
objs = []
for maker, count in makers:
    for i in range(count):
        objs.append(maker(i).finish(col))

report = []
for o in objs:
    r = validate(o)
    report.append(r)
    # Export each model on its own, origin at base centre.
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.gltf(filepath=str(ASSETS / (o.name + '.glb')), use_selection=True, export_format='GLB',
                              export_apply=True, export_yup=True, export_normals=True, export_materials='EXPORT')
(OUT / 'nature_validation.json').write_text(json.dumps(report, indent=1))

# Preview sheet: line the models up and render with flat workbench shading.
x = 0
for o in objs:
    s = 2.4 if o.name.startswith(('Pine', 'Oak')) else (0.9 if o.name.startswith('Cliff') else (1.8 if o.name.startswith('Bush') else 1.0))
    o.scale = (s, s, s)
    dims = o.dimensions
    o.location = (x + dims.x / 2, 0, 0)
    x += dims.x + .35
cam_data = bpy.data.cameras.new('Cam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = x * 1.02
cam = bpy.data.objects.new('Cam', cam_data)
bpy.context.scene.collection.objects.link(cam)
cam.location = (x / 2, -30, 1.4 + 6)
cam.rotation_euler = (math.radians(78), 0, 0)
scene = bpy.context.scene
scene.camera = cam
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_cavity = True
scene.display.shading.show_object_outline = False
scene.render.resolution_x = 2400
scene.render.resolution_y = 520
scene.render.film_transparent = False
world = bpy.data.worlds.new('W')
scene.world = world
world.color = (.78, .84, .88)
scene.render.filepath = str(OUT / 'nature_kit_preview.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'nature_kit.blend'))
print('NATURE_DONE', json.dumps([r['name'] + (':ok' if r['ok'] else ':FAIL') for r in report]))
