"""Builds the game's Blender-made models and exports them as FBX to Assets/_Project/Resources/Models:

  HomeStage   the Home screen: a beach-town street with the tour bus (driver at the wheel) at a bus stop, a queue of
              passengers, shops, palms, the promenade and the sea view. Empties mark the bus sign and the camera.
  Toy<Kind>   the four depot vehicles (Coupe, BigCar, Minivan, Bus) on their Core footprints, roofs flat for the arrow.
  materials.txt  every material's role name, colour, roughness and alpha.

Run (no UI):
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python Tools/Blender/models.py -- [--render DIR] [--no-export]

Conventions: models face Blender -Y (Unity +Z) and stand on z = 0; 1 Blender unit = 1 Unity unit. Materials are named by
role; the game rebuilds each from its own device-safe shader templates in the listed colour, recolouring Paint (vehicle
body) and Shirt/Cap (passengers) per use. The Home camera looks +Y from -Y; the game turns the stage 180 degrees so it
looks the same, which makes the --render preview what the phone shows (at 720 x 1600).
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "Models")
PALETTE = ["ee5253", "fed330", "1e90ff", "2bd97c", "8854d0", "e67e22", "e84393", "0fbcf9"]  # ColorPaletteDatabase order
QUEUE = [2, 0, 4, 1, 3, 6, 2, 0, 1, 4]  # palette index per Home passenger (SliceHome.QueueColours)

# Home camera: sensor fitted to the width (the game keeps this width on every screen). SliceHome.HalfWidthTan = 18 / LENS.
LENS = 30.0
CAM_POS, CAM_LOOK = (-0.24, -2.86, 1.88), (-0.24, 1.63, -0.31)

# ------------------------------------------------------------------ helpers


def lin(hexstr):
    """sRGB hex -> linear RGB, which is what Blender's colour inputs expect."""
    c = [int(hexstr[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


MATS = {}  # name -> (material, hex, roughness, alpha)


def mat(name, hexstr, rough=0.5, alpha=1.0, emit=0.0):
    if name in MATS:
        return MATS[name][0]
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*lin(hexstr), alpha)
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*lin(hexstr), 1)
    bsdf.inputs["Roughness"].default_value = rough
    if alpha < 1:
        bsdf.inputs["Alpha"].default_value = alpha
        m.surface_render_method = "BLENDED"
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*lin(hexstr), 1)
        bsdf.inputs["Emission Strength"].default_value = emit
    MATS[name] = (m, hexstr, rough, alpha)
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


