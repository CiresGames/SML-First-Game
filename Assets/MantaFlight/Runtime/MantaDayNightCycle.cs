using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MantaFlight
{
    [ExecuteAlways, DefaultExecutionOrder(-105)]
    public sealed class MantaDayNightCycle : MonoBehaviour
    {
        public static MantaDayNightCycle Active { get; private set; }
        [Header("Clock")]
        [Range(0,24)] public float timeOfDay = 17;
        public bool animate = true;
        [Min(1)] public float cycleMinutes = 18;
        [Range(-180,180)] public float sunAzimuth = -25;
        [Header("Lighting")]
        [Range(0,5)] public float daylightIntensity = 2.4f;
        [Range(0,1)] public float moonlightIntensity = .32f;
        [Range(0,2)] public float dayFillIntensity = .7f;
        [Range(0,1)] public float nightFillIntensity = .4f;
        public Color daylightColor = new Color(1,.96f,.86f);
        public Color sunsetColor = new Color(1,.48f,.2f);
        public Color moonlightColor = new Color(.4f,.58f,1);
        public Color dayZenith = new Color(.08f,.28f,.55f);
        public Color dayHorizon = new Color(.6f,.76f,.86f);
        public Color duskHorizon = new Color(.85f,.34f,.16f);
        public Color nightZenith = new Color(.015f,.026f,.065f);
        public Color nightHorizon = new Color(.065f,.095f,.16f);
        [Header("Atmosphere")]
        [Range(0,.003f)] public float dayFogDensity = .0005f;
        [Range(0,.003f)] public float nightFogDensity = .0008f;
        [Range(0,3)] public float starBrightness = 1.2f;
        [Header("God rays")]
        public bool godRays = true;
        [Range(0,3)] public float rayIntensity = .8f;
        [Tooltip("Forward scattering anisotropy: 0 is isotropic; higher values concentrate light toward the sun.")]
        [Range(0,.85f)] public float raySpread = .6f;
        [Tooltip("Atmospheric extinction per metre at sea level.")]
        [Range(.00001f,.002f)] public float airExtinction = .00035f;
        [Range(100,3000)] public float airHeight = 800;
        [Range(100,3000)] public float rayDistance = 1400;
        [Range(16,64)] public int raySamples = 48;
        [Header("Scene references")]
        public Light sun, moon, skyFill;
        public Material skyMaterial;
        public Volume grading;
        public MantaCloudscape clouds;
        Material runtimeSky;
        bool dirty=true;
        public Vector3 SunDirection { get; private set; }
        public float Daylight { get; private set; }
        public float RayStrength { get; private set; }
        public Color RayColor { get; private set; }

        void OnEnable() { Active=this; dirty=true; }
        void OnValidate() { dirty=true; }
        void OnDisable()
        {
            if(Active==this) Active=null;
            if(clouds) clouds.SetEnvironmentLighting(Color.white,Color.white);
            if(runtimeSky)
            {
                if(RenderSettings.skybox==runtimeSky) RenderSettings.skybox=skyMaterial;
                CoreUtils.Destroy(runtimeSky); runtimeSky=null;
            }
        }
        void Update()
        {
            if(Application.isPlaying)
            {
                if(animate) timeOfDay=AdvanceClock(timeOfDay,Time.deltaTime,cycleMinutes);
                ApplyLighting();
            }
            else if(dirty) { dirty=false; ApplyLighting(); }
        }
        public static float AdvanceClock(float hour,float seconds,float minutes) =>
            Mathf.Repeat(hour+seconds*24/(Mathf.Max(1,minutes)*60),24);
        public void SetTime(float hour) { timeOfDay=Mathf.Repeat(hour,24); ApplyLighting(); }

        public void ApplyLighting()
        {
            if(!sun || !moon || !skyMaterial) return;
            Active=this;
            timeOfDay=Mathf.Repeat(timeOfDay,24);
            sun.transform.rotation=Quaternion.Euler((timeOfDay-6)*15,sunAzimuth,0);
            SunDirection=-sun.transform.forward;
            moon.transform.rotation=Quaternion.LookRotation(SunDirection,
                Mathf.Abs(Vector3.Dot(SunDirection,Vector3.up))>.99f ? Vector3.forward : Vector3.up);
            float elevation=SunDirection.y;
            Daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.16f,.22f,elevation));
            float highSun=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.65f,elevation));
            float dusk=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.38f,Mathf.Abs(elevation))))*Daylight;
            float storm=clouds ? Mathf.InverseLerp(.95f,1.5f,clouds.density) : 0;
            sun.color=Color.Lerp(sunsetColor,daylightColor,highSun);
            sun.intensity=daylightIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.03f,.4f,elevation))*(1-storm*.45f);
            sun.enabled=elevation>-.04f;
            moon.color=moonlightColor;
            moon.intensity=moonlightIntensity*(1-Daylight);
            moon.enabled=Daylight<.99f;
            if(skyFill)
            {
                // Broad southern sky bounce keeps the main flight corridor readable.
                skyFill.transform.rotation=Quaternion.Euler(38,0,0);
                skyFill.intensity=Mathf.Lerp(nightFillIntensity,dayFillIntensity,Daylight);
                skyFill.color=Color.Lerp(new Color(.42f,.58f,.85f),new Color(.69f,.8f,1),Daylight);
            }
            RenderSettings.sun=Daylight>.15f ? sun : moon;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.045f,.07f,.14f),new Color(.37f,.49f,.63f),Daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.025f,.045f,.09f),Color.Lerp(new Color(.36f,.24f,.18f),new Color(.28f,.34f,.4f),highSun),Daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.018f,.027f,.045f),new Color(.12f,.14f,.14f),Daylight);
            // Update the ambient probe explicitly: dynamic time-of-day must not rely on
            // a stale baked sky probe or asynchronous environment readback.
            var ambient=new SphericalHarmonicsL2();
            ambient.AddAmbientLight(Color.Lerp(new Color(.014f,.025f,.05f),new Color(.23f,.27f,.34f),Daylight));
            ambient.AddDirectionalLight(Vector3.up,RenderSettings.ambientSkyColor,.35f);
            RenderSettings.ambientProbe=ambient;
            Color horizon=Color.Lerp(nightHorizon,Color.Lerp(dayHorizon,duskHorizon,dusk),Daylight);
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=Color.Lerp(horizon,new Color(.3f,.35f,.41f),storm*.4f);
            // The volume integrates daytime extinction itself; retain legacy fog at night.
            RenderSettings.fogDensity=Mathf.Lerp(nightFogDensity,godRays ? 0 : dayFogDensity,Daylight);
            RenderSettings.reflectionIntensity=Mathf.Lerp(.18f,.7f,Daylight);
            Material sky=skyMaterial;
            if(Application.isPlaying)
            {
                if(!runtimeSky) runtimeSky=new Material(skyMaterial){hideFlags=HideFlags.DontSave};
                sky=runtimeSky;
            }
            sky.SetColor("_Zenith",Color.Lerp(nightZenith,dayZenith,Daylight));
            sky.SetColor("_Horizon",horizon);
            sky.SetColor("_Ground",RenderSettings.fogColor*.7f);
            sky.SetVector("_SunDirection",SunDirection); sky.SetVector("_MoonDirection",-SunDirection);
            sky.SetColor("_SunColor",sun.color*3.5f);
            sky.SetColor("_MoonColor",moonlightColor*1.5f);
            sky.SetFloat("_Daylight",Daylight); sky.SetFloat("_Stars",starBrightness);
            RenderSettings.skybox=sky;
            if(clouds)
                clouds.SetEnvironmentLighting(
                    Color.Lerp(new Color(.16f,.25f,.44f),Color.Lerp(new Color(1.3f,.65f,.35f),new Color(1.15f,1.1f,1),highSun),Daylight),
                    Color.Lerp(new Color(.16f,.24f,.4f),new Color(.72f,.83f,1),Daylight));
            if(grading)
            {
                var profile=Application.isPlaying ? grading.profile : grading.sharedProfile;
                if(profile && profile.TryGet<ColorAdjustments>(out var color)) color.postExposure.value=Mathf.Lerp(.8f,.1f,Daylight);
            }
            RayStrength=godRays ? rayIntensity*Daylight*Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,elevation))*(1-storm*.55f) : 0;
            // The volume applies its own slant-path solar extinction. Feeding the
            // already horizon-dimmed surface light here attenuated sunset twice.
            RayColor=sun.color*daylightIntensity;
        }
    }
}
