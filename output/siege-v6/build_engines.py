# Siege engines V6: procedural low-poly ballista, ram and catapult built in the live Blender session (Blender MCP).
# Blender axes: +Y is the engine's forward (Unity +Z after the holder's 180 degree art rotation), +Z is up.
# Contract names used by runtime scripts: WheelPivot*, SuspendedLog, ThrowingArm, StoneSocket.
from pathlib import Path
import bpy
for c in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    [c.remove(x) for x in list(c)]
exec(Path('/Users/k.shcheglov/Games/REMAINS/output/siege-v4/build_models.py').read_text().split("\nnormalized_import('ballista'")[0])
O = R/'output/siege-v6'; A = R/'Assets/Remains/SiegeV6/Models'; A.mkdir(parents=True, exist_ok=True)
rng = random.Random(613)
if not bpy.context.scene.world: bpy.context.scene.world = bpy.data.worlds.new('World')
hide = mat('Tanned hide', (.50, .34, .19)); pad = mat('Padded leather', (.30, .16, .07)); rope = mat('Hemp rope', (.52, .40, .21))
wood = mat('Warm oak', (.29, .135, .05)); edge = mat('Fresh oak edges', (.45, .27, .11))


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def group(name, objects, p):
    g = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(g); g.location = p; bpy.context.view_layer.update()
    for o in objects:
        mw = o.matrix_world.copy(); o.parent = g; o.matrix_world = mw
    return g


def new_since(before):
    bpy.context.view_layer.update(); return [o for o in bpy.data.objects if o not in before]


def wheel(x, y, r=.65):
    before = set(bpy.data.objects); s = 1 if x > 0 else -1
    cyl('Tyre', (x, y, r), r, .27, iron, (1, 0, 0), 16); cyl('Oak_Wheel', (x, y, r), r * .88, .3, wood, (1, 0, 0), 16); cyl('Hub', (x, y, r), .18, .5, iron, (1, 0, 0), 8)
    for k in range(8):
        a = k * math.tau / 8; beam('Spoke', (x + s * .17, y, r), (x + s * .17, y + math.cos(a) * r * .8, r + math.sin(a) * r * .8), .10, edge)
    group('WheelPivot', new_since(before), (x, y, r))


def mesh_obj(n, bm, m, p=(0, 0, 0)):
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces)); me = bpy.data.meshes.new(n); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(n, me); bpy.context.collection.objects.link(o); o.location = p; me.materials.append(m); return o


def twisted(n, p, r, h, m, axis=(0, 0, 1), turns=.4, seg=8, rings=6):
    """Rope skein: a cylinder whose rings rotate along its length, giving faceted spiral strands."""
    bm = bmesh.new(); rs = []
    for i in range(rings + 1):
        t = i / rings; a0 = t * turns * math.tau; rr = r * (1 + .06 * math.sin(t * math.pi))
        rs.append([bm.verts.new((rr * math.cos(a0 + k * math.tau / seg), rr * math.sin(a0 + k * math.tau / seg), -h / 2 + h * t)) for k in range(seg)])
    for i in range(rings):
        for k in range(seg):
            bm.faces.new((rs[i][k], rs[i][(k + 1) % seg], rs[i + 1][(k + 1) % seg], rs[i + 1][k]))
    bm.faces.new(list(reversed(rs[0]))); bm.faces.new(rs[-1]); bmesh.ops.triangulate(bm, faces=list(bm.faces))
    o = mesh_obj(n, bm, m, p); o.rotation_euler = Vector(axis).to_track_quat('Z', 'Y').to_euler(); return o


def torus(n, p, R_, r, m, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=12, minor_segments=6, location=p, major_radius=R_, minor_radius=r, rotation=rot)
    o = bpy.context.object; o.name = n; o.data.materials.append(m); return o


