"""Cape V9: rocky sides, rocky arch ceiling and a cut-stone bridge abutment for the southern cape (Blender MCP).
Authored directly in Unity world coordinates through U(x, y, z) -> Blender (-x, -z, y), so the GLB is placed at the origin.
Measured cape (Unity): top y ~15.9 from z -121 (shore) to -84; half width 18 -> 9 m; arch underside y 11-13 over z -116..-86;
river bed y ~4, water y 7. Lowered drawbridge: hinge (0, 16, -62.2), 18 m deck, tip at z -80.2, beams bottom ~y 14.8.
Run: python3 output/siege-v6/bmcp.py run output/map-rework/build_cape_v9.py
"""
import bpy, bmesh, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT = Path('/Users/k.shcheglov/Games/REMAINS')
src = (ROOT / 'output/map-rework/build_nature.py').read_text()
src = src.replace("bpy.ops.wm.read_factory_settings(use_empty=True)", "").split("col = bpy.data.collections.new('Nature')")[0]
for c in (bpy.data.objects, bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.collections):
    [c.remove(x) for x in list(c)]
exec(src)
OUT = ROOT / 'output/map-rework'; A = ROOT / 'Assets/Remains/CapeV9'; A.mkdir(parents=True, exist_ok=True)
crng = random.Random(902)
for k, c in enumerate([(.42, .40, .34), (.46, .43, .37), (.39, .37, .32), (.49, .46, .40), (.36, .34, .30)]):
    m = bpy.data.materials.new(f'Cape_Limestone_{k}'); m.use_nodes = True; b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*c, 1); b.inputs['Roughness'].default_value = .9; m.diffuse_color = (*c, 1); MATS[f'Lime_{k}'] = m


def U(x, y, z):
    return Vector((-x, -z, y))


EDGE = [(-122, 19), (-120, 18), (-116, 17), (-112, 15), (-108, 13), (-104, 13), (-100, 11), (-96, 11), (-92, 11), (-88, 9.2), (-84, 9.2), (-80, 9.2)]


def hw(z):
    for (z0, w0), (z1, w1) in zip(EDGE, EDGE[1:]):
        if z0 <= z <= z1:
            return w0 + (w1 - w0) * (z - z0) / (z1 - z0)
    return EDGE[0][1] if z < EDGE[0][0] else EDGE[-1][1]


def rock(builder, centre, rx, ry, rz, strata=.8, rough=.3, ratio=.14, yaw=0.0):
    """Natural rock shell centred at a Unity point with radii along Unity x, y (up), z."""
    sh = good_shell((rx, rz, ry), strata=strata, ratio=ratio, rough=rough, flat_bottom=.98, face=.82, top=.92)
    if yaw:
        bmesh.ops.rotate(sh, verts=list(sh.verts), cent=(0, 0, 0), matrix=__import__('mathutils').Matrix.Rotation(yaw, 3, 'Z'))
    bmesh.ops.translate(sh, verts=list(sh.verts), vec=U(*centre))
    builder.add(sh, ROCK_SET, by_height=rock_mats)


ARCH = (-114, -88)  # open water under the cape between these z
sides = Builder('Cape_Side_Rocks')
for s in (-1, 1):
    z = -123.0
    while z < -80.5:
        L = crng.uniform(3.2, 4.4); zc = z + L / 2; w = hw(zc)
        top = 15.75 + crng.uniform(-.15, .55)
        pier = not (ARCH[0] < zc < ARCH[1])
        bottom = 2.6 if pier else 10.3 + crng.uniform(-.4, .3)
        rock(sides, (s * (w - .2), (top + bottom) / 2, zc), crng.uniform(1.6, 2.2), (top - bottom) / 2, L * .62, yaw=crng.uniform(-.15, .15))
        if pier:  # heavy waterline boulders in front of the piers
            rock(sides, (s * (w + 2.0 + crng.uniform(0, .8)), 5.2, zc + crng.uniform(-.8, .8)), crng.uniform(1.6, 2.3), crng.uniform(2.0, 2.8), L * .55, strata=.5)
        z += L * .78
# Arch ceiling: flattened rocks hanging under the slab so the opening reads as a natural rock arch.
soffit = Builder('Cape_Arch_Soffit')
for zc in [ARCH[0] + 2 + i * 3.4 for i in range(8)]:
    if zc > ARCH[1] - 1.5:
        break
    w = hw(zc); x = -w + 1.8
    while x < w - 1.8:
        rock(soffit, (x + crng.uniform(-.5, .5), 12.0 + crng.uniform(-.25, .25), zc + crng.uniform(-.6, .6)), crng.uniform(1.9, 2.5), crng.uniform(.75, 1.0), crng.uniform(1.8, 2.3), strata=.3, rough=.25)
        x += crng.uniform(3.0, 3.8)

