"""Style V8 trees for the RE:MAINS island: clustered rounded oak canopies, jagged drooping fir tiers, clump bushes.
Silhouettes stay low-poly, but foliage carries custom "volume" normals (pointing out of the canopy / clump centre),
the trick stylized-foliage artists use for soft painterly shading. Bark keeps flat facets.
Rebuilds Oak_0-2, Pine_0-2, Bush_0-1 with the same names, material slot order and normalised size, so every placed
instance in the Unity scene picks up the new mesh on reimport.
Run inside Blender via MCP: python3 output/siege-v6/bmcp.py run output/map-rework/build_trees_v8.py
"""
import bpy, bmesh, math, json, random
from pathlib import Path
from mathutils import Vector

ROOT = Path('/Users/k.shcheglov/Games/REMAINS'); OUT = ROOT / 'output/map-rework'; ASSETS = ROOT / 'Assets/Remains/MapRework/Models'
for c in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.collections):
    [c.remove(x) for x in list(c)]
rng = random.Random(20261013)
# Muted, painterly palette (sRGB-ish values; Unity materials are retinted separately).
PALETTE = {'Bark_Dark': (.20, .13, .08), 'Bark_Light': (.34, .23, .14),
           'Leaf_Dark': (.12, .22, .11), 'Leaf_Mid': (.25, .38, .15), 'Leaf_Light': (.47, .57, .23), 'Leaf_Autumn': (.62, .45, .14),
           'Pine_Dark': (.06, .15, .11), 'Pine_Mid': (.11, .24, .15), 'Pine_Light': (.21, .34, .17)}
MATS = {}
for n, c in PALETTE.items():
    m = bpy.data.materials.new('Nature_' + n); m.use_nodes = True; b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*c, 1); b.inputs['Roughness'].default_value = .9; m.diffuse_color = (*c, 1); MATS[n] = m


class Tree:
    """Accumulates closed shells; each face remembers its material slot and how its loop normals are made."""
    def __init__(self, name, slots):
        self.name, self.slots, self.bm, self.info = name, list(slots), bmesh.new(), []

    def add(self, verts, faces, mat_fn, normal_fn=None):
        vs = [self.bm.verts.new(v) for v in verts]
        for f in faces:
            face = self.bm.faces.new([vs[i] for i in f])
            co = [Vector(verts[i]) for i in f]; ctr = sum(co, Vector()) / len(co)
            nrm = (co[1] - co[0]).cross(co[2] - co[0]).normalized()
            face.material_index = self.slots.index(mat_fn(ctr, nrm)); self.info.append(normal_fn)

    def finish(self, height):
        bm = self.bm; zs = [v.co.z for v in bm.verts]; s = height / (max(zs) - min(zs))
        for v in bm.verts:
            v.co *= s
        me = bpy.data.meshes.new(self.name); bm.to_mesh(me); bm.free()
        for m in self.slots:
            me.materials.append(MATS[m])
        loops = []
        for p, fn in zip(me.polygons, self.info):
            p.use_smooth = fn is not None
            for li in p.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co / s
                loops.append(fn(co).normalized() if fn else p.normal.copy())
        me.normals_split_custom_set(loops)
        o = bpy.data.objects.new(self.name, me); bpy.context.collection.objects.link(o); return o


def ring_tube(rings, n, twist=0):
    """Closed tube through rings [(center, radius)], n sides; returns verts, faces."""
    v, f = [], []
    for k, (c, r) in enumerate(rings):
        for i in range(n):
            a = i * math.tau / n + twist * k
            v.append((c[0] + r * math.cos(a), c[1] + r * math.sin(a), c[2]))
    for k in range(len(rings) - 1):
        for i in range(n):
            j = (i + 1) % n; f.append((k * n + i, k * n + j, (k + 1) * n + j, (k + 1) * n + i))
    f.append(tuple(reversed(range(n)))); f.append(tuple((len(rings) - 1) * n + i for i in range(n)))
    return v, f


def limb(a, b, r0, r1, n=5):
    a, b = Vector(a), Vector(b); d = (b - a).normalized(); q = d.to_track_quat('Z', 'Y')
    v, f = ring_tube([((0, 0, 0), r0), ((0, 0, (b - a).length), r1)], n)
    return [tuple(a + q @ Vector(p)) for p in v], f


