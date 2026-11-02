# -*- coding: utf-8 -*-
"""Jelly characters: small / normal / big, split twins, merge, and 3 liquid FX molds (High-Detail v2)."""
import math
import bpy
from .common import (
    C, mat_jelly, mat_jelly_core, mat_plastic, mat_neon, sphere, jelly_dome,
    torus, cone, cylinder, poly_curve, model_group, label, place, apply_scale,
    finish, smooth, to_coll,
)


def _mats(kind):
    if kind == "green":
        return (
            mat_jelly("JellyGreen", C["green"], C["green_light"], alpha=0.95, rim_strength=3.2),
            mat_jelly_core("JellyGreenCore", C["green_light"], C["green"]),
        )
    if kind == "purple":
        return (
            mat_jelly("JellyPurple", C["purple"], C["magenta"], alpha=0.95, rim_strength=3.2),
            mat_jelly_core("JellyPurpleCore", C["magenta"], C["pink"]),
        )
    return (
        mat_jelly("JellyCyan", C["cyan"], C["cyan_light"], alpha=0.95, rim_strength=3.2),
        mat_jelly_core("JellyCyanCore", C["cyan_light"], C["cyan"]),
    )


def _teardrop(name, loc, radius, coll, mat, rot=(0, 0, 0), stretch=1.65):
    """Creates a glossy teardrop droplet mesh."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=16, radius=1.0, location=(0, 0, 0))
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    for v in ob.data.vertices:
        z = v.co.z
        t = (z + 1.0) * 0.5  # 0 bottom, 1 tip
        taper = (1.0 - 0.65 * (t * t)) if z > 0 else 1.0
        v.co.x *= radius * taper
        v.co.y *= radius * taper
        v.co.z *= radius * (stretch if z > 0 else 0.95)
    ob.data.update()
    ob.location = loc
    ob.rotation_euler = rot
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    smooth(ob)
    return ob


def _wavy_puddle(name, loc, radius, thickness, waves, amp, coll, mat):
    """Creates an organic wavy liquid splash puddle base."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=1.0, depth=1.0, location=(0, 0, 0))
    ob = bpy.context.object
    ob.name = name
    ob.data.name = name
    for v in ob.data.vertices:
        ang = math.atan2(v.co.y, v.co.x)
        r_mod = 1.0 + amp * math.sin(ang * waves) + (amp * 0.45) * math.cos(ang * (waves + 3))
        v.co.x *= radius * r_mod
        v.co.y *= radius * r_mod
        v.co.z = (v.co.z + 0.5) * thickness
    ob.data.update()
    ob.location = loc
    to_coll(ob, coll)
    if mat:
        ob.data.materials.append(mat)
    smooth(ob)
    mod = ob.modifiers.new("Subd", 'SUBSURF')
    mod.levels = 1
    mod.render_levels = 2
    return ob


def jelly(name, empty, coll, r, kind="cyan", z=0.0, x=0.0, y=0.0,
          squash_z=1.0, widen_xy=1.0, green_shards=False):
    outer_mat, _ = _mats(kind)
    glint = mat_neon("GlintWhite", C["white"], strength=4.5)

    # 1. Plump, flat-bottomed organic jelly dome
    body = jelly_dome(name + "_Body", (x, y, z), r, coll, outer_mat,
                      squash_z=squash_z, widen_xy=widen_xy, seg=72, ring=36)
    place(body, empty)

    # 2. Subtle glowing contact rim right at the base edge
    aura_col = C["green"] if kind == "green" else (C["magenta"] if kind == "purple" else C["cyan"])
    aura_mat = mat_neon("JellyAura_" + kind, aura_col, 2.2)
    aura = torus(name + "_BaseAura", (x, y, z + 0.02), r * 0.96 * widen_xy, 0.018 * r,
                 coll, aura_mat, mseg=56, nseg=12)
    place(aura, empty)

    # 3. Signature cartoon glossy highlights nestled on upper-left front surface (-Y is front)
    h_z = z + r * 1.02 * squash_z
    g1 = sphere(name + "_GlintMain",
                (x - 0.30 * r * widen_xy, y - 0.82 * r * widen_xy, h_z),
                0.15 * r, coll, glint, seg=24, ring=16)
    g1.scale = (1.35, 0.25, 0.68)
    g1.rotation_euler = (math.radians(28), math.radians(-22), math.radians(16))
    apply_scale(g1)
    place(g1, empty)

    g2 = sphere(name + "_GlintDot",
                (x - 0.52 * r * widen_xy, y - 0.76 * r * widen_xy, z + r * 0.68 * squash_z),
                0.055 * r, coll, glint, seg=18, ring=12)
    g2.scale = (1.0, 0.30, 0.85)
    apply_scale(g2)
    place(g2, empty)

    # 4. Floating emerald crystal/jelly shards around the base of Big Jelly (matching reference)
    if green_shards:
        shard_mat = mat_neon("BigJellyShard", C["green_light"], 3.2)
        shard_specs = (
            (-1.12 * r, -0.35 * r, 0.12, 0.11, (0.3, -0.5, 0.2)),
            (-0.92 * r, -0.75 * r, 0.08, 0.09, (0.4, -0.3, 0.8)),
            (-0.58 * r, -0.98 * r, 0.06, 0.08, (0.2, 0.4, 0.5)),
            (1.12 * r, -0.32 * r, 0.12, 0.11, (-0.3, 0.5, -0.3)),
            (0.92 * r, -0.74 * r, 0.08, 0.09, (0.3, 0.4, -0.6)),
            (0.56 * r, -0.96 * r, 0.06, 0.08, (-0.2, -0.4, 0.4)),
        )
        for i, (sx, sy, sz, sr, srot) in enumerate(shard_specs):
            sh = cone(f"{name}_Shard_{i}", (x + sx, y + sy, z + sz),
                      sr * 0.65, 0.0, sr * 1.85, coll, shard_mat, rot=srot, verts=5)
            place(sh, empty)

    return empty


