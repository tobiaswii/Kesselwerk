"""
Kesselwerk – Kreaturen-Generator (Blender, bpy).
Baut stilisierte Monster (eigene Designs) aus Metaball-Körpern + Anbauteilen,
riggt sie, animiert sie und exportiert FBX für Unity.

Koordinaten: Blender, Z = oben, Kreatur schaut nach -Y.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Quaternion, Matrix, Euler

V = Vector


# ---------------------------------------------------------------------------
# Grundlagen
# ---------------------------------------------------------------------------

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


EMIT_SCALE = 0.35  # Vorschau: Leuchten gedämpft (Unity setzt eigenes Glühen)


def mat(name, color, rough=0.55, emit=0.0, metal=0.0, spec=0.5):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    c = (*color, 1.0) if len(color) == 3 else color
    b.inputs["Base Color"].default_value = c
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit > 0:
        b.inputs["Emission Color"].default_value = c
        b.inputs["Emission Strength"].default_value = emit * EMIT_SCALE
    m.diffuse_color = c
    return m


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj


def select_only(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


# ---------------------------------------------------------------------------
# Metaball-Körper
# ---------------------------------------------------------------------------

class Blob:
    """Sammelt Metaball-Elemente und wandelt sie in ein glattes Mesh."""

    def __init__(self, name, res=0.035):
        self.mb = bpy.data.metaballs.new(name)
        self.mb.resolution = res
        self.mb.render_resolution = res
        self.mb.threshold = 0.6
        self.obj = link(bpy.data.objects.new(name, self.mb))

    RS = 1.45

    def ball(self, co, r, scale=(1, 1, 1), rot=None, neg=False, stiff=2.0):
        r *= self.RS
        e = self.mb.elements.new()
        e.type = 'ELLIPSOID'
        e.co = V(co)
        e.radius = r
        e.size_x, e.size_y, e.size_z = scale
        e.stiffness = stiff
        e.use_negative = neg
        if rot is not None: e.rotation = rot
        return e

    def capsule(self, a, b, r, stiff=2.0):
        """Kapsel von Punkt a nach b."""
        a, b = V(a), V(b)
        r *= self.RS
        d = b - a
        e = self.mb.elements.new()
        e.type = 'CAPSULE'
        e.co = (a + b) / 2
        e.radius = r
        e.size_x = d.length / 2
        e.stiffness = stiff
        e.rotation = V((1, 0, 0)).rotation_difference(d.normalized()) if d.length > 1e-6 else Quaternion()
        return e

    def chain(self, pts, radii, stiff=2.0):
        for i in range(len(pts) - 1):
            self.capsule(pts[i], pts[i + 1], (radii[i] + radii[i + 1]) / 2, stiff)

    def to_mesh(self, voxel=0.018, smooth_iter=6, target_faces=7000):
        select_only([self.obj])
        bpy.ops.object.convert(target='MESH')
        o = bpy.context.view_layer.objects.active
        # gleichmäßiges Netz
        r = o.modifiers.new("remesh", 'REMESH')
        r.mode = 'VOXEL'; r.voxel_size = voxel; r.adaptivity = 0.0
        bpy.ops.object.modifier_apply(modifier=r.name)
        s = o.modifiers.new("smooth", 'CORRECTIVE_SMOOTH')
        s.iterations = smooth_iter; s.smooth_type = 'LENGTH_WEIGHTED'; s.use_only_smooth = True
        s.rest_source = 'ORCO'
        bpy.ops.object.modifier_apply(modifier=s.name)
        n = len(o.data.polygons)
        if n > target_faces:
            d = o.modifiers.new("dec", 'DECIMATE'); d.ratio = target_faces / n
            bpy.ops.object.modifier_apply(modifier=d.name)
        bpy.ops.object.shade_smooth()
        return o


# ---------------------------------------------------------------------------
# Anbauteile (scharfe Formen, die Metaballs nicht können)
# ---------------------------------------------------------------------------

def lathe(name, profile, segs=16, material=None):
    """Drehkörper um die Z-Achse aus (radius, z)-Profil."""
    bm = bmesh.new()
    rings = []
    for (r, z) in profile:
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    # Kappen
    if profile[0][0] > 1e-4: bm.faces.new(list(reversed(rings[0])))
    if profile[-1][0] > 1e-4: bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me))
    if material: o.data.materials.append(material)
    for p in o.data.polygons: p.use_smooth = True
    return o


def horn(name, length, base_r, curve=0.3, segs=10, material=None, rings=8):
    """Gebogenes Horn entlang +Z, Spitze gebogen nach -Y (vorne) bei curve>0."""
    bm = bmesh.new()
    rings_v = []
    for k in range(rings + 1):
        t = k / rings
        r = base_r * (1 - t) ** 0.9 + 0.002
        z = length * t
        off = curve * length * t * t
        ring = []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a) - off, z)))
        rings_v.append(ring)
    for k in range(rings):
        for i in range(segs):
            j = (i + 1) % segs
            bm.faces.new((rings_v[k][i], rings_v[k][j], rings_v[k + 1][j], rings_v[k + 1][i]))
    bm.faces.new(list(reversed(rings_v[0])))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me))
    if material: o.data.materials.append(material)
    for p in o.data.polygons: p.use_smooth = True
    return o


def leaf(name, length, width, thick=0.025, curl=0.2, material=None, segs=8, inner=None, tip_bend=0.0):
    """Blatt-/Ohrform in der XZ-Ebene, Spitze +Z, gewölbt (curl) nach -Y. Mit Dicke."""
    bm = bmesh.new()
    cols = 7
    grid_f, grid_b = [], []
    for k in range(segs + 1):
        t = k / segs
        w = width * math.sin(math.pi * min(1.0, t * 0.92 + 0.08)) * (1 - t * 0.55)
        z = length * t
        rowf, rowb = [], []
        for c in range(cols):
            u = c / (cols - 1) * 2 - 1
            x = u * w / 2
            y = -curl * (1 - u * u) * w - tip_bend * t * t * length
            th = thick * (1 - t) * (1 - abs(u) * 0.7) + 0.003
            rowf.append(bm.verts.new((x, y - th / 2, z)))
            rowb.append(bm.verts.new((x, y + th / 2, z)))
        grid_f.append(rowf); grid_b.append(rowb)
    for k in range(segs):
        for c in range(cols - 1):
            bm.faces.new((grid_f[k][c], grid_f[k + 1][c], grid_f[k + 1][c + 1], grid_f[k][c + 1]))
            bm.faces.new((grid_b[k][c], grid_b[k][c + 1], grid_b[k + 1][c + 1], grid_b[k + 1][c]))
        bm.faces.new((grid_f[k][0], grid_b[k][0], grid_b[k + 1][0], grid_f[k + 1][0]))
        bm.faces.new((grid_f[k][-1], grid_f[k + 1][-1], grid_b[k + 1][-1], grid_b[k][-1]))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me))
    if material: o.data.materials.append(material)
    if inner:  # Innenseite (Vorderseite -Y) mit zweitem Material
        o.data.materials.append(inner)
        for p in o.data.polygons:
            if p.normal.y < -0.5 and abs(p.center.x) < width * 0.3 and p.center.z < length * 0.8:
                p.material_index = 1
    for p in o.data.polygons: p.use_smooth = True
    sub = o.modifiers.new("sub", 'SUBSURF'); sub.levels = 1
    return o


def sphere(name, r, material=None, seg=24, ring=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=ring, radius=r)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me))
    if material: o.data.materials.append(material)
    for p in o.data.polygons: p.use_smooth = True
    return o


def surface(obj, origin, direction, embed=0.0):
    """Punkt + Normale, wo ein Strahl von innen (origin) in Richtung direction die Oberfläche verlässt."""
    from mathutils.bvhtree import BVHTree
    bvh = getattr(obj, "_bvh", None)
    if bvh is None:
        bm = bmesh.new(); bm.from_mesh(obj.data); bm.transform(obj.matrix_world)
        bvh = BVHTree.FromBMesh(bm); bm.free()
        _BVH[obj.name] = bvh
    bvh = _BVH[obj.name]
    o = V(origin); d = V(direction).normalized()
    far = o + d * 5.0
    hit = bvh.ray_cast(far, -d)  # von außen zurück nach innen
    if hit[0] is None: return o, d
    loc, nor = hit[0], hit[1]
    return loc - nor * embed, nor


_BVH = {}


def place(o, loc, rot=(0, 0, 0), scale=(1, 1, 1)):
    o.location = V(loc)
    o.rotation_euler = Euler(rot)
    o.scale = V(scale)
    return o


def apply_tf(objs):
    select_only(objs)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in objs:
        for m in list(o.modifiers):
            select_only([o])
            bpy.ops.object.modifier_apply(modifier=m.name)


# ---------------------------------------------------------------------------
# Augen im Anime-Stil
# ---------------------------------------------------------------------------

def anime_eye(name, center, normal, size, iris_color, pupil_color=(0.03, 0.02, 0.05), white=True, angle_up=0.0):
    """Großes Auge: weißer Augapfel, große Iris, Pupille, zwei Glanzpunkte. Gibt Objektliste zurück."""
    normal = V(normal).normalized()
    q = V((0, -1, 0)).rotation_difference(normal)  # Basis schaut nach -Y
    parts = []
    m_white = mat("Augenweiss", (0.97, 0.97, 0.98), 0.25)
    m_iris = mat("Iris_" + name, iris_color, 0.2)
    m_pupil = mat("Pupille", pupil_color, 0.15)
    m_glint = mat("Glanz", (1, 1, 1), 0.1, emit=1.5)
    def put(o, off, scl):
        o.scale = V(scl)
        o.rotation_mode = 'QUATERNION'
        o.rotation_quaternion = q
        o.location = V(center) + q @ V(off)
        parts.append(o)
    dark = tuple(c * 0.28 for c in iris_color)
    m_irisd = mat("IrisD_" + name, dark, 0.2)
    if white:
        put(sphere(name + "_W", 1, m_white), (0, 0, 0), (size * 0.92, size * 0.34, size * 1.08))
    put(sphere(name + "_I", 1, m_irisd), (0, -size * 0.22, 0.0), (size * 0.86, size * 0.18, size * 1.02))
    put(sphere(name + "_L", 1, m_iris), (0, -size * 0.30, -size * 0.30), (size * 0.66, size * 0.12, size * 0.55))
    put(sphere(name + "_P", 1, m_pupil), (0, -size * 0.36, 0.02 * size), (size * 0.34, size * 0.1, size * 0.5))
    put(sphere(name + "_G1", 1, m_glint, 12, 8), (size * 0.26, -size * 0.40, size * 0.38), (size * 0.24, size * 0.06, size * 0.28))
    put(sphere(name + "_G2", 1, m_glint, 10, 6), (-size * 0.28, -size * 0.38, -size * 0.40), (size * 0.1, size * 0.05, size * 0.1))
    return parts


# ---------------------------------------------------------------------------
# Einfärben des Körpers über Zonen
# ---------------------------------------------------------------------------

def smoothstep(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def col_of(m):
    c = m.diffuse_color
    glow = 0.0
    if m.use_nodes:
        b = m.node_tree.nodes.get("Principled BSDF")
        if b and b.inputs["Emission Strength"].default_value > 0: glow = 1.0
    return (c[0], c[1], c[2]), glow


def vcol_zones(obj, base_mat, zones, soft=0.25):
    """Weiche Zonen als Eckpunktfarben. zones: (material, center, radii)."""
    me = obj.data
    if "Col" not in me.color_attributes:
        me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    ca = me.color_attributes["Col"]
    base, _ = col_of(base_mat)
    zc = [(col_of(m), V(c), V(r)) for m, c, r in zones]
    vcols = []
    for v in me.vertices:
        p = obj.matrix_world @ v.co
        col = V(base); glow = 0.0
        for (zcol, zg), c, r in zc:
            d = V(((p.x - c.x) / r.x, (p.y - c.y) / r.y, (p.z - c.z) / r.z)).length
            w = 1.0 - smoothstep(1.0 - soft, 1.0, d)
            if w > 0:
                col = col.lerp(V(zcol), w); glow = glow * (1 - w) + zg * w
        vcols.append((col, glow))
    for li, loop in enumerate(me.loops):
        c, g = vcols[loop.vertex_index]
        ca.data[li].color = (c.x, c.y, c.z, 1.0 - 0.5 * g)


def vcol_solid(obj):
    """Teile: Farbe aus ihren Materialien übernehmen."""
    me = obj.data
    if "Col" not in me.color_attributes:
        me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    ca = me.color_attributes["Col"]
    for p in me.polygons:
        m = me.materials[p.material_index] if me.materials else None
        (c, g) = col_of(m) if m else ((0.8, 0.8, 0.8), 0.0)
        for li in p.loop_indices:
            ca.data[li].color = (c[0], c[1], c[2], 1.0 - 0.5 * g)


def creature_material():
    """Ein Material für alles: Farbe aus Eckpunktfarbe, Alpha < 1 = Leuchten."""
    m = bpy.data.materials.get("Kreatur")
    if m: return m
    m = bpy.data.materials.new("Kreatur"); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    a = nt.nodes.new("ShaderNodeVertexColor"); a.layer_name = "Col"
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(a.outputs["Alpha"], inv.inputs[1])
    mul = nt.nodes.new("ShaderNodeMath"); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = 6.0 * EMIT_SCALE
    nt.links.new(inv.outputs[0], mul.inputs[0])
    nt.links.new(a.outputs["Color"], b.inputs["Emission Color"])
    nt.links.new(mul.outputs[0], b.inputs["Emission Strength"])
    b.inputs["Roughness"].default_value = 0.6
    m.diffuse_color = (1, 1, 1, 1)
    return m


def fix_normals(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()


def bake_ao(obj, rays=10, dist=0.22, strength=0.55):
    """Einfache Umgebungsverdeckung je Eckpunkt: Spalten, Achseln und Unterseite werden dunkler."""
    from mathutils.bvhtree import BVHTree
    import random as _r
    me = obj.data
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(obj.matrix_world)
    bvh = BVHTree.FromBMesh(bm); bm.free()
    rnd = _r.Random(7)
    dirs = []
    for _ in range(rays):
        while True:
            d = V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
            if 0.1 < d.length <= 1: break
        dirs.append(d.normalized())
    zmin = min((obj.matrix_world @ v.co).z for v in me.vertices)
    zmax = max((obj.matrix_world @ v.co).z for v in me.vertices)
    occ = []
    for v in me.vertices:
        p = obj.matrix_world @ v.co
        n = (obj.matrix_world.to_3x3() @ v.normal).normalized()
        hit = 0
        for d in dirs:
            if d.dot(n) < 0: d = -d
            d = (d + n * 0.3).normalized()
            if bvh.ray_cast(p + n * 0.004, d, dist)[0] is not None: hit += 1
        h = (p.z - zmin) / max(1e-4, zmax - zmin)
        occ.append(min(1.0, hit / rays * strength + (1 - min(1, h * 3)) * 0.18))
    ca = me.color_attributes["Col"]
    for li, loop in enumerate(me.loops):
        c = ca.data[li].color
        k = 1.0 - occ[loop.vertex_index]
        if c[3] < 0.99: k = 1.0  # Leuchtstellen nicht abdunkeln
        ca.data[li].color = (c[0] * k, c[1] * k, c[2] * k, c[3])


def finalize(meshes, rig):
    """Alle Teile zu einem Mesh mit einem Material zusammenführen (Gewichte bleiben erhalten)."""
    for o in meshes[1:]:
        vcol_solid(o)
        fix_normals(o)
    m = creature_material()
    for o in meshes:
        o.data.materials.clear(); o.data.materials.append(m)
        for mod in list(o.modifiers):
            if mod.type == 'ARMATURE': o.modifiers.remove(mod)
    select_only(meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    body = bpy.context.view_layer.objects.active
    body.name = "Kreatur"
    bake_ao(body)
    mod = body.modifiers.new("Armature", 'ARMATURE'); mod.object = rig.obj
    body.parent = rig.obj
    return body


def paint_zones(obj, base_mat, zones):
    """zones: Liste (material, center, radii) – Flächen im Ellipsoid bekommen das Material (letzte gewinnt)."""
    obj.data.materials.clear()
    obj.data.materials.append(base_mat)
    idx = {}
    for m, c, r in zones:
        if m.name not in idx:
            obj.data.materials.append(m); idx[m.name] = len(obj.data.materials) - 1
    for p in obj.data.polygons:
        pc = p.center
        for m, c, r in zones:
            d = V(((pc.x - c[0]) / r[0], (pc.y - c[1]) / r[1], (pc.z - c[2]) / r[2]))
            if d.length <= 1.0:
                p.material_index = idx[m.name]


# ---------------------------------------------------------------------------
# Skelett + Gewichte
# ---------------------------------------------------------------------------

class Rig:
    def __init__(self, name="Armature"):
        self.arm = bpy.data.armatures.new(name)
        self.obj = link(bpy.data.objects.new(name, self.arm))
        self.bones = []  # (name, head, tail, parent, deform)

    def bone(self, name, head, tail, parent=None, deform=True):
        self.bones.append((name, V(head), V(tail), parent, deform))
        return name

    def build(self):
        select_only([self.obj])
        bpy.ops.object.mode_set(mode='EDIT')
        eb = self.arm.edit_bones
        for name, h, t, p, d in self.bones:
            b = eb.new(name)
            b.head, b.tail = h, t
            b.use_deform = d
            if p: b.parent = eb[p]
        bpy.ops.object.mode_set(mode='OBJECT')

    def seg(self, name):
        for n, h, t, p, d in self.bones:
            if n == name: return h, t
        raise KeyError(name)


def dist_to_seg(p, a, b):
    ab = b - a
    t = 0.0 if ab.length_squared < 1e-9 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (a + ab * t - p).length


def skin(mesh_obj, rig, bones=None, falloff=8.0, maxinf=3, rigid=None):
    """Gewichte nach Abstand zu den Knochen. rigid: ganzes Objekt an einen Knochen."""
    names = [b[0] for b in rig.bones if b[4]] if bones is None else bones
    for n in names:
        if n not in mesh_obj.vertex_groups: mesh_obj.vertex_groups.new(name=n)
    if rigid:
        vg = mesh_obj.vertex_groups[rigid] if rigid in mesh_obj.vertex_groups else mesh_obj.vertex_groups.new(name=rigid)
        vg.add([v.index for v in mesh_obj.data.vertices], 1.0, 'REPLACE')
    else:
        segs = [(n, *rig.seg(n)) for n in names]
        for v in mesh_obj.data.vertices:
            p = mesh_obj.matrix_world @ v.co
            ws = []
            for n, a, b in segs:
                d = dist_to_seg(p, a, b)
                ws.append((1.0 / (d ** falloff + 1e-6), n))
            ws.sort(reverse=True)
            ws = ws[:maxinf]
            tot = sum(w for w, _ in ws)
            for w, n in ws:
                if w / tot > 0.02:
                    mesh_obj.vertex_groups[n].add([v.index], w / tot, 'REPLACE')
    mod = mesh_obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig.obj
    mesh_obj.parent = rig.obj


# ---------------------------------------------------------------------------
# Animationen (Keyframes auf Posen-Knochen)
# ---------------------------------------------------------------------------

class Anim:
    def __init__(self, rig_obj, name, frames, fps=30):
        self.rig = rig_obj
        self.act = bpy.data.actions.new(name)
        self.act.use_fake_user = True
        if rig_obj.animation_data is None: rig_obj.animation_data_create()
        rig_obj.animation_data.action = self.act
        self.frames = frames
        for pb in rig_obj.pose.bones:
            pb.rotation_mode = 'XYZ'
            pb.location = (0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)

    def key(self, bone, frame, rot=None, loc=None, scl=None):
        pb = self.rig.pose.bones.get(bone)
        if pb is None: return
        if rot is not None:
            pb.rotation_euler = Euler([math.radians(a) for a in rot])
            pb.keyframe_insert("rotation_euler", frame=frame)
        if loc is not None:
            pb.location = V(loc)
            pb.keyframe_insert("location", frame=frame)
        if scl is not None:
            pb.scale = V(scl)
            pb.keyframe_insert("scale", frame=frame)

    def curve(self, bone, fn, step=2, rot=True, loc=False, scl=False):
        """fn(t) -> (rot, loc, scl) mit t in 0..1 über die Länge."""
        for f in range(0, self.frames + 1, step):
            r, l, s = fn(f / self.frames)
            self.key(bone, f, rot=r if rot else None, loc=l if loc else None, scl=s if scl else None)

    def done(self):
        # alle Knochen ohne Schlüssel auf Ruhe setzen, damit Clips sich nicht beeinflussen
        for pb in self.rig.pose.bones:
            if not any(fc.data_path.startswith(f'pose.bones["{pb.name}"]') for fc in all_fcurves(self.act)):
                self.key(pb.name, 0, rot=(0, 0, 0), loc=(0, 0, 0))
        return self.act


def all_fcurves(act):
    # Blender 4.4+/5.x: Slots/Layers; ältere: act.fcurves
    try:
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for cb in strip.channelbags:
                    out.extend(cb.fcurves)
        return out
    except AttributeError:
        return list(act.fcurves)


# ---------------------------------------------------------------------------
# Export + Vorschau
# ---------------------------------------------------------------------------

def export_fbx(path):
    for n in ("Floor", "PrevCam", "Key", "Rim"):
        o = bpy.data.objects.get(n)
        if o: bpy.data.objects.remove(o, do_unlink=True)
    rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    rig.animation_data.action = bpy.data.actions["Idle"]
    bpy.context.scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=False, object_types={'ARMATURE', 'MESH'},
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.5, apply_scale_options='FBX_SCALE_ALL',
        mesh_smooth_type='FACE', colors_type='LINEAR', use_armature_deform_only=True, path_mode='AUTO')


def preview(path, size=520, yaw=-28, pitch=12, dist_mul=2.6, samples=24, bg=(0.17, 0.2, 0.24), action=None, frame=0):
    sc = bpy.context.scene
    objs = [o for o in sc.objects if o.type == 'MESH' and o.name != "Floor"]
    if action is not None:
        rig = next(o for o in sc.objects if o.type == 'ARMATURE')
        rig.animation_data.action = bpy.data.actions[action]
        sc.frame_set(frame)
    bpy.context.view_layer.update()
    mn = V((1e9, 1e9, 1e9)); mx = V((-1e9, -1e9, -1e9))
    dg = bpy.context.evaluated_depsgraph_get()
    for o in objs:
        oe = o.evaluated_get(dg)
        for v in oe.to_mesh().vertices:
            w = o.matrix_world @ v.co
            mn = V((min(mn.x, w.x), min(mn.y, w.y), min(mn.z, w.z)))
            mx = V((max(mx.x, w.x), max(mx.y, w.y), max(mx.z, w.z)))
        oe.to_mesh_clear()
    c = (mn + mx) / 2
    h = max((mx - mn).length, 0.5)
    cam = bpy.data.objects.get("PrevCam")
    if cam is None:
        cam = link(bpy.data.objects.new("PrevCam", bpy.data.cameras.new("PrevCam")))
        cam.data.lens = 50
        key = link(bpy.data.objects.new("Key", bpy.data.lights.new("Key", 'AREA')))
        key.data.energy = 250; key.data.size = 3
        rim = link(bpy.data.objects.new("Rim", bpy.data.lights.new("Rim", 'AREA')))
        rim.data.energy = 220; rim.data.size = 2; rim.data.color = (0.8, 0.9, 1.0)
        floor = bpy.data.meshes.new("Floor")
        bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20); bm.to_mesh(floor); bm.free()
        fo = link(bpy.data.objects.new("Floor", floor))
        fo.data.materials.append(mat("Boden", (0.35, 0.42, 0.3), 0.9))
        world = bpy.data.worlds.new("W"); sc.world = world
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (*bg, 1)
        world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    fo = bpy.data.objects["Floor"]; fo.location = (0, 0, mn.z)
    d = h * dist_mul * 0.55
    ya, pa = math.radians(yaw), math.radians(pitch)
    cam.location = c + V((math.sin(ya) * d * math.cos(pa), -math.cos(ya) * d * math.cos(pa), math.sin(pa) * d))
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.data.objects["Key"].data.energy = 45 * h * h; bpy.data.objects["Rim"].data.energy = 40 * h * h
    key = bpy.data.objects["Key"]; key.location = c + V((-h, -h * 1.2, h * 1.4)); key.rotation_euler = (c - key.location).to_track_quat('-Z', 'Y').to_euler()
    rim = bpy.data.objects["Rim"]; rim.location = c + V((h * 1.1, h * 1.2, h * 1.0)); rim.rotation_euler = (c - rim.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x = size; sc.render.resolution_y = size
    sc.render.filepath = path
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
    bpy.ops.render.render(write_still=True)
