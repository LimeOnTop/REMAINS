"""RE:MAINS fortifications v4: towers with inward doors and stone spiral stairs, the gatehouse (central wall with
arch, chain holes and winch rooms), and the drawbridge machinery (thick chain link, pulleys, capstan).
Geometry is authored in Unity coordinates (metres, Y up) and converted on export; every stone is a closed block.
Run: Blender --background --factory-startup --python build_fortifications.py
Input: towers_in.json (tower centres, floor levels, wall links sampled from the Unity scene).
"""
import bpy, bmesh, json, math, random
from pathlib import Path
from mathutils import Vector

ROOT = Path('/Users/k.shcheglov/Games/REMAINS')
OUT = ROOT / 'output/fortifications'
ASSETS = ROOT / 'Assets/Remains/Fortifications/Models'
ASSETS.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
rng = random.Random(20261010)

# Same palette as CastleArtV3 so new and old masonry read as one castle.
PALETTE = {
    'Limestone_0': (.43, .405, .34), 'Limestone_1': (.51, .48, .405), 'Limestone_2': (.57, .54, .46),
    'Limestone_3': (.47, .465, .405), 'Limestone_4': (.62, .58, .49), 'Limestone_5': (.42, .43, .36),
    'Cut_stone': (.57, .535, .44), 'Flagstone': (.50, .47, .40), 'Mortar': (.25, .26, .23),
    'Oak_0': (.24, .115, .047), 'Oak_1': (.34, .18, .08), 'Oak_2': (.43, .25, .12),
    'Forged_iron': (.07, .085, .095), 'Moss': (.22, .28, .09),
}
MATS = {}
for name, c in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    b.inputs['Base Color'].default_value = (*c, 1)
    b.inputs['Roughness'].default_value = .88
    b.inputs['Metallic'].default_value = .65 if name == 'Forged_iron' else 0
    m.diffuse_color = (*c, 1)
    MATS[name] = m
STONES = ['Limestone_%d' % i for i in range(6)]


def stone():
    return rng.choice(STONES)