# Cut-stone abutment: main wall up to the road (y 15.85), a lower bearing ledge in front for the deck beams (y 14.8),
# flagstone paving and kerbs on top. Corner buttress rocks blend it into the side cliffs.
mason = Builder('Cape_Abutment')


def block(x0, x1, y0, y1, z0, z1, inset=.0, bevel=.05):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (x1 - x0) - inset, (y1 - y0) - inset, (z1 - z0) - inset
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sz, v.co.z * sy))  # Blender y is Unity z, Blender z is Unity y
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
    bmesh.ops.translate(bm, verts=list(bm.verts), vec=U((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mason.add(bm, [f'Lime_{crng.randrange(5)}'])


def wall(x0, x1, y0, y1, z0, z1, course=1.05, length=(1.6, 2.6)):
    y, row = y0, 0
    while y < y1 - .05:
        h = min(course * crng.uniform(.9, 1.1), y1 - y); x = x0 - (crng.uniform(.4, 1.0) if row % 2 else 0)
        while x < x1 - .05:
            l = crng.uniform(*length); a, b = max(x, x0), min(x + l, x1)
            if b - a > .25:
                block(a, b, y, y + h, z0 - crng.uniform(0, .06), z1, inset=.03)
            x += l
        y += h; row += 1


W = 9.25
wall(-W, W, 2.6, 15.8, -86.5, -81.2)          # main abutment wall under the road
wall(-W, W, 2.6, 14.78, -81.2, -79.35)        # bearing ledge for the deck tip
block(-W, W, 14.62, 14.8, -81.15, -79.3, bevel=.04)  # smooth seat stone the beams rest on
for zt in [-86.4 + i * 1.3 for i in range(4)]:  # flagstone paving on the road surface
    x = -8.8
    while x < 8.8:
        l = crng.uniform(1.3, 2.1); block(x, min(x + l, 8.8), 15.8, 15.93 + crng.uniform(0, .03), zt, min(zt + 1.25, -81.25), inset=.06, bevel=.03); x += l
for s in (-1, 1):
    block(min(s * W, s * (W - .5)), max(s * W, s * (W - .5)), 15.8, 16.3, -86.5, -81.25, bevel=.06)  # kerb
for s in (-1, 1):  # buttress rocks at the abutment corners
    rock(sides, (s * (W + .9), 8.6, -82.6), 1.9, 6.4, 2.6, strata=.9)
    rock(sides, (s * (W + 2.6), 4.6, -81.2), 2.1, 2.4, 2.2, strata=.5)

col = bpy.context.collection
objs = [b.finish(col) for b in (sides, soffit, mason)]
report = []
for o in objs:
    bm = bmesh.new(); bm.from_mesh(o.data)
    report.append(dict(name=o.name, tris=sum(len(f.verts) - 2 for f in bm.faces), non_manifold=sum(1 for e in bm.edges if not e.is_manifold),
                       loose=sum(1 for v in bm.verts if not v.link_faces)))
    bm.free()
bpy.ops.object.select_all(action='DESELECT')
for o in objs:
    o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.export_scene.gltf(filepath=str(A / 'cape-v9.glb'), use_selection=True, export_format='GLB', export_apply=True, export_yup=True, export_normals=True, export_materials='EXPORT')
(OUT / 'cape_v9_validation.json').write_text(json.dumps(report, indent=1))
# Preview render looking at the bridge end.
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
sc.render.resolution_x, sc.render.resolution_y = 1400, 900
if not sc.world: sc.world = bpy.data.worlds.new('W')
sc.world.color = (.75, .82, .88)
cd = bpy.data.cameras.new('c'); cam = bpy.data.objects.new('cam', cd); col.objects.link(cam); sc.camera = cam
for name, pos, look in (('cape_v9_preview_end', (-26, 20, -66), (0, 10, -88)), ('cape_v9_preview_side', (-48, 18, -122), (0, 9, -98))):
    cam.location = U(*pos); cam.rotation_euler = (U(*look) - U(*pos)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = str(OUT / (name + '.png')); bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'cape_v9.blend'))
print('CAPE_V9', json.dumps(report))
