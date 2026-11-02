# -*- coding: utf-8 -*-
"""Track environment: S-curve spline road, curved neon glass barrier, crystal floating island with pink trees, and arched bridge/ramp (High-Detail v2)."""
import math
import bpy
from mathutils import Vector
from .common import (
    C, srgb, mat_plastic, mat_neon, mat_jelly, cube, sphere, jelly_dome,
    cylinder, cone, torus, ico, model_group, label, poly_curve, prism,
    from_mesh, place, finish, apply_scale, text, smooth,
)


def _neon(name, color, strength=3.8):
    return mat_neon(name, color, strength, base=color)


def _extrude_ribbon_3d(name, pts_3d, width, height, coll, mat, z_offset=0.0):
    """Creates a thick 3D ribbon following pts_3d [(x,y,z), ...] with given width and vertical height."""
    n = len(pts_3d)
    verts = []
    for i, (x, y, z) in enumerate(pts_3d):
        if i == 0:
            dx, dy = pts_3d[1][0] - x, pts_3d[1][1] - y
        elif i == n - 1:
            dx, dy = x - pts_3d[n - 2][0], y - pts_3d[n - 2][1]
        else:
            dx = pts_3d[i + 1][0] - pts_3d[i - 1][0]
            dy = pts_3d[i + 1][1] - pts_3d[i - 1][1]
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L, dx / L
        z_top = z + z_offset + height * 0.5
        z_bot = z + z_offset - height * 0.5
        for s in (-1, 1):
            verts.append((x + nx * width * 0.5 * s, y + ny * width * 0.5 * s, z_top))
        for s in (-1, 1):
            verts.append((x + nx * width * 0.5 * s, y + ny * width * 0.5 * s, z_bot))
    faces = []
    for i in range(n - 1):
        a = 4 * i
        b = 4 * (i + 1)
        faces.append((a, b, b + 1, a + 1))          # top
        faces.append((a + 2, a + 3, b + 3, b + 2))  # bottom
        faces.append((a, a + 2, b + 2, b))          # left side
        faces.append((a + 1, b + 1, b + 3, a + 3))  # right side
    faces.append((0, 1, 3, 2))
    last = 4 * (n - 1)
    faces.append((last, last + 2, last + 3, last + 1))
    return from_mesh(name, verts, faces, coll, mat, bevel=0.03, shade_smooth=True)


def _offset_curve_pts(pts_3d, lateral_offset, z_offset=0.0):
    out = []
    n = len(pts_3d)
    for i, (x, y, z) in enumerate(pts_3d):
        if i == 0:
            dx, dy = pts_3d[1][0] - x, pts_3d[1][1] - y
        elif i == n - 1:
            dx, dy = x - pts_3d[n - 2][0], y - pts_3d[n - 2][1]
        else:
            dx = pts_3d[i + 1][0] - pts_3d[i - 1][0]
            dy = pts_3d[i + 1][1] - pts_3d[i - 1][1]
        L = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / L, dx / L
        out.append((x + nx * lateral_offset, y + ny * lateral_offset, z + z_offset))
    return out