def rbox(size, loc, m, r=0.03, rot=(0, 0, 0), seg=3, top=None):
    """Rounded box: size in units, r the corner radius. top=(sx, sy, shift_y) narrows the top face, for slanted glass."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if top:
        for v in o.data.vertices:
            if v.co.z > 0:
                v.co.x *= top[0]
                v.co.y = v.co.y * top[1] + top[2]
    if r > 0:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width, b.segments, b.limit_method = r, seg, "NONE"
    return finish(o, m)


def ball(radius, loc, m, scale=(1, 1, 1), rot=(0, 0, 0), seg=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=max(6, seg // 2), radius=radius, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = scale
    return finish(o, m, smooth=180)


def cyl(radius, depth, loc, m, rot=(0, 0, 0), verts=20, bevel=0.0, top=None):
    """Cylinder along its Z; top = the top radius, for cones and tapered towers."""
    if top is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=top, depth=depth, location=loc, rotation=rot)
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


def empty(name, loc):
    e = bpy.data.objects.new(name, None)
    e.location = loc
    bpy.context.scene.collection.objects.link(e)
    return e


R = math.radians

# ------------------------------------------------------------------ passenger


def chibi(at=(0, 0, 0), s=1.0, shirt="Shirt", cap="Cap", shirt_hex="1e90ff", cap_hex="1e90ff"):
    """One passenger, 1 unit tall at s = 1, feet at `at`, facing -Y. Big head, cap, shirt, arms, legs."""
    ox, oy, oz = at

    def p(x, y, z):
        return (ox + x * s, oy + y * s, oz + z * s)

    skin, eye = mat("Skin", "ffc89a", 0.6), mat("Eye", "1d1d2b", 0.25)
    shirt_m, cap_m = mat(shirt, shirt_hex, 0.55), mat(cap, cap_hex, 0.5)
    for side in (-1, 1):
        ball(0.07 * s, p(side * 0.085, -0.025, 0.04), mat("Shoe", "f4f4f6", 0.4), scale=(1, 1.35, 0.62), seg=12)
        ball(0.07 * s, p(side * 0.085, 0, 0.14), mat("Pants", "3b4a6b", 0.6), scale=(1, 1, 1.5), seg=12)
        ball(0.058 * s, p(side * 0.215, 0, 0.32), shirt_m, scale=(1, 1, 2.0), rot=(0, R(-20 * side), 0), seg=12)
        ball(0.06 * s, p(side * 0.255, 0, 0.2), skin, seg=12)
    ball(0.2 * s, p(0, 0, 0.33), shirt_m, scale=(1, 0.85, 1.05), seg=20)
    ball(0.27 * s, p(0, 0, 0.72), skin, scale=(1, 0.95, 0.93), seg=24)
    # Cap: the top of a slightly larger ball, and a flattened brim over the eyes.
    dome = ball(0.276 * s, p(0, 0.01, 0.745), cap_m, scale=(1, 1, 0.92), seg=24)
    cut_below(dome, oz + 0.775 * s)
    ball(0.15 * s, p(0, -0.215, 0.79), cap_m, scale=(1, 0.85, 0.1), rot=(R(-6), 0, 0), seg=16)
    # Face, on the head's surface.
    for side in (-1, 1):
        ball(0.042 * s, p(side * 0.095, -0.233, 0.69), eye, scale=(0.8, 0.5, 1.25), seg=12)
        ball(0.013 * s, p(side * 0.095 + 0.012, -0.252, 0.71), mat("Shine", "ffffff", 0.2, emit=0.4), seg=8)
        ball(0.04 * s, p(side * 0.165, -0.175, 0.625), mat("Cheek", "ff9aa2", 0.7), scale=(1.2, 0.35, 0.75), seg=10)
    ball(0.022 * s, p(0, -0.228, 0.615), mat("Mouth", "8a2d3b", 0.5), scale=(1.4, 0.4, 0.7), seg=10)


# ------------------------------------------------------------------ Home tour bus

W, L = 1.05, 2.45  # the bus footprint (Core VehicleDef)


def tour_bus(ox):
    """Open-top beach tour bus: tinted lower deck with a driver, sign band, open upper deck with seats and a rail.
    Front at -Y, centred on x = ox."""
    paint, trim, dark = mat("Paint", "2f7bff", 0.45), mat("Trim", "e4e7ec", 0.35), mat("Tyre", "2b2b33", 0.8)
    glass, interior = mat("Glass", "a8d8f0", 0.05, alpha=0.25), mat("Interior", "2a3550", 0.7)
    front, back = -L / 2, L / 2

    def X(x):
        return ox + x

    for y in (-0.78, 0.76):
        for side in (-1, 1):
            cyl(0.19, 0.14, (X(side * (W / 2 - 0.06)), y, 0.19), dark, rot=(0, R(90), 0), bevel=0.03, verts=24)
            cyl(0.1, 0.15, (X(side * (W / 2 - 0.06)), y, 0.19), trim, rot=(0, R(90), 0), bevel=0.02)

    rbox((W, L, 0.34), (X(0), 0, 0.36), paint, r=0.09, seg=4)
    rbox((W + 0.012, L - 0.3, 0.05), (X(0), 0.05, 0.42), mat("Stripe", "ffffff", 0.4), r=0.02)
    rbox((W + 0.03, 0.1, 0.1), (X(0), front - 0.02, 0.23), trim, r=0.04)
    rbox((W + 0.03, 0.1, 0.1), (X(0), back + 0.02, 0.23), trim, r=0.04)

    rbox((0.34, 0.04, 0.13), (X(0), front - 0.005, 0.33), trim, r=0.03)
    for i in range(3):
        rbox((0.28, 0.02, 0.02), (X(0), front - 0.025, 0.295 + i * 0.035), dark, r=0.008)
    for side in (-1, 1):
        x = side * (W / 2 - 0.17)
        cyl(0.1, 0.03, (X(x), front - 0.005, 0.33), trim, rot=(R(90), 0, 0), bevel=0.01, verts=24)
        cyl(0.078, 0.03, (X(x), front - 0.02, 0.33), mat("Light", "fff4d2", 0.2, emit=1.5), rot=(R(90), 0, 0), bevel=0.01, verts=24)
        rbox((0.13, 0.025, 0.025), (X(side * (W / 2 + 0.06)), front + 0.06, 0.78), dark, r=0.01)
        rbox((0.05, 0.035, 0.15), (X(side * (W / 2 + 0.12)), front + 0.05, 0.71), dark, r=0.02)
        rbox((0.1, 0.03, 0.07), (X(side * (W / 2 - 0.12)), back + 0.005, 0.33), mat("Tail", "d92b2b", 0.4, emit=0.3), r=0.02)

    # Lower deck: a dark cabin behind tinted glass, with an open driver's corner at the front so the driver shows.
    z0, h = 0.53, 0.3
    zc = z0 + h / 2
    rbox((W - 0.1, L - 0.42, h - 0.02), (X(0), 0.16, zc), interior, r=0.02)
    rbox((W - 0.1, 0.12, 0.08), (X(0), front + 0.1, z0 + 0.06), interior, r=0.02)  # dashboard
    rbox((0.016, L - 0.08, h - 0.02), (X(-(W / 2 - 0.02)), 0, zc), glass, r=0.005)
    rbox((0.016, L - 0.08, h - 0.02), (X(W / 2 - 0.02), 0, zc), glass, r=0.005)
    rbox((W - 0.06, 0.016, h - 0.02), (X(0), front + 0.025, zc), glass, r=0.005)
    rbox((W - 0.06, 0.016, h - 0.02), (X(0), back - 0.025, zc), glass, r=0.005)
    for sx in (-1, 1):
        for sy in (-1, 1):
            rbox((0.07, 0.07, h), (X(sx * (W / 2 - 0.035)), sy * (L / 2 - 0.035), zc), paint, r=0.025)
        for y in (front + 0.45, front + 0.85, front + 1.25, front + 1.65, front + 2.05):
            rbox((0.03, 0.06, h), (X(sx * (W / 2 - 0.012)), y, zc), paint, r=0.01)
    shine = mat("Shine", "ffffff", 0.2, emit=0.4)
    rbox((0.035, 0.006, 0.24), (X(-0.22), front + 0.012, zc), shine, r=0.003, rot=(0, R(28), 0))
    rbox((0.02, 0.006, 0.14), (X(-0.13), front + 0.012, zc + 0.03), shine, r=0.003, rot=(0, R(28), 0))
    # Door on the kerb side (the bus's right, -X), just behind the front wheel.
    rbox((0.02, 0.32, 0.56), (X(-(W / 2 + 0.002)), front + 0.42, 0.52), glass, r=0.01)
    rbox((0.024, 0.012, 0.56), (X(-(W / 2 + 0.004)), front + 0.42, 0.52), dark, r=0.004)

    chibi(at=(X(0.22), front + 0.27, 0.27), s=0.6, shirt="Uniform", cap="Uniform", shirt_hex="35507e", cap_hex="35507e")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.075, minor_radius=0.012, location=(X(0.22), front + 0.15, 0.63),
                                     rotation=(R(60), 0, 0), major_segments=20, minor_segments=6)
    finish(bpy.context.active_object, dark, smooth=180)

    # Band between the decks, carrying the sign (the game writes the level on it).
    rbox((W, L, 0.14), (X(0), 0, 0.89), paint, r=0.05)
    rbox((0.78, 0.02, 0.17), (X(0), front - 0.005, 0.89), mat("SignFrame", "2e3448", 0.5), r=0.03)
    rbox((0.72, 0.03, 0.12), (X(0), front - 0.01, 0.89), mat("Sign", "ffffff", 0.4), r=0.025)

    # Open upper deck: low walls, a chrome rail on posts, rows of seats.
    top = 0.96
    rbox((0.06, L, 0.24), (X(-(W / 2 - 0.03)), 0, top + 0.12), paint, r=0.025)
    rbox((0.06, L, 0.24), (X(W / 2 - 0.03), 0, top + 0.12), paint, r=0.025)
    rbox((W, 0.06, 0.24), (X(0), front + 0.03, top + 0.12), paint, r=0.025)
    rbox((W, 0.06, 0.24), (X(0), back - 0.03, top + 0.12), paint, r=0.025)
    rail = top + 0.34
    for side in (-1, 1):
        cyl(0.02, L - 0.1, (X(side * (W / 2 - 0.03)), 0, rail), trim, rot=(R(90), 0, 0), verts=10)
        for y in (-1.0, -0.5, 0.0, 0.5, 1.0):
            cyl(0.014, 0.12, (X(side * (W / 2 - 0.03)), y, rail - 0.06), trim, verts=8)
    cyl(0.02, W - 0.06, (X(0), front + 0.03, rail), trim, rot=(0, R(90), 0), verts=10)
    seat = mat("Seat", "f3e2c4", 0.6)
    for y in (front + 0.42, front + 0.86, front + 1.30, front + 1.74):
        for side in (-1, 1):
            rbox((0.36, 0.28, 0.08), (X(side * 0.235), y, top + 0.08), seat, r=0.03)
            rbox((0.36, 0.08, 0.28), (X(side * 0.235), y + 0.13, top + 0.2), seat, r=0.035)
    return (X(0), front - 0.026, 0.89)  # the sign's face, where the game writes the level


# ------------------------------------------------------------------ depot vehicles

# Footprints from Core (VehicleDef Width x Length) and proportions carried over from the old code-built ToyVehicle.
# z values are along the vehicle's forward axis (Unity +Z = Blender -Y).
TOYS = {
    "Coupe": dict(w=0.88, l=1.35, clear=0.12, body=0.31, cab_len=0.80, cab_z=-0.08, cab_h=0.27, tx=0.84, tz=0.70, shift=-0.05,
                  wheel=0.17, axles=(0.42, -0.42), pillars=0),
    "BigCar": dict(w=0.92, l=1.62, clear=0.14, body=0.34, cab_len=1.10, cab_z=-0.12, cab_h=0.30, tx=0.90, tz=0.82, shift=-0.04,
                   wheel=0.20, axles=(0.52, -0.52), pillars=1),
    "Minivan": dict(w=0.95, l=1.90, clear=0.13, body=0.34, cab_len=1.46, cab_z=-0.14, cab_h=0.40, tx=0.92, tz=0.88, shift=-0.07,
                    wheel=0.19, axles=(0.62, -0.62), pillars=2),
    "Bus": dict(w=1.05, l=2.45, clear=0.14, body=0.40, cab_len=2.28, cab_z=-0.02, cab_h=0.42, tx=0.97, tz=0.97, shift=-0.01,
                wheel=0.21, axles=(0.80, -0.74), pillars=5),
}


def toy(kind):
    """Chunky toy vehicle: rounded body, slanted tinted glass, flat roof (the game puts the arrow on it), pillars,
    lights, grille, bumpers, mirrors and wheels with hubcaps."""
    t = TOYS[kind]
    w, l = t["w"], t["l"]
    paint, trim, dark = mat("Paint", "2f7bff", 0.45), mat("Trim", "e4e7ec", 0.35), mat("Tyre", "2b2b33", 0.8)
    tint = mat("Tint", "223048", 0.1)
    body_top = t["clear"] + t["body"]
    rbox((w - 0.02, l, t["body"]), (0, 0, t["clear"] + t["body"] / 2), paint, r=min(0.15, t["body"] * 0.4), seg=3)

    cab_w, cab_y = w - 0.12, -t["cab_z"]
    cab_zc = body_top - 0.045 + t["cab_h"] / 2
    cab_top = cab_zc + t["cab_h"] / 2
    rbox((cab_w, t["cab_len"], t["cab_h"]), (0, cab_y, cab_zc), tint, r=0.07, seg=2, top=(t["tx"], t["tz"], -t["shift"]))
    roof_y = cab_y - t["shift"]
    rbox((cab_w * t["tx"] - 0.02, t["cab_len"] * t["tz"] - 0.01, 0.075), (0, roof_y, cab_top + 0.005), paint, r=0.035)

    if t["pillars"]:
        mid_len = t["cab_len"] * (1 + t["tz"]) / 2
        mid_x = cab_w / 2 * (1 + t["tx"]) / 2
        lean = math.atan2(cab_w / 2 * (1 - t["tx"]), t["cab_h"])
        for i in range(1, t["pillars"] + 1):
            y = cab_y - t["shift"] / 2 - (i / (t["pillars"] + 1) - 0.5) * mid_len
            for side in (-1, 1):
                rbox((0.05, 0.07, t["cab_h"] - 0.06), (side * mid_x, y, cab_zc), paint, r=0.02, seg=2, rot=(0, -side * lean, 0))

    lamp_z = t["clear"] + t["body"] * 0.68
    light, tail = mat("Light", "fff4d2", 0.2, emit=1.5), mat("Tail", "d92b2b", 0.4, emit=0.3)
    for side in (-1, 1):
        rbox((0.17, 0.05, 0.08), (side * (w / 2 - 0.19), -(l / 2 - 0.015), lamp_z), light, r=0.024, seg=2)
        rbox((0.15, 0.05, 0.07), (side * (w / 2 - 0.19), l / 2 - 0.015, lamp_z), tail, r=0.022, seg=2)
        rbox((0.05, 0.07, 0.05), (side * (w / 2 + 0.02), cab_y - t["cab_len"] / 2 + 0.06, body_top + 0.03), dark, r=0.015, seg=2)
    rbox((w * 0.42, 0.03, 0.07), (0, -(l / 2 + 0.005), t["clear"] + t["body"] * 0.38), dark, r=0.02, seg=2)
    for sy in (-1, 1):
        rbox((w - 0.12, 0.07, 0.08), (0, sy * (l / 2 - 0.005), t["clear"] + 0.09), trim, r=0.03, seg=2)
    if kind in ("Bus", "Minivan"):
        rbox((w - 0.008, l - 0.35, 0.045), (0, 0.04, t["clear"] + t["body"] * 0.55), mat("Stripe", "ffffff", 0.4), r=0.015, seg=2)

    r = t["wheel"]
    for axle in t["axles"]:
        for side in (-1, 1):
            x = side * (w / 2 - 0.08)
            cyl(r, 0.15, (x, -axle, r), dark, rot=(0, R(90), 0), bevel=0.03, verts=20)
            cyl(r * 0.52, 0.155, (x, -axle, r), trim, rot=(0, R(90), 0), bevel=0.015, verts=16)


# ------------------------------------------------------------------ Home street


def tiles(x0, x1, y0, y1, size, a, b, z):
    """Pavement of square tiles in two tones on a grout slab."""
    nx, ny = max(1, round((x1 - x0) / size)), max(1, round((y1 - y0) / size))
    sx, sy = (x1 - x0) / nx, (y1 - y0) / ny
    rbox((x1 - x0, y1 - y0, z), ((x0 + x1) / 2, (y0 + y1) / 2, z / 2), mat("Grout", "e0c597", 0.9), r=0)
    for i in range(nx):
        for j in range(ny):
            rbox((sx - 0.04, sy - 0.04, 0.02), (x0 + (i + 0.5) * sx, y0 + (j + 0.5) * sy, z + 0.005), a if (i + j) % 2 == 0 else b, r=0.012, seg=1)


def palm(x, y, h=2.6):
    bark, leaf = mat("Bark", "b48659", 0.8), mat("Leaf", "3fbf6a", 0.5)
    lean = 0.12
    for i in range(6):
        t = i / 6
        cyl(0.12 - 0.03 * t, h / 6 + 0.02, (x + lean * t * t * 3, y, (i + 0.5) * h / 6), bark, verts=10, bevel=0.02)
    tx, tz = x + lean * 3, h
    for i in range(8):
        a = R(i * 45 + 10)
        d = Vector((math.cos(a), math.sin(a), 0))
        ball(0.75, (tx + d.x * 0.62, y + d.y * 0.62, tz - 0.12), leaf, scale=(1, 0.24, 0.05), rot=(0, R(22), a), seg=12)
    for i in range(3):
        a = R(i * 120)
        ball(0.08, (tx + math.cos(a) * 0.1, y + math.sin(a) * 0.1, tz - 0.12), mat("Coconut", "7a5230", 0.6), seg=10)


def windows(x, y0, y1, z0, z1, cols, rows, glass, frame):
    """A grid of framed windows on a wall facing -X at x."""
    for i in range(cols):
        for j in range(rows):
            y = y0 + (i + 0.5) * (y1 - y0) / cols
            z = z0 + (j + 0.5) * (z1 - z0) / rows
            rbox((0.06, 0.62, 0.62), (x - 0.02, y, z), frame, r=0.03, seg=2)
            rbox((0.06, 0.5, 0.5), (x - 0.045, y, z), glass, r=0.02, seg=1)


def building(x0, y0, y1, height, wall, roof_trim):
    """Block on the road's right: its road-facing wall at x0, running back from y0 to y1."""
    rbox((4.0, y1 - y0, height), (x0 + 2.0, (y0 + y1) / 2, height / 2), wall, r=0.05, seg=2)
    rbox((4.2, y1 - y0 + 0.2, 0.18), (x0 + 2.0, (y0 + y1) / 2, height + 0.06), roof_trim, r=0.05, seg=2)


