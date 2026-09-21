using UnityEngine;
namespace MantaFlight.Rider
{
    public enum MantaServiceState { Piloted, Hover, Idle, Intercept, Retry, Landing, Catch, Following }
    [DefaultExecutionOrder(-20)]
    public sealed class MantaMountAdapter : MonoBehaviour, IMount
    {
        public MantaController manta;
        public Transform seat;
        public RiderController rider;
        public MantaServiceState Mode { get; private set; }
        public Transform Seat => seat;
        public Vector3 Velocity => Mode == MantaServiceState.Piloted ? manta.Velocity : velocity;
        public bool Calling => Mode == MantaServiceState.Intercept || Mode == MantaServiceState.Retry || Mode == MantaServiceState.Landing;
        public Vector3 InterceptTarget { get; private set; }
        public int PassCount { get; private set; }
        public Vector3 LandingNormal { get; private set; } = Vector3.up;
        public bool Landed => landed;
        public float AutonomousBank { get; private set; }
        Vector3 velocity, retryTarget, followForward = Vector3.forward, previousRelative;
        Vector3 sampledRiderPosition, sampledRiderVelocity, smoothFollowPosition;
        float sampleTime;
        bool haveRiderSample;
        Vector3 Position => manta.PhysicsPosition;
        Vector3 Forward => manta.PhysicsRotation * Vector3.forward;
        Vector3 PhysicsSeatPosition => Position + manta.PhysicsRotation * Vector3.Scale(transform.InverseTransformPoint(seat.position), transform.lossyScale);
        Quaternion landingRotation;
        float callTime, passTime, followTime, closestLandedDistance;
        bool landed, relativeValid;
        MantaInput input;
        MountFeel S => rider.settings.mount;
        void Awake() { input = manta.GetComponent<MantaInput>(); }
        void LateUpdate()
        {
            if (rider == null || rider.Paused) return;
            sampledRiderPosition = rider.transform.position; sampledRiderVelocity = rider.Motor.Velocity;
            sampleTime = Time.time; haveRiderSample = true;
        }
        void FixedUpdate() { if (rider != null && !rider.Paused) Tick(Time.fixedDeltaTime); }

