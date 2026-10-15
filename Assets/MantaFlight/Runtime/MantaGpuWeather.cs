using UnityEngine;
using UnityEngine.Rendering;
namespace MantaFlight
{
    // One regional weather field drives GPU rendering and inexpensive gameplay queries.
    [ExecuteAlways, DefaultExecutionOrder(-99), RequireComponent(typeof(MantaCloudscape))]
    public sealed class MantaGpuWeather : MonoBehaviour
    {
        public static MantaGpuWeather Active { get; private set; }
        [Tooltip("Switch between the shared GPU field and the original mesh volumes.")] public bool useGpuClouds=true;
        public ComputeShader cloudCompute;
        [Tooltip("Repeating regional weather field width in metres.")][Min(4000)] public float worldSize=24000;
        [Tooltip("Bottom altitude and vertical extent of the main cumulus layer.")] public Vector2 mainLayer=new Vector2(0,2200);
        [Tooltip("Bottom altitude and thickness of the higher thin cloud layer.")] public Vector2 highLayer=new Vector2(3200,260);
        [Tooltip("Maximum view distance through clouds.")][Range(3000,40000)] public float viewDistance=18000;
        [Tooltip("Shape noise scale in metres. Larger values produce broader billows.")][Min(100)] public float billowSize=2200;
        [Tooltip("Optical extinction per metre, before the forecast density multiplier.")][Range(.001f,.025f)] public float extinction=.012f;
        [Tooltip("Maximum ray samples per low-resolution pixel.")][Range(32,128)] public int samples=80;
        [Tooltip("2 renders at half width and half height; 3 is a faster quality option.")][Range(1,4)] public int resolutionDivisor=2;
        [Tooltip("Previous-frame contribution. Automatically rejected on camera cuts, lightning and weather changes.")][Range(0,.95f)] public float historyWeight=.85f;
        [Tooltip("Distance in metres where fine cloud erosion starts fading out.")][Min(0)] public float detailFadeStart=3000;
        [Tooltip("Distance in metres where only the broad cloud shape is sampled.")][Min(1)] public float detailFadeEnd=6000;
        public MantaCloudscape Clouds {get;private set;}
        public RenderTexture WeatherMap {get;private set;}
        public RTHandle WeatherHandle,ShapeHandle,DetailHandle;
        public Vector3 WindOffset {get;private set;}
        public float Evolution {get;private set;}
        public bool Ready => useGpuClouds && isActiveAndEnabled && cloudCompute && SystemInfo.supportsComputeShaders && SystemInfo.supports3DRenderTextures && Clouds && Clouds.shapeNoise && Clouds.detailNoise && MantaGpuCloudFeature.Available && KernelsSupported;
        ComputeShader checkedCompute; bool supported;
        bool KernelsSupported
        {
            get
            {
                if(checkedCompute!=cloudCompute)
                {
                    checkedCompute=cloudCompute;supported=true;
                    foreach(var kernel in new[]{"GenerateWeather","BuildLightCache","TraceClouds","ResolveHistory"})
                        supported &= cloudCompute.HasKernel(kernel) && cloudCompute.IsSupported(cloudCompute.FindKernel(kernel));
                }
                return supported;
            }
        }
        public static bool SupportsCamera(Camera camera) => camera && !camera.orthographic && !camera.stereoEnabled && (camera.cameraType==CameraType.Game || camera.cameraType==CameraType.SceneView);
        public static bool Rendering => Active && Active.Ready;
        int mapSeed=int.MinValue;
        MantaCloudDistribution distribution;
        public int Seed => distribution ? distribution.seed : 73421;
        public Vector4 Region => new Vector4(1/Mathf.Max(4000,worldSize),WindOffset.x,WindOffset.z,Evolution);
        public Vector4 Layers => new Vector4(mainLayer.x,Mathf.Max(50,mainLayer.y),highLayer.x,Mathf.Max(50,highLayer.y));
        public Vector4 Weather => new Vector4(Clouds.coverage,Clouds.density,Clouds.overcast,Clouds.rain);
        public Vector4 Shape => new Vector4(billowSize/Mathf.Max(.3f,Clouds.noiseScale),extinction,Mathf.Clamp(Clouds.thickness,.15f,1),Clouds.erosion);
        void OnEnable(){Active=this;Clouds=GetComponent<MantaCloudscape>();distribution=GetComponent<MantaCloudDistribution>();checkedCompute=null;}
        void OnValidate(){checkedCompute=null;}
        void Update()
        {
            if(!Ready)return;EnsureMap();
            if(Application.isPlaying){WindOffset+=Clouds.CurrentWind*Time.deltaTime;Evolution+=Clouds.evolution*Time.deltaTime;}
        }
        public void ResetMotion(){WindOffset=Vector3.zero;Evolution=0;}
        public void EnsureMap()
        {
            if(!cloudCompute)return;
            if(WeatherMap && mapSeed==Seed)return;
            if(!WeatherMap){WeatherMap=new RenderTexture(256,256,0,RenderTextureFormat.ARGBHalf){name="Manta GPU regional weather",enableRandomWrite=true,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};WeatherMap.Create();WeatherHandle=RTHandles.Alloc(WeatherMap);ShapeHandle=RTHandles.Alloc(Clouds.shapeNoise);DetailHandle=RTHandles.Alloc(Clouds.detailNoise);}
            mapSeed=Seed;ResetMotion();int k=cloudCompute.FindKernel("GenerateWeather");
            cloudCompute.SetInt("_Seed",mapSeed);cloudCompute.SetTexture(k,"_WeatherWrite",WeatherMap);cloudCompute.Dispatch(k,32,32,1);
        }
        public void Bind(MaterialPropertyBlock block)
        {
            EnsureMap();block.SetTexture("_GpuWeather",WeatherMap);block.SetTexture("_GpuShape",Clouds.shapeNoise);block.SetTexture("_GpuDetail",Clouds.detailNoise);
            block.SetVector("_Region",Region);block.SetVector("_Layers",Layers);block.SetVector("_Weather",Weather);block.SetVector("_FieldShape",Shape);
        }
        // Integer hash matches the compute generator; no GPU readback is needed for rain.
        static uint Hash(uint n){unchecked{n^=n>>16;n*=0x7feb352d;n^=n>>15;n*=0x846ca68b;n^=n>>16;return n;}}
        static float Cell(int x,int z,int seed,int period){unchecked{return (Hash((uint)(x&(period-1))*1597334677u^(uint)(z&(period-1))*3812015801u^(uint)seed)&65535)/65535f;}}
        static float Noise(float x,float z,int seed,int period){int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);float u=x-ix,v=z-iz;u=u*u*(3-2*u);v=v*v*(3-2*v);return Mathf.Lerp(Mathf.Lerp(Cell(ix,iz,seed,period),Cell(ix+1,iz,seed,period),u),Mathf.Lerp(Cell(ix,iz+1,seed,period),Cell(ix+1,iz+1,seed,period),u),v);}
        public static float CoverageNoise(float u,float v,int seed) => Mathf.Clamp01(.5f+(Noise(u*256,v*256,seed,8)*.7f+Noise(u*512,v*512,unchecked(seed+71),16)*.3f-.5f)*1.65f);
        public float CoverageAt(Vector3 p)
        {
            float u=(p.x-WindOffset.x)/Mathf.Max(4000,worldSize),v=(p.z-WindOffset.z)/Mathf.Max(4000,worldSize);
            // Match the weather texture's periodic coordinates and coarse regional scale.
            float n=CoverageNoise(Mathf.Repeat(u,1)/32,Mathf.Repeat(v,1)/32,Seed);
            float threshold=1-Mathf.Lerp(.12f,.92f,Clouds.coverage);
            return Mathf.SmoothStep(0,1,Mathf.InverseLerp(threshold-.16f,threshold+.16f,n));
        }
        public float SampleRain(Vector3 p)
        {
            if(!Ready || Clouds.rain<=0 || p.y>mainLayer.x+mainLayer.y*Shape.z*.8f)return 0;
            // Conservative wet interior; drops remain local and shelter tests remain authoritative.
            return Clouds.rain*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.95f,CoverageAt(p)));
        }
        void OnDisable(){if(Active==this)Active=null;WeatherHandle?.Release();ShapeHandle?.Release();DetailHandle?.Release();WeatherHandle=ShapeHandle=DetailHandle=null;if(WeatherMap){WeatherMap.Release();if(Application.isPlaying)Destroy(WeatherMap);else DestroyImmediate(WeatherMap);}WeatherMap=null;if(Clouds)Clouds.Apply();}
    }
}

