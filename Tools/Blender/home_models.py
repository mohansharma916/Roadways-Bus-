"""Builds the Home screen's tour bus and chibi passenger in Blender and exports them as FBX for Unity.

Run (no UI):
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Tools/Blender/home_models.py -- [--render DIR] [--no-export]

Models face Blender -Y (Unity +Z), stand on z = 0, and 1 Blender unit = 1 Unity unit. Every material is named by its
role (Paint, Glass, Shirt, ...): the game swaps them for its own colours and shader at runtime, so the colours here only
matter for the preview renders.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Models")
W, L = 1.05, 2.45  # the game's bus footprint (Core VehicleDef)
PALETTE = ["ee5253", "fed330", "1e90ff", "2bd97c", "8854d0", "e67e22", "e84393", "0fbcf9"]

# ------------------------------------------------------------------ helpers


def lin(hexstr):
    """sRGB hex -> linear RGB, which is what Blender's colour inputs expect."""
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


MATS = {}


def mat(name, hexstr, rough=0.5, alpha=1.0, emit=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True  # deprecated (always on) in newer Blender
    except Exception:
        pass
    m.diffuse_color = (*lin(hexstr), alpha)
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*lin(hexstr), 1)
    bsdf.inputs["Roughness"].default_value = rough
    if alpha < 1:
        bsdf.inputs["Alpha"].default_value = alpha
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            m.blend_method = "BLEND"
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*lin(hexstr), 1)
        bsdf.inputs["Emission Strength"].default_value = emit
    MATS[name] = m
    return m


PARTS = []


def finish(o, m, smooth=40):
    """Bakes modifiers and transforms into the mesh (world space) and queues the part for joining."""
    o.data.materials.append(m)
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    for mod in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth))
    PARTS.append(o)
    return o


def rbox(size, loc, m, r=0.03, rot=(0, 0, 0), seg=3):
    """Rounded box: size in units, r the corner radius."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if r > 0:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width, b.segments, b.limit_method = r, seg, "NONE"
    return finish(o, m)


def ball(radius, loc, m, scale=(1, 1, 1), rot=(0, 0, 0), seg=24):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=seg // 2, radius=radius, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, m, smooth=180)


def cyl(radius, depth, loc, m, rot=(0, 0, 0), verts=32, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    if bevel:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width, b.segments, b.limit_method = bevel, 2, "NONE"
    return finish(o, m)


def cut_below(o, z):
    """Deletes the part's vertices under height z (turns a ball into a dome)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < z], context="VERTS")
    bm.to_mesh(o.data)
    bm.free()