class Mesh:
    """Collects closed polyhedra in Unity coordinates; exported as one object with material slots."""

    def __init__(self, name):
        self.name, self.v, self.f, self.m, self.slots = name, [], [], [], []

    def slot(self, mat):
        if mat not in self.slots:
            self.slots.append(mat)
        return self.slots.index(mat)

    def poly(self, verts, faces, mat):
        o = len(self.v)
        self.v.extend(verts)
        s = self.slot(mat)
        for f in faces:
            self.f.append([o + i for i in f])
            self.m.append(s)

    def hexa(self, c, mat):
        """Closed 8-corner block. Corner order: bottom (0..3) then top (4..7), counter-clockwise seen from above."""
        self.poly(c, [[0, 3, 2, 1], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], mat)

    def box(self, center, size, mat, yaw=0.0):
        cx, cy, cz = center
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        ca, sa = math.cos(yaw), math.sin(yaw)
        corners = []
        for y in (-sy, sy):
            for x, z in ((-sx, -sz), (sx, -sz), (sx, sz), (-sx, sz)):
                corners.append((cx + x * ca + z * sa, cy + y, cz - x * sa + z * ca))
        self.hexa(corners, mat)

    def beam(self, a, b, w, h, mat):
        """Square timber/iron bar between two points (Unity coords)."""
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        up = Vector((0, 1, 0)) if abs(d.y) < .95 else Vector((1, 0, 0))
        s = d.cross(up).normalized() * (w / 2)
        u = s.cross(d).normalized() * (h / 2)
        c = [a - s - u, a + s - u, b + s - u, b - s - u, a - s + u, a + s + u, b + s + u, b - s + u]
        # re-order into bottom/top rings for hexa(): bottom = -u side
        self.hexa([tuple(p) for p in (c[0], c[1], c[2], c[3], c[4], c[5], c[6], c[7])], mat)

    def cylinder(self, center, r, h, mat, n=12, axis='y', r2=None):
        cx, cy, cz = center
        r2 = r if r2 is None else r2
        ring0, ring1 = [], []
        for i in range(n):
            a = i * 2 * math.pi / n
            p, q = math.cos(a), math.sin(a)
            if axis == 'y':
                ring0.append((cx + r * p, cy - h / 2, cz + r * q)); ring1.append((cx + r2 * p, cy + h / 2, cz + r2 * q))
            elif axis == 'x':
                ring0.append((cx - h / 2, cy + r * p, cz + r * q)); ring1.append((cx + h / 2, cy + r2 * p, cz + r2 * q))
            else:
                ring0.append((cx + r * p, cy + r * q, cz - h / 2)); ring1.append((cx + r2 * p, cy + r2 * q, cz + h / 2))
        faces = [list(range(n))[::-1], list(range(n, 2 * n))]
        for i in range(n):
            j = (i + 1) % n
            faces.append([i, j, n + j, n + i])
        self.poly(ring0 + ring1, faces, mat)

    def torus(self, center, R, r, mat, axis='x', n=16, m=8, stretch=0.0):
        """Ring around `axis`; stretch lengthens it along the in-plane major direction (chain link)."""
        cx, cy, cz = center
        verts, faces = [], []
        for i in range(n):
            a = i * 2 * math.pi / n
            pa, qa = math.cos(a), math.sin(a)
            for j in range(m):
                b = j * 2 * math.pi / m
                rr = R + r * math.cos(b)
                u = rr * pa + (stretch if pa > 0 else -stretch)
                v = rr * qa
                w = r * math.sin(b)
                if axis == 'x':
                    verts.append((cx + w, cy + v, cz + u))
                elif axis == 'yz':  # ring in the x-z plane, elongated along z
                    verts.append((cx + v, cy + w, cz + u))
                elif axis == 'y':
                    verts.append((cx + u, cy + w, cz + v))
                else:
                    verts.append((cx + u, cy + v, cz + w))
        for i in range(n):
            for j in range(m):
                a, b = i * m + j, ((i + 1) % n) * m + j
                c, d = ((i + 1) % n) * m + (j + 1) % m, i * m + (j + 1) % m
                faces.append([a, b, c, d])
        self.poly(verts, faces, mat)

    def build(self, collection):
        me = bpy.data.meshes.new(self.name)
        # Unity (x, y, z) -> Blender (-x, -z, y): the glTF export + glTFast import map it back exactly.
        me.from_pydata([(-x, -z, y) for x, y, z in self.v], [], self.f)
        for s in self.slots:
            me.materials.append(MATS[s])
        for p, mi in zip(me.polygons, self.m):
            p.material_index = mi
            p.use_smooth = False
        me.validate(clean_customdata=False)
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))  # per closed block, outward
        bm.to_mesh(me)
        bm.free()
        obj = bpy.data.objects.new(self.name, me)
        collection.objects.link(obj)
        return obj


# ---------------------------------------------------------------- masonry engine
class Arch:
    """Opening with vertical jambs and an elliptical head; band = cut-stone frame width."""

    def __init__(self, cu, base, half, spring, rise, band=.75, frame=True, sill=False):
        self.cu, self.base, self.half, self.spring, self.rise, self.band, self.frame, self.sill = cu, base, half, spring, rise, band, frame, sill

    def inside(self, u, v, e):
        du = abs(u - self.cu)
        if v < self.base - (e if self.sill else 0) or du > self.half + e:
            return False
        if v <= self.spring:
            return True
        return (du / (self.half + e)) ** 2 + ((v - self.spring) / (self.rise + e)) ** 2 < 1

    def bbox(self, e):
        return (self.cu - self.half - e, self.base - e, self.cu + self.half + e, self.spring + self.rise + e)


class Rect(Arch):
    def __init__(self, cu, base, half, height, band=.5, frame=True, sill=True):
        super().__init__(cu, base, half, base + height, 0, band, frame, sill)

    def inside(self, u, v, e):
        return abs(u - self.cu) < self.half + e and self.base - e < v < self.spring + e