def build_path(coll, label_mat):
    """Builds the S-curved spline track winding away in 3D perspective with cyan road & chunky purple curbs."""
    e = model_group("Track_Path", (-8, -27, 0), coll)

    road_cyan = mat_plastic("SplineRoadCyan", srgb("#2E86FF"), rough=0.20, metal=0.15, coat=0.75,
                            emission=srgb("#1B62E0"), strength=0.35)
    curb_purp = mat_plastic("SplineCurbPurple", C["purple"], rough=0.22, coat=0.65,
                            emission=C["magenta"], strength=0.28)
    lane_glow = _neon("SplineLaneGlow", C["cyan_light"], 3.8)
    rail_gold = _neon("SplineRailGold", C["gold"], 3.2)

    # S-curve winding along Y from front (-4.8) to back (+5.2), rising gently in Z so the whole S is visible
    n = 56
    centers = []
    for i in range(n):
        t = i / (n - 1)
        y = -4.6 + 9.6 * t
        x = -1.65 * math.sin((t - 0.5) * math.pi * 1.85)
        z = 0.22 + 1.35 * t + 0.18 * math.sin(t * math.pi)
        centers.append((x, y, z))

    # Main glossy cyan-blue road deck
    road = _extrude_ribbon_3d("Path_RoadDeck", centers, 2.35, 0.24, coll, road_cyan)
    place(road, e)

    # Thick chunky purple-magenta raised side curbs along left and right edges
    for side in (-1, 1):
        c_pts = _offset_curve_pts(centers, side * 1.28, z_offset=0.12)
        curb = _extrude_ribbon_3d(f"Path_SideCurb_{side}", c_pts, 0.34, 0.36, coll, curb_purp)
        place(curb, e)
        # Upper glowing gold/cyan neon top rail above the curb
        r_pts = _offset_curve_pts(centers, side * 1.28, z_offset=0.34)
        rail = poly_curve(f"Path_TopRail_{side}", r_pts, 0.055, coll,
                          rail_gold if side > 0 else lane_glow, res=6)
        place(rail, e)

    # Glowing lane lines on the road surface
    for l_off in (-0.52, 0.0, 0.52):
        l_pts = _offset_curve_pts(centers, l_off, z_offset=0.13)
        ln = poly_curve(f"Path_Lane_{l_off}", l_pts, 0.032, coll, lane_glow, res=4)
        place(ln, e)

    label(e, coll, "KIVRIMLI SPLINE PİST  ·  6.4 m", -5.2, 0.38, label_mat)
    finish(e, coll)
    return e


def build_barrier(coll, label_mat):
    """Builds the curved translucent cyan neon glass barrier wall on a chunky purple base curb."""
    e = model_group("Track_Barrier", (9, -27, 0), coll)

    curb_purp = mat_plastic("BarrierCurbPurp", C["purple"], rough=0.22, coat=0.65,
                            emission=C["magenta"], strength=0.30)
    curb_rim = _neon("BarrierCurbRim", C["pink_light"], 3.0)
    glass_wall = mat_jelly("BarrierCyanGlass", srgb("#1EA8FF"), C["cyan_light"], alpha=0.82, rim_strength=3.2)
    top_rail_mat = _neon("BarrierTopCyan", C["cyan_light"], 4.2)

    # Gently curved arc in XY plane, angled slightly for 3/4 view
    n = 36
    arc_pts = []
    for i in range(n):
        t = (i / (n - 1)) - 0.5
        ang = t * math.radians(38)
        R = 6.2
        x = math.sin(ang) * R
        y = (math.cos(ang) - 1.0) * R
        z = 0.22 + 0.25 * (t + 0.5)
        arc_pts.append((x, y, z))

    # 1. Chunky bevelled purple-magenta base curb
    base_curb = _extrude_ribbon_3d("Barrier_PurpleBase", arc_pts, 0.56, 0.38, coll, curb_purp, z_offset=0.0)
    place(base_curb, e)
    rim_pts = _offset_curve_pts(arc_pts, -0.28, z_offset=0.12)
    rim_line = poly_curve("Barrier_PurpleRim", rim_pts, 0.032, coll, curb_rim, res=4)
    place(rim_line, e)

    # 2. Tall thick curved translucent cyan-blue glass wall above the purple curb
    glass = _extrude_ribbon_3d("Barrier_GlassWall", arc_pts, 0.28, 1.18, coll, glass_wall, z_offset=0.76)
    place(glass, e)

    # 3. Thick glowing light-cyan top & front edge frame around the glass wall
    top_pts = _offset_curve_pts(arc_pts, 0.0, z_offset=1.36)
    top_rail = poly_curve("Barrier_TopNeonRail", top_pts, 0.075, coll, top_rail_mat, res=8)
    place(top_rail, e)

    # End vertical frame posts of the glass wall
    for end_pt in (arc_pts[0], arc_pts[-1]):
        post = cylinder("Barrier_GlassEndPost", (end_pt[0], end_pt[1], end_pt[2] + 0.76),
                        0.15, 1.24, coll, top_rail_mat, verts=24, bevel=0.03)
        place(post, e)

    label(e, coll, "KENAR BARİYERİ  ·  neon/cam", -1.45, 0.36, label_mat)
    finish(e, coll)
    return e


