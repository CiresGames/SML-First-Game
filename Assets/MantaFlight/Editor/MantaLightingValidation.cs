using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MantaFlight.Editor
{
    public static class MantaLightingValidation
    {
        [MenuItem("Manta/Environment/Validate lighting")]
        public static string Validate()
        {
            var cycle=Object.FindFirstObjectByType<MantaDayNightCycle>();
            Require(cycle && cycle.sun && cycle.moon && cycle.skyFill,"Lighting references");
            float hour=cycle.timeOfDay; bool rays=cycle.godRays;
            Color authoredSun=cycle.clouds.sunlight;
            try
            {
                Require(Mathf.Abs(MantaDayNightCycle.AdvanceClock(23,90,18)-1)<.001f,"Midnight wrap");
                Require(MantaDayNightCycle.AdvanceClock(12,0,18)==12,"Paused clock");
                cycle.godRays=true; cycle.SetTime(12);
                Require(cycle.Daylight>.99f && cycle.sun.enabled && cycle.sun.intensity>0,"Noon sunlight");
                Require(!cycle.moon.enabled && cycle.RayStrength>0,"Day shafts");
                cycle.SetTime(0);
                Require(cycle.Daylight<.01f && !cycle.sun.enabled && cycle.moon.enabled,"Night moonlight");
                Require(cycle.RayStrength==0 && cycle.moon.intensity>0,"Night shafts disabled");
                Require(cycle.clouds.sunlight==authoredSun,"Weather colors preserved");
                var bank=cycle.clouds.GetComponentInChildren<MeshRenderer>();
                Require(bank,"Generated clouds present");
                var block=new MaterialPropertyBlock(); bank.GetPropertyBlock(block);
                Require(block.GetColor("_SunTint").maxColorComponent<authoredSun.maxColorComponent,"Night cloud shading");
                cycle.SetTime(12); cycle.godRays=false; cycle.ApplyLighting();
                Require(cycle.RayStrength==0,"Shaft toggle");
                var camera=Camera.main.GetUniversalAdditionalCameraData();
                Require(camera.renderPostProcessing && camera.requiresDepthTexture,"Camera effects");
                Require(cycle.grading.sharedProfile.TryGet<Tonemapping>(out var tone) && tone.mode.overrideState,"Tone mapping override");
                Require(cycle.grading.sharedProfile.TryGet<Bloom>(out var bloom) && bloom.intensity.overrideState,"Bloom override");
                Require(!ShaderUtil.ShaderHasError(cycle.skyMaterial.shader),"Sky shader compilation");
                Require(!ShaderUtil.ShaderHasError(Shader.Find("Hidden/Manta/God Rays")),"Shaft shader compilation");
                return "PASS: clock wrap/pause, sun/moon, shafts, cloud lighting, camera, grading and shaders.";
            }
            finally { cycle.godRays=rays; cycle.SetTime(hour); }
        }
        static void Require(bool condition,string label)
        {
            if(!condition) throw new System.InvalidOperationException("Lighting validation failed: "+label);
        }
    }
}