def masonry(mesh, place, W, H, t, openings, course=.72, blen=1.45, sub=.36, v0=0.0, jitter=.035, cap=None):
    """Lays full-depth stone blocks over a W x H wall (u along the wall, v up, n through it).
    place(u, v, n) -> Unity point. Blocks touching an opening are split into small cells; cells outside the
    opening's keep-out are kept, and a cut-stone frame (voussoirs, jambs, lintel) closes the jagged edge."""
    rows = int(math.ceil(H / course))
    for r in range(rows):
        va, vb = v0 + r * course, min(v0 + (r + 1) * course, v0 + H)
        if vb - va < .05:
            continue
        u = -(blen / 2 if r % 2 else 0) - rng.uniform(0, .3)
        while u < W:
            ua, ub = max(0.0, u), min(W, u + blen * rng.uniform(.8, 1.2))
            u = ub
            if ub - ua < .05:
                continue
            near = [o for o in openings if not (ub < o.bbox(o.band)[0] or ua > o.bbox(o.band)[2] or vb < o.bbox(o.band)[1] or va > o.bbox(o.band)[3])]
            if not near:
                _block(mesh, place, ua, ub, va, vb, t, stone(), jitter)
                continue
            nu, nv = max(1, int(round((ub - ua) / sub))), max(1, int(round((vb - va) / sub)))
            for i in range(nu):
                for j in range(nv):
                    a, b = ua + (ub - ua) * i / nu, ua + (ub - ua) * (i + 1) / nu
                    c, d = va + (vb - va) * j / nv, va + (vb - va) * (j + 1) / nv
                    cu, cv = (a + b) / 2, (c + d) / 2
                    if any(o.inside(cu, cv, o.band - .22) for o in near):
                        continue
                    _block(mesh, place, a, b, c, d, t, stone(), jitter * .5)
    for o in openings:
        if o.frame:
            _frame(mesh, place, o, t)


def _block(mesh, place, ua, ub, va, vb, t, mat, jitter):
    jo = rng.uniform(-jitter, jitter)
    pts = []
    for v in (va, vb):
        for u, n in ((ua, -t / 2), (ub, -t / 2), (ub, t / 2 + jo), (ua, t / 2 + jo)):
            pts.append(place(u, v, n))
    mesh.hexa(pts, mat)


def _frame(mesh, place, o, t):
    """Cut-stone surround: jamb stones, voussoirs following the head, optional sill."""
    w, e = o.band, .04
    if o.rise > 0:
        n = max(7, int(math.pi * (o.half + w / 2) / .7))
        for k in range(n):
            a0, a1 = math.pi * k / n, math.pi * (k + 1) / n
            pts = []
            for v_ in (0, 1):
                ring = []
                for a, rr in ((a0, 0), (a1, 0), (a1, 1), (a0, 1)):
                    hx, hy = o.half + rr * w, o.rise + rr * w
                    ring.append((o.cu + hx * math.cos(a), o.spring + hy * math.sin(a)))
                n_ = -t / 2 - e if v_ == 0 else t / 2 + e
                pts.extend(place(u, v, n_) for u, v in ring)
            # pts: ring at back then ring at front -> reorder to bottom/top convention (back=bottom)
            mesh.hexa(pts, 'Cut_stone')
    top = o.spring
    for side in (-1, 1):
        x0 = o.cu + side * o.half
        x1 = x0 + side * w
        y = o.base
        while y < top - .01:
            y1 = min(top, y + rng.choice((.6, .8)))
            ua, ub = min(x0, x1), max(x0, x1)
            pts = []
            for v in (y, y1):
                for u, n in ((ua, -t / 2 - e), (ub, -t / 2 - e), (ub, t / 2 + e), (ua, t / 2 + e)):
                    pts.append(place(u, v, n))
            mesh.hexa(pts, 'Cut_stone')
            y = y1
    if o.rise == 0:  # lintel and sill
        for v0, v1 in ((o.spring, o.spring + w), (o.base - w, o.base)):
            if v1 <= o.base and not o.sill:
                continue
            pts = []
            for v in (v0, v1):
                for u, n in ((o.cu - o.half - w, -t / 2 - e), (o.cu + o.half + w, -t / 2 - e), (o.cu + o.half + w, t / 2 + e), (o.cu - o.half - w, t / 2 + e)):
                    pts.append(place(u, v, n))
            mesh.hexa(pts, 'Cut_stone')


def plane_wall(origin, along, W, H, t, openings, **kw):
    """Straight wall: origin = Unity point at u=0, v=0 on the wall centre plane; along = unit (x, z)."""
    ax, az = along
    nx, nz = az, -ax  # wall normal in the ground plane

    def place(u, v, n):
        return (origin[0] + ax * u + nx * n, origin[1] + v, origin[2] + az * u + nz * n)
    return place


def ring_wall(center, r_mid):
    def place(u, v, n):
        a = u / r_mid
        r = r_mid + n
        return (center[0] + r * math.sin(a), v, center[2] + r * math.cos(a))
    return place


