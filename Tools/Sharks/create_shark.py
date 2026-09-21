"""Blender 2.93+: blender -b --python Tools/Sharks/create_shark.py"""
import bpy, math, os, json
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
OUT=os.path.join(ROOT,'Assets/Wildlife/WhiteShark/Models')
SOURCE=os.path.join(ROOT,'ArtSource/Sharks')
os.makedirs(OUT,exist_ok=True); os.makedirs(SOURCE,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
def mat(name,color,rough=.4):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes['Principled BSDF']; p.inputs['Base Color'].default_value=(*color,1); p.inputs['Roughness'].default_value=rough
    return m
gray=mat('Shark Slate',(.18,.26,.30)); white=mat('Shark Ivory',(.82,.84,.77)); black=mat('Shark Black',(.009,.016,.020),.18); gum=mat('Shark Mouth',(.10,.027,.032)); tooth=mat('Shark Teeth',(.95,.93,.80))
parts=[]
def bind(o,m,bone):
    o.data.materials.append(m)
    if bone:
        g=o.vertex_groups.new(name=bone); g.add(list(range(len(o.data.vertices))),1,'REPLACE')
    parts.append(o); return o
def mesh(name,vs,fs,m,bone):
    d=bpy.data.meshes.new(name); d.from_pydata(vs,[],fs); d.update()
    o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o); return bind(o,m,bone)
def sphere(name,pos,scale,m,bone):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=pos)
    o=bpy.context.object; o.name=name; o.scale=scale; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for p in o.data.polygons: p.use_smooth=True
    return bind(o,m,bone)
# Nose is -Y; a spindle-shaped body with a broad head and narrow caudal peduncle.
rings=[(-2.65,.025,.045,.02),(-2.48,.24,.20,.08),(-2.16,.49,.37,.06),(-1.65,.64,.53,.02),(-.95,.68,.64,0),(-.15,.60,.60,0),(.65,.43,.45,.01),(1.3,.27,.30,.02),(1.9,.13,.17,.03),(2.35,.085,.11,.04)]
vs=[]; fs=[]; n=40
for y,w,h,z in rings:
    for j in range(n):
        a=j*math.tau/n; vs.append((w*math.cos(a),y,z+h*math.sin(a)))
for i in range(len(rings)-1):
    for j in range(n): fs.append((i*n+j,(i+1)*n+j,(i+1)*n+(j+1)%n,i*n+(j+1)%n))
fs.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+j for j in range(n))])
body=mesh('Great white body',vs,fs,gray,None); body.data.materials.append(white)
for p in body.data.polygons:
    p.use_smooth=True
    if sum(body.data.vertices[v].co.z for v in p.vertices)/len(p.vertices)<-.16: p.material_index=1
for name in ['Body','Spine','Tail','Fluke']: body.vertex_groups.new(name=name)
anchors=[(-.8,'Body'),(.55,'Spine'),(1.5,'Tail'),(2.3,'Fluke')]
for v in body.data.vertices:
    y=v.co.y
    if y<=anchors[0][0]: weights=[('Body',1)]
    elif y>=anchors[-1][0]: weights=[('Fluke',1)]
    else:
        for (a,an),(b,bn) in zip(anchors,anchors[1:]):
            if a<=y<=b:
                t=(y-a)/(b-a); weights=[(an,1-t),(bn,t)]; break
    for name,w in weights:
        if w>0: body.vertex_groups[name].add([v.index],w,'REPLACE')
def fin(name,points,thickness,m,bone):
    # A closed tapered wedge; base follows the body, tips stay sharp.
    c=sum((Vector(p) for p in points),Vector())/len(points)
    verts=list(points)+[tuple(c+Vector((thickness,0,0))),tuple(c-Vector((thickness,0,0)))]
    l=len(points); faces=[]
    for i in range(l): faces += [(l,i,(i+1)%l),(l+1,(i+1)%l,i)]
    return mesh(name,verts,faces,m,bone)
fin('Tall swept dorsal',[(0,-.8,.48),(0,-.55,1.55),(0,.4,.50),(0,.03,.44)],.12,gray,'Body')
fin('Second dorsal',[(0,1.15,.24),(0,1.35,.57),(0,1.67,.18)],.055,gray,'Tail')
fin('Crescent caudal',[(0,2.12,.08),(0,2.58,.75),(0,3.10,1.42),(0,2.98,.62),(0,2.62,.08),(0,2.94,-.53),(0,2.99,-1.02),(0,2.48,-.51)],.075,gray,'Fluke')
for s in [-1,1]:
    side='L' if s<0 else 'R'
    fin('Pectoral '+side,[(s*.48,-1.0,-.23),(s*1.88,-.0,-.55),(s*1.4,.20,-.46),(s*.43,-.15,-.29)],.045,gray,'Fin.'+side)
    fin('Pelvic '+side,[(s*.30,.65,-.26),(s*.65,1.27,-.37),(s*.18,1.18,-.23)],.035,gray,'Spine')
    sphere('Eye '+side,(s*.438,-2.20,.17),(.085,.09,.085),black,'Body')
    sphere('Eye highlight '+side,(s*.49,-2.225,.196),(.015,.022,.018),white,'Body')
    sphere('Nostril '+side,(s*.23,-2.47,.045),(.042,.023,.018),black,'Body')
    for i in range(5):
        y=-1.46+i*.12
        sphere('Gill '+side+str(i),(s*(.643+i*.005),y,-.025),(.012,.023,.22-i*.012),black,'Body')
