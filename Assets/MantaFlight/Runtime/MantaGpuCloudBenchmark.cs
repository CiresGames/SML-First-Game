using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace MantaFlight
{
    // Opt-in standalone validation. Normal game sessions never instantiate this object.
    [DefaultExecutionOrder(32000)]
    public sealed class MantaGpuCloudBenchmark : MonoBehaviour
    {
        [Serializable] public class Result {public string name;public int frames;public double medianFrameMs,p95FrameMs,medianGpuMs;public int gpuSamples;}
        [Serializable] public class Report {public string gpu,measurement;public int width,height;public List<Result> results=new List<Result>();}
        Camera viewer;Vector3 anchor;bool moving;float began;readonly FrameTiming[] timing=new FrameTiming[1];
        bool offscreen;RenderTexture target;Texture2D syncPixel;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        {if(Array.IndexOf(Environment.GetCommandLineArgs(),"--manta-cloud-benchmark")>=0)new GameObject("Cloud benchmark").AddComponent<MantaGpuCloudBenchmark>();}
        void LateUpdate()
        {
            if(!viewer)return;
            float t=Time.unscaledTime-began;viewer.transform.SetPositionAndRotation(anchor+(moving ? new Vector3(t*90,Mathf.Sin(t*.4f)*100,t*45) : Vector3.zero),Quaternion.Euler(8,moving ? Mathf.Sin(t*.3f)*35 : 0,0));
            if(offscreen)
            {
                // Hidden Windows players may skip presenting/rendering entirely.
                // Force a real GPU render and completion; report this separately
                // from ordinary gameplay timing because readback adds overhead.
                viewer.Render();var old=RenderTexture.active;RenderTexture.active=target;
                syncPixel.ReadPixels(new Rect(0,0,1,1),0,0,false);RenderTexture.active=old;
            }
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(3);
            // Apply after scene startup. Some platform/window initialization can
            // override the first request before the first rendered frame.
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(2);
            var f=MantaGpuWeather.Active;if(!f){Application.Quit(2);yield break;}viewer=Camera.main;
            offscreen=Array.IndexOf(Environment.GetCommandLineArgs(),"--manta-cloud-offscreen")>=0;
            if(offscreen){target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf);target.Create();viewer.targetTexture=target;syncPixel=new Texture2D(1,1,TextureFormat.RGB24,false);}
            foreach(var script in viewer.GetComponents<MonoBehaviour>())if(script.GetType().Namespace=="MantaFlight")script.enabled=false;
            var c=f.Clouds;var forecast=c.GetComponent<MantaRainForecast>();forecast.BeginSession(90210);forecast.automatic=false;c.GetComponent<MantaCloudWeather>().enabled=false;
            var day=MantaDayNightCycle.Active;day.animate=false;day.SetTime(14);
            var report=new Report{gpu=SystemInfo.graphicsDeviceName,width=Screen.width,height=Screen.height,measurement=offscreen ? "Offscreen scene render plus synchronous GPU readback and simulation; not gameplay FPS" : "Standalone presented frame timings"};
            string root=Path.Combine(Application.dataPath,"../BenchmarkResults");Directory.CreateDirectory(root);
            for(int scenario=0;scenario<7;scenario++)
            {
                f.useGpuClouds=scenario!=0;moving=scenario==4;anchor=new Vector3(0,scenario==3 ? 750 : scenario==5 ? 1350 : 2300,-1000);began=Time.unscaledTime;
                c.SetAppearance(forecast.forecasts[scenario==3 ? 6 : 1].appearance);c.coverage=scenario==2 || scenario==3 ? .95f : .55f;c.thickness=1;day.SetTime(scenario==5 ? 18 : scenario==6 ? 2 : scenario==3 ? 15 : 14);
                for(int j=0;j<90;j++)yield return null;
                var wall=new List<double>();var gpu=new List<double>();double last=Time.realtimeSinceStartupAsDouble;
                for(int j=0;j<240;j++)
                {
                    FrameTimingManager.CaptureFrameTimings();yield return null;
                    double now=Time.realtimeSinceStartupAsDouble;wall.Add((now-last)*1000);last=now;
                    if(FrameTimingManager.GetLatestTimings(1,timing)>0 && timing[0].gpuFrameTime>0)gpu.Add(timing[0].gpuFrameTime);
                }
                wall.Sort();gpu.Sort();string name=new[]{"Legacy fair","GPU fair","GPU overcast","GPU inside storm","GPU fast flight","GPU sunset","GPU night"}[scenario];
                report.width=offscreen ? target.width : Screen.width;report.height=offscreen ? target.height : Screen.height;
                report.results.Add(new Result{name=name,frames=wall.Count,medianFrameMs=wall[wall.Count/2],p95FrameMs=wall[(int)(wall.Count*.95)],medianGpuMs=gpu.Count>0 ? gpu[gpu.Count/2] : -1,gpuSamples=gpu.Count});
                File.WriteAllText(Path.Combine(root,"results.json"),JsonUtility.ToJson(report,true));
                string capture=Path.Combine(root,"case-"+scenario+".png");
                if(offscreen)
                {
                    var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(capture,image.EncodeToPNG());Destroy(image);RenderTexture.active=old;
                }
                else ScreenCapture.CaptureScreenshot(capture);
                yield return new WaitForSecondsRealtime(.3f);
            }
            Application.Quit();
        }
        void OnDestroy(){if(viewer && viewer.targetTexture==target)viewer.targetTexture=null;if(target){target.Release();Destroy(target);}if(syncPixel)Destroy(syncPixel);}
    }
}
