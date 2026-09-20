using UnityEngine;
namespace MantaFlight.Rider
{
    public enum RiderState { Mounted, Dismounting, Grounded, Falling, Deploying, Glide, JumpOff, Landing, Rolling, Remounting }
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(RiderGroundMotor),typeof(RiderGlideMotor),typeof(RiderInput))]
    public sealed class RiderController : MonoBehaviour
    {
        public RiderSettings settings;
        public MantaMountAdapter mount;
        public MantaInput mantaInput;
        public Camera view;
        public bool startMounted = true;
        public RiderState State { get; private set; }
        public RiderGroundMotor Motor { get; private set; }
        public RiderGlideMotor Glide { get; private set; }
        public RiderInput Input { get; private set; }
        public RiderSettings SourceSettings { get; private set; }
        public bool Airborne => State == RiderState.Falling || State == RiderState.Glide || State == RiderState.Deploying || State == RiderState.JumpOff;
        public bool Mounted => State == RiderState.Mounted;
        public bool Attached => Mounted || State == RiderState.Remounting;
        public Vector2 LookInput { get; private set; }
        public float AirTime { get; private set; }
        public float SinceDetach { get; private set; }
        public float StateTime { get; private set; }
        public float Height => Motor.HeightAboveGround();
        public float GlidePose { get; private set; }
        public float LandingPulse { get; private set; }
        public float RollProgress => State == RiderState.Rolling ? Mathf.Clamp01(StateTime / settings.ground.rollDuration) : 0;
        public bool GlideAvailable => State == RiderState.Falling && Height >= settings.glide.minimumDeployHeight
            && (AirTime >= settings.glide.minimumAirTime || -Motor.Velocity.y >= settings.glide.minimumDownSpeed);
        public bool Paused => mantaInput != null && mantaInput.MenuOpen;
        float rollCooldown, landingDuration;
        Vector3 transitionStart, transitionEnd, rollDirection;
        Quaternion transitionRotation;
        void Awake()
        {
            SourceSettings = settings; settings = Instantiate(settings);
            Motor = GetComponent<RiderGroundMotor>(); Glide = GetComponent<RiderGlideMotor>(); Input = GetComponent<RiderInput>();
            Motor.settings = Glide.settings = Input.settings = settings;
        }
        void Start()
        {
            if (mount != null) { mount.rider = this; mount.manta.Respawned += ResetRider; }
            if (startMounted) ResetRider(); else SetState(RiderState.Falling);
        }
        void OnDestroy()
        { if (mount != null) mount.manta.Respawned -= ResetRider; if (settings != null) Destroy(settings); }
        void Update() { if (!Paused) Tick(Input.Read(Time.deltaTime), Time.deltaTime); }
        public void Tick(RiderCommand command, float dt)
        {
            if (dt <= 0) return;
            LookInput = command.look;
            dt = Mathf.Min(dt,.05f); StateTime += dt; SinceDetach += dt;
            rollCooldown = Mathf.Max(0,rollCooldown-dt); LandingPulse = Mathf.MoveTowards(LandingPulse,0,dt / settings.camera.landingPulseDuration);
            if (command.call && State != RiderState.Dismounting && State != RiderState.Remounting && !Mounted) mount.ToggleCall();
            if (Airborne) AirTime += dt;
            Vector3 forward = view == null ? transform.forward : view.transform.forward;
            switch (State)
            {
                case RiderState.Mounted:
                    FollowSeat();
                    if (command.jumpOff) Detach(true);
                    else if (command.drop) Detach(false);
                    else if (command.context && mount.CanDismount(out var ground))
                    {
                        transitionStart = transform.position; transitionEnd = ground;
                        mount.Dismount(); Motor.Capsule.enabled = false; SetState(RiderState.Dismounting);
                    }
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
                case RiderState.JumpOff:
                case RiderState.Deploying:
                case RiderState.Glide:
                    if (command.toggleGlide || (settings.secondJumpDeploys && command.jump)) { SetState(RiderState.Falling); Motor.FallMove(command,forward,dt); ResolveLanding(); break; }
                    float deployment = State == RiderState.Glide || State == RiderState.JumpOff ? 1 : Mathf.Clamp01(StateTime/settings.glide.deployDuration);
                    Motor.Velocity = Glide.Integrate(Motor.Velocity,command.glide,deployment,dt); Motor.Move(dt);
                    transform.rotation = Quaternion.Euler(0,Glide.Yaw,0);
                    if (StateTime >= settings.glide.deployDuration && State != RiderState.Glide) SetState(RiderState.Glide);
                    ResolveLanding();
                    break;
                case RiderState.Remounting:
                    float t = Mathf.SmoothStep(0,1,StateTime/settings.mount.blendDuration);
                    transform.position = Vector3.Lerp(transitionStart, mount.Seat.position,t);
                    transform.rotation = Quaternion.Slerp(transitionRotation,mount.Seat.rotation,t);
                    if(StateTime >= settings.mount.blendDuration) { mount.AttachRider(); SetState(RiderState.Mounted); FollowSeat(); }
                    break;
            }
            float poseTarget = State == RiderState.Glide || State == RiderState.Deploying || State == RiderState.JumpOff ? 1 : 0;
            GlidePose = Mathf.MoveTowards(GlidePose,poseTarget,dt / Mathf.Max(.01f,poseTarget > 0 ? settings.glide.deployDuration : settings.glide.retractDuration));
            if(transform.position.y < settings.resetBelowHeight || transform.position.sqrMagnitude > 16000000) mount.manta.ResetFlight();
        }
        void SetState(RiderState next) { State = next; StateTime = 0; }
        void StartGlide() { Motor.SetCrouched(false); Glide.Deploy(Motor.Velocity); SetState(RiderState.Deploying); }
        public void Detach(bool wingsuit)
        {
            Vector3 velocity = mount.Velocity * settings.mount.velocityInheritance;
            if(wingsuit) velocity += Vector3.up * settings.mount.jumpUpSpeed + mount.Seat.forward * settings.mount.jumpForwardSpeed;
            Vector3 position = mount.Seat.position + Vector3.up * .2f;
            mount.Dismount(); transform.SetParent(null,true); Motor.Capsule.enabled = true;
            Motor.Place(position,Quaternion.Euler(0,mount.Seat.eulerAngles.y,0),velocity);
            SinceDetach = AirTime = 0;
            if(wingsuit) { Glide.Deploy(velocity); GlidePose = 1; SetState(RiderState.JumpOff); } else SetState(RiderState.Falling);
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
        public bool CanScoop => Airborne && SinceDetach >= settings.mount.gracePeriod;
        public void BeginRemount()
        {
            transitionStart = transform.position; transitionRotation = transform.rotation;
            Motor.Capsule.enabled = false; mount.BeginCatch(Motor.Velocity); SetState(RiderState.Remounting);
        }
        void FollowSeat() { transform.SetPositionAndRotation(mount.Seat.position,mount.Seat.rotation); Motor.Velocity = mount.Velocity; }
        public void ResetRider()
        {
            mount.CancelCall(); Motor.Capsule.enabled = false; Motor.Velocity = Vector3.zero; GlidePose = 0; AirTime = SinceDetach = 0;
            mount.AttachRider(); SetState(RiderState.Mounted); FollowSeat();
        }
        public void PlaceForTest(Vector3 position,Vector3 velocity,RiderState state)
        { mount.Dismount(); Motor.Capsule.enabled = true; Motor.Place(position,Quaternion.identity,velocity); AirTime = SinceDetach = 2; SetState(state); if(state == RiderState.Glide) Glide.Deploy(velocity); }
    }
}