def clump(center, r, squash=.85, sub=2, rough=.07):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1)
    c = Vector(center); ph = rng.uniform(0, 9)
    v = []
    for x in bm.verts:
        p = x.co.normalized(); k = 1 + rough * math.sin(p.x * 5 + ph) * math.cos(p.y * 4 - ph) + rng.uniform(-rough, rough) * .5
        v.append(tuple(c + Vector((p.x * r * k, p.y * r * k, p.z * r * k * squash))))
    f = [tuple(x.index for x in face.verts) for face in bm.faces]; bm.free(); return v, f


def fix_winding(v, f):
    """Make every face point away from its shell centroid (all shells here are star-shaped)."""
    c = sum((Vector(p) for p in v), Vector()) / len(v); out = []
    for face in f:
        co = [Vector(v[i]) for i in face]; n = (co[1] - co[0]).cross(co[2] - co[0]); m = sum(co, Vector()) / len(co)
        out.append(face if n.dot(m - c) >= 0 else tuple(reversed(face)))
    return out


def volume_normal(canopy, clump_c, up=.18):
    return lambda p: (p - canopy).normalized() * .6 + (p - clump_c).normalized() * .4 + Vector((0, 0, up))


def make_oak(i):
    t = Tree(f'Oak_{i}', ['Bark_Dark', 'Bark_Light', 'Leaf_Dark', 'Leaf_Mid', 'Leaf_Light', 'Leaf_Autumn'])
    lean = Vector((rng.uniform(-.03, .03), rng.uniform(-.03, .03), 0)); th = rng.uniform(.30, .36)
    rings = [((0, 0, 0), .085), ((0, 0, .05), .062), ((lean.x * .5, lean.y * .5, th * .55), .05), ((lean.x, lean.y, th), .04)]
    v, f = ring_tube(rings, 7, .12); t.add(v, f, lambda c, n: 'Bark_Light' if n.x + n.y > .4 else 'Bark_Dark')
    top = Vector((lean.x, lean.y, th)); canopy = top + Vector((0, 0, .26))
    centers = [(canopy + Vector((0, 0, .06)), .2)]
    k = rng.randint(5, 7)
    for j in range(k):
        a = j * math.tau / k + rng.uniform(-.25, .25); d = rng.uniform(.15, .21); z = rng.uniform(-.1, .1)
        centers.append((canopy + Vector((math.cos(a) * d, math.sin(a) * d, z)), rng.uniform(.12, .16)))
    for c, r in centers[1:4]:
        base = top + Vector((0, 0, -.04)); v, f = limb(base, base.lerp(c, .75), .028, .014); t.add(v, f, lambda c_, n: 'Bark_Dark')
    autumn = .14 if i == 2 else 0
    z0 = min(c.z - r for c, r in centers); z1 = max(c.z + r for c, r in centers)
    for c, r in centers:
        v, f = clump(c, r); f = fix_winding(v, f)
        def mat(ctr, n, z0=z0, z1=z1):
            h = (ctr.z - z0) / (z1 - z0) + n.z * .08
            if autumn and rng.random() < autumn: return 'Leaf_Autumn'
            return 'Leaf_Light' if h > .66 and n.z > .1 else ('Leaf_Dark' if h < .34 or n.z < -.45 else 'Leaf_Mid')
        t.add(v, f, mat, volume_normal(canopy, c))
    return t.finish(rng.uniform(.78, .86))


def tier(z, h, r, lean, n=12, droop=.05):
    """Jagged fir tier: apex, star-shaped drooping rim, concave underside. Closed."""
    v = [(lean[0], lean[1], z + h)]; off = rng.uniform(0, math.tau)
    for i in range(n):
        a = i * math.tau / n + off; rr = r * (1 if i % 2 == 0 else .74) * rng.uniform(.93, 1.07)
        dz = -droop * (rr / r) + rng.uniform(-.01, .01)
        v.append((rr * math.cos(a), rr * math.sin(a), z + dz))
    v.append((0, 0, z + h * .22))
    f = [(0, 1 + i, 1 + (i + 1) % n) for i in range(n)] + [(n + 1, 1 + (i + 1) % n, 1 + i) for i in range(n)]
    return v, fix_winding(v, f)