def sheet(n, profile, y0, y1, m, thick=.025, ragged=.06):
    """Thin solid sheet following an XZ profile between y0 and y1, with ragged ends (hides, canvas)."""
    bm = bmesh.new(); top = []; bot = []
    for i, (x, z) in enumerate(profile):
        ja, jb = rng.uniform(-ragged, ragged), rng.uniform(-ragged, ragged)
        top.append((bm.verts.new((x, y0 + ja, z)), bm.verts.new((x, y1 + jb, z))))
        bot.append((bm.verts.new((x, y0 + ja, z - thick)), bm.verts.new((x, y1 + jb, z - thick))))
    for i in range(len(profile) - 1):
        bm.faces.new((top[i][0], top[i][1], top[i + 1][1], top[i + 1][0])); bm.faces.new((bot[i][0], bot[i + 1][0], bot[i + 1][1], bot[i][1]))
        bm.faces.new((top[i][0], top[i + 1][0], bot[i + 1][0], bot[i][0])); bm.faces.new((top[i][1], bot[i][1], bot[i + 1][1], top[i + 1][1]))
    for e in (0, -1):
        bm.faces.new((top[e][0], bot[e][0], bot[e][1], top[e][1]))
    return mesh_obj(n, bm, m)


def lash(n, a, b, t, w, m, length=.14):
    a, b = Vector(a), Vector(b); d = (b - a).normalized(); c = a + (b - a) * t
    return beam(n, c - d * length / 2, c + d * length / 2, w, m)


def validate(tag):
    deps = bpy.context.evaluated_depsgraph_get(); report = {'objects': 0, 'tris': 0, 'non_manifold': [], 'loose': []}
    for o in bpy.context.scene.objects:
        if o.type != 'MESH':
            continue
        me = o.evaluated_get(deps).to_mesh(); bm = bmesh.new(); bm.from_mesh(me)
        nm = sum(1 for e in bm.edges if not e.is_manifold); lv = sum(1 for v in bm.verts if not v.link_faces)
        report['objects'] += 1; report['tris'] += sum(len(f.verts) - 2 for f in bm.faces)
        if nm: report['non_manifold'].append(f'{o.name}:{nm}')
        if lv: report['loose'].append(f'{o.name}:{lv}')
        bm.free(); o.evaluated_get(deps).to_mesh_clear()
    print('VALIDATE', tag, json.dumps(report), flush=True); return report


reports = {}
# ---------------------------------------------------------------- Ballista: carriage, swivel pedestal, torsion frame, winch, bolt.
clear(); chassis(3.7, 2.0)
box('Pedestal_Post', (0, 0, 1.25), (.36, .36, .92), wood)
for a, b in [((0, 1.45, .93), (0, .16, 1.55)), ((0, -1.45, .93), (0, -.16, 1.55)), ((.95, 0, .93), (.16, 0, 1.55)), ((-.95, 0, .93), (-.16, 0, 1.55))]:
    beam('Pedestal_Brace', a, b, .16, edge)
cyl('Swivel_Plate', (0, 0, 1.74), .32, .1, iron, verts=12)
box('Cradle', (0, 0, 1.86), (.62, .9, .16), wood)
box('Stock', (0, .1, 2.05), (.34, 3.6, .26), wood)
box('Slider_Rail', (0, .3, 2.24), (.2, 3.5, .12), edge, .02)
for y in (-1.3, -.2, .9, 1.75):
    box('Stock_Band', (0, y, 2.1), (.38, .1, .32), iron, .015)
# Capitulum: two rope skeins held between iron washers in a timber frame at the front.
for z in (1.85, 2.8):
    box('Frame_Bar', (0, 1.15, z), (2.1, .32, .16), wood)
for x in (-1.0, -.28, .28, 1.0):
    box('Frame_Stile', (x, 1.15, 2.325), (.16, .3, .8), edge)
for x in (-1.0, 1.0):
    for z in (1.85, 2.8):
        box('Frame_Corner_Iron', (x, 1.15, z), (.2, .34, .2), iron, .015)
