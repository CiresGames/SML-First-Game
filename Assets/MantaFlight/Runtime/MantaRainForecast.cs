using UnityEngine;
namespace MantaFlight
{
    [RequireComponent(typeof(MantaCloudWeather)),DefaultExecutionOrder(-115)]
    public sealed class MantaRainForecast : MonoBehaviour
    {
        [Tooltip("Ordered from fair to overcast, light rain, moderate rain and heavy rain.")]
        public MantaCloudPreset[] forecasts;
        public int seed=90210;
        [Range(0,4)] public int startingForecast=2;
        public bool automatic=true;
        public Vector2 holdSeconds=new Vector2(120,240);
        public Vector2 transitionSeconds=new Vector2(50,90);
        public string CurrentForecast => weather && weather.TargetPreset ? weather.TargetPreset.name : "";
        MantaCloudWeather weather;
        System.Random random;
        int current,trend=1;
        float remaining;
        void Start()
        {
            weather=GetComponent<MantaCloudWeather>(); random=new System.Random(seed);
            current=Mathf.Clamp(startingForecast,0,(forecasts?.Length??1)-1);
            if(forecasts==null || forecasts.Length==0) { enabled=false; return; }
            weather.TransitionTo(forecasts[current],1);
            remaining=Pick(holdSeconds);
        }
        float Pick(Vector2 range)=>Mathf.Lerp(Mathf.Max(1,Mathf.Min(range.x,range.y)),Mathf.Max(1,Mathf.Max(range.x,range.y)),(float)random.NextDouble());
        public static int Adjacent(int current,int trend,int count)=>Mathf.Clamp(current+trend,0,count-1);
        void Update()
        {
            if(!weather || !automatic || weather.paused || weather.IsTransitioning) return;
            remaining-=Time.deltaTime*Mathf.Max(0,weather.cycleSpeed);
            if(remaining>0) return;
            // Persistent fronts tend to develop/clear rather than jumping between extremes.
            if(current==0) trend=1;
            else if(current==forecasts.Length-1) trend=-1;
            else if(random.NextDouble()<.25) trend=-trend;
            current=Adjacent(current,trend,forecasts.Length);
            if(forecasts[current]) weather.TransitionTo(forecasts[current],Pick(transitionSeconds));
            remaining=Pick(holdSeconds);
        }
        public void Preview(int index)
        {
            if(forecasts==null || index<0 || index>=forecasts.Length || !forecasts[index]) return;
            if(!weather) weather=GetComponent<MantaCloudWeather>();
            automatic=false; current=index;
            if(Application.isPlaying) weather.TransitionTo(forecasts[index],15);
            else GetComponent<MantaCloudscape>().SetAppearance(forecasts[index].appearance);
        }
    }
}