def make_pine(i):
    t = Tree(f'Pine_{i}', ['Bark_Dark', 'Bark_Light', 'Pine_Dark', 'Pine_Mid', 'Pine_Light'])
    v, f = ring_tube([((0, 0, 0), .05), ((0, 0, .05), .04), ((0, 0, .8), .018)], 6, .2)
    t.add(v, f, lambda c, n: 'Bark_Light' if n.x > .3 else 'Bark_Dark')
    tiers = rng.choice((4, 5)); z = .17; r = rng.uniform(.31, .35); h = .30
    for k in range(tiers):
        lean = (rng.uniform(-.012, .012), rng.uniform(-.012, .012)); v, f = tier(z, h, r, lean, 12 if k < 3 else 10, .05 * r / .3)
        apex = Vector((lean[0], lean[1], z + h)); up_k = k / max(tiers - 1, 1)
        def mat(c, n, z=z, h=h, up_k=up_k):
            if n.z < 0: return 'Pine_Dark'
            s = (c.z - z) / h * 1.4 + up_k * .3 + (n.x + n.y) * .18
            return 'Pine_Light' if s > .62 else ('Pine_Dark' if s < .2 else 'Pine_Mid')
        def nf(p, z=z, apex=apex):
            radial = Vector((p.x - apex.x, p.y - apex.y, 0))
            return radial.normalized() * .75 + Vector((0, 0, .9)) if p.z > z + .02 or radial.length > .05 else radial.normalized() * .6 + Vector((0, 0, -.25))
        t.add(v, f, mat, nf)
        z += h * rng.uniform(.5, .56); r *= .78; h *= .93
    return t.finish(1.0)


def make_bush(i):
    t = Tree(f'Bush_{i}', ['Leaf_Dark', 'Leaf_Mid', 'Leaf_Light'])
    centre = Vector((0, 0, .1))
    for k in range(rng.randint(3, 5)):
        c = Vector((rng.uniform(-.16, .16), rng.uniform(-.14, .14), .14 + rng.uniform(0, .06))); r = rng.uniform(.17, .25)
        v, f = clump(c, r, .8, 2, .09); v = [(x, y, max(z, 0)) for x, y, z in v]; f = fix_winding(v, f)
        t.add(v, f, lambda ctr, n: 'Leaf_Light' if n.z > .45 else ('Leaf_Dark' if n.z < -.05 or ctr.z < .06 else 'Leaf_Mid'),
              volume_normal(centre, c, .25))
    return t.finish(rng.uniform(.36, .42))


def validate(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    r = dict(name=o.name, tris=sum(len(f.verts) - 2 for f in bm.faces), non_manifold=sum(1 for e in bm.edges if not e.is_manifold),
             loose=sum(1 for v in bm.verts if not v.link_faces), degenerate=sum(1 for f in bm.faces if f.calc_area() < 1e-8),
             slots_used=sorted({f.material_index for f in bm.faces}), slots=[m.name for m in o.data.materials],
             size=[round(x, 3) for x in o.dimensions])
    bm.free(); need = len(r['slots']) - (1 if o.name.startswith('Oak') and o.name != 'Oak_2' else 0)
    r['ok'] = r['non_manifold'] == 0 and r['loose'] == 0 and r['degenerate'] == 0 and r['slots_used'] == list(range(need)); return r


objs = [make_oak(i) for i in range(3)] + [make_pine(i) for i in range(3)] + [make_bush(i) for i in range(2)]
report = []
for o in objs:
    report.append(validate(o)); bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.gltf(filepath=str(ASSETS / (o.name + '.glb')), use_selection=True, export_format='GLB', export_apply=True,
                              export_yup=True, export_normals=True, export_materials='EXPORT')
(OUT / 'trees_v8_validation.json').write_text(json.dumps(report, indent=1))
x = 0
for o in objs:
    s = 2.4 if o.name.startswith(('Pine', 'Oak')) else 1.8; o.scale = (s, s, s); bpy.context.view_layer.update()
    o.location = (x + o.dimensions.x / 2, 0, 0); x += o.dimensions.x + .3
cd = bpy.data.cameras.new('Cam'); cd.type = 'ORTHO'; cd.ortho_scale = x * 1.04; cam = bpy.data.objects.new('Cam', cd); bpy.context.collection.objects.link(cam)
cam.location = (x / 2, -30, 7.4); cam.rotation_euler = (math.radians(78), 0, 0)
sc = bpy.context.scene; sc.camera = cam; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
sc.display.shading.show_cavity = False; sc.render.resolution_x = 2400; sc.render.resolution_y = 560
if not sc.world: sc.world = bpy.data.worlds.new('W')
sc.world.color = (.78, .84, .88); sc.render.filepath = str(OUT / 'trees_v8_preview.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'trees_v8.blend'))
print('TREES_V8', json.dumps([(r['name'], r['ok'], r['tris'], r['size']) for r in report]))
