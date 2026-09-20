using UnityEngine;
namespace MantaFlight.Rider
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RiderGroundMotor : MonoBehaviour
    {
        public RiderSettings settings;
        public Vector3 Velocity { get; set; }
        public bool Crouched { get; private set; }
        public bool Grounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float ImpactSpeed { get; private set; }
        public float HorizontalImpactSpeed { get; private set; }
        public CharacterController Capsule { get; private set; }
        public bool LandedThisStep { get; private set; }
        void Awake() { Capsule = GetComponent<CharacterController>(); }
        public float HeightAboveGround()
        { return Physics.Raycast(transform.position + Vector3.up * .05f, Vector3.down, out var hit, 2000, settings.environment, QueryTriggerInteraction.Ignore) ? Mathf.Max(0, hit.distance - .05f) : 2000; }
        public bool HasStandingRoom()
        {
            var s = settings.ground;
            return !Physics.CheckCapsule(transform.position + Vector3.up * (s.crouchHeight - s.radius),
                transform.position + Vector3.up * (s.standingHeight - s.radius), s.radius * .95f, settings.environment, QueryTriggerInteraction.Ignore);
        }
        public void SetCrouched(bool value)
        {
            if (!value && Crouched && !HasStandingRoom()) return;
            Crouched = value; Capsule.height = value ? settings.ground.crouchHeight : settings.ground.standingHeight;
            Capsule.center = Vector3.up * (Capsule.height * .5f);
        }
        public void GroundMove(RiderCommand command, Vector3 cameraForward, float dt, bool locked)
        {
            var s = settings.ground;
            if (!locked) SetCrouched(command.crouch);
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = transform.forward;
            Vector3 target = locked ? Vector3.zero : forward * command.move.y + Vector3.Cross(Vector3.up, forward) * command.move.x;
            float speed = Crouched ? s.crouchSpeed : command.run ? s.runSpeed : s.walkSpeed;
            Vector3 horizontal = Vector3.ProjectOnPlane(Velocity, Vector3.up);
            if (target.sqrMagnitude > .01f)
            {
                Vector3 direction = horizontal.sqrMagnitude > .25f ? horizontal.normalized : transform.forward;
                direction = Vector3.RotateTowards(direction, target.normalized, s.turnRate * Mathf.Deg2Rad * dt, 0);
                float magnitude = Mathf.MoveTowards(horizontal.magnitude, speed * target.magnitude, s.acceleration * dt);
                horizontal = direction * magnitude;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), s.turnRate * dt);
            }
            else horizontal = Vector3.MoveTowards(horizontal, Vector3.zero, s.deceleration * dt);
            Velocity = horizontal + Vector3.down * 2;
            Move(dt);
        }
        public void FallMove(RiderCommand command, Vector3 cameraForward, float dt)
        {
            var s = settings.ground;
            Vector3 forward = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
            Vector3 acceleration = (forward * command.move.y + Vector3.Cross(Vector3.up, forward) * command.move.x) * s.airAcceleration;
            Velocity += acceleration * dt + Vector3.down * s.gravity * dt;
            Velocity = new Vector3(Velocity.x, Mathf.Max(-s.terminalSpeed, Velocity.y), Velocity.z);
            Move(dt);
        }
        public void Jump() { Velocity = Vector3.ProjectOnPlane(Velocity, Vector3.up) + Vector3.up * Mathf.Sqrt(2 * settings.ground.gravity * settings.ground.jumpHeight); Grounded = false; }
        public void Move(float dt)
        {
            bool before = Grounded;
            ImpactSpeed = Mathf.Max(0, -Velocity.y); HorizontalImpactSpeed = Vector3.ProjectOnPlane(Velocity, Vector3.up).magnitude;
            var flags = Capsule.Move(Velocity * dt);
            bool probe = Physics.SphereCast(transform.position + Vector3.up * (settings.ground.radius + .04f), settings.ground.radius * .9f,
                Vector3.down, out var ground, settings.ground.groundProbe + .06f, settings.environment, QueryTriggerInteraction.Ignore)
                && Vector3.Angle(ground.normal, Vector3.up) <= Capsule.slopeLimit;
            Grounded = Velocity.y <= 0 && (((flags & CollisionFlags.Below) != 0) || probe);
            if (probe) GroundNormal = ground.normal;
            LandedThisStep = Grounded && !before;
            if ((flags & CollisionFlags.Above) != 0 && Velocity.y > 0) Velocity = new Vector3(Velocity.x, 0, Velocity.z);
            if (Grounded) Velocity = Vector3.ProjectOnPlane(Velocity, Vector3.up);
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y < .5f && Vector3.Dot(Velocity, hit.normal) < 0) Velocity = Vector3.ProjectOnPlane(Velocity, hit.normal);
        }
        public void Place(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            bool enabledBefore = Capsule.enabled; Capsule.enabled = false; transform.SetPositionAndRotation(position, rotation);
            Capsule.enabled = enabledBefore; Velocity = velocity; Grounded = false; LandedThisStep = false;
        }
    }
}