for s in (-1, 1):
    twisted('Torsion_Skein', (s * .64, 1.15, 2.33), .19, 1.0, rope, turns=.45 * s)
    for z in (1.735, 2.925):
        cyl('Skein_Washer', (s * .64, 1.15, z), .25, .07, iron, verts=10)
    box('Washer_Lever', (s * .64, 1.15, 2.98), (.58, .09, .07), iron, .01)
    # Bow arm leaves the skein and sweeps back; iron cap at the tip holds the string.
    beam('Bow_Arm', (s * .64, 1.15, 2.33), (s * 1.72, .42, 2.33), .15, wood)
    box('Arm_Tip_Cap', (s * 1.72, .42, 2.33), (.2, .2, .2), iron, .02)
    beam('Bow_String', (s * 1.74, .40, 2.34), (0, -.62, 2.36), .035, rope)
    box('Winch_Cheek', (s * .3, -1.25, 2.3), (.1, .42, .44), wood, .02)
    beam('Handspike', (s * .42, -1.25, 2.07), (s * .42, -1.25, 2.77), .07, edge)
    beam('Handspike', (s * .42, -1.6, 2.42), (s * .42, -.9, 2.42), .07, edge)
cyl('Winch_Drum', (0, -1.25, 2.42), .1, .74, wood, (1, 0, 0), 10)
beam('Winch_Rope', (0, -1.25, 2.52), (0, -.68, 2.38), .04, rope)
box('Trigger_Claw', (0, -.6, 2.36), (.2, .24, .13), iron, .02)
beam('Trigger_Lever', (0, -.7, 2.33), (0, -1.0, 2.12), .05, iron)
cyl('Bolt_Shaft', (0, .8, 2.38), .045, 2.5, edge, (0, 1, 0), 6)
bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=.1, radius2=0, depth=.32, location=(0, 2.2, 2.38)); o = bpy.context.object; o.name = 'Bolt_Head'; o.rotation_euler = (-math.pi / 2, 0, 0); o.data.materials.append(iron)
for a in (0, math.pi / 2):
    o = box('Bolt_Fletching', (0, -.3, 2.38), (.22, .3, .02), red, 0); o.rotation_euler.y = a
reports['ballista-v6'] = validate('ballista-v6'); export('ballista-v6')

# ---------------------------------------------------------------- Ram: gable roof on plates and rafters, hide covering, outrigger push rails.
clear(); chassis(5.6, 2.7)
for x in (-1.15, 1.15):
    for y in (-1.7, 1.7):
        box('Roof_Post', (x, y, 1.9), (.24, .26, 2.4), wood)
        beam('Knee_Brace', (x, y, 2.55), (x, y - math.copysign(.6, y), 3.1), .13, edge)
    box('Top_Plate', (x, 0, 3.2), (.22, 5.4, .22), wood)
for y in (-1.35, 1.35):
    box('Suspension_Crossbeam', (0, y, 3.05), (2.5, .25, .27), wood)
    torus('Hanger_Eye', (0, y, 2.86), .1, .035, iron, (0, math.pi / 2, 0))
dn = Vector((1.6, -.85)).normalized(); nr = Vector((.85, 1.6)).normalized()
for y in (-2.6, -1.3, 0, 1.3, 2.6):
    for s in (-1, 1):
        beam('Rafter', (s * 1.62, y, 3.08), (0, y, 3.95), .14, wood)
for s in (-1, 1):
    for k in range(4):
        sl = .24 + k * .455; p = Vector((0, 3.95)) + dn * sl + nr * .12
        o = box('Roof_Plank', (s * p.x, rng.uniform(-.06, .06), p.y), (.44, 5.75 + rng.uniform(-.15, .1), .08), wood if k % 2 else edge, .02)
        o.rotation_euler.y = s * math.atan2(.85, 1.6) + rng.uniform(-.015, .015)
