# -*- coding: utf-8 -*-
"""
PROJECT JELLY - Blender 3D kit builder
=====================================
Builds the full object set from the design sheet at real gameplay scale:
jellies, pickups, gates, tunnels, hazards, spline track, island and bridge.

Usage:
    blender -b -P build_project_jelly_kit.py
    set PJ_QUICK=1  (fast preview renders)
"""
import math
import os
import sys

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)

from pj.common import (  # noqa: E402
    C, new_collection, mat_neon, mat_floor, mat_plastic, aim, MATS, text,
)
from pj.jellies import build_jelly_variants, build_jelly_effect_molds  # noqa: E402
from pj.pickups import build_pickups, build_extra_pickups  # noqa: E402
from pj.obstacles import (  # noqa: E402
    build_gates, build_tunnels, build_pendulum, build_spinner,
    build_multihole_walls, build_phase4_object_families,
)
from pj.environment import (  # noqa: E402
    build_path, build_barrier, build_island, build_bridge, build_biome_theme_molds,
)

QUICK = os.environ.get("PJ_QUICK", "0") == "1"
INDIVIDUAL = os.environ.get("PJ_INDIVIDUAL", "1") == "1"
RENDERS = os.path.join(ROOT, "renders")
ITEMS_DIR = os.path.join(RENDERS, "items")
BLEND_PATH = os.path.join(ROOT, "ProjectJelly_Kit.blend")
GLB_DIR = r"F:\Jelly\Assets\Jelly\Models"
GLB_PATH = os.path.join(GLB_DIR, "ProjectJelly_Kit.glb")
FBX_PATH = os.path.join(GLB_DIR, "ProjectJelly_Kit.fbx")
os.makedirs(RENDERS, exist_ok=True)
os.makedirs(ITEMS_DIR, exist_ok=True)
os.makedirs(GLB_DIR, exist_ok=True)

# --------------------------------------------------------------- fresh file --
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0

C_JELLY = new_collection("01_PLAYER_JELLIES")
C_PICK = new_collection("02_PICKUPS")
C_OBS = new_collection("03_OBSTACLES")
C_ENV = new_collection("04_ENVIRONMENT")
C_STAGE = new_collection("05_STAGE_LIGHTS")

LABEL = mat_neon("LabelWhite", C["white"], 1.1, base=C["white"], rough=0.4)
LABEL_DIM = mat_neon("LabelDim", C["glass"], 0.7, base=C["glass"], rough=0.4)

# --------------------------------------------------------------- build kit ---
build_jelly_variants(C_JELLY, LABEL)
build_jelly_effect_molds(C_JELLY, LABEL)
build_pickups(C_PICK, LABEL)
build_extra_pickups(C_PICK, LABEL)
build_gates(C_OBS, LABEL)
build_tunnels(C_OBS, LABEL)
build_pendulum(C_OBS, LABEL)
build_spinner(C_OBS, LABEL)
build_multihole_walls(C_OBS, LABEL)
build_phase4_object_families(C_OBS, LABEL)
build_path(C_ENV, LABEL)
build_barrier(C_ENV, LABEL)
build_island(C_ENV, LABEL)
build_bridge(C_ENV, LABEL)
build_biome_theme_molds(C_ENV, LABEL)

print("[PJ] objects:", len(bpy.data.objects))


