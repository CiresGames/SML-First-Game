"""Blender 2.93+: build editable Rider wingsuit and export UV-parameterized panels."""
import bpy, os
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/MantaFlight/Rider/Models')
SOURCE=os.path.join(ROOT,'ArtSource/Rider')
os.makedirs(OUT,exist_ok=True); os.makedirs(SOURCE,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT,'Assets/Starter Assets/Runtime/ThirdPersonController/Character/Models/Armature.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
def anchor(name): return rig.matrix_world @ rig.data.bones[name].head_local
def mat(name,color):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes['Principled BSDF'];p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.55
    return m
fabric=mat('Wingsuit Petrol',(.025,.17,.20));seam=mat('Wingsuit Amber',(.95,.40,.075));edge=mat('Wingsuit Dark Trim',(.012,.025,.035))
body=mat('Rider preview graphite',(.19,.23,.27))
for ob in bpy.context.scene.objects:
    if ob.type=='MESH':
        for slot in ob.material_slots: slot.material=body
panels=[]
def panel(name,a,b,c):
    n=18; verts=[];uv=[];indices={};faces=[]
    normal=(b-a).cross(c-a).normalized()
    for i in range(n+1):
        for j in range(n+1-i):
            u,v=i/n,j/n;w=1-u-v;indices[(i,j)]=len(verts)
            verts.append(a*w+b*u+c*v+normal*(27*u*v*w*.07));uv.append((u,v))
    for i in range(n):
        for j in range(n-i):
            faces.append((indices[i,j],indices[i+1,j],indices[i,j+1]))
            if i+j<n-1: faces.append((indices[i+1,j],indices[i+1,j+1],indices[i,j+1]))
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);panels.append(ob)
    for m in (fabric,seam,edge): mesh.materials.append(m)
    layer=mesh.uv_layers.new(name='MembraneCoordinates')
    for poly in mesh.polygons:
        values=[uv[k] for k in poly.vertices];u=sum(v[0] for v in values)/3;v=sum(v[1] for v in values)/3
        poly.material_index=2 if min(u,v,1-u-v)<.025 else (1 if .76<u+v<.85 or .43<u<.49 else 0)
        poly.use_smooth=True
        for loop in poly.loop_indices: layer.data[loop].uv=uv[mesh.loops[loop].vertex_index]
    return ob
for side in ('Left','Right'):
    panel(side+'Wing',anchor(side+'_UpperArm'),anchor(side+'_Hand'),anchor(side+'_UpperLeg'))
panel('LegWing',anchor('Hips'),anchor('Left_Foot'),anchor('Right_Foot'))
bpy.ops.object.select_all(action='DESELECT')
for ob in panels:ob.select_set(True)
bpy.context.view_layer.objects.active=panels[0]
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'RiderWingsuit.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=False)
scene=bpy.context.scene;scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.06,.08,.11,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.7
center=anchor('Spine')
def point(ob):ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=center+Vector((2,-4,1.7)));cam=bpy.context.object;point(cam);cam.data.type='ORTHO';cam.data.ortho_scale=2.6;scene.camera=cam
for offset,power in [((2,-3,4),450),((-3,-1,2),350),((0,3,2),500)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(offset));light=bpy.context.object;light.data.energy=power;light.data.size=3;point(light)
scene.render.engine='BLENDER_EEVEE';scene.eevee.use_gtao=True
scene.render.resolution_x=1100;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
for material in list(bpy.data.materials):
    if material.users==0:bpy.data.materials.remove(material)
for image in list(bpy.data.images):
    if image.users==0:bpy.data.images.remove(image)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'RiderWingsuit.blend'),compress=True)
scene.render.filepath=os.path.join(SOURCE,'RiderWingsuit-preview.png');bpy.ops.render.render(write_still=True)
print('WINGSUIT_COMPLETE panels='+str(len(panels)))
