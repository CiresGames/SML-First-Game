using UnityEngine;
using MantaFlight.Rider;

namespace MantaFlight.Sharks
{
    /// <summary>Free-flight predator. All distances are world metres; visual forward is +Z.</summary>
    public sealed class FlyingShark : MonoBehaviour
    {
        public enum Behaviour { Patrol, Chase, Bite, Recover, Return }
        public SharkPlayerHealth target;
        public Vector3 patrolCenter;
        [Min(5)] public float patrolRadius = 28;
        public float phase;
        [Min(1)] public float patrolSpeed = 10, chaseSpeed = 28;
        [Min(1)] public float detectionRange = 32, loseRange = 52, leashRange = 90;
        [Min(.1f)] public float biteRange = 3.8f, biteDamage = 25, biteCooldown = 2.5f;
        [Min(1)] public float turnSpeed = 100;
        public LayerMask obstructionMask = ~0;
        public Behaviour State { get; private set; }
        Animator animator;
        RiderController rider;
        float clock, stateTime, cooldown;
        bool struck;
        Vector3 retreat;
        readonly RaycastHit[] hits = new RaycastHit[24];
        void Start()
        {
            animator = GetComponentInChildren<Animator>();
            if (!target) target = FindFirstObjectByType<SharkPlayerHealth>();
            if (target) rider = target.GetComponent<RiderController>();
            if (animator) animator.Play("Swim",0,Mathf.Repeat(phase,1));
        }
        void Update() { if (!rider || !rider.Paused) Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if (dt <= 0) return;
            dt = Mathf.Min(dt,.05f); clock += dt; stateTime += dt; cooldown -= dt;
            bool valid = target && target.CanBeAttacked;
            Vector3 prey = target ? target.transform.position + Vector3.up * .6f : patrolCenter;
            float distance = Vector3.Distance(transform.position,prey);
            if (State == Behaviour.Patrol && valid && distance <= detectionRange && ClearPath(prey)) Change(Behaviour.Chase);
            if (State == Behaviour.Chase && (!valid || distance > loseRange || Vector3.Distance(transform.position,patrolCenter)>leashRange)) Change(Behaviour.Return);
            Vector3 destination;
            float speed = patrolSpeed;
            switch (State)
            {
                case Behaviour.Chase:
                    destination = prey; speed = chaseSpeed;
                    if (distance <= biteRange && cooldown <= 0 && ClearPath(prey)) { struck = false; Change(Behaviour.Bite); }
                    break;
                case Behaviour.Bite:
                    destination = prey; speed = chaseSpeed * .35f;
                    if (!struck && stateTime >= .32f)
                    {
                        struck = true;
                        if (valid && distance <= biteRange && ClearPath(prey)) target.TakeDamage(biteDamage);
                    }
                    if (stateTime >= .65f)
                    {
                        cooldown = biteCooldown;
                        retreat = transform.position + transform.forward * 18 + Vector3.up * 6;
                        Change(Behaviour.Recover);
                    }
                    break;
                case Behaviour.Recover:
                    destination = retreat; speed = chaseSpeed * .7f;
                    if (stateTime > 1.3f) Change(Behaviour.Return);
                    break;
                case Behaviour.Return:
                    destination = PatrolPoint(clock);
                    if (Vector3.Distance(transform.position,destination)<8) Change(Behaviour.Patrol);
                    break;
                default: destination = PatrolPoint(clock); break;
            }
            Vector3 direction = destination - transform.position;
            if (direction.sqrMagnitude < .01f) return;
            // Steer away from scenery. Ignore this shark and the player for obstacle probes.
            if (Blocked(transform.position, transform.forward, 8, out var obstacle)) direction = (obstacle.normal + Vector3.up * .8f) * 12;
            var rotation = Quaternion.LookRotation(direction.normalized,Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,rotation,turnSpeed * dt);
            float travel = Mathf.Min(speed*dt,Vector3.Distance(transform.position,destination));
            if (!Blocked(transform.position,transform.forward,travel + .9f,out _)) transform.position += transform.forward * travel;
            if (animator) animator.speed = State == Behaviour.Chase ? 1.65f : 1;
        }
        bool Blocked(Vector3 origin, Vector3 direction, float length, out RaycastHit obstacle)
        {
            int count = Physics.SphereCastNonAlloc(origin,.6f,direction,hits,length,obstructionMask,QueryTriggerInteraction.Ignore);
            obstacle = default; float nearest = float.MaxValue;
            for (int i=0;i<count;i++)
            {
                var t=hits[i].transform;
                if (t.IsChildOf(transform) || (target && (t.IsChildOf(target.transform) || (rider && rider.mount && t.IsChildOf(rider.mount.transform))))) continue;
                if (hits[i].distance < nearest) { nearest=hits[i].distance; obstacle=hits[i]; }
            }
            return nearest<float.MaxValue;
        }
        bool ClearPath(Vector3 point) => !Blocked(transform.position,(point-transform.position).normalized,Vector3.Distance(transform.position,point),out _);
        void Change(Behaviour next)
        {
            State=next; stateTime=0;
            if (animator) animator.CrossFadeInFixedTime(next==Behaviour.Bite ? "Bite" : "Swim",.15f);
        }
        public Vector3 PatrolPoint(float time)
        {
            float a=phase+time*patrolSpeed/Mathf.Max(5,patrolRadius);
            return patrolCenter + new Vector3(Mathf.Cos(a)*patrolRadius,Mathf.Sin(a*2)*4,Mathf.Sin(a)*patrolRadius);
        }
        public void PlaceOnPatrol()
        {
            transform.position=PatrolPoint(0);
            transform.rotation=Quaternion.LookRotation(PatrolPoint(.1f)-transform.position);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan; Gizmos.DrawWireSphere(patrolCenter,patrolRadius);
            Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(transform.position,detectionRange);
            Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position,biteRange);
        }
    }
}
