"""Author reusable floating-island meshes in Blender, metres normalized to radius/depth 1."""
import bpy, math, random, os
from mathutils import Vector
ROOT=r'C:\__dev\quentin\SML-First-Game'
OUT=os.path.join(ROOT,'Assets','MantaFlight','Environment','CloudIsles','Models')
os.makedirs(OUT,exist_ok=True)
os.makedirs(os.path.join(ROOT,'ArtSource','CloudIsles'),exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
rock=bpy.data.materials.new('Island Stone');rock.diffuse_color=(.26,.33,.37,1)
grass=bpy.data.materials.new('Island Meadow');grass.diffuse_color=(.2,.35,.12,1)
def mesh(name,verts,faces,mat):
    m=bpy.data.meshes.new(name);m.from_pydata(verts,[],faces);m.update()
    o=bpy.data.objects.new(name,m);bpy.context.collection.objects.link(o);m.materials.append(mat)
    bpy.context.view_layer.objects.active=o;o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=1.05,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT');o.select_set(False)
    return o
all_objs=[]
for variant in range(6):
    rng=random.Random(1209+variant*781);n=64;phase=rng.random()*6.28
    def outline(a):return 1+.12*math.sin(a*3+phase)+.07*math.sin(a*7+phase*2)+.035*math.cos(a*13+phase)
    angles=[math.tau*i/n for i in range(n)]
    edge=[outline(a) for a in angles]
    levels=[(0,1),(-.035,1.035),(-.12,1.015),(-.27,.93),(-.34,.95),(-.53,.80),(-.59,.82),(-.77,.57),(-.94,.33),(-1.09,.08)]
    verts=[];faces=[]
    for j,(z,r) in enumerate(levels):
        for i,a in enumerate(angles):
            rr=edge[i]*r*(1+.025*math.sin(i*3.7+j*2.1))
            zz=z if j==0 else z+.025*math.sin(a*5+phase+j*.35)+rng.uniform(-.012,.012)
            verts.append((math.cos(a)*rr+.13*z,math.sin(a)*rr-.1*z,zz))
    for j in range(len(levels)-1):
        for i in range(n):
            a=j*n+i;b=j*n+(i+1)%n;c=(j+1)*n+(i+1)%n;d=(j+1)*n+i
            faces.extend([(a,b,c),(a,c,d)])
    faces.append(tuple(range((len(levels)-1)*n,len(levels)*n)))
    cliff=mesh('Cliff',verts,faces,rock)
    # Long fractured buttresses break the rotational silhouette and catch grazing light.
    parts=[cliff]
    for k in range(20):
        a=math.tau*k/20+rng.uniform(-.07,.07);rr=outline(a)*.89
        width=rng.uniform(.08,.17);bottom=rng.uniform(.50,1.14)
        vv=[];ff=[]
        for j,(z,fac) in enumerate([(-.025,1),(-.18,1.13),(-bottom*.65,.8),(-bottom,.15)]):
            for q in range(6):
                t=math.tau*q/6
                vv.append((math.cos(a)*(rr+.12*z)+math.cos(t)*width*fac,
                           math.sin(a)*(rr+.12*z)+math.sin(t)*width*fac,z))
        ff.append(tuple(range(5,-1,-1)))
        for j in range(3):
            for q in range(6):ff.append((j*6+q,j*6+(q+1)%6,(j+1)*6+(q+1)%6,(j+1)*6+q))
        ff.append(tuple(range(18,24)))
        parts.append(mesh('Fracture',vv,ff,rock))
    for p in parts:p.select_set(True)
    bpy.context.view_layer.objects.active=cliff;bpy.ops.object.join();cliff.select_set(False)
    # A traversable meadow with a level rim and subtly raised interior.
    vv=[(0,0,.018)];ff=[]
    for j in range(1,7):
        t=j/6
        for i,a in enumerate(angles):
            z=(1-t)*(.016+.010*math.sin(a*2+phase))
            vv.append((math.cos(a)*edge[i]*t,math.sin(a)*edge[i]*t,z))
    for i in range(n):ff.append((0,1+i,1+(i+1)%n))
    for j in range(5):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;c=1+(j+1)*n+(i+1)%n;d=1+(j+1)*n+i
            ff.extend([(a,d,c),(a,c,b)])
    meadow=mesh('Meadow',vv,ff,grass)
    for p in meadow.data.polygons:p.use_smooth=True
    for o in (cliff,meadow):o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,f'Island_{variant+1:02}.fbx'),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,
        add_leaf_bones=False,mesh_smooth_type='FACE',use_mesh_modifiers=True)
    for o in (cliff,meadow):o.select_set(False);o.location.x=variant*3.3
    all_objs.extend((cliff,meadow))
# A separate weathered crown outcrop for breaking up the meadow skylines.
vv=[];ff=[];n=9
for j,(z,r) in enumerate([(0,1),(.12,1.02),(.38,.88),(.48,.91),(.85,.55),(1,.43)]):
    for i in range(n):
        a=math.tau*i/n;rr=r*(1+.12*math.sin(a*3+.4))
        vv.append((math.cos(a)*rr+z*.20,math.sin(a)*rr,z+(0 if j==0 else .035*math.sin(a*2))))
ff.append(tuple(range(n-1,-1,-1)))
for j in range(5):
    for i in range(n):ff.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
ff.append(tuple(range(5*n,6*n)))
crown=mesh('CrownRock',vv,ff,rock);bpy.context.view_layer.objects.active=crown;crown.select_set(True)
mod=crown.modifiers.new('Chipped edges','BEVEL');mod.width=.027;mod.segments=1
bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'CrownRock.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False)
crown.location=(0,3,0)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource','CloudIsles','CloudIsles_IslandKit.blend'))
print('Finished: six Blender island variants, cliffs + unwrapped meadows.')
