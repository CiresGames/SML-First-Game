using UnityEngine;
namespace MantaFlight
{
    [DefaultExecutionOrder(100)]
    public sealed class MantaLocalRain : MonoBehaviour
    {
        public MantaCloudscape clouds;
        public Transform viewer;
        public Transform player;
        public ParticleSystem rainParticles;
        [Range(100,3000)] public int maximumDrops=2200;
        [Range(5,40)] public float radius=22;
        public LayerMask shelterMask=~0;
        [Tooltip("How strongly flight-camera movement tilts rain streaks. 1 matches the drop velocity stretch; 0 ignores camera motion.")]
        [Range(0,2)] public float movementTilt=1;
        ParticleSystemRenderer rainRenderer;
        MaterialPropertyBlock motionProperties;
        Vector3 previousViewerPosition,viewerVelocity;
        Transform previousViewer;
        bool hasViewerPosition;
        static readonly System.Collections.Generic.List<ParticleSystemVertexStream> tiltStreams=new System.Collections.Generic.List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,ParticleSystemVertexStream.UV2,
            ParticleSystemVertexStream.Center,ParticleSystemVertexStream.AgePercent,
            ParticleSystemVertexStream.Velocity
        };
        public void ApplyMovementTilt()
        {
            if(!rainParticles) return;
            if(!rainRenderer)
            {
                rainRenderer=rainParticles.GetComponent<ParticleSystemRenderer>();
                if(rainRenderer) rainRenderer.SetActiveVertexStreams(tiltStreams);
            }
            if(!rainRenderer) return;
            // World-space drops keep their physical fall velocity. Only their apparent
            // streak includes camera motion, avoiding applying player movement twice.
            rainRenderer.renderMode=ParticleSystemRenderMode.Billboard;
            rainRenderer.cameraVelocityScale=0;
            motionProperties ??= new MaterialPropertyBlock();
            rainRenderer.GetPropertyBlock(motionProperties);
            motionProperties.SetVector("_RainViewerVelocity",viewerVelocity*Mathf.Clamp(movementTilt,0,2));
            motionProperties.SetFloat("_RainExposure",rainRenderer.velocityScale);
            motionProperties.SetFloat("_RainLength",rainRenderer.lengthScale);
            rainRenderer.SetPropertyBlock(motionProperties);
        }
        public float LocalRain {get;private set;}
        MantaCloudLayerVolume[] banks;
        ParticleSystem.Particle[] particles;
        System.Random random=new System.Random(7342);
        float cacheTimer,emission;
        void OnEnable() { particles=new ParticleSystem.Particle[3000]; cacheTimer=0; hasViewerPosition=false; viewerVelocity=Vector3.zero; ApplyMovementTilt(); }
        float Next()=> (float)random.NextDouble();
        // A conservative inner elliptical footprint: no rain beyond a cloud's visible envelope.
        public static float Influence(Vector3 p,Vector3 center,Vector3 size,float footprint,float thickness)
        {
            float rx=Mathf.Max(1,size.x*.32f*footprint), rz=Mathf.Max(1,size.z*.32f*footprint);
            float dx=(p.x-center.x)/rx,dz=(p.z-center.z)/rz;
            float radial=dx*dx+dz*dz;
            float top=center.y+size.y*.32f*thickness*Mathf.Sqrt(Mathf.Max(0,1-radial));
            if(radial>=1 || p.y>top) return 0;
            return Mathf.SmoothStep(0,1,Mathf.Clamp01((1-radial)*3));
        }
        public float SampleRain(Vector3 p)
        {
            if(!clouds || banks==null || clouds.rain<=.001f) return 0;
            float result=0;
            foreach(var b in banks)
                if(b && b.isActiveAndEnabled && b.rainCloud)
                    result=Mathf.Max(result,b.edgeFade*Influence(p,b.transform.position,b.transform.lossyScale,clouds.footprint,clouds.thickness));
            return result*clouds.rain;
        }
        void LateUpdate()
        {
            if(!viewer && Camera.main) viewer=Camera.main.transform;
            if(!viewer || !clouds || !rainParticles) return;
            float dt=Time.deltaTime;
            Vector3 delta=viewer.position-previousViewerPosition;
            if(!hasViewerPosition || previousViewer!=viewer || delta.sqrMagnitude>10000)
                viewerVelocity=Vector3.zero;
            else if(dt>.0001f)
                viewerVelocity=Vector3.Lerp(viewerVelocity,Vector3.ClampMagnitude(delta/dt,200),1-Mathf.Exp(-dt*14));
            previousViewerPosition=viewer.position;previousViewer=viewer;hasViewerPosition=true;
            ApplyMovementTilt();
            cacheTimer-=Time.deltaTime;
            if(cacheTimer<=0) { banks=clouds.GetComponentsInChildren<MantaCloudLayerVolume>(); cacheTimer=1; }
            Vector3 subject=player ? player.position+Vector3.up*2 : viewer.position;
            LocalRain=SampleRain(subject);
            // Local emitter only: leaving a wet footprint or entering solid shelter clears residual drops.
            if(LocalRain<=.001f || Physics.Raycast(subject,Vector3.up,2000,shelterMask,QueryTriggerInteraction.Ignore))
            { rainParticles.Clear(); emission=0; LocalRain=0; return; }
            rainParticles.transform.position=viewer.position;
            if(!rainParticles.isPlaying) rainParticles.Play();
            emission+=1400*LocalRain*Time.deltaTime;
            int spawn=Mathf.Min(150,(int)emission); emission-=spawn;
            for(int i=0;i<spawn && rainParticles.particleCount<maximumDrops;i++)
            {
                var p=viewer.position+new Vector3((Next()*2-1)*radius,8+Next()*16,(Next()*2-1)*radius);
                if(SampleRain(p)<=.001f) continue;
                var emit=new ParticleSystem.EmitParams {
                    position=p,velocity=new Vector3(clouds.CurrentWind.x*.35f,-Mathf.Lerp(18,32,LocalRain),clouds.CurrentWind.z*.35f),
                    startLifetime=1.4f,startSize=Mathf.Lerp(.025f,.055f,Next()),
                    startColor=new Color(.65f,.75f,.85f,Mathf.Lerp(.18f,.45f,LocalRain))
                };
                rainParticles.Emit(emit,1);
            }
            int count=rainParticles.GetParticles(particles);
            for(int i=0;i<count;i++)
                if(SampleRain(particles[i].position)<=.001f || (particles[i].position-viewer.position).sqrMagnitude>radius*radius*5)
                    particles[i].remainingLifetime=0;
            rainParticles.SetParticles(particles,count);
        }
        void OnDisable() { if(rainParticles) rainParticles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); }
    }
}
