"""Build the energy core pickup (frameless cyan crystal with small shards) and render a HUD icon.
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
shard_mat = material('CoreShard', (0.55, 0.98, 1.0), emit=4.0)

# Frameless crystal: a tall faceted bipyramid with three small shards leaning out of its base (no cage).
parts = []
bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.32, radius2=0.0, depth=0.6, location=(0, 0, 0.3))
top = bpy.context.active_object
bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.32, radius2=0.0, depth=0.4, location=(0, 0, -0.2), rotation=(math.pi, 0, 0))
bottom = bpy.context.active_object
crystal = top
for p in (top, bottom):
    p.data.materials.append(glow)
    parts.append(p)
for i in range(3):
    a = math.radians(120 * i + 20)
    bpy.ops.mesh.primitive_cone_add(vertices=5, radius1=0.09, radius2=0.0, depth=0.32,
                                    location=(0.3 * math.cos(a), 0.3 * math.sin(a), -0.18),
                                    rotation=(math.radians(25) * math.sin(a), -math.radians(25) * math.cos(a), 0))
    shard = bpy.context.active_object
    shard.data.materials.append(shard_mat)
    parts.append(shard)

bpy.ops.object.select_all(action='DESELECT')
for p in parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = crystal
bpy.ops.object.join()
core = bpy.context.active_object
core.name = core.data.name = 'EnergyCore'
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
assert {m.name for m in core.data.materials} == {'CoreGlow', 'CoreShard'}, [m.name for m in core.data.materials]
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
cam.data.ortho_scale = 1.4
scene.camera = cam
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
print('CORE_OK', out_fbx, out_png)