def _peanut_split_blob(name, empty, coll, r, x=0.0, y=0.0, z=0.0):
    """Creates the stretching figure-8 peanut blob ('Bölünme ÷2') from the reference sheet."""
    outer_mat, core_mat = _mats("cyan")
    glint = mat_neon("GlintWhite", C["white"], strength=5.5)

    bpy.ops.mesh.primitive_uv_sphere_add(segments=72, ring_count=36, radius=1.0, location=(0, 0, 0))
    ob = bpy.context.object
    ob.name = name + "_Peanut"
    ob.data.name = name + "_Peanut"
    for v in ob.data.vertices:
        vx, vy, vz = v.co.x, v.co.y, v.co.z
        t = (vz + 1.0) * 0.5
        # Stretch horizontally along X and pinch the waist at vx == 0
        sx = vx * 1.85
        pinch = 0.48 + 0.58 * min(1.0, abs(vx) * 1.35)
        belly = 1.0 + 0.20 * math.sin(min(1.0, max(0.0, t)) * math.pi)
        if t < 0.15:
            nz = 0.06 * ((t / 0.15) ** 2)
        else:
            nz = 0.06 + 1.25 * pow((t - 0.15) / 0.85, 0.88)
        v.co.x = sx * r
        v.co.y = vy * belly * pinch * r * 0.92
        v.co.z = nz * pinch * r
    ob.data.update()
    ob.location = (x, y, z)
    to_coll(ob, coll)
    ob.data.materials.append(outer_mat)
    smooth(ob)
    place(ob, empty)

    # Two highlights on left and right lobes
    for lx in (-0.95 * r, 0.65 * r):
        g = sphere(name + "_PeanutGlint", (x + lx, y - 0.72 * r, z + 0.78 * r),
                   0.12 * r, coll, glint, seg=20, ring=12)
        g.scale = (1.3, 0.3, 0.68)
        g.rotation_euler = (math.radians(22), math.radians(-18), 0)
        apply_scale(g)
        place(g, empty)