def awning(x, y0, y1, z, a, b):
    """Striped awning sloping out over the pavement from a wall at x."""
    n = max(2, round((y1 - y0) / 0.3))
    step = (y1 - y0) / n
    for i in range(n):
        rbox((0.75, step + 0.002, 0.04), (x - 0.33, y0 + (i + 0.5) * step, z), a if i % 2 == 0 else b, r=0.01, seg=1, rot=(0, R(-18), 0))
    for i in range(n):
        ball(step * 0.5, (x - 0.7, y0 + (i + 0.5) * step, z - 0.13), a if i % 2 == 0 else b, scale=(0.25, 1, 0.5), seg=10)


def street():
    """Everything static on the Home screen except the sea surface: road, pavements, bus stop, shops, palms, promenade,
    and the view beyond (boat, lighthouse, headland, clouds)."""
    sand = mat("Sand", "f4d9a0", 0.9)
    rbox((60, 40, 0.1), (0, 0, -0.05), sand, r=0)
    # Road from the camera up to the promenade, white edge lines.
    road_x0, road_x1, road_y0, road_y1 = -0.68, 0.95, -9.0, 10.0
    rbox((road_x1 - road_x0, road_y1 - road_y0, 0.02), ((road_x0 + road_x1) / 2, (road_y0 + road_y1) / 2, 0.01), mat("Road", "6b7080", 0.85), r=0)
    line = mat("Line", "ffffff", 0.5)
    for x in (road_x0 + 0.16, road_x1 - 0.16):
        rbox((0.06, road_y1 - road_y0, 0.006), (x, (road_y0 + road_y1) / 2, 0.022), line, r=0)
    kerb = mat("Kerb", "f2f2f2", 0.6)
    for x in (road_x0 - 0.06, road_x1 + 0.06):
        rbox((0.12, road_y1 - road_y0, 0.1), (x, (road_y0 + road_y1) / 2, 0.05), kerb, r=0.02, seg=1)
    tile_a, tile_b = mat("Tile", "f7dfb7", 0.85), mat("TileB", "f0cf9c", 0.85)
    tiles(-4.4, road_x0 - 0.12, road_y0, road_y1, 0.62, tile_a, tile_b, 0.08)
    tiles(road_x1 + 0.12, 2.4, road_y0, road_y1, 0.62, tile_a, tile_b, 0.08)

    # Promenade across the end of the road, a white railing, a sea wall dropping to the water.
    tiles(-12, 12, road_y1, road_y1 + 1.6, 0.62, tile_a, tile_b, 0.08)
    white = mat("Rail", "ffffff", 0.4)
    ry = road_y1 + 1.45
    for i in range(41):
        cyl(0.035, 0.5, (-12 + i * 0.6, ry, 0.33), white, verts=8)
    for z in (0.36, 0.56):
        cyl(0.03, 24, (0, ry, z), white, rot=(0, R(90), 0), verts=8)
    rbox((60, 0.3, 0.7), (0, ry + 0.25, -0.27), mat("SeaWall", "d9cdb5", 0.9), r=0)

    # Bus stop behind the queue: blue roof on two posts, glass back, a bench, and the stop sign at the front of the queue.
    blue, post = mat("StopBlue", "3d8bff", 0.4), mat("Post", "dfe4ea", 0.4)
    sx, sy0, sy1 = -2.1, 0.6, 2.8
    rbox((1.1, sy1 - sy0 + 0.3, 0.1), (sx + 0.1, (sy0 + sy1) / 2, 1.45), blue, r=0.04)
    for y in (sy0, sy1):
        cyl(0.04, 1.4, (sx - 0.3, y, 0.78), post, verts=10)
        cyl(0.04, 1.4, (sx + 0.45, y, 0.78), post, verts=10)
    rbox((0.04, sy1 - sy0, 1.0), (sx + 0.47, (sy0 + sy1) / 2, 0.9), mat("Window", "9fd3ec", 0.1), r=0.01)
    rbox((0.36, sy1 - sy0 - 0.3, 0.06), (sx + 0.25, (sy0 + sy1) / 2, 0.45), mat("Wood", "c9894f", 0.7), r=0.02)
    rbox((0.06, sy1 - sy0 - 0.3, 0.3), (sx + 0.42, (sy0 + sy1) / 2, 0.62), mat("Wood", "c9894f", 0.7), r=0.02)
    cyl(0.035, 1.6, (-1.45, 3.2, 0.88), post, verts=10)
    cyl(0.2, 0.04, (-1.45, 3.2, 1.75), blue, rot=(R(90), 0, 0), verts=24, bevel=0.01)
    cyl(0.14, 0.05, (-1.45, 3.2, 1.75), mat("Sign", "ffffff", 0.4), rot=(R(90), 0, 0), verts=24)

    # Shops along the right: pink ice-cream parlour, yellow cafe, mint block; awnings over the pavement.
    frame, glass = mat("Frame", "ffffff", 0.5), mat("Window", "9fd3ec", 0.1)
    bx = 2.4
    building(bx, -6.5, -0.6, 2.8, mat("WallPink", "ff9fb3", 0.7), frame)
    rbox((0.06, 3.4, 1.0), (bx - 0.03, -3.8, 0.75), glass, r=0.02)
    rbox((0.07, 0.6, 1.05), (bx - 0.035, -1.5, 0.6), mat("Door", "8a5a3c", 0.6), r=0.02)
    awning(bx, -5.6, -1.0, 1.55, mat("AwningRed", "ef5a5a", 0.6), frame)
    windows(bx, -6.0, -1.0, 1.9, 2.6, 3, 1, glass, frame)
    cone_m = mat("Cone", "e8b16a", 0.7)
    cyl(0.01, 0.9, (bx + 0.6, -3.5, 3.4), cone_m, top=0.32, verts=16)
    ball(0.33, (bx + 0.6, -3.5, 3.95), mat("Scoop", "ff8fb1", 0.5))
    ball(0.25, (bx + 0.6, -3.5, 4.35), mat("ScoopB", "fff3e0", 0.5))

    building(bx, -0.2, 5.0, 3.8, mat("WallYellow", "ffd36b", 0.7), frame)
    windows(bx, 0.0, 4.8, 1.9, 3.6, 4, 2, glass, frame)
    rbox((0.06, 4.2, 0.95), (bx - 0.03, 2.4, 0.7), glass, r=0.02)
    awning(bx, 0.4, 4.4, 1.45, mat("AwningGreen", "2bb673", 0.6), frame)

    building(bx, 5.4, 10.4, 3.2, mat("WallMint", "8fe3c6", 0.7), frame)
    windows(bx, 5.6, 10.2, 0.4, 3.0, 4, 2, glass, frame)

    palm(-2.9, 4.2)
    palm(-3.0, 7.6, 2.9)

    # The view beyond: a sailing boat, a lighthouse on its rocks, a green headland with cottages, clouds.
    hull, sail = mat("Hull", "ffffff", 0.4), mat("Sail", "fffaf0", 0.6)
    rbox((1.4, 0.45, 0.3), (-3.6, 24, -0.15), hull, r=0.14)
    cyl(0.03, 2.2, (-3.6, 24, 1.0), mat("Post", "dfe4ea", 0.4), verts=8)
    bpy.ops.mesh.primitive_cone_add(vertices=3, radius1=1, depth=0.03, location=(-3.25, 24, 1.05), rotation=(R(90), 0, 0))
    o = bpy.context.active_object
    o.scale = (0.55, 1.0, 1.0)
    finish(o, sail, smooth=0)

    rock = mat("Rock", "a8aab0", 0.9)
    for dx, dy, r_ in ((0, 0, 1.4), (1.2, 0.6, 1.0), (-1.0, 0.4, 0.9)):
        ball(r_, (8.5 + dx, 34 + dy, -0.4), rock, scale=(1, 1, 0.55), seg=12)
    red = mat("LightRed", "e74c3c", 0.5)
    for i in range(4):
        cyl(0.55 - 0.05 * i, 0.75, (8.5, 34, 0.55 + i * 0.75), red if i % 2 == 0 else mat("Rail", "ffffff", 0.4),
            top=0.5 - 0.05 * i, verts=20)
    cyl(0.42, 0.1, (8.5, 34, 3.55), mat("Tyre", "2b2b33", 0.8), verts=20)
    cyl(0.3, 0.45, (8.5, 34, 3.82), mat("Light", "fff4d2", 0.2, emit=1.5), verts=16)
    ball(0.33, (8.5, 34, 4.05), red, scale=(1, 1, 0.8), seg=16)

    hill = mat("Hill", "7cc96b", 0.9)
    for dx, dy, r_ in ((0, 0, 9), (8, 4, 7), (-8, 3, 7.5)):
        ball(r_, (-26 + dx, 52 + dy, -3.0), hill, scale=(1, 0.7, 0.55), seg=20)
    for i, (hx, hy, hz) in enumerate(((-24, 44, 1.2), (-21, 45, 1.6), (-27, 46, 1.9))):
        rbox((1.1, 0.9, 0.8), (hx, hy, hz), mat("Cottage", "ffffff", 0.6), r=0.05, seg=1)
        rbox((0.9, 1.0, 0.9), (hx, hy, hz + 0.45), mat("Roof", "e8705a", 0.6), r=0.04, seg=1, rot=(0, R(45), 0))

    cloud = mat("Cloud", "ffffff", 1.0)
    for cx, cy, cz, s in ((-14, 60, 14, 2.2), (2, 66, 17, 2.8), (16, 58, 13, 2.0), (-4, 55, 22, 1.6)):
        for dx, dz, r_ in ((0, 0, 1.0), (1.1, -0.2, 0.75), (-1.1, -0.25, 0.7), (0.5, 0.45, 0.65), (-0.5, 0.35, 0.6)):
            ball(r_ * s, (cx + dx * s, cy, cz + dz * s), cloud, scale=(1, 0.6, 0.8), seg=14)