# ------------------------------------------------------------------ stage ----
def setup_stage():
    bpy.ops.mesh.primitive_plane_add(size=420, location=(0, 0, -0.02))
    floor = bpy.context.object
    floor.name = "Stage_Floor"
    for c in list(floor.users_collection):
        c.objects.unlink(floor)
    C_STAGE.objects.link(floor)
    floor.data.materials.append(mat_floor("StageFloorMat"))

    world = bpy.data.worlds.new("PJ_World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (*C["dark2"], 1)
    bg.inputs[1].default_value = 0.55


def setup_titles():
    text("Title_Project", "PROJECT", (-5.5, 23.0, 0.03), 1.55, C_STAGE,
         mat_neon("TitleCyan", C["cyan"], 4.0), rot=(0, 0, 0))
    text("Title_Jelly", "JELLY", (5.6, 23.0, 0.03), 1.55, C_STAGE,
         mat_neon("TitlePink", C["pink"], 4.0), rot=(0, 0, 0))
    text("Title_Sub", "3D OBJECT KIT  ·  1 UNIT = 1 M", (0, 21.2, 0.03), 0.52,
         C_STAGE, mat_neon("TitleSub", C["glass"], 1.6), rot=(0, 0, 0))
    frame = mat_neon("StageFrame", C["cyan_deep"], 2.2, base=C["cyan_deep"])
    for loc, dims in (((0, 24.9, 0.035), (49.0, 0.07, 0.035)),
                      ((0, -34.0, 0.035), (49.0, 0.07, 0.035)),
                      ((-24.45, -4.55, 0.035), (0.07, 58.9, 0.035)),
                      ((24.45, -4.55, 0.035), (0.07, 58.9, 0.035))):
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
        ob = bpy.context.object
        ob.name = "Stage_Frame"
        ob.scale = dims
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        ob.select_set(False)
        for c in list(ob.users_collection):
            c.objects.unlink(ob)
        C_STAGE.objects.link(ob)
        ob.data.materials.append(frame)


def area_light(name, loc, target, size, energy, color):
    data = bpy.data.lights.new(name, 'AREA')
    data.energy = energy
    data.shape = 'DISK'
    data.size = size
    data.color = color
    ob = bpy.data.objects.new(name, data)
    C_STAGE.objects.link(ob)
    ob.location = loc
    aim(ob, target)
    return ob


def setup_lights():
    area_light("Key_Warm", (-24, -34, 34), (0, -4, 0), 34, 13000, (1.0, 0.96, 0.9))
    area_light("Fill_Cyan", (30, -10, 24), (0, -4, 0), 28, 7500, (0.55, 0.82, 1.0))
    area_light("Rim_Magenta", (0, 32, 28), (0, -6, 1), 32, 12000, (1.0, 0.5, 0.88))
    area_light("Top_Cool", (0, -18, 48), (0, -10, 0), 42, 9500, (0.86, 0.92, 1.0))

    sd = bpy.data.lights.new("Sun", 'SUN')
    sd.energy = 1.4
    sd.angle = math.radians(9)
    sd.color = (1.0, 0.97, 0.92)
    sun = bpy.data.objects.new("Sun", sd)
    C_STAGE.objects.link(sun)
    sun.rotation_euler = (math.radians(52), 0, math.radians(28))


def make_camera(name, loc, target, lens=50):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.sensor_width = 36
    cd.clip_end = 2000
    ob = bpy.data.objects.new(name, cd)
    C_STAGE.objects.link(ob)
    ob.location = loc
    aim(ob, target)
    return ob


setup_stage()
setup_titles()
setup_lights()

CAM_OVER = make_camera("CAM_Overview", (1, -86, 60), (0, -11, 0.6), 50)
CAM_PICK = make_camera("CAM_JelliesPickups", (0, -17, 27), (0, 10, 1.3), 48)
CAM_OBS = make_camera("CAM_Obstacles", (3, -45, 30), (3, -14, 7.0), 38)
CAM_ENV = make_camera("CAM_Environment", (2, -66, 34), (2, -27, 5.0), 32)
CAM_HERO = make_camera("CAM_Hero", (2.2, 8.4, 1.45), (0, 18, 0.86), 85)

# -------------------------------------------------------------- render cfg ---
scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.render.film_transparent = False
for attr, val in (("use_raytracing", True), ("use_shadows", True)):
    try:
        setattr(scene.eevee, attr, val)
    except Exception:
        pass
try:
    scene.eevee.ray_tracing_options.use_denoise = True
except Exception:
    pass
scene.eevee.taa_render_samples = 12 if QUICK else 64
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
try:
    scene.view_settings.look = 'AgX - Punchy'
except Exception:
    pass
scene.view_settings.exposure = 0.3

scene.use_nodes = True
nt = scene.node_tree
for n in list(nt.nodes):
    nt.nodes.remove(n)
rl = nt.nodes.new("CompositorNodeRLayers")
gl = nt.nodes.new("CompositorNodeGlare")
gl.glare_type = 'FOG_GLOW'
gl.quality = 'HIGH'
gl.threshold = 1.05
gl.size = 7
comp = nt.nodes.new("CompositorNodeComposite")
nt.links.new(rl.outputs["Image"], gl.inputs["Image"])
nt.links.new(gl.outputs["Image"], comp.inputs["Image"])


def render_view(name, cam, w, h):
    scene.camera = cam
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.filepath = os.path.join(RENDERS, name)
    bpy.ops.render.render(write_still=True)
    print("[PJ] rendered", name)


def render_section(name, cam, w, h, show):
    """Render one kit section with the other model collections hidden."""
    hidden = [c for c in (C_JELLY, C_PICK, C_OBS, C_ENV) if c not in show]
    for c in hidden:
        c.hide_render = True
    frame_obs = [o for o in C_STAGE.objects
                 if (o.name.startswith("Stage_Frame") or o.name.startswith("Title_"))
                 and not o.hide_render]
    for o in frame_obs:
        o.hide_render = True
    render_view(name, cam, w, h)
    for c in hidden:
        c.hide_render = False
    for o in frame_obs:
        o.hide_render = False


def setup_hero_stage():
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 21.6, 3.2),
                                     rotation=(math.pi / 2, 0, 0))
    back = bpy.context.object
    back.name = "Hero_Backdrop"
    back.scale = (44, 16, 1)
    bpy.context.view_layer.objects.active = back
    back.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    back.select_set(False)
    for c in list(back.users_collection):
        c.objects.unlink(back)
    C_STAGE.objects.link(back)
    back.data.materials.append(mat_plastic("HeroBackdrop", C["dark2"],
                                           rough=1.0, metal=0.05))
    bd = bpy.data.materials["HeroBackdrop"].node_tree.nodes["Principled BSDF"]
    bd.inputs["Specular IOR Level"].default_value = 0.0
    lights = [
        area_light("Hero_Key", (-4.0, 13.0, 4.6), (0, 18, 1.0), 2.6, 240,
                   (1.0, 0.97, 0.90)),
        area_light("Hero_Fill", (5.0, 14.0, 2.8), (0, 18, 1.0), 3.2, 110,
                   (0.55, 0.85, 1.0)),
        area_light("Hero_Rim", (1.0, 20.6, 2.8), (0, 18, 1.1), 1.6, 220,
                   (0.45, 1.0, 0.82)),
    ]
    return back, lights


