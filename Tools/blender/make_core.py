"""Build the energy core pickup (cyan crystal in a 4-bar metal frame) and render a HUD icon.
Usage: blender -b --python Tools/blender/make_core.py -- <out.fbx> <icon.png>"""
import bpy, math, sys

out_fbx, out_png = sys.argv[sys.argv.index('--') + 1:]
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

glow = material('CoreGlow', (0.18, 0.90, 0.90), emit=3.0)
frame = material('CoreFrame', (0.45, 0.47, 0.50))

parts = []
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=0.5)
crystal = bpy.context.active_object
crystal.scale = (0.45, 0.45, 0.8)
crystal.data.materials.append(glow)
parts.append(crystal)
for i in range(4):
    a = math.radians(45 + 90 * i)
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.04, depth=0.9,
                                        location=(0.32 * math.cos(a), 0.32 * math.sin(a), 0))
    bar = bpy.context.active_object
    bar.data.materials.append(frame)
    parts.append(bar)
for z in (-0.45, 0.45):
    bpy.ops.mesh.primitive_torus_add(major_radius=0.32, minor_radius=0.04, major_segments=12, minor_segments=4,
                                     location=(0, 0, z))
    ring = bpy.context.active_object
    ring.data.materials.append(frame)
    parts.append(ring)

bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = crystal
bpy.ops.object.join()
core = bpy.context.active_object
core.name = core.data.name = 'EnergyCore'
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
assert {m.name for m in core.data.materials} == {'CoreGlow', 'CoreFrame'}
bpy.ops.export_scene.fbx(filepath=out_fbx, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)

# HUD icon: orthographic front view, transparent background.
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'FLAT'
scene.display.shading.color_type = 'MATERIAL'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = scene.render.resolution_y = 256
bpy.ops.object.camera_add(location=(0, -5, 0), rotation=(math.radians(90), 0, 0))
cam = bpy.context.active_object
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 1.9
scene.camera = cam
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
print('CORE_OK', out_fbx, out_png)
