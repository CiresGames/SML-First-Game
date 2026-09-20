using UnityEngine;
namespace MantaFlight.Rider
{
    public enum MantaServiceState { Piloted, Hover, Idle, Intercept, Retry, Landing, Catch }
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
        Vector3 velocity, retryTarget;
        float callTime, passTime;
        bool landed;
        MantaInput input;
        MountFeel S => rider.settings.mount;
        void Awake() { input = manta.GetComponent<MantaInput>(); }
        void FixedUpdate() { if(rider != null && !rider.Paused) Tick(Time.fixedDeltaTime); }
        public void Tick(float dt)
        {
            if (Mode == MantaServiceState.Piloted)
            {
                if (rider.Mounted && input.State.brake >= S.hoverBrakeThreshold && manta.Speed <= S.hoverEntrySpeed
                    && GroundDistance() <= S.dismountHeight)
                { velocity = manta.Velocity; Mode = MantaServiceState.Hover; manta.Paused = true; }
                return;
            }
            manta.Paused = true;
            if (Mode == MantaServiceState.Hover || Mode == MantaServiceState.Idle)
            {
                velocity = Vector3.MoveTowards(velocity,Vector3.zero,S.hoverDeceleration * dt);
                Move(velocity,dt);
                if(Mode == MantaServiceState.Hover && rider.Mounted && input.State.throttle > .1f) AttachRider();
                return;
            }
            if(Mode == MantaServiceState.Catch) { Move(velocity,dt); return; }
            callTime += dt; passTime += dt;
            if(callTime > S.callTimeout || Vector3.Distance(transform.position,rider.transform.position) > S.maximumCallDistance)
            { CancelCall(); return; }
            if(!rider.Airborne && Mode != MantaServiceState.Landing) { Mode = MantaServiceState.Landing; passTime = 0; }
            Vector3 desired;
            if(Mode == MantaServiceState.Landing)
            {
                Vector3 beside = rider.transform.position + rider.transform.right * S.landingOffset;
                if(!Physics.Raycast(beside + Vector3.up * 40,Vector3.down,out var ground,150,rider.settings.environment,QueryTriggerInteraction.Ignore)) { CancelCall(); return; }
                InterceptTarget = ground.point + Vector3.up * S.hoverHeight;
                desired = Vector3.ClampMagnitude((InterceptTarget-transform.position) * 2,S.callSpeed);
                if(Vector3.Distance(transform.position,InterceptTarget) < .7f && velocity.magnitude < 2)
                { landed = true; Mode = MantaServiceState.Idle; velocity = Vector3.zero; return; }
            }
            else if(Mode == MantaServiceState.Retry)
            {
                InterceptTarget = retryTarget;
                desired = (retryTarget-transform.position).normalized * S.callSpeed;
                if(passTime >= S.retryDuration) { Mode = MantaServiceState.Intercept; passTime = 0; }
            }
            else
            {
                Vector3 rv = rider.Motor.Velocity;
                Vector3 forward = rv.sqrMagnitude > 4 ? rv.normalized : rider.transform.forward;
                float distance = Vector3.Distance(seat.position,rider.transform.position);
                float approach = Mathf.InverseLerp(S.scoopRadius,S.matchDistance,distance);
                Vector3 predicted = rider.transform.position + rv * (S.predictionTime * approach);
                InterceptTarget = predicted - forward * (S.approachBehind * approach) - Vector3.up * (S.approachBelow * approach);
                desired = Vector3.ClampMagnitude(rv + (InterceptTarget-seat.position) * 2,S.callSpeed);
                if(rider.CanScoop && distance <= S.scoopRadius && (Velocity-rv).magnitude <= S.maximumRelativeSpeed)
                { rider.BeginRemount(); return; }
                if(passTime > S.passTimeout)
                { PassCount++; Mode = MantaServiceState.Retry; passTime = 0; retryTarget = rider.transform.position - forward * S.approachBehind * 2 - Vector3.up * S.approachBelow; }
            }
            float speed = Mathf.MoveTowards(velocity.magnitude,desired.magnitude,S.callAcceleration * dt);
            Vector3 direction = velocity.sqrMagnitude > 1 ? velocity.normalized : transform.forward;
            direction = Vector3.RotateTowards(direction,desired.sqrMagnitude > .01f ? desired.normalized : direction,S.callTurnRate * Mathf.Deg2Rad * dt,0);
            velocity = direction * speed; Move(velocity,dt);
        }
        void Move(Vector3 motion,float dt)
        {
            Vector3 position = manta.GetComponent<Rigidbody>().position;
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
            if(velocity.sqrMagnitude>1)
            {
                Vector3 forward=velocity.normalized;
                Vector3 up=Mathf.Abs(forward.y)>.96f ? heading*Vector3.up : Vector3.up;
                heading=Quaternion.RotateTowards(heading,Quaternion.LookRotation(forward,up),S.callTurnRate*dt);
            }
            manta.SetExternalMotion(position,heading,velocity);
        }
        float GroundDistance() => Physics.Raycast(transform.position,Vector3.down,out var hit,100,rider.settings.environment,QueryTriggerInteraction.Ignore) ? hit.distance : 100;
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
            landed=GroundDistance()<=S.dismountHeight && Velocity.magnitude<=S.dismountSpeed;
            velocity=Vector3.zero;Mode=MantaServiceState.Idle;manta.Paused=true;input.ControlEnabled=false;
            manta.GetComponent<MantaManeuvers>().Cancel();
        }
        public void ToggleCall() { if(Calling) CancelCall(); else CallToPosition(rider.transform.position,rider.Motor.Velocity,rider.Airborne); }
        public void CallToPosition(Vector3 position,Vector3 riderVelocity,bool airborne)
        {
            if(Vector3.Distance(transform.position,position)>S.maximumCallDistance) return;
            callTime=passTime=0;PassCount=1;landed=false;
            InterceptTarget=position;Mode=airborne?MantaServiceState.Intercept:MantaServiceState.Landing;manta.Paused=true;
        }
        public void CancelCall() { if(Calling) {Mode=MantaServiceState.Idle;velocity=Vector3.zero;} }
        public void BeginCatch(Vector3 riderVelocity) { Mode=MantaServiceState.Catch;velocity=Vector3.Lerp(velocity,riderVelocity,.8f); }
        public void AttachRider()
        {
            Mode=MantaServiceState.Piloted;landed=false;input.ControlEnabled=true;manta.Paused=false;
            manta.SetExternalMotion(manta.GetComponent<Rigidbody>().position,manta.Heading,manta.Heading*Vector3.forward*Mathf.Max(manta.settings.minimumSpeed,Velocity.magnitude));
        }
        void OnDrawGizmosSelected()
        {
            if(rider==null || seat==null || rider.settings==null)return;
            Gizmos.color=Color.cyan;Gizmos.DrawWireSphere(seat.position,S.scoopRadius);
            Gizmos.color=Color.yellow;Gizmos.DrawLine(transform.position,InterceptTarget);Gizmos.DrawWireSphere(InterceptTarget,.6f);
            Gizmos.color=Color.green;Gizmos.DrawLine(transform.position,transform.position+Vector3.down*S.dismountHeight);
        }
    }
}