def render_hero(fname, w, h):
    back, hero_lights = setup_hero_stage()
    hidden = []
    for ob in bpy.data.objects:
        if ob.name.startswith("LBL_") or ob.name.startswith("Title_"):
            if not ob.hide_render:
                ob.hide_render = True
                hidden.append(ob)
    main_lights = []
    for ob in C_STAGE.objects:
        if ob.type == 'LIGHT' and ob not in hero_lights and not ob.hide_render:
            ob.hide_render = True
            main_lights.append(ob)
    render_view(fname, CAM_HERO, w, h)
    for ob in hidden:
        ob.hide_render = False
    for ob in main_lights:
        ob.hide_render = False
    bpy.data.objects.remove(back, do_unlink=True)
    for l in hero_lights:
        bpy.data.objects.remove(l, do_unlink=True)


# ------------------------------------------------------------ export / save --
def convert_curves():
    for c in (C_JELLY, C_PICK, C_OBS, C_ENV):
        for ob in list(c.all_objects):
            if ob.type == 'CURVE':
                bpy.ops.object.select_all(action='DESELECT')
                ob.select_set(True)
                bpy.context.view_layer.objects.active = ob
                bpy.ops.object.convert(target='MESH')
                for p in ob.data.polygons:
                    p.use_smooth = True


