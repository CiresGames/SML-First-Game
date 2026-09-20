using UnityEngine;
namespace MantaFlight.Rider
{
    [DefaultExecutionOrder(300)]
    public sealed class RiderCamera : MonoBehaviour
    {
        public RiderController rider;
        public MantaCameraController mountedCamera;
        Camera lens;
        Vector3 followVelocity,stableForward=Vector3.forward,blendStart;
        Quaternion blendRotation;
        Vector2 lookOffset;
        RiderState previous;
        float yaw,pitch=15,blendTime;
        void Start() { lens=GetComponent<Camera>();mountedCamera.enabled=false;previous=rider.State;blendTime=99; }
        void OnDisable() { if(mountedCamera!=null)mountedCamera.enabled=true; }
        void LateUpdate() { if(!rider.Paused) Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            var s=rider.settings.camera;
            if(previous!=rider.State)
            {
                previous=rider.State;blendTime=0;blendStart=transform.position;blendRotation=transform.rotation;
                yaw=transform.eulerAngles.y;followVelocity=Vector3.zero;lookOffset=Vector2.zero;
                mountedCamera.Snap();
            }
            blendTime+=dt;
            if(rider.Mounted && rider.mount.Mode==MantaServiceState.Piloted)
            {
                mountedCamera.Tick(dt);
                if(blendTime<s.blendDuration)
                {float t=Mathf.SmoothStep(0,1,blendTime/s.blendDuration);transform.position=Vector3.Lerp(blendStart,transform.position,t);transform.rotation=Quaternion.Slerp(blendRotation,transform.rotation,t);}
                return;
            }
            Vector3 velocity=rider.Motor.Velocity;
            float speed=velocity.magnitude, speed01=Mathf.InverseLerp(rider.settings.glide.stallSpeed,rider.settings.glide.maximumSpeed,speed);
            bool gliding=rider.State==RiderState.Glide || rider.State==RiderState.Deploying || rider.State==RiderState.JumpOff;
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
                lookOffset=Vector2.ClampMagnitude(lookOffset+look,s.lookLimit)*Mathf.Exp(-s.lookRecenter*dt);
                float bank=Mathf.Clamp(-rider.Glide.Bank*s.bankFraction,-s.maximumRoll,s.maximumRoll);
                rotation=Quaternion.LookRotation(safe,Vector3.up)*Quaternion.Euler(-lookOffset.y,lookOffset.x,bank);
            }
            else
            {
                yaw+=look.x;pitch=Mathf.Clamp(pitch-look.y,-25,65);
                rotation=Quaternion.Euler(pitch,yaw,0);
            }
            rotation=Quaternion.Slerp(blendRotation,rotation,Mathf.SmoothStep(0,1,blendTime/Mathf.Max(.01f,s.blendDuration)));
            float distance=gliding?s.glideDistance+speed01*s.speedPullback:rider.Airborne?s.fallDistance:s.groundDistance;
            if(rider.Mounted)distance=rider.mount.manta.settings.cameraDistance;
            Vector3 focus=rider.transform.position+Vector3.up*s.groundHeight;
            Vector3 desired=focus-rotation*Vector3.forward*distance;
            Vector3 position=Vector3.SmoothDamp(transform.position,desired,ref followVelocity,s.lag,Mathf.Infinity,dt);
            float pulse=rider.LandingPulse*rider.settings.ground.landingShake*Mathf.Sin(rider.StateTime*s.landingPulseFrequency);
            position+=Vector3.up*pulse;
            Vector3 ray=position-focus;
            if(Physics.SphereCast(focus,s.collisionRadius,ray.normalized,out var hit,ray.magnitude,rider.settings.environment,QueryTriggerInteraction.Ignore))
            {position=focus+ray.normalized*Mathf.Max(.2f,hit.distance-.1f);followVelocity=Vector3.zero;}
            transform.position=position;
            transform.rotation=Quaternion.Slerp(transform.rotation,rotation,MantaFlightSettings.Damp(s.rotationDamping,dt));
            lens.fieldOfView=Mathf.Lerp(lens.fieldOfView,gliding?Mathf.Lerp(s.minFOV,s.maxFOV,speed01):s.minFOV,MantaFlightSettings.Damp(s.rotationDamping,dt));
        }
    }
}