def flagstones(mesh, x0, x1, z0, z1, y, thick=.3, size=1.1, mat_pool=('Flagstone', 'Cut_stone', 'Limestone_2', 'Limestone_4'), skip=None):
    z = z0
    while z < z1 - .01:
        zb = min(z1, z + size)
        x = x0 - rng.uniform(0, size * .5)
        while x < x1 - .01:
            xa, xb = max(x0, x), min(x1, x + size * rng.uniform(.8, 1.3))
            x = xb
            if xb - xa < .05 or (skip and skip((xa + xb) / 2, (z + zb) / 2)):
                continue
            h = rng.uniform(0, .02)
            mesh.box(((xa + xb) / 2, y - thick / 2 + h / 2, (z + zb) / 2), (xb - xa, thick + h, zb - z), rng.choice(mat_pool))
        z = zb


# ---------------------------------------------------------------- towers
TOWER_R, TOWER_T = 8.24, 1.3
STAIR_RISE, STAIR_STEPS, STAIR_TURN = .2, 70, 2.5 * math.pi  # 450 degrees
STAIR_IN = 4.9


def tower(info):
    """Round tower in tower-local Unity coordinates (origin = tower centre at y = 0)."""
    m = Mesh('Tower_V4_%d' % info['i'])
    B, T = info['base'], info['top']
    r_mid, r_in = TOWER_R - TOWER_T / 2, TOWER_R - TOWER_T
    # Door faces the courtyard centre; angle measured from +z towards +x (sin for x, cos for z).
    door = math.atan2(0 - info['x'], 8 - info['z'])
    links = [math.atan2(l['dx'], l['dz']) for l in info['links']]
    place = ring_wall((0, 0, 0), r_mid)
    C = 2 * math.pi * r_mid

    def U(a):
        return (a % (2 * math.pi)) * r_mid
    # Foundation: plain cylinder below the ground floor.
    m.cylinder((0, (B - 1.2) / 2, 0), TOWER_R, B - 1.2, 'Limestone_1', n=40)
    # Main shaft: door arch at the ground floor, arrow slits on the field side.
    ops = [Arch(U(door), B, 1.2, B + 2.5, 1.2, band=.6)]
    for k, da in enumerate((math.pi * .55, math.pi, math.pi * 1.45)):
        for hy in (B + 5.2, B + 10.2):
            ops.append(Rect(U(door + da + k * .2), hy, .14, 1.3, band=.32))
    # Wrap-around: openings near u=0/C are duplicated so the seam stays correct.
    # Wrap-around copies (frame drawn once, by the original) keep blocks at the u=0/C seam clear.
    ops += [Arch(o.cu + d, o.base, o.half, o.spring, o.rise, o.band, frame=False) if o.rise else Rect(o.cu + d, o.base, o.half, o.spring - o.base, o.band, frame=False)
            for o in list(ops) for d in (C, -C)]
    masonry(m, place, C, T - (B - 1.2), TOWER_T, ops, v0=B - 1.2)
    # Corbel band and parapet with merlons; gaps where the curtain walls and the gatehouse walk arrive.
    for k in range(48):
        a0, a1 = k * 2 * math.pi / 48, (k + 1) * 2 * math.pi / 48
        pts = []
        for v in (T - .55, T):
            for a, r in ((a0, r_in + .2), (a1, r_in + .2), (a1, TOWER_R + .18), (a0, TOWER_R + .18)):
                pts.append((r * math.sin(a), v, r * math.cos(a)))
        m.hexa(pts, 'Cut_stone')
    pr_mid = TOWER_R - .35
    pplace = ring_wall((0, 0, 0), pr_mid)
    Cp = 2 * math.pi * pr_mid
    gaps = []
    for la in links:
        cu = (la % (2 * math.pi)) * pr_mid
        for off in (0, Cp, -Cp):
            gaps.append(Rect(cu + off, T - .2, 2.3, 3.5, band=.35, frame=False))
    masonry(m, pplace, Cp, 1.35, .7, gaps, v0=T, blen=1.2, course=.68)
    nm = 26
    for k in range(nm):
        if k % 2:
            continue
        a = (k + .5) * 2 * math.pi / nm
        if any(abs(math.atan2(math.sin(a - la), math.cos(a - la))) * pr_mid < 2.9 for la in links):
            continue
        m.box((pr_mid * math.sin(a), T + 1.35 + .35, pr_mid * math.cos(a)), (1.05, .7, .7), 'Cut_stone', yaw=a)
    # Ground floor and roof (roof has the stairwell opening over the last part of the flight).
    flagstones(m, -r_in, r_in, -r_in, r_in, B, thick=.35, skip=lambda x, z: x * x + z * z > (r_in + .15) ** 2)
    a_start = door + math.radians(28)
    a_end = a_start + STAIR_TURN
    hole0, hole1 = a_end - math.radians(88), a_end + math.radians(3)

    def in_hole(x, z):
        r = math.hypot(x, z)
        a = math.atan2(x, z)
        d0 = (a - hole0) % (2 * math.pi)
        return r > STAIR_IN - .25 and d0 < (hole1 - hole0)
    flagstones(m, -r_in, r_in, -r_in, r_in, T, thick=.4, size=.9, skip=lambda x, z: x * x + z * z > (r_in + .15) ** 2 or in_hole(x, z))
    # Stone spiral stair along the wall, every tread a solid wedge reaching into the masonry.
    da = STAIR_TURN / STAIR_STEPS
    for k in range(STAIR_STEPS):
        a0, a1 = a_start + k * da, a_start + (k + 1) * da + da * .08
        top = B + (k + 1) * STAIR_RISE
        bot = max(B, top - .55)
        pts = []
        for v in (bot, top):
            for a, r in ((a0, STAIR_IN), (a1, STAIR_IN), (a1, r_in + .25), (a0, r_in + .25)):
                pts.append((r * math.sin(a), v, r * math.cos(a)))
        m.hexa(pts, 'Limestone_2' if k % 2 else 'Limestone_4')
    # Wooden railing on the open inner edge: posts every second tread and a continuous handrail.
    posts = []
    for k in range(0, STAIR_STEPS + 1, 2):
        a = a_start + (k + .5) * da
        top = B + max(1, k) * STAIR_RISE
        r = STAIR_IN + .12
        p = (r * math.sin(a), top, r * math.cos(a))
        posts.append((p, a))
        m.box((p[0], top + .5, p[2]), (.13, 1.0, .13), 'Oak_1', yaw=a)
    for (p, a), (q, b) in zip(posts, posts[1:]):
        m.beam((p[0], p[1] + 1.0, p[2]), (q[0], q[1] + 1.0, q[2]), .11, .09, 'Oak_2')
        m.beam((p[0], p[1] + .5, p[2]), (q[0], q[1] + .5, q[2]), .07, .06, 'Oak_0')
    # Roof railing around the stairwell opening (inner arc + the closed radial edge).
    arc = []
    steps = 9
    for k in range(steps + 1):
        a = hole0 + (hole1 - hole0) * k / steps
        r = STAIR_IN - .32
        arc.append((r * math.sin(a), T, r * math.cos(a)))
    edge = [((STAIR_IN - .32 + (r_in - STAIR_IN + .32) * k / 3) * math.sin(hole0), T, (STAIR_IN - .32 + (r_in - STAIR_IN + .32) * k / 3) * math.cos(hole0)) for k in range(4)]
    for line in (arc, edge):
        for p in line:
            m.box((p[0], T + .5, p[2]), (.13, 1.0, .13), 'Oak_1')
        for p, q in zip(line, line[1:]):
            m.beam((p[0], T + 1.0, p[2]), (q[0], T + 1.0, q[2]), .11, .09, 'Oak_2')
            m.beam((p[0], T + .5, p[2]), (q[0], T + .5, q[2]), .07, .06, 'Oak_0')
    meta = dict(door_angle=math.degrees(door), stair_start=math.degrees(a_start), stair_end=math.degrees(a_end), base=B, top=T)
    return m, meta


