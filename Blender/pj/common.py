# -*- coding: utf-8 -*-
"""Shared helpers, palette and materials for the Project Jelly kit (High-Detail v2)."""
import bpy
import bmesh
import math
from mathutils import Vector


# ---------------------------------------------------------------- colors ----
def srgb(hexstr):
    h = hexstr.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(pow(v, 2.2) for v in c)


C = {
    "cyan":        srgb("#00C8FF"),
    "cyan_light":  srgb("#66F2FF"),
    "cyan_deep":   srgb("#0062E6"),
    "green":       srgb("#1CE86A"),
    "green_light": srgb("#78FF9E"),
    "green_deep":  srgb("#08993C"),
    "white":       srgb("#F6FAFF"),
    "purple":      srgb("#8042F2"),
    "purple_mid":  srgb("#5D2EC2"),
    "purple_dk":   srgb("#2A1858"),
    "indigo":      srgb("#3B2B78"),
    "magenta":     srgb("#E83CF0"),
    "pink":        srgb("#FF5CA8"),
    "pink_light":  srgb("#FF94C8"),
    "gold":        srgb("#FFC414"),
    "gold_deep":   srgb("#E08200"),
    "yellow":      srgb("#FFE21F"),
    "red":         srgb("#FF2442"),
    "red_deep":    srgb("#A80822"),
    "blue":        srgb("#2468FF"),
    "dark":        srgb("#181A30"),
    "dark2":       srgb("#11152C"),
    "stage_bg":    srgb("#161B38"),
    "metal":       srgb("#B0BDD4"),
    "metal_dk":    srgb("#3C4464"),
    "glass":       srgb("#78ECFF"),
    "grass":       srgb("#34C46A"),
    "rock":        srgb("#3898E8"),
    "rock_light":  srgb("#6CD4FF"),
    "sand":        srgb("#FFBE42"),
    "orange":      srgb("#FF7814"),
    "black":       srgb("#14121D"),
    "road":        srgb("#2458D8"),
    "road2":       srgb("#4298FF"),
}

MATS = {}


# ------------------------------------------------------------- materials ----
def _principled(mat):
    return mat.node_tree.nodes.get("Principled BSDF")


