"""Build the support pickups in the energy-core style (spec §6.4): a faceted crystal with a big emblem through it,
readable from the phone camera without bloom. Shield = blue crystal + shield plate, Heal10 = green crystal + one plus,
Heal20 = brighter green crystal + two plusses. Also renders the HUD shield icon.
Usage: blender -b --python Tools/blender/make_support.py -- <out_dir> <shield_icon.png>"""
import bpy, bmesh, math, os, sys

out_dir, icon_png = sys.argv[sys.argv.index('--') + 1:]

def material(name, rgb, emit):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Emission Color'].default_value = (*rgb, 1)
    bsdf.inputs['Emission Strength'].default_value = emit
    return m

def crystal(mat):
    parts = []
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.30, radius2=0.0, depth=0.55, location=(0, 0, 0.275))
    parts.append(bpy.context.active_object)
    bpy.ops.mesh.primitive_cone_add(vertices=6, radius1=0.30, radius2=0.0, depth=0.40, location=(0, 0, -0.2), rotation=(math.pi, 0, 0))
    parts.append(bpy.context.active_object)
    for p in parts:
        p.data.materials.append(mat)
    return parts

def prism(outline, depth, mat, z=0.0):
    mesh = bpy.data.meshes.new('Emblem')
    bm = bmesh.new()
    verts = [bm.verts.new((x, -depth / 2, zz + z)) for x, zz in outline]
    face = bm.faces.new(verts)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    for v in [e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)]:
        v.co.y += depth
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    obj = bpy.data.objects.new('Emblem', mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return obj

def plus(size, thick, depth, mat, z):
    a = prism([(-size, -thick), (size, -thick), (size, thick), (-size, thick)], depth, mat, z)
    b = prism([(-thick, -size), (thick, -size), (thick, size), (-thick, size)], depth, mat, z)
    return [a, b]

def build(name, crystal_rgb, emblem_rgb, emblem):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    body = material(name + 'Glow', crystal_rgb, 2.0)
    mark = material(name + 'Emblem', emblem_rgb, 1.2)
    parts = crystal(body) + emblem(mark)
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = obj.data.name = name
    bpy.ops.object.shade_flat()
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + '.fbx'), apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True)
    return obj

shield_outline = [(-0.24, 0.22), (0.24, 0.22), (0.24, 0.02), (0.0, -0.26), (-0.24, 0.02)]
build('SupportShield', (0.20, 0.45, 1.0), (0.85, 0.93, 1.0), lambda m: [prism(shield_outline, 0.66, m, 0.05)])
build('SupportHeal10', (0.20, 0.75, 0.30), (0.92, 1.0, 0.92), lambda m: plus(0.22, 0.07, 0.66, m, 0.05))
build('SupportHeal20', (0.35, 1.0, 0.45), (1.0, 1.0, 1.0), lambda m: plus(0.13, 0.045, 0.66, m, 0.25) + plus(0.13, 0.045, 0.66, m, -0.12))

# HUD shield icon: the emblem alone, front view.
bpy.ops.wm.read_factory_settings(use_empty=True)
prism(shield_outline, 0.1, material('Icon', (0.45, 0.7, 1.0), 1.0))
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'FLAT'
scene.display.shading.color_type = 'MATERIAL'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.render.resolution_x = scene.render.resolution_y = 128
bpy.ops.object.camera_add(location=(0, -5, 0), rotation=(math.radians(90), 0, 0))
cam = bpy.context.active_object
cam.data.type = 'ORTHO'
cam.data.ortho_scale = 0.6
scene.camera = cam
scene.render.filepath = icon_png
bpy.ops.render.render(write_still=True)
print('SUPPORT_OK')