box('Ridge_Cap', (0, 0, 4.11), (.34, 5.9, .1), wood, .02)
# Wet hides against fire arrows: three sheets draped over the ridge, each tied down with two ropes.
for yc in (-1.75, 0, 1.75):
    prof = []
    for i in range(9):
        t = -1.12 + 2.24 * i / 8; p = Vector((0, 3.95)) + dn * abs(t) + nr * .2
        prof.append((math.copysign(p.x, t) if t else 0, p.y + (.03 if abs(t) < .05 else 0)))
    sheet('Hide_Cover', prof, yc - .7, yc + .7, hide, .03, .08)
    for dy in (-.45, .45):
        y = yc + dy
        for s in (-1, 1):
            a = Vector((0, 3.95)) + nr * .25; b = Vector((0, 3.95)) + dn * 1.16 + nr * .24
            beam('Hide_Tie', (0, y, a.y), (s * b.x, y, b.y), .035, rope)
# Push rails stand outboard of the wheels on outriggers; grips match SiegeCrew slots (x 2.65, z -1.8..1.8).
for s in (-1, 1):
    box('Push_Rail', (s * 2.2, 0, 1.15), (.12, 5.5, .14), edge, .02)
    for y in (-2.65, -.6, .6, 2.65):
        beam('Rail_Outrigger', (s * 1.35, y, .88), (s * 2.2, y, 1.15), .12, wood)
        box('Rail_Bolt_Plate', (s * 2.2, y, 1.15), (.16, .2, .2), iron, .015)
    for y in (-1.8, -.6, .6, 1.8):
        beam('Grip_Spoke', (s * 2.2, y, 1.15), (s * 2.42, y, 1.18), .07, iron)
        box('Push_Grip', (s * 2.45, y, 1.18), (.09, .46, .09), edge, .015)
before = set(bpy.data.objects)
cyl('Ram_Log', (0, .2, 1.75), .39, 6.2, wood, (0, 1, 0), 12)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.5, radius2=.3, depth=.6, location=(0, 3.46, 1.75)); o = bpy.context.object; o.name = 'Iron_Ram_Head'; o.rotation_euler = (-math.pi / 2, 0, 0); o.data.materials.append(iron)
cyl('Ram_Head_Collar', (0, 3.12, 1.75), .47, .16, iron, (0, 1, 0), 12)
for y in (-2.5, -1.35, 1.35, 2.6):
    cyl('Log_Band', (0, y, 1.75), .41, .16, iron, (0, 1, 0), 12)
for y in (-1.35, 1.35):
    torus('Log_Eye', (0, y, 2.2), .1, .035, iron, (0, math.pi / 2, 0))
group('SuspendedLog', new_since(before), (0, .2, 1.75))
reports['ram-v6'] = validate('ram-v6'); export('ram-v6')

# ---------------------------------------------------------------- Catapult (onager): V5 layout kept; twisted skein, lashed arm, padded stop.
clear(); chassis(5.2, 2.8)
for side in (-1, 1):
    beam('A_Frame', (side * 1.1, -1.5, .9), (side * .95, .2, 2.8), .27, wood); beam('A_Frame', (side * 1.1, 1.5, .9), (side * .95, .2, 2.8), .27, wood)
    box('Frame_Iron_Strap', (side * .97, .2, 2.72), (.33, .5, .2), iron, .015)
    box('Axle_Bearing_Post', (side * 1.1, .2, 1.49), (.30, .40, 1.38), wood)
    box('Axle_Bearing_Housing', (side * 1.1, .2, 2.2), (.34, .62, .62), iron)
    cyl('Skein_Ratchet', (side * .78, .2, 2.2), .5, .1, iron, (1, 0, 0), 12)
cyl('Torsion_Axle', (0, .2, 2.2), .2, 2.8, iron, (1, 0, 0), 10)
for side in (-1, 1):
    twisted('Torsion_Skein', (side * .42, .2, 2.2), .42, .6, rope, (1, 0, 0), .5 * side, 10, 6)