def sea():
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 50, -0.35))
    o = bpy.context.active_object
    o.scale = (140, 76, 1)
    finish(o, mat("Sea", "1fb6d0", 0.2), smooth=0)
    return join("Sea")


def home_stage():
    """The Home screen's objects: Scenery, Sea, Bus, Queue0..9 (passengers to recolour), SignAnchor, CameraPos, CameraLook."""
    objs = []
    street()
    objs.append(join("Scenery"))
    objs.append(sea())
    sign = tour_bus(0.0)
    objs.append(join("Bus"))
    chibi()
    proto = join("Chibi")
    for i in range(10):
        q = proto.copy()  # linked: one mesh, ten passengers
        bpy.context.scene.collection.objects.link(q)
        q.name = "Queue%d" % i
        q.scale = (0.55,) * 3
        q.location = (-1.0 + (0.07 if i % 2 else 0), -1.45 + i * 0.46, 0.08)
        q.rotation_euler = (0, 0, R(18 + (i % 3) * 6))  # turned a little toward the bus
        objs.append(q)
    bpy.data.objects.remove(proto)
    objs.append(empty("SignAnchor", sign))
    objs.append(empty("CameraPos", CAM_POS))
    objs.append(empty("CameraLook", CAM_LOOK))
    return objs


# ------------------------------------------------------------------ export & preview


