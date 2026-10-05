using UnityEngine;
using UnityEngine.Rendering;

namespace MantaFlight
{
    /// <summary>Art direction for the decorative volumes; motion is evaluated on the GPU.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public sealed class MantaCloudscape : MonoBehaviour
    {
        [Tooltip("The imported ProceduralClouds package's baked Worley shape volume.")]
        public Texture3D shapeNoise;
        public Texture3D detailNoise;
        [Header("Cloud shape — driven by weather when enabled")]
        [Range(0, 1)] public float coverage = .55f;
        [Range(.2f, 1)] public float footprint = 1;
        [Range(.06f, 1)] public float thickness = 1;
        [Range(.3f, 3)] public float noiseScale = 1;
        [Range(0, 1.5f)] public float erosion = .65f;
        [Range(0, 1)] public float lenticular;
        [Range(0, 1)] public float layers;
        [Header("Light and motion")]
        public Color sunlight = new Color(1f, .88f, .69f);
        public Color shadow = new Color(.36f, .48f, .61f);
        [Range(.1f, 2f)] public float density = .8f;
        [Tooltip("World-space drift in metres per second.")]
        public Vector3 wind = new Vector3(2.8f, .12f, .8f);
        [System.NonSerialized] public Vector3 windVariation;
        public Vector3 CurrentWind=>wind+windVariation;
        public void ResetSessionMotion() {windOffset=Vector3.zero;detailPhase=0;windVariation=Vector3.zero;}
        [Range(0f, 2f)] public float evolution = .35f;
        [Range(0,1)] public float rain;
        [Range(0,1)] public float overcast,thunder;
        [Range(32, 96)] public int raySteps = 64;
        [Header("Distance LOD and visibility")]
        public bool distanceLod=true, frustumCulling=true;
        [Tooltip("Distance to the volume surface where medium LOD starts, in metres.")]
        [Min(0)] public float mediumLodDistance=450;
        [Tooltip("Distance to the volume surface where far LOD starts, in metres. Always after the medium transition.")]
        [Min(1)] public float farLodDistance=1600;
        [Tooltip("Width of each smooth LOD transition in metres.")]
        [Min(1)] public float lodBlendDistance=500;
        [Tooltip("Medium-distance ray samples; near volumes use Ray Steps.")]
        [Range(12,96)] public int mediumRaySteps=32;
        [Tooltip("Far-distance samples. Fine erosion and extra lighting probes are removed progressively.")]
        [Range(8,64)] public int farRaySteps=24;
        [Tooltip("World-space margin around cloud and shadow visibility bounds, in metres.")]
        [Min(0)] public float cullingBias=100;
        public int VisibleVolumes {get; private set;}
        public int CulledVolumes {get; private set;}
        [Header("Cloud shadows on scenery")]
        public bool surfaceShadows=true;
        [Tooltip("Maximum scenery darkening under clouds. Ambient light is retained by limiting this value.")]
        [Range(0,.8f)] public float surfaceShadowStrength=.45f;
        [Tooltip("Shadow reach in metres, limited to the atmospheric cache range. Fades over the last 20 percent.")]
        [Min(50)] public float surfaceShadowDistance=1600;
        readonly Plane[] cameraPlanes=new Plane[6];
        MantaCloudLayerVolume[] volumeLayers;
        MaterialPropertyBlock properties;
        readonly Vector4[] animatedRadii=new Vector4[5];
        MeshRenderer[] volumes;
        Vector3 windOffset;
        float detailPhase;
        float distributionSeed;
        Color environmentKey = Color.white, environmentFill = Color.white;
        public void SetEnvironmentLighting(Color key, Color fill)
        {
            environmentKey=key; environmentFill=fill; if(!Application.isPlaying) Apply();
        }

        void OnEnable() { RenderPipelineManager.beginCameraRendering+=BeginCamera; RenderPipelineManager.endCameraRendering+=EndCamera; RefreshVolumes(); }
        void OnDisable() { RenderPipelineManager.beginCameraRendering-=BeginCamera; RenderPipelineManager.endCameraRendering-=EndCamera; RestoreVisibility(); }
        void BeginCamera(ScriptableRenderContext context,Camera camera) => ApplyForCamera(camera);
        void EndCamera(ScriptableRenderContext context,Camera camera) => RestoreVisibility();
        void RestoreVisibility() { if(volumes!=null) foreach(var v in volumes) if(v) v.forceRenderingOff=false; }
        void OnValidate() => Apply();
        void OnTransformChildrenChanged() => RefreshVolumes();
        public void RefreshVolumes()
        {
            RestoreVisibility(); volumes = null; volumeLayers=null;
            var distribution = GetComponent<MantaCloudDistribution>();
            distributionSeed = distribution ? ((uint)distribution.seed % 997) * .371f : 0;
            Apply();
        }
        void Update()
        {
            if (!Application.isPlaying) return;
            // Integrate velocity instead of multiplying changing wind by absolute time:
            // weather transitions must never teleport the noise pattern.
            windOffset += CurrentWind * Time.deltaTime;
            detailPhase += evolution * Time.deltaTime;
            // Per-camera rendering uploads only visible volumes, after their motion update.
        }

        public CloudAppearance CaptureAppearance() => new CloudAppearance
        {
            coverage=coverage, footprint=footprint, thickness=thickness, noiseScale=noiseScale,
            erosion=erosion, lenticular=lenticular, layers=layers, density=density,
            sunlight=sunlight, shadow=shadow, wind=wind, evolution=evolution,rain=rain,overcast=overcast,thunder=thunder
        };

        public void SetAppearance(CloudAppearance a)
        {
            coverage=a.coverage; footprint=a.footprint; thickness=a.thickness; noiseScale=a.noiseScale;
            erosion=a.erosion; lenticular=a.lenticular; layers=a.layers; density=a.density;
            sunlight=a.sunlight; shadow=a.shadow; wind=a.wind; evolution=a.evolution; rain=a.rain;
            overcast=a.overcast;thunder=a.thunder;
            if(!Application.isPlaying) Apply();
        }

        public void Apply() => ApplyForCamera(null);
        public void ApplyForCamera(Camera camera)
        {
            if (!shapeNoise || !detailNoise) return;
            properties ??= new MaterialPropertyBlock();
            // Retired procedural roots stay inactive until Unity's deferred Destroy completes.
            // Exclude them immediately, without reparenting during scene deactivation.
            if(volumes==null)
            {
                volumes=GetComponentsInChildren<MeshRenderer>(false);
                volumeLayers=new MantaCloudLayerVolume[volumes.Length];
                for(int i=0;i<volumes.Length;i++) volumeLayers[i]=volumes[i].GetComponent<MantaCloudLayerVolume>();
            }
            if(camera) GeometryUtility.CalculateFrustumPlanes(camera,cameraPlanes);
            VisibleVolumes=0; CulledVolumes=0;
            for(int index=0;index<volumes.Length;index++)
            {
                var volume=volumes[index];
                if (!volume) continue;
                var bounds=volume.bounds; bounds.Expand(Mathf.Max(0,cullingBias)*2);
                bool culled=camera && frustumCulling && !GeometryUtility.TestPlanesAABB(cameraPlanes,bounds);
                volume.forceRenderingOff=culled;
                if(culled) { CulledVolumes++; continue; }
                VisibleVolumes++;
                var layer=volumeLayers[index];
                float distance=camera ? Mathf.Sqrt(volume.bounds.SqrDistance(camera.transform.position)) : 0;
                float blend=Mathf.Max(1,lodBlendDistance);
                float medium=distanceLod ? Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-Mathf.Max(0,mediumLodDistance))/blend)) : 0;
                float far=distanceLod ? Mathf.SmoothStep(0,1,Mathf.Clamp01((distance-Mathf.Max(mediumLodDistance+blend,farLodDistance))/blend)) : 0;
                float steps=Mathf.Lerp(Mathf.Clamp(raySteps,32,96),Mathf.Min(raySteps,Mathf.Clamp(mediumRaySteps,12,96)),medium);
                steps=Mathf.Lerp(steps,Mathf.Min(mediumRaySteps,Mathf.Clamp(farRaySteps,8,64)),far);
                float densityScale=layer ? layer.density*layer.edgeFade : 1;
                float speed=layer ? layer.windSpeed : 1;
                properties.Clear();
                properties.SetTexture("_ShapeNoise", shapeNoise);
                properties.SetTexture("_DetailNoise", detailNoise);
                float storm=layer && layer.rainCloud ? rain : 0;
                properties.SetColor("_SunTint", Color.Lerp(sunlight,new Color(.5f,.54f,.6f),storm*.7f) * environmentKey);
                properties.SetColor("_ShadowTint", Color.Lerp(shadow,new Color(.09f,.12f,.17f),storm*.8f) * environmentFill);
                properties.SetFloat("_Density", density*densityScale);
                properties.SetVector("_Wind", WindManager.Instance ? WindManager.GetWindAt(volume.transform.position) : CurrentWind);
                properties.SetFloat("_Evolution", evolution);
                properties.SetVector("_FlowOffset", layer && WindManager.Instance ? layer.WindFlowOffset : windOffset*speed);
                properties.SetFloat("_DetailPhase", detailPhase);
                if(layer)
                {
                    properties.SetVectorArray("_LobeCenters",layer.lobeCenters);
                    for(int l=0;l<5;l++) animatedRadii[l]=layer.lobeRadii[l]*(1+.075f*Mathf.Sin(detailPhase*.035f+distributionSeed+index*17.31f+l*2.1f));
                    properties.SetVectorArray("_LobeRadii",animatedRadii);
                }
                properties.SetVector("_ShapeParams", new Vector4(coverage,footprint,thickness,noiseScale));
                properties.SetVector("_DetailParams", new Vector4(erosion,lenticular,layers,0));
                properties.SetFloat("_Steps", steps);
                properties.SetVector("_CloudLod",new Vector4(medium,far,0,0));
                properties.SetFloat("_Seed", distributionSeed + index * 17.31f);
                volume.SetPropertyBlock(properties);
            }
        }
    }
}