# ---------------------------------------------------------------- gatehouse (local origin = passage floor centre, world (0, 16, -58))
def gatehouse():
    m = Mesh('Gatehouse_V4')
    X = 15.8
    # Outer facade: arch for the doors, two chain holes beside it.
    # The door hinges sit 1 m inside the facade, so the arch is wider and taller than the leaves: they swing clear.
    facade = [Arch(X, 0 + 4, 6.6, 6.2 + 4, 6.3, band=.8)]
    for side in (-1, 1):
        facade.append(Rect(X + side * 8.5, 9.15 + 4, .55, 2.3, band=.45))
    masonry(m, plane_wall((-X, -4, -2.3), (1, 0), 0, 0, 0, []), 2 * X, 18.0, 1.4, facade)
    # Inner (courtyard) wall: wide segmental arch and the two winch-room doors with small windows.
    inner = [Arch(X, 0 + 1, 6.45, 8.2 + 1, 4.0, band=.8)]
    for side in (-1, 1):
        inner.append(Arch(X + side * 11.0, 0 + 1, 1.1, 2.4 + 1, 1.1, band=.5))
        inner.append(Rect(X + side * 11.0, 8.0 + 1, .3, 1.3, band=.35))
    masonry(m, plane_wall((-X, -1, 8.1), (1, 0), 0, 0, 0, []), 2 * X, 15.0, 1.0, inner)
    # Passage side walls and room end walls (run along +z).
    for side in (-1, 1):
        masonry(m, plane_wall((side * 6.95, 0, -1.6), (0, 1), 0, 0, 0, []), 9.2, 13.0, 1.0, [])
        masonry(m, plane_wall((side * 14.9, 0, -1.6), (0, 1), 0, 0, 0, []), 9.2, 13.6, 1.0, [])
    # Floors: passage (with the old threshold height 0) and winch rooms; roof walk at y = 14.
    flagstones(m, -6.45, 6.45, -3.0, 8.6, 0, thick=.4, size=1.2)
    for side in (-1, 1):
        x0, x1 = sorted((side * 7.45, side * 14.4))
        flagstones(m, x0, x1, -1.6, 7.6, 0, thick=.4, size=1.0)
    m.box((0, 13.4, 3.0), (2 * X, 1.2, 9.2), 'Limestone_1')  # vault/roof mass between facade and inner wall
    for x in (-5.5, -3, -.5, 2, 4.5):  # timber ceiling beams under the passage roof
        m.box((x, 12.6, 3.0), (.45, .45, 9.4), 'Oak_1')
    flagstones(m, -X, X, -3.0, 8.6, 14.0, thick=.35, size=1.1)
    # Parapets: crenellated on the field side, low wall on the courtyard side.
    masonry(m, plane_wall((-X, 14.0, -2.65), (1, 0), 0, 0, 0, []), 2 * X, 1.35, .7, [], blen=1.2, course=.68)
    for k in range(int(2 * X / 2.2)):
        x = -X + 1.1 + k * 2.2
        m.box((x, 15.35 + .35, -2.65), (1.1, .7, .7), 'Cut_stone')
    masonry(m, plane_wall((-X, 14.0, 8.3), (1, 0), 0, 0, 0, []), 2 * X, 1.05, .6, [], blen=1.2, course=.53)
    # Machicolation corbels under the field parapet.
    for k in range(int(2 * X / 1.3)):
        m.box((-X + .65 + k * 1.3, 13.75, -3.15), (.45, .5, .4), 'Cut_stone')
    return m


