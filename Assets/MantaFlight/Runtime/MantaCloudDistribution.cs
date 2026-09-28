using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MantaFlight
{
    /// <summary>Deterministic scene-wide cloud placement; only the recipe is serialized.</summary>
    [ExecuteAlways, RequireComponent(typeof(MantaCloudscape))]
    [DefaultExecutionOrder(-120)]
    public sealed class MantaCloudDistribution : MonoBehaviour
    {
        [Serializable]
        public sealed class CloudLayer
        {
            public string name = "Cloud layer";
            public bool enabled = true;
            [Tooltip("This layer can produce localized rain during wet forecasts.")]
            public bool rainCloud;
            [Range(0,32)] public int count = 8;
            [Tooltip("Cloud center heights relative to the cloudscape, in metres.")]
            public Vector2 altitude = new Vector2(500,800);
            public Vector3 minimumSize = new Vector3(500,160,500);
            public Vector3 maximumSize = new Vector3(1000,350,1000);
            [Range(.05f,2)] public float density = 1;
            [Range(.1f,3)] public float windSpeed = 1;
        }
        [Header("Altitude layers (empty uses the original single layer)")]
        public CloudLayer[] altitudeLayers = Array.Empty<CloudLayer>();
        [Header("Distribution recipe")]
        public int seed = 73421;
        [Range(0, 64)] public int cloudCount = 24;
        [Tooltip("Region center relative to this object. Altitudes are relative to its Y.")]
        public Vector3 regionCenter = new Vector3(0, 0, 350);
        public Vector2 regionSize = new Vector2(5400, 5000);
        [Tooltip("Minimum / maximum cloud center altitude, in metres.")]
        public Vector2 altitudeRange = new Vector2(490, 1120);
        public Vector3 minimumSize = new Vector3(520, 280, 460);
        public Vector3 maximumSize = new Vector3(1350, 780, 1200);
        [Min(0)] public float minimumCloudBase = 220;
        [Tooltip("Extra gap between the visible portions of neighbouring banks.")]
        [Min(0)] public float minimumSpacing = 80;
        [Tooltip("0 is uniform coverage. Higher values gather clouds into broad weather patches.")]
        [Range(0, 1)] public float patchiness = .55f;
        [Min(100)] public float patchSize = 1800;
        [Tooltip("Seeded density variation across the weather patches. Zero preserves uniform layer density.")]
        [Range(0,1)] public float patchDensityVariation=.4f;

        [Header("Flight corridor")]
        [Tooltip("Half-width and half-length around the region center. Lower clouds avoid this area.")]
        public Vector2 clearCorridor = new Vector2(220, 750);
        [Min(0)] public float corridorClearance = 440;

        [Header("Rendering resources")]
        public Mesh volumeMesh;
        public Material volumeMaterial;
        [Tooltip("Draw the procedural region and flight corridor when selected.")]
        public bool showRegion = true;
        [SerializeField, HideInInspector] int generatedCount;
        [Tooltip("Generated result; recomputed whenever the recipe changes.")]
        [SerializeField, TextArea(1, 3)] string generationStatus;
        public int GeneratedCount => generatedCount;
        public string GenerationStatus => generationStatus;
        bool rebuildRequested;
        MantaCloudscape clouds;
        GameObject generatedRoot;

        public struct Placement
        {
            public Vector3 position, size;
            public int layer;
            public float density, windSpeed;
        }

        void OnEnable()
        {
            clouds = GetComponent<MantaCloudscape>(); rebuildRequested = true;
            Regenerate();
        }
        void OnValidate() { rebuildRequested = true; }
        void Update()
        {
            // Unity can discard DontSave preview geometry during reload/scene restoration.
            if (!rebuildRequested && (generatedRoot || !volumeMesh || !volumeMaterial)) return;
            rebuildRequested = false;
            Regenerate();
        }
        void OnDisable()
        {
            ClearGenerated();
        }

        // Pure layout generation: does not touch transforms, meshes, or Unity's random state.
        public List<Placement> CreateLayout()
        {
            if(altitudeLayers==null || altitudeLayers.Length==0)
                return CreateLayer(seed,cloudCount,regionCenter,altitudeRange,minimumSize,maximumSize,minimumCloudBase,-1,1,1);
            var result=new List<Placement>();
            for(int i=0;i<altitudeLayers.Length;i++)
            {
                var layer=altitudeLayers[i];
                if(layer==null || !layer.enabled) continue;
                result.AddRange(CreateLayer(unchecked(seed+i*7919),Mathf.Clamp(layer.count,0,32),
                    new Vector3(regionCenter.x,0,regionCenter.z),layer.altitude,layer.minimumSize,layer.maximumSize,0,
                    i,Mathf.Clamp(layer.density,.05f,2),Mathf.Clamp(layer.windSpeed,.1f,3)));
            }
            return result;
        }

        List<Placement> CreateLayer(int seed,int cloudCount,Vector3 regionCenter,Vector2 altitudeRange,
            Vector3 minimumSize,Vector3 maximumSize,float minimumCloudBase,int layer,float density,float windSpeed)
        {
            var result = new List<Placement>();
            var random = new System.Random(seed);
            int desired = Mathf.Clamp(cloudCount,0,64);
            var area = new Vector2(Mathf.Max(1,regionSize.x),Mathf.Max(1,regionSize.y));
            var small = Vector3.Max(Vector3.one,Vector3.Min(minimumSize,maximumSize));
            var large = Vector3.Max(small,Vector3.Max(minimumSize,maximumSize));
            float low = Mathf.Min(altitudeRange.x,altitudeRange.y);
            float high = Mathf.Max(altitudeRange.x,altitudeRange.y);
            float noiseX = Next(random,0,1000), noiseZ = Next(random,0,1000);
            // Bounded rejection sampling respects impossible configurations without hanging.
            for(int attempt=0; attempt<desired*160 && result.Count<desired; attempt++)
            {
                // Shared scale gives each bank coherent proportions while retaining variation.
                float sizeFactor = (float)random.NextDouble();
                var size = new Vector3(Mathf.Lerp(small.x,large.x,sizeFactor),
                    Next(random,small.y,large.y),Mathf.Lerp(small.z,large.z,Mathf.Clamp01(sizeFactor+Next(random,-.2f,.2f))));
                if(size.x>area.x || size.z>area.y) continue;
                float x=Next(random,-(area.x-size.x)*.5f,(area.x-size.x)*.5f);
                float z=Next(random,-(area.y-size.z)*.5f,(area.y-size.z)*.5f);
                float patch=Mathf.PerlinNoise(x/Mathf.Max(100,patchSize)+noiseX,z/Mathf.Max(100,patchSize)+noiseZ);
                if(random.NextDouble()>Mathf.Lerp(1,Mathf.SmoothStep(.12f,1,patch),Mathf.Clamp01(patchiness))) continue;
                float minY=Mathf.Max(low,Mathf.Max(0,minimumCloudBase)+size.y*.5f);
                if(minY>high) continue;
                float y=Next(random,minY,high);
                if(y-size.y*.5f<corridorClearance &&
                    Mathf.Abs(x)<Mathf.Max(0,clearCorridor.x)+size.x*.5f &&
                    Mathf.Abs(z)<Mathf.Max(0,clearCorridor.y)+size.z*.5f) continue;
                var candidate=new Placement { position=regionCenter+new Vector3(x,y,z),size=size,
                    layer=layer,density=density*Mathf.Lerp(1,Mathf.Lerp(.65f,1.35f,patch),patchDensityVariation),windSpeed=windSpeed };
                bool crowded=false;
                foreach(var existing in result)
                {
                    float dx=candidate.position.x-existing.position.x, dz=candidate.position.z-existing.position.z;
                    float radius=(Mathf.Max(size.x,size.z)+Mathf.Max(existing.size.x,existing.size.z))*.38f+Mathf.Max(0,minimumSpacing);
                    if(dx*dx+dz*dz<radius*radius) { crowded=true; break; }
                }
                if(!crowded) result.Add(candidate);
            }
            return result;
        }

        static float Next(System.Random random,float min,float max) => Mathf.Lerp(min,max,(float)random.NextDouble());

        [ContextMenu("Regenerate from seed")]
        public void Regenerate()
        {
            rebuildRequested=false;
            if(!isActiveAndEnabled) return;
            if(!clouds) clouds=GetComponent<MantaCloudscape>();
            if(!volumeMesh || !volumeMaterial)
            {
                ClearGenerated(); generationStatus="Assign the cloud volume mesh and material."; return;
            }
            var layout=CreateLayout();
            ClearGenerated();
            var root=new GameObject("Generated clouds • seed "+seed);
            generatedRoot=root;
            root.hideFlags=HideFlags.DontSave;
            root.transform.SetParent(transform,false);
            root.AddComponent<MantaGeneratedCloudRoot>().owner=this;
            for(int i=0;i<layout.Count;i++)
            {
                var bank=new GameObject((layout[i].layer<0 ? "Cloud" : altitudeLayers[layout[i].layer].name)+" "+(i+1).ToString("00"));
                bank.hideFlags=HideFlags.DontSave;
                bank.transform.SetParent(root.transform,false);
                bank.transform.localPosition=layout[i].position;
                bank.transform.localScale=layout[i].size;
                bank.AddComponent<MeshFilter>().sharedMesh=volumeMesh;
                var tuning=bank.AddComponent<MantaCloudLayerVolume>();
                tuning.density=layout[i].density; tuning.windSpeed=layout[i].windSpeed;
                tuning.rainCloud=layout[i].layer>=0 && altitudeLayers[layout[i].layer].rainCloud;
                tuning.Initialize(unchecked(seed+i*3571),this);
                var renderer=bank.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=volumeMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off; renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            generatedCount=layout.Count;
            int requested=0;
            if(altitudeLayers==null || altitudeLayers.Length==0) requested=Mathf.Clamp(cloudCount,0,64);
            else foreach(var layer in altitudeLayers) if(layer!=null && layer.enabled) requested+=Mathf.Clamp(layer.count,0,32);
            generationStatus=generatedCount==requested
                ? generatedCount+" clouds generated from seed "+seed
                : generatedCount+" / "+requested+" fit. Increase region size or reduce cloud size / spacing.";
            clouds.RefreshVolumes();
        }

        void ClearGenerated()
        {
            foreach(var marker in GetComponentsInChildren<MantaGeneratedCloudRoot>(true))
            {
                if(marker.owner!=this) continue;
                // Runtime Destroy is delayed: disable immediately so regeneration cannot double-render.
                marker.gameObject.SetActive(false);
                if(Application.isPlaying) Destroy(marker.gameObject); else DestroyImmediate(marker.gameObject);
            }
            generatedCount=0;
            generatedRoot=null;
            if(clouds) clouds.RefreshVolumes();
        }

        void OnDrawGizmosSelected()
        {
            if(!showRegion) return;
            Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(.5f,.8f,1,.65f);
            float low=Mathf.Min(altitudeRange.x,altitudeRange.y),high=Mathf.Max(altitudeRange.x,altitudeRange.y);
            if(altitudeLayers==null || altitudeLayers.Length==0)
                Gizmos.DrawWireCube(regionCenter+Vector3.up*(low+high)*.5f,new Vector3(regionSize.x,high-low,regionSize.y));
            else foreach(var layer in altitudeLayers)
            {
                if(layer==null || !layer.enabled) continue;
                Gizmos.DrawWireCube(new Vector3(regionCenter.x,(layer.altitude.x+layer.altitude.y)*.5f,regionCenter.z),
                    new Vector3(regionSize.x,Mathf.Abs(layer.altitude.y-layer.altitude.x),regionSize.y));
            }
            Gizmos.color=new Color(1,.76f,.3f,.8f);
            Gizmos.DrawWireCube(regionCenter+Vector3.up*corridorClearance*.5f,
                new Vector3(clearCorridor.x*2,corridorClearance,clearCorridor.y*2));
            Gizmos.matrix=Matrix4x4.identity;
        }
    }
}
