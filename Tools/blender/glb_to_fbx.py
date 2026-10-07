"""Convert a glTF/GLB character to FBX with baked animations. Removes stray 'Icosphere' meshes.
Usage: blender -b --python Tools/blender/glb_to_fbx.py -- <in.glb> <out.fbx>"""
import bpy, sys

src, dst = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for o in [o for o in bpy.data.objects if o.name.startswith('Icosphere')]:
    bpy.data.objects.remove(o, do_unlink=True)
print('ACTIONS', [a.name for a in bpy.data.actions])
bpy.ops.export_scene.fbx(filepath=dst, apply_scale_options='FBX_SCALE_ALL', bake_anim=True,
                         bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, add_leaf_bones=False,
                         axis_forward='-Z', axis_up='Y', path_mode='COPY', embed_textures=True)
print('FBX_OK', dst)
