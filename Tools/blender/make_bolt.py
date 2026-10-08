"""Build the Overdrive pickup: a yellow lightning bolt (spec §9), about 0.7 m tall, and its HUD icon.
Usage: blender -b --python Tools/blender/make_bolt.py -- <out.fbx> <icon.png>"""
import bpy, bmesh, math, sys

out_fbx, out_png = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)

m = bpy.data.materials.new('BoltGlow')
m.diffuse_color = (1.0, 0.82, 0.1, 1)
m.use_nodes = True
bsdf = m.node_tree.nodes['Principled BSDF']
bsdf.inputs['Base Color'].default_value = (1.0, 0.82, 0.1, 1)
bsdf.inputs['Emission Color'].default_value = (1.0, 0.75, 0.05, 1)
bsdf.inputs['Emission Strength'].default_value = 3.0

# Zigzag bolt outline in the XZ plane (front view), extruded along Y.
outline = [(0.10, 0.35), (-0.16, 0.0), (0.0, 0.0), (-0.10, -0.35), (0.18, 0.06), (0.02, 0.06), (0.16, 0.35)]
mesh = bpy.data.meshes.new('Overdrive')
bm = bmesh.new()
verts = [bm.verts.new((x, 0.0, z)) for x, z in outline]
face = bm.faces.new(verts)
ext = bmesh.ops.extrude_face_region(bm, geom=[face])
for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
    v.co.y += 0.12
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
bolt = bpy.data.objects.new('Overdrive', mesh)
bpy.context.collection.objects.link(bolt)
mesh.materials.append(m)
bpy.context.view_layer.objects.active = bolt
bolt.select_set(True)
bpy.ops.object.shade_flat()
bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
bpy.ops.export_scene.fbx(filepath=out_fbx, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True)

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
cam.data.ortho_scale = 0.85
scene.camera = cam
scene.render.filepath = out_png
bpy.ops.render.render(write_still=True)
print('BOLT_OK', out_fbx, out_png)
