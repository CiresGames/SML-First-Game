"""Blender 2.93+: blender -b --python Tools/MantaFruits/create_fruits.py.
The JSON palette is shared with Unity. Exports are centered, 0.9 x 0.9 x 1 m.
"""
import bpy, math, os, json
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
SOURCE = os.path.join(ROOT, 'ArtSource/MantaFruits')
EXPORT = os.path.join(ROOT, 'Assets/MantaFlight/Resources/MantaFruits')
for path in (SOURCE, EXPORT): os.makedirs(path, exist_ok=True)
with open(os.path.join(ROOT, 'Assets/MantaFlight/Resources/MantaFruitPalette.json')) as f:
    PALETTE = json.load(f)['entries']
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'; scene.cycles.samples = 40
scene.cycles.use_denoising = True
scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'Medium High Contrast'
scene.view_settings.exposure = 0; scene.view_settings.gamma = 1
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.12, .16, .22, 1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .35
parts = []; models = []; report = []

def linear(c): return c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4
def material(entry):
    rgb = [linear(int(entry['hex'][i:i+2], 16)/255) for i in (0,2,4)]
    mat = bpy.data.materials.new(entry['stat']+' | #'+entry['hex'])
    mat.diffuse_color = (*rgb,1); mat.use_nodes = True
    bsdf = mat.node_tree.nodes['Principled BSDF']
    bsdf.inputs['Base Color'].default_value = (*rgb,1)
    bsdf.inputs['Roughness'].default_value = .32
    bsdf.inputs['Metallic'].default_value = .02
    return mat

def mesh(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name); data.from_pydata(vertices, [], faces); data.update()
    ob = bpy.data.objects.new(name, data); bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat)
    for face in data.polygons: face.use_smooth = True
    parts.append(ob); return ob

def surface(name, fn, mat, rings=32, segments=64):
    verts = [fn(i/rings, 2*math.pi*j/segments) for i in range(rings+1) for j in range(segments)]
    faces = []
    for i in range(rings):
        for j in range(segments):
            a=i*segments+j; b=i*segments+(j+1)%segments
            faces.append((a,b,b+segments,a+segments))
    ob=mesh(name,verts,faces,mat)
    uv=ob.data.uv_layers.new(name='FruitUV')
    for poly in ob.data.polygons:
        for li in poly.loop_indices:
            vi=ob.data.loops[li].vertex_index
            uv.data[li].uv=(vi%segments/segments,vi//segments/rings)
    return ob

def stem(mat, base, length=.15, lean=.06):
    def fn(t,a):
        r=.027*(1-.45*t)
        return (base[0]+lean*t*t+r*math.cos(a),base[1]+r*math.sin(a),base[2]+length*t)
    ob=surface('Curved stem',fn,mat,8,12)
    # Close the open ends without changing silhouette.
    return ob

def leaf(mat, base, angle, length=.25, width=.08, lift=.06):
    direction=Vector((math.cos(angle),math.sin(angle),0)); side=Vector((-math.sin(angle),math.cos(angle),0))
    verts=[]; faces=[]
    for i in range(9):
        t=i/8; center=Vector(base)+direction*(t*length)+Vector((0,0,lift*math.sin(t*math.pi*.7)))
        w=width*math.sin(math.pi*t)**.7
        for k in (-1,0,1): verts.append(center+side*(k*w)+Vector((0,0,.025*math.sin(t*math.pi)*(1-abs(k)))))
    for i in range(8):
        for j in range(2):
            a=i*3+j; faces.append((a,a+1,a+4,a+3))
    ob=mesh('Folded leaf',verts,faces,mat)
    solid=ob.modifiers.new('Leaf thickness','SOLIDIFY'); solid.thickness=.012
    bpy.context.view_layer.objects.active=ob; bpy.ops.object.modifier_apply(modifier=solid.name)
    return ob

def radial(r,z,a): return (r*math.cos(a),r*math.sin(a),z)
def build(entry):
    global parts
    parts=[]; mat=material(entry); name=entry['stat']
    if name=='Speed':
        def fn(t,a):
            r=math.sin(math.pi*t)**.75*(.42-.22*t)*(1+.09*math.cos(3*a+2.5*t))
            return radial(r,-.48+.93*t,a)
        surface('Streamlined teardrop',fn,mat)
        stem(mat,(0,0,.44),.1,.05)
        for a in (0,2.1,4.2): leaf(mat,(0,0,.34),a,.28,.04,.13)
    elif name=='Manoeuvrability':
        def fn(t,a):
            r=math.sin(math.pi*t)**.5*(.32+.105*math.cos(5*(a-t*.72)))
            return radial(r,-.38+.76*t,a)
        surface('Twisted five-point starfruit',fn,mat,32,80)
        stem(mat,(0,0,.37),.15,-.07)
        leaf(mat,(0,0,.43),.2,.24,.065,.04)
    elif name=='Endurance':
        def fn(t,a):
            r=math.sin(math.pi*t)**.65*(.30+.18*math.exp(-((t-.3)/.24)**2)-.1*t)
            return radial(r*(1+.025*math.cos(6*a)),-.43+.86*t,a)
        surface('Plump long-neck pear',fn,mat)
        stem(mat,(0,0,.42),.14,.04)
        leaf(mat,(0,0,.45),.4,.27,.13,.05)
        leaf(mat,(0,0,.47),3.5,.18,.07,.04)
    elif name=='Force':
        def fn(t,a):
            r=math.sin(math.pi*t)**.52*(.40+.045*math.cos(8*a))
            return radial(r,-.32+.60*t,a)
        surface('Eight-rib armored pumpkin',fn,mat)
        stem(mat,(0,0,.28),.19,.02)
        for a in range(5): leaf(mat,(0,0,.30),a*math.pi*2/5,.20,.055,.025)
    else:
        # Heart cross-section in X/Z with a rounded front and back. The cleft and tip remain real geometry.
        def fn(t,a):
            depth=math.cos(math.pi*t); ring=math.sin(math.pi*t)
            x=16*math.sin(a)**3/36
            z=(13*math.cos(a)-5*math.cos(2*a)-2*math.cos(3*a)-math.cos(4*a))/36
            return (x*ring,.30*depth,z*ring)
        surface('Heart berry',fn,mat,32,80)
        stem(mat,(0,0,.19),.14,.045)
        leaf(mat,(.02,0,.31),.1,.19,.08,.045)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts: ob.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join()
    ob=bpy.context.object; ob.name=name+' Fruit'; ob.data.name=name+' Fruit Mesh'
    # Merge duplicate pole vertices, repair normals and create a usable secondary UV chart.
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=.00001); bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    coords=[v.co for v in ob.data.vertices]
    lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)))
    hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
    center=(lo+hi)*.5; bounds=hi-lo; target=(.9,.9,1)
    for v in ob.data.vertices:
        v.co=Vector(tuple((v.co[i]-center[i])*target[i]/bounds[i] for i in range(3)))
    ob.data.update(); bpy.context.view_layer.update()
    # One material slot, with all surfaces matching the stat swatch.
    ob.data.materials.clear(); ob.data.materials.append(mat)
    for face in ob.data.polygons: face.material_index=0
    ob['stat']=name; ob['palette_hex']='#'+entry['hex']; ob['size_metres']='0.9 x 0.9 x 1.0'; ob['shape']=entry['shape']
    bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,name+'.fbx'),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,bake_anim=False,use_mesh_modifiers=True)
    triangles=sum(len(p.vertices)-2 for p in ob.data.polygons)
    report.append({'stat':name,'hex':'#'+entry['hex'],'shape':entry['shape'],'blender_dimensions':list(ob.dimensions),'triangles':triangles})
    models.append(ob)

