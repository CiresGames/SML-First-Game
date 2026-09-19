using UnityEngine;

namespace MantaFlight
{
    public sealed class MantaManeuvers : MonoBehaviour
    {
        public MantaTrick Current { get; private set; }
        public float VisualRoll { get; private set; }
        public Quaternion VisualRotationOffset { get; private set; } = Quaternion.identity;
        public Vector3 VisualPositionOffset { get; private set; }
        public float VisualReturnSpeed { get; private set; } = 160;
        public float VisualRecoveryTime { get; private set; } = .3f;
        public float SwerveCameraSignal { get; private set; }
        float swerveSide = 1, proximity;
        Collider avoidanceSurface;
        public float Progress { get; private set; }
        public bool IsLooping => Current == MantaTrick.LoopForward || Current == MantaTrick.LoopBackward;
        public bool IsImpactTurning => Current == MantaTrick.ImpactTurn;
        public Vector3 EntryPosition { get; private set; }
        public Quaternion EntryHeading => entry;
        public Vector3 EntryVelocity { get; private set; }
        public float EntrySpeed { get; private set; }
        public float Duration => duration;
        public bool ControlsHeading => IsLooping || IsImpactTurning || Current == MantaTrick.Turnaround;
        public bool LocksSpeed => Current != MantaTrick.None;
        public bool HasTravelOverride { get; private set; }
        public Vector3 TravelDirection { get; private set; }
        public int ImpactCount { get; private set; }
        public float LoopSteeringMargin => activeLoop == null ? 0 : activeLoop.maximumExitDeviation;
        Quaternion entry, entryVisualOffset;
        float elapsed, duration, cooldown, impactCooldown, turnSign = 1, rotationFraction;
        float loopMinimumSpeed, loopMaximumSpeed;
        Vector2 steeringOffset;
        MantaLoopSettings activeLoop;
        MantaImpactSettings activeImpact;
        Vector3 impactNormal, impactDesiredExit, impactExit;

