using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace MantaFlight.Editor
{
    public static class MantaNightSkySetup
    {
        const int Width=2048,Height=1024;
        const string Path="Assets/MantaFlight/Materials/Earthlike star atlas.asset";
        [MenuItem("Manta/Environment/Build Earthlike night sky")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var cycle=Object.FindFirstObjectByType<MantaDayNightCycle>();
            if(!cycle) throw new System.InvalidOperationException("Day/night sky required.");
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
            var existing=atlas;
            // This menu explicitly rebuilds the deterministic atlas while retaining its asset GUID.
            {
                var pixels=new Color[Width*Height];
                var normal=new Vector3(.48f,.38f,.79f).normalized;
                var right=Vector3.Cross(normal,Vector3.up).normalized;
                var forward=Vector3.Cross(right,normal);
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
                {
                    float longitude=((x+.5f)/Width-.5f)*Mathf.PI*2;
                    float latitude=((y+.5f)/Height-.5f)*Mathf.PI;
                    var d=new Vector3(Mathf.Sin(longitude)*Mathf.Cos(latitude),Mathf.Sin(latitude),Mathf.Cos(longitude)*Mathf.Cos(latitude));
                    float b=Vector3.Dot(d,normal);
                    float n=Noise(d*8), fine=Noise(d*35);
                    float band=Mathf.Exp(-b*b/ .018f);
                    float halo=Mathf.Exp(-b*b/.075f);
                    float core=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,forward)),8);
                    float dust=Mathf.Lerp(.18f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01((fine+n*.3f-.32f)*2.5f)));
                    float glow=(band*(.035f+.11f*core)*(.35f+n)*dust+halo*.009f);
                    pixels[y*Width+x]=Color.Lerp(new Color(.54f,.67f,1),new Color(1,.78f,.56f),core)*glow;
                    pixels[y*Width+x].a=0;
                }
                var rng=new System.Random(190723);
                for(int i=0;i<11000;i++)
                {
                    Vector3 d;
                    do {
                        float z=(float)rng.NextDouble()*2-1, a=(float)rng.NextDouble()*Mathf.PI*2;
                        d=new Vector3(Mathf.Sqrt(1-z*z)*Mathf.Sin(a),z,Mathf.Sqrt(1-z*z)*Mathf.Cos(a));
                    } while(i>6500 && Mathf.Abs(Vector3.Dot(d,normal))>.16f);
                    float distance=Mathf.Lerp(15,2500,Mathf.Pow((float)rng.NextDouble(),.65f));
                    float luminosity=Mathf.Pow(10,Mathf.Lerp(1,5,(float)rng.NextDouble()));
                    float flux=Mathf.Clamp(luminosity/(distance*distance)*8,.014f,3.5f);
                    float radius=Mathf.Lerp(.28f,.65f,Mathf.Sqrt(flux/3.5f));
                    float x=(Mathf.Atan2(d.x,d.z)/(2*Mathf.PI)+.5f)*Width;
                    float y=(Mathf.Asin(d.y)/Mathf.PI+.5f)*Height;
                    float stretch=1/Mathf.Max(.12f,Mathf.Sqrt(1-d.y*d.y));
                    var tint=Color.Lerp(new Color(.61f,.76f,1),new Color(1,.62f,.34f),(float)rng.NextDouble());
                    int spanX=Mathf.CeilToInt(radius*stretch*3),spanY=Mathf.CeilToInt(radius*3);
                    for(int dy=-spanY;dy<=spanY;dy++) for(int dx=-spanX;dx<=spanX;dx++)
                    {
                        int px=Mathf.FloorToInt(x)+dx,py=Mathf.FloorToInt(y)+dy;
                        if(py<0 || py>=Height) continue;
                        float u=(px+.5f-x)/stretch,v=py+.5f-y;
                        float value=flux*Mathf.Exp(-(u*u+v*v)/(radius*radius));
                        int index=py*Width+((px%Width+Width)%Width);
                        var p=pixels[index]; p.r+=tint.r*value;p.g+=tint.g*value;p.b+=tint.b*value;
                        p.a=Mathf.Max(p.a,Mathf.Clamp01(value*4)); pixels[index]=p;
                    }
                }
                var packed=new Color32[pixels.Length];
                for(int i=0;i<pixels.Length;i++)
                {
                    var p=pixels[i];
                    packed[i]=new Color(Mathf.Sqrt(Mathf.Clamp01(p.r/4)),Mathf.Sqrt(Mathf.Clamp01(p.g/4)),Mathf.Sqrt(Mathf.Clamp01(p.b/4)),p.a);
                }
                atlas=new Texture2D(Width,Height,TextureFormat.RGBA32,true,true){name="Earthlike stars and Milky Way",wrapModeU=TextureWrapMode.Repeat,wrapModeV=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=1};
                atlas.SetPixels32(packed); atlas.Apply(true,true);
                if(existing) { EditorUtility.CopySerialized(atlas,existing); Object.DestroyImmediate(atlas); atlas=existing; EditorUtility.SetDirty(atlas); }
                else AssetDatabase.CreateAsset(atlas,Path);
            }
            Undo.RecordObject(cycle.skyMaterial,"Add star atlas");
            cycle.skyMaterial.SetTexture("_StarAtlas",atlas); cycle.ApplyLighting();
            EditorUtility.SetDirty(cycle.skyMaterial); EditorUtility.SetDirty(cycle);
            EditorSceneManager.MarkSceneDirty(cycle.gameObject.scene); EditorSceneManager.SaveScene(cycle.gameObject.scene); AssetDatabase.SaveAssets();
        }
        static float Noise(Vector3 p)=> (Mathf.PerlinNoise(p.x+37,p.y+91)+Mathf.PerlinNoise(p.y+17,p.z+53)+Mathf.PerlinNoise(p.z+73,p.x+11))/3;
    }
}
