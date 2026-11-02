# -*- coding: utf-8 -*-
"""Pickups: grow, shrink, split, merge, speed + shield, magnet, star, slow-time (High-Detail v2)."""
import math
import bpy
from .common import (
    C, srgb, mat_jelly, mat_jelly_core, mat_plastic, mat_neon, sphere, torus,
    cylinder, cone, cube, model_group, label, prism, from_mesh, poly_curve,
    boolean_diff, place, apply_scale, finish, text, smooth,
)


def _glow(name, color, strength=3.8):
    return mat_neon(name, color, strength, base=color)


def pickup_sphere(prefix, empty, coll, color, rim, plus=True):
    glass = mat_jelly(prefix + "_Glass", color, rim, alpha=1.0, rim_strength=3.2)
    white = mat_neon("Pickup_WhiteIcon", C["white"], 4.2)
    glint = mat_neon("Pickup_Glint", C["white"], 4.8)

    cz = 1.10
    r = 0.62
    # Glossy jelly sphere
    body = sphere(prefix + "_Body", (0, 0, cz), r, coll, glass, seg=64, ring=32)
    place(body, empty)

    # Upper-left curved cartoon highlight on front of sphere
    g1 = sphere(prefix + "_Highlight", (-0.24, -0.56, cz + 0.30), 0.11, coll, glint, seg=20, ring=12)
    g1.scale = (1.35, 0.25, 0.68)
    g1.rotation_euler = (math.radians(26), math.radians(-24), math.radians(15))
    apply_scale(g1)
    place(g1, empty)

    # Single centered, chunky rounded-pill '+' or '-' icon on the front face
    bar_h = cube(prefix + "_BarH", (0, -0.58, cz), (0.60, 0.14, 0.19), coll, white, bevel=0.06)
    place(bar_h, empty)
    if plus:
        bar_v = cube(prefix + "_BarV", (0, -0.58, cz), (0.19, 0.14, 0.60), coll, white, bevel=0.06)
        place(bar_v, empty)

    # Floating glowing particles around the orb (matching reference aura)
    part_mat = _glow(prefix + "_Particle", rim, 4.5)
    pts = (
        (-0.74, -0.10, cz + 0.34, 0.045),
        (-0.80, 0.0, cz - 0.15, 0.035),
        (0.74, -0.10, cz + 0.30, 0.042),
        (0.78, 0.05, cz - 0.22, 0.038),
        (-0.52, -0.15, cz - 0.56, 0.032),
        (0.55, -0.15, cz + 0.56, 0.035),
    )
    for i, (px, py, pz, pr) in enumerate(pts):
        sp = sphere(f"{prefix}_Spark_{i}", (px, py, pz), pr, coll, part_mat, seg=14, ring=8)
        place(sp, empty)

    return empty


def _puffy_3d_star(name, loc, r_outer, r_inner, depth, coll, mat):
    """Creates a true 3D faceted puffy 5-pointed game star with raised front/back center apex."""
    verts = [(0.0, -depth * 0.5, 0.0), (0.0, depth * 0.5, 0.0)]  # 0: front apex, 1: back apex
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        rr = r_outer if i % 2 == 0 else r_inner
        verts.append((rr * math.cos(a), 0.0, rr * math.sin(a)))
    faces = []
    for i in range(10):
        c_idx = 2 + i
        n_idx = 2 + ((i + 1) % 10)
        faces.append((0, c_idx, n_idx))
        faces.append((1, n_idx, c_idx))
    ob = from_mesh(name, verts, faces, coll, mat, bevel=0.035, shade_smooth=True)
    ob.location = loc
    return ob


def _sculpted_3d_heart(name, loc, scale, depth, coll, mat):
    """Creates a plump, smooth 3D heart icon for the center of the Shield pickup."""
    pts = []
    n = 36
    for i in range(n):
        t = 2.0 * math.pi * i / n
        # Classic parametric heart curve scaled to unit range
        hx = 16.0 * (math.sin(t) ** 3) / 17.0
        hz = (13.0 * math.cos(t) - 5.0 * math.cos(2 * t) - 2.0 * math.cos(3 * t) - math.cos(4 * t)) / 17.0
        pts.append((hx * scale, hz * scale))
    ob = prism(name, pts, depth, coll, mat, plane='XZ', bevel=0.035)
    ob.location = loc
    return ob