        public void Tick(float dt)
        {
            if (dt <= 0) return;
            if (Mode == MantaServiceState.Piloted)
            {
                if (rider.Mounted && input.State.brake >= S.hoverBrakeThreshold && manta.Speed <= S.hoverEntrySpeed
                    && GroundDistance() <= S.dismountHeight)
                { velocity = manta.Velocity; Mode = MantaServiceState.Hover; manta.Paused = true; }
                return;
            }
            manta.Paused = true;
            if (Mode == MantaServiceState.Idle || Mode == MantaServiceState.Hover || Mode == MantaServiceState.Landing || Mode == MantaServiceState.Catch)
                AutonomousBank = Mathf.Lerp(AutonomousBank, 0, MantaFlightSettings.Damp(manta.settings.bankingSmoothing, dt));
            if (Mode == MantaServiceState.Hover)
            {
                // Hand ownership back before submitting a pose; the piloted FixedUpdate will move once.
                if (rider.Mounted && input.State.throttle > .1f) { AttachRider(); return; }
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, S.hoverDeceleration * dt); Move(velocity, dt);
                return;
            }
            if (Mode == MantaServiceState.Idle)
            {
                velocity = Vector3.MoveTowards(velocity, Vector3.zero, S.hoverDeceleration * dt); Move(velocity, dt);
                // Une manta posée attend près du rider, puis reprend le suivi s'il s'éloigne.
                float riderDistance = Vector3.Distance(Position, rider.transform.position);
                closestLandedDistance = Mathf.Min(closestLandedDistance, riderDistance);
                if (!rider.Attached && (!landed || riderDistance > Mathf.Max(S.followDistance, closestLandedDistance + S.followDistance))) StartFollowing();
                return;
            }
            if (Mode == MantaServiceState.Following) { Follow(dt); return; }
            if (Mode == MantaServiceState.Catch) { Move(velocity, dt); return; }
            callTime += dt; passTime += dt;
            if (callTime > S.callTimeout || Vector3.Distance(Position, rider.transform.position) > S.maximumCallDistance)
            { CancelCall(); return; }
            if (!rider.Airborne && Mode != MantaServiceState.Landing)
            {
                CallToPosition(rider.transform.position, rider.Motor.Velocity, false);
                if (Mode != MantaServiceState.Landing) return;
            }
            Vector3 desired;
            if (Mode == MantaServiceState.Landing)
            {
                Vector3 error = InterceptTarget - Position;
                float brakingSpeed = Mathf.Sqrt(2 * S.callAcceleration * error.magnitude);
                desired = Vector3.ClampMagnitude(error * S.approachResponse, Mathf.Min(S.callSpeed, brakingSpeed));
                Steer(desired, dt);
                if (error.magnitude <= S.landingTolerance && velocity.magnitude <= S.dismountSpeed)
                { landed = true; closestLandedDistance = Vector3.Distance(Position, rider.transform.position); Mode = MantaServiceState.Idle; }
                return;
            }
            if (Mode == MantaServiceState.Retry)
            {
                InterceptTarget = retryTarget;
                desired = Vector3.ClampMagnitude((retryTarget - Position) * S.approachResponse, S.callSpeed);
                if (passTime >= S.retryDuration) { Mode = MantaServiceState.Intercept; passTime = 0; relativeValid = false; }
            }
            else
            {
                Vector3 rv = rider.Motor.Velocity;
                Vector3 relative = PhysicsSeatPosition - rider.transform.position;
                float distance = relative.magnitude;
                if (TryCapture(relative, rv)) return;
                Vector3 forward = rv.sqrMagnitude > 4 ? rv.normalized : rider.transform.forward;
                float approach = Mathf.InverseLerp(S.scoopRadius, Mathf.Max(S.scoopRadius + 1, S.matchDistance), distance);
                // Prédire le temps de fermeture, puis retirer progressivement l'avance à proximité.
                float closingSpeed = Mathf.Max(S.maximumRelativeSpeed, S.callSpeed - rv.magnitude);
                float horizon = Mathf.Min(S.predictionTime, distance / closingSpeed) * approach;
                Vector3 predicted = rider.transform.position + rv * horizon;
                InterceptTarget = predicted - forward * (S.approachBehind * approach) - Vector3.up * (S.approachBelow * approach);
                float speedLimit = Mathf.Min(S.callSpeed, rv.magnitude + Mathf.Max(S.maximumRelativeSpeed, Mathf.Sqrt(2 * S.callAcceleration * distance)));
                desired = Vector3.ClampMagnitude(rv + (InterceptTarget - PhysicsSeatPosition) * S.approachResponse, speedLimit);
                previousRelative = relative; relativeValid = true;
                Steer(desired, dt);
                if (TryCapture(PhysicsSeatPosition - rider.transform.position, rv)) return;
                if (passTime > S.passTimeout)
                {
                    PassCount++; Mode = MantaServiceState.Retry; passTime = 0; relativeValid = false;
                    retryTarget = rider.transform.position + rv * S.predictionTime - forward * S.approachBehind - Vector3.up * S.approachBelow;
                }
                return;
            }
            Steer(desired, dt);
        }

