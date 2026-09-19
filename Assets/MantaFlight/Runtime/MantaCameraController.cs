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
        Vector3 loopPosition, loopLook;
        float loopFOV, loopBlend;
        bool wasLooping;
        void OnEnable() { if (target != null) target.Respawned += Snap; }
        void OnDisable() { if (target != null) target.Respawned -= Snap; }
        void Awake() { lens = GetComponent<Camera>(); }
        public void Snap() { snap = true; positionVelocity = Vector3.zero; wasLooping = false; loopBlend = 0; }
        void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (target == null) return;
            if (target.Paused || target.GetComponent<MantaInput>().MenuOpen) return;
            var s = target.settings;
            if (dt <= 0) return;
            if (maneuvers == null) maneuvers = target.GetComponent<MantaManeuvers>();
            bool looping = maneuvers != null && maneuvers.IsLooping;
            if (looping && !wasLooping) FrameLoop();
            wasLooping = looping;
            Vector3 forward = target.transform.forward;
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (!looping && flat.sqrMagnitude > .06f) flatForward = flat.normalized;
            // Limit camera pitch and exclude barrel-roll rotation; the horizon stays readable.
            float elevation = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg, -48, 48);
            Quaternion desired = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(-elevation * .65f, 0, 0);
            if (!looping)
                followRotation = snap ? desired : Quaternion.Slerp(followRotation, desired, MantaFlightSettings.Damp(s.cameraRotationDamping, dt));
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
            // Freeze the SHOT, not just yaw: no chasing the manta's position, velocity or pitch during a loop.
            loopBlend = looping ? 1 : Mathf.MoveTowards(loopBlend, 0, dt / Mathf.Max(.1f, s.loopCameraReturnTime));
            float blend = Mathf.SmoothStep(0, 1, loopBlend);
            desiredPosition = Vector3.Lerp(desiredPosition, loopPosition, blend);
            look = Vector3.Lerp(look, loopLook, blend);
            desiredBank *= 1 - blend;
            fov = Mathf.Lerp(fov, loopFOV, blend);
            Vector3 smooth = snap ? desiredPosition : Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity,
                Mathf.Lerp(s.cameraLag, s.loopCameraPullbackTime, blend), Mathf.Infinity, dt);
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
            Quaternion lookRotation = Quaternion.LookRotation((look - smooth).normalized, Vector3.up) * Quaternion.Euler(0, 0, bank);
            transform.rotation = snap ? lookRotation : Quaternion.Slerp(transform.rotation, lookRotation, MantaFlightSettings.Damp(s.cameraRotationDamping * 1.8f, dt));
            lens.fieldOfView = snap ? fov : Mathf.Lerp(lens.fieldOfView, fov, MantaFlightSettings.Damp(2.5f, dt));
            snap = false;
        }

        void FrameLoop()
        {
            var s = target.settings;
            // Estimate the swept trajectory once in the incoming camera frame. Frame the entire curve,
            // including the wing span, instead of orbiting around the flipping flight direction.
            Quaternion view = Quaternion.LookRotation(transform.forward, Vector3.up);
            Quaternion inverse = Quaternion.Inverse(view);
            Vector3 origin = maneuvers.EntryPosition;
            Vector3 position = origin;
            Vector3 direction = maneuvers.EntryVelocity.normalized;
            if (direction.sqrMagnitude < .5f) direction = maneuvers.EntryHeading * Vector3.forward;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            const int samples = 128;
            float dt = maneuvers.Duration / samples;
            float sign = maneuvers.Current == MantaTrick.LoopForward ? 1 : -1;
            for (int i = 1; i <= samples; i++)
            {
                float progress = (float)i / samples;
                float angle = sign * 360 * Mathf.SmoothStep(0, 1, progress);
                Vector3 heading = maneuvers.EntryHeading * Quaternion.AngleAxis(angle, Vector3.right) * Vector3.forward;
                direction = Vector3.Slerp(direction, heading, MantaFlightSettings.Damp(28, dt)).normalized;
                position += direction * target.Speed * dt;
                bounds.Encapsulate(inverse * (position - origin));
            }
            bounds.Expand(Mathf.Max(1, s.loopCameraPadding) * 2);
            loopFOV = Mathf.Max(lens.fieldOfView, Mathf.Lerp(s.minimumFOV, s.maximumFOV, target.Speed01));
            float tan = Mathf.Tan(loopFOV * Mathf.Deg2Rad * .5f);
            float fit = Mathf.Max(bounds.extents.y / tan, bounds.extents.x / (tan * Mathf.Max(.1f, lens.aspect)));
            Vector3 localCamera = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z - fit);
            // Never pull closer than the normal chase distance when entering a small loop.
            localCamera.z = Mathf.Min(localCamera.z, (inverse * (transform.position - origin)).z);
            loopPosition = origin + view * localCamera;
            loopLook = origin + view * bounds.center;
            positionVelocity = Vector3.zero;
        }
    }
}
