"""Run with Blender 2.93+: blender -b --python Tools/Hawks/create_hawk.py."""
import bpy, math, os, json
from mathutils import Vector
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Assets/MantaFlight/Hawks/Models')
os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def material(name, color):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
    m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.72
    return m
earth=material('Hawk Umber',(.24,.115,.052)); cover=material('Hawk Chestnut',(.39,.22,.10))
cream=material('Hawk Cream',(.76,.65,.43)); dark=material('Hawk Flight Feathers',(.10,.067,.044))
rust=material('Hawk Tail Rufous',(.47,.205,.09)); gold=material('Hawk Gold',(.82,.49,.08)); black=material('Hawk Obsidian',(.018,.012,.008))
parts=[]
def bind(obj, mat, bone):
    obj.data.materials.append(mat); g=obj.vertex_groups.new(name=bone); g.add(list(range(len(obj.data.vertices))),1,'REPLACE'); parts.append(obj)
    return obj
def ellipsoid(name, pos, scale, mat, bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, location=pos)
    ob=bpy.context.object; ob.name=name; ob.scale=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for p in ob.data.polygons: p.use_smooth=True
    return bind(ob,mat,bone)
def feather(name, base, tip, width, mat, bone):
    a,b=Vector(base),Vector(tip); along=b-a; side=Vector((-along.y,along.x,0)).normalized()*width
    verts=[a-side*.25,a+side*.25,a+along*.38+side*.55,a+along*.82+side*.36,b,a+along*.82-side*.36,a+along*.38-side*.55,a+along*.48+Vector((0,0,.018))]
    faces=[(7,i,(i+1)%7) for i in range(7)]+[(6,5,4,3,2,1,0)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(verts,[],faces); mesh.update()
    ob=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(ob); return bind(ob,mat,bone)
# Blender forward is -Y, up Z; FBX converts to Unity +Z, up Y.
ellipsoid('Torso',(0,0,0),(.16,.34,.155),earth,'Body')
ellipsoid('Pale breast',(0,-.065,-.058),(.135,.255,.12),cream,'Body')
ellipsoid('Shoulders',(0,-.12,.045),(.19,.20,.115),cover,'Body')
ellipsoid('Head',(0,-.34,.085),(.113,.145,.112),cover,'Head')
ellipsoid('Throat',(0,-.35,.012),(.085,.115,.075),cream,'Head')
ellipsoid('Cere',(0,-.464,.071),(.047,.067,.035),gold,'Head')
feather('Hooked beak',(0,-.48,.08),(0,-.558,.015),.072,black,'Head')
for s in [-1,1]:
    ellipsoid('Amber eye',(s*.101,-.386,.107),(.018,.025,.022),gold,'Head')
    ellipsoid('Pupil',(s*.116,-.391,.11),(.007,.013,.014),black,'Head')
    ellipsoid('Brow',(s*.102,-.388,.134),(.023,.046,.011),earth,'Head')
    ellipsoid('Tucked foot',(s*.07,.14,-.134),(.03,.069,.023),gold,'Body')
    # Distinct broad inner wing and fingered outer primaries.
    side='L' if s<0 else 'R'
    ellipsoid('Wing shoulder '+side,(s*.31,-.03,.035),(.29,.15,.047),earth,'Wing.'+side)
    for i in range(9):
        x=.17+i*.049
        feather('Secondary '+side+str(i),(s*x,-.105,.036),(s*(x+.045),.26-i*.006,.018),.086,cover if i%2 else earth,'Wing.'+side)
        feather('Covert '+side+str(i),(s*x,-.12,.063),(s*(x+.045),.075,.052),.072,cover,'Wing.'+side)
    for i in range(8):
        x=.54+i*.044
        feather('Primary '+side+str(i),(s*x,-.078,.033),(s*(.76+i*.052),.29-i*.058,.012),.081,dark if i>3 else earth,'Tip.'+side)
        feather('Primary cover '+side+str(i),(s*x,-.082,.053),(s*(x+.09),.065-i*.013,.04),.058,cover,'Tip.'+side)
    for i in range(7):
        ellipsoid('Breast streak',(s*(.031+(i%2)*.037),-.23+(i//2)*.095,-.153),(.012,.03,.004),earth,'Body')
for i in range(9):
    x=(i-4)*.029
    feather('Tail '+str(i),(x*.6,.23,0),(x*1.5,.62-abs(i-4)*.012,-.018),.071,rust,'Tail')
    feather('Tail band '+str(i),(x*1.35,.52,.005),(x*1.44,.553,.002),.064,dark,'Tail')
bpy.ops.object.select_all(action='DESELECT')
for ob in parts: ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); mesh=bpy.context.object; mesh.name='CommonHawk_Mesh'
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.armature_add(); rig=bpy.context.object; rig.name='CommonHawk_Rig'; rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT'); eb=rig.data.edit_bones; eb.remove(eb[0])
def bone(name,head,tail,parent=None):
    b=eb.new(name); b.head=head; b.tail=tail
    if parent: b.parent=eb[parent]
bone('Root',(0,0,-.25),(0,0,-.1))
bone('Body',(0,.12,0),(0,-.2,0),'Root'); bone('Head',(0,-.22,.05),(0,-.47,.08),'Body'); bone('Tail',(0,.20,0),(0,.58,0),'Body')
for s in [-1,1]:
    side='L' if s<0 else 'R'
    bone('Wing.'+side,(s*.12,-.04,.03),(s*.56,-.04,.03),'Body')
    bone('Tip.'+side,(s*.56,-.04,.03),(s*1.08,-.08,.03),'Wing.'+side)
bpy.ops.object.mode_set(mode='OBJECT')
mesh.parent=rig; mod=mesh.modifiers.new('Hawk skin','ARMATURE'); mod.object=rig
scene=bpy.context.scene; scene.render.fps=30; scene.frame_start=1; scene.frame_end=41
rig.animation_data_create()
for action_name in ['Hawk_Flight','Hawk_Glide']:
    action=bpy.data.actions.new(action_name); rig.animation_data.action=action
    for f in range(1,42,2):
        phase=(f-1)/40*math.tau
        for p in rig.pose.bones: p.rotation_mode='XYZ'; p.rotation_euler=(0,0,0)
        for s in [-1,1]:
            side='L' if s<0 else 'R'
            # Local X of these horizontal bones corresponds to world Z;
            # local Z bends wings vertically about the body-forward axis.
            rig.pose.bones['Wing.'+side].rotation_euler.z=s*((.50*math.cos(phase)) if action_name=='Hawk_Flight' else .10+.025*math.sin(phase))
            rig.pose.bones['Tip.'+side].rotation_euler.z=s*((.24*math.cos(phase-.7)) if action_name=='Hawk_Flight' else .09)
        rig.pose.bones['Tail'].rotation_euler.x=.045*math.sin(phase)
        rig.pose.bones['Head'].rotation_euler.x=.025*math.sin(phase)
        for p in rig.pose.bones: p.keyframe_insert('rotation_euler',frame=f,group=p.name)
    for fc in action.fcurves:
        for k in fc.keyframe_points: k.interpolation='BEZIER'
    action.use_fake_user=True
rig.animation_data.action=bpy.data.actions['Hawk_Flight']; scene.frame_set(11)
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'CommonHawk.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
# A studio camera is retained in the editable source, excluded from FBX.
world=scene.world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.17,.22,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.5
def point_at(ob,p): ob.rotation_euler=(Vector(p)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(1.5,-2.5,1.7)); camera=bpy.context.object; point_at(camera,(0,.02,0)); camera.data.type='ORTHO'; camera.data.ortho_scale=2.65; scene.camera=camera
for pos,power,size in [((1,-3,4),450,4),((-3,0,2),300,3),((0,3,3),500,2)]:
    bpy.ops.object.light_add(type='AREA',location=pos); light=bpy.context.object; light.data.energy=power; light.data.size=size; point_at(light,(0,0,0))
scene.render.engine='BLENDER_EEVEE'; scene.eevee.use_gtao=True; scene.eevee.gtao_distance=3
scene.render.resolution_x=1200; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'; scene.view_settings.look='Medium High Contrast'
source=os.path.join(ROOT,'ArtSource/Hawks'); os.makedirs(source,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(source,'CommonHawk.blend'))
scene.render.filepath=os.path.join(source,'CommonHawk-preview.png'); bpy.ops.render.render(write_still=True)
print('HAWK_COMPLETE '+json.dumps({'vertices':len(mesh.data.vertices),'polygons':len(mesh.data.polygons),'bones':len(rig.data.bones),'actions':[a.name for a in bpy.data.actions]}))
