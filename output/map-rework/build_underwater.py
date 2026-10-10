"""Low-poly underwater kit for the RE:MAINS river: kelp clumps, starfish and small fish.
Each part is a closed shell (convex hull, tapered prism or cone), validated like the nature kit.
Orientation for Unity: Blender +Z = up; fish face Blender -Y, which becomes Unity +Z (forward).
Run: Blender --background --factory-startup --python build_underwater.py
"""
import bpy, bmesh, math, json, random
from pathlib import Path
from mathutils import Vector

ROOT = Path('/Users/k.shcheglov/Games/REMAINS')
OUT = ROOT / 'output/map-rework'
ASSETS = ROOT / 'Assets/Remains/MapRework/Models'
bpy.ops.wm.read_factory_settings(use_empty=True)
rng = random.Random(77)

PALETTE = {
    'Kelp_Dark': (.06, .16, .05), 'Kelp_Mid': (.13, .28, .07), 'Kelp_Light': (.26, .38, .09), 'Kelp_Red': (.32, .09, .07),
    'Star_Orange': (.85, .28, .05), 'Star_Coral': (.80, .16, .14), 'Star_Purple': (.30, .10, .45), 'Star_Dots': (.95, .80, .55),
    'Fish_Yellow': (.95, .62, .05), 'Fish_Blue': (.04, .22, .65), 'Fish_Orange': (.95, .30, .03), 'Fish_White': (.88, .88, .84),
    'Fish_Teal': (.03, .45, .45), 'Fish_Dark': (.03, .04, .06),
}
MATS = {}
for name, c in PALETTE.items():
    m = bpy.data.materials.new('Sea_' + name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*c, 1)
    b.inputs['Roughness'].default_value = .6
    m.diffuse_color = (*c, 1)
    MATS[name] = m


class Builder:
    def __init__(self, name):
        self.name, self.bm, self.slots = name, bmesh.new(), []

    def add(self, shell, mat_of_face):
        tmp = bpy.data.meshes.new('tmp')
        shell.to_mesh(tmp)
        shell.free()
        vmap = [self.bm.verts.new(v.co) for v in tmp.vertices]
        for poly in tmp.polygons:
            f = self.bm.faces.new([vmap[i] for i in poly.vertices])
            c = sum((tmp.vertices[i].co for i in poly.vertices), Vector()) / len(poly.vertices)
            mat = mat_of_face(c, poly.normal)
            if mat not in self.slots:
                self.slots.append(mat)
            f.material_index = self.slots.index(mat)
        bpy.data.meshes.remove(tmp)

    def finish(self, col):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for s in self.slots:
            me.materials.append(MATS[s])
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        col.objects.link(ob)
        return ob


def hull(points):
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return bm


def strand(base, height, r0, segs, bend, sides=4):
    """Closed tapered stalk: rings of `sides` verts along a gentle curve, capped at the base, pointed tip."""
    bm = bmesh.new()
    rings = []
    ph = rng.uniform(0, 6.28)
    for k in range(segs + 1):
        t = k / segs
        c = Vector((base[0] + bend[0] * t * t + .08 * math.sin(t * 5 + ph), base[1] + bend[1] * t * t, base[2] + height * t))
        r = r0 * (1 - .75 * t)
        twist = t * 1.8 + ph
        rings.append([bm.verts.new(c + Vector((r * math.cos(twist + j * 2 * math.pi / sides), r * math.sin(twist + j * 2 * math.pi / sides) * .45, 0))) for j in range(sides)])
    tip = bm.verts.new(Vector((base[0] + bend[0] + .08 * math.sin(5 + ph), base[1] + bend[1], base[2] + height * 1.06)))
    bm.faces.new(list(reversed(rings[0])))
    for a, b in zip(rings, rings[1:]):
        for j in range(sides):
            bm.faces.new([a[j], a[(j + 1) % sides], b[(j + 1) % sides], b[j]])
    for j in range(sides):
        bm.faces.new([rings[-1][j], rings[-1][(j + 1) % sides], tip])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return bm


def kelp(i):
    b = Builder(f'Kelp_{i}')
    n = rng.randint(4, 7)
    red = i == 2
    for k in range(n):
        a = rng.uniform(0, 6.28)
        d = rng.uniform(0, .35)
        base = (d * math.cos(a), d * math.sin(a), 0)
        h = rng.uniform(1.1, 2.4)
        bend = (rng.uniform(-.35, .35), rng.uniform(-.35, .35))
        b.add(strand(base, h, rng.uniform(.06, .1), 7, bend), lambda c, nrm, h=h: ('Kelp_Red' if red and c.z > h * .4 else ('Kelp_Light' if c.z > h * .6 else ('Kelp_Mid' if c.z > h * .25 else 'Kelp_Dark'))))
        # Leaf blades: flat hulls hanging off the stalk.
        for j in range(rng.randint(2, 4)):
            t = rng.uniform(.25, .85)
            c = Vector((base[0] + bend[0] * t * t, base[1] + bend[1] * t * t, h * t))
            side = rng.choice((-1, 1))
            pts = [c + Vector((0, 0, 0)), c + Vector((side * .28, .02, .18)), c + Vector((side * .42, -.02, .42)), c + Vector((side * .1, .03, .3)),
                   c + Vector((side * .2, -.04, .25))]
            b.add(hull(pts), lambda cc, nrm: 'Kelp_Light' if not red else 'Kelp_Red')
    return b


