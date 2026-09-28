using System;
using UnityEngine;
namespace MantaFlight
{
    [Serializable]
    public sealed class MantaForecastRules
    {
        [Tooltip("Label for the ordered weather state.")] public string name;
        [Tooltip("Minimum/maximum dwell time after blending, in seconds.")] public Vector2 holdSeconds=new Vector2(120,240);
        [Tooltip("Minimum/maximum time to enter this state, in seconds.")] public Vector2 transitionSeconds=new Vector2(50,90);
        [Tooltip("Relative chance that a new session starts in this state.")][Min(0)] public float initialWeight=1;
        [Tooltip("Weights indexed like Forecasts. Only the same state or adjacent states are allowed, preventing fair-to-storm jumps.")] public float[] transitionWeights;
        [Tooltip("State wind speed multiplier range; the selected value blends with cloud appearance.")] public Vector2 windMultiplier=new Vector2(.7f,1.3f);
        [Tooltip("Maximum wind direction change per front, in degrees.")][Range(0,90)] public float windTurn=35;
    }

    [RequireComponent(typeof(MantaCloudWeather)),DefaultExecutionOrder(-115)]
    public sealed class MantaRainForecast : MonoBehaviour
    {
        [Tooltip("Ordered progression: fair, overcast, light rain, moderate rain, heavy rain. Existing cloud presets define density, coverage, rain and base wind.")]
        public MantaCloudPreset[] forecasts;
        [Tooltip("Generate a fresh seed at each Play/session start. Disable for reproducible weather.")]
        public bool randomizeSessionSeed=true;
        [Tooltip("Fixed session/world seed when randomization is disabled.")] public int seed=90210;
        [SerializeField,Tooltip("Actual seed for this session. Copy it into Seed and disable randomization to replay.")] int sessionSeed;
        public int SessionSeed=>sessionSeed;
        [Tooltip("Choose initial weather using per-state Initial Weight.")] public bool randomizeStartingForecast=true;
        [Range(0,4)] public int startingForecast=2;
        public bool automatic=true;
        [Tooltip("Fallback dwell range for states without an override.")] public Vector2 holdSeconds=new Vector2(120,240);
        [Tooltip("Fallback blend range for states without an override.")] public Vector2 transitionSeconds=new Vector2(50,90);
        public MantaForecastRules[] stateRules;
        [Tooltip("Extra preference for fronts to continue developing or clearing.")][Range(0,3)] public float persistenceBias=.7f;
        [Tooltip("Smooth wind speed/direction variation between forecast transitions.")][Range(0,.5f)] public float gustStrength=.18f;
        [Tooltip("Time scale of coherent wind fluctuations, in seconds.")][Min(1)] public float gustPeriod=45;
        [Tooltip("State to enter through the Force debug forecast context menu.")][Min(0)] public int debugForecast=2;
        public string CurrentForecast => weather && weather.TargetPreset ? weather.TargetPreset.name : "";
        public int CurrentIndex=>current;
        MantaCloudWeather weather;
        System.Random random;
        int current,trend=1;
        float remaining,windHeading,gustTime;
        bool started;
        void Start() { if(!started) BeginSession(randomizeSessionSeed ? Guid.NewGuid().GetHashCode() : seed); }
        void OnDisable() { var clouds=GetComponent<MantaCloudscape>();if(clouds)clouds.windVariation=Vector3.zero; }
        MantaForecastRules Rules(int index)=>stateRules!=null && index>=0 && index<stateRules.Length ? stateRules[index] : null;
        float Pick(Vector2 range,float minimum=1)=>Mathf.Lerp(Mathf.Max(minimum,Mathf.Min(range.x,range.y)),Mathf.Max(minimum,Mathf.Max(range.x,range.y)),(float)random.NextDouble());
        public static int Adjacent(int current,int trend,int count)=>Mathf.Clamp(current+trend,0,count-1);

