using System.Collections.Generic;
using UnityEngine;
namespace MantaFlight.Rider
{
    /// <summary>Owns only rider/manta collision pairs. World collisions stay active throughout a jump.</summary>
    public sealed class RiderMountCollisionGuard
    {
        struct Pair { public Collider other; public bool ignoredBefore; }
        readonly List<Pair> pairs = new List<Pair>();
        CharacterController capsule;
        float elapsed;
        public bool Active => pairs.Count > 0;

        public bool TryGetLaunch(RiderController rider, out Vector3 position, out Vector3 direction)
        {
            var s = rider.settings.mount;
            var c = rider.Motor.Capsule;
            position = rider.mount.Seat.position; direction = Vector3.zero;
            var colliders = rider.mount.GetComponentsInChildren<Collider>();
            // The seated pose may put the feet inside the body. Start above the actual surface,
            // not at an arbitrary socket offset, before enabling the standing capsule.
            foreach (var other in colliders)
            {
                if (!other.enabled || other.isTrigger) continue;
                var bounds = other.bounds;
                var origin = new Vector3(position.x, bounds.max.y + c.height, position.z);
                if (other.Raycast(new Ray(origin, Vector3.down), out var hit, bounds.size.y + c.height * 2))
                    position.y = Mathf.Max(position.y, hit.point.y + s.jumpStartClearance);
            }
            float radius = rider.settings.ground.radius;
            float height = rider.settings.ground.standingHeight;
            Vector3 bottom = position + Vector3.up * radius, top = position + Vector3.up * (height - radius);
            if (Physics.CheckCapsule(bottom, top, radius, rider.settings.environment, QueryTriggerInteraction.Ignore)) return false;
            Vector3 forward = Vector3.ProjectOnPlane(rider.mount.Seat.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = rider.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            // Prefer forward; try either side and behind if the take-off corridor is blocked.
            foreach (var outward in new[] { forward, right, -right, -forward })
            {
                Vector3 probe = (outward * s.jumpForwardSpeed + Vector3.up * s.jumpUpSpeed).normalized;
                if (!Physics.CapsuleCast(bottom, top, radius, probe, out _, s.jumpProbeDistance,
                    rider.settings.environment, QueryTriggerInteraction.Ignore))
                { direction = outward; return true; }
            }
            return false; // Keep the rider mounted if every launch direction is obstructed.
        }
        public void Begin(RiderController rider)
        {
            Restore(); capsule = rider.Motor.Capsule; elapsed = 0;
            foreach (var other in rider.mount.GetComponentsInChildren<Collider>())
            {
                if (!other.enabled || other.isTrigger || other == capsule) continue;
                pairs.Add(new Pair { other = other, ignoredBefore = Physics.GetIgnoreCollision(capsule, other) });
                Physics.IgnoreCollision(capsule, other, true);
            }
        }
        public void Tick(RiderController rider, float dt)
        {
            if (!Active) return;
            elapsed += dt;
            bool separated = true;
            Bounds riderBounds = capsule.bounds;
            riderBounds.Expand(rider.settings.mount.jumpSeparationClearance * 2);
            foreach (var pair in pairs)
            {
                if (pair.other == null || !pair.other.enabled) continue;
                // Reapply after a capsule enable/resize; Unity can reset ignored collision pairs.
                if (capsule.enabled) Physics.IgnoreCollision(capsule, pair.other, true);
                if (riderBounds.Intersects(pair.other.bounds)) separated = false;
            }
            // Never restore an overlapping pair just because a timer elapsed.
            if (elapsed >= rider.settings.mount.jumpCollisionIgnoreDuration && separated) Restore();
        }
        public void Restore()
        {
            foreach (var pair in pairs)
                if (capsule != null && pair.other != null) Physics.IgnoreCollision(capsule, pair.other, pair.ignoredBefore);
            pairs.Clear(); capsule = null;
        }
    }
}