def starfish(i):
    b = Builder(f'Starfish_{i}')
    col = ['Star_Orange', 'Star_Coral', 'Star_Purple'][i % 3]
    R = rng.uniform(.16, .2)
    rot = rng.uniform(0, 6.28)
    centre = [Vector((.07 * math.cos(k * 1.2566 + rot), .07 * math.sin(k * 1.2566 + rot), z)) for k in range(5) for z in (0, .06)] + [Vector((0, 0, .08))]
    b.add(hull(centre), lambda c, n: col)
    for k in range(5):
        a = k * 2 * math.pi / 5 + rot + rng.uniform(-.1, .1)
        d = Vector((math.cos(a), math.sin(a), 0))
        s = Vector((-d.y, d.x, 0))
        L = R * rng.uniform(.9, 1.15)
        pts = [d * .04 + s * .055, d * .04 - s * .055, d * .04 + s * .04 + Vector((0, 0, .055)), d * .04 - s * .04 + Vector((0, 0, .055)),
               d * L + s * .012, d * L - s * .012, d * L + Vector((0, 0, .015))]
        b.add(hull(pts), lambda c, n: 'Star_Dots' if n.z > .9 and rng.random() < .3 else col)
    return b


def fish(i):
    """~0.34 m fish, head toward Blender -Y. Stripe/fin colours differ per variant."""
    b = Builder(f'Fish_{i}')
    body, accent = [('Fish_Yellow', 'Fish_Blue'), ('Fish_Orange', 'Fish_White'), ('Fish_Teal', 'Fish_Yellow')][i]
    L, H, W = .17, .075 + .015 * i, .035
    pts = []
    for k in range(9):
        t = k / 8
        y = -L + 2 * L * t
        prof = math.sin(math.pi * (t * .9 + .05)) ** .8
        for a in range(6):
            ang = a * math.pi / 3
            pts.append(Vector((W * prof * math.cos(ang), y, H * prof * math.sin(ang) * (1.1 if math.sin(ang) > 0 else .85))))
    stripe = (-.02 + .03 * i, .04 + .03 * i)
    b.add(hull(pts), lambda c, n: accent if stripe[0] < c.y < stripe[1] else ('Fish_Dark' if c.y < -L * .8 and abs(c.x) > .015 and c.z > 0 else body))
    tail = [Vector((0, L * .9, 0)), Vector((.008, L * 1.05, .0)), Vector((-.008, L * 1.05, 0)),
            Vector((0, L * 1.45, H * .9)), Vector((0, L * 1.45, -H * .9)), Vector((.006, L * 1.38, 0))]
    b.add(hull(tail), lambda c, n: accent)
    fin = [Vector((0, -L * .2, H * .8)), Vector((0, L * .45, H * .7)), Vector((0, L * .1, H * 1.45)), Vector((.006, L * .1, H * .9)), Vector((-.006, L * .1, H * .9))]
    b.add(hull(fin), lambda c, n: accent)
    return b


def validate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-8)
    seen, inward, shells = set(), 0, 0
    for f in bm.faces:
        if f.index in seen:
            continue
        stack, vol = [f], 0.0
        seen.add(f.index)
        while stack:
            g = stack.pop()
            vs = [v.co for v in g.verts]
            for k in range(1, len(vs) - 1):
                vol += vs[0].dot(vs[k].cross(vs[k + 1])) / 6
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index)
                        stack.append(h)
        shells += 1
        inward += vol < 0
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    bm.free()
    return dict(name=obj.name, triangles=tris, shells=shells, non_manifold_edges=nonman, loose_verts=loose, degenerate_faces=degenerate,
                inward_shells=inward, ok=nonman == 0 and loose == 0 and degenerate == 0 and inward == 0)


col = bpy.data.collections.new('Underwater')
bpy.context.scene.collection.children.link(col)
objs = [kelp(i).finish(col) for i in range(3)] + [starfish(i).finish(col) for i in range(3)] + [fish(i).finish(col) for i in range(3)]
report = []
for o in objs:
    report.append(validate(o))
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.gltf(filepath=str(ASSETS / (o.name + '.glb')), use_selection=True, export_format='GLB', export_apply=True, export_yup=True)
(OUT / 'underwater_validation.json').write_text(json.dumps(report, indent=1))
# Preview sheet
x = 0
for o in objs:
    s = 1.0 if o.name.startswith('Kelp') else 3.0
    o.scale = (s, s, s)
    o.rotation_euler = (0, 0, math.radians(90)) if o.name.startswith('Fish') else (0, 0, 0)
    bpy.context.view_layer.update()
    o.location = (x + o.dimensions.x / 2, 0, (.4 if o.name.startswith('Fish') else 0))
    x += o.dimensions.x + .3
cam_data = bpy.data.cameras.new('Cam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = x * 1.02
cam = bpy.data.objects.new('Cam', cam_data)
bpy.context.scene.collection.objects.link(cam)
cam.location = (x / 2, -20, 1.2 + 4)
cam.rotation_euler = (math.radians(78), 0, 0)
sc = bpy.context.scene
sc.camera = cam
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'MATERIAL'
sc.render.resolution_x, sc.render.resolution_y = 2400, 600
w = bpy.data.worlds.new('W')
sc.world = w
sc.render.filepath = str(OUT / 'underwater_kit_preview.png')
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'underwater_kit.blend'))
print('SEA_DONE', json.dumps([(r['name'], r['triangles'], r['ok']) for r in report]))
