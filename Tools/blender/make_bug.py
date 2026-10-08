"""Build the creep: a low four-legged scout bug robot with an orange-red eye (spec §6.1), about 0.85 m long.
Legs are separate objects with their origin at the hip so the game can swing them (BugWalk).
Usage: blender -b --python Tools/blender/make_bug.py -- <out.fbx>"""
import bpy, math, sys

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

shell = material('BugShell', (0.20, 0.22, 0.26))
joint = material('BugJoint', (0.55, 0.57, 0.60))
eye = material('BugEye', (1.0, 0.12, 0.03), emit=1.5)

def finish(obj, name, mat):
    obj.name = obj.data.name = name
    obj.data.materials.append(mat)
    bpy.ops.object.shade_flat()
    return obj

# Blender front is -Y. Body: flattened octagonal hull, low to the ground.
bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.3, depth=0.18, location=(0, 0.02, 0.32))
body = finish(bpy.context.active_object, 'Body', shell)
body.scale = (1.0, 1.25, 1.0)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# Head wedge at the front with the eye.
bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.17, radius2=0.10, depth=0.22, location=(0, -0.38, 0.34),
                                rotation=(math.radians(90), 0, 0))
finish(bpy.context.active_object, 'Head', shell)
bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=0.075, location=(0, -0.49, 0.36))
finish(bpy.context.active_object, 'Eye', eye)

# Legs: thigh going out and up, shin down to the ground; origin at the hip.
for name, sx, sy in (('Leg_FL', -1, -1), ('Leg_FR', 1, -1), ('Leg_BL', -1, 1), ('Leg_BR', 1, 1)):
    hip = (sx * 0.24, sy * 0.22, 0.32)
    knee = (sx * 0.46, sy * 0.30, 0.42)
    foot = (sx * 0.52, sy * 0.34, 0.0)
    parts = []
    for a, b, r in ((hip, knee, 0.035), (knee, foot, 0.03)):
        mid = tuple((a[i] + b[i]) / 2 for i in range(3))
        d = [b[i] - a[i] for i in range(3)]
        length = math.sqrt(sum(c * c for c in d))
        bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=r, depth=length, location=mid)
        seg = bpy.context.active_object
        seg.rotation_mode = 'QUATERNION'
        from mathutils import Vector
        seg.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(Vector(d))
        seg.data.materials.append(joint)
        parts.append(seg)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.05, location=knee)
    k = bpy.context.active_object
    k.data.materials.append(shell)
    parts.append(k)
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    leg = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.context.scene.cursor.location = hip
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    leg.name = leg.data.name = name
    bpy.ops.object.shade_flat()

names = sorted(o.name for o in bpy.data.objects)
assert names == ['Body', 'Eye', 'Head', 'Leg_BL', 'Leg_BR', 'Leg_FL', 'Leg_FR'], names
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out_fbx, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)
print('BUG_OK', out_fbx)
