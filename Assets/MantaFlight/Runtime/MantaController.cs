using UnityEngine;

namespace MantaFlight
{
    [RequireComponent(typeof(Rigidbody), typeof(MantaInput), typeof(MantaManeuvers))]
    public sealed class MantaController : MonoBehaviour
    {
        public MantaFlightSettings settings;
        public MantaFlightSettings SourceSettings { get; private set; }
        public float Speed { get; private set; }
        public Vector3 Velocity { get; private set; }
        public Quaternion Heading { get; private set; }
        public float Bank { get; private set; }
        public float Pitch => -Mathf.Asin(Mathf.Clamp((Heading * Vector3.forward).y, -1, 1)) * Mathf.Rad2Deg;
        public float Speed01 => Mathf.InverseLerp(settings.minimumSpeed, settings.maximumSpeed, Speed);
        public float GroundClearance { get; private set; } = 500;
        public float Acceleration { get; private set; }
        public bool Paused { get; set; }
        public event System.Action Respawned;
        Rigidbody body;
        MantaInput input;
        MantaManeuvers maneuvers;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float pitch, yaw, yawRate, pitchRate;
        int groundCounter;
        bool recoveringOrientation;
        float recoverySpeed;

        void Awake()
        {
            // Play-mode tuning never edits the shared asset accidentally.
            SourceSettings = settings; settings = Instantiate(settings);
            body = GetComponent<Rigidbody>(); input = GetComponent<MantaInput>(); maneuvers = GetComponent<MantaManeuvers>();
            if (settings.useProfileInput) settings.input.Apply(input);
            body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate;
            spawnPosition = transform.position; spawnRotation = transform.rotation;
            input.ResetRequested += ResetFlight;
            ResetFlight();
        }
        void OnDestroy() { if (input != null) input.ResetRequested -= ResetFlight; if (settings != null) Destroy(settings); }
        void FixedUpdate()
        {
            if (Paused || input.MenuOpen) return;
            var trick = input.ConsumeTrick();
            if (trick != MantaTrick.None) maneuvers.TryStart(trick, this, input.State.steering.x);
            Simulate(input.State, Time.fixedDeltaTime);
        }
        public void Simulate(FlightInput state, float dt)
        {
            var s = settings;
            bool advanced = s.Has(FlightPhase.AdvancedManeuvers);
            bool energy = s.Has(FlightPhase.SpeedAndCamera);
            bool tight = advanced && state.tightTurn;
            float before = Speed;
            maneuvers.ProbeObstacles(this);
            if (!maneuvers.LocksSpeed)
            {
                float power = state.throttle * s.acceleration * Mathf.Max(.1f, s.accelerationCurve.Evaluate(Speed01));
                float drag = state.brake * s.deceleration;
                if (state.throttle < .01f && state.brake < .01f)
                    power += Mathf.Clamp(s.cruiseSpeed - Speed, -s.cruiseRelaxation, s.cruiseRelaxation);
                if (energy)
                {
                    float vertical = (Heading * Vector3.forward).y;
                    power += Mathf.Max(0, -vertical) * s.diveAcceleration;
                    drag += Mathf.Max(0, vertical) * s.climbDeceleration;
                }
                if (tight) drag += Mathf.Abs(state.steering.x) * s.tightTurnDrag;
                float limit = energy ? s.diveMaximumSpeed : s.maximumSpeed;
                if (Speed >= s.maximumSpeed) power -= state.throttle * s.acceleration * Mathf.Max(.1f, s.accelerationCurve.Evaluate(Speed01));
                Speed = Mathf.Clamp(Speed + (power - drag) * dt, s.minimumSpeed, limit);
            }
            if (!maneuvers.ControlsHeading)
            {
                float verticalInput = Mathf.Clamp(state.steering.y - (energy ? state.dive : 0), -1, 1);
                float response = state.steering.sqrMagnitude > .001f || state.dive > .01f ? s.turnAcceleration : s.turnDamping;
                yawRate = Mathf.Lerp(yawRate, state.steering.x * s.yawSpeed * Mathf.Lerp(.75f, 1.18f, Speed01) * (tight ? s.tightTurnMultiplier : 1), MantaFlightSettings.Damp(response, dt));
                pitchRate = Mathf.Lerp(pitchRate, -verticalInput * s.pitchSpeed, MantaFlightSettings.Damp(response, dt));
                yaw += yawRate * dt;
                pitch = Mathf.Clamp(pitch + pitchRate * dt, -s.pitchLimit, s.pitchLimit);
                Quaternion stableHeading = Quaternion.Euler(pitch, yaw, 0);
                Heading = recoveringOrientation ? Quaternion.RotateTowards(Heading, stableHeading, recoverySpeed * dt) : stableHeading;
                if (Quaternion.Angle(Heading, stableHeading) < .1f) recoveringOrientation = false;
            }
            maneuvers.Step(this, dt, state);
            float targetBank = -state.steering.x * s.maximumBanking * Mathf.Lerp(.55f, 1, Speed01) * (tight ? 1.55f : 1);
            targetBank = Mathf.Clamp(targetBank, -78, 78);
            float easedBank = Mathf.Lerp(Bank, targetBank, MantaFlightSettings.Damp(s.bankingSmoothing, dt));
            Bank = Mathf.MoveTowards(Bank, easedBank, s.rollSpeed * dt);
            Vector3 wanted = Heading * Vector3.forward;
            Vector3 direction = Vector3.Slerp(Velocity.sqrMagnitude > .01f ? Velocity.normalized : wanted, wanted,
                MantaFlightSettings.Damp(maneuvers.ControlsHeading ? 28 : s.momentumResponse * (tight ? 2 : 1), dt)).normalized;
            if (maneuvers.HasTravelOverride) direction = maneuvers.TravelDirection;
            Velocity = direction * Speed;
            MoveSafely(Velocity * dt);
            body.MoveRotation(Heading);
            Acceleration = (Speed - before) / dt;
            if (++groundCounter % 5 == 0)
                GroundClearance = Physics.Raycast(body.position, Vector3.down, out var ground, 1500, s.environmentMask, QueryTriggerInteraction.Ignore) ? ground.distance : 1500;
            if (body.position.y < -80 || body.position.sqrMagnitude > 16000000) ResetFlight();
        }
        void MoveSafely(Vector3 delta)
        {
            Vector3 position = body.position;
            // Sweep and slide: no frame-sized teleport through thin walls at dive speed.
            for (int pass = 0; pass < 3 && delta.sqrMagnitude > .00001f; pass++)
            {
                float length = delta.magnitude;
                if (!Physics.SphereCast(position, settings.collisionRadius, delta / length, out var hit,
                    length + settings.collisionSkin, settings.environmentMask, QueryTriggerInteraction.Ignore))
                { position += delta; break; }
                float travel = Mathf.Clamp(hit.distance - settings.collisionSkin, 0, length);
                position += delta.normalized * travel;
                if (maneuvers.IsImpactTurning || maneuvers.TryImpact(this, hit, delta.normalized))
                {
                    maneuvers.ResolveImpactContact(hit.normal);
                    Velocity = maneuvers.TravelDirection * Speed;
                    delta = maneuvers.TravelDirection * (length - travel);
                    continue;
                }
                delta = Vector3.ProjectOnPlane(delta.normalized * (length - travel), hit.normal);
                Speed = Mathf.Max(settings.minimumSpeed, Speed * .85f);
                Velocity = Vector3.ProjectOnPlane(Velocity, hit.normal);
                if (maneuvers.ControlsHeading) BeginManeuverRelease(settings.rollSpeed);
                maneuvers.Cancel();
                SyncAngles();
            }
            body.MovePosition(position);
        }
        public void SetHeading(Quaternion value) { Heading = value; SyncAngles(); }
        void SyncAngles()
        {
            Vector3 forward = Heading * Vector3.forward;
            pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
            if (new Vector2(forward.x, forward.z).sqrMagnitude > .0001f) yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }
        public void ScaleSpeed(float factor) { Speed *= factor; }
        public void SetSpeed(float value) => Speed = Mathf.Clamp(value, settings.minimumSpeed, settings.diveMaximumSpeed);
        public void BeginManeuverRelease(float uprightSpeed)
        {
            SyncAngles(); yawRate = pitchRate = 0;
            recoveringOrientation = true; recoverySpeed = Mathf.Max(1, uprightSpeed);
        }
        public void ResetFlight()
        {
            if (body == null) return;
            maneuvers.ResetState(); Heading = spawnRotation; SyncAngles();
            yawRate = pitchRate = Bank = Acceleration = 0; recoveringOrientation = false;
            Speed = settings.cruiseSpeed; Velocity = Heading * Vector3.forward * Speed;
            body.position = spawnPosition; body.rotation = Heading;
            transform.SetPositionAndRotation(spawnPosition, Heading);
            Respawned?.Invoke();
        }
    }
}
