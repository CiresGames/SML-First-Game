using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.ProBuilder;
using Object=UnityEngine.Object;

namespace MantaFlight.Editor
{
    /// <summary>Editor-only assembly of the Blender island kit. Never rebuilds automatically.</summary>
    public static class CloudIslesEnvironmentBuilder
    {
        const string Dir="Assets/MantaFlight/Environment/CloudIsles";
        const string RootName="Cloud Isles • floating archipelago";
        static Material rock,grass,water,river,spray,ruin,bark;
        static Material[] leaves;
        static System.Random rng;
        static GameObject treeModel;
        struct Island
        {
            public string name;public Vector3 p,size;public bool falls;public int trees;
            public Island(string n,float x,float y,float z,float r,float depth,float rz,int t,bool f=true)
            {name=n;p=new Vector3(x,y,z);size=new Vector3(r,depth,rz);trees=t;falls=f;}
        }
        static readonly Island[] Layout={
            new Island("01 • The departure garden",0,240,-490,92,152,86,20,false),
            new Island("02 • Twin Veil Falls",-280,362,-15,150,266,126,65),
            new Island("03 • Sunward terraces",290,292,175,162,215,142,70),
            new Island("04 • Crown of the wind",-85,476,715,156,314,136,66),
            new Island("05 • Emerald hanging forest",665,405,865,199,276,168,82),
            new Island("06 • Western cascades",-660,296,655,182,226,146,70),
            new Island("07 • Cloud-skimming garden",30,151,310,65,105,72,18,false),
            new Island("08 • Half-hidden grove",-375,125,365,93,102,75,25,false),
            new Island("09 • Sentinel of dawn",-450,580,1495,174,382,143,66),
            new Island("10 • High sanctuary",510,622,1560,152,330,130,58),
            new Island("11 • Far horizon forest",70,423,2170,207,297,185,75),
            new Island("12 • Eastward mist",1080,170,460,150,150,122,45),
            new Island("13 • Lost in the cloud sea",-980,100,1125,128,133,108,35,false),
            new Island("14 • Northern cloud rim",1050,236,1840,174,185,143,50),
            new Island("15 • Distant needle",-1110,465,1890,80,280,73,22),
            new Island("16 • Quiet southern isle",-525,205,-650,103,165,90,32),
            new Island("17 • Sunrise ledge",680,362,-425,115,244,95,37),
            new Island("18 • Beneath the mist",-60,105,1100,110,122,85,30,false)
        };
        static float R(float a,float b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
        static GameObject Group(string name,Transform parent=null){var g=new GameObject(name);if(parent)g.transform.SetParent(parent,false);return g;}
        static Material Mat(string name,string shader,Color color)
        {
            string path=Dir+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
        }
        static Mesh SaveMesh(Mesh m,string name)
        {
            string path=Dir+"/Meshes/"+name+".asset";m.name=name;
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);return old;}
            AssetDatabase.CreateAsset(m,path);return m;
        }
        static GameObject MeshObject(string name,Mesh mesh,Material mat,Transform parent,bool collision=false)
        {
            var go=Group(name,parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
            return go;
        }
        static Vector3 Surface(MeshCollider meadow,Vector3 p)
        {
            var ray=new Ray(new Vector3(p.x,meadow.bounds.max.y+30,p.z),Vector3.down);
            return meadow.Raycast(ray,out var hit,100)?hit.point:p;
        }
        [MenuItem("Manta/Environment/Build Cloud Isles")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Build in Edit mode.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MantaFlight")throw new InvalidOperationException("Open MantaFlight first.");
            foreach(string sub in new[]{"Materials","Meshes","Weather"})Directory.CreateDirectory(Dir+"/"+sub);
            AssetDatabase.Refresh();rng=new System.Random(91026);
            rock=Mat("Stratified grey stone","MantaFlight/Cloud Isles/Weathered Stone and Meadow",new Color(.50f,.56f,.57f));
            grass=Mat("Meadow and moss","MantaFlight/Cloud Isles/Weathered Stone and Meadow",new Color(.30f,.43f,.14f));grass.SetFloat("_Meadow",1);
            water=Mat("Waterfall ribbons","MantaFlight/Cloud Isles/Cascades",new Color(.28f,.57f,.62f));
            river=Mat("Highland streams","MantaFlight/Cloud Isles/Cascades",new Color(.055f,.32f,.36f));river.SetFloat("_River",1);river.SetFloat("_Flow",.08f);
            spray=Mat("Waterfall spray","MantaFlight/Cloud Isles/Soft Spray",new Color(.80f,.88f,.92f,.10f));
            ruin=Mat("Ancient ivory sandstone","MantaFlight/Cloud Isles/Weathered Stone and Meadow",new Color(.62f,.61f,.49f));
            bark=Mat("Grove bark","Universal Render Pipeline/Lit",new Color(.24f,.19f,.13f));bark.SetFloat("_Smoothness",.06f);
            var leafSource=AssetDatabase.LoadAssetAtPath<Material>("Assets/MantaFlight/Trees/Shaders/ReiLeaves_Green.mat");
            leaves=new Material[3];
            for(int i=0;i<3;i++){
                string path=Dir+"/Materials/Grove foliage "+(i+1)+".mat";leaves[i]=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!leaves[i]){leaves[i]=new Material(leafSource);AssetDatabase.CreateAsset(leaves[i],path);}
                leaves[i].SetTexture("_LeafTexture",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Tree 1/textures/TreeLeaves01.png"));
                leaves[i].SetColor("_LeafColor",new Color(.15f+i*.025f,.34f+i*.025f,.075f));
                leaves[i].SetColor("_LeafVariation",new Color(.33f+i*.035f,.49f+i*.02f,.12f));
                leaves[i].SetColor("_ShadowTint",new Color(.58f,.70f,.53f));
                leaves[i].SetFloat("_RimStrength",.055f);leaves[i].SetFloat("_LightColorInfluence",.35f);leaves[i].SetFloat("_ShadowStrength",.6f);EditorUtility.SetDirty(leaves[i]);
            }
            treeModel=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Models/GroveTree.fbx");
            var old=GameObject.Find(RootName);if(old)Undo.DestroyObjectImmediate(old);
            var legacy=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name.StartsWith("Flight Playground"));
            if(legacy){Undo.RecordObject(legacy,"Retire prototype landscape");legacy.SetActive(false);}
            var root=Group(RootName);Undo.RegisterCreatedObjectUndo(root,"Build floating islands");
            int trees=0;
            for(int i=0;i<Layout.Length;i++){
                var spec=Layout[i];var island=Group(spec.name,root.transform);island.transform.position=spec.p;
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Models/Island_"+(i%6+1).ToString("00")+".fbx");
                var ground=(GameObject)PrefabUtility.InstantiatePrefab(model,island.transform);ground.name="Blender cliff and meadow";ground.transform.localScale=spec.size;
                MeshCollider meadow=null;
                foreach(var mr in ground.GetComponentsInChildren<MeshRenderer>()){
                    mr.sharedMaterial=mr.name.StartsWith("Meadow")?grass:rock;
                    var col=mr.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mr.GetComponent<MeshFilter>().sharedMesh;
                    if(mr.name.StartsWith("Meadow"))meadow=col;
                }
                trees+=Forest(island.transform,meadow,spec,i);
                if(spec.falls){Cascade(island.transform,meadow,spec,i,0);if(i==1||i==4||i==8)Cascade(island.transform,meadow,spec,i,1);}
                if(i==0||i==3||i==9)Ruins(island.transform,meadow,i);
                if(i!=0 && spec.size.x>100)CrownRocks(island.transform,meadow,spec,i);
                if(i==3||i==8||i==10)Satellites(island.transform,spec,i);
            }
            ConfigureAtmosphere();
            var manta=Object.FindFirstObjectByType<MantaController>();var delta=new Vector3(0,272,-542)-manta.transform.position;
            Undo.RecordObject(manta.transform,"Move departure point");manta.transform.SetPositionAndRotation(new Vector3(0,272,-542),Quaternion.identity);
            var rider=Object.FindFirstObjectByType<MantaFlight.Rider.RiderController>();if(rider){Undo.RecordObject(rider.transform,"Move rider departure");rider.transform.position+=delta;}
            var camera=Camera.main;Undo.RecordObject(camera.transform,"Frame new departure");
            camera.transform.SetPositionAndRotation(new Vector3(0,278,-562),Quaternion.Euler(3,0,0));camera.farClipPlane=10000;
            AddView(root.transform,"View • departure",new Vector3(0,280,-565),new Vector3(0,325,400));
            AddView(root.transform,"View • archipelago panorama",new Vector3(-650,640,-960),new Vector3(80,290,620));
            AddView(root.transform,"View • cloud level",new Vector3(120,180,-70),new Vector3(-250,260,360));
            Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(manta.gameObject.scene);
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,280,400),Quaternion.Euler(16,15,0),950);
            return "Created "+Layout.Length+" islands, "+trees+" trees, 16 cascades, three ProBuilder ruins. Scene ready for validation.";
        }
        static int Forest(Transform parent,MeshCollider meadow,Island spec,int index)
        {
            var used=new List<Vector3>();var positions=new List<Vector3>();var scales=new List<Vector3>();var yaws=new List<float>();int count=0;
            for(int attempt=0;attempt<spec.trees*18 && count<spec.trees;attempt++){
                float x=R(-.84f,.84f)*spec.size.x,z=R(-.84f,.84f)*spec.size.z;
                if(x*x/(spec.size.x*spec.size.x)+z*z/(spec.size.z*spec.size.z)>.70f)continue;
                // Leave the stream and the landing lawn open.
                if(spec.falls && z<20 && Mathf.Abs(x)<16)continue;
                if(index==0 && Mathf.Abs(x)<23)continue;
                if((index==3||index==9)&&Mathf.Abs(x)<30&&z>0&&z<55)continue;
                if(spec.size.x>100 && z>spec.size.z*.32f && x>spec.size.x*.22f)continue;
                Vector3 local=new Vector3(x,0,z);if(used.Any(p=>(p-local).sqrMagnitude<200))continue;
                Vector3 point=Surface(meadow,parent.position+local);if(point.y<parent.position.y-2)continue;
                float scale=R(2.3f,4.8f);positions.Add(point-parent.position);yaws.Add(R(0,360));scales.Add(new Vector3(scale*R(.85f,1.2f),scale,scale));
                used.Add(local);count++;
            }
            var grove=Group("Instanced forest • shared leaf cards",parent).AddComponent<MantaIslandGrove>();
            grove.positions=positions.ToArray();grove.scales=scales.ToArray();grove.yaws=yaws.ToArray();grove.trunkMaterial=bark;grove.leafMaterial=leaves[index%3];
            grove.leafMaterial.enableInstancing=true;grove.trunkMaterial.enableInstancing=true;
            foreach(var mf in treeModel.GetComponentsInChildren<MeshFilter>()){
                var source=treeModel.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;
                if(mf.name.ToLowerInvariant().Contains("leav")){grove.leafMesh=mf.sharedMesh;grove.leafSource=source;}else{grove.trunkMesh=mf.sharedMesh;grove.trunkSource=source;}
            }
            grove.localBounds=new Bounds(new Vector3(0,25,0),new Vector3(spec.size.x*2.4f,70,spec.size.z*2.4f));
            // Retire only this builder's superseded combined forest assets.
            foreach(string suffix in new[]{"Trunks","Leaf cards"})AssetDatabase.DeleteAsset(Dir+"/Meshes/Grove_"+index+"_"+suffix+".asset");
            return count;
        }
        static void Cascade(Transform parent,MeshCollider meadow,Island spec,int index,int branch)
        {
            float x=branch==0?0:spec.size.x*.55f;
            // Probe the exact meadow edge along -Z; this matches all six irregular silhouettes.
            Vector3 last=Surface(meadow,parent.position+new Vector3(x,0,0));float edge=0;
            for(float z=0;z>-spec.size.z*1.3f;z-=1){
                var ray=new Ray(parent.position+new Vector3(x,30,z),Vector3.down);
                if(!meadow.Raycast(ray,out var hit,60))break;last=hit.point;edge=z;
            }
            float width=branch==0?R(9,15):R(4,7);float length=Mathf.Max(72,spec.p.y-65);
            var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();int cols=8,rows=28;
            for(int j=0;j<=rows;j++)for(int k=0;k<=cols;k++){
                float t=j/(float)rows,u=k/(float)cols;
                float spread=1+t*.55f;
                v.Add(new Vector3(x+(u-.5f)*width*spread+Mathf.Sin(t*14+index)*t*1.7f,last.y-spec.p.y+1-t*length,edge-2-18*(1-Mathf.Exp(-t*12))));
                uv.Add(new Vector2(u,t));
            }
            GridTriangles(tris,rows,cols);var mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            var waterfall=MeshObject("Waterfall • "+(branch+1),SaveMesh(mesh,"Cascade_"+index+"_"+branch),water,parent);waterfall.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            v.Clear();uv.Clear();tris.Clear();rows=24;cols=1;
            for(int j=0;j<=rows;j++)for(int k=0;k<=cols;k++){
                float t=j/(float)rows;float z=Mathf.Lerp(10,edge-2,t);float cx=x+Mathf.Sin(t*5)*(1-t)*8;
                Vector3 p=Surface(meadow,parent.position+new Vector3(cx+(k-.5f)*width*1.05f,0,z))-parent.position;
                if(j==rows)p.y=last.y-spec.p.y;
                p.y+=1.05f;v.Add(p);uv.Add(new Vector2(k,t));
            }
            GridTriangles(tris,rows,cols);mesh=new Mesh();mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            MeshObject("Feeding stream",SaveMesh(mesh,"Stream_"+index+"_"+branch),river,parent).GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            var go=Group("Drifting waterfall spray",parent);go.transform.localPosition=new Vector3(x,-length*.76f,edge-19);
            var ps=go.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=6;main.startSpeed=1.5f;main.startSize=new ParticleSystem.MinMaxCurve(10,24);main.startColor=new Color(1,1,1,.35f);main.maxParticles=28;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var em=ps.emission;em.rateOverTime=3;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=width*.6f;
            var vel=ps.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.World;vel.y=-2;vel.x=2;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.5f,1,1.8f));
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.3f),new GradientAlphaKey(0,1)});color.color=gradient;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=spray;
        }
        static void GridTriangles(List<int> tris,int rows,int cols){for(int j=0;j<rows;j++)for(int k=0;k<cols;k++){int a=j*(cols+1)+k;tris.AddRange(new[]{a,a+cols+1,a+1,a+1,a+cols+1,a+cols+2});}}
        static void Ruins(Transform parent,MeshCollider meadow,int index)
        {
            var center=Surface(meadow,parent.position+new Vector3(index==0?30:0,0,28));
            var root=Group("Ancient lookout • editable ProBuilder",parent);root.transform.position=center;
            void Block(string name,Vector3 p,Vector3 size){var pb=ShapeGenerator.GenerateCube(PivotLocation.Center,size);pb.name=name;pb.transform.SetParent(root.transform,false);pb.transform.localPosition=p;pb.GetComponent<MeshRenderer>().sharedMaterial=ruin;var col=pb.GetComponent<MeshCollider>();if(!col)col=pb.gameObject.AddComponent<MeshCollider>();col.sharedMesh=pb.GetComponent<MeshFilter>().sharedMesh;}
            for(int s=0;s<3;s++)Block("Weathered step",new Vector3(0,s*.75f,0),new Vector3(24-s*2,1,17-s*2));
            foreach(float x in new[]{-8f,8f}){Block("Column foot",new Vector3(x,3,0),new Vector3(5,2,5));Block("Standing column",new Vector3(x,12,0),new Vector3(3,16,3));Block("Capital",new Vector3(x,20,0),new Vector3(5,2,5));}
            var arch=ShapeGenerator.GenerateArch(PivotLocation.Center,180,9,2.5f,3,11,true,true,true,true,true);
            arch.name="Sun gate arch";arch.transform.SetParent(root.transform,false);arch.transform.localPosition=new Vector3(0,20,0);arch.GetComponent<MeshRenderer>().sharedMaterial=ruin;
            var ac=arch.GetComponent<MeshCollider>();if(!ac)ac=arch.gameObject.AddComponent<MeshCollider>();ac.sharedMesh=arch.GetComponent<MeshFilter>().sharedMesh;
            for(int k=0;k<5;k++){Block("Fallen masonry",new Vector3(R(-17,17),2,R(6,15)),new Vector3(R(2,4),R(1,3),R(2,4)));}
        }
        static void Satellites(Transform parent,Island spec,int index)
        {
            for(int j=0;j<3;j++){
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Models/Island_"+((index+j)%6+1).ToString("00")+".fbx");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(model,parent);go.name="Detached hanging shard";
                go.transform.localPosition=new Vector3(spec.size.x*R(.9f,1.35f),-spec.size.y*R(.35f,.75f),R(-70,70));go.transform.localScale=new Vector3(R(12,23),R(35,75),R(12,24));
                foreach(var mr in go.GetComponentsInChildren<MeshRenderer>()){mr.sharedMaterial=mr.name.StartsWith("Meadow")?grass:rock;mr.gameObject.AddComponent<MeshCollider>().sharedMesh=mr.GetComponent<MeshFilter>().sharedMesh;}
            }
        }
        static void AddView(Transform parent,string name,Vector3 p,Vector3 look){var g=Group(name,parent);g.transform.position=p;g.transform.LookAt(look);}
        static void CrownRocks(Transform parent,MeshCollider meadow,Island spec,int index)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Models/CrownRock.fbx");
            for(int j=0;j<4;j++){
                var go=(GameObject)PrefabUtility.InstantiatePrefab(model,parent);go.name="Weathered crown outcrop "+j;
                go.transform.position=Surface(meadow,parent.position+new Vector3(spec.size.x*(.34f+j*.095f),0,spec.size.z*(.63f-j*.08f)));
                float height=(index%3==0?70:40)*R(.6f,1.1f);go.transform.localScale=new Vector3(R(15,26),height,R(14,24));go.transform.localRotation=Quaternion.Euler(0,R(0,360),0);
                foreach(var mr in go.GetComponentsInChildren<MeshRenderer>()){mr.sharedMaterial=rock;mr.gameObject.AddComponent<MeshCollider>().sharedMesh=mr.GetComponent<MeshFilter>().sharedMesh;}
            }
        }
        static void ConfigureAtmosphere()
        {
            var cloud=Object.FindFirstObjectByType<MantaCloudscape>();var gpu=cloud.GetComponent<MantaGpuWeather>();var forecast=cloud.GetComponent<MantaRainForecast>();var weather=cloud.GetComponent<MantaCloudWeather>();
            Undo.RecordObjects(new Object[]{cloud,gpu,forecast,weather},"Art direct island atmosphere");
            gpu.mainLayer=new Vector2(-110,430);gpu.highLayer=new Vector2(-100,5);gpu.billowSize=1400;gpu.extinction=.016f;gpu.viewDistance=10000;
            var original=forecast.forecasts;
            var presets=new MantaCloudPreset[original.Length];
            for(int i=0;i<original.Length;i++){
                string path=Dir+"/Weather/Cloud sea "+i+".asset";var preset=AssetDatabase.LoadAssetAtPath<MantaCloudPreset>(path);
                if(!preset){preset=Object.Instantiate(original[i]);AssetDatabase.CreateAsset(preset,path);}
                var a=preset.appearance;a.coverage=Mathf.Max(.84f,a.coverage);a.density=Mathf.Max(.9f,a.density);a.thickness=1;a.noiseScale=1;a.erosion=.32f;preset.appearance=a;
                preset.description="Cloud-island environment variant; original weather preset preserved.";EditorUtility.SetDirty(preset);presets[i]=preset;
            }
            forecast.forecasts=presets;forecast.randomizeStartingForecast=false;forecast.startingForecast=0;
            forecast.randomizeSessionSeed=false;weather.startingPreset=presets[0];cloud.SetAppearance(presets[0].appearance);cloud.Apply();
            var day=Object.FindFirstObjectByType<MantaDayNightCycle>();Undo.RecordObject(day,"Island golden-hour atmosphere");
            day.dayFogDensity=.00023f;day.airExtinction=.00016f;day.airHeight=1000;day.rayIntensity=.7f;day.dayFillIntensity=.85f;day.sunAzimuth=-38;day.SetTime(15.7f);
        }
        public static string Capture(string name,Vector3 position,Vector3 look)
        {
            var c=Camera.main;var p=c.transform.position;var q=c.transform.rotation;float f=c.fieldOfView;
            var oldTarget=c.targetTexture;var oldActive=RenderTexture.active;var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            try{
                c.transform.position=position;c.transform.LookAt(look);c.fieldOfView=60;Directory.CreateDirectory("ArtSource/CloudIsles");c.targetTexture=rt;
                for(int i=0;i<4;i++)c.Render();RenderTexture.active=rt;
                var image=new Texture2D(1600,900,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
                File.WriteAllBytes("ArtSource/CloudIsles/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);return "Captured "+name;
            }
            finally{c.targetTexture=oldTarget;RenderTexture.active=oldActive;Object.DestroyImmediate(rt);c.transform.SetPositionAndRotation(p,q);c.fieldOfView=f;}
        }
    }
}