        bool TryCapture(Vector3 relative, Vector3 riderVelocity)
        {
            if (!rider.CanScoop || (velocity - riderVelocity).magnitude > S.maximumRelativeSpeed) return false;
            // Balayer le déplacement relatif évite de rater la fenêtre entre deux pas physiques.
            Vector3 closest = relative;
            if (relativeValid)
            {
                Vector3 segment = relative - previousRelative;
                float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(-Vector3.Dot(previousRelative, segment) / segment.sqrMagnitude) : 1;
                closest = previousRelative + segment * t;
            }
            if (closest.magnitude > S.scoopRadius || relative.magnitude > S.scoopRadius + S.maximumRelativeSpeed * Time.fixedDeltaTime) return false;
            Vector3 from = rider.transform.position + Vector3.up * rider.settings.ground.radius;
            Vector3 path = seat.position + Vector3.up * rider.settings.ground.radius - from;
            if (Physics.SphereCast(from, rider.settings.ground.radius, path.normalized, out _, path.magnitude,
                rider.settings.environment, QueryTriggerInteraction.Ignore)) return false;
            rider.BeginRemount(); relativeValid = false; return true;
        }

        void Follow(float dt)
        {
            followTime += dt;
            // Rider uses Update; sample once after Update and predict to this physics tick.
            // A time-based filter removes the render-rate staircase without changing movement ownership.
            Vector3 rv = rider.Motor.Velocity;
            Vector3 targetPosition = rider.transform.position;
            if (Time.inFixedTimeStep && haveRiderSample)
            {
                rv = sampledRiderVelocity;
                targetPosition = sampledRiderPosition + rv * Mathf.Clamp(Time.fixedTime - sampleTime, 0, Time.fixedDeltaTime * 2);
            }
            smoothFollowPosition = Vector3.Lerp(smoothFollowPosition, targetPosition,
                MantaFlightSettings.Damp(1 / Mathf.Max(.001f, S.followTargetSmoothing), dt));
            Vector3 flat = Vector3.ProjectOnPlane(rv, Vector3.up);
            Vector3 heading = flat.sqrMagnitude > 1 ? flat.normalized : rider.transform.forward;
            followForward = Vector3.Slerp(followForward, heading, MantaFlightSettings.Damp(S.followResponse, dt)).normalized;
            float phase = followTime * Mathf.PI * 2 / Mathf.Max(.1f, S.followSwayPeriod);
            Vector3 right = Vector3.Cross(Vector3.up, followForward);
            InterceptTarget = smoothFollowPosition - followForward * S.followDistance + Vector3.up * S.followHeight
                + (right * Mathf.Sin(phase) + Vector3.up * Mathf.Sin(phase * .5f)) * S.followSway;
            // Conserver de la garde au sol, même lorsque le rider descend une pente.
            if (Physics.Raycast(InterceptTarget + Vector3.up * S.landingProbeHeight, Vector3.down, out var ground,
                S.landingProbeHeight * 2, rider.settings.environment, QueryTriggerInteraction.Ignore))
                InterceptTarget = new Vector3(InterceptTarget.x, Mathf.Max(InterceptTarget.y, ground.point.y + S.hoverHeight), InterceptTarget.z);
            Vector3 feedForward = rider.Airborne ? rv : flat;
            Vector3 desired = Vector3.ClampMagnitude(feedForward + (InterceptTarget - Position) * S.followResponse, S.callSpeed);
            Steer(desired, dt);
        }
        void Steer(Vector3 desired, float dt)
        {
            // Réutiliser le banking de vol normal pour garder la créature vivante en pilotage autonome.
            if (Mode == MantaServiceState.Following || Mode == MantaServiceState.Intercept || Mode == MantaServiceState.Retry)
            {
                Vector3 from = Vector3.ProjectOnPlane(Forward, Vector3.up);
                Vector3 to = Vector3.ProjectOnPlane(desired, Vector3.up);
                float turn = to.sqrMagnitude > 1 ? Vector3.SignedAngle(from, to, Vector3.up) : 0;
                float targetBank = -Mathf.Clamp(turn / Mathf.Max(1, manta.settings.yawSpeed), -1, 1) * manta.settings.maximumBanking;
                AutonomousBank = Mathf.Lerp(AutonomousBank, targetBank, MantaFlightSettings.Damp(manta.settings.bankingSmoothing, dt));
            }
            // Accélération vectorielle bornée : freinage et changement de cap sans orbite interminable.
            velocity = Vector3.MoveTowards(velocity, desired, S.callAcceleration * dt);
            Move(velocity, dt);
        }
        void StartFollowing()
        {
            Mode = MantaServiceState.Following; landed = false; relativeValid = false;
            smoothFollowPosition = rider.transform.position;
            followForward = Vector3.ProjectOnPlane(Forward, Vector3.up).normalized;
            if (followForward.sqrMagnitude < .01f) followForward = Vector3.forward;
        }
        void Move(Vector3 motion,float dt)
        {
            Vector3 position = Position;
            Vector3 delta = motion * dt;
            for(int i=0;i<3 && delta.sqrMagnitude > .00001f;i++)
            {
                float length=delta.magnitude;
                if(!Physics.SphereCast(position,manta.settings.collisionRadius,delta/length,out var hit,length+manta.settings.collisionSkin,manta.settings.environmentMask,QueryTriggerInteraction.Ignore)) {position+=delta;break;}
                float travel=Mathf.Clamp(hit.distance-manta.settings.collisionSkin,0,length);position+=delta.normalized*travel;
                delta=Vector3.ProjectOnPlane(delta.normalized*(length-travel),hit.normal);
                velocity=Vector3.ProjectOnPlane(velocity,hit.normal);
            }
            Quaternion heading=manta.Heading;
            if (Mode == MantaServiceState.Landing || (Mode == MantaServiceState.Idle && landed))
                heading = Quaternion.RotateTowards(heading, landingRotation, S.callTurnRate * dt);
            else if(velocity.sqrMagnitude>1)
            {
                Vector3 forward=velocity.normalized;
                Vector3 up=Mathf.Abs(forward.y)>.96f ? heading*Vector3.up : Vector3.up;
                heading=Quaternion.RotateTowards(heading,Quaternion.LookRotation(forward,up),S.callTurnRate*dt);
            }
            manta.MoveExternalMotion(position,heading,velocity);
        }
        float GroundDistance() => Physics.Raycast(Position,Vector3.down,out var hit,100,rider.settings.environment,QueryTriggerInteraction.Ignore) ? hit.distance : 100;
        public bool CanDismount(out Vector3 groundPosition)
        {
            groundPosition=Vector3.zero;
            if(Velocity.magnitude>S.dismountSpeed || GroundDistance()>S.dismountHeight) return false;
            for(int side=1;side>=-1;side-=2)
            {
                Vector3 origin=seat.position+seat.right*S.sideDistance*side;
                if(!Physics.Raycast(origin,Vector3.down,out var hit,S.dismountHeight+4,rider.settings.environment,QueryTriggerInteraction.Ignore)) continue;
                if(Vector3.Angle(hit.normal,Vector3.up)>rider.Motor.Capsule.slopeLimit) continue;
                Vector3 point=hit.point+Vector3.up*.04f;
                if(Physics.CheckCapsule(point+Vector3.up*rider.settings.ground.radius,point+Vector3.up*(rider.settings.ground.standingHeight-rider.settings.ground.radius),rider.settings.ground.radius,rider.settings.environment,QueryTriggerInteraction.Ignore)) continue;
                Vector3 path=point-seat.position;
                float radius=rider.settings.ground.radius;
                if(Physics.CapsuleCast(seat.position+Vector3.up*radius,seat.position+Vector3.up*(rider.settings.ground.standingHeight-radius),radius,
                    path.normalized,out var block,path.magnitude,rider.settings.environment,QueryTriggerInteraction.Ignore)) continue;
                groundPosition=point;return true;
            }
            return false;
        }
        public bool CanMountGround() => landed && !Calling && Velocity.magnitude<=S.dismountSpeed
            && Vector3.Distance(rider.transform.position,seat.position)<=S.landedMountDistance;
        public void Dismount()
        {
            velocity = Velocity;
            landed = GroundDistance() <= S.dismountHeight && velocity.magnitude <= S.dismountSpeed;
            Mode = MantaServiceState.Idle; manta.Paused = true; input.ControlEnabled = false; relativeValid = false;
            landingRotation = manta.Heading; closestLandedDistance = Vector3.Distance(Position, rider.transform.position);
            manta.GetComponent<MantaManeuvers>().Cancel();
        }
        public void ToggleCall() { if (Calling) CancelCall(); else CallToPosition(rider.transform.position, rider.Motor.Velocity, rider.Airborne); }
        public void CallToPosition(Vector3 position, Vector3 riderVelocity, bool airborne)
        {
            if (Vector3.Distance(transform.position, position) > S.maximumCallDistance) return;
            if (!airborne)
            {
                Vector3 beside = position + rider.transform.right * S.landingOffset;
                if (Physics.Raycast(beside + Vector3.up * S.landingProbeHeight, Vector3.down, out var hit,
                    S.landingProbeHeight * 2, rider.settings.environment, QueryTriggerInteraction.Ignore) && RequestLanding(hit.point, hit.normal)) return;
                CancelCall(); return;
            }
            callTime = passTime = 0; PassCount = 1; landed = false; relativeValid = false;
            InterceptTarget = position + riderVelocity * S.predictionTime;
            Mode = MantaServiceState.Intercept; manta.Paused = true;
        }
        public bool IsLandingSurfaceValid(Vector3 point, Vector3 normal)
        {
            if (normal.sqrMagnitude < .5f || Vector3.Angle(normal, Vector3.up) > S.maximumLandingSlope
                || Vector3.Distance(point, rider.transform.position) > S.landingAimRange) return false;
            Vector3 destination = point + normal.normalized * S.hoverHeight;
            return !Physics.CheckSphere(destination, manta.settings.collisionRadius + manta.settings.collisionSkin,
                rider.settings.environment, QueryTriggerInteraction.Ignore);
        }
        public bool RequestLanding(Vector3 point, Vector3 normal)
        {
            if (!IsLandingSurfaceValid(point, normal)) return false;
            LandingNormal = normal.normalized;
            InterceptTarget = point + LandingNormal * S.hoverHeight;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, LandingNormal).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.ProjectOnPlane(rider.transform.forward, LandingNormal).normalized;
            landingRotation = Quaternion.LookRotation(forward, LandingNormal);
            Mode = MantaServiceState.Landing; landed = false; callTime = passTime = 0;
            manta.Paused = true; relativeValid = false; return true;
        }
        public void CancelCall() { if (Calling) StartFollowing(); }
        public void BeginCatch(Vector3 riderVelocity) { Mode = MantaServiceState.Catch; velocity = Vector3.Lerp(velocity, riderVelocity, .8f); }
        public void AttachRider()
        {
            float speed = Velocity.magnitude;
            Mode = MantaServiceState.Piloted; landed = false; AutonomousBank = 0; input.ControlEnabled = true; manta.Paused = false;
            manta.UpdateExternalVelocity(manta.Heading, manta.Heading * Vector3.forward * Mathf.Max(manta.settings.minimumSpeed, speed));
        }
        void OnDrawGizmosSelected()
        {
            if (rider == null || seat == null || rider.settings == null) return;
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(seat.position, S.scoopRadius);
            Gizmos.color = Color.yellow; Gizmos.DrawLine(transform.position, InterceptTarget); Gizmos.DrawWireSphere(InterceptTarget, .6f);
            Gizmos.color = Color.green; Gizmos.DrawLine(transform.position, transform.position + Vector3.down * S.dismountHeight);
            if (Mode == MantaServiceState.Landing) Gizmos.DrawRay(InterceptTarget, LandingNormal * S.hoverHeight);
        }
    }
}
