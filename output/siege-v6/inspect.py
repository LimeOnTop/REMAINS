import bpy, math, os
from mathutils import Vector
OUT = '/Users/k.shcheglov/Games/REMAINS/output/siege-v6/'
SRC = {'catapult': '/Users/k.shcheglov/Games/REMAINS/Assets/Remains/SiegeV5/Models/catapult-v5.glb',
       'ram': '/Users/k.shcheglov/Games/REMAINS/Assets/Remains/SiegeV5/Models/ram-v5.glb',
       'ballista': '/Users/k.shcheglov/Games/REMAINS/Assets/Remains/SiegeV4/Models/ballista.glb'}
bpy.ops.wm.read_homefile(use_empty=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'MATERIAL'
sc.display.shading.show_cavity = True
sc.render.resolution_x, sc.render.resolution_y = 1000, 800
w = bpy.data.worlds.new('W'); sc.world = w; w.color = (.3, .3, .32)
report = {}
for name, path in SRC.items():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    mn = Vector((1e9, 1e9, 1e9)); mx = -mn
    tris = 0; mats = set()
    for o in meshes:
        for c in o.bound_box:
            p = o.matrix_world @ Vector(c); mn = Vector(map(min, mn, p)); mx = Vector(map(max, mx, p))
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
        mats |= {m.name for m in o.data.materials if m}
    report[name] = dict(objects=len(meshes), tris=tris, size=[round(v, 2) for v in (mx - mn)], mats=sorted(mats))
    ctr = (mn + mx) / 2; r = (mx - mn).length
    cd = bpy.data.cameras.new('c'); cam = bpy.data.objects.new('cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    for k, (az, el) in enumerate(((35, 25), (215, 20), (90, 5))):
        a, e = math.radians(az), math.radians(el)
        cam.location = ctr + Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))) * r * 1.3
        cam.rotation_euler = (ctr - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f'{OUT}inspect_{name}_{k}.png'
        bpy.ops.render.render(write_still=True)
print(report)