def export(objs, name):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH" and o.data.users == 1)
    print("exported %s: ~%d triangles (linked copies counted once)" % (name, tris))


def write_materials():
    with open(os.path.join(OUT, "materials.txt"), "w") as f:
        f.write("# role colour roughness alpha, written by Tools/Blender/models.py\n")
        for name, (_, hexstr, rough, alpha) in sorted(MATS.items()):
            f.write("%s %s %.2f %.2f\n" % (name, hexstr, rough, alpha))


def preview(stage, toys, out_dir):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (*lin("8fd9ee"), 1)  # the game camera's sky
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
    # The Home sun (SliceHome.SunRotation, Euler 44, 30): from behind the camera's left, so faces are lit.
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.4
    sun.rotation_euler = Vector((0.36, 0.623, -0.695)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)

    for o in stage:  # preview colours for the queue
        if o.name.startswith("Queue"):
            i = int(o.name[5:])
            o.data = o.data.copy()
            for k, m in enumerate(o.data.materials):
                if m.name in ("Shirt", "Cap"):
                    o.data.materials[k] = mat(m.name + str(i), PALETTE[QUEUE[i]], MATS[m.name][2])

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.sensor_fit, cam.data.sensor_width = "HORIZONTAL", 36

    def shoot(pos, target, lens, size, path):
        cam.location = pos
        cam.rotation_euler = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        scene.render.resolution_x, scene.render.resolution_y = size
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)

    shoot(CAM_POS, CAM_LOOK, LENS, (720, 1600), os.path.join(out_dir, "home.png"))

    # Depot vehicles in the game's colours, from the level camera's 60 degree pitch, far from the street.
    for i, o in enumerate(toys):
        o.location = (200 + (i - 1.5) * 1.6, 0, 0)
        o.data = o.data.copy()
        for k, m in enumerate(o.data.materials):
            if m.name == "Paint":
                o.data.materials[k] = mat("Paint" + str(i), PALETTE[(i * 3) % 8], 0.45)
    rbox((12, 8, 0.02), (200, 0, -0.01), mat("Depot", "aab1c2", 0.9), r=0)
    shoot((200, -7.5, 9.5), (200, 0.3, 0.3), 70, (1000, 560), os.path.join(out_dir, "vehicles.png"))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    stage = home_stage()
    toys = []
    for kind in TOYS:
        toy(kind)
        toys.append(join("Toy" + kind))
    if "--no-export" not in args:
        export(stage, "HomeStage")
        for o in toys:
            export([o], o.name)
        write_materials()
    if "--render" in args:
        preview(stage, toys, args[args.index("--render") + 1])


main()