def select_kit():
    bpy.ops.object.select_all(action='DESELECT')
    for c in (C_JELLY, C_PICK, C_OBS, C_ENV):
        for ob in c.all_objects:
            if ob.type in {'MESH', 'EMPTY'}:
                ob.select_set(True)


convert_curves()

# documentation inside the .blend
notes = bpy.data.texts.new("README_ProjectJelly_Kit")
notes.write(
    "PROJECT JELLY - 3D Object Kit\n"
    "=============================\n\n"
    "Real gameplay scale (1 unit = 1 m):\n"
    "- Track width: 6.40 m\n"
    "- Gate opening height: 2.30 m, gate thickness: 0.55 m\n"
    "- Small jelly: D 0.85 m (<=75), Normal: D 1.0 m (76-114), Big: D 1.65 m (>=115)\n\n"
    "Collections:\n"
    "01_PLAYER_JELLIES : small, normal, big, split twins, merge\n"
    "02_PICKUPS        : grow, shrink, split, merge, speed, shield, magnet, star, slow\n"
    "03_OBSTACLES      : narrow gate, piston gate, glass wall, twin tunnel,\n"
    "                    moving twin tunnel, pendulum, spinner\n"
    "04_ENVIRONMENT    : spline track, side barrier, theme island, bridge/ramp\n"
    "05_STAGE_LIGHTS   : studio floor, lights and showcase cameras\n\n"
    "Each model is parented to a GRP_ empty placed on the floor; labels are\n"
    "children of the same empty, so the whole model + label can be moved.\n"
    "Exports: ProjectJelly_Kit.glb / .fbx (models only, stage excluded).\n"
)

select_kit()
try:
    bpy.ops.export_scene.gltf(filepath=GLB_PATH, export_format='GLB',
                              use_selection=True, export_apply=True)
    print("[PJ] GLB ->", GLB_PATH)
except Exception as exc:
    print("[PJ] GLB export failed:", exc)

try:
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True,
                             object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_ALL',
                             use_mesh_modifiers=True, bake_anim=False)
    print("[PJ] FBX ->", FBX_PATH)
except Exception as exc:
    print("[PJ] FBX export failed:", exc)

bpy.ops.object.select_all(action='DESELECT')
scene.camera = CAM_OVER
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("[PJ] BLEND ->", BLEND_PATH)

if os.environ.get("PJ_EXPORT_ONLY", "0") == "1":
    print("[PJ] PJ_EXPORT_ONLY=1 -> Skipping renders.")
    sys.exit(0)