# ---------------------------------------------------------------- machinery (each centred on its own origin)
def chain_pair():
    """Two interlocked thick links, 0.64 m pitch; local +z runs from the drum end to the bridge end."""
    m = Mesh('Chain_Link_Pair')
    R, r, stretch = .13, .075, .09
    m.torus((0, 0, -.16), R, r, 'Forged_iron', axis='x', n=14, m=6, stretch=stretch)   # link in the y-z plane
    m.torus((0, 0, .16), R, r, 'Forged_iron', axis='yz', n=14, m=6, stretch=stretch)   # link in the x-z plane
    return m


def pulley():
    """Grooved iron-rimmed wheel, axle along local x, radius 0.6 at the chain groove."""
    m = Mesh('Chain_Pulley')
    for dx in (-.16, .16):
        m.cylinder((dx, 0, 0), .72, .1, 'Forged_iron', n=24, axis='x')
    m.cylinder((0, 0, 0), .56, .22, 'Oak_1', n=24, axis='x')
    m.cylinder((0, 0, 0), .11, .9, 'Forged_iron', n=10, axis='x')
    for k in range(6):
        a = k * math.pi / 3
        m.beam((-.13, 0, 0), (-.13, .52 * math.sin(a), .52 * math.cos(a)), .07, .07, 'Forged_iron')
    return m


def pulley_yoke():
    """Ceiling hanger: two iron straps from the wheel axle up to a timber cross beam (beam length 3 m along x)."""
    m = Mesh('Pulley_Yoke')
    for dx in (-.42, .42):
        m.beam((dx, 0, 0), (dx, 1.25, 0), .1, .14, 'Forged_iron')
    m.box((0, 1.4, 0), (3.0, .38, .45), 'Oak_0')
    return m


