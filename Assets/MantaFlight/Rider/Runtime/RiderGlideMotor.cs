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
            Yaw = Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
            Pitch = Mathf.Clamp(-Mathf.Asin(v.y) * Mathf.Rad2Deg, -settings.glide.pitchLimit, settings.glide.pitchLimit);
            Bank = 0; smoothedInput = Vector2.zero; recovery = 0;
            Stalled = velocity.magnitude < settings.glide.stallSpeed;
        }

        public Vector3 Integrate(Vector3 velocity, Vector2 input, float deployment, float dt)
        {
            var s = settings.glide;
            if (dt <= 0) return velocity;
            float speed = velocity.magnitude;
            Vector3 direction = speed > .1f ? velocity / speed : Heading * Vector3.forward;
            smoothedInput = Vector2.Lerp(smoothedInput, input, MantaFlightSettings.Damp(s.inputSmoothing, dt));
            Pitch += smoothedInput.y * s.pitchRate * dt;
            Bank = Mathf.MoveTowards(Bank, smoothedInput.x * s.bankLimit, s.bankRate * dt);

            // Relâcher le pitch retrouve une incidence porteuse, même après un décrochage.
            // La vitesse reste intégrée par les forces : aucune réinitialisation ni rétraction automatique.
            float pathPitch = -Mathf.Asin(Mathf.Clamp(direction.y, -1, 1)) * Mathf.Rad2Deg;
            float sink = Mathf.Max(s.minimumSink, Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude / Mathf.Max(1, s.targetGlideRatio));
            float wantedLift = Mathf.Max(0, s.gravity + (-sink - velocity.y) * s.neutralTrimResponse);
            float liftCapacity = Mathf.Max(.01f, s.liftCoefficient * speed * speed);
            float wantedIncidence = Mathf.Asin(Mathf.Clamp01(wantedLift / liftCapacity)) * Mathf.Rad2Deg * .5f;
            wantedIncidence = Mathf.Min(wantedIncidence, Mathf.Max(1, s.stallAngle - s.stallHysteresis));
            float trimPitch = Stalled ? pathPitch : pathPitch + s.trimAngle - wantedIncidence;
            float neutral = 1 - Mathf.Abs(smoothedInput.y);
            float targetPitch = Mathf.LerpAngle(Pitch, trimPitch, MantaFlightSettings.Damp(s.neutralTrimResponse * neutral, dt));
            Pitch = Mathf.MoveTowardsAngle(Pitch, targetPitch, s.pitchRate * dt);
            Pitch = Mathf.Clamp(Pitch, -s.pitchLimit, s.pitchLimit);

            // Le banking redirige aussi la trajectoire, pas seulement le modèle. Rotation sans ajout d'énergie.
            float authority = Mathf.InverseLerp(s.stallSpeed * .5f, s.recoverSpeed, speed) * deployment;
            float yawStep = Mathf.Sin(Bank * Mathf.Deg2Rad) * s.yawRate * authority * dt;
            velocity = Quaternion.AngleAxis(yawStep, Vector3.up) * velocity;
            direction = speed > .1f ? velocity / speed : Heading * Vector3.forward;
            if (Vector3.ProjectOnPlane(direction, Vector3.up).sqrMagnitude > .001f)
                Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            else Yaw += yawStep;
            Vector3 forward = Heading * Vector3.forward;
            Vector3 up = Heading * Vector3.up;
            AngleOfAttack = Mathf.Atan2(-Vector3.Dot(direction, up), Vector3.Dot(direction, forward)) * Mathf.Rad2Deg + s.trimAngle;
            if (speed < s.stallSpeed || Mathf.Abs(AngleOfAttack) > s.stallAngle)
            { Stalled = true; recovery = 0; }
            else if (Stalled)
            {
                // La récupération dépend de l'air relatif, jamais du signe du pitch dans le monde.
                if (speed >= s.recoverSpeed && Mathf.Abs(AngleOfAttack) <= s.stallAngle - s.stallHysteresis) recovery += dt;
                else recovery = 0;
                if (recovery >= s.stallRecoveryTime) Stalled = false;
            }
            float coefficient = Mathf.Sin(2 * Mathf.Clamp(AngleOfAttack, -s.stallAngle, s.stallAngle) * Mathf.Deg2Rad);
            Vector3 liftDirection = Vector3.ProjectOnPlane(up, direction).normalized;
            float lift = s.liftCoefficient * speed * speed * coefficient * (Stalled ? s.stallLiftFraction : 1) * deployment;
            float drag = (s.baseDrag + s.inducedDrag * coefficient * coefficient) * speed * speed;
            Vector3 acceleration = Vector3.down * Mathf.Lerp(settings.ground.gravity, s.gravity, deployment)
                + liftDirection * lift - direction * drag * deployment;
            acceleration += Vector3.down * Mathf.Max(0, -forward.y) * s.diveAcceleration * deployment;
            return Vector3.ClampMagnitude(velocity + acceleration * dt, Mathf.Max(s.maximumSpeed, s.recoverSpeed));
        }
    }
}