def mat_plastic(name, color, rough=0.26, metal=0.0, coat=0.45, emission=None, strength=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = _principled(m)
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    b.inputs["Coat Weight"].default_value = coat
    b.inputs["Coat Roughness"].default_value = 0.05
    if emission is not None:
        b.inputs["Emission Color"].default_value = (*emission, 1)
        b.inputs["Emission Strength"].default_value = strength
    MATS[name] = m
    return m


def mat_neon(name, color, strength=3.8, base=None, rough=0.20):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = _principled(m)
    b.inputs["Base Color"].default_value = (*(base if base else color), 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Emission Color"].default_value = (*color, 1)
    b.inputs["Emission Strength"].default_value = strength
    MATS[name] = m
    return m


def _set_blended(m):
    for attr, val in (("surface_render_method", "BLENDED"),
                      ("blend_method", "BLEND")):
        try:
            setattr(m, attr, val)
            break
        except Exception:
            pass
    for attr in ("use_transparency_overlap", "use_transparent_shadow"):
        try:
            setattr(m, attr, True)
        except Exception:
            pass


def mat_jelly(name, color, rim, alpha=0.95, rim_strength=2.8):
    """Rich, vibrant, seamless stylized jelly material matching the reference sheet (no alpha-sort seams)."""
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (640, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (20, 80)
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Roughness"].default_value = 0.05
    bsdf.inputs["IOR"].default_value = 1.48
    bsdf.inputs["Coat Weight"].default_value = 1.0
    bsdf.inputs["Coat Roughness"].default_value = 0.03
    bsdf.inputs["Specular IOR Level"].default_value = 0.85

    # Use Geometry Normal Z for smooth top-to-bottom inner jelly glow (no UV/Generated seam!)
    geom = nt.nodes.new("ShaderNodeNewGeometry")
    geom.location = (-900, 260)
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    sep.location = (-700, 260)
    map_range = nt.nodes.new("ShaderNodeMapRange")
    map_range.location = (-540, 260)
    map_range.inputs["From Min"].default_value = -0.8
    map_range.inputs["From Max"].default_value = 0.9
    grad = nt.nodes.new("ShaderNodeValToRGB")
    grad.location = (-340, 260)
    deep = tuple(min(1.0, c * 0.42) for c in color)
    bright = tuple(min(1.0, c * 1.15 + 0.04) for c in rim)
    grad.color_ramp.elements[0].position = 0.05
    grad.color_ramp.elements[0].color = (*deep, 1)
    grad.color_ramp.elements[1].position = 0.95
    grad.color_ramp.elements[1].color = (*bright, 1)
    mid = grad.color_ramp.elements.new(0.50)
    mid.color = (*color, 1)
    bsdf.inputs["Emission Strength"].default_value = 0.55

    # Fresnel rim glow for translucent gummy edge appearance
    lw = nt.nodes.new("ShaderNodeLayerWeight")
    lw.location = (-520, -180)
    lw.inputs["Blend"].default_value = 0.28
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-300, -180)
    ramp.color_ramp.elements[0].position = 0.15
    ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
    ramp.color_ramp.elements[1].position = 0.82
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)

    em = nt.nodes.new("ShaderNodeEmission")
    em.location = (20, -220)
    em.inputs["Color"].default_value = (*bright, 1)
    em.inputs["Strength"].default_value = rim_strength * 0.75

    mix = nt.nodes.new("ShaderNodeMixShader")
    mix.location = (360, 0)
    link = nt.links.new
    link(geom.outputs["Normal"], sep.inputs["Vector"])
    link(sep.outputs["Z"], map_range.inputs["Value"])
    link(map_range.outputs["Result"], grad.inputs["Fac"])
    link(grad.outputs["Color"], bsdf.inputs["Base Color"])
    link(grad.outputs["Color"], bsdf.inputs["Emission Color"])
    link(lw.outputs["Facing"], ramp.inputs["Fac"])
    link(ramp.outputs["Color"], mix.inputs["Fac"])
    link(bsdf.outputs["BSDF"], mix.inputs[1])
    link(em.outputs["Emission"], mix.inputs[2])
    link(mix.outputs["Shader"], out.inputs["Surface"])
    MATS[name] = m
    return m


def mat_jelly_core(name, color, rim):
    """Luminous inner nucleus giving the jelly rich depth."""
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = _principled(m)
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Roughness"].default_value = 0.25
    b.inputs["Emission Color"].default_value = (*rim, 1)
    b.inputs["Emission Strength"].default_value = 2.2
    MATS[name] = m
    return m


def mat_cracked_glass(name, tint, crack_color):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (700, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (0, 100)
    bsdf.inputs["Base Color"].default_value = (*tint, 1)
    bsdf.inputs["Roughness"].default_value = 0.10
    bsdf.inputs["IOR"].default_value = 1.48
    bsdf.inputs["Alpha"].default_value = 0.82
    bsdf.inputs["Coat Weight"].default_value = 1.0
    bsdf.inputs["Emission Color"].default_value = (*tint, 1)
    bsdf.inputs["Emission Strength"].default_value = 0.55
    tc = nt.nodes.new("ShaderNodeTexCoord")
    tc.location = (-760, -160)
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.location = (-540, -160)
    vor.feature = 'DISTANCE_TO_EDGE'
    vor.inputs["Scale"].default_value = 2.8
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-320, -160)
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (1, 1, 1, 1)
    ramp.color_ramp.elements[1].position = 0.045
    ramp.color_ramp.elements[1].color = (0, 0, 0, 1)
    em = nt.nodes.new("ShaderNodeEmission")
    em.location = (0, -220)
    em.inputs["Color"].default_value = (*crack_color, 1)
    em.inputs["Strength"].default_value = 3.2
    mix = nt.nodes.new("ShaderNodeMixShader")
    mix.location = (350, 0)
    link = nt.links.new
    link(tc.outputs["Object"], vor.inputs["Vector"])
    link(vor.outputs["Distance"], ramp.inputs["Fac"])
    link(ramp.outputs["Color"], mix.inputs["Fac"])
    link(bsdf.outputs["BSDF"], mix.inputs[1])
    link(em.outputs["Emission"], mix.inputs[2])
    link(mix.outputs["Shader"], out.inputs["Surface"])
    _set_blended(m)
    MATS[name] = m
    return m


def mat_hazard(name, color_a, color_b, scale=2.2):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (500, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.location = (220, 0)
    bsdf.inputs["Roughness"].default_value = 0.28
    bsdf.inputs["Coat Weight"].default_value = 0.35
    tc = nt.nodes.new("ShaderNodeTexCoord")
    tc.location = (-680, 0)
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.location = (-480, 0)
    mp.inputs["Rotation"].default_value = (0, math.radians(45), 0)
    mp.inputs["Scale"].default_value = (1, 1, 1)
    wave = nt.nodes.new("ShaderNodeTexWave")
    wave.location = (-260, 0)
    wave.wave_type = 'BANDS'
    wave.bands_direction = 'X'
    wave.inputs["Scale"].default_value = scale
    wave.inputs["Distortion"].default_value = 0.0
    wave.inputs["Detail"].default_value = 0.0
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-20, 0)
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (*color_a, 1)
    ramp.color_ramp.elements[1].position = 0.5
    ramp.color_ramp.elements[1].color = (*color_b, 1)
    link = nt.links.new
    link(tc.outputs["Object"], mp.inputs["Vector"])
    link(mp.outputs["Vector"], wave.inputs["Vector"])
    link(wave.outputs["Fac"], ramp.inputs["Fac"])
    link(ramp.outputs["Color"], bsdf.inputs["Base Color"])
    link(bsdf.outputs["BSDF"], out.inputs["Surface"])
    MATS[name] = m
    return m


def mat_floor(name):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = _principled(m)
    b.inputs["Base Color"].default_value = (*C["stage_bg"], 1)
    b.inputs["Roughness"].default_value = 0.35
    b.inputs["Metallic"].default_value = 0.25
    b.inputs["Coat Weight"].default_value = 0.20
    b.inputs["Coat Roughness"].default_value = 0.12
    MATS[name] = m
    return m


# ------------------------------------------------------------- primitives ---
def to_coll(ob, coll):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)
    return ob


