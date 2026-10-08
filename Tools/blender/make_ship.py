"""Build the Ending spaceship (spec §8): a low-poly lander about 12 m long in the steel-blue launch-pad style, with a
rear ramp door (origin on its hinge), a charging port on the left side, four light pods on the spine and two engine
flames. Door, Port, Pod_1..4 and Flame_L/R are separate objects so the Ending timeline can drive them.
Blender front is -Y (Unity +Z after export).
Usage: blender -b --python Tools/blender/make_ship.py -- <out.fbx>"""
import bpy, bmesh, math, sys

out_fbx = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, rgb, emit=0.0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Emission Color'].default_value = (*rgb, 1)
    bsdf.inputs['Emission Strength'].default_value = emit
    return m

hull_mat = material('ShipHull', (0.42, 0.52, 0.60))
trim_mat = material('ShipTrim', (0.86, 0.48, 0.16))
dark_mat = material('ShipDark', (0.10, 0.12, 0.15))
glass_mat = material('ShipGlass', (0.20, 0.75, 0.85), emit=0.6)
pod_mat = material('ShipPod', (0.25, 0.28, 0.30))
flame_mat = material('ShipFlame', (1.0, 0.55, 0.12), emit=6.0)

def obj(name, mat):
    o = bpy.context.active_object
    o.name = o.data.name = name
    o.data.materials.append(mat)
    bpy.ops.object.shade_flat()
    return o

def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    o.name = o.data.name = name
    return o

parts = []
# Fuselage: octagonal body lying along Y (turned 22.5° about its own axis for a flat floor), nose cone at the front (-Y).
# Open tube (the nose cone closes the front); the rear is a thin bulkhead with a doorway cut right through it
# (1.3 m wide, floor level with the ramp hinge). Cutting a thin plate, not the solid tube, leaves no hidden wall.
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=1.8, depth=8.0, end_fill_type='NOTHING', location=(0, 0.5, 3.0), rotation=(math.radians(90), math.radians(22.5), 0))
parts.append(obj('Body', hull_mat))
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=1.8, depth=0.06, location=(0, 4.47, 3.0), rotation=(math.radians(90), math.radians(22.5), 0))
bulkhead = obj('Bulkhead', hull_mat)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 4.47, 2.83))
cutter = bpy.context.active_object
cutter.scale = (1.3, 1.0, 2.94)
cut = bulkhead.modifiers.new('Doorway', 'BOOLEAN')
cut.operation = 'DIFFERENCE'
cut.object = cutter
bpy.context.view_layer.objects.active = bulkhead
bpy.ops.object.modifier_apply(modifier='Doorway')
bpy.data.objects.remove(cutter, do_unlink=True)
parts.append(bulkhead)
bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=1.8, radius2=0.35, depth=3.2, location=(0, -5.1, 3.0), rotation=(math.radians(90), math.radians(22.5), 0))
parts.append(obj('Nose', hull_mat))
# Wings with orange tips.
for sx in (-1, 1):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(sx * 3.6, 1.2, 2.6))
    w = obj('Wing', hull_mat)
    w.scale = (3.8, 3.0, 0.22)
    w.rotation_euler = (0, sx * math.radians(-8), 0)
    parts.append(w)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(sx * 5.6, 1.4, 2.35))
    t = obj('WingTip', trim_mat)
    t.scale = (0.4, 2.6, 0.5)
    parts.append(t)
    # Landing legs.
    for y in (-2.5, 3.0):
        bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.16, depth=2.2, location=(sx * 1.6, y, 1.1))
        parts.append(obj('Leg', dark_mat))
        bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.45, depth=0.15, location=(sx * 1.6, y, 0.08))
        parts.append(obj('Foot', dark_mat))
hull = join(parts, 'Hull')

# Cockpit glass on top of the nose.
bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=5, radius=1.1, location=(0, -3.4, 4.1))
cockpit = obj('Cockpit', glass_mat)
cockpit.scale = (1.0, 1.6, 0.6)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# Engines at the rear corners with their flames (hidden until take-off in Unity).
for sx, side in ((-1, 'L'), (1, 'R')):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.75, depth=2.6, location=(sx * 2.3, 4.2, 3.0), rotation=(math.radians(90), 0, 0))
    obj('Engine_' + side, trim_mat)
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=0.6, radius2=0.0, depth=2.4, location=(sx * 2.3, 6.7, 3.0), rotation=(math.radians(-90), 0, 0))
    obj('Flame_' + side, flame_mat)

# Dark cabin behind the doorway: a box with inward faces and no rear wall, narrow enough to stay inside the octagon;
# its floor (z 1.36) is level with the hinge.
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0.6, 2.83))
interior = obj('Interior', dark_mat)
interior.scale = (1.3, 7.7, 2.94)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bm = bmesh.new()
bm.from_mesh(interior.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.y > 0.9], context='FACES')
bmesh.ops.reverse_faces(bm, faces=bm.faces)
bm.to_mesh(interior.data)
bm.free()

# Rear ramp door, hinged at its bottom edge (y = 4.55, z = 1.33); closed it covers the whole octagonal rear opening.
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 4.55, 3.005))
door = obj('Door', dark_mat)
door.scale = (3.4, 0.15, 3.35)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bpy.context.scene.cursor.location = (0, 4.55, 1.33)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

# Charging port on the left side and four light pods along the spine.
bpy.ops.mesh.primitive_torus_add(major_radius=0.45, minor_radius=0.12, location=(-1.85, 0.5, 2.6), rotation=(0, math.radians(90), 0))
obj('Port', trim_mat)
for i in range(4):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.32, location=(0, -2.4 + i * 1.9, 4.85))
    obj('Pod_%d' % (i + 1), pod_mat)

names = sorted(o.name for o in bpy.data.objects)
expected = sorted(['Hull', 'Interior', 'Cockpit', 'Engine_L', 'Engine_R', 'Flame_L', 'Flame_R', 'Door', 'Port', 'Pod_1', 'Pod_2', 'Pod_3', 'Pod_4'])
assert names == expected, names
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out_fbx, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)
print('SHIP_OK', out_fbx)
