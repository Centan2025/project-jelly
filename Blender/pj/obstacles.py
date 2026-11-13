# -*- coding: utf-8 -*-
"""Obstacles and gates: narrow gate, piston, glass wall, tunnels, pendulum, spinner, multi-hole & Phase 4 (High-Detail v2)."""
import math
import bpy
from mathutils import Vector
from .common import (
    C, srgb, mat_plastic, mat_neon, mat_hazard, mat_cracked_glass, mat_jelly,
    cube, sphere, cylinder, cone, torus, arch_band, prism, obstacle_pedestal,
    model_group, label, place, boolean_diff, text, aim, tube, finish, from_mesh,
)


def _neon(name, color, strength=3.8):
    return mat_neon(name, color, strength, base=color)


def _fortress_buttress_frame(prefix, empty, coll, z0, wall_w=4.30, wall_h=2.18,
                             pillar_mat=None, trim_mat=None):
    """Builds the signature chunky angled purple/violet side buttress pillars + top armor trim from Row 3."""
    if pillar_mat is None:
        pillar_mat = mat_plastic(prefix + "_ButtressPurp", C["purple"], rough=0.22, coat=0.65,
                                 emission=C["magenta"], strength=0.22)
    if trim_mat is None:
        trim_mat = _neon(prefix + "_ButtressNeon", C["magenta"], 3.8)
    dark_trim = mat_plastic(prefix + "_DarkTrim", C["purple_dk"], rough=0.30, metal=0.45)

    for side in (-1, 1):
        px = side * (wall_w * 0.5 + 0.42)
        tilt = side * math.radians(3.5)
        # Main thick angled buttress column
        pil = cube(f"{prefix}_Buttress_{side}", (px, 0, z0 + wall_h * 0.50),
                   (0.96, 0.92, wall_h + 0.08), coll, pillar_mat,
                   rot=(0, tilt, 0), bevel=0.09)
        place(pil, empty)
        # Outer glowing vertical neon rib on the buttress
        rib = cube(f"{prefix}_ButtressRib_{side}", (px + side * 0.36, -0.36, z0 + wall_h * 0.50),
                   (0.12, 0.16, wall_h - 0.14), coll, trim_mat,
                   rot=(0, tilt, 0), bevel=0.03)
        place(rib, empty)
        # Base collar block
        col = cube(f"{prefix}_ButtressFoot_{side}", (px, 0, z0 + 0.12),
                   (1.06, 1.00, 0.24), coll, dark_trim, bevel=0.05)
        place(col, empty)


def _arrow_3d(name, loc, direction, scale, depth, coll, mat):
    """Builds a bold chunky 3D arrow ('DOWN', 'LEFT', 'RIGHT') matching the reference icons."""
    s = scale
    if direction == 'DOWN':
        pts = [
            (-0.18 * s, 0.45 * s), (0.18 * s, 0.45 * s),
            (0.18 * s, 0.02 * s), (0.42 * s, 0.02 * s),
            (0.0, -0.45 * s), (-0.42 * s, 0.02 * s),
            (-0.18 * s, 0.02 * s),
        ]
    elif direction == 'LEFT':
        pts = [
            (0.45 * s, 0.18 * s), (0.45 * s, -0.18 * s),
            (0.02 * s, -0.18 * s), (0.02 * s, -0.42 * s),
            (-0.45 * s, 0.0), (0.02 * s, 0.42 * s),
            (0.02 * s, 0.18 * s),
        ]
    else:  # RIGHT
        pts = [
            (-0.45 * s, 0.18 * s), (-0.45 * s, -0.18 * s),
            (-0.02 * s, -0.18 * s), (-0.02 * s, -0.42 * s),
            (0.45 * s, 0.0), (-0.02 * s, 0.42 * s),
            (-0.02 * s, 0.18 * s),
        ]
    ob = prism(name, pts, depth, coll, mat, plane='XZ', bevel=0.025)
    ob.location = loc
    return ob


def _build_clean_hole_wall(name, width, depth, z_min, z_max, hole_z, hole_r, coll, mat, segs=48):
    """Builds a clean quad-ring wall around a circular hole (ZERO N-gons, ZERO diagonal X-shading)."""
    hw = width * 0.5
    hd = depth * 0.5
    verts = []
    faces = []
    # For each angle, raycast from (0, hole_z) to the outer rectangle [+-hw, z_min..z_max]
    for i in range(segs):
        a = (2.0 * math.pi * i) / segs
        ca = math.cos(a)
        sa = math.sin(a)
        ix = hole_r * ca
        iz = max(z_min, hole_z + hole_r * sa)

        tx = (hw / abs(ca)) if abs(ca) > 1e-5 else 1e9
        tz_hi = ((z_max - hole_z) / sa) if sa > 1e-5 else 1e9
        tz_lo = ((z_min - hole_z) / sa) if sa < -1e-5 else 1e9
        t = min(tx, tz_hi, tz_lo)
        ox = max(-hw, min(hw, t * ca))
        oz = max(z_min, min(z_max, hole_z + t * sa))

        # 4 verts per slice: 0=front-inner, 1=front-outer, 2=back-outer, 3=back-inner
        verts.append((ix, -hd, iz))
        verts.append((ox, -hd, oz))
        verts.append((ox, +hd, oz))
        verts.append((ix, +hd, iz))

    for i in range(segs):
        ni = (i + 1) % segs
        b0 = i * 4
        b1 = ni * 4
        faces.append((b0 + 0, b0 + 1, b1 + 1, b1 + 0))  # Front flat quad
        faces.append((b0 + 2, b0 + 3, b1 + 3, b1 + 2))  # Back flat quad
        faces.append((b0 + 1, b0 + 2, b1 + 2, b1 + 1))  # Outer perimeter quad
        faces.append((b0 + 3, b0 + 0, b1 + 0, b1 + 3))  # Inner hole tunnel quad

    return from_mesh(name, verts, faces, coll, mat, bevel=0.025, shade_smooth=False)