def _stylized_pink_tree(prefix, empty, coll, base_loc, scale=1.0):
    """Builds a lush stylized tree with curved purple-brown trunk and 7 plump glossy pink foliage clouds."""
    bx, by, bz = base_loc
    trunk_mat = mat_plastic(prefix + "_Trunk", srgb("#6E3B78"), rough=0.35, coat=0.3)
    pink_main = mat_plastic(prefix + "_LeafPink", C["pink"], rough=0.22, coat=0.65,
                            emission=C["pink"], strength=0.38)
    pink_hi = mat_plastic(prefix + "_LeafLight", C["pink_light"], rough=0.20, coat=0.7,
                          emission=C["pink_light"], strength=0.45)

    # Curved tapered trunk + 2 upper branches
    t_pts = [
        (bx, by, bz, 1.0, 1.45),
        (bx - 0.06 * scale, by, bz + 0.45 * scale, 1.0, 0.95),
        (bx + 0.04 * scale, by, bz + 0.95 * scale, 1.0, 0.65),
    ]
    tr = poly_curve(prefix + "_TrunkMain", t_pts, 0.14 * scale, coll, trunk_mat, res=6)
    place(tr, empty)

    # Cluster of 7 plump overlapping pink spherical cloud puffs forming a lush rounded canopy
    puffs = (
        (0.0, -0.05, 1.28, 0.52, pink_hi),
        (-0.42, -0.06, 1.12, 0.42, pink_main),
        (0.42, -0.06, 1.12, 0.42, pink_main),
        (-0.24, -0.18, 1.38, 0.38, pink_hi),
        (0.24, -0.18, 1.36, 0.38, pink_hi),
        (-0.58, 0.05, 1.00, 0.32, pink_main),
        (0.58, 0.05, 1.00, 0.32, pink_main),
    )
    for i, (px, py, pz, pr, pmat) in enumerate(puffs):
        sp = sphere(f"{prefix}_Canopy_{i}",
                    (bx + px * scale, by + py * scale, bz + pz * scale),
                    pr * scale, coll, pmat, seg=32, ring=18)
        sp.scale = (1.12, 0.95, 0.84)
        apply_scale(sp)
        place(sp, empty)


def build_island(coll, label_mat):
    """Builds the faceted light-blue crystal floating island topped with two lush pink trees."""
    e = model_group("Deko_Island", (-18, -16, 0), coll)

    ice_top = mat_plastic("IslandTopCyan", srgb("#5CE0FF"), rough=0.20, coat=0.7,
                          emission=C["cyan_light"], strength=0.35)
    ice_cliff = mat_plastic("IslandCliffBlue", srgb("#2892F0"), rough=0.22, metal=0.15, coat=0.75,
                            emission=C["cyan"], strength=0.25)
    ice_deep = mat_plastic("IslandDeepBlue", srgb("#1662C4"), rough=0.28, metal=0.2, coat=0.6)
    bush_purp = mat_plastic("IslandBushPurp", C["purple"], rough=0.28, coat=0.5,
                            emission=C["magenta"], strength=0.25)

    z_plat = 0.85
    # 1. Wide multi-faceted crystal island body (stepped polygonal blocks + faceted cliff chunks)
    main_oct = [
        (-1.95, -0.45), (-1.35, -1.10), (0.0, -1.22), (1.35, -1.10),
        (1.95, -0.45), (1.85, 0.65), (1.10, 1.15), (-1.10, 1.15), (-1.85, 0.65),
    ]
    top_slab = prism("Island_TopSlab", main_oct, 0.28, coll, ice_top, plane='XY', bevel=0.06)
    top_slab.location = (0, 0, z_plat)
    place(top_slab, e)

    mid_oct = [(x * 0.90, y * 0.88) for x, y in main_oct]
    mid_slab = prism("Island_MidCliff", mid_oct, 0.46, coll, ice_cliff, plane='XY', bevel=0.06)
    mid_slab.location = (0, 0, z_plat - 0.30)
    place(mid_slab, e)

    bot_oct = [(x * 0.72, y * 0.70) for x, y in main_oct]
    bot_slab = prism("Island_BotCliff", bot_oct, 0.38, coll, ice_deep, plane='XY', bevel=0.05)
    bot_slab.location = (0, 0, z_plat - 0.64)
    place(bot_slab, e)

    # Faceted side cliff crystal blocks around the perimeter for chunky crystalline silhouette
    cliff_chunks = (
        (-1.55, -0.55, z_plat - 0.24, (0.68, 0.58, 0.62), 18),
        (-0.85, -0.88, z_plat - 0.28, (0.72, 0.55, 0.58), -12),
        (0.0, -0.96, z_plat - 0.25, (0.82, 0.56, 0.64), 5),
        (0.88, -0.86, z_plat - 0.28, (0.72, 0.55, 0.58), 15),
        (1.55, -0.52, z_plat - 0.24, (0.68, 0.58, 0.62), -20),
    )
    for i, (cx, cy, cz, cdims, rot_z) in enumerate(cliff_chunks):
        ch = cube(f"Island_FacetChunk_{i}", (cx, cy, cz), cdims, coll, ice_cliff,
                  rot=(math.radians(8), 0, math.radians(rot_z)), bevel=0.06)
        place(ch, e)

    # 2. Two lush pink/magenta stylized trees on the island top (matching reference)
    _stylized_pink_tree("Tree_LeftBig", e, coll, (-0.52, 0.05, z_plat + 0.12), scale=1.18)
    _stylized_pink_tree("Tree_RightMid", e, coll, (0.68, -0.10, z_plat + 0.12), scale=0.88)

    # 3. Small purple/blue foliage mounds at the base of the trees
    for i, (bx, by, br) in enumerate(((-1.25, -0.25, 0.22), (0.05, -0.42, 0.20), (1.28, -0.22, 0.19))):
        b = sphere(f"Island_Bush_{i}", (bx, by, z_plat + 0.18), br, coll, bush_purp, 20, 12)
        b.scale = (1.2, 1.0, 0.72)
        apply_scale(b)
        place(b, e)

    label(e, coll, "TEMA ADASI  ·  kristal + ağaç", -2.45, 0.36, label_mat)
    finish(e, coll)
    return e