        public bool TryStart(MantaTrick trick, MantaController controller, float steering = 0)
        {
            if (!controller.settings.Has(FlightPhase.AdvancedManeuvers) || Current != MantaTrick.None || cooldown > 0
                || trick == MantaTrick.None || trick == MantaTrick.ImpactTurn) return false;
            Begin(trick, controller);
            turnSign = steering < -.1f ? -1 : 1;
            var s = controller.settings;
            if (IsLooping)
            {
                // Snapshot one figure's feel: editing the profile mid-figure applies on the next trigger.
                activeLoop = JsonUtility.FromJson<MantaLoopSettings>(JsonUtility.ToJson(trick == MantaTrick.LoopForward ? s.forwardLoop : s.backwardLoop));
                duration = activeLoop.ResolveDuration(EntrySpeed);
                loopMinimumSpeed = s.minimumSpeed; loopMaximumSpeed = s.diveMaximumSpeed;
                if (EntryVelocity.sqrMagnitude > .01f)
                    entry = Quaternion.FromToRotation(entry * Vector3.forward, EntryVelocity.normalized) * entry;
                entryVisualOffset = Quaternion.Inverse(entry) * controller.Heading;
                VisualRotationOffset = entryVisualOffset;
                VisualRecoveryTime = Mathf.Max(.01f, activeLoop.visualRecoveryTime);
                VisualReturnSpeed = activeLoop.releaseUprightSpeed;
            }
            else duration = trick == MantaTrick.Turnaround ? s.turnaroundDuration : s.barrelDuration;
            return true;
        }
        void Begin(MantaTrick trick, MantaController controller)
        {
            Current = trick; elapsed = Progress = rotationFraction = 0; steeringOffset = Vector2.zero;
            entry = controller.Heading; EntryPosition = controller.GetComponent<Rigidbody>().position;
            EntryVelocity = controller.Velocity; EntrySpeed = controller.Speed;
            VisualRoll = 0; VisualPositionOffset = Vector3.zero; VisualRotationOffset = Quaternion.identity; HasTravelOverride = false;
        }
        public void ProbeObstacles(MantaController controller)
        {
            var s = controller.settings; var a = s.swerve;
            if (!a.enabled || !s.Has(FlightPhase.AdvancedManeuvers)) return;
            Vector3 incoming = controller.Velocity.sqrMagnitude > .01f ? controller.Velocity.normalized : controller.Heading * Vector3.forward;
            float distance = Mathf.Max(s.collisionSkin, a.lookAheadDistance + controller.Speed * a.lookAheadSeconds);
            if (!Physics.SphereCast(controller.GetComponent<Rigidbody>().position, Mathf.Max(s.collisionRadius, a.probeRadius), incoming,
                out var hit, distance, s.environmentMask & a.layers, QueryTriggerInteraction.Ignore)) return;
            proximity = Mathf.Clamp01(1 - hit.distance / distance);
            if (!TryImpact(controller, hit, incoming) && IsImpactTurning && hit.collider != avoidanceSurface && Eligible(controller, hit, incoming))
            {
                avoidanceSurface = hit.collider; impactNormal = hit.normal;
                impactDesiredExit = ChooseEscape(controller, hit, incoming);
            }
        }
        bool Eligible(MantaController controller, RaycastHit hit, Vector3 incoming)
        {
            var a = controller.settings.swerve;
            return a.enabled && controller.settings.Has(FlightPhase.AdvancedManeuvers) && controller.Speed >= a.minimumSpeed
                && hit.collider != null && (a.layers.value & (1 << hit.collider.gameObject.layer)) != 0
                && (string.IsNullOrEmpty(a.requiredTag) || hit.collider.tag == a.requiredTag)
                && Vector3.Dot(-incoming.normalized, hit.normal) >= Mathf.Cos(a.maximumAngleFromNormal * Mathf.Deg2Rad);
        }
        Vector3 ChooseEscape(MantaController controller, RaycastHit hit, Vector3 incoming)
        {
            var a = controller.settings.swerve;
            Vector3 tangent = Vector3.ProjectOnPlane(incoming, hit.normal);
            if (tangent.sqrMagnitude < .04f) tangent = Vector3.ProjectOnPlane(controller.Heading * Vector3.right, hit.normal);
            if (tangent.sqrMagnitude < .001f) tangent = Vector3.Cross(hit.normal, Vector3.forward);
            tangent.Normalize();
            Vector3 best = Outward(tangent, hit.normal, a.minimumOutwardDot);
            float score = float.NegativeInfinity;
            Vector3 second = Vector3.Cross(hit.normal, tangent).normalized;
            foreach (var side in new[] { tangent, -tangent, second, -second })
            {
                Vector3 candidate = Outward(side, hit.normal, a.minimumOutwardDot);
                float clearance = Physics.SphereCast(controller.GetComponent<Rigidbody>().position, controller.settings.collisionRadius,
                    candidate, out var obstruction, a.escapeProbeDistance, controller.settings.environmentMask, QueryTriggerInteraction.Ignore)
                    ? obstruction.distance : a.escapeProbeDistance;
                float value = clearance + Vector3.Dot(candidate, incoming) * 2;
                if (value > score) { score = value; best = candidate; }
            }
            swerveSide = Vector3.Dot(best, controller.Heading * Vector3.right) < 0 ? -1 : 1;
            return best;
        }
        public bool TryImpact(MantaController controller, RaycastHit hit, Vector3 incoming)
        {
            if (IsImpactTurning || !Eligible(controller, hit, incoming)) return false;
            // A different obstacle must remain avoidable during the same-surface cooldown.
            if (impactCooldown > 0 && hit.collider == avoidanceSurface) return false;
            Quaternion previousVisual = VisualRotationOffset * Quaternion.Euler(0, 0, VisualRoll);
            Cancel(); Begin(MantaTrick.ImpactTurn, controller);
            entryVisualOffset = previousVisual; VisualRotationOffset = previousVisual;
            activeImpact = JsonUtility.FromJson<MantaImpactSettings>(JsonUtility.ToJson(controller.settings.swerve));
            duration = Mathf.Max(.1f, activeImpact.duration); impactCooldown = duration + activeImpact.repeatCooldown;
            impactNormal = hit.normal.normalized; avoidanceSurface = hit.collider;
            impactDesiredExit = impactExit = ChooseEscape(controller, hit, incoming);
            TravelDirection = incoming; HasTravelOverride = true; ImpactCount++;
            return true;
        }
        public void ResolveImpactContact(Vector3 normal)
        {
            // Redirect the ongoing escape without restarting its animation or cooldown in a corner.
            Vector3 combined = impactNormal + normal;
            impactNormal = combined.sqrMagnitude > .01f ? combined.normalized : normal;
            impactDesiredExit = Outward(Outward(impactDesiredExit, normal, activeImpact.minimumOutwardDot), impactNormal, activeImpact.minimumOutwardDot);
            TravelDirection = Outward(TravelDirection, normal, activeImpact.minimumOutwardDot);
        }
        static Vector3 Outward(Vector3 direction, Vector3 normal, float minimumDot)
        {
            direction.Normalize(); minimumDot = Mathf.Clamp(minimumDot, .01f, .8f);
            if (Vector3.Dot(direction, normal) >= minimumDot) return direction;
            Vector3 tangent = Vector3.ProjectOnPlane(direction, normal);
            return tangent.sqrMagnitude < .0001f ? normal :
                tangent.normalized * Mathf.Sqrt(1 - minimumDot * minimumDot) + normal * minimumDot;
        }
        public void Step(MantaController controller, float dt, FlightInput input = default)
        {
            HasTravelOverride = false;
            cooldown = Mathf.Max(0, cooldown - dt); impactCooldown = Mathf.Max(0, impactCooldown - dt);
            if (Current == MantaTrick.None)
            {
                SwerveCameraSignal = Mathf.MoveTowards(SwerveCameraSignal, 0, dt / Mathf.Max(.01f, controller.settings.swerve.cameraBlendOut));
                return;
            }
            float oldProgress = Progress;
            elapsed += dt; Progress = Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration));
            if (IsLooping) { StepLoop(controller, input, dt); return; }
            if (IsImpactTurning) { StepImpact(controller, input, dt); return; }
            float t = Mathf.SmoothStep(0, 1, Progress);
            switch (Current)
            {
                case MantaTrick.RollLeft: VisualRoll = 360 * t; break;
                case MantaTrick.RollRight: VisualRoll = -360 * t; break;
                case MantaTrick.Turnaround:
                    controller.SetHeading(Quaternion.AngleAxis(180 * t * turnSign, Vector3.up) * entry);
                    VisualRoll = -turnSign * 72 * Mathf.Sin(t * Mathf.PI);
                    controller.ScaleSpeed(Mathf.Pow(controller.settings.turnaroundSpeedRetention, Progress - oldProgress));
                    break;
            }
            if (Progress >= 1) Finish(controller, controller.settings.rollSpeed);
        }
        void StepLoop(MantaController controller, FlightInput input, float dt)
        {
            if (elapsed >= activeLoop.inputLockTime && activeLoop.brakeCanCancel && input.brake >= activeLoop.brakeCancelThreshold)
            { Finish(controller, activeLoop.releaseUprightSpeed); return; }
            if (elapsed >= activeLoop.inputLockTime)
            {
                float influence = activeLoop.steeringInfluence * Mathf.Clamp01(activeLoop.steeringCurve == null ? 1 : activeLoop.steeringCurve.Evaluate(Progress));
                steeringOffset += input.steering * (influence * activeLoop.steeringDegreesPerSecond * dt);
                steeringOffset = Vector2.ClampMagnitude(steeringOffset, activeLoop.maximumExitDeviation);
            }
            Quaternion nominal = EvaluateLoopHeading(Progress, ref rotationFraction);
            Quaternion correction = Quaternion.AngleAxis(steeringOffset.x, Vector3.up)
                * Quaternion.AngleAxis(-steeringOffset.y, entry * Vector3.right);
            controller.SetHeading(correction * entry);
            controller.SetSpeed(EvaluateLoopSpeed(Progress));
            TravelDirection = controller.Heading * Vector3.forward; HasTravelOverride = true;
            float alignment = activeLoop.inputLockTime <= 0 ? 1 : Mathf.SmoothStep(0, 1, elapsed / activeLoop.inputLockTime);
            VisualRotationOffset = Quaternion.Inverse(entry) * nominal * Quaternion.Slerp(entryVisualOffset, Quaternion.identity, alignment);
            float angle = rotationFraction * Mathf.PI * 2;
            float sign = Current == MantaTrick.LoopForward ? -1 : 1;
            VisualPositionOffset = new Vector3(0, sign * activeLoop.visualRadius * (1 - Mathf.Cos(angle)), activeLoop.visualRadius * Mathf.Sin(angle));
            if (Progress >= 1) Finish(controller, activeLoop.releaseUprightSpeed);
        }
        public Quaternion EvaluateLoopHeading(float progress, ref float previousFraction)
        {
            previousFraction = Mathf.Max(previousFraction, activeLoop.RotationFraction(progress));
            float sign = Current == MantaTrick.LoopForward ? 1 : -1;
            return entry * Quaternion.AngleAxis(sign * 360 * previousFraction, Vector3.right);
        }
        public float EvaluateLoopSpeed(float progress) => Mathf.Clamp(EntrySpeed * activeLoop.SpeedFactor(progress), loopMinimumSpeed, loopMaximumSpeed);
        void StepImpact(MantaController controller, FlightInput input, float dt)
        {
            bool released = elapsed >= activeImpact.inputLockTime;
            if (released && activeImpact.brakeCanCancel && input.brake >= activeImpact.brakeCancelThreshold
                && Vector3.Dot(controller.Heading * Vector3.forward, impactNormal) > activeImpact.minimumOutwardDot)
            { Finish(controller, activeImpact.releaseUprightSpeed); return; }
            if (released)
            {
                steeringOffset += input.steering * (activeImpact.steeringInfluence * activeImpact.steeringDegreesPerSecond * dt);
                steeringOffset = Vector2.ClampMagnitude(steeringOffset, activeImpact.maximumExitDeviation);
            }
            impactExit = Vector3.Slerp(impactExit, impactDesiredExit, MantaFlightSettings.Damp(activeImpact.normalAdaptation, dt)).normalized;
            Vector3 exit = Quaternion.Euler(-steeringOffset.y, steeringOffset.x, 0) * impactExit;
            exit = Outward(exit, impactNormal, activeImpact.minimumOutwardDot);
            float curve = activeImpact.RotationFraction(Progress);
            rotationFraction = Mathf.Max(rotationFraction, Mathf.Clamp01(curve));
            if (Progress >= 1) rotationFraction = 1;
            Vector3 up = Mathf.Abs(Vector3.Dot(exit, Vector3.up)) > .95f ? entry * Vector3.right : Vector3.up;
            controller.SetHeading(Quaternion.Slerp(entry, Quaternion.LookRotation(exit, up), rotationFraction));
            TravelDirection = controller.Heading * Vector3.forward;
            HasTravelOverride = true;
            float slowdown = Mathf.Clamp01(activeImpact.slowdownCurve == null ? 0 : activeImpact.slowdownCurve.Evaluate(Progress)) * (4 * Progress * (1 - Progress));
            float factor = Mathf.Lerp(1, activeImpact.exitSpeedFactor, Mathf.SmoothStep(0, 1, Progress)) - (1 - activeImpact.minimumSpeedFactor) * slowdown;
            factor = Mathf.Max(Mathf.Min(activeImpact.minimumSpeedFactor, activeImpact.exitSpeedFactor), factor);
            controller.SetSpeed(EntrySpeed * Mathf.Max(.1f, factor));
            float envelope = Mathf.Clamp01(activeImpact.cameraEnvelope == null ? 0 : activeImpact.cameraEnvelope.Evaluate(Progress));
            float signal = swerveSide * envelope * Mathf.Lerp(.35f, 1, controller.Speed01) * Mathf.Lerp(.5f, 1, proximity);
            SwerveCameraSignal = Mathf.MoveTowards(SwerveCameraSignal, signal, dt / Mathf.Max(.01f, activeImpact.cameraBlendIn));
            VisualRoll = -swerveSide * activeImpact.banking * Mathf.Sin(rotationFraction * Mathf.PI);
            VisualRotationOffset = Quaternion.Slerp(entryVisualOffset, Quaternion.identity, rotationFraction);
            if (Progress >= 1) Finish(controller, activeImpact.releaseUprightSpeed);
        }
        void Finish(MantaController controller, float uprightSpeed)
        {
            bool controlledHeading = ControlsHeading;
            Current = MantaTrick.None; VisualRoll = 0; VisualPositionOffset = Vector3.zero; VisualRotationOffset = Quaternion.identity;
            cooldown = controller.settings.maneuverCooldown;
            if (controlledHeading) controller.BeginManeuverRelease(uprightSpeed);
        }
        public void Cancel()
        {
            Current = MantaTrick.None; VisualRoll = elapsed = Progress = cooldown = 0;
            VisualPositionOffset = Vector3.zero; VisualRotationOffset = Quaternion.identity; HasTravelOverride = false;
        }
        public void ResetState() { Cancel(); impactCooldown = 0; ImpactCount = 0; SwerveCameraSignal = 0; avoidanceSurface = null; }
    }
}
