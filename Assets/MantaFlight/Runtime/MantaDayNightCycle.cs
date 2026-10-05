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
        [Range(0,4)] public float milkyWayBrightness=1.4f;
        [Range(0,.3f)] public float starTwinkle=.1f;
        [Range(-90,90)] public float celestialLatitude=45;
        [Range(0,360)] public float starMapRotation=35;
        [Header("Occasional northern lights")]
        public bool auroraEnabled=true;
        [Tooltip("Editor preview; leave off for occasional events during play.")]
        public bool previewAurora;
        [Tooltip("Band count used only when Preview Aurora is enabled. Normal events randomly choose 1–4.")]
        [Range(1,4)] public int previewAuroraBands=3;
        [Range(0,3)] public float auroraBrightness=1.3f;
        [Range(-180,180)] public float auroraHeading=15;
        public Vector2 auroraQuietSeconds=new Vector2(180,360);
        public Vector2 auroraDurationSeconds=new Vector2(70,130);
        public int auroraSeed=270927;
        [Range(0,1)] public float auroraShapeVariation=1;
        [Range(0,3)] public float auroraDriftSpeed=1;
        [Tooltip("Strength of traveling ripples and the sideways bend of auroral rays.")]
        [Range(0,2)] public float auroraWaveAmount=1;
        [Tooltip("Speed of curtain undulation, independent of the broad shape drift.")]
        [Range(0,3)] public float auroraWaveSpeed=1;
        [Tooltip("World scale of the auroral sheets. Lower values exaggerate flight parallax.")]
        [Range(.5f,4)] public float auroraDistanceScale=1;
        readonly Vector4[] auroraShapes=new Vector4[4];
        int auroraBands=1;
        public int AuroraBandCount => previewAurora ? Mathf.Clamp(previewAuroraBands,1,4) : auroraBands;
        System.Random auroraRandom;
        float auroraElapsed,auroraDuration,auroraMotion;
        bool auroraActive;
        public float AuroraStrength {get; private set;}
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

        void OnEnable() { Active=this; dirty=true; auroraRandom=new System.Random(auroraSeed); auroraElapsed=0; auroraActive=false; auroraDuration=PickAurora(auroraQuietSeconds); }
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
                AdvanceAurora(Time.deltaTime);
                ApplyLighting();
            }
            else if(dirty) { dirty=false; ApplyLighting(); }
        }
        public static float AdvanceClock(float hour,float seconds,float minutes) =>
            Mathf.Repeat(hour+seconds*24/(Mathf.Max(1,minutes)*60),24);
        public void SetTime(float hour) { timeOfDay=Mathf.Repeat(hour,24); ApplyLighting(); }
        float PickAurora(Vector2 range)=>Mathf.Lerp(Mathf.Max(20,Mathf.Min(range.x,range.y)),Mathf.Max(20,Mathf.Max(range.x,range.y)),(float)auroraRandom.NextDouble());
        public static float AuroraEnvelope(float elapsed,float duration)
        {
            float fade=Mathf.Min(20,duration*.3f);
            return Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/fade))*Mathf.SmoothStep(0,1,Mathf.Clamp01((duration-elapsed)/fade));
        }
        public Vector4 AuroraShape(int curtain,float seconds)
        {
            float seed=((uint)auroraSeed%10007)*.017f+curtain*31.7f;
            float t=seconds*.025f*auroraDriftSpeed;
            float amount=auroraShapeVariation;
            // Each curtain drifts and expands independently; no per-pixel noise cost.
            float width=Mathf.Lerp(.58f,1.35f,Mathf.PerlinNoise(seed,t*.7f+13));
            float height=Mathf.Lerp(.45f,1.8f,Mathf.PerlinNoise(seed+43,t*.9f+37));
            float drift=(Mathf.PerlinNoise(seed+89,t*.55f+71)-.5f)*1.1f;
            float lift=(Mathf.PerlinNoise(seed+137,t*.63f+97)-.5f)*.22f;
            return new Vector4(Mathf.Lerp(1,width,amount),Mathf.Lerp(1,height,amount),drift*amount,lift*amount);
        }
        public void AdvanceAurora(float seconds)
        {
            if(seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || !auroraEnabled || Daylight>.15f) return;
            auroraMotion+=seconds;
            auroraElapsed+=seconds;
            while(auroraElapsed>=auroraDuration)
            {
                auroraElapsed-=auroraDuration; auroraActive=!auroraActive;
                if(auroraActive) auroraBands=auroraRandom.Next(1,5);
                auroraDuration=PickAurora(auroraActive ? auroraDurationSeconds : auroraQuietSeconds);
            }
        }

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
            float overcast=clouds ? Mathf.Clamp01(clouds.overcast) : 0;
            sun.color=Color.Lerp(sunsetColor,daylightColor,highSun);
            sun.intensity=daylightIntensity*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.03f,.4f,elevation))*(1-storm*.45f);
            sun.enabled=elevation>-.04f;
            sun.intensity*=Mathf.Lerp(1,.18f,overcast);
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
            sky.SetFloat("_Overcast",overcast);
            sky.SetColor("_Horizon",horizon);
            sky.SetColor("_Ground",RenderSettings.fogColor*.7f);
            sky.SetVector("_SunDirection",SunDirection); sky.SetVector("_MoonDirection",-SunDirection);
            sky.SetColor("_SunColor",sun.color*3.5f*(1-overcast*.97f));
            sky.SetColor("_MoonColor",moonlightColor*1.5f);
            sky.SetFloat("_Daylight",Daylight); sky.SetFloat("_Stars",starBrightness);
            sky.SetFloat("_GalaxyBrightness",milkyWayBrightness); sky.SetFloat("_Twinkle",starTwinkle);
            sky.SetMatrix("_StarRotation",Matrix4x4.Rotate(Quaternion.Euler(0,timeOfDay*15+starMapRotation,0)*Quaternion.Euler(90-celestialLatitude,0,0)));
            float eventStrength=previewAurora ? 1 : (auroraActive ? AuroraEnvelope(auroraElapsed,auroraDuration) : 0);
            AuroraStrength=auroraEnabled ? eventStrength*auroraBrightness*Mathf.Pow(1-Daylight,4) : 0;
            sky.SetVector("_Aurora",new Vector4(AuroraStrength,auroraMotion,auroraHeading*Mathf.Deg2Rad,auroraSeed*.0137f));
            for(int curtain=0;curtain<AuroraBandCount;curtain++) auroraShapes[curtain]=AuroraShape(curtain,auroraMotion);
            sky.SetVectorArray("_AuroraShapes",auroraShapes);
            sky.SetFloat("_AuroraBandCount",AuroraBandCount);
            sky.SetVector("_AuroraAnchor",new Vector4(transform.position.x,transform.position.y,transform.position.z,auroraDistanceScale));
            sky.SetVector("_AuroraWaves",new Vector4(auroraWaveAmount,auroraMotion*.35f*auroraWaveSpeed,0,0));
            RenderSettings.skybox=sky;
            if(clouds)
                clouds.SetEnvironmentLighting(
                    Color.Lerp(Color.Lerp(new Color(.16f,.25f,.44f),Color.Lerp(new Color(1.3f,.65f,.35f),new Color(1.15f,1.1f,1),highSun),Daylight),Color.Lerp(new Color(.12f,.16f,.23f),new Color(.7f,.74f,.8f),Daylight),overcast),
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
            RayStrength*=1-overcast*.85f;
        }
    }
}