def build_gates(coll, label_mat):
    groups = []

    # ========================================================== 1. DAR GEÇİT (Toy Waterpark Coral & Sunny Gold) ==
    e = model_group("Gate_Narrow", (-10, -6, 0), coll)
    coral_wall = mat_plastic("NarrowWallCoral", srgb("#F66C78"), rough=0.22, metal=0.04, coat=0.45)
    coral_pillar = mat_plastic("NarrowPillarCoral", srgb("#F05A67"), rough=0.22, metal=0.04, coat=0.45)
    sunny_gold = mat_plastic("NarrowSunnyGold", srgb("#FFD026"), rough=0.20, metal=0.06, coat=0.50)

    # Chunky rounded coral side pillars (taller than the top header beam, matching Alternative 1)
    for side in (-1, 1):
        pil = cube(f"Narrow_Buttress_{side}", (side * 2.10, 0, 1.38),
                   (0.76, 0.86, 2.76), coll, coral_pillar, bevel=0.22)
        place(pil, e)

    # Chunky rounded sunny-yellow top header beam nestled between the coral pillars
    header = cube("Narrow_TopHeaderGold", (0, 0, 2.18), (3.52, 0.78, 0.76), coll, sunny_gold, bevel=0.14)
    place(header, e)

    # Clean quad-ring lower coral wall with open ground-level circular tunnel (flush with floor, NO bottom step!)
    hole_r = 0.58
    hole_z = 0.54
    wall = _build_clean_hole_wall("Narrow_Wall", 3.52, 0.72, 0.0, 1.80, hole_z, hole_r, coll, coral_wall, segs=48)
    place(wall, e)

    # Chunky rounded sunny-yellow circular tunnel collar front & back + inner open yellow sleeve
    for cy in (-0.34, 0.34):
        collar = torus("Narrow_HoleCollar", (0, cy, hole_z), hole_r + 0.08, 0.13, coll, sunny_gold,
                       rot=(math.pi / 2, 0, 0), mseg=56, nseg=20)
        place(collar, e)
    inner_sleeve = _build_clean_hole_wall("Narrow_HoleCollarSleeve", (hole_r + 0.14) * 2.0, 0.70,
                                          0.0, hole_z + hole_r + 0.14,
                                          hole_z, hole_r - 0.01, coll, sunny_gold, segs=48)
    place(inner_sleeve, e)

    label(e, coll, "DAR GEÇİT  ·  Ø0.72-0.85 m", -1.20, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # ======================================================== 2. PİSTON KAPI ==
    e = model_group("Gate_Piston", (0, -6, 0), coll)
    z0 = obstacle_pedestal("Piston", e, coll, width=6.60, depth=1.65, height=0.28)
    hz = mat_hazard("PistonHazard", C["gold"], C["black"], scale=1.8)
    purp_cap = mat_plastic("PistonPurpCap", C["purple"], rough=0.22, coat=0.6,
                           emission=C["purple"], strength=0.18)
    upper_wall_mat = mat_plastic("PistonUpperWall", srgb("#483896"), rough=0.28, metal=0.25, coat=0.5)
    yellow_glow = _neon("PistonArrowYellow", C["gold"], 3.2)

    # Left & Right thick vertical pillars wrapped in yellow/black diagonal hazard stripes + purple top/bottom caps
    for side in (-1, 1):
        px = side * 2.28
        tilt = side * math.radians(2.5)
        pil_hz = cube(f"Piston_HazardPillar_{side}", (px, 0, z0 + 1.08),
                      (0.94, 0.90, 1.92), coll, hz, rot=(0, tilt, 0), bevel=0.08)
        place(pil_hz, e)
        top_c = cube(f"Piston_TopCap_{side}", (px - side * 0.04, 0, z0 + 2.12),
                     (1.02, 0.96, 0.24), coll, purp_cap, bevel=0.05)
        place(top_c, e)
        bot_c = cube(f"Piston_BotCap_{side}", (px + side * 0.04, 0, z0 + 0.10),
                     (1.04, 0.98, 0.20), coll, purp_cap, bevel=0.04)
        place(bot_c, e)

    # Recessed purple upper wall panel + top beam between the pillars
    top_beam = cube("Piston_TopBeam", (0, 0, z0 + 2.14), (3.70, 0.76, 0.20), coll, purp_cap, bevel=0.05)
    place(top_beam, e)
    upper_panel = cube("Piston_UpperPanel", (0, 0.04, z0 + 1.56), (3.68, 0.56, 1.04), coll, upper_wall_mat, bevel=0.04)
    place(upper_panel, e)

    # Large bold 3D golden-yellow downward arrow ('↓') centered on the purple upper panel
    arr = _arrow_3d("Piston_BigDownArrow", (0, -0.26, z0 + 1.62), 'DOWN', 0.72, 0.10, coll, yellow_glow)
    place(arr, e)

    # Horizontal yellow/black diagonal hazard crusher bar below the upper panel (leaving 0.76m gap at bottom)
    crusher = cube("Piston_CrusherBar", (0, -0.02, z0 + 0.94), (3.76, 0.74, 0.42), coll, hz, bevel=0.06)
    place(crusher, e)

    label(e, coll, "PİSTON KAPI  ·  Açıklık 0.62-0.85 m", -1.35, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # ============================================== 3. KIRILABİLİR CAM DUVAR ==
    e = model_group("Obstacle_GlassWall", (10, -6, 0), coll)
    z0 = obstacle_pedestal("Glass", e, coll, width=6.60, depth=1.65, height=0.28)
    pil_cyan = mat_plastic("GlassPillarCyan", srgb("#1B82E8"), rough=0.18, metal=0.25, coat=0.8,
                           emission=C["cyan"], strength=0.35)
    pil_trim = _neon("GlassPillarNeon", C["cyan_light"], 3.8)
    ice_chunk_mat = mat_jelly("GlassChunkIce", srgb("#2898FF"), C["cyan_light"], alpha=0.92, rim_strength=2.8)
    crack_glow = _neon("GlassCrackGlow", C["cyan_light"], 4.2)

    # Chunky segmented cyan-blue crystal buttress pillars on left & right
    for side in (-1, 1):
        px = side * 2.35
        pil = cube(f"Glass_Pillar_{side}", (px, 0, z0 + 1.12), (0.92, 0.90, 2.24),
                   coll, pil_cyan, bevel=0.08)
        place(pil, e)
        rib = cube(f"Glass_PillarRib_{side}", (px + side * 0.34, -0.34, z0 + 1.12),
                   (0.12, 0.16, 2.02), coll, pil_trim, bevel=0.03)
        place(rib, e)
        cap = cube(f"Glass_PillarCap_{side}", (px, 0, z0 + 2.28), (1.00, 0.96, 0.18),
                   coll, pil_cyan, bevel=0.04)
        place(cap, e)

    # Inner glowing cyan fissure backing plate + 12 real 3D bevelled polygonal crystal blocks!
    backing = cube("Glass_CrackCore", (0, 0.02, z0 + 1.08), (3.82, 0.36, 2.12), coll, crack_glow, bevel=0.02)
    place(backing, e)

    # Tiled Voronoi-like 2D polygons across X in [-1.86, 1.86], Z in [z0+0.04, z0+2.12]
    cells = [
        # Bottom row
        [(-1.86, 0.04), (-0.75, 0.04), (-0.58, 0.68), (-1.15, 0.82), (-1.86, 0.72)],
        [(-0.67, 0.04), (0.52, 0.04), (0.38, 0.62), (-0.50, 0.68)],
        [(0.60, 0.04), (1.86, 0.04), (1.86, 0.76), (1.05, 0.86), (0.46, 0.62)],
        # Middle row
        [(-1.86, 0.80), (-1.15, 0.90), (-0.82, 1.44), (-1.86, 1.50)],
        [(-1.07, 0.88), (-0.48, 0.76), (0.05, 1.12), (-0.28, 1.52), (-0.74, 1.44)],
        [(-0.40, 0.74), (0.42, 0.68), (0.96, 0.92), (0.64, 1.46), (0.12, 1.14)],
        [(1.04, 0.94), (1.86, 0.84), (1.86, 1.52), (0.72, 1.48)],
        # Top row
        [(-1.86, 1.58), (-0.80, 1.52), (-0.52, 2.12), (-1.86, 2.12)],
        [(-0.72, 1.52), (-0.24, 1.58), (0.58, 1.54), (0.36, 2.12), (-0.44, 2.12)],
        [(0.66, 1.54), (1.86, 1.60), (1.86, 2.12), (0.44, 2.12)],
    ]
    for idx, poly in enumerate(cells):
        shifted = [(px, z0 + pz) for px, pz in poly]
        depth_v = 0.56 + (idx % 3) * 0.04
        blk = prism(f"Glass_VoronoiBlock_{idx}", shifted, depth_v, coll, ice_chunk_mat,
                    plane='XZ', bevel=0.045)
        place(blk, e)

    label(e, coll, "KIRILABİLİR CAM DUVAR  ·  ≥115", -1.25, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)
    return groups


def _sculpted_double_arch(prefix, empty, coll, z0, arch_mat, rib_mat, keystone_mat=None):
    """Builds the chunky sculpted double-arch bridge/tunnel from Row 3 of the reference sheet."""
    hole_dark = mat_plastic(prefix + "_ArchDark", C["dark2"], rough=0.45)

    for side in (-1, 1):
        cx = side * 1.36
        # Main chunky purple-magenta arch
        a = arch_band(f"{prefix}_Arch_{side}", 2.76, 2.24, 0.64, 1.18, coll, arch_mat, seg=36, bevel=0.07)
        a.location = (cx, 0, z0)
        place(a, empty)

        # Raised outer arch collar rim
        rim = arch_band(f"{prefix}_Rim_{side}", 2.82, 2.28, 0.16, 1.24, coll, rib_mat, seg=36, bevel=0.03)
        rim.location = (cx, 0, z0)
        place(rim, empty)

        # Recessed dark tunnel interior backing
        inner_back = arch_band(f"{prefix}_DarkBack_{side}", 1.52, 1.62, 0.76, 0.65, coll, hole_dark, seg=24, bevel=0.01)
        inner_back.location = (cx, 0.12, z0)
        place(inner_back, empty)

        # Base foot collars on outer and center pillars
        outer_foot = cube(f"{prefix}_OuterFoot_{side}", (cx + side * 1.06, 0, z0 + 0.10),
                          (0.76, 1.28, 0.20), coll, rib_mat, bevel=0.04)
        place(outer_foot, empty)

        # Optional golden keystone band at top of each arch (used on Moving Twin Tunnel)
        if keystone_mat is not None:
            ks = cube(f"{prefix}_Keystone_{side}", (cx, 0, z0 + 1.96),
                      (0.46, 1.28, 0.66), coll, keystone_mat, bevel=0.05)
            place(ks, empty)
            bf = cube(f"{prefix}_GoldFoot_{side}", (cx + side * 1.06, 0, z0 + 0.10),
                      (0.78, 1.30, 0.22), coll, keystone_mat, bevel=0.04)
            place(bf, empty)

    # Central shared pillar reinforcement
    center_pil = cube(f"{prefix}_CenterPillar", (0, 0, z0 + 0.86), (0.72, 1.24, 1.72),
                      coll, arch_mat, bevel=0.06)
    place(center_pil, empty)


def build_tunnels(coll, label_mat):
    groups = []
    arch_purp = mat_plastic("TunnelArchPurple", C["purple"], rough=0.22, coat=0.65,
                            emission=C["magenta"], strength=0.25)
    rib_mag = mat_plastic("TunnelRibMag", C["magenta"], rough=0.20, coat=0.7,
                          emission=C["pink"], strength=0.45)
    gold_key = mat_plastic("TunnelGoldKey", C["gold"], rough=0.20, metal=0.4, coat=0.6,
                           emission=C["gold"], strength=0.55)
    arrow_glow = _neon("TunnelArrowYellow", C["yellow"], 4.2)

    # ========================================================== 4. İKİZ TÜNEL ==
    e = model_group("Tunnel_Twin", (-10, -16, 0), coll)
    z0 = obstacle_pedestal("Twin", e, coll, width=6.60, depth=1.75, height=0.28)
    _sculpted_double_arch("Twin", e, coll, z0, arch_purp, rib_mag, keystone_mat=None)
    label(e, coll, "İKİZ TÜNEL  ·  sol/sağ ayrım", -1.55, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)

    # ================================================ 5. HAREKETLİ İKİZ TÜNEL ==
    e = model_group("Tunnel_Moving", (1, -16, 0), coll)
    z0 = obstacle_pedestal("Moving", e, coll, width=6.60, depth=1.75, height=0.28)
    _sculpted_double_arch("Moving", e, coll, z0, arch_purp, rib_mag, keystone_mat=gold_key)

    # Bold glowing yellow arrows inside the left ('←') and right ('→') tunnel openings
    arr_l = _arrow_3d("Moving_ArrowL", (-1.36, -0.32, z0 + 0.82), 'LEFT', 0.62, 0.10, coll, arrow_glow)
    arr_r = _arrow_3d("Moving_ArrowR", (1.36, -0.32, z0 + 0.82), 'RIGHT', 0.62, 0.10, coll, arrow_glow)
    place(arr_l, e)
    place(arr_r, e)

    label(e, coll, "HAREKETLİ İKİZ TÜNEL", -1.55, 0.34, label_mat)
    finish(e, coll)
    groups.append(e)
    return groups


def _spiked_mace_on_arm(prefix, empty, coll, pivot_loc, angle_deg, arm_len, ball_r,
                        arm_mat, ball_mat, spike_mat, y_offset=0.0):
    """Creates a pendulum arm + spiked morningstar ball rotated at angle_deg around Y from pivot_loc."""
    ang = math.radians(angle_deg)
    px, py, pz = pivot_loc
    py += y_offset
    bx = px + math.sin(ang) * arm_len
    bz = pz - math.cos(ang) * arm_len

    # Thick suspension rod from top pivot to ball
    rod = tube(f"{prefix}_Rod", (px, py, pz), (bx, py, bz), 0.12, coll, arm_mat, verts=24)
    place(rod, empty)

    # Collar at top of ball
    col_x = px + math.sin(ang) * (arm_len - ball_r * 0.95)
    col_z = pz - math.cos(ang) * (arm_len - ball_r * 0.95)
    col = cylinder(f"{prefix}_Collar", (col_x, py, col_z), 0.22, 0.18, coll, arm_mat,
                   rot=(0, -ang, 0), verts=24, bevel=0.03)
    place(col, empty)

    # Spiked morningstar ball
    ball = sphere(f"{prefix}_Ball", (bx, py, bz), ball_r, coll, ball_mat, 48, 24)
    place(ball, empty)

    golden_angle = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(16):
        z_d = 1.0 - (i / 15.0) * 1.85
        rad = math.sqrt(max(0.0, 1.0 - z_d * z_d))
        theta = golden_angle * i
        d = Vector((math.cos(theta) * rad, math.sin(theta) * rad, z_d)).normalized()
        sp_pos = Vector((bx, py, bz)) + d * (ball_r * 1.05)
        s = cone(f"{prefix}_Spike_{i}", sp_pos, ball_r * 0.22, 0.0, ball_r * 0.48,
                 coll, spike_mat, verts=16, bevel=0.01)
        s.rotation_mode = 'QUATERNION'
        s.rotation_quaternion = d.to_track_quat('Z', 'Y')
        place(s, empty)


def build_pendulum(coll, label_mat):
    e = model_group("Obstacle_Pendulum", (10, -16, 0), coll)
    z0 = obstacle_pedestal("Pend", e, coll, width=5.20, depth=1.75, height=0.28)

    piv_purp = mat_plastic("PendPivotPurp", C["purple"], rough=0.22, coat=0.6,
                           emission=C["magenta"], strength=0.35)
    piv_inner = _neon("PendPivotCenter", C["pink_light"], 3.5)
    arm_metal = mat_plastic("PendArmMetal", srgb("#586288"), rough=0.28, metal=0.65)
    gold_mace = mat_plastic("PendMaceGold", C["gold"], rough=0.18, metal=0.45, coat=0.7,
                            emission=C["gold"], strength=0.65)
    shadow_mace = mat_plastic("PendMaceShadow", srgb("#3A4268"), rough=0.32, metal=0.55)
    shadow_arm = mat_plastic("PendArmShadow", srgb("#2E3454"), rough=0.38, metal=0.5)

    pivot_loc = (0.0, 0.0, z0 + 2.85)

    # Top purple circular pivot hub (matching reference top joint)
    hub = cylinder("Pend_TopHub", pivot_loc, 0.34, 0.36, coll, piv_purp,
                   rot=(math.pi / 2, 0, 0), verts=36, bevel=0.05)
    place(hub, e)
    pin = cylinder("Pend_TopPin", (0.0, -0.19, z0 + 2.85), 0.12, 0.08, coll, piv_inner,
                   rot=(math.pi / 2, 0, 0), verts=24, bevel=0.02)
    place(pin, e)

    # Primary active Golden Spiked Mace swung to the left (-26°)
    _spiked_mace_on_arm("Pend_MainGold", e, coll, pivot_loc, -26.0, 2.25, 0.56,
                        arm_metal, gold_mace, gold_mace, y_offset=-0.06)

    # Motion-trail / secondary dark metallic maces swung to the right (+16° and +32°, matching reference)
    _spiked_mace_on_arm("Pend_Ghost1", e, coll, pivot_loc, 16.0, 2.25, 0.52,
                        shadow_arm, shadow_mace, shadow_mace, y_offset=0.08)
    _spiked_mace_on_arm("Pend_Ghost2", e, coll, pivot_loc, 31.0, 2.25, 0.48,
                        shadow_arm, shadow_mace, shadow_mace, y_offset=0.18)

    label(e, coll, "SARKAÇ  ·  zamanlamalı", -1.55, 0.34, label_mat)
    finish(e, coll)
    return e


def build_spinner(coll, label_mat):
    """Builds the exact T-shaped purple & gold Spinner Bar on a hazard-striped center pedestal from Row 3."""
    e = model_group("Obstacle_Spinner", (17, -16, 0), coll)
    z0 = obstacle_pedestal("Spin", e, coll, width=5.40, depth=1.85, height=0.28)

    hz = mat_hazard("SpinHazardCol", C["gold"], C["black"], scale=2.2)
    arm_purp = mat_plastic("SpinArmPurple", C["purple"], rough=0.20, coat=0.7,
                           emission=C["magenta"], strength=0.35)
    hub_purp = mat_plastic("SpinHubPurple", srgb("#6B34E0"), rough=0.22, coat=0.65,
                           emission=C["purple"], strength=0.25)
    cap_gold = mat_plastic("SpinCapGold", C["gold"], rough=0.18, metal=0.45, coat=0.7,
                           emission=C["gold"], strength=0.55)

    # 1. Thick central cylindrical pedestal wrapped in yellow/black diagonal hazard stripes
    col_hz = cylinder("Spin_HazardPedestal", (0, 0, z0 + 0.46), 0.54, 0.92, coll, hz, verts=40, bevel=0.04)
    place(col_hz, e)

    # 2. Purple T-joint central hub + golden top cap dome
    arm_z = z0 + 1.24
    hub_col = cylinder("Spin_CenterHub", (0, 0, arm_z), 0.62, 0.78, coll, hub_purp, verts=40, bevel=0.07)
    place(hub_col, e)
    top_cap = cylinder("Spin_TopGoldCap", (0, 0, arm_z + 0.44), 0.46, 0.16, coll, cap_gold, verts=36, bevel=0.05)
    place(top_cap, e)

    # 3. Two thick horizontal cylindrical purple-magenta arms extending left & right + golden end caps
    bar = cylinder("Spin_HorizArms", (0, 0, arm_z), 0.32, 4.65, coll, arm_purp,
                   rot=(0, math.pi / 2, 0), verts=36, bevel=0.04)
    place(bar, e)

    for sx in (-2.34, 2.34):
        end_c = cylinder(f"Spin_EndCap_{sx}", (sx, 0, arm_z), 0.36, 0.28, coll, cap_gold,
                         rot=(0, math.pi / 2, 0), verts=36, bevel=0.05)
        place(end_c, e)

    label(e, coll, "DÖNEN ENGEL  ·  360° kollar", -1.55, 0.34, label_mat)
    finish(e, coll)
    return e


def _cut_hole_with_rings(wall, empty, coll, prefix, hx, hz, radius, ring_mat, tag_text=None, tag_mat=None):
    cutter = cylinder(f"{prefix}_Cut", (hx, 0, hz), radius, 1.8, coll, None,
                      rot=(math.pi / 2, 0, 0), verts=48)
    boolean_diff(wall, cutter)
    r = torus(f"{prefix}_Collar", (hx, -0.30, hz), radius + 0.05, 0.085, coll,
              ring_mat, rot=(math.pi / 2, 0, 0), mseg=48, nseg=16)
    place(r, empty)
    if tag_text and tag_mat:
        txt = text(f"{prefix}_Tag", tag_text, (hx, -0.35, hz + radius + 0.22), 0.19, coll, tag_mat,
                   rot=(math.radians(90), 0, 0))
        place(txt, empty)


def _base_solid_gate_wall(empty, coll, prefix, wall_mat, cap_mat, trim_mat):
    z0 = obstacle_pedestal(prefix, empty, coll, width=6.60, depth=1.65, height=0.28)
    _fortress_buttress_frame(prefix, empty, coll, z0, wall_w=4.30, wall_h=2.18,
                             pillar_mat=cap_mat, trim_mat=trim_mat)
    wall = cube(f"{prefix}_Wall", (0, 0, z0 + 1.08), (4.40, 0.68, 2.16), coll, wall_mat, bevel=0.07)
    return wall, z0


def build_multihole_walls(coll, label_mat):
    """Builds the 3 configurable variations of the signature Multi-Hole Decision Wall using the fortress architecture."""
    groups = []
    wall_indigo = mat_plastic("MH_WallIndigo", srgb("#5642B8"), rough=0.25, metal=0.18, coat=0.55,
                              emission=C["purple"], strength=0.14)
    pil_purp = mat_plastic("MH_PilPurple", C["purple"], rough=0.22, coat=0.65,
                           emission=C["magenta"], strength=0.22)
    trim_mag = _neon("MH_TrimMag", C["magenta"], 3.8)
    cyan_neon = _neon("MH_Cyan", C["cyan"], 4.2)
    green_neon = _neon("MH_Green", C["green"], 4.2)
    gold_neon = _neon("MH_Gold", C["gold"], 4.5)
    white_neon = _neon("MH_White", C["white"], 2.8)

    # 1) Boyut Seçimi (Size Choice): Sol Küçük Delik (<=75, Ø0.82m) + Sağ Normal Delik (76-114, Ø1.14m)
    e1 = model_group("Gate_MultiHole_SizeChoice", (-10, -25, 0), coll)
    w1, z0 = _base_solid_gate_wall(e1, coll, "MH_Size", wall_indigo, pil_purp, trim_mag)
    _cut_hole_with_rings(w1, e1, coll, "MH_Size_Small", -1.10, z0 + 0.92, 0.42, cyan_neon, "≤75", white_neon)
    _cut_hole_with_rings(w1, e1, coll, "MH_Size_Normal", 1.05, z0 + 1.04, 0.56, green_neon, "76-114", white_neon)
    place(w1, e1)
    label(e1, coll, "ÇOKLU DELİK (1: BOYUT SEÇİMİ)", -1.35, 0.32, label_mat)
    finish(e1, coll)
    groups.append(e1)

    # 2) Bölünme Seçimi (Split Choice): Merkez Dar Tekil (<=70) vs Sol/Sağ İkiz Bölünme (÷2)
    e2 = model_group("Gate_MultiHole_SplitChoice", (0, -25, 0), coll)
    w2, z0 = _base_solid_gate_wall(e2, coll, "MH_Split", wall_indigo, pil_purp, trim_mag)
    _cut_hole_with_rings(w2, e2, coll, "MH_Split_Left", -1.35, z0 + 0.96, 0.44, trim_mag, "÷2 SOL", white_neon)
    _cut_hole_with_rings(w2, e2, coll, "MH_Split_Center", 0.0, z0 + 0.88, 0.38, cyan_neon, "TEK ≤70", white_neon)
    _cut_hole_with_rings(w2, e2, coll, "MH_Split_Right", 1.35, z0 + 0.96, 0.44, trim_mag, "÷2 SAĞ", white_neon)
    place(w2, e2)
    label(e2, coll, "ÇOKLU DELİK (2: BÖLÜNME SEÇİMİ)", -1.35, 0.32, label_mat)
    finish(e2, coll)
    groups.append(e2)

    # 3) Risk-Ödül Seçimi (Risk-Reward Choice): Sol Güvenli Geniş vs Sağ Dar Ödüllü (x2)
    e3 = model_group("Gate_MultiHole_RiskReward", (10, -25, 0), coll)
    w3, z0 = _base_solid_gate_wall(e3, coll, "MH_Risk", wall_indigo, pil_purp, gold_neon)
    _cut_hole_with_rings(w3, e3, coll, "MH_Risk_Safe", -1.05, z0 + 1.04, 0.56, cyan_neon, "GÜVENLİ", white_neon)
    _cut_hole_with_rings(w3, e3, coll, "MH_Risk_Bonus", 1.15, z0 + 0.90, 0.38, gold_neon, "★ RİSK x2", gold_neon)
    place(w3, e3)
    label(e3, coll, "ÇOKLU DELİK (3: RİSK-ÖDÜL)", -1.35, 0.32, label_mat)
    finish(e3, coll)
    groups.append(e3)

    return groups


def build_phase4_object_families(coll, label_mat):
    """Builds the 7 recommended Phase 4 & 6 new object families."""
    groups = []
    purple = mat_plastic("P4_Purple", C["purple"], rough=0.24, coat=0.6,
                         emission=C["purple"], strength=0.18)
    cap_mat = mat_plastic("P4_Cap", C["purple_dk"], rough=0.25, metal=0.45)
    dark_metal = mat_plastic("P4_DarkMetal", C["dark"], rough=0.30, metal=0.72)
    cyan_neon = _neon("P4_Cyan", C["cyan"], 4.5)
    mag_neon = _neon("P4_Mag", C["magenta"], 4.0)
    green_neon = _neon("P4_Green", C["green"], 4.5)
    gold_neon = _neon("P4_Gold", C["gold"], 4.5)
    orange_neon = _neon("P4_Orange", C["orange"], 4.2)
    white_neon = _neon("P4_White", C["white"], 2.5)

    # 1) Esnek Geçit (Flexible Squeeze Corridor)
    e1 = model_group("Gate_FlexibleSqueeze", (-18, -33, 0), coll)
    z0 = obstacle_pedestal("Flex", e1, coll, width=6.60, depth=2.40, height=0.28)
    w1 = cube("Flex_Wall", (0, 0, z0 + 1.12), (5.6, 2.00, 2.24), coll, purple, bevel=0.09)
    slot = cube("Flex_SlotCut", (0, 0, z0 + 0.96), (0.64, 2.8, 1.68), coll, None)
    boolean_diff(w1, slot)
    place(w1, e1)
    for sx in (-0.36, 0.36):
        rail = cube("Flex_NeonRail", (sx, 0, z0 + 0.96), (0.07, 2.04, 1.68), coll, cyan_neon, bevel=0.02)
        place(rail, e1)
    label(e1, coll, "ESNEK GEÇİT  ·  Squash Koridoru", -1.65, 0.32, label_mat)
    finish(e1, coll)
    groups.append(e1)

    # 2) Kütle Terazisi (Mass Scale Gate)
    e2 = model_group("Gate_MassScale", (-6, -33, 0), coll)
    z0 = obstacle_pedestal("Scale", e2, coll, width=6.60, depth=2.40, height=0.28)
    pad_top = cube("Scale_PadTop", (0, -0.85, z0 + 0.08), (2.4, 1.1, 0.12), coll, gold_neon, bevel=0.03)
    place(pad_top, e2)
    for sx in (-2.35, 2.35):
        pil = cube("Scale_Pillar", (sx, 0.25, z0 + 1.15), (0.85, 0.80, 2.30), coll, purple, bevel=0.07)
        place(pil, e2)
    door2 = cube("Scale_Door", (0, 0.25, z0 + 1.12), (3.8, 0.38, 2.05), coll,
                 mat_cracked_glass("Scale_Glass", C["gold"], C["orange"]), bevel=0.04)
    place(door2, e2)
    label(e2, coll, "KÜTLE TERAZİSİ  ·  Baskı Kapısı", -2.65, 0.32, label_mat)
    finish(e2, coll)
    groups.append(e2)

    # 3) Hareketli Delik Duvarı (Sliding Hole Wall)
    e3 = model_group("Gate_SlidingHoleWall", (6, -33, 0), coll)
    w3, z0 = _base_solid_gate_wall(e3, coll, "SlideHole", purple, cap_mat, orange_neon)
    _cut_hole_with_rings(w3, e3, coll, "SlideHole_Cut", 0.55, z0 + 1.00, 0.48, orange_neon, "⇄ ZAMANLA", white_neon)
    place(w3, e3)
    for sz in (z0 + 0.34, z0 + 1.72):
        track = cube("SlideHole_Track", (0, -0.36, sz), (4.1, 0.08, 0.09), coll, cyan_neon, bevel=0.02)
        place(track, e3)
    label(e3, coll, "HAREKETLİ DELİK DUVARI", -1.35, 0.32, label_mat)
    finish(e3, coll)
    groups.append(e3)

    # 4) Manyetik Alan (Magnetic Field Hazard)
    e4 = model_group("Hazard_MagneticField", (18, -33, 0), coll)
    z0 = obstacle_pedestal("MagField", e4, coll, width=6.40, depth=2.10, height=0.28)
    for sx in (-2.35, 2.35):
        coil_core = cylinder("Mag_Core", (sx, 0, z0 + 1.00), 0.42, 1.90, coll, purple, verts=32, bevel=0.04)
        place(coil_core, e4)
        for rz in (z0 + 0.45, z0 + 1.00, z0 + 1.55):
            ring = torus("Mag_CoilRing", (sx, 0, rz), 0.50, 0.08, coll,
                         mag_neon if sx < 0 else cyan_neon, mseg=36, nseg=12)
            place(ring, e4)
    for bx in (-1.1, 0.0, 1.1):
        wave = torus("Mag_Wave", (bx, 0, z0 + 0.95), 0.55, 0.035, coll, mag_neon,
                     rot=(0, math.pi / 2, 0), mseg=32, nseg=10)
        place(wave, e4)
    label(e4, coll, "MANYETİK ALAN  ·  Yanal Çekim", -1.65, 0.32, label_mat)
    finish(e4, coll)
    groups.append(e4)

    # 5) Yapışkan Zemin (Sticky Slime Floor)
    e5 = model_group("Hazard_StickyFloor", (-12, -41, 0), coll)
    slime_mat = mat_jelly("StickySlimeMat", C["gold"], C["orange"], alpha=0.92)
    slime_pad = cube("Sticky_Pad", (0, 0, 0.10), (4.8, 2.6, 0.18), coll, slime_mat, bevel=0.07)
    place(slime_pad, e5)
    for bx, by, br in ((-1.4, -0.5, 0.24), (-0.3, 0.4, 0.30), (0.9, -0.3, 0.26), (1.5, 0.5, 0.20)):
        bub = sphere("Sticky_Bubble", (bx, by, 0.22), br, coll, gold_neon, 24, 14)
        place(bub, e5)
    label(e5, coll, "YAPIŞKAN ZEMİN  ·  Hız Düşürücü", -1.85, 0.32, label_mat)
    finish(e5, coll)
    groups.append(e5)

    # 6) Zıplama Pedi (Jump Pad)
    e6 = model_group("Hazard_JumpPad", (0, -41, 0), coll)
    base6 = cylinder("Jump_Base", (0, 0, 0.12), 1.20, 0.24, coll, purple, verts=40, bevel=0.04)
    place(base6, e6)
    for rz in (0.28, 0.44):
        spr = torus("Jump_Spring", (0, 0, rz), 0.86, 0.08, coll, cyan_neon, mseg=40, nseg=12)
        place(spr, e6)
    top6 = cylinder("Jump_Plate", (0, 0, 0.58), 1.08, 0.16, coll, green_neon, verts=40, bevel=0.05)
    place(top6, e6)
    label(e6, coll, "ZIPLAMA PEDİ  ·  Üst Katman Fırlatıcı", -1.65, 0.32, label_mat)
    finish(e6, coll)
    groups.append(e6)

    # 7) Parçalanma Kapısı (Micro Shatter Gate)
    e7 = model_group("Gate_MicroShatter", (12, -41, 0), coll)
    frame7, z0 = _base_solid_gate_wall(e7, coll, "Shatter", purple, cap_mat, cyan_neon)
    win = cube("Shatter_WindowCut", (0, 0, z0 + 1.08), (3.4, 1.6, 1.55), coll, None)
    boolean_diff(frame7, win)
    place(frame7, e7)
    for gx in (-0.85, 0.0, 0.85):
        bar_v = cube("Shatter_GridV", (gx, 0, z0 + 1.08), (0.08, 0.20, 1.55), coll, cyan_neon, bevel=0.02)
        place(bar_v, e7)
    for gz in (z0 + 0.68, z0 + 1.08, z0 + 1.48):
        bar_h = cube("Shatter_GridH", (0, 0, gz), (3.4, 0.20, 0.08), coll, mag_neon, bevel=0.02)
        place(bar_h, e7)
    label(e7, coll, "PARÇALANMA KAPISI  ·  Çoklu Bölünme", -1.35, 0.32, label_mat)
    finish(e7, coll)
    groups.append(e7)

    return groups