before = set(bpy.data.objects)
arm_a, arm_b = (0, -2.6, 1.48), (0, 1.0, 2.4)
beam('Spoon_Arm', arm_a, arm_b, .3, edge)
for t in (.33, .52, .78):
    lash('Arm_Lashing', arm_a, arm_b, t, .36, rope, .16)
lash('Arm_Iron_Band', arm_a, arm_b, .93, .35, iron, .1)
box('Spoon_Saddle', (0, -2.55, 1.58), (.44, .48, .20), wood)
v = []; faces = []
for ring in range(5):
    theta = .18 + ring * .29
    for j in range(16):
        a = j * math.tau / 16; v.append((.72 * math.sin(theta) * math.cos(a), -2.55 + .72 * math.sin(theta) * math.sin(a), 2.10 - .42 * math.cos(theta)))
for k in range(4):
    for j in range(16):
        a = k * 16 + j; b = k * 16 + (j + 1) % 16; faces.append((a, b, b + 16, a + 16))
faces.append(tuple(reversed(range(16))))
me = bpy.data.meshes.new('Bowl'); me.from_pydata(v, [], faces); o = bpy.data.objects.new('Spoon_Bowl', me); bpy.context.collection.objects.link(o); o.data.materials.append(iron)
mod = o.modifiers.new('Bowl thickness', 'SOLIDIFY'); mod.thickness = .07
for a in range(4):
    ang = a * math.tau / 4 + math.pi / 4; beam('Bowl_Strap', (0, -2.55, 1.66), (.55 * math.cos(ang), -2.55 + .55 * math.sin(ang), 1.86), .06, iron)
g = group('ThrowingArm', new_since(before), (0, .2, 2.2))
socket = bpy.data.objects.new('StoneSocket', None); bpy.context.collection.objects.link(socket); socket.location = (0, -2.55, 2.23); bpy.context.view_layer.update(); mw = socket.matrix_world.copy(); socket.parent = g; socket.matrix_world = mw
beam('Stop_Bar', (-1.15, 1.15, 3.1), (1.15, 1.15, 3.1), .28, wood)
box('Stop_Pad', (0, 1.0, 3.06), (.8, .2, .42), pad, .07)
for x in (-.3, .3):
    box('Pad_Tie', (x, 1.07, 3.06), (.06, .36, .48), rope, .01)
for side in (-1, 1):
    beam('Stop_Bar_Post', (side * 1.15, 1.15, .80), (side * 1.15, 1.15, 3.1), .30, wood)
    beam('Stop_Bar_Brace', (side * 1.15, 2.1, .80), (side * 1.15, 1.15, 2.6), .23, edge)
    box('Stop_Bar_Iron_Joint', (side * 1.15, 1.15, 3.02), (.36, .36, .27), iron)
    box('Drum_Bearing_Post', (side * 1.07, -1.75, .93), (.30, .42, .48), wood)
    box('Drum_Bearing_Strap', (side * 1.07, -1.75, 1.02), (.34, .46, .17), iron)
cyl('Drum_Axle', (0, -1.75, 1.0), .11, 2.5, iron, (1, 0, 0))
cyl('Winding_Drum', (0, -1.75, 1.0), .22, 2.0, wood, (1, 0, 0), 10)
for x in (-.6, 0, .6):
    cyl('Drum_Rope_Coil', (x, -1.75, 1.0), .25, .3, rope, (1, 0, 0), 10)
beam('Winding_Crank', (-1.2, -1.75, 1), (-1.2, -1.75, 1.6), .10, iron); beam('Crank_Handgrip', (-1.2, -1.75, 1.6), (-1.65, -1.75, 1.6), .11, edge)
reports['catapult-v6'] = validate('catapult-v6'); export('catapult-v6')
(O/'validation.json').write_text(json.dumps(reports, indent=1))
print('V6 ENGINES COMPLETE', flush=True)
