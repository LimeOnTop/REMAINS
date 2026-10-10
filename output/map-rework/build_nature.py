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


# Natural rocks (v2): a noise-displaced icosphere with soft horizontal strata, decimated to large facets.
# Separate RNG so the trees keep their shapes when the rocks change.
rrng = random.Random(4127)
from mathutils import noise


def natural_shell(size, strata=0.0, ratio=.1, subdiv=3, rough=.28, flat_bottom=.8, face=1.0, top=1.0):
    sx, sy, sz = size
    off = Vector((rrng.uniform(0, 100), rrng.uniform(0, 100), rrng.uniform(0, 100)))
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    lean = Vector((rrng.uniform(-.15, .15), rrng.uniform(-.1, .1), 0))
    for v in bm.verts:
        d = v.co.normalized()
        n = noise.fractal(d * 1.3 + off, 1.0, 2.0, 4)
        n2 = noise.noise(d * 3.1 + off * .5)
        r = 1 + rough * n + .08 * n2
        p = Vector((d.x * sx * r, d.y * sy * r, d.z * sz * r))
        if strata > 0:  # terraces: pull heights toward bands, stronger on the steep sides
            band = sz * .32
            q = round(p.z / band) * band
            side = 1 - abs(d.z)
            p.z = p.z + (q - p.z) * strata * side
        p += lean * (p.z / sz)
        p.z = max(p.z, -sz * flat_bottom)
        # Sheer front/back faces and a flattened crown read as broken rock rather than a pebble.
        lim = sy * face * (1 + .12 * noise.noise(Vector((p.x, p.z, 7)) * 1.7 + off))
        p.y = max(-lim, min(lim, p.y))
        p.z = min(p.z, sz * top * (1 + .1 * noise.noise(Vector((p.x, p.y, 3)) * 2 + off)))
        v.co = p
    me = bpy.data.meshes.new('tmp_rock')
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new('tmp_rock', me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new('dec', 'DECIMATE')
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    out = bmesh.new()
    out.from_mesh(ev.to_mesh())
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    bmesh.ops.remove_doubles(out, verts=list(out.verts), dist=1e-4)
    bmesh.ops.dissolve_degenerate(out, edges=list(out.edges), dist=1e-4)
    bmesh.ops.recalc_face_normals(out, faces=list(out.faces))
    return out


def shell_ok(bm):
    return all(e.is_manifold for e in bm.edges) and all(f.calc_area() > 1e-6 for f in bm.faces)


def good_shell(*a, **k):
    for _ in range(8):
        bm = natural_shell(*a, **k)
        if shell_ok(bm):
            return bm
        bm.free()
    return natural_shell(*a, **dict(k, ratio=.2))


def make_cliff(i):
    """Massive stratified cliff block about 2 wide, 1.2 deep, 2.3 tall (bottom at z=0)."""
    b = Builder(f'Cliff_Rock_{i}')
    sh = good_shell((1.15, .75, 1.25), strata=.85, ratio=.17, rough=.33, face=.72, top=.8)
    bmesh.ops.translate(sh, verts=list(sh.verts), vec=(0, 0, 1.15))
    b.add(sh, ROCK_SET, by_height=rock_mats)
    if i % 2 == 0:  # a lower shoulder block fused to one side
        sh2 = good_shell((.65, .55, .6), strata=.7, ratio=.2, rough=.3, face=.75, top=.8)
        bmesh.ops.translate(sh2, verts=list(sh2.verts), vec=(rrng.choice((-.7, .7)), rrng.uniform(-.1, .15), .45))
        b.add(sh2, ROCK_SET, by_height=rock_mats)
    return b


def make_boulder(i):
    b = Builder(f'Boulder_{i}')
    sx, sy, sz = rrng.uniform(.5, .62), rrng.uniform(.42, .52), rrng.uniform(.3, .4)
    b.add(good_shell((sx, sy, sz), strata=.3, ratio=.16, rough=.3, flat_bottom=.7, face=.85, top=.85), ROCK_SET, by_height=rock_mats)
    if i % 2 == 1:
        sh = good_shell((.22, .2, .17), ratio=.2, subdiv=2, rough=.25, flat_bottom=.6)
        bmesh.ops.translate(sh, verts=list(sh.verts), vec=(sx * .9, 0, -sz * .35))
        b.add(sh, ROCK_SET, by_height=rock_mats)
    return b


def make_stone(i):
    b = Builder(f'Stone_{i}')
    b.add(good_shell((.5, .4, .25), ratio=.25, subdiv=2, rough=.25, flat_bottom=.55), ROCK_SET, weights=[1, 3, 2, 0])
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