def build_bridge(coll, label_mat):
    """Builds the chunky vaulted arch bridge/ramp with purple side walls & massive gold/purple end abutments."""
    e = model_group("Track_Bridge", (17, -27, 0), coll)

    deck_cyan = mat_plastic("BridgeDeckCyan", srgb("#4298FF"), rough=0.20, coat=0.75,
                            emission=C["cyan"], strength=0.35)
    wall_purp = mat_plastic("BridgeWallPurp", C["purple"], rough=0.22, coat=0.65,
                            emission=C["magenta"], strength=0.28)
    abut_purp = mat_plastic("BridgeAbutPurp", srgb("#6236D8"), rough=0.24, coat=0.6,
                            emission=C["purple"], strength=0.22)
    abut_gold = mat_plastic("BridgeAbutGold", C["gold"], rough=0.22, metal=0.35, coat=0.65,
                            emission=C["orange"], strength=0.35)
    lane_glow = _neon("BridgeLaneGlow", C["cyan_light"], 3.8)
    trim_gold = _neon("BridgeTrimGold", C["gold"], 3.2)

    # Rotate the bridge slightly (-16° around Z) so camera sees both the ramp face and the arched side wall!
    piv = bpy.data.objects.new("Bridge_AnglePivot", None)
    piv.location = (0, 0, 0)
    piv.rotation_euler = (0, 0, math.radians(-16))
    coll.objects.link(piv)
    place(piv, e)

    n = 36
    centers = []
    for i in range(n):
        t = i / (n - 1)
        x = -2.35 + 4.70 * t
        # Smooth vaulted arch profile
        z = 0.52 + 1.18 * math.sin(t * math.pi)
        centers.append((x, 0.0, z))

    # 1. Wide arched cyan-blue roadway deck
    deck = _extrude_ribbon_3d("Bridge_ArchedDeck", centers, 2.15, 0.28, coll, deck_cyan)
    deck.parent = piv

    # 2. Chunky arched purple-magenta side walls flanking the deck
    for side in (-1, 1):
        w_pts = _offset_curve_pts(centers, side * 1.12, z_offset=0.14)
        wall = _extrude_ribbon_3d(f"Bridge_SideWall_{side}", w_pts, 0.34, 0.48, coll, wall_purp)
        wall.parent = piv
        t_pts = _offset_curve_pts(centers, side * 1.12, z_offset=0.40)
        top_r = poly_curve(f"Bridge_TopTrim_{side}", t_pts, 0.045, coll,
                           trim_gold if side > 0 else lane_glow, res=6)
        top_r.parent = piv

    # Glowing lane strips over the bridge deck
    for l_off in (-0.45, 0.0, 0.45):
        l_pts = _offset_curve_pts(centers, l_off, z_offset=0.16)
        ln = poly_curve(f"Bridge_Lane_{l_off}", l_pts, 0.032, coll, lane_glow, res=4)
        ln.parent = piv

    # 3. Massive chunky bevelled end abutment blocks (Purple block on left, Golden-orange block on right, matching reference!)
    left_blk = cube("Bridge_LeftAbutment", (-2.05, 0, 0.44), (1.28, 2.58, 0.88),
                    coll, abut_purp, bevel=0.09)
    left_blk.parent = piv
    right_blk = cube("Bridge_RightAbutment", (1.95, 0, 0.44), (1.35, 2.58, 0.88),
                     coll, abut_gold, bevel=0.09)
    right_blk.parent = piv

    label(e, coll, "KÖPRÜ / RAMPA  ·  eğim + neon", -2.9, 0.36, label_mat)
    finish(e, coll)
    return e


