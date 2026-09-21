using UnityEngine;
namespace MantaFlight.Rider
{
    /// <summary>Hold Call on foot to aim; release a valid target to commit. Airborne calls remain a toggle.</summary>
    public sealed class RiderLandingTarget : MonoBehaviour
    {
        public RiderController rider;
        public bool Aiming { get; private set; }
        public bool Valid { get; private set; }
        public Vector3 Point { get; private set; }
        public Vector3 Normal { get; private set; } = Vector3.up;
        public string Feedback { get; private set; } = "Hold H / D-pad up to aim a landing";
        GameObject indicator;
        Material material;

        public void Tick(bool held, bool released)
        {
            if (held)
            {
                Aiming = true;
                var s = rider.settings.mount;
                Ray ray = rider.view != null ? rider.view.ViewportPointToRay(new Vector3(.5f, .5f, 0))
                    : new Ray(transform.position + Vector3.up, transform.forward + Vector3.down);
                bool hitSurface = Physics.Raycast(ray, out var hit, s.landingAimRange, rider.settings.environment, QueryTriggerInteraction.Ignore);
                if (!hitSurface)
                {
                    // Si la caméra regarde l'horizon, viser le sol devant le rider reste facile.
                    Vector3 forward = Vector3.ProjectOnPlane(ray.direction, Vector3.up).normalized;
                    Vector3 ahead = transform.position + forward * s.landingOffset;
                    hitSurface = Physics.Raycast(ahead + Vector3.up * s.landingProbeHeight, Vector3.down, out hit,
                        s.landingProbeHeight * 2, rider.settings.environment, QueryTriggerInteraction.Ignore);
                    if (!hitSurface) Point = ahead;
                }
                if (hitSurface) { Point = hit.point; Normal = hit.normal; }
                Valid = hitSurface && rider.mount.IsLandingSurfaceValid(Point, Normal);
                Feedback = !hitSurface ? "No ground" : Valid ? "Landing valid - release to confirm" : "Landing blocked / slope too steep";
                ShowIndicator();
                // Preview only. Do not mutate the service state or destination while held.
            }
            if (released && Aiming)
            {
                // La destination du service reste fixe après validation, même si le joueur se déplace.
                Valid = Valid && rider.mount.IsLandingSurfaceValid(Point, Normal);
                if (Valid) Valid = rider.mount.RequestLanding(Point, Normal);
                Feedback = Valid ? "Landing confirmed" : "Landing cancelled";
                Aiming = false; HideIndicator();
            }
            else if (!held && !released && Aiming) CancelPreview(); // Lost input focus/device.
        }
        public void CancelPreview()
        {
            if (!Aiming) return;
            Aiming = Valid = false; HideIndicator();
            Feedback = "Hold H / D-pad up to aim a landing";
        }
        void ShowIndicator()
        {
            if (indicator == null)
            {
                indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                indicator.name = "Manta landing target"; indicator.layer = 2;
                // Disabled immediately: the indicator must never intercept its own ray or the manta.
                var collider = indicator.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                material = new Material(Shader.Find("Manta/Rider Landing Indicator"));
                var renderer = indicator.GetComponent<Renderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            indicator.SetActive(true);
            indicator.transform.position = Point + Normal * rider.settings.mount.landingIndicatorRadius * .25f;
            indicator.transform.localScale = Vector3.one * rider.settings.mount.landingIndicatorRadius * 2;
            material.SetColor("_Color", Valid ? new Color(.1f, .9f, .75f, .32f) : new Color(1, .15f, .12f, .38f));
        }
        void HideIndicator() { if (indicator != null) indicator.SetActive(false); }
        void OnDisable() { if (rider != null && rider.mount != null) CancelPreview(); HideIndicator(); }
        void OnDestroy() { if (indicator != null) Destroy(indicator); if (material != null) Destroy(material); }
    }
}
