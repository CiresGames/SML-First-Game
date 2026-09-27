using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MantaFlight.Editor
{
    public static class MantaCloudWeatherValidation
    {
        [MenuItem("Manta/Environment/Validate cloud weather")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            var report=new List<string>();
            var source=UnityEngine.Object.FindFirstObjectByType<MantaCloudWeather>();
            if(!source || source.sequence.Length!=5) throw new Exception("Install the five weather presets first.");
            var a=new GameObject("Weather validation A") { hideFlags=HideFlags.HideAndDontSave };
            var b=new GameObject("Weather validation B") { hideFlags=HideFlags.HideAndDontSave };
            try
            {
                var x=a.AddComponent<MantaCloudWeather>(); var y=b.AddComponent<MantaCloudWeather>();
                x.enabled=false; y.enabled=false;
                x.sequence=source.sequence; y.sequence=source.sequence;
                x.startingPreset=source.startingPreset; y.startingPreset=source.startingPreset;
                var sky=a.GetComponent<MantaCloudscape>(); var other=b.GetComponent<MantaCloudscape>();
                sky.enabled=false; other.enabled=false;
                x.RestartCycle(); y.RestartCycle();
                float firstHold=source.sequence[0].holdSeconds;
                float blend=source.sequence[1].transitionSeconds;
                x.Advance(firstHold+blend*.5f);
                var expected=CloudAppearance.Lerp(source.startingPreset.appearance,source.sequence[1].preset.appearance,.5f);
                Check(Mathf.Abs(sky.coverage-expected.coverage)<.0001f && x.IsTransitioning, "Mid-transition appearance is interpolated",report);
                var snapshot=sky.CaptureAppearance();
                x.TransitionTo(source.sequence[4].preset,10);
                Check(Mathf.Abs(sky.coverage-snapshot.coverage)<.0001f,"Interrupting a blend does not snap",report);
                x.Advance(5);
                Check(Mathf.Abs(sky.density-Mathf.Lerp(snapshot.density,source.sequence[4].preset.appearance.density,.5f))<.0001f,"Manual transition blends from current state",report);
                x.paused=true; float density=sky.density; x.Advance(100);
                Check(sky.density==density,"Pause freezes weather progression",report);
                x.ResumeCycle(); y.ResumeCycle();
                float total=0;
                foreach(var stage in source.sequence) total+=stage.holdSeconds+stage.transitionSeconds;
                x.Advance(total);
                Check(x.CurrentPreset==x.startingPreset && !x.IsTransitioning,"Full cycle loops to the starting preset",report);
                x.RestartCycle(); y.RestartCycle();
                float sample=firstHold+blend+source.sequence[1].holdSeconds+13;
                x.Advance(sample);
                int steps=Mathf.FloorToInt(sample/.25f);
                for(int i=0;i<steps;i++) y.Advance(.25f);
                y.Advance(sample-steps*.25f);
                Check(Mathf.Abs(sky.thickness-other.thickness)<.0001f && x.TargetPreset==y.TargetPreset,"Variable frame sizes preserve the timeline and blend",report);
                x.sequence=Array.Empty<CloudWeatherStage>(); x.RestartCycle(); x.Advance(1000);
                Check(!x.IsTransitioning,"Empty sequence safely holds the starting preset",report);
                x.sequence=new[]{new CloudWeatherStage{preset=null},new CloudWeatherStage{preset=source.startingPreset}};
                x.RestartCycle(); x.Advance(1000);
                Check(!x.IsTransitioning && x.CurrentPreset==source.startingPreset,"Null entries and single-preset sequences are safe",report);
                foreach(var stage in source.sequence)
                    Check(stage.preset && stage.preset.appearance.thickness>=.06f,"Preset available: "+stage.preset.name,report);
                return string.Join("\n",report);
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }
        static void Check(bool pass,string message,List<string> report)
        {
            if(!pass) throw new Exception("CLOUD WEATHER: "+message);
            report.Add("PASS "+message);
        }
    }
}
