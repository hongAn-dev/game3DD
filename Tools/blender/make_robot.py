"""Build the player robot ball: radius 0.5 sphere, panel seams, cyan equator light, yellow eye, rust spots.
Usage: blender -b --python Tools/blender/make_robot.py -- <out.fbx>"""
import bpy, bmesh, math, random, sys

out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(7)

def material(name, rgb):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    return m

mats = [material('RoboShell', (0.62, 0.64, 0.66)), material('RoboPanel', (0.22, 0.24, 0.27)),
        material('RoboLight', (0.18, 0.90, 0.90)), material('RoboEye', (1.0, 0.77, 0.30)),
        material('RoboRust', (0.45, 0.25, 0.12))]
SHELL, PANEL, LIGHT, EYE, RUST = range(5)

bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=0.5)
obj = bpy.context.active_object
obj.name = obj.data.name = 'RoboBall'
for m in mats:
    obj.data.materials.append(m)

bm = bmesh.new()
bm.from_mesh(obj.data)
for f in bm.faces:
    c = f.calc_center_median()
    lat = math.degrees(math.asin(max(-1, min(1, c.z / 0.5))))
    lon = math.degrees(math.atan2(c.y, c.x))
    if abs(lat) < 6:
        f.material_index = LIGHT
    elif c.y < -0.38 and abs(c.x) < 0.12 and 8 < lat < 30:
        f.material_index = EYE
    elif abs((lon + 180) % 60) < 4 or abs(abs(lat) - 45) < 3:
        f.material_index = PANEL
    elif random.random() < 0.06:
        f.material_index = RUST
    else:
        f.material_index = SHELL
bm.to_mesh(obj.data)
bm.free()
bpy.ops.object.shade_flat()

dims = obj.dimensions
assert all(abs(d - 1.0) < 0.01 for d in dims), dims
used = {p.material_index for p in obj.data.polygons}
assert used == {SHELL, PANEL, LIGHT, EYE, RUST}, used
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)
print('ROBOT_OK', out)
