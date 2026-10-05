using UnityEngine;
using UnityEngine.Rendering;
namespace MantaFlight
{
    // Two reused ribbons and one unshadowed light: no particle simulation or per-frame searches.
    public sealed class MantaStormLightning : MonoBehaviour
    {
        public Material boltMaterial;
        [Tooltip("Seconds between electrical discharges at full storm intensity.")] public Vector2 interval=new Vector2(12,32);
        [Min(100)] public float visibleRange=3500;
        [Range(0,4)] public float flashIntensity=1.4f;
        public LayerMask groundMask=~0;
        MantaCloudscape clouds;
        MantaRainForecast forecast;
        MantaCloudLayerVolume[] banks;
        LineRenderer bolt,branch;
        Light flash;
        GameObject effects;
        System.Random random;
        int lastSeed;
        float timer=8,age=1;
        float Next()=>(float)random.NextDouble();
        void OnEnable() {clouds=GetComponent<MantaCloudscape>();forecast=GetComponent<MantaRainForecast>();}
        void Build()
        {
            effects=new GameObject("Storm lightning (pooled)"){hideFlags=HideFlags.DontSave};effects.transform.SetParent(transform,false);
            bolt=Ribbon("Bolt",22,1.5f);branch=Ribbon("Branch",9,.65f);
            flash=effects.AddComponent<Light>();flash.type=LightType.Point;flash.shadows=LightShadows.None;flash.range=1200;flash.color=new Color(.65f,.76f,1);flash.enabled=false;
        }
        LineRenderer Ribbon(string name,int count,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(effects.transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=boltMaterial;line.positionCount=count;line.useWorldSpace=true;
            line.widthMultiplier=width;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;return line;
        }
        void Update()
        {
            if(!clouds || !boltMaterial)return;
            int seed=forecast ? forecast.SessionSeed : 7342;
            if(random==null || seed!=lastSeed){lastSeed=seed;random=new System.Random(unchecked(seed^193847));timer=Mathf.Lerp(8,20,Next());}
            if(!effects)Build();
            age+=Time.deltaTime;
            float pulse=age<.065f ? 1-age/.065f : age>.12f && age<.21f ? (1-(age-.12f)/.09f)*.6f : 0;
            if(clouds.thunder<.05f)pulse=0;
            SetFlash(pulse);
            if(clouds.thunder<.05f)return;
            timer-=Time.deltaTime*clouds.thunder;
            if(timer>0)return;
            timer=Mathf.Lerp(Mathf.Max(3,interval.x),Mathf.Max(3,interval.y),Next());
            TryStrike();
        }
        public bool TryStrike()
        {
            if(!clouds || clouds.thunder<.05f || !boltMaterial)return false;
            if(random==null)random=new System.Random(7342);
            if(!effects)Build();
            // Search only for an occasional strike, not for every frame.
            banks=clouds.GetComponentsInChildren<MantaCloudLayerVolume>();
            var cam=Camera.main;if(!cam || banks.Length==0)return false;
            int start=random.Next(banks.Length);
            for(int i=0;i<banks.Length;i++)
            {
                var bank=banks[(start+i)%banks.Length];
                if(!bank.rainCloud || bank.edgeFade<.5f || Vector3.Distance(bank.transform.position,cam.transform.position)>visibleRange)continue;
                Vector3 top=bank.transform.position-Vector3.up*bank.transform.lossyScale.y*.18f;
                Vector3 end=top+new Vector3((Next()-.5f)*300,-Mathf.Clamp(bank.transform.lossyScale.y*.8f,150,700),(Next()-.5f)*300);
                // Most discharges remain in/cloud-to-cloud; occasional ground strikes use actual scenery.
                if(Next()<.25f && Physics.Raycast(top,Vector3.down,out var hit,2500,groundMask,QueryTriggerInteraction.Ignore))end=hit.point;
                Fill(bolt,top,end,45);var fork=bolt.GetPosition(10);Fill(branch,fork,Vector3.Lerp(top,end,.85f)+new Vector3(140,0,80),22);
                flash.transform.position=top;age=0;SetFlash(1);return true;
            }
            return false;
        }
        void Fill(LineRenderer line,Vector3 a,Vector3 b,float jitter)
        {
            for(int i=0;i<line.positionCount;i++){float t=(float)i/(line.positionCount-1);var p=Vector3.Lerp(a,b,t);if(i>0 && i<line.positionCount-1)p+=new Vector3(Next()-.5f,Next()-.5f,Next()-.5f)*jitter;line.SetPosition(i,p);}
        }
        void SetFlash(float pulse)
        {
            bolt.enabled=branch.enabled=pulse>0;
            bolt.startColor=bolt.endColor=branch.startColor=branch.endColor=new Color(2,2.5f,3.5f,pulse);
            flash.enabled=pulse>0;flash.intensity=pulse*flashIntensity;
        }
        void OnDisable(){if(effects) {if(Application.isPlaying)Destroy(effects);else DestroyImmediate(effects);}effects=null;}
    }
}