# ------------------------------------------------------------------ renders --
def render_single_group(grp_name, fname, dist=6.0, height=2.2, target_z=0.9, lens=55,
                        cam_dx=0.0, w=960, h=720):
    grp = bpy.data.objects.get("GRP_" + grp_name)
    if grp is None:
        print("[PJ] missing group:", grp_name)
        return
    gx, gy, gz = grp.location
    cam = make_camera("CAM_Single_" + grp_name, (gx + cam_dx, gy - dist, gz + height),
                      (gx, gy, gz + target_z), lens)

    # Local studio backdrop & 3-point product lighting centered on (gx, gy, gz)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(gx, gy + 5.5, gz + 3.0),
                                     rotation=(math.pi / 2, 0, 0))
    back = bpy.context.object
    back.name = "Item_Backdrop"
    back.scale = (26.0, 14.0, 1.0)
    bpy.context.view_layer.objects.active = back
    back.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    back.select_set(False)
    for c in list(back.users_collection):
        c.objects.unlink(back)
    C_STAGE.objects.link(back)
    bg_mat = mat_plastic("ItemBackdropMat", C["stage_bg"], rough=0.95, metal=0.0, coat=0.0)
    back.data.materials.append(bg_mat)

    tgt = (gx, gy, gz + target_z)
    item_lights = [
        area_light("Item_Key", (gx - 4.2, gy - 5.5, gz + 5.2), tgt, 4.5, 420, (1.0, 0.97, 0.92)),
        area_light("Item_Fill", (gx + 4.8, gy - 4.2, gz + 3.4), tgt, 5.0, 260, (0.65, 0.88, 1.0)),
        area_light("Item_Rim", (gx, gy + 3.6, gz + 4.6), tgt, 4.2, 340, (0.85, 0.65, 1.0)),
    ]

    # Collect objects belonging to this group (excluding LBL_ floor labels!)
    keep = {grp}
    changed = True
    while changed:
        changed = False
        for ob in bpy.data.objects:
            if ob not in keep and ob.parent in keep and not ob.name.startswith("LBL_"):
                keep.add(ob)
                changed = True

    stage_coll = bpy.data.collections.get("05_STAGE_LIGHTS")
    stage_objs = set(stage_coll.all_objects) if stage_coll else set()

    hidden_objs = []
    for ob in bpy.data.objects:
        if ob == cam or ob == back or ob in item_lights:
            continue
        if ob in stage_objs:
            if (ob.name.startswith("Stage_Frame") or ob.name.startswith("Title_")
                    or (ob.type == 'LIGHT' and ob not in item_lights)) and not ob.hide_render:
                ob.hide_render = True
                hidden_objs.append(ob)
        elif ob not in keep and not ob.hide_render:
            ob.hide_render = True
            hidden_objs.append(ob)

    scene.camera = cam
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.filepath = os.path.join(ITEMS_DIR, fname)
    bpy.ops.render.render(write_still=True)
    print("[PJ] rendered item:", fname)

    for ob in hidden_objs:
        ob.hide_render = False
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.objects.remove(back, do_unlink=True)
    for l_ob in item_lights:
        bpy.data.objects.remove(l_ob, do_unlink=True)


SKIP_OVERVIEW = os.environ.get("PJ_SKIP_OVERVIEW", "0") == "1"
if not SKIP_OVERVIEW:
    if QUICK:
        render_view("00_overview_quick.png", CAM_OVER, 1280, 720)
        render_view("01_jellies_quick.png", CAM_PICK, 1280, 720)
        render_section("02_obstacles_quick.png", CAM_OBS, 1280, 720, (C_OBS,))
        render_section("03_environment_quick.png", CAM_ENV, 1280, 720, (C_ENV,))
        render_hero("04_hero_quick.png", 1280, 720)
    else:
        render_view("00_kit_overview.png", CAM_OVER, 2560, 1440)
        render_view("01_jellies_pickups.png", CAM_PICK, 2560, 1440)
        render_section("02_obstacles_gates.png", CAM_OBS, 2560, 1440, (C_OBS,))
        render_section("03_environment.png", CAM_ENV, 2560, 1440, (C_ENV,))
        render_hero("04_hero_jelly.png", 2560, 1440)

