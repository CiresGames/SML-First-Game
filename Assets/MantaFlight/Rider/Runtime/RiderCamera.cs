using UnityEngine;
namespace MantaFlight.Rider
{
    [DefaultExecutionOrder(300)]
    public sealed class RiderCamera : MonoBehaviour
    {
        public RiderController rider;
        public MantaCameraController mountedCamera;
        public FlightFreeLook freeLook=new FlightFreeLook();
        Camera lens;
        Vector3 followVelocity,stableForward=Vector3.forward,blendStart;
        Quaternion blendRotation;
        RiderState previous;
        float yaw,pitch=15,blendTime;
        bool wasLookingBack;
        bool orbitReady;
        Vector3 orbitDirection;
        float orbitRadius,orbitRadiusVelocity;
        Vector3 lastRiderPosition;
        Transform hips;
        float standingHipHeight,rollFocusOffset,rollFocusVelocity;
        readonly RaycastHit[] cameraHits=new RaycastHit[64];
        void Start()
        {
            lens=GetComponent<Camera>();mountedCamera.enabled=false;previous=rider.State;blendTime=99;
            lastRiderPosition=rider.transform.position;
            var visuals=rider.GetComponent<RiderVisuals>();
            if(visuals!=null && visuals.animator!=null && visuals.animator.isHuman)
                hips=visuals.animator.GetBoneTransform(HumanBodyBones.Hips);
            if(hips!=null)standingHipHeight=hips.position.y-rider.transform.position.y;
        }
        static bool GroundFollow(RiderState state)=>state==RiderState.Grounded || state==RiderState.Landing || state==RiderState.Rolling;
        void OnDisable() { if(mountedCamera!=null)mountedCamera.enabled=true; }
        void LateUpdate() { if(!rider.Paused) Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if(dt<=0 || rider.Paused)return;
            var s=rider.settings.camera;
            bool rolling=rider.State==RiderState.Rolling;
            bool followRollTravel=rolling || previous==RiderState.Rolling;
            Vector3 travel=rider.transform.position-lastRiderPosition;
            lastRiderPosition=rider.transform.position;
            if(previous!=rider.State)
            {
                bool continuingGlide=(previous==RiderState.Deploying || previous==RiderState.Glide) && (rider.State==RiderState.Deploying || rider.State==RiderState.Glide);
                bool continuingGround=GroundFollow(previous) && GroundFollow(rider.State);
                previous=rider.State;
                if(!continuingGround)
                {
                    blendTime=0;blendStart=transform.position;blendRotation=transform.rotation;
                    yaw=transform.eulerAngles.y;followVelocity=Vector3.zero;
                    if(!continuingGlide){freeLook.Reset();orbitReady=false;orbitRadiusVelocity=0;}
                    mountedCamera.Snap();
                }
            }
            blendTime+=dt;
            if(rider.Mounted && rider.mount.Mode==MantaServiceState.Piloted)
            {
                mountedCamera.Tick(dt);
                if(!mountedCamera.ImmediateView && blendTime<s.blendDuration)
                {float t=Mathf.SmoothStep(0,1,blendTime/s.blendDuration);transform.position=Vector3.Lerp(blendStart,transform.position,t);transform.rotation=Quaternion.Slerp(blendRotation,transform.rotation,t);}
                return;
            }
            bool rear=rider.LookBackHeld;
            bool immediate=rear || wasLookingBack;
            if(immediate){followVelocity=Vector3.zero;blendTime=Mathf.Max(blendTime,s.blendDuration);}
            bool gliding=rider.State==RiderState.Glide || rider.State==RiderState.Deploying;
            // Wind transports the rider in world space, but must not steer the camera
            // or pump its zoom/FOV. Use the same air-relative velocity as the glide motor.
            Vector3 velocity=rider.Motor.Velocity;
            if(gliding)velocity-=WindManager.GetWindAt(rider.transform.position)*rider.Glide.windInfluence;
            float speed=velocity.magnitude, speed01=Mathf.InverseLerp(rider.settings.glide.stallSpeed,rider.settings.glide.maximumSpeed,speed);
            Vector2 look=rider.LookInput;
            Quaternion rotation;
            if(gliding)
            {
                Vector3 v=speed>1?velocity.normalized:rider.transform.forward;
                Vector3 flat=Vector3.ProjectOnPlane(v,Vector3.up);
                if(flat.sqrMagnitude>.04f)stableForward=Vector3.Slerp(stableForward,flat.normalized,MantaFlightSettings.Damp(s.rotationDamping,dt));
                float stable= Mathf.Max(Mathf.InverseLerp(s.verticalFallbackStart,s.verticalFallbackEnd,Mathf.Abs(v.y)),1-Mathf.InverseLerp(0,s.lowSpeedFallback,speed));
                Vector3 safe=Vector3.Slerp(v,stableForward,stable).normalized;
                if(safe.sqrMagnitude<.01f)safe=Vector3.forward;
                if(!rear)freeLook.Tick(look,dt,true);
                float bank=Mathf.Clamp(-rider.Glide.Bank*s.bankFraction,-s.maximumRoll,s.maximumRoll);
                rotation=Quaternion.LookRotation(safe,Vector3.up)*(rear?Quaternion.Euler(0,180,0):freeLook.Rotation)*Quaternion.Euler(0,0,bank);
            }
            else
            {
                if(!rear){yaw+=look.x;pitch=Mathf.Clamp(pitch-look.y,-25,65);}
                rotation=rear?Quaternion.Euler(15,rider.transform.eulerAngles.y+180,0):Quaternion.Euler(pitch,yaw,0);
            }
            rotation=Quaternion.Slerp(blendRotation,rotation,Mathf.SmoothStep(0,1,blendTime/Mathf.Max(.01f,s.blendDuration)));
            float distance=gliding?s.glideDistance+speed01*s.speedPullback:rider.Airborne?s.fallDistance:s.groundDistance;
            if(rider.Mounted)distance=rider.mount.manta.settings.cameraDistance;
            Vector3 focus=rider.transform.position+Vector3.up*s.groundHeight;
            if(hips!=null && rider.State==RiderState.Grounded)
                standingHipHeight=Mathf.Lerp(standingHipHeight,hips.position.y-rider.transform.position.y,MantaFlightSettings.Damp(6,dt));
            float rollDip=rolling && hips!=null?Mathf.Clamp(hips.position.y-rider.transform.position.y-standingHipHeight,-.55f,.15f):0;
            rollFocusOffset=Mathf.SmoothDamp(rollFocusOffset,rollDip,ref rollFocusVelocity,.1f,Mathf.Infinity,dt);
            if(!gliding && !rider.Mounted)focus+=Vector3.up*rollFocusOffset;
            Vector3 desired=focus-rotation*Vector3.forward*distance;
            Vector3 position;
            if(gliding)
            {
                // Rotate along the orbit instead of smoothing a chord through the Rider.
                Vector3 direction=-(rotation*Vector3.forward);
                orbitDirection=!orbitReady || immediate?direction:Vector3.Slerp(orbitDirection,direction,MantaFlightSettings.Damp(s.rotationDamping,dt)).normalized;
                orbitRadius=!orbitReady || immediate?distance:Mathf.SmoothDamp(orbitRadius,distance,ref orbitRadiusVelocity,s.lag,Mathf.Infinity,dt);
                if(immediate)orbitRadiusVelocity=0;
                orbitReady=true;
                position=focus+orbitDirection*orbitRadius;
            }
            else
            {
                // Carry actual collision-resolved travel through the roll, rather than making
                // the camera accelerate from rest and catch up after the short animation ends.
                var carried=transform.position+(followRollTravel && travel.sqrMagnitude<16?travel:Vector3.zero);
                position=immediate?desired:Vector3.SmoothDamp(carried,desired,ref followVelocity,rolling?Mathf.Min(s.lag,.07f):s.lag,Mathf.Infinity,dt);
            }
            float pulse=rider.LandingPulse*rider.settings.ground.landingShake*Mathf.Sin(rider.StateTime*s.landingPulseFrequency);
            if(!gliding)position+=Vector3.up*pulse;
            Vector3 ray=position-focus;
            float clearance=CameraClearance(focus,ray.normalized,ray.magnitude,s.collisionRadius);
            if(clearance<ray.magnitude)
            {position=focus+ray.normalized*Mathf.Max(.2f,clearance-.1f);followVelocity=Vector3.zero;}
            transform.position=position;
            transform.rotation=gliding?Quaternion.LookRotation((focus-position).normalized,rotation*Vector3.up):immediate?rotation:Quaternion.Slerp(transform.rotation,rotation,MantaFlightSettings.Damp(s.rotationDamping,dt));
            wasLookingBack=rear;
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,gliding?Mathf.Lerp(s.minFOV,s.maxFOV,speed01):s.minFOV,MantaFlightSettings.Damp(s.rotationDamping,dt));
        }
        float CameraClearance(Vector3 origin,Vector3 direction,float distance,float radius)
        {
            int count=Physics.SphereCastNonAlloc(origin,radius,direction,cameraHits,distance,rider.settings.environment,QueryTriggerInteraction.Ignore);
            var hits=count==cameraHits.Length?Physics.SphereCastAll(origin,radius,direction,distance,rider.settings.environment,QueryTriggerInteraction.Ignore):cameraHits;
            if(hits!=cameraHits)count=hits.Length;
            float nearest=distance;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(rider.transform))continue;
                nearest=Mathf.Min(nearest,hits[i].distance);
            }
            return nearest;
        }
    }
}