        // Call once from a new-game/world-seed system. Never regenerate banks on state changes.
        public void BeginSession(int worldSeed)
        {
            weather=GetComponent<MantaCloudWeather>();
            if(forecasts==null || forecasts.Length==0) return;
            sessionSeed=worldSeed; random=new System.Random(worldSeed);trend=1;
            gustTime=0;GetComponent<MantaCloudscape>().ResetSessionMotion();
            windHeading=Pick(new Vector2(-180,180),-180);
            var distribution=GetComponent<MantaCloudDistribution>();
            if(distribution) {distribution.seed=unchecked(worldSeed*397^73421);distribution.Regenerate();}
            current=Mathf.Clamp(startingForecast,0,forecasts.Length-1);
            if(randomizeStartingForecast) current=ChooseInitial();
            if(!forecasts[current]) {current=Array.FindIndex(forecasts,p=>p);if(current<0)return;}
            weather.automaticCycle=false;weather.startingPreset=forecasts[current];weather.RestartCycle();
            GetComponent<MantaCloudscape>().SetAppearance(Appearance(current));
            remaining=Pick(Rules(current)?.holdSeconds??holdSeconds);started=true;
        }
        int ChooseInitial()
        {
            float total=0;
            for(int i=0;i<forecasts.Length;i++) if(forecasts[i]) total+=Mathf.Max(0,Rules(i)?.initialWeight??1);
            if(total<=0) return Mathf.Clamp(startingForecast,0,forecasts.Length-1);
            float draw=(float)random.NextDouble()*total;
            for(int i=0;i<forecasts.Length;i++) if(forecasts[i]) {draw-=Mathf.Max(0,Rules(i)?.initialWeight??1);if(draw<0)return i;}
            return 0;
        }
        public int ChooseNext()
        {
            float total=0;
            for(int i=Mathf.Max(0,current-1);i<=Mathf.Min(forecasts.Length-1,current+1);i++) total+=Weight(i);
            if(total<=0)return current;
            float draw=(float)random.NextDouble()*total;
            for(int i=Mathf.Max(0,current-1);i<=Mathf.Min(forecasts.Length-1,current+1);i++) {draw-=Weight(i);if(draw<0)return i;}
            return current;
        }
        float Weight(int next)
        {
            if(!forecasts[next])return 0;
            var weights=Rules(current)?.transitionWeights;
            float weight=weights!=null && next<weights.Length ? Mathf.Max(0,weights[next]) : next==current ? .2f : 1;
            return weight*(next-current==trend ? 1+persistenceBias : 1);
        }
        CloudAppearance Appearance(int index)
        {
            var appearance=forecasts[index].appearance;
            var rules=Rules(index);
            windHeading+=Pick(new Vector2(-(rules?.windTurn??35),rules?.windTurn??35),-90);
            float speed=new Vector2(appearance.wind.x,appearance.wind.z).magnitude*Pick(rules?.windMultiplier??new Vector2(.7f,1.3f),0);
            float angle=windHeading*Mathf.Deg2Rad;
            appearance.wind=new Vector3(Mathf.Cos(angle)*speed,appearance.wind.y,Mathf.Sin(angle)*speed);
            return appearance;
        }
        void Update()
        {
            AdvanceForecast(Time.deltaTime);
            AdvanceWind(Time.deltaTime);
        }
        public void AdvanceWind(float seconds)
        {
            if(!started || weather.paused)return;
            if(seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds))return;
            gustTime+=seconds*Mathf.Max(0,weather.cycleSpeed);
            var clouds=GetComponent<MantaCloudscape>();
            float offset=((uint)sessionSeed%10007)*.031f;
            float t=gustTime/Mathf.Max(1,gustPeriod);
            float speed=1+(Mathf.PerlinNoise(offset,t)-.5f)*2*gustStrength;
            float angle=(Mathf.PerlinNoise(offset+73,t*.7f)-.5f)*60*gustStrength;
            // Keep gusts separate from the authored wind to avoid feeding changes back into blends.
            var varied=Quaternion.AngleAxis(angle,Vector3.up)*new Vector3(clouds.wind.x,0,clouds.wind.z)*speed;
            clouds.windVariation=varied-new Vector3(clouds.wind.x,0,clouds.wind.z);
        }
        public void AdvanceForecast(float seconds)
        {
            if(!started || !automatic || weather.paused || weather.IsTransitioning || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds))return;
            remaining-=seconds*Mathf.Max(0,weather.cycleSpeed);
            if(remaining>0)return;
            int next=ChooseNext();
            if(next!=current)trend=next>current ? 1 : -1;
            current=next;
            weather.TransitionToAppearance(forecasts[current],Appearance(current),Pick(Rules(current)?.transitionSeconds??transitionSeconds));
            remaining=Pick(Rules(current)?.holdSeconds??holdSeconds);
        }
        [ContextMenu("Force debug forecast (smooth transition)")] void ForceDebug()=>Preview(debugForecast);
        [ContextMenu("Restart with fixed seed")] void RestartFixed() { if(Application.isPlaying) BeginSession(seed); }
        public void Preview(int index)
        {
            if(forecasts==null || index<0 || index>=forecasts.Length || !forecasts[index])return;
            if(!weather)weather=GetComponent<MantaCloudWeather>();
            automatic=false;current=index;
            if(Application.isPlaying)weather.TransitionTo(forecasts[index],15);
            else GetComponent<MantaCloudscape>().SetAppearance(forecasts[index].appearance);
        }
    }
}