def join(name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in PARTS:
        o.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = o.data.name = name
    PARTS.clear()
    return o


# ------------------------------------------------------------------ chibi


def chibi(at=(0, 0, 0), s=1.0, shirt="Shirt", cap="Cap", shirt_hex="1e90ff", cap_hex="1e90ff"):
    """One passenger, 1 unit tall at s = 1, feet at `at`, facing -Y. Big head, cap, shirt, arms, legs."""
    ox, oy, oz = at

    def p(x, y, z):
        return (ox + x * s, oy + y * s, oz + z * s)

    def sc(t):
        return tuple(v * s for v in t)

    skin, eye = mat("Skin", "ffc89a", 0.6), mat("Eye", "1d1d2b", 0.25)
    shirt_m, cap_m = mat(shirt, shirt_hex, 0.55), mat(cap, cap_hex, 0.5)
    for side in (-1, 1):
        ball(0.07 * s, p(side * 0.085, -0.025, 0.04), mat("Shoe", "f4f4f6", 0.4), scale=(1, 1.35, 0.62))
        ball(0.07 * s, p(side * 0.085, 0, 0.14), mat("Pants", "3b4a6b", 0.6), scale=(1, 1, 1.5))
        ball(0.058 * s, p(side * 0.215, 0, 0.32), shirt_m, scale=(1, 1, 2.0), rot=(0, math.radians(-20 * side), 0))
        ball(0.06 * s, p(side * 0.255, 0, 0.2), skin)
    ball(0.2 * s, p(0, 0, 0.33), shirt_m, scale=(1, 0.85, 1.05))
    ball(0.27 * s, p(0, 0, 0.72), skin, scale=(1, 0.95, 0.93), seg=32)
    # Cap: the top of a slightly larger ball, and a flattened brim that tips down over the eyes.
    dome = ball(0.276 * s, p(0, 0.01, 0.745), cap_m, scale=(1, 1, 0.92), seg=32)
    cut_below(dome, oz + 0.775 * s)
    ball(0.15 * s, p(0, -0.215, 0.79), cap_m, scale=(1, 0.85, 0.1), rot=(math.radians(-6), 0, 0))
    # Face, placed on the head's surface.
    for side in (-1, 1):
        ball(0.042 * s, p(side * 0.095, -0.233, 0.69), eye, scale=(0.8, 0.5, 1.25))
        ball(0.013 * s, p(side * 0.095 + 0.012 * s, -0.252, 0.71), mat("Shine", "ffffff", 0.2, emit=0.4))
        ball(0.04 * s, p(side * 0.165, -0.175, 0.625), mat("Cheek", "ff9aa2", 0.7), scale=(1.2, 0.35, 0.75))
    ball(0.022 * s, p(0, -0.228, 0.615), mat("Mouth", "8a2d3b", 0.5), scale=(1.4, 0.4, 0.7))


# ------------------------------------------------------------------ bus


def bus():
    """Open-top beach tour bus on the game's footprint: lower deck with tinted windows and a driver, sign band,
    open upper deck with seats and a rail. Front at -Y."""
    paint, trim, dark = mat("Paint", "2f7bff", 0.45), mat("Trim", "e4e7ec", 0.35), mat("Tyre", "2b2b33", 0.8)
    glass, interior = mat("Glass", "a8d8f0", 0.05, alpha=0.25), mat("Interior", "2a3550", 0.7)
    front, back = -L / 2, L / 2

    # Wheels: tyres with hubcaps, half hidden under the body like a toy's.
    for y in (-0.78, 0.76):
        for side in (-1, 1):
            cyl(0.19, 0.14, (side * (W / 2 - 0.06), y, 0.19), dark, rot=(0, math.radians(90), 0), bevel=0.03)
            cyl(0.1, 0.15, (side * (W / 2 - 0.06), y, 0.19), trim, rot=(0, math.radians(90), 0), bevel=0.02)

    # Lower body, a white stripe along it, bumpers.
    rbox((W, L, 0.34), (0, 0, 0.36), paint, r=0.09, seg=4)
    rbox((W + 0.012, L - 0.3, 0.05), (0, 0.05, 0.42), mat("Stripe", "ffffff", 0.4), r=0.02)
    rbox((W + 0.03, 0.1, 0.1), (0, front - 0.02, 0.23), trim, r=0.04)
    rbox((W + 0.03, 0.1, 0.1), (0, back + 0.02, 0.23), trim, r=0.04)

    # Front face: grille, round headlights in chrome rings, mirrors.
    rbox((0.34, 0.04, 0.13), (0, front - 0.005, 0.33), trim, r=0.03)
    for i in range(3):
        rbox((0.28, 0.02, 0.02), (0, front - 0.025, 0.295 + i * 0.035), dark, r=0.008)
    for side in (-1, 1):
        x = side * (W / 2 - 0.17)
        cyl(0.1, 0.03, (x, front - 0.005, 0.33), trim, rot=(math.radians(90), 0, 0), bevel=0.01)
        cyl(0.078, 0.03, (x, front - 0.02, 0.33), mat("Light", "fff4d2", 0.2, emit=1.5), rot=(math.radians(90), 0, 0), bevel=0.01)
        rbox((0.13, 0.025, 0.025), (side * (W / 2 + 0.06), front + 0.06, 0.78), dark, r=0.01)
        rbox((0.05, 0.035, 0.15), (side * (W / 2 + 0.12), front + 0.05, 0.71), dark, r=0.02)
        rbox((0.1, 0.03, 0.07), (side * (W / 2 - 0.12), back + 0.005, 0.33), mat("Tail", "d92b2b", 0.4, emit=0.3), r=0.02)

    # Lower deck: a dark cabin behind tinted glass, with an open driver's corner at the front so the driver shows.
    z0, h = 0.53, 0.3
    zc = z0 + h / 2
    rbox((W - 0.1, L - 0.42, h - 0.02), (0, 0.16, zc), interior, r=0.02)
    rbox((W - 0.1, 0.12, 0.08), (0, front + 0.1, z0 + 0.06), interior, r=0.02)  # dashboard
    rbox((0.016, L - 0.08, h - 0.02), (-(W / 2 - 0.02), 0, zc), glass, r=0.005)
    rbox((0.016, L - 0.08, h - 0.02), (W / 2 - 0.02, 0, zc), glass, r=0.005)
    rbox((W - 0.06, 0.016, h - 0.02), (0, front + 0.025, zc), glass, r=0.005)
    rbox((W - 0.06, 0.016, h - 0.02), (0, back - 0.025, zc), glass, r=0.005)
    for sx in (-1, 1):
        for sy in (-1, 1):
            rbox((0.07, 0.07, h), (sx * (W / 2 - 0.035), sy * (L / 2 - 0.035), zc), paint, r=0.025)
        for y in (front + 0.45, front + 0.85, front + 1.25, front + 1.65, front + 2.05):
            rbox((0.03, 0.06, h), (sx * (W / 2 - 0.012), y, zc), paint, r=0.01)
    shine = mat("Shine", "ffffff", 0.2, emit=0.4)
    rbox((0.035, 0.006, 0.24), (-0.22, front + 0.012, zc), shine, r=0.003, rot=(0, math.radians(28), 0))
    rbox((0.02, 0.006, 0.14), (-0.13, front + 0.012, zc + 0.03), shine, r=0.003, rot=(0, math.radians(28), 0))
    # Door on the kerb side (the bus's right, -X), just behind the front wheel.
    rbox((0.02, 0.32, 0.56), (-(W / 2 + 0.002), front + 0.42, 0.52), glass, r=0.01)
    rbox((0.024, 0.012, 0.56), (-(W / 2 + 0.004), front + 0.42, 0.52), dark, r=0.004)

    # Driver on the left, hands on a steering wheel.
    chibi(at=(0.22, front + 0.27, 0.27), s=0.6, shirt="Uniform", cap="Uniform", shirt_hex="35507e", cap_hex="35507e")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.075, minor_radius=0.012, location=(0.22, front + 0.15, 0.63),
                                     rotation=(math.radians(60), 0, 0))
    finish(bpy.context.active_object, dark, smooth=180)

    # Band between the decks, carrying the sign (the game writes the level on it).
    rbox((W, L, 0.14), (0, 0, 0.89), paint, r=0.05)
    rbox((0.78, 0.02, 0.17), (0, front - 0.005, 0.89), mat("SignFrame", "2e3448", 0.5), r=0.03)
    rbox((0.72, 0.03, 0.12), (0, front - 0.01, 0.89), mat("Sign", "ffffff", 0.4), r=0.025)

    # Open upper deck: low walls, a chrome rail on posts, rows of seats.
    top = 0.96
    rbox((0.06, L, 0.24), (-(W / 2 - 0.03), 0, top + 0.12), paint, r=0.025)
    rbox((0.06, L, 0.24), (W / 2 - 0.03, 0, top + 0.12), paint, r=0.025)
    rbox((W, 0.06, 0.24), (0, front + 0.03, top + 0.12), paint, r=0.025)
    rbox((W, 0.06, 0.24), (0, back - 0.03, top + 0.12), paint, r=0.025)
    rail = top + 0.34
    for side in (-1, 1):
        cyl(0.02, L - 0.1, (side * (W / 2 - 0.03), 0, rail), trim, rot=(math.radians(90), 0, 0), verts=12)
        for y in (-1.0, -0.5, 0.0, 0.5, 1.0):
            cyl(0.014, 0.12, (side * (W / 2 - 0.03), y, rail - 0.06), trim, verts=10)
    cyl(0.02, W - 0.06, (0, front + 0.03, rail), trim, rot=(0, math.radians(90), 0), verts=12)
    seat = mat("Seat", "f3e2c4", 0.6)
    for y in (front + 0.42, front + 0.86, front + 1.30, front + 1.74):
        for side in (-1, 1):
            rbox((0.36, 0.28, 0.08), (side * 0.235, y, top + 0.08), seat, r=0.03)
            rbox((0.36, 0.08, 0.28), (side * 0.235, y + 0.13, top + 0.2), seat, r=0.035)