for entry in PALETTE: build(entry)
# Store the individual source files at origin, with no studio objects.
for ob in models:
    for other in models: other.hide_render=other!=ob; other.hide_set(other!=ob)
    bpy.ops.object.select_all(action='DESELECT'); ob.hide_set(False); ob.select_set(True); bpy.context.view_layer.objects.active=ob
    single=bpy.data.scenes.new(ob['stat']+' Fruit'); single.collection.objects.link(ob); single.world=scene.world
    bpy.data.libraries.write(os.path.join(SOURCE,ob['stat']+'.blend'),{single},fake_user=True)
    bpy.data.scenes.remove(single)
for i,ob in enumerate(models): ob.hide_render=False; ob.hide_set(False); ob.location=(i*1.65-3.3,0,.55)

def point_at(ob,target): ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
def text(label,x,z,size,color):
    bpy.ops.object.text_add(location=(x,-.15,z)); ob=bpy.context.object; ob.name=label
    ob.data.body=label; ob.data.align_x='CENTER'; ob.data.size=size; ob.rotation_euler=(math.pi/2,0,0)
    mat=bpy.data.materials.new('Label '+label); mat.diffuse_color=(*color,1); ob.data.materials.append(mat)
    return ob
for i,entry in enumerate(PALETTE):
    x=i*1.65-3.3
    text(entry['stat'].upper(),x,.05,.15,(.8,.86,.9))
    text('#'+entry['hex']+' / '+entry['shape'],x,-.12,.095,(.5,.62,.67))
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.035)); ground=bpy.context.object; ground.name='Studio floor'
floor=bpy.data.materials.new('Midnight blue'); floor.diffuse_color=(.014,.024,.038,1); ground.data.materials.append(floor)
# Place labels in front of floor in camera space.
for ob in list(bpy.data.objects):
    if ob.type=='FONT': ob.location.y=-.85; ob.location.z+=.18
bpy.ops.object.camera_add(location=(0,-11,4.4)); camera=bpy.context.object; point_at(camera,(0,0,.32))
camera.data.type='ORTHO'; camera.data.ortho_scale=9.3; scene.camera=camera
for pos,power,size in [((0,-4,7),950,7),((-5,-1,3),450,5),((4,4,5),1000,5)]:
    bpy.ops.object.light_add(type='AREA',location=pos); light=bpy.context.object; light.data.energy=power; light.data.size=size; point_at(light,(0,0,.4))
scene.render.resolution_x=1800; scene.render.resolution_y=700; scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
with open(os.path.join(SOURCE,'fruit-manifest.json'),'w') as f: json.dump(report,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'MantaStatFruits.blend'))
scene.render.filepath=os.path.join(SOURCE,'MantaStatFruits-preview.png'); bpy.ops.render.render(write_still=True)
print('MANTA_FRUITS_COMPLETE '+json.dumps(report))