def floor_bracket():
    """Low guide-pulley stand: two oak cheeks bolted to a sill."""
    m = Mesh('Pulley_Floor_Stand')
    for dx in (-.42, .42):
        m.box((dx, .55, 0), (.18, 1.1, .9), 'Oak_1')
    m.box((0, .07, 0), (1.3, .14, 1.2), 'Oak_0')
    return m


def capstan():
    """Vertical winch drum (radius 0.55) on a ground ring; bars are separate so they turn with the head."""
    m = Mesh('Capstan_Base')
    m.cylinder((0, .06, 0), 1.0, .12, 'Oak_0', n=16)
    m.cylinder((0, .2, 0), .62, .16, 'Forged_iron', n=16)
    m.cylinder((0, .75, 0), .55, 1.0, 'Oak_1', n=16)   # drum y 0.25..1.25
    m.cylinder((0, 1.42, 0), .68, .34, 'Oak_2', n=16)  # head with bar sockets
    m.cylinder((0, 1.64, 0), .5, .1, 'Forged_iron', n=16)
    for k in range(8):  # whelps (vertical ribs) on the drum
        a = k * math.pi / 4
        m.box((.56 * math.sin(a), .75, .56 * math.cos(a)), (.1, .95, .1), 'Oak_0', yaw=a)
    bars = Mesh('Capstan_Bars')
    for a in (0, math.pi / 2):
        bars.beam((-2.4 * math.sin(a), 1.42, -2.4 * math.cos(a)), (2.4 * math.sin(a), 1.42, 2.4 * math.cos(a)), .16, .16, 'Oak_2')
    for a in (0, math.pi / 2, math.pi, 1.5 * math.pi):  # iron grips at the ends
        bars.cylinder((2.25 * math.sin(a), 1.42, 2.25 * math.cos(a)), .1, .3, 'Forged_iron', n=8, axis='x' if abs(math.sin(a)) > .5 else 'z')
    return m, bars


def eye_plate():
    """Iron anchor plate with a ring, bolted to the drawbridge leaf end (local +z toward the chain)."""
    m = Mesh('Leaf_Chain_Eye')
    m.box((0, 0, 0), (.5, .7, .12), 'Forged_iron')
    m.torus((0, 0, .2), .14, .06, 'Forged_iron', axis='x', n=12, m=6)
    return m


def coil():
    """Wound chain turns for the capstan drum (visual only)."""
    m = Mesh('Capstan_Chain_Coil')
    for k in range(5):
        m.torus((0, .42 + k * .16, 0), .62, .07, 'Forged_iron', axis='y', n=28, m=6)
    return m


# ---------------------------------------------------------------- validation and export
def validate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    nonman = sum(1 for e in bm.edges if not e.is_manifold)
    loose = sum(1 for v in bm.verts if not v.link_faces)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-7)
    # Inward shells: signed volume per connected shell.
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
    return dict(name=obj.name, triangles=tris, shells=shells, non_manifold_edges=nonman, loose_verts=loose,
                degenerate_faces=degenerate, inward_shells=inward, ok=nonman == 0 and loose == 0 and degenerate == 0 and inward == 0)


def export(obj, name):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=str(ASSETS / (name + '.glb')), use_selection=True, export_format='GLB',
                              export_apply=True, export_yup=True, export_normals=True, export_materials='EXPORT')


col = bpy.data.collections.new('Fortifications')
bpy.context.scene.collection.children.link(col)
towers = json.loads((OUT / 'towers_in.json').read_text())
report, meta = [], {}
for info in towers:
    mesh, tm = tower(info)
    obj = mesh.build(col)
    report.append(validate(obj))
    export(obj, obj.name)
    meta['tower_%d' % info['i']] = tm
parts = [gatehouse(), chain_pair(), pulley(), pulley_yoke(), floor_bracket(), eye_plate(), coil()]
cap, bars = capstan()
parts += [cap, bars]
for mesh in parts:
    obj = mesh.build(col)
    report.append(validate(obj))
    export(obj, obj.name)
(OUT / 'fortifications_validation.json').write_text(json.dumps(report, indent=1))
(OUT / 'fortifications_meta.json').write_text(json.dumps(meta, indent=1))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'fortifications.blend'))
print('FORT_DONE', json.dumps([(r['name'], r['triangles'], r['ok']) for r in report]))
