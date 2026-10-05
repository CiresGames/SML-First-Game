using UnityEngine;

namespace MantaFlight
{
    [DisallowMultipleComponent]
    public sealed class WindZone3D : MonoBehaviour
    {
        [Tooltip("Volume trigger : Box, Sphere, Capsule ou MeshCollider convexe fermé.")]
        public Collider volume;
        [Tooltip("Direction dans le monde, normalisée lors de la lecture. Y positif crée une ascendance.")]
        public Vector3 direction = new Vector3(1, .2f, 0);
        [Tooltip("Vitesse de l'air en mètres par seconde.")][Min(0)] public float strength = 12;
        [Tooltip("Épaisseur intérieure de la transition, en mètres.")][Min(.01f)] public float falloff = 60;
        [Tooltip("Poids relatif dans les chevauchements.")][Min(.01f)] public float weight = 1;
        [Tooltip("Amplitude relative des rafales Perlin.")][Range(0, 1)] public float gusts = .2f;
        [Tooltip("Période des rafales en secondes.")][Min(.1f)] public float gustPeriod = 12;
        [Tooltip("Variation angulaire maximale des rafales, en degrés.")][Range(0, 60)] public float directionVariation = 8;
        [Tooltip("Graine indépendante pour cette zone.")] public float noiseSeed = 13;
        [Tooltip("Appliquer le multiplicateur et la dérive fournis par la météo.")] public bool weatherInfluence = true;
        [Tooltip("Ignorer rafales et météo pour tester un vent constant.")] public bool debugOverride;
        [Tooltip("Vent de test en mètres par seconde, direction et force combinées.")] public Vector3 debugWind = new Vector3(0, 15, 0);
        [Tooltip("Couleur du volume dans la vue Scene.")] public Color gizmoColor = new Color(.25f, .85f, 1, .18f);

        public bool Usable => isActiveAndEnabled && volume && volume.enabled && volume.isTrigger
            && (!(volume is MeshCollider mesh) || mesh.convex);
        public Bounds WorldBounds => volume ? volume.bounds : new Bounds(transform.position, Vector3.zero);
        void Reset() { volume = GetComponent<Collider>(); if (volume) volume.isTrigger = true; }
        void OnEnable() { if (!volume) volume = GetComponent<Collider>(); WindManager.Register(this); }
        void OnDisable() => WindManager.Unregister(this);
        void OnValidate() { if (!volume) volume = GetComponent<Collider>(); WindManager.Invalidate(); }

        public float InfluenceAt(Vector3 position)
        {
            if (!Usable || (volume.ClosestPoint(position) - position).sqrMagnitude > .000001f) return 0;
            float depth;
            if (volume is BoxCollider box)
            {
                Vector3 p = box.transform.InverseTransformPoint(position) - box.center;
                Vector3 d = box.size * .5f - new Vector3(Mathf.Abs(p.x), Mathf.Abs(p.y), Mathf.Abs(p.z));
                Vector3 scale = box.transform.lossyScale;
                depth = Mathf.Min(d.x * Mathf.Abs(scale.x), d.y * Mathf.Abs(scale.y), d.z * Mathf.Abs(scale.z));
            }
            else if (volume is SphereCollider sphere)
            {
                Vector3 s = sphere.transform.lossyScale;
                depth = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z))
                    - Vector3.Distance(position, sphere.transform.TransformPoint(sphere.center));
            }
            else
            {
                // Approximation de la distance à la surface pour un volume convexe :
                // rayons entrants depuis l'extérieur (les rayons internes sont ignorés par PhysX).
                depth = float.PositiveInfinity;
                float reach = volume.bounds.size.magnitude + 1;
                for (int axis = 0; axis < 6; axis++)
                {
                    Vector3 d = axis < 2 ? Vector3.right : axis < 4 ? Vector3.up : Vector3.forward;
                    if ((axis & 1) != 0) d = -d;
                    if (volume.Raycast(new Ray(position + d * reach, -d), out var hit, reach + .01f))
                        depth = Mathf.Min(depth, Mathf.Max(0, reach - hit.distance));
                }
                if (float.IsInfinity(depth)) return 0;
            }
            return Mathf.SmoothStep(0, 1, Mathf.Clamp01(depth / Mathf.Max(.01f, falloff)));
        }

        public Vector3 Sample(float time, float weatherMultiplier, float weatherYaw)
        {
            if (debugOverride) return debugWind;
            float t = time / Mathf.Max(.1f, gustPeriod);
            float noise = Mathf.PerlinNoise(noiseSeed, t) * 2 - 1;
            float yaw = (Mathf.PerlinNoise(noiseSeed + 31, t * .7f) * 2 - 1) * directionVariation;
            float pitch = (Mathf.PerlinNoise(noiseSeed + 67, t * .6f) * 2 - 1) * directionVariation * .3f;
            var rotation = Quaternion.Euler(pitch, yaw + (weatherInfluence ? weatherYaw : 0), 0);
            return rotation * direction.normalized * Mathf.Max(0, strength) * Mathf.Max(0, 1 + noise * gusts)
                * (weatherInfluence ? weatherMultiplier : 1);
        }

        void OnDrawGizmosSelected()
        {
            if (!volume) volume = GetComponent<Collider>();
            if (!volume) return;
            Gizmos.color = gizmoColor;
            if (volume is BoxCollider b)
            {
                Gizmos.matrix = b.transform.localToWorldMatrix;
                Gizmos.DrawCube(b.center, b.size); Gizmos.DrawWireCube(b.center, b.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (volume is SphereCollider s)
            {
                Vector3 scale = s.transform.lossyScale;
                Gizmos.DrawWireSphere(s.transform.TransformPoint(s.center), s.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
            else Gizmos.DrawWireCube(volume.bounds.center, volume.bounds.size);
            Vector3 start = volume.bounds.center;
            Vector3 wind = Sample(Application.isPlaying ? Time.time : 0, 1, 0);
            Vector3 end = start + wind * 4;
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1);
            Gizmos.DrawLine(start, end);
            if (wind.sqrMagnitude < .001f) return;
            Vector3 side = Vector3.Cross(wind.normalized, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            Gizmos.DrawLine(end, end - wind * .7f + side.normalized * wind.magnitude * .3f);
            Gizmos.DrawLine(end, end - wind * .7f - side.normalized * wind.magnitude * .3f);
        }
    }
}