def build_pickups(coll, label_mat):
    groups = []

    # 1. Büyüme Küresi (+)
    e = model_group("Pickup_Grow", (-12, 10, 0), coll)
    pickup_sphere("Grow", e, coll, C["green"], C["green_light"], plus=True)
    label(e, coll, "BÜYÜME (+)   +25", -1.25, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 2. Küçülme Küresi (-)
    e = model_group("Pickup_Shrink", (-6, 10, 0), coll)
    pickup_sphere("Shrink", e, coll, C["red"], C["pink"], plus=False)
    label(e, coll, "KÜÇÜLME (-)   -20", -1.25, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 3. Bölünme Kristali (÷2) — Faceted purple/magenta diamond + tilted swirl ring + crystal shards
    e = model_group("Pickup_Split", (0, 10, 0), coll)
    crystal = mat_jelly("SplitCrystal", C["purple"], C["magenta"], alpha=0.96, rim_strength=3.8)
    edge_glow = _glow("SplitEdgeGlow", C["pink_light"], 3.5)
    cz = 1.15
    top_pyr = cone("Split_Top", (0, 0, cz + 0.38), 0.68, 0.0, 0.76, coll, crystal,
                   rot=(0, 0, math.radians(45)), verts=4, bevel=0.025)
    bot_pyr = cone("Split_Bottom", (0, 0, cz - 0.38), 0.68, 0.0, 0.76, coll, crystal,
                   rot=(math.pi, 0, math.radians(45)), verts=4, bevel=0.025)
    place(top_pyr, e)
    place(bot_pyr, e)

    # Tilted glowing purple/magenta energy swirl orbit ring around waist
    swirl1 = torus("Split_SwirlMain", (0, 0, cz), 0.82, 0.052, coll,
                   _glow("SplitSwirlMag", C["magenta"], 4.2),
                   rot=(math.radians(74), math.radians(-14), 0), mseg=64, nseg=14)
    swirl2 = torus("Split_SwirlSub", (0, 0, cz - 0.04), 0.92, 0.024, coll,
                   _glow("SplitSwirlPink", C["pink_light"], 3.5),
                   rot=(math.radians(68), math.radians(12), 0), mseg=64, nseg=10)
    place(swirl1, e)
    place(swirl2, e)

    # Centered bold '÷2' on front of crystal
    txt = _glow("SplitText", C["white"], 5.0)
    t = text("Split_Text", "÷2", (0.02, -0.56, cz + 0.02), 0.46, coll, txt,
             rot=(math.radians(90), 0, 0), extrude=0.025, bevel=0.008)
    place(t, e)

    # Floating crystal shards around the gem
    shard_specs = (
        (-0.62, -0.15, cz + 0.66, 0.11, (0.3, -0.5, 0.2)),
        (0.62, -0.15, cz + 0.66, 0.11, (-0.3, 0.5, -0.2)),
        (-0.88, 0.0, cz + 0.24, 0.09, (0.4, -0.7, 0.1)),
        (0.88, 0.0, cz + 0.24, 0.09, (-0.4, 0.7, -0.1)),
        (-0.66, -0.10, cz - 0.54, 0.09, (2.5, -0.4, 0.2)),
        (0.66, -0.10, cz - 0.54, 0.09, (2.5, 0.4, -0.2)),
    )
    for i, (sx, sy, sz, sr, srot) in enumerate(shard_specs):
        sh = cone(f"Split_Shard_{i}", (sx, sy, sz), sr * 0.55, 0.0, sr * 1.9,
                  coll, edge_glow, rot=srot, verts=4)
        place(sh, e)

    label(e, coll, "BÖLÜNME (÷2)", -1.6, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 4. Birleşme Halkası (><) — Chunky bevelled golden ring + dark warm inner recess + bold golden >< chevrons
    e = model_group("Pickup_Merge", (6, 10, 0), coll)
    gold_body = mat_plastic("MergeGoldBody", C["gold"], rough=0.18, metal=0.45, coat=0.7,
                            emission=C["gold"], strength=0.65)
    gold_bright = _glow("MergeGoldBright", C["yellow"], 3.2)
    inner_dk = mat_plastic("MergeInnerDark", srgb("#3A2408"), rough=0.35, metal=0.2)
    cz = 1.12

    # Outer thick golden torus ring + inner recessed dark coin disc
    ring_outer = torus("Merge_OuterRing", (0, 0, cz), 0.62, 0.135, coll, gold_body,
                       rot=(math.radians(82), math.radians(-8), 0), mseg=64, nseg=20)
    place(ring_outer, e)
    disc_in = cylinder("Merge_InnerDisc", (0, 0.03, cz), 0.52, 0.08, coll, inner_dk,
                       rot=(math.radians(82), math.radians(-8), 0), verts=48)
    place(disc_in, e)
    ring_bezel = torus("Merge_InnerBezel", (0, -0.04, cz), 0.50, 0.032, coll, gold_bright,
                       rot=(math.radians(82), math.radians(-8), 0), mseg=64, nseg=12)
    place(ring_bezel, e)

    # Two chunky rounded golden chevrons '>' and '<' inside the ring
    # Left '>' chevron (two angled capsule bars meeting at x = -0.06)
    for s_z in (-1, 1):
        b_l = cube("Merge_ChevL", (-0.22, -0.06, cz + s_z * 0.095), (0.30, 0.09, 0.095),
                   coll, gold_bright, rot=(0, math.radians(s_z * 34), 0), bevel=0.04)
        place(b_l, e)
        b_r = cube("Merge_ChevR", (0.22, -0.06, cz + s_z * 0.095), (0.30, 0.09, 0.095),
                   coll, gold_bright, rot=(0, math.radians(-s_z * 34), 0), bevel=0.04)
        place(b_r, e)

    # Subtle tilted golden orbit trail around the ring
    orbit = torus("Merge_Orbit", (0, 0, cz), 0.84, 0.018, coll, gold_bright,
                  rot=(math.radians(72), math.radians(16), 0), mseg=64, nseg=8)
    place(orbit, e)

    label(e, coll, "BİRLEŞME (><)", -1.55, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # 5. Süper Hız Bandı (SpeedBoost) — Trapezoidal bevelled cyan booster pad with 2 forward '^^' chevrons
    e = model_group("Pickup_Speed", (12, 10, 0), coll)
    frame_cyan = _glow("SpeedFrameCyan", C["cyan"], 3.8)
    plate_dk = mat_plastic("SpeedPlateDark", srgb("#0B3878"), rough=0.22, metal=0.35, coat=0.6,
                           emission=C["cyan_deep"], strength=0.35)
    chev_glow = _glow("SpeedChevGlow", C["cyan_light"], 5.2)

    tilt = math.radians(38)
    cz_pad = 0.68
    # Trapezoidal outer glowing cyan frame
    outer_pts = [(-1.12, -0.78), (1.12, -0.78), (0.86, 0.78), (-0.86, 0.78)]
    pad_frame = prism("Speed_OuterFrame", outer_pts, 0.16, coll, frame_cyan, plane='XY', bevel=0.05)
    pad_frame.location = (0, 0, cz_pad)
    pad_frame.rotation_euler = (tilt, 0, 0)
    place(pad_frame, e)

    # Recessed dark-blue inner panel
    inner_pts = [(-0.94, -0.62), (0.94, -0.62), (0.72, 0.62), (-0.72, 0.62)]
    pad_inner = prism("Speed_InnerPlate", inner_pts, 0.14, coll, plate_dk, plane='XY', bevel=0.03)
    pad_inner.location = (0, -0.03, cz_pad + 0.03)
    pad_inner.rotation_euler = (tilt, 0, 0)
    place(pad_inner, e)

    # Two wide forward-pointing ('^' pointing UP along the pad) glowing cyan V-chevron stripes
    for i, y_off in enumerate((-0.26, 0.16)):
        w_scale = 1.0 - i * 0.12
        chev_pts = [
            (-0.58 * w_scale, y_off - 0.16),
            (0.0, y_off + 0.16),
            (0.58 * w_scale, y_off - 0.16),
            (0.58 * w_scale, y_off - 0.34),
            (0.0, y_off - 0.02),
            (-0.58 * w_scale, y_off - 0.34),
        ]
        ch = prism(f"Speed_ChevronV_{i}", chev_pts, 0.08, coll, chev_glow, plane='XY', bevel=0.02)
        ch.location = (0, -0.06, cz_pad + 0.06)
        ch.rotation_euler = (tilt, 0, 0)
        place(ch, e)

    label(e, coll, "SÜPER HIZ BANDI (⚡)  ·  +35%", -1.9, 0.32, label_mat)
    finish(e, coll)
    groups.append(e)
    return groups


def build_extra_pickups(coll, label_mat):
    groups = []

    # 1. Kalkan (Shield) — Chunky bevelled violet/magenta heater shield + plump 3D white/pink HEART
    e = model_group("Pickup_Shield", (-9, 4, 0), coll)
    cz = 1.32
    shield_outer_pts = [
        (-0.52, 0.56), (0.0, 0.64), (0.52, 0.56),
        (0.50, 0.02), (0.34, -0.36), (0.0, -0.66),
        (-0.34, -0.36), (-0.50, 0.02),
    ]
    rim_mat = mat_plastic("ShieldRim", C["magenta"], rough=0.18, coat=0.7,
                          emission=C["magenta"], strength=1.2)
    face_mat = mat_plastic("ShieldFace", C["purple"], rough=0.22, coat=0.6,
                           emission=C["purple"], strength=0.45)
    heart_mat = _glow("ShieldHeart", C["white"], 3.8)

    sh_outer = prism("Shield_OuterFrame", shield_outer_pts, 0.22, coll, rim_mat, plane='XZ', bevel=0.05)
    sh_outer.location = (0, 0, cz)
    place(sh_outer, e)

    sh_inner = prism("Shield_InnerFace", [(x * 0.80, z * 0.80) for x, z in shield_outer_pts],
                     0.24, coll, face_mat, plane='XZ', bevel=0.03)
    sh_inner.location = (0, -0.02, cz)
    place(sh_inner, e)

    heart = _sculpted_3d_heart("Shield_Heart", (0, -0.11, cz + 0.02), 0.26, 0.12, coll, heart_mat)
    place(heart, e)

    label(e, coll, "KALKAN  ·  1 Hasar Engeller", -1.5, 0.33, label_mat)
    finish(e, coll)
    groups.append(e)

    # 2. Mıknatıs (Magnet) — Chunky U-shaped horseshoe magnet tilted at -38° diagonal with silver pole ends
    e = model_group("Pickup_Magnet", (-3, 4, 0), coll)
    cz = 1.42
    red_mat = mat_plastic("MagnetRed", C["red"], rough=0.18, coat=0.75,
                          emission=C["red"], strength=0.45)
    silver_mat = mat_plastic("MagnetSilver", C["white"], rough=0.18, metal=0.65, coat=0.5,
                             emission=C["cyan_light"], strength=0.25)
    white_band = _glow("MagnetBand", C["white"], 2.5)

    # Create a parent pivot at (0, 0, cz) rotated by -38° around Y so the U-magnet opens toward top-right!
    piv = bpy.data.objects.new("Magnet_TiltPivot", None)
    piv.location = (0, 0, cz)
    piv.rotation_euler = (math.radians(12), math.radians(38), 0)
    coll.objects.link(piv)
    place(piv, e)

    # U-bend arch (half torus + two straight legs)
    u_rad = 0.36
    tube_r = 0.17
    arc = torus("Magnet_UArc", (0, 0, -0.08), u_rad, tube_r, coll, red_mat,
                rot=(math.pi / 2, 0, 0), mseg=48, nseg=24)
    cut = cube("Magnet_UCut", (0, 0, 0.42), (1.8, 1.2, 1.0), coll, None)
    boolean_diff(arc, cut)
    arc.parent = piv

    for sx in (-u_rad, u_rad):
        leg = cylinder("Magnet_Leg", (sx, 0, 0.10), tube_r, 0.36, coll, red_mat, verts=28)
        leg.parent = piv
        sep = cylinder("Magnet_Sep", (sx, 0, 0.30), tube_r * 1.01, 0.05, coll, white_band, verts=28)
        sep.parent = piv
        tip = cylinder("Magnet_PoleTip", (sx, 0, 0.45), tube_r * 1.02, 0.26, coll, silver_mat,
                       verts=28, bevel=0.03)
        tip.parent = piv

    label(e, coll, "MIKNATIS  ·  3 sn", -1.5, 0.33, label_mat)
    finish(e, coll)
    groups.append(e)

    # 3. Puan Yıldızı (Star) — Plump 3D faceted 5-pointed golden star (NO text on body!)
    e = model_group("Pickup_Star", (3, 4, 0), coll)
    cz = 1.36
    gold_star = mat_plastic("StarGold3D", C["gold"], rough=0.18, metal=0.45, coat=0.7,
                            emission=C["gold"], strength=0.75)
    st = _puffy_3d_star("Star_Faceted3D", (0, 0, cz), 0.72, 0.33, 0.38, coll, gold_star)
    st.rotation_euler = (math.radians(8), math.radians(-10), 0)
    place(st, e)

    # Small golden sparkles around the star
    spark_mat = _glow("StarSparkle", C["yellow"], 4.5)
    for i, (sx, sy, sz, sr) in enumerate(((-0.74, -0.10, cz + 0.42, 0.045),
                                          (0.76, -0.10, cz + 0.32, 0.040),
                                          (-0.58, -0.10, cz - 0.52, 0.036),
                                          (0.62, -0.10, cz - 0.48, 0.038))):
        sp = sphere(f"Star_Spark_{i}", (sx, sy, sz), sr, coll, spark_mat, seg=14, ring=8)
        place(sp, e)

    label(e, coll, "PUAN YILDIZI  +500", -1.55, 0.33, label_mat)
    finish(e, coll)
    groups.append(e)

    # 4. Yavaş Zaman (Hourglass) — Stylized chunky purple caps & curved side pillars + glass bulbs + golden sand
    e = model_group("Pickup_Slow", (9, 4, 0), coll)
    frame_purp = mat_plastic("HourPurple", C["purple"], rough=0.22, coat=0.65,
                             emission=C["magenta"], strength=0.45)
    glass_bulb = mat_jelly("HourGlassBulb", C["glass"], C["cyan_light"], alpha=0.52, rim_strength=2.2)
    sand_gold = _glow("HourSandGold", C["gold"], 2.8)

    # Chunky rounded purple top and bottom caps
    for z_cap in (0.72, 2.02):
        cap = cylinder("Hour_Cap", (0, 0, z_cap), 0.52, 0.15, coll, frame_purp, verts=40, bevel=0.045)
        place(cap, e)
        rim = torus("Hour_CapRim", (0, 0, z_cap + (0.06 if z_cap < 1.0 else -0.06)),
                    0.48, 0.035, coll, frame_purp, mseg=40, nseg=12)
        place(rim, e)

    # Curved purple side pillars hugging the hourglass
    for sx in (-1, 1):
        pts = [
            (sx * 0.46, 0.0, 0.78),
            (sx * 0.52, 0.0, 1.08),
            (sx * 0.36, 0.0, 1.37),
            (sx * 0.52, 0.0, 1.66),
            (sx * 0.46, 0.0, 1.96),
        ]
        pil = poly_curve(f"Hour_SidePillar_{sx}", pts, 0.065, coll, frame_purp, res=6)
        place(pil, e)

    # Upper & lower glass bulbs
    u_bulb = cone("Hour_UpperBulb", (0, 0, 1.66), 0.09, 0.42, 0.58, coll, glass_bulb, verts=36, bevel=0.03)
    l_bulb = cone("Hour_LowerBulb", (0, 0, 1.08), 0.42, 0.09, 0.58, coll, glass_bulb, verts=36, bevel=0.03)
    place(u_bulb, e)
    place(l_bulb, e)

    # Golden sand in upper bulb, falling stream, and lower sand mound
    u_sand = cone("Hour_UpperSand", (0, 0, 1.56), 0.07, 0.30, 0.32, coll, sand_gold, verts=32, bevel=0.02)
    l_sand = cone("Hour_LowerSand", (0, 0, 0.96), 0.35, 0.05, 0.34, coll, sand_gold, verts=32, bevel=0.03)
    stream = cylinder("Hour_SandStream", (0, 0, 1.28), 0.028, 0.52, coll, sand_gold, verts=16)
    place(u_sand, e)
    place(l_sand, e)
    place(stream, e)

    label(e, coll, "YAVAŞ ZAMAN  ·  2 sn", -1.55, 0.33, label_mat)
    finish(e, coll)
    groups.append(e)
    return groups