# Dark mouth inset and lower jaw make the tooth line legible at gameplay scale.
sphere('Mouth opening',(0,-2.12,-.235),(.43,.43,.15),gum,'Body')
sphere('Lower jaw',(0,-2.05,-.345),(.40,.44,.12),white,'Jaw')
for row in [0,1]:
    for i in range(13):
        a=math.pi*.08+i/12*math.pi*.84
        x=.385*math.cos(a); y=-2.03-.385*math.sin(a)
        z=-.18 if row==0 else -.31; tip=z+(-.095 if row==0 else .065)
        mesh('Triangular tooth',[(x-.035,y,z),(x+.035,y,z),(x,y-.018,tip),(x,y+.02,z)],[(0,1,2),(0,3,1),(1,3,2),(2,3,0)],tooth,'Body' if row==0 else 'Jaw')
bpy.ops.object.select_all(action='DESELECT')
for o in parts: o.select_set(True)
bpy.context.view_layer.objects.active=body; bpy.ops.object.join(); body.name='GreatWhite_SkinnedMesh'
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.armature_add(); rig=bpy.context.object; rig.name='GreatWhite_Rig'; rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT'); eb=rig.data.edit_bones; eb.remove(eb[0])
def bone(name,head,tail,parent=None):
    b=eb.new(name); b.head=head; b.tail=tail
    if parent: b.parent=eb[parent]
bone('Root',(0,0,-.8),(0,0,-.5)); bone('Body',(0,-1.5,0),(0,.15,0),'Root')
bone('Spine',(0,.15,0),(0,1.1,0),'Body'); bone('Tail',(0,1.1,0),(0,2.1,.04),'Spine'); bone('Fluke',(0,2.1,.04),(0,2.8,.04),'Tail')
bone('Jaw',(0,-1.7,-.25),(0,-2.45,-.25),'Body')
for s in [-1,1]: bone('Fin.'+('L' if s<0 else 'R'),(s*.45,-.8,-.2),(s*1.7,.0,-.5),'Body')
bpy.ops.object.mode_set(mode='OBJECT'); body.parent=rig; mod=body.modifiers.new('Shark skin','ARMATURE'); mod.object=rig
scene=bpy.context.scene; scene.render.fps=30; scene.frame_start=1; scene.frame_end=41
rig.animation_data_create()
for action_name in ['Shark_Swim','Shark_Bite']:
    action=bpy.data.actions.new(action_name); rig.animation_data.action=action
    for f in range(1,42,2):
        phase=(f-1)/40*math.tau
        for p in rig.pose.bones: p.rotation_mode='XYZ'; p.rotation_euler=(0,0,0)
        for name,amp,lag in [('Body',.025,0),('Spine',.12,.5),('Tail',.23,1),('Fluke',.30,1.5)]: rig.pose.bones[name].rotation_euler.z=amp*math.sin(phase-lag)
        for s in [-1,1]: rig.pose.bones['Fin.'+('L' if s<0 else 'R')].rotation_euler.z=.035*math.sin(phase)
        rig.pose.bones['Jaw'].rotation_euler.x=(.40*math.sin(math.pi*(f-1)/40)**2 if action_name=='Shark_Bite' else .025)
        for p in rig.pose.bones: p.keyframe_insert('rotation_euler',frame=f,group=p.name)
    action.use_fake_user=True
rig.animation_data.action=bpy.data.actions['Shark_Swim']; scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'GreatWhite.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0)
def look(o,p): o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
scene.world.use_nodes=True; scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.115,.15,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
bpy.ops.object.camera_add(location=(8,-7,3.8)); camera=bpy.context.object; look(camera,(0,.2,0)); camera.data.type='ORTHO'; camera.data.ortho_scale=7.6; scene.camera=camera
for pos,power,size in [((2,-5,7),1400,5),((-4,-2,3),950,4),((1,5,5),1800,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos); o=bpy.context.object; o.data.energy=power; o.data.size=size; look(o,(0,0,0))
scene.render.engine='BLENDER_EEVEE'; scene.eevee.use_gtao=True
scene.render.resolution_x=1200; scene.render.resolution_y=850; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'; scene.view_settings.look='Medium High Contrast'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'GreatWhite.blend'))
scene.render.filepath=os.path.join(SOURCE,'GreatWhite-preview.png'); bpy.ops.render.render(write_still=True)
print('SHARK_COMPLETE '+json.dumps({'vertices':len(body.data.vertices),'bones':len(rig.data.bones),'actions':[a.name for a in bpy.data.actions]}))
