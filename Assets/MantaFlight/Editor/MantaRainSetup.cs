using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace MantaFlight.Editor
{
    public static class MantaRainSetup
    {
        [MenuItem("Manta/Environment/Set up localized rain forecasts")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var clouds=Object.FindFirstObjectByType<MantaCloudscape>();
            if(!clouds) throw new System.InvalidOperationException("Add clouds first.");
            var d=clouds.GetComponent<MantaCloudDistribution>();
            Undo.RecordObject(d,"Enable rain-bearing layers");
            foreach(var layer in d.altitudeLayers)
                if(layer.name=="Mid sky billows") layer.rainCloud=true;
            d.Regenerate();
            var weather=clouds.GetComponent<MantaCloudWeather>();
            var forecast=clouds.GetComponent<MantaRainForecast>() ?? Undo.AddComponent<MantaRainForecast>(clouds.gameObject);
            const string folder="Assets/MantaFlight/Settings/Cloud Weather/";
            var heavy=AssetDatabase.LoadAssetAtPath<MantaCloudPreset>(folder+"Heavy clouds.asset");
            var fair=AssetDatabase.LoadAssetAtPath<MantaCloudPreset>(folder+"Scattered clouds.asset");
            var light=Preset("Light rain",heavy,.2f,.74f,1.05f,3);
            var moderate=Preset("Moderate rain",heavy,.55f,.86f,1.35f,5);
            var storm=Preset("Heavy rain",heavy,1,.97f,1.8f,8);
            if(forecast.forecasts==null || forecast.forecasts.Length==0)
                forecast.forecasts=new[]{fair,heavy,light,moderate,storm};
            weather.automaticCycle=false; weather.startingPreset=light;
            weather.PreviewStartingPreset();
            var local=clouds.GetComponent<MantaLocalRain>() ?? Undo.AddComponent<MantaLocalRain>(clouds.gameObject);
            local.clouds=clouds; local.viewer=Camera.main.transform;
            var manta=Object.FindFirstObjectByType<MantaController>();
            if(manta) { local.player=manta.transform; local.shelterMask=manta.settings.environmentMask; }
            if(!local.rainParticles)
            {
                var go=new GameObject("Rain • local particle volume"); Undo.RegisterCreatedObjectUndo(go,"Create local rain");
                go.transform.SetParent(clouds.transform,false);
                local.rainParticles=go.AddComponent<ParticleSystem>();
                var ps=local.rainParticles; ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main; main.loop=true; main.playOnAwake=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.maxParticles=3000; main.startSpeed=0; main.startLifetime=1.4f; main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                var emission=ps.emission; emission.enabled=false;
                var shape=ps.shape; shape.enabled=false;
                var collision=ps.collision; collision.enabled=true; collision.type=ParticleSystemCollisionType.World;
                collision.mode=ParticleSystemCollisionMode.Collision3D; collision.quality=ParticleSystemCollisionQuality.Low;
                collision.lifetimeLoss=1; collision.enableDynamicColliders=false;
                var renderer=go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode=ParticleSystemRenderMode.Stretch; renderer.velocityScale=.045f; renderer.lengthScale=3;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
                const string path="Assets/MantaFlight/Materials/Rain streak.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material) { material=new Material(Shader.Find("Manta/Rain streak")); AssetDatabase.CreateAsset(material,path); }
                renderer.sharedMaterial=material;
            }
            local.ApplyMovementTilt();
            EditorUtility.SetDirty(d); EditorUtility.SetDirty(weather); EditorUtility.SetDirty(forecast); EditorUtility.SetDirty(local);
            EditorSceneManager.MarkSceneDirty(clouds.gameObject.scene); EditorSceneManager.SaveScene(clouds.gameObject.scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject=clouds.gameObject;
        }
        static MantaCloudPreset Preset(string name,MantaCloudPreset basis,float rain,float coverage,float density,float wind)
        {
            string path="Assets/MantaFlight/Settings/Cloud Weather/"+name+".asset";
            var p=AssetDatabase.LoadAssetAtPath<MantaCloudPreset>(path); if(p) return p;
            p=ScriptableObject.CreateInstance<MantaCloudPreset>(); p.name=name; p.appearance=basis.appearance;
            p.description="Rain-bearing grey cloud banks; precipitation only inside their projected footprints.";
            var a=p.appearance; a.rain=rain; a.coverage=coverage; a.density=density; a.thickness=1;
            a.wind=new Vector3(wind,.03f,wind*.25f); a.sunlight=new Color(.59f,.63f,.69f); a.shadow=new Color(.13f,.17f,.23f);
            p.appearance=a; AssetDatabase.CreateAsset(p,path); return p;
        }
        [MenuItem("Manta/Environment/Preview rain/Light")] static void Light()=>Preview(2);
        [MenuItem("Manta/Environment/Preview rain/Moderate")] static void Moderate()=>Preview(3);
        [MenuItem("Manta/Environment/Preview rain/Heavy")] static void Heavy()=>Preview(4);
        static void Preview(int index)
        {
            var f=Object.FindFirstObjectByType<MantaRainForecast>(); if(f) f.Preview(index);
        }
        public static string Validate()
        {
            var size=new Vector3(1000,500,1000); var center=new Vector3(0,800,0);
            if(MantaLocalRain.Influence(Vector3.zero,center,size,1,1)<=0 ||
                MantaLocalRain.Influence(new Vector3(400,0,0),center,size,1,1)!=0 ||
                MantaLocalRain.Influence(new Vector3(0,1000,0),center,size,1,1)!=0 ||
                MantaLocalRain.Influence(center,center,size,1,1)<=0)
                throw new System.Exception("Rain footprint failed.");
            for(int i=0;i<5;i++) for(int sign=-1;sign<=1;sign+=2)
                if(Mathf.Abs(MantaRainForecast.Adjacent(i,sign,5)-i)>1) throw new System.Exception("Forecast jump.");
            if(ShaderUtil.ShaderHasError(Shader.Find("Manta/Rain streak"))) throw new System.Exception("Rain shader failed.");
            return "PASS: under-cloud, inside-cloud, outside-footprint, above-cloud, adjacent forecast transitions and rain shader.";
        }
    }
}
