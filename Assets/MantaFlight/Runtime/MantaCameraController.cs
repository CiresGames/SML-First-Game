using UnityEngine;

namespace MantaFlight
{
    [RequireComponent(typeof(Camera))]
    public sealed class MantaCameraController : MonoBehaviour
    {
        public MantaController target;
        Camera lens;
        Vector3 positionVelocity;
        Vector3 flatForward = Vector3.forward;
        Quaternion followRotation;
        float bank;
        bool snap = true;
        MantaManeuvers maneuvers;
        Vector3 previousRoot;
        float loopBlend;
        void OnEnable() { if (target != null) target.Respawned += Snap; }
        void OnDisable() { if (target != null) target.Respawned -= Snap; }
        void Awake() { lens = GetComponent<Camera>(); }
        public void Snap() { snap = true; positionVelocity = Vector3.zero; loopBlend = 0; }
        void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (target == null) return;
            if (target.Paused || target.GetComponent<MantaInput>().MenuOpen) return;
            var s = target.settings;
            if (dt <= 0) return;
            if (maneuvers == null) maneuvers = target.GetComponent<MantaManeuvers>();
            bool looping = maneuvers != null && maneuvers.IsLooping;
            var lc = s.loopCamera;
            loopBlend = Mathf.MoveTowards(loopBlend, looping ? 1 : 0, dt / Mathf.Max(.01f, looping ? lc.blendIn : lc.blendOut));
            float blend = Mathf.SmoothStep(0, 1, loopBlend);
            float rotationDamping = Mathf.Lerp(s.cameraRotationDamping, lc.rotationDamping, blend);
            Vector3 forward = target.transform.forward;
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude > .06f) flatForward = flat.normalized;
            // Limit camera pitch and exclude barrel-roll rotation; the horizon stays readable.
            float elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg, -48, 48);
            Quaternion desired = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(-elevation * .65f, 0, 0);
            followRotation = snap ? desired : Quaternion.Slerp(followRotation, desired, MantaFlightSettings.Damp(rotationDamping, dt));
            bool speedFeel = s.Has(FlightPhase.SpeedAndCamera);
            float speed = speedFeel ? target.Speed01 : .25f;
            float dive = Mathf.Max(0, -forward.y);
            float distance = s.cameraDistance + speed * s.speedCameraDistance + (speedFeel ? dive * 1.5f : 0);
            Vector3 focus = target.transform.position + Vector3.up * 1.1f;
            Vector3 desiredPosition = focus + followRotation * new Vector3(0, s.cameraHeight, -distance);
            Vector3 look = focus + Vector3.ClampMagnitude(target.Velocity * s.cameraAnticipation, 10);
            float desiredBank = s.Has(FlightPhase.Polish) ? target.Bank * s.cameraBankFraction : 0;
            float nearGround = 1 - Mathf.InverseLerp(5, 35, target.GroundClearance);
            float fov = Mathf.Lerp(s.minimumFOV, s.maximumFOV, speed) + (speedFeel ? dive * 2 + nearGround * speed * 2 : 0);
            // Only the stable flight root drives this close shot; the model's loop is local animation.
            desiredPosition = Vector3.Lerp(desiredPosition, focus + followRotation * (lc.offset + Vector3.back * Mathf.Max(1, lc.distance)), blend);
            look = Vector3.Lerp(look, focus + followRotation * lc.lookOffset, blend);
            desiredBank *= 1 - blend;
            fov += lc.fovChange * blend;
            float sway = maneuvers == null ? 0 : maneuvers.SwerveCameraSignal;
            desiredPosition += followRotation * Vector3.right * (sway * s.swerve.cameraOffset);
            desiredBank -= sway * s.swerve.cameraRoll;
            // Transport the loop camera with the root before damping the relative framing.
            Vector3 transported = transform.position + (target.transform.position - previousRoot) * blend;
            Vector3 smooth = snap ? desiredPosition : Vector3.SmoothDamp(transported, desiredPosition, ref positionVelocity,
                Mathf.Lerp(s.cameraLag, lc.positionLag, blend), Mathf.Infinity, dt);
            previousRoot = target.transform.position;
            Vector3 ray = smooth - focus;
            // Collision is resolved AFTER lag so smoothing can never drag the camera through a wall.
            if (Physics.SphereCast(focus, s.cameraCollisionRadius, ray.normalized, out var hit, ray.magnitude,
                s.environmentMask, QueryTriggerInteraction.Ignore))
            {
                smooth = focus + ray.normalized * Mathf.Max(.3f, hit.distance - .2f);
                positionVelocity = Vector3.zero;
            }
            transform.position = smooth;
            bank = Mathf.Lerp(bank, desiredBank, MantaFlightSettings.Damp(3, dt));
            Quaternion lookRotation = Quaternion.LookRotation((look - smooth).normalized, Vector3.up) * Quaternion.Euler(0, sway * s.swerve.cameraYaw, bank);
            transform.rotation = snap ? lookRotation : Quaternion.Slerp(transform.rotation, lookRotation, MantaFlightSettings.Damp(rotationDamping * 1.8f, dt));
            lens.fieldOfView = snap ? fov : Mathf.Lerp(lens.fieldOfView, fov, MantaFlightSettings.Damp(2.5f, dt));
            snap = false;
        }

    }
}
