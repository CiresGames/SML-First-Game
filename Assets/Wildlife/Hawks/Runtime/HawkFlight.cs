using UnityEngine;

namespace MantaFlight.Hawks
{
    /// <summary>Ambient hawk: an elliptical thermal, smooth banking and alternating flaps/glides.</summary>
    public sealed class HawkFlight : MonoBehaviour
    {
        public Vector3 center = new Vector3(0, 65, -135);
        public Vector2 radius = new Vector2(38, 55);
        [Min(.1f)] public float speed = 9;
        public float phase;
        public float altitudeVariation = 3;
        public bool clockwise;
        [Range(.5f, 1.5f)] public float wingbeatSpeed = 1;
        Animator animator;
        float elapsed;
        bool gliding;

        void Start()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator)
            {
                animator.speed = wingbeatSpeed;
                animator.Play("Flight", 0, Mathf.Repeat(phase / (2 * Mathf.PI), 1));
            }
            ApplyPose(0);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            ApplyPose(elapsed);
            bool nextGlide = Mathf.Repeat(elapsed + phase * 3, 11) > 4.5f;
            if (animator && nextGlide != gliding)
            {
                animator.CrossFadeInFixedTime(nextGlide ? "Glide" : "Flight", .45f);
                gliding = nextGlide;
            }
        }

        public void ApplyPose(float time)
        {
            float direction = clockwise ? -1 : 1;
            float angularSpeed = direction * speed / Mathf.Max(1, (radius.x + radius.y) * .5f);
            float a = phase + time * angularSpeed;
            Vector3 offset = new Vector3(Mathf.Cos(a) * radius.x, Mathf.Sin(a * 2) * altitudeVariation, Mathf.Sin(a) * radius.y);
            Vector3 velocity = new Vector3(-Mathf.Sin(a) * radius.x, 2 * Mathf.Cos(a * 2) * altitudeVariation, Mathf.Cos(a) * radius.y) * angularSpeed;
            // A right turn needs a right bank (negative roll in Unity's +Z-forward convention).
            float bank = direction * Mathf.Clamp(speed * speed / (Mathf.Max(1, radius.magnitude) * 9.81f) * Mathf.Rad2Deg, 4, 22);
            transform.SetPositionAndRotation(center + offset, Quaternion.LookRotation(velocity, Vector3.up) * Quaternion.Euler(0, 0, bank));
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.9f, .65f, .2f, .6f);
            Vector3 last = center + new Vector3(radius.x, 0, 0);
            for (int i = 1; i <= 64; i++)
            {
                float a = i * Mathf.PI * 2 / 64;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius.x, Mathf.Sin(a * 2) * altitudeVariation, Mathf.Sin(a) * radius.y);
                Gizmos.DrawLine(last, next); last = next;
            }
        }
    }
}
