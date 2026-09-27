using UnityEngine;

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
        [Range(0f, 2f)] public float evolution = .35f;
        [Range(0,1)] public float rain;
        [Range(32, 96)] public int raySteps = 64;
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

        void OnEnable() { volumes = GetComponentsInChildren<MeshRenderer>(false); Apply(); }
        void OnValidate() => Apply();
        void OnTransformChildrenChanged() => RefreshVolumes();
        public void RefreshVolumes()
        {
            volumes = null;
            var distribution = GetComponent<MantaCloudDistribution>();
            distributionSeed = distribution ? ((uint)distribution.seed % 997) * .371f : 0;
            Apply();
        }
        void Update()
        {
            if (!Application.isPlaying) return;
            // Integrate velocity instead of multiplying changing wind by absolute time:
            // weather transitions must never teleport the noise pattern.
            windOffset += wind * Time.deltaTime;
            detailPhase += evolution * Time.deltaTime;
            Apply();
        }

        public CloudAppearance CaptureAppearance() => new CloudAppearance
        {
            coverage=coverage, footprint=footprint, thickness=thickness, noiseScale=noiseScale,
            erosion=erosion, lenticular=lenticular, layers=layers, density=density,
            sunlight=sunlight, shadow=shadow, wind=wind, evolution=evolution,rain=rain
        };

        public void SetAppearance(CloudAppearance a)
        {
            coverage=a.coverage; footprint=a.footprint; thickness=a.thickness; noiseScale=a.noiseScale;
            erosion=a.erosion; lenticular=a.lenticular; layers=a.layers; density=a.density;
            sunlight=a.sunlight; shadow=a.shadow; wind=a.wind; evolution=a.evolution; rain=a.rain;
            if(!Application.isPlaying) Apply();
        }

        public void Apply()
        {
            if (!shapeNoise || !detailNoise) return;
            properties ??= new MaterialPropertyBlock();
            // Retired procedural roots stay inactive until Unity's deferred Destroy completes.
            // Exclude them immediately, without reparenting during scene deactivation.
            volumes ??= GetComponentsInChildren<MeshRenderer>(false);
            int index = 0;
            foreach (var volume in volumes)
            {
                if (!volume) continue;
                var layer=volume.GetComponent<MantaCloudLayerVolume>();
                float densityScale=layer ? layer.density*layer.edgeFade : 1;
                float speed=layer ? layer.windSpeed : 1;
                properties.Clear();
                properties.SetTexture("_ShapeNoise", shapeNoise);
                properties.SetTexture("_DetailNoise", detailNoise);
                float storm=layer && layer.rainCloud ? rain : 0;
                properties.SetColor("_SunTint", Color.Lerp(sunlight,new Color(.5f,.54f,.6f),storm*.7f) * environmentKey);
                properties.SetColor("_ShadowTint", Color.Lerp(shadow,new Color(.09f,.12f,.17f),storm*.8f) * environmentFill);
                properties.SetFloat("_Density", density*densityScale);
                properties.SetVector("_Wind", wind);
                properties.SetFloat("_Evolution", evolution);
                properties.SetVector("_FlowOffset", windOffset*speed);
                properties.SetFloat("_DetailPhase", detailPhase);
                if(layer)
                {
                    properties.SetVectorArray("_LobeCenters",layer.lobeCenters);
                    for(int l=0;l<5;l++) animatedRadii[l]=layer.lobeRadii[l]*(1+.075f*Mathf.Sin(detailPhase*.035f+distributionSeed+index*17.31f+l*2.1f));
                    properties.SetVectorArray("_LobeRadii",animatedRadii);
                }
                properties.SetVector("_ShapeParams", new Vector4(coverage,footprint,thickness,noiseScale));
                properties.SetVector("_DetailParams", new Vector4(erosion,lenticular,layers,0));
                properties.SetFloat("_Steps", raySteps);
                properties.SetFloat("_Seed", distributionSeed + index++ * 17.31f);
                volume.SetPropertyBlock(properties);
            }
        }
    }
}
