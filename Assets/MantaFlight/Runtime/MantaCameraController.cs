using UnityEngine;

namespace MantaFlight
{
    [RequireComponent(typeof(Camera))]
    public sealed class MantaCameraController : MonoBehaviour
    {
        public MantaController target;
        public FlightFreeLook freeLook=new FlightFreeLook();
        Camera lens;
        Vector3 orbitDirection;
        float orbitRadius, radiusVelocity;
        readonly RaycastHit[] cameraHits=new RaycastHit[64];
        Vector3 flatForward = Vector3.forward;
        Quaternion followRotation;
        float bank;
        bool snap = true;
        bool wasLookingBack;
        public bool ImmediateView { get; private set; }
        MantaManeuvers maneuvers;
        void OnEnable() { if (target != null) target.Respawned += Snap; }
        void OnDisable() { if (target != null) target.Respawned -= Snap; }
        void Awake() { lens = GetComponent<Camera>(); }
        public void Snap() { snap = true; radiusVelocity = 0; freeLook.Reset(); }
        void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (target == null) return;
            if (target.Paused || target.GetComponent<MantaInput>().MenuOpen) return;
            var s = target.settings;
            if (dt <= 0) return;
            var input=target.GetComponent<MantaInput>();
            bool rear=input.LookBackHeld;
            ImmediateView=rear || wasLookingBack;
            if(ImmediateView)radiusVelocity=0;
            if(!rear)freeLook.Tick(input.LookDelta,dt,true);
            if (maneuvers == null) maneuvers = target.GetComponent<MantaManeuvers>();
            Vector3 forward = target.transform.forward;
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude > .06f) flatForward = flat.normalized;
            // Limit camera pitch and exclude barrel-roll rotation; the horizon stays readable.
            float elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg, -48, 48);
            Quaternion desired = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(-elevation * .65f, 0, 0);
            followRotation = snap ? desired : Quaternion.Slerp(followRotation, desired, MantaFlightSettings.Damp(s.cameraRotationDamping, dt));
            bool speedFeel = s.Has(FlightPhase.SpeedAndCamera);
            float speed = speedFeel ? target.Speed01 : .25f;
            float dive = Mathf.Max(0, -forward.y);
            float distance = s.cameraDistance + speed * s.speedCameraDistance + (speedFeel ? dive * 1.5f : 0);
            Vector3 focus = target.transform.position + Vector3.up * 1.1f;
            Quaternion orbit=(rear?desired:followRotation)*(rear?Quaternion.Euler(0,180,0):freeLook.Rotation);
            Vector3 desiredPosition = focus + orbit * new Vector3(0, s.cameraHeight, -distance);
            Vector3 look = focus;
            float desiredBank = s.Has(FlightPhase.Polish) ? target.Bank * s.cameraBankFraction : 0;
            float nearGround = 1 - Mathf.InverseLerp(5, 35, target.GroundClearance);
            float fov = Mathf.Lerp(s.minimumFOV, s.maximumFOV, speed) + (speedFeel ? dive * 2 + nearGround * speed * 2 : 0);
            float sway = maneuvers == null ? 0 : maneuvers.SwerveCameraSignal;
            desiredPosition += followRotation * Vector3.right * (sway * s.swerve.cameraOffset);
            desiredBank -= sway * s.swerve.cameraRoll;
            // Smooth around the sphere, never through its chord toward the manta.
            Vector3 desiredOffset=desiredPosition-focus;
            orbitDirection=snap || ImmediateView?desiredOffset.normalized:Vector3.Slerp(orbitDirection,desiredOffset.normalized,MantaFlightSettings.Damp(s.cameraRotationDamping*1.8f,dt)).normalized;
            orbitRadius=snap || ImmediateView?desiredOffset.magnitude:Mathf.SmoothDamp(orbitRadius,desiredOffset.magnitude,ref radiusVelocity,s.cameraLag,Mathf.Infinity,dt);
            Vector3 smooth=focus+orbitDirection*orbitRadius;
            Vector3 ray = smooth - focus;
            // Collision is resolved AFTER lag so smoothing can never drag the camera through a wall.
            float clearDistance=CameraClearance(focus,ray.normalized,ray.magnitude,s.cameraCollisionRadius,s.environmentMask);
            if(clearDistance<ray.magnitude)smooth=focus+ray.normalized*Mathf.Max(.3f,clearDistance-.2f);
            transform.position = smooth;
            bank = Mathf.Lerp(bank, desiredBank, MantaFlightSettings.Damp(3, dt));
            Quaternion lookRotation = Quaternion.LookRotation((look - smooth).normalized, Vector3.up) * Quaternion.Euler(0, sway * s.swerve.cameraYaw, bank);
            // Position already has angular smoothing; keep the subject centered throughout the orbit.
            transform.rotation = lookRotation;
            lens.fieldOfView = snap ? fov : Mathf.Lerp(lens.fieldOfView, fov, MantaFlightSettings.Damp(2.5f, dt));
            snap = false;
            wasLookingBack=rear;
        }

        float CameraClearance(Vector3 origin,Vector3 direction,float distance,float radius,int mask)
        {
            int count=Physics.SphereCastNonAlloc(origin,radius,direction,cameraHits,distance,mask,QueryTriggerInteraction.Ignore);
            var hits=count==cameraHits.Length?Physics.SphereCastAll(origin,radius,direction,distance,mask,QueryTriggerInteraction.Ignore):cameraHits;
            if(hits!=cameraHits)count=hits.Length;
            float nearest=distance;
            for(int i=0;i<count;i++)
            {
                var collider=hits[i].collider;
                if(collider.transform.IsChildOf(target.transform))continue;
                var rider=collider.GetComponentInParent<MantaFlight.Rider.RiderController>();
                if(rider!=null && rider.Attached && rider.mount!=null && rider.mount.manta==target)continue;
                nearest=Mathf.Min(nearest,hits[i].distance);
            }
            return nearest;
        }

    }
}