if INDIVIDUAL:
    # 1. Player Jellies (5) - Same camera distance on Small/Normal/Big to show true scale progression
    render_single_group("Jelly_Small", "jelly_01_small.png", dist=4.1, height=1.05, target_z=0.45, lens=60)
    render_single_group("Jelly_Normal", "jelly_02_normal.png", dist=4.1, height=1.05, target_z=0.48, lens=60)
    render_single_group("Jelly_Big", "jelly_03_big.png", dist=4.1, height=1.12, target_z=0.58, lens=60)
    render_single_group("Jelly_SplitTwin", "jelly_04_split_twin.png", dist=4.9, height=1.22, target_z=0.38, lens=55)
    render_single_group("Jelly_Merge", "jelly_05_merge.png", dist=4.1, height=1.08, target_z=0.50, lens=60)

    # 2. Core Pickups (5)
    render_single_group("Pickup_Grow", "pickup_01_grow.png", dist=3.5, height=1.20, target_z=1.10, lens=62)
    render_single_group("Pickup_Shrink", "pickup_02_shrink.png", dist=3.5, height=1.20, target_z=1.10, lens=62)
    render_single_group("Pickup_Split", "pickup_03_split.png", dist=4.2, height=1.32, target_z=1.15, lens=58)
    render_single_group("Pickup_Merge", "pickup_04_merge.png", dist=3.8, height=1.25, target_z=1.12, lens=60)
    render_single_group("Pickup_Speed", "pickup_05_speed.png", dist=4.1, height=1.75, target_z=0.68, lens=56)

    # 3. Phase 2 Approved 5 Core Obstacles (+ 3 Multi-Hole Decision Wall Variations)
    render_single_group("Gate_Narrow", "obs_01_narrow_gate.png", dist=9.8, height=2.25, target_z=1.25, lens=48)
    render_single_group("Gate_Piston", "obs_02_piston_gate.png", dist=9.8, height=2.25, target_z=1.25, lens=48)
    render_single_group("Obstacle_GlassWall", "obs_03_glass_wall.png", dist=9.8, height=2.25, target_z=1.25, lens=48)
    render_single_group("Tunnel_Twin", "obs_04_twin_tunnel.png", dist=9.8, height=2.25, target_z=1.22, lens=48)
    render_single_group("Gate_MultiHole_SizeChoice", "obs_05a_multihole_size.png", dist=9.8, height=2.25, target_z=1.25, lens=48)
    render_single_group("Gate_MultiHole_SplitChoice", "obs_05b_multihole_split.png", dist=9.8, height=2.25, target_z=1.25, lens=48)
    render_single_group("Gate_MultiHole_RiskReward", "obs_05c_multihole_risk_reward.png", dist=9.8, height=2.25, target_z=1.25, lens=48)

if os.environ.get("PJ_PHASE4_ITEMS", "1") == "1":
    render_single_group("Gate_FlexibleSqueeze", "obs_06_flexible_squeeze.png", dist=9.8, height=2.4, target_z=1.25, lens=48)
    render_single_group("Gate_MassScale", "obs_07_mass_scale.png", dist=9.8, height=2.6, target_z=1.25, lens=48)
    render_single_group("Gate_SlidingHoleWall", "obs_08_sliding_hole_wall.png", dist=9.8, height=2.25, target_z=1.25, lens=48)
    render_single_group("Hazard_MagneticField", "obs_09_magnetic_field.png", dist=9.5, height=2.4, target_z=1.10, lens=48)
    render_single_group("Hazard_StickyFloor", "obs_10_sticky_floor.png", dist=7.5, height=2.6, target_z=0.35, lens=50)
    render_single_group("Hazard_JumpPad", "obs_11_jump_pad.png", dist=5.2, height=1.9, target_z=0.45, lens=54)
    render_single_group("Gate_MicroShatter", "obs_12_micro_shatter.png", dist=9.8, height=2.25, target_z=1.25, lens=48)

