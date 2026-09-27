using System;
using UnityEngine;

namespace MantaFlight
{
    [Serializable]
    public sealed class CloudWeatherStage
    {
        public MantaCloudPreset preset;
        [Min(.1f)] public float holdSeconds = 90;
        [Tooltip("Time to blend INTO this stage.")][Min(.1f)] public float transitionSeconds = 45;
    }

    [RequireComponent(typeof(MantaCloudscape))]
    [DefaultExecutionOrder(-110)]
    public sealed class MantaCloudWeather : MonoBehaviour
    {
        [Tooltip("Appearance at Play-mode startup. Use Preview starting preset to apply it in Edit mode.")]
        public MantaCloudPreset startingPreset;
        [Tooltip("Ordered loop. Each entry holds, then blends into the next valid entry; null entries are skipped.")]
        public CloudWeatherStage[] sequence = Array.Empty<CloudWeatherStage>();
        [Tooltip("Automatically loop the sequence. Manual TransitionTo calls turn this off.")]
        public bool automaticCycle = true;
        [Tooltip("Freeze the weather blend and hold timer. Wind still drifts; pausing game time freezes both.")]
        public bool paused;
        [Tooltip("Multiplier for hold and transition timers; 0 freezes progression. Uses scaled game time.")]
        [Range(0, 20)] public float cycleSpeed = 1;
        MantaCloudscape clouds;
        CloudAppearance from, destination;
        bool initialized, transitioning;
        float elapsed, duration, hold = 90;
        int index = -1, nextIndex = -1;
        public bool IsTransitioning => transitioning;
        public float TransitionProgress => transitioning ? Mathf.Clamp01(elapsed / duration) : 1;
        public MantaCloudPreset CurrentPreset { get; private set; }
        public MantaCloudPreset TargetPreset { get; private set; }

        void OnEnable() { clouds = GetComponent<MantaCloudscape>(); if (Application.isPlaying) RestartCycle(); }
        void Update() => Advance(Time.deltaTime * Mathf.Max(0,cycleSpeed));

        [ContextMenu("Preview starting preset")]
        public void PreviewStartingPreset()
        {
            clouds = GetComponent<MantaCloudscape>();
            if (startingPreset) clouds.SetAppearance(startingPreset.appearance);
        }

        [ContextMenu("Resume and restart weather cycle")]
        public void ResumeCycle()
        {
            automaticCycle=true; paused=false; RestartCycle();
        }

        public void RestartCycle()
        {
            clouds = GetComponent<MantaCloudscape>();
            elapsed=0; transitioning=false; index=-1; nextIndex=-1;
            CurrentPreset=startingPreset;
            for (int i=0; i<(sequence?.Length ?? 0); i++)
                if (sequence[i]?.preset && (!CurrentPreset || sequence[i].preset==CurrentPreset))
                { index=i; CurrentPreset=sequence[i].preset; break; }
            hold=index>=0 ? Mathf.Max(.1f,sequence[index].holdSeconds) : 90;
            if (CurrentPreset) clouds.SetAppearance(CurrentPreset.appearance);
            TargetPreset=CurrentPreset; initialized=true;
        }

        // Manual requests take over from the cycle and blend from the current appearance,
        // including when another transition is only partly complete.
        public void TransitionTo(MantaCloudPreset preset, float seconds=45)
        {
            if (!preset) return;
            if (!initialized) RestartCycle();
            automaticCycle=false; nextIndex=-1;
            Begin(preset,seconds);
        }

        void Begin(MantaCloudPreset preset,float seconds)
        {
            from=clouds.CaptureAppearance(); destination=preset.appearance;
            TargetPreset=preset; elapsed=0; duration=Mathf.Max(.1f,seconds); transitioning=true;
        }

        int FindNext()
        {
            int length=sequence?.Length ?? 0;
            for(int n=1;n<=length;n++)
            {
                int candidate=(index+n)%length;
                if(sequence[candidate]?.preset) return candidate;
            }
            return -1;
        }

        public void Advance(float seconds)
        {
            if(paused || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if(!initialized) RestartCycle();
            // Consume overshoot so variable frame times cannot stretch or skip a phase.
            while(seconds>0)
            {
                if(transitioning)
                {
                    float step=Mathf.Min(seconds,Mathf.Max(0,duration-elapsed));
                    elapsed+=step; seconds-=step;
                    float t=Mathf.Clamp01(elapsed/duration);
                    clouds.SetAppearance(CloudAppearance.Lerp(from,destination,t*t*(3-2*t)));
                    if(elapsed<duration) return;
                    CurrentPreset=TargetPreset; index=nextIndex; elapsed=0; transitioning=false;
                    hold=index>=0 && index<(sequence?.Length ?? 0) && sequence[index]!=null
                        ? Mathf.Max(.1f,sequence[index].holdSeconds) : 90;
                }
                else
                {
                    if(!automaticCycle) return;
                    int next=FindNext();
                    if(next<0 || next==index) return;
                    float step=Mathf.Min(seconds,Mathf.Max(0,hold-elapsed));
                    elapsed+=step; seconds-=step;
                    if(elapsed<hold) return;
                    nextIndex=next;
                    Begin(sequence[next].preset,sequence[next].transitionSeconds);
                }
            }
        }
    }
}