def build_jelly_variants(coll, label_mat):
    groups = []

    # 1. Küçük Jöle (<= 75, Ø 0.62 - 0.85 m)
    e = model_group("Jelly_Small", (-12, 18, 0), coll)
    jelly("Small", e, coll, 0.44, squash_z=0.92, widen_xy=1.08)
    label(e, coll, "KÜÇÜK JÖLE  Ø0.85 m", -1.55, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 2. Normal Jöle (76 - 114, Ø ~1.0 m)
    e = model_group("Jelly_Normal", (-6, 18, 0), coll)
    jelly("Normal", e, coll, 0.56, squash_z=0.95, widen_xy=1.08)
    label(e, coll, "NORMAL JÖLE  Ø1.0 m", -1.75, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 3. Dev Jöle (>= 115, Ø 1.16 - 1.65 m)
    e = model_group("Jelly_Big", (0, 18, 0), coll)
    jelly("Big", e, coll, 0.78, kind="green", squash_z=0.96, widen_xy=1.10, green_shards=True)
    label(e, coll, "DEV JÖLE  Ø1.65 m  ·  ≥115", -2.35, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 4. Bölünme (÷2) & İkiz Parçalar (%50 + %50)
    e = model_group("Jelly_SplitTwin", (6, 18, 0), coll)
    _peanut_split_blob("SplitStretch", e, coll, 0.34, x=-0.82, y=0.18, z=0.0)
    jelly("SplitA", e, coll, 0.35, x=0.25, y=-0.05, squash_z=0.92, widen_xy=1.06)
    jelly("SplitB", e, coll, 0.35, x=1.08, y=-0.05, squash_z=0.92, widen_xy=1.06)
    label(e, coll, "İKİZ PARÇALAR  %50 + %50", -1.55, 0.32, label_mat)
    finish(e, coll)
    groups.append(e)

    # 5. Birleşme (><)
    e = model_group("Jelly_Merge", (12, 18, 0), coll)
    jelly("Merge", e, coll, 0.62, squash_z=0.95, widen_xy=1.08)
    label(e, coll, "BİRLEŞME  (><)", -2.05, 0.32, label_mat)
    finish(e, coll)
    groups.append(e)

    return groups


def build_jelly_effect_molds(coll, label_mat):
    """Builds the 3 Jelly Effect Molds from top-right of the reference sheet:
    1. Sıçrama (Squash & Stretch) — Plump squashed cyan bell dome + puddle rim + splashing teardrops
    2. Darbe (Çarpışma) — Purple/magenta crown liquid splash with upward curved tendrils & droplets
    3. Patlama (Parça Feda) — Cyan radial liquid burst with splash arms & flying teardrops
    """
    groups = []
    cyan_j, cyan_core = _mats("cyan")
    purp_j, purp_core = _mats("purple")
    cyan_neon = mat_neon("FX_CyanGlow", C["cyan_light"], 3.5)
    purp_neon = mat_neon("FX_PurpGlow", C["pink"], 3.8)
    glint = mat_neon("FX_Glint", C["white"], 5.5)

    # 1) Sıçrama (Squash & Stretch)
    e1 = model_group("FX_SquashSplash", (-18, 18, 0), coll)
    puddle1 = _wavy_puddle("FX_Squash_Puddle", (0, 0, 0.0), 0.88, 0.06, 7, 0.14, coll, cyan_j)
    place(puddle1, e1)
    # Pear/bell-shaped squashed jelly dome
    jelly("FX_Squash_Main", e1, coll, 0.56, kind="cyan", z=0.02, squash_z=0.82, widen_xy=1.22)
    # Splash droplets flying left and right from the base
    drops1 = (
        (-0.98, -0.15, 0.24, 0.11, (0, math.radians(-55), 0)),
        (-1.18, 0.10, 0.12, 0.08, (0, math.radians(-70), 0)),
        (0.98, -0.15, 0.24, 0.11, (0, math.radians(55), 0)),
        (1.18, 0.10, 0.12, 0.08, (0, math.radians(70), 0)),
        (-0.78, -0.45, 0.08, 0.07, (math.radians(40), math.radians(-40), 0)),
        (0.78, -0.45, 0.08, 0.07, (math.radians(40), math.radians(40), 0)),
    )
    for i, (dx, dy, dz, dr, drot) in enumerate(drops1):
        d = _teardrop(f"FX_Squash_Drop_{i}", (dx, dy, dz), dr, coll, cyan_j, rot=drot, stretch=1.6)
        place(d, e1)
    label(e1, coll, "SIÇRAMA (Squash & Stretch)", -1.65, 0.30, label_mat)
    finish(e1, coll)
    groups.append(e1)

    # 2) Darbe (Çarpışma) — Vibrant Purple Crown Liquid Splash
    e2 = model_group("FX_ImpactHit", (18, 18, 0), coll)
    puddle2 = _wavy_puddle("FX_Impact_Puddle", (0, 0, 0.0), 0.92, 0.07, 9, 0.18, coll, purp_j)
    place(puddle2, e2)
    jelly("FX_Impact_Core", e2, coll, 0.44, kind="purple", z=0.02, squash_z=0.72, widen_xy=1.25)

    # 9 upward-curving liquid splash tendrils + tip droplets
    tendril_specs = (
        (-0.22, -0.10, -0.68, -0.18, 0.96, 0.11),
        (0.05, -0.12, 0.18, -0.15, 1.15, 0.12),
        (0.26, -0.08, 0.72, -0.15, 0.92, 0.11),
        (-0.38, 0.05, -0.98, 0.08, 0.68, 0.09),
        (0.38, 0.05, 0.98, 0.08, 0.68, 0.09),
        (-0.15, 0.20, -0.45, 0.35, 0.85, 0.09),
        (0.20, 0.20, 0.52, 0.35, 0.82, 0.09),
    )
    for i, (x0, y0, x1, y1, h_top, rad) in enumerate(tendril_specs):
        xm = (x0 + x1) * 0.55
        ym = (y0 + y1) * 0.55
        pts = [
            (x0, y0, 0.18, 1.0, 1.35),
            (xm, ym, h_top * 0.58, 1.0, 0.75),
            (x1, y1, h_top, 1.0, 0.30),
        ]
        tc = poly_curve(f"FX_Impact_Tendril_{i}", pts, rad, coll, purp_j, res=6)
        place(tc, e2)
        # Tip droplet
        ang_y = math.atan2(x1 - x0, h_top)
        tip = _teardrop(f"FX_Impact_Tip_{i}", (x1 * 1.15, y1, h_top + 0.14), rad * 0.85,
                        coll, purp_neon, rot=(0, ang_y, 0), stretch=1.7)
        place(tip, e2)
    label(e2, coll, "DARBE (Çarpışma)", -1.65, 0.30, label_mat)
    finish(e2, coll)
    groups.append(e2)

    # 3) Patlama (Parça Feda) — Vibrant Cyan Radial Splash Crown & Flying Droplets
    e3 = model_group("FX_SacrificeBurst", (24, 18, 0), coll)
    puddle3 = _wavy_puddle("FX_Burst_Puddle", (0, 0, 0.0), 0.86, 0.07, 8, 0.18, coll, cyan_j)
    place(puddle3, e3)
    jelly("FX_Burst_Core", e3, coll, 0.38, kind="cyan", z=0.02, squash_z=0.65, widen_xy=1.28)

    burst_arms = (
        (-0.20, -0.10, -0.62, -0.15, 0.78, 0.10),
        (0.0, -0.12, 0.04, -0.18, 0.88, 0.11),
        (0.22, -0.10, 0.65, -0.15, 0.76, 0.10),
        (-0.34, 0.04, -0.88, 0.05, 0.52, 0.09),
        (0.34, 0.04, 0.88, 0.05, 0.52, 0.09),
    )
    for i, (x0, y0, x1, y1, h_top, rad) in enumerate(burst_arms):
        pts = [
            (x0, y0, 0.15, 1.0, 1.4),
            ((x0 + x1) * 0.5, (y0 + y1) * 0.5, h_top * 0.6, 1.0, 0.8),
            (x1, y1, h_top, 1.0, 0.35),
        ]
        ac = poly_curve(f"FX_Burst_Arm_{i}", pts, rad, coll, cyan_j, res=6)
        place(ac, e3)

    flying_drops = (
        (0.18, -0.12, 1.18, 0.13, (0, math.radians(15), 0)),
        (-0.58, -0.15, 0.96, 0.12, (0, math.radians(-32), 0)),
        (0.62, -0.14, 0.92, 0.12, (0, math.radians(35), 0)),
        (-0.98, -0.05, 0.66, 0.13, (0, math.radians(-52), 0)),
        (1.02, -0.05, 0.64, 0.14, (0, math.radians(52), 0)),
        (-1.12, 0.12, 0.34, 0.10, (0, math.radians(-68), 0)),
        (1.14, 0.12, 0.32, 0.10, (0, math.radians(68), 0)),
        (-0.28, 0.25, 0.82, 0.10, (0, math.radians(-18), 0)),
        (0.35, 0.25, 0.78, 0.10, (0, math.radians(22), 0)),
    )
    for i, (bx, by, bz, br, brot) in enumerate(flying_drops):
        fd = _teardrop(f"FX_Burst_Drop_{i}", (bx, by, bz), br, coll,
                       cyan_j if i % 3 != 0 else cyan_neon, rot=brot, stretch=1.55)
        place(fd, e3)

    label(e3, coll, "PATLAMA (Parça Feda)", -1.65, 0.30, label_mat)
    finish(e3, coll)
    groups.append(e3)

    return groups
