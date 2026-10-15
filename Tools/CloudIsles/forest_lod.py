import bpy,os
root=r'C:\__dev\quentin\SML-First-Game'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.path.join(root,'Assets','Tree 1','Rei_treeLeavesGN.fbx'))
for obj in list(bpy.data.objects):
    if obj.type!='MESH':continue
    if 'trunk' in obj.name.lower():
        bpy.context.view_layer.objects.active=obj
        m=obj.modifiers.new('Distant grove trunk','DECIMATE');m.ratio=.12
        bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(root,'Assets','MantaFlight','Environment','CloudIsles','Models','GroveTree.fbx'),
    use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False)
print('Grove tree exported, leaf cards and imported crown normals retained')