if os.environ.get("PJ_EXTRA_ITEMS", "0") == "1":
    # 4. Optional Pickups (4)
    render_single_group("Pickup_Shield", "pickup_06_shield.png", dist=3.5, height=1.42, target_z=1.32, lens=60)
    render_single_group("Pickup_Magnet", "pickup_07_magnet.png", dist=3.7, height=1.52, target_z=1.42, lens=58)
    render_single_group("Pickup_Star", "pickup_08_star.png", dist=3.5, height=1.46, target_z=1.36, lens=60)
    render_single_group("Pickup_Slow", "pickup_09_slow.png", dist=3.8, height=1.50, target_z=1.36, lens=58)

    # 5. Dynamic Obstacles from Reference Sheet (3)
    render_single_group("Tunnel_Moving", "obs_13_moving_twin_tunnel.png", dist=9.8, height=2.25, target_z=1.22, lens=48)
    render_single_group("Obstacle_Pendulum", "obs_14_pendulum.png", dist=8.8, height=2.25, target_z=1.65, lens=48)
    render_single_group("Obstacle_Spinner", "obs_15_spinner.png", dist=8.6, height=2.10, target_z=1.15, lens=48)

    # 6. Environment & Track Modules (4)
    render_single_group("Track_Path", "env_01_spline_track.png", dist=9.2, height=3.9, target_z=0.85, lens=46)
    render_single_group("Track_Barrier", "env_02_side_barrier.png", dist=7.2, height=2.2, target_z=0.88, lens=50, cam_dx=1.2)
    render_single_group("Deko_Island", "env_03_theme_island.png", dist=7.6, height=2.3, target_z=1.38, lens=48)
    render_single_group("Track_Bridge", "env_04_bridge_ramp.png", dist=8.4, height=2.5, target_z=0.95, lens=48, cam_dx=0.8)

    # 7. Faz 5: 3 Visual Concept Target Renders (Renkli Arcade / Premium Minimalist / Futuristik Neon)
    bg_node = scene.world.node_tree.nodes.get("Background")
    floor_mat = bpy.data.materials.get("StageFloorMat")
    floor_bsdf = floor_mat.node_tree.nodes.get("Principled BSDF") if floor_mat and floor_mat.use_nodes else None

    if bg_node:
        bg_node.inputs[0].default_value = (0.26, 0.14, 0.48, 1.0)
        bg_node.inputs[1].default_value = 0.85
    if floor_bsdf:
        floor_bsdf.inputs["Base Color"].default_value = (0.20, 0.12, 0.42, 1.0)
        floor_bsdf.inputs["Roughness"].default_value = 0.22
    scene.view_settings.exposure = 0.42
    render_view(os.path.join("items", "concept_01_renkli_arcade.png"), CAM_OVER, 1280, 720)

    if bg_node:
        bg_node.inputs[0].default_value = (0.78, 0.84, 0.92, 1.0)
        bg_node.inputs[1].default_value = 1.05
    if floor_bsdf:
        floor_bsdf.inputs["Base Color"].default_value = (0.72, 0.78, 0.88, 1.0)
        floor_bsdf.inputs["Roughness"].default_value = 0.65
    scene.view_settings.exposure = 0.25
    render_view(os.path.join("items", "concept_02_premium_minimalist.png"), CAM_OVER, 1280, 720)

    if bg_node:
        bg_node.inputs[0].default_value = (0.015, 0.02, 0.055, 1.0)
        bg_node.inputs[1].default_value = 0.35
    if floor_bsdf:
        floor_bsdf.inputs["Base Color"].default_value = (0.02, 0.03, 0.08, 1.0)
        floor_bsdf.inputs["Roughness"].default_value = 0.08
    scene.view_settings.exposure = 0.48
    render_view(os.path.join("items", "concept_03_futuristik_neon.png"), CAM_OVER, 1280, 720)

if os.environ.get("PJ_MOLDS_ITEMS", "0") == "1":
    # 8. Jelly Effect Molds (3)
    render_single_group("FX_SquashSplash", "fx_01_squash_splash.png", dist=3.6, height=1.15, target_z=0.38, lens=58)
    render_single_group("FX_ImpactHit", "fx_02_impact_hit.png", dist=3.6, height=1.25, target_z=0.52, lens=58)
    render_single_group("FX_SacrificeBurst", "fx_03_sacrifice_burst.png", dist=3.8, height=1.28, target_z=0.52, lens=58)

    # 9. 5 Biome Theme Showcase Molds (5)
    render_single_group("Biome_01_JellyLand", "biome_01_jelly_land.png", dist=5.8, height=2.1, target_z=0.85, lens=52)
    render_single_group("Biome_02_CandyLand", "biome_02_candy_land.png", dist=5.8, height=2.1, target_z=0.85, lens=52)
    render_single_group("Biome_03_IceCrystal", "biome_03_ice_crystal.png", dist=5.8, height=2.1, target_z=0.85, lens=52)
    render_single_group("Biome_04_LavaJelly", "biome_04_lava_jelly.png", dist=5.8, height=2.1, target_z=0.85, lens=52)
    render_single_group("Biome_05_SpaceNeon", "biome_05_space_neon.png", dist=5.8, height=2.1, target_z=0.85, lens=52)

print("[PJ] done.")

