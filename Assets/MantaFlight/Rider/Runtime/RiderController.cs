using UnityEngine;
namespace MantaFlight.Rider
{
    public enum RiderState { Mounted, Dismounting, Grounded, Falling, Deploying, Glide, Landing = 7, Rolling, Remounting }
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(RiderGroundMotor),typeof(RiderGlideMotor),typeof(RiderInput))]
    public sealed class RiderController : MonoBehaviour
    {
        public RiderSettings settings;
        public MantaMountAdapter mount;
        public MantaInput mantaInput;
        public Camera view;
        public bool startMounted = true;
        public RiderLandingTarget LandingTarget { get; private set; }
        public RiderState State { get; private set; }
        public RiderGroundMotor Motor { get; private set; }
        public RiderGlideMotor Glide { get; private set; }
        public RiderInput Input { get; private set; }
        public RiderSettings SourceSettings { get; private set; }
        public bool Airborne => State == RiderState.Falling || State == RiderState.Glide || State == RiderState.Deploying;
        public bool Mounted => State == RiderState.Mounted;
        public bool Attached => Mounted || State == RiderState.Remounting;
        public Vector2 LookInput { get; private set; }
        public float AirTime { get; private set; }
        public float SinceDetach { get; private set; }
        public float StateTime { get; private set; }
        public float LandingDuration => landingDuration;
        public float Height => Motor.HeightAboveGround();
        public float GlidePose { get; private set; }
        public float LandingPulse { get; private set; }
        public float RollProgress => State == RiderState.Rolling ? Mathf.Clamp01(StateTime / settings.ground.rollDuration) : 0;
        public bool GlideAvailable => State == RiderState.Falling && Height >= settings.glide.minimumDeployHeight
            && (AirTime >= settings.glide.minimumAirTime || -Motor.Velocity.y >= settings.glide.minimumDownSpeed);
        public bool Paused => mantaInput != null && mantaInput.MenuOpen;
        readonly RiderMountCollisionGuard mountCollisions = new RiderMountCollisionGuard();
        public bool IgnoringMountCollisions => mountCollisions.Active;
        float rollCooldown, landingDuration;
        Vector3 transitionStart, transitionEnd, transitionVelocity, rollDirection;
        Quaternion transitionRotation;
        void Awake()
        {
            SourceSettings = settings; settings = Instantiate(settings);
            Motor = GetComponent<RiderGroundMotor>(); Glide = GetComponent<RiderGlideMotor>(); Input = GetComponent<RiderInput>();
            Motor.settings = Glide.settings = Input.settings = settings;
            LandingTarget = GetComponent<RiderLandingTarget>();
            if (LandingTarget == null) LandingTarget = gameObject.AddComponent<RiderLandingTarget>();
            LandingTarget.rider = this;
        }
        void Start()
        {
            if (mount != null) { mount.rider = this; mount.manta.Respawned += ResetRider; }
            if (startMounted) ResetRider(); else SetState(RiderState.Falling);
        }
        void OnDisable() { mountCollisions.Restore(); }
        void OnDestroy()
        { if (mount != null) mount.manta.Respawned -= ResetRider; if (settings != null) Destroy(settings); }
        void Update() { if (!Paused) Tick(Input.Read(Time.deltaTime), Time.deltaTime); }
        public void Tick(RiderCommand command, float dt)
        {
            if (dt <= 0) return;
            mountCollisions.Tick(this, dt);
            LookInput = command.look;
            dt = Mathf.Min(dt,.05f); StateTime += dt; SinceDetach += dt;
            rollCooldown = Mathf.Max(0,rollCooldown-dt); LandingPulse = Mathf.MoveTowards(LandingPulse,0,dt / settings.camera.landingPulseDuration);
            if (State == RiderState.Grounded) LandingTarget.Tick(command.callHeld, command.callReleased);
            else LandingTarget.CancelPreview();
            if (command.call && Airborne) mount.ToggleCall();
            if (Airborne) AirTime += dt;
            Vector3 forward = view == null ? transform.forward : view.transform.forward;
            switch (State)
            {
                case RiderState.Mounted:
                    FollowSeat();
                    if (command.jumpOff) Detach(true);
                    else if (command.drop) Detach(true); // Legacy exit binding also uses a safe outward jump.
                    else if (command.context && mount.CanDismount(out _)) Detach(true);
                    break;
                case RiderState.Dismounting:
                    transform.position = Vector3.Lerp(transitionStart, transitionEnd, Mathf.SmoothStep(0,1,StateTime / settings.mount.dismountDuration));
                    if (StateTime >= settings.mount.dismountDuration) { Motor.Capsule.enabled = true; Motor.Velocity = Vector3.zero; SetState(RiderState.Falling); }
                    break;
                case RiderState.Grounded:
                case RiderState.Landing:
                    bool recovering = State == RiderState.Landing;
                    Motor.GroundMove(command,forward,dt,recovering);
                    if (!Motor.Grounded) { SetState(RiderState.Falling); break; }
                    AirTime = 0;
                    if (recovering) { if(StateTime >= landingDuration) SetState(RiderState.Grounded); break; }
                    if (command.context && mount.CanMountGround()) { BeginRemount(); break; }
                    if (command.roll && rollCooldown <= 0) { BeginRoll(); break; }
                    if (command.jump && Motor.HasStandingRoom()) { Motor.SetCrouched(false); Motor.Jump(); SetState(RiderState.Falling); }
                    break;
                case RiderState.Rolling:
                    Motor.Velocity = rollDirection * (settings.ground.rollDistance / Mathf.Max(.1f,settings.ground.rollDuration))
                        + Vector3.up * (Motor.Grounded ? -2 : Motor.Velocity.y - settings.ground.gravity * dt);
                    Motor.Move(dt);
                    if (StateTime >= settings.ground.rollDuration) { Motor.SetCrouched(command.crouch); SetState(Motor.Grounded ? RiderState.Grounded : RiderState.Falling); }
                    break;
                case RiderState.Falling:
                    if ((command.toggleGlide || (settings.secondJumpDeploys && command.jump)) && GlideAvailable) StartGlide();
                    if (State == RiderState.Falling) Motor.FallMove(command,forward,dt);
                    else { Motor.Velocity = Glide.Integrate(Motor.Velocity,command.glide,0,dt); Motor.Move(dt); }
                    ResolveLanding();
                    break;
                case RiderState.Deploying:
                case RiderState.Glide:
                    if (command.toggleGlide || (settings.secondJumpDeploys && command.jump)) { SetState(RiderState.Falling); Motor.FallMove(command,forward,dt); ResolveLanding(); break; }
                    float deployment = State == RiderState.Glide ? 1 : Mathf.Clamp01(StateTime/settings.glide.deployDuration);
                    Motor.Velocity = Glide.Integrate(Motor.Velocity,command.glide,deployment,dt); Motor.Move(dt);
                    transform.rotation = Quaternion.Euler(0,Glide.Yaw,0);
                    if (StateTime >= settings.glide.deployDuration && State != RiderState.Glide) SetState(RiderState.Glide);
                    ResolveLanding();
                    break;
                case RiderState.Remounting:
                    float t = Mathf.SmoothStep(0,1,StateTime/settings.mount.blendDuration);
                    // Continue the incoming trajectory while the saddle closes the remaining gap.
                    transform.position = Vector3.Lerp(transitionStart + transitionVelocity * StateTime, mount.Seat.position,t);
                    transform.rotation = Quaternion.Slerp(transitionRotation,mount.Seat.rotation,t);
                    if(StateTime >= settings.mount.blendDuration) { mount.AttachRider(); SetState(RiderState.Mounted); FollowSeat(); }
                    break;
            }
            float poseTarget = State == RiderState.Glide || State == RiderState.Deploying ? 1 : 0;
            GlidePose = Mathf.MoveTowards(GlidePose,poseTarget,dt / Mathf.Max(.01f,poseTarget > 0 ? settings.glide.deployDuration : settings.glide.retractDuration));
            if(transform.position.y < settings.resetBelowHeight || transform.position.sqrMagnitude > 16000000) mount.manta.ResetFlight();
        }
        void SetState(RiderState next) { State = next; StateTime = 0; }
        void StartGlide() { Motor.SetCrouched(false); Glide.Deploy(Motor.Velocity); SetState(RiderState.Deploying); }
        public void Detach(bool jumpImpulse)
        {
            if (!mountCollisions.TryGetLaunch(this, out var position, out var outward)) return;
            Vector3 velocity = mount.Velocity * settings.mount.velocityInheritance;
            if (jumpImpulse)
            {
                velocity += outward * settings.mount.jumpForwardSpeed;
                // A steep manta dive must not consume the entire upward jump impulse.
                velocity.y = Mathf.Max(velocity.y + settings.mount.jumpUpSpeed, settings.mount.jumpUpSpeed);
            }
            mount.Dismount(); transform.SetParent(null, true);
            Motor.Capsule.enabled = true; Motor.SetCrouched(false);
            Motor.Place(position, Quaternion.LookRotation(outward, Vector3.up), velocity);
            // Apply after Place: disabling/enabling CharacterController can reset IgnoreCollision.
            mountCollisions.Begin(this);
            SinceDetach = AirTime = 0; GlidePose = 0; SetState(RiderState.Falling);
        }
        void ResolveLanding()
        {
            if(!Motor.Grounded) return;
            LandingPulse = Mathf.Clamp01(Motor.ImpactSpeed / settings.ground.hardLandingImpact);
            AirTime = 0; mount.CancelCall();
            float angle = Mathf.Atan2(Motor.ImpactSpeed,Mathf.Max(.1f,Motor.HorizontalImpactSpeed)) * Mathf.Rad2Deg;
            if(Motor.ImpactSpeed >= settings.ground.hardLandingImpact)
            { landingDuration = settings.ground.hardLandingRecovery; SetState(RiderState.Landing); }
            else if(Motor.ImpactSpeed >= settings.ground.forcedRollImpact || Motor.HorizontalImpactSpeed >= settings.ground.forcedRollSpeed
                || (Motor.HorizontalImpactSpeed > settings.ground.runSpeed && angle > settings.ground.gentleLandingAngle)) BeginRoll();
            else { landingDuration = settings.ground.landingRecovery; SetState(RiderState.Landing); }
        }
        void BeginRoll()
        {
            rollDirection = Vector3.ProjectOnPlane(Motor.Velocity,Vector3.up).sqrMagnitude > 1 ? Vector3.ProjectOnPlane(Motor.Velocity,Vector3.up).normalized : transform.forward;
            Motor.SetCrouched(true); rollCooldown = settings.ground.rollDuration + settings.ground.rollCooldown; SetState(RiderState.Rolling);
        }
        public bool CanScoop => Airborne && !IgnoringMountCollisions && SinceDetach >= settings.mount.gracePeriod;
        public void BeginRemount()
        {
            mountCollisions.Restore();
            transitionStart = transform.position; transitionRotation = transform.rotation; transitionVelocity = Motor.Velocity;
            Motor.Capsule.enabled = false; mount.BeginCatch(Motor.Velocity); SetState(RiderState.Remounting);
        }
        void FollowSeat() { transform.SetPositionAndRotation(mount.Seat.position,mount.Seat.rotation); Motor.Velocity = mount.Velocity; }
        public void ResetRider()
        {
            mountCollisions.Restore(); LandingTarget.CancelPreview(); mount.CancelCall(); Motor.Capsule.enabled = false; Motor.Velocity = Vector3.zero; GlidePose = 0; AirTime = SinceDetach = 0;
            mount.AttachRider(); SetState(RiderState.Mounted); FollowSeat();
        }
        public void PlaceForTest(Vector3 position,Vector3 velocity,RiderState state)
        { mountCollisions.Restore(); mount.Dismount(); Motor.Capsule.enabled = true; Motor.Place(position,Quaternion.identity,velocity); AirTime = SinceDetach = 2; SetState(state); if(state == RiderState.Glide) Glide.Deploy(velocity); }
    }
}