def build_biome_theme_molds(coll, label_mat):
    """Builds the 5 Biome Theme Showcase Molds from Section 4 of the reference sheet."""
    groups = []
    specs = (
        ("Biome_01_JellyLand", (-20, -49, 0), "JÖLE DİYARI (Ana Tema)",
         srgb("#2892F0"), C["cyan_light"], C["pink"], C["cyan"]),
        ("Biome_02_CandyLand", (-10, -49, 0), "ŞEKER ÜLKESİ (Candy)",
         srgb("#C84298"), C["pink_light"], C["gold"], C["pink"]),
        ("Biome_03_IceCrystal", (0, -49, 0), "BUZ KRİSTAL (Ice)",
         srgb("#1E78E8"), C["white"], C["cyan_light"], C["glass"]),
        ("Biome_04_LavaJelly", (10, -49, 0), "VOLKANİK JEL (Lava)",
         srgb("#4A1E38"), C["orange"], C["red"], C["gold"]),
        ("Biome_05_SpaceNeon", (20, -49, 0), "UZAY JEL (Neon/Galaksi)",
         srgb("#281858"), C["magenta"], C["cyan"], C["purple"]),
    )

    for grp_name, loc, lbl_text, base_col, rail_col, prop_col, accent_col in specs:
        e = model_group(grp_name, loc, coll)
        island_m = mat_plastic(grp_name + "_Base", base_col, rough=0.22, coat=0.7,
                               emission=base_col, strength=0.25)
        rail_m = _neon(grp_name + "_Rail", rail_col, 3.8)
        prop_m = mat_plastic(grp_name + "_Prop", prop_col, rough=0.22, coat=0.65,
                             emission=prop_col, strength=0.35)
        acc_m = mat_jelly(grp_name + "_JellyProp", accent_col, rail_col, alpha=0.94, rim_strength=2.8)

        # Mini crystal island base
        oct_pts = [(-1.8, -0.8), (1.8, -0.8), (2.1, 0.2), (1.4, 0.9), (-1.4, 0.9), (-2.1, 0.2)]
        deck = prism(grp_name + "_Island", oct_pts, 0.42, coll, island_m, plane='XY', bevel=0.06)
        deck.location = (0, 0, 0.32)
        place(deck, e)

        # Stylized tree + crystal spire + plump jelly dome on the biome island
        _stylized_pink_tree(grp_name + "_Tree", e, coll, (-0.85, 0.10, 0.52), scale=0.78)
        spire = cone(grp_name + "_Crystal", (0.05, 0.25, 0.95), 0.32, 0.0, 0.95, coll, rail_m,
                     rot=(0.1, -0.12, 0), verts=6, bevel=0.02)
        place(spire, e)
        dome = jelly_dome(grp_name + "_Jelly", (0.98, -0.05, 0.52), 0.42, coll, acc_m,
                          squash_z=0.92, widen_xy=1.08, seg=36, ring=20)
        place(dome, e)

        label(e, coll, lbl_text, -1.95, 0.30, label_mat)
        finish(e, coll)
        groups.append(e)

    return groups
