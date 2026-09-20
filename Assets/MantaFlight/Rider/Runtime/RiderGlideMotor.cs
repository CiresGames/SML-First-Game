using UnityEngine;
namespace MantaFlight.Rider
{
    public sealed class RiderGlideMotor : MonoBehaviour
    {
        public RiderSettings settings;
        public float Pitch { get; private set; }
        public float Bank { get; private set; }
        public float Yaw { get; private set; }
        public float AngleOfAttack { get; private set; }
        public bool Stalled { get; private set; }
        public Quaternion Heading => Quaternion.Euler(Pitch, Yaw, -Bank);
        Vector2 smoothedInput;
        float recovery;
        public void Deploy(Vector3 velocity)
        {
            Vector3 v = velocity.sqrMagnitude > 1 ? velocity.normalized : transform.forward;
            Yaw = Mathf.Atan2(v.x,v.z) * Mathf.Rad2Deg;
            Pitch = Mathf.Clamp(-Mathf.Asin(v.y) * Mathf.Rad2Deg, -30, 60);
            Bank = 0; smoothedInput = Vector2.zero; recovery = 0;
            Stalled = velocity.magnitude < settings.glide.stallSpeed;
        }
        public Vector3 Integrate(Vector3 velocity, Vector2 input, float deployment, float dt)
        {
            var s = settings.glide;
            smoothedInput = Vector2.Lerp(smoothedInput, input, MantaFlightSettings.Damp(s.inputSmoothing, dt));
            Pitch = Mathf.Clamp(Pitch + smoothedInput.y * s.pitchRate * dt, -s.pitchLimit, s.pitchLimit);
            Bank = Mathf.MoveTowards(Bank, smoothedInput.x * s.bankLimit, s.bankRate * dt);
            float speed = velocity.magnitude;
            Yaw += Mathf.Sin(Bank * Mathf.Deg2Rad) * s.yawRate * Mathf.InverseLerp(s.stallSpeed * .5f, s.recoverSpeed * 2, speed) * dt;
            Vector3 forward = Heading * Vector3.forward;
            Vector3 direction = speed > .1f ? velocity / speed : forward;
            Vector3 right = Heading * Vector3.right;
            AngleOfAttack = -Vector3.SignedAngle(direction, forward, right) + s.trimAngle;
            if (speed < s.stallSpeed || Mathf.Abs(AngleOfAttack) > s.stallAngle) { Stalled = true; recovery = 0; }
            else if (Stalled && speed > s.recoverSpeed && Pitch > 0)
            { recovery += dt; if (recovery >= s.stallRecoveryTime) Stalled = false; }
            
            float pathPitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg; // + = descending
            float coefficient = Mathf.Sin(2 * Mathf.Clamp(AngleOfAttack, -s.stallAngle, s.stallAngle) * Mathf.Deg2Rad);
            Vector3 liftDirection = Vector3.ProjectOnPlane(Heading * Vector3.up, direction).normalized;
            float lift = s.liftCoefficient * speed * speed * coefficient * (Stalled ? s.stallLiftFraction : 1) * deployment;
            float drag = (s.baseDrag + s.inducedDrag * coefficient * coefficient) * speed * speed;
            Vector3 acceleration = Vector3.down * Mathf.Lerp(settings.ground.gravity, s.gravity, deployment)
                + liftDirection * lift - direction * drag * deployment;
            // Extra dive energy is directed downwards; pitching level never creates free altitude.
            acceleration += Vector3.down * Mathf.Max(0, -forward.y) * s.diveAcceleration * deployment;
            velocity += acceleration * dt;
            if (Pitch >= -2 && !Stalled && deployment > 0)  
            {
                float sink = Mathf.Max(s.minimumSink, Vector3.ProjectOnPlane(velocity,Vector3.up).magnitude / Mathf.Max(1,s.targetGlideRatio));
                velocity.y = Mathf.MoveTowards(velocity.y, Mathf.Min(velocity.y,-sink), s.gravity * dt * deployment);
            }

            return Vector3.ClampMagnitude(velocity, Mathf.Max(s.maximumSpeed, s.recoverSpeed));
        }
    }
}
