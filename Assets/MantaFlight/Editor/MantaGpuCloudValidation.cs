using UnityEngine;
using UnityEditor;
using System.Linq;
namespace MantaFlight.Editor
{
    public static class MantaGpuCloudValidation
    {
        public static void BuildBenchmark()
        {
            // Defer the synchronous build so an Editor command can return first.
            EditorApplication.delayCall+=()=>
            {
                bool timing=PlayerSettings.enableFrameTimingStats;
                try
                {
                    PlayerSettings.enableFrameTimingStats=true;
                    System.IO.Directory.CreateDirectory("Builds/MantaGpuBenchmark");
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                        scenes=new[]{"Assets/MantaFlight/Scenes/MantaFlight.unity"},
                        locationPathName="Builds/MantaGpuBenchmark/MantaClouds.exe",
                        target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                    System.IO.File.WriteAllText("Logs/MantaGpuBuildFinal.txt",report.summary.result+" errors="+report.summary.totalErrors+" duration="+report.summary.totalTime+"\n"+
                        string.Join("\n",report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Error).Select(m=>m.content)));
                }
                catch(System.Exception e){System.IO.File.WriteAllText("Logs/MantaGpuBuildFinal.txt",e.ToString());}
                finally{PlayerSettings.enableFrameTimingStats=timing;AssetDatabase.SaveAssets();}
            };
        }
        // Synchronous readback belongs only in this explicit editor validation,
        // never in gameplay. Compare every map texel with the deterministic CPU mirror.
        public static string ValidateField()
        {
            var f=MantaGpuWeather.Active;f.EnsureMap();var old=RenderTexture.active;
            var texture=new Texture2D(256,256,TextureFormat.RGBAFloat,false,true);
            var appearance=f.Clouds.CaptureAppearance();
            try
            {
                RenderTexture.active=f.WeatherMap;texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();
                var pixels=texture.GetPixels();float error=0;
                for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                    error=Mathf.Max(error,Mathf.Abs(pixels[y*256+x].r-MantaGpuWeather.CoverageNoise((x+.5f)/8192,(y+.5f)/8192,f.Seed)));
                if(error>.002f)throw new System.Exception("Weather map mismatch: "+error);
                f.Clouds.rain=1;f.Clouds.coverage=.6f;int wet=0,dry=0;
                for(int z=0;z<32;z++)for(int x=0;x<32;x++)
                {
                    var p=new Vector3(x*f.worldSize/32,0,z*f.worldSize/32)+f.WindOffset;
                    float rain=f.SampleRain(p);if(rain>0)wet++;else dry++;
                    p.y=f.mainLayer.x+f.mainLayer.y+100;
                    if(f.SampleRain(p)!=0)throw new System.Exception("Rain above cloud layer");
                }
                if(wet==0 || dry==0)throw new System.Exception("Missing wet/dry regional variation");
                var renderer=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
                var feature=renderer.rendererFeatures.Find(v=>v is MantaGpuCloudFeature);bool active=feature.isActive;
                try{feature.SetActive(false);if(f.Ready)throw new System.Exception("Legacy fallback not enabled");}
                finally{feature.SetActive(active);}
                return "PASS: 65,536 CPU/GPU map samples, max error="+error+"; wet columns="+wet+", dry columns="+dry+"; no rain above layer; feature-toggle fallback.";
            }
            finally{RenderTexture.active=old;Object.DestroyImmediate(texture);f.Clouds.SetAppearance(appearance);}
        }
        public static string Capture(string label,float altitude=1700,float pitch=9,float coverage=.64f,float hour=12,bool gpu=true)
        {
            var f=MantaGpuWeather.Active;var c=f.Clouds;var day=MantaDayNightCycle.Active;var camera=Camera.main;
            var appearance=c.CaptureAppearance();var time=day.timeOfDay;var pos=camera.transform.position;var rotation=camera.transform.rotation;bool enabled=f.useGpuClouds;
            try
            {
                f.useGpuClouds=gpu;c.SetAppearance(c.GetComponent<MantaRainForecast>().forecasts[1].appearance);c.coverage=coverage;c.thickness=1;day.SetTime(hour);
                camera.transform.position=new Vector3(0,altitude,-1000);camera.transform.rotation=Quaternion.Euler(pitch,0,0);
                System.IO.Directory.CreateDirectory("Logs/MantaFlight/Lighting");
                return MantaAtmosphereBenchmark.Run("Logs/MantaFlight/Lighting/"+label+".png",1920,1080);
            }
            finally {f.useGpuClouds=enabled;c.SetAppearance(appearance);day.SetTime(time);camera.transform.SetPositionAndRotation(pos,rotation);c.Apply();}
        }
    }
}