def place(ob, empty):
    ob.parent = empty
    return ob


def apply_scale(ob):
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.select_set(False)


def add_bevel(ob, width=0.03, segments=3, angle=0.55):
    if width <= 0:
        return
    m = ob.modifiers.new("Bevel", 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = angle
    m.harden_normals = False
    return m


def smooth(ob):
    for p in ob.data.polygons:
        p.use_smooth = True


def cube(name, loc, dims, coll, mat, rot=(0, 0, 0), bevel=0.0, bseg=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    ob.scale = dims
    apply_scale(ob)
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    if bevel:
        add_bevel(ob, min(bevel, min(dims) * 0.45), bseg)
        smooth(ob)
    return ob


def sphere(name, loc, radius, coll, mat, seg=48, ring=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring,
                                         radius=radius, location=loc)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    smooth(ob)
    return ob


def jelly_dome(name, loc, radius, coll, mat, squash_z=1.0, widen_xy=1.0, seg=64, ring=32):
    """Creates an organic, plump, flat-bottomed jelly pudding/droplet dome matching the reference sheet."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=1.0, location=(0, 0, 0))
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    me = ob.data
    for v in me.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        # z is in [-1, 1]. Map bottom (-1) to 0.0 (flat soft base) and top (+1) to ~1.55
        t = (z + 1.0) * 0.5  # 0 at bottom, 1 at top
        # Smooth flat base with slight rounded bevel near bottom
        if t < 0.15:
            s_t = t / 0.15
            nz = 0.08 * (s_t * s_t)
        else:
            nz = 0.08 + 1.48 * pow((t - 0.15) / 0.85, 0.88)
        # Belly bulge around t = 0.28, gently tapering toward the dome crown
        belly = 1.0 + 0.24 * math.sin(min(1.0, max(0.0, t)) * math.pi) - 0.10 * (t * t)
        v.co.x = x * belly * radius * widen_xy
        v.co.y = y * belly * radius * widen_xy
        v.co.z = nz * radius * squash_z
    me.update()
    ob.location = loc
    to_coll(ob, coll)
    if mat:
        me.materials.append(mat)
    smooth(ob)
    return ob


def obstacle_pedestal(prefix, empty, coll, width=6.70, depth=1.55, height=0.28):
    """Creates the signature chunky rounded-end purple/blue platform base under every obstacle."""
    base_mat = mat_plastic(prefix + "_PedBase", C["indigo"], rough=0.26, metal=0.25, coat=0.5,
                           emission=C["purple_mid"], strength=0.18)
    top_mat = mat_plastic(prefix + "_PedTop", C["purple"], rough=0.22, metal=0.20, coat=0.6,
                          emission=C["purple"], strength=0.15)
    rim_mat = mat_neon(prefix + "_PedRim", C["cyan_light"], 3.2)

    half_span = max(0.2, (width - depth) * 0.5)
    r = depth * 0.5

    # Main lower body + rounded cylinder ends
    c_mid = cube(prefix + "_PedMid", (0, 0, height * 0.5), (half_span * 2.0, depth, height),
                 coll, base_mat, bevel=0.05)
    place(c_mid, empty)
    for sx in (-half_span, half_span):
        c_end = cylinder(prefix + "_PedEnd", (sx, 0, height * 0.5), r, height,
                         coll, base_mat, verts=36, bevel=0.05)
        place(c_end, empty)

    # Raised inner platform lip
    top_w = width - 0.26
    top_d = depth - 0.22
    top_span = max(0.2, (top_w - top_d) * 0.5)
    t_mid = cube(prefix + "_PedTopMid", (0, 0, height + 0.04), (top_span * 2.0, top_d, 0.10),
                 coll, top_mat, bevel=0.03)
    place(t_mid, empty)
    for sx in (-top_span, top_span):
        t_end = cylinder(prefix + "_PedTopEnd", (sx, 0, height + 0.04), top_d * 0.5, 0.10,
                         coll, top_mat, verts=36, bevel=0.03)
        place(t_end, empty)

    # Glowing front/side rim strip near bottom of pedestal
    f_rim = cube(prefix + "_PedRimFront", (0, -r - 0.01, 0.05), (half_span * 2.0, 0.04, 0.045),
                 coll, rim_mat, bevel=0.01)
    place(f_rim, empty)
    return height + 0.08


def cylinder(name, loc, radius, depth, coll, mat, rot=(0, 0, 0), verts=36, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth,
                                        location=loc, rotation=rot)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    smooth(ob)
    if bevel:
        add_bevel(ob, bevel, 3)
    return ob


def cone(name, loc, r1, r2, depth, coll, mat, rot=(0, 0, 0), verts=36, bevel=0.0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2,
                                    depth=depth, location=loc, rotation=rot)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    if bevel:
        add_bevel(ob, bevel, 2)
    return ob


def torus(name, loc, major, minor, coll, mat, rot=(0, 0, 0), mseg=56, nseg=16):
    bpy.ops.mesh.primitive_torus_add(major_segments=mseg, minor_segments=nseg,
                                     major_radius=major, minor_radius=minor,
                                     location=loc, rotation=rot)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    smooth(ob)
    return ob


def ico(name, loc, radius, coll, mat, subdiv=3):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=loc)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    smooth(ob)
    return ob


def from_mesh(name, verts, faces, coll, mat, bevel=0.0, shade_smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.update()
    ob = bpy.data.objects.new(name, me)
    coll.objects.link(ob)
    if mat:
        me.materials.append(mat)
    if bevel:
        add_bevel(ob, bevel, 3)
    if shade_smooth:
        smooth(ob)
    return ob


def tube(name, p1, p2, radius, coll, mat, verts=16):
    p1 = Vector(p1)
    p2 = Vector(p2)
    d = p2 - p1
    mid = (p1 + p2) * 0.5
    ob = cylinder(name, mid, radius, d.length, coll, mat, verts=verts)
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = d.to_track_quat('Z', 'Y')
    return ob


def boolean_diff(target, cutter):
    mod = target.modifiers.new("Bool", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.solver = 'EXACT'
    mod.object = cutter
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def prism(name, pts, depth, coll, mat, plane='XZ', bevel=0.0):
    """Extruded polygon. pts are (a,b) in the chosen plane, extrusion centred."""
    n = len(pts)
    d = depth * 0.5
    verts = []
    if plane == 'XZ':
        verts = [(x, -d, z) for x, z in pts] + [(x, d, z) for x, z in pts]
    else:
        verts = [(x, y, -d) for x, y in pts] + [(x, y, d) for x, y in pts]
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    return from_mesh(name, verts, faces, coll, mat, bevel=bevel, shade_smooth=bool(bevel))


def arch_band(name, W, H, T, depth, coll, mat, seg=32, bevel=0.04):
    """Inverted-U arch frame (outer width W, height H, radial thickness T)."""
    R = W * 0.5
    r = R - T
    cz = H - R
    m = seg + 4
    outer = [(-R, 0.0), (-R, cz)]
    inner = [(-r, 0.0), (-r, cz)]
    for i in range(seg + 1):
        a = math.pi - math.pi * i / seg
        outer.append((R * math.cos(a), cz + R * math.sin(a)))
        inner.append((r * math.cos(a), cz + r * math.sin(a)))
    outer.append((R, 0.0))
    inner.append((r, 0.0))
    d = depth * 0.5
    verts = []
    verts += [(x, -d, z) for x, z in outer]
    verts += [(x, -d, z) for x, z in inner]
    verts += [(x, d, z) for x, z in outer]
    verts += [(x, d, z) for x, z in inner]
    faces = []
    for i in range(m - 1):
        faces.append((i, i + 1, m + i + 1, m + i))
        faces.append((2 * m + i, 3 * m + i, 3 * m + i + 1, 2 * m + i + 1))
        faces.append((i, 2 * m + i, 2 * m + i + 1, i + 1))
        faces.append((m + i, m + i + 1, 3 * m + i + 1, 3 * m + i))
    faces.append((0, m, 3 * m, 2 * m))
    faces.append((m - 1, 2 * m - 1, 3 * m - 1, 4 * m - 1))
    return from_mesh(name, verts, faces, coll, mat, bevel=bevel, shade_smooth=True)


def poly_curve(name, pts, radius, coll, mat, res=4, closed=False):
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    sp = cu.splines.new('NURBS')
    sp.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        w = p[3] if len(p) > 3 else 1.0
        sp.points[i].co = (p[0], p[1], p[2], w)
        if len(p) > 4:
            sp.points[i].radius = p[4]
    sp.order_u = min(4, len(pts))
    sp.use_endpoint_u = True
    sp.use_cyclic_u = closed
    sp.resolution_u = 8
    cu.bevel_depth = radius
    cu.bevel_resolution = res
    cu.use_fill_caps = True
    ob = bpy.data.objects.new(name, cu)
    coll.objects.link(ob)
    if mat:
        cu.materials.append(mat)
    return ob


def text(name, body, loc, size, coll, mat, rot=(0, 0, 0), extrude=0.012, align='CENTER', bevel=0.004):
    bpy.ops.object.text_add(location=loc, rotation=rot)
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    ob.data.body = body
    ob.data.size = size
    ob.data.align_x = align
    ob.data.align_y = 'CENTER'
    ob.data.extrude = extrude
    ob.data.bevel_depth = bevel
    ob.data.bevel_resolution = 2
    ob.data.materials.append(mat)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.convert(target='MESH')
    ob = bpy.context.object
    to_coll(ob, coll)
    return ob


def model_group(name, loc, coll):
    e = bpy.data.objects.new("GRP_" + name, None)
    e.empty_display_size = 0.6
    e.location = loc
    coll.objects.link(e)
    return e


def label(empty, coll, body, y, size, mat, z=0.025):
    ob = text("LBL_" + body[:18], body, (0, y, z), size, coll, mat, extrude=0.004, bevel=0.0)
    ob.parent = empty
    return ob


def finish(empty, coll):
    """Parent every not-yet-parented object created for this group."""
    for ob in list(coll.objects):
        if ob is empty or ob.type == 'EMPTY':
            continue
        if ob.parent is None:
            ob.parent = empty


def new_collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def aim(ob, target):
    d = Vector(target) - Vector(ob.location)
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
