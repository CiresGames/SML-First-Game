using UnityEngine;
using UnityEngine.Rendering;

namespace MantaFlight
{
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class WindZoneParticles : MonoBehaviour
    {
        [Tooltip("Zone propriétaire. Seule sa portion autour de la cible émet.")] public WindZone3D zone;
        [Tooltip("Caméra utilisée pour le LOD et le frustum ; Camera.main par défaut.")] public Camera viewCamera;
        [Tooltip("Centre d'émission ; caméra par défaut, peut référencer le joueur.")] public Transform follow;
        [Tooltip("Matériau de traits transparents compatible avec le pipeline.")] public Material material;
        [Tooltip("Rayon maximal autour de la cible, en mètres.")][Min(1)] public float radius = 32;
        [Tooltip("Budget maximal de particules pour cette zone.")][Range(16, 4096)] public int maxParticles = 400;
        [Tooltip("Durée de vie des traits en secondes.")][Min(.1f)] public float lifetime = 2.5f;
        [Tooltip("Émission par seconde à faible/fort vent.")] public Vector2 emissionRange = new Vector2(3, 100);
        [Tooltip("Taille des traits à faible/fort vent, en mètres.")] public Vector2 sizeRange = new Vector2(.025f, .1f);
        [Tooltip("Vent atteignant la densité et la longueur maximales, en m/s.")][Min(.1f)] public float fullStrength = 25;
        [Tooltip("Couleur des traits. L'alpha est aussi modulé par la force.")] public Color tint = new Color(.85f, .96f, 1, .55f);
        [Tooltip("Distance caméra/cible où l'émission commence à baisser.")][Min(0)] public float lodStart = 60;
        [Tooltip("Distance caméra/cible où le système s'arrête.")][Min(1)] public float lodEnd = 180;
        [Tooltip("Suspendre et vider les particules lorsque le volume local sort du frustum.")] public bool frustumCulling = true;
        [Tooltip("Étirement lié à la vitesse, faible/fort vent.")] public Vector2 velocityStretch = new Vector2(.015f, .09f);
        [Tooltip("Longueur relative des traits, faible/fort vent.")] public Vector2 lengthRange = new Vector2(1, 5);
        ParticleSystem system;
        ParticleSystemRenderer particleRenderer;
        ParticleSystem.Particle[] particles;
        readonly Plane[] planes = new Plane[6];
        float emissionDebt, nextCameraSearch;

        void Awake()
        {
            system = GetComponent<ParticleSystem>(); particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (!zone) zone = GetComponentInParent<WindZone3D>();
            Configure();
        }
        public void Configure()
        {
            if (!system) system = GetComponent<ParticleSystem>();
            if (!particleRenderer) particleRenderer = GetComponent<ParticleSystemRenderer>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0; main.gravityModifier = 0; main.maxParticles = Mathf.Clamp(maxParticles, 16, 4096);
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var velocity = system.velocityOverLifetime; velocity.enabled = false;
            var force = system.forceOverLifetime; force.enabled = false;
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off; particleRenderer.receiveShadows = false;
            particleRenderer.cameraVelocityScale = 0;
            if (material) particleRenderer.sharedMaterial = material;
            particles = new ParticleSystem.Particle[main.maxParticles];
        }
        void OnDisable() { if (system) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); emissionDebt = 0; }
        void LateUpdate()
        {
            if (!viewCamera && Time.unscaledTime >= nextCameraSearch)
            { viewCamera = Camera.main; nextCameraSearch = Time.unscaledTime + 1; }
            if (!viewCamera || !zone || !zone.Usable) { Suspend(); return; }
            Vector3 center = follow ? follow.position : viewCamera.transform.position;
            float r = Mathf.Max(1, radius);
            float distance = Vector3.Distance(center, viewCamera.transform.position);
            float lod = 1 - Mathf.InverseLerp(lodStart, Mathf.Max(lodStart + 1, lodEnd), distance);
            GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);
            var bounds = new Bounds(center, Vector3.one * r * 2);
            if (lod <= 0 || zone.InfluenceAt(center) <= 0 ||
                (frustumCulling && !GeometryUtility.TestPlanesAABB(planes, bounds))) { Suspend(); return; }
            if (particles == null || particles.Length != Mathf.Clamp(maxParticles, 16, 4096)) Configure();
            transform.position = center;
            if (!system.isPlaying) system.Play();
            Vector3 wind = WindManager.GetWindAt(center);
            float intensity = Mathf.Clamp01(wind.magnitude / Mathf.Max(.1f, fullStrength));
            particleRenderer.velocityScale = Mathf.Lerp(velocityStretch.x, velocityStretch.y, intensity);
            particleRenderer.lengthScale = Mathf.Lerp(lengthRange.x, lengthRange.y, intensity);
            float strengthFade = Mathf.Clamp01(wind.magnitude);
            emissionDebt += Mathf.Max(0, Mathf.Lerp(emissionRange.x, emissionRange.y, intensity)) * lod
                * strengthFade * zone.InfluenceAt(center) * Time.deltaTime;
            int requested = Mathf.Min(Mathf.FloorToInt(emissionDebt), particles.Length - system.particleCount);
            emissionDebt = Mathf.Min(emissionDebt - requested, 1);
            for (int i = 0; i < requested; i++)
            {
                Vector3 position = center + Random.insideUnitSphere * r;
                if (zone.InfluenceAt(position) <= 0) continue;
                system.Emit(new ParticleSystem.EmitParams {
                    position = position, velocity = WindManager.GetWindAt(position),
                    startLifetime = Mathf.Max(.1f, lifetime), startSize = Mathf.Lerp(sizeRange.x, sizeRange.y, intensity),
                    startColor = tint
                }, 1);
            }
            // Lire le champ à chaque particule permet aussi aux traits déjà émis de traverser
            // un chevauchement. Aucun tableau ni recherche de scène n'est alloué par image.
            int count = system.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                if ((particles[i].position - center).sqrMagnitude > r * r) { particles[i].remainingLifetime = 0; continue; }
                Vector3 localWind = WindManager.GetWindAt(particles[i].position);
                particles[i].velocity = localWind;
                float age = 1 - particles[i].remainingLifetime / particles[i].startLifetime;
                float fade = Mathf.Clamp01(age * 8) * Mathf.Clamp01((1 - age) * 4);
                Color color = tint;
                float force01 = Mathf.Clamp01(localWind.magnitude / Mathf.Max(.1f, fullStrength));
                color.a *= Mathf.Lerp(.12f, 1, force01) * Mathf.Clamp01(localWind.magnitude) * fade;
                particles[i].startColor = color;
                particles[i].startSize = Mathf.Lerp(sizeRange.x, sizeRange.y, force01);
            }
            system.SetParticles(particles, count);
        }
        void Suspend()
        {
            if (system && (system.isPlaying || system.particleCount > 0)) system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            emissionDebt = 0;
        }
    }
}