# ------------------------------------------------------------------ export & preview


def export(obj, name):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    print("exported", name, len(obj.data.polygons), "faces,", [m.name for m in obj.data.materials])


def recolour(obj, hexstr):
    """Preview copy of a passenger in another palette colour."""
    copy = obj.copy()
    copy.data = obj.data.copy()
    bpy.context.collection.objects.link(copy)
    for i, m in enumerate(copy.data.materials):
        if m.name in ("Shirt", "Cap"):
            copy.data.materials[i] = mat(m.name + hexstr, hexstr, m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value)
    return copy


def shoot(scene, cam, pos, target, lens, size, path):
    cam.location = pos
    cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = lens
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def preview(bus_obj, man, out_dir):
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            pass
    scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (*lin("bfe6f5"), 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(42), 0, math.radians(-30))
    scene.collection.objects.link(sun)

    # Ground: sand, a road under the bus, a pavement for the queue.
    rbox((40, 40, 0.1), (0, 0, -0.05), mat("Sand", "f5deb0", 0.9), r=0)
    rbox((1.9, 40, 0.02), (0, 0, 0.01), mat("Road", "6b7080", 0.8), r=0)
    rbox((1.0, 40, 0.04), (-1.45, 0, 0.02), mat("Pavement", "f6e7c8", 0.8), r=0)
    PARTS.clear()

    # The queue beside the bus, in the game's colours, at about half the bus's height.
    for i, hexstr in enumerate(PALETTE):
        q = recolour(man, hexstr)
        q.scale = (0.6, 0.6, 0.6)
        q.location = (-1.42 + (i % 2) * 0.1, -1.7 + i * 0.5, 0.04)
    man.hide_render = True

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    shoot(scene, cam, (0.1, -5.6, 2.6), (-0.3, 0.0, 0.55), 38, (720, 1000), os.path.join(out_dir, "home_preview.png"))
    shoot(scene, cam, (-3.4, -4.2, 3.6), (0, 0, 0.6), 45, (900, 700), os.path.join(out_dir, "bus_three_quarter.png"))

    # Close-up line-up of passengers, well away from the bus.
    man.hide_render = False
    for i, hexstr in enumerate(PALETTE[:4]):
        q = recolour(man, hexstr)
        q.location = (40 + (i - 1.5) * 0.7, 0, 0)
    man.location = (60, 0, 0)  # keep the original out of the close-up
    shoot(scene, cam, (40, -3.6, 1.5), (40, 0, 0.5), 50, (900, 600), os.path.join(out_dir, "passengers.png"))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bus()
    bus_obj = join("HomeBus")
    chibi()
    man = join("Chibi")
    if "--no-export" not in args:
        export(bus_obj, "HomeBus")
        export(man, "Chibi")
    if "--render" in args:
        preview(bus_obj, man, args[args.index("--render") + 1])


main()
