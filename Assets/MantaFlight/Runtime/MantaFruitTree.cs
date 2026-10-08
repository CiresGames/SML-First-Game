using UnityEngine;

namespace MantaFlight
{
    /// <summary>A large Rei tree landmark, with an anchor for its matching collectible.</summary>
    [DisallowMultipleComponent]
    public sealed class MantaFruitTree : MonoBehaviour
    {
        public MantaStat stat;
        public Transform fruitAnchor;
        public MantaFruit fruit;
        public bool HasHangingFruit => fruit != null && fruit.gameObject.activeInHierarchy && !fruit.Dropped
            && (fruit.companion == null || !fruit.companion.Data.collectedFruits.Contains(fruit.fruitId));
        public static string ResourcePath(MantaStat stat) => "MantaFruitTrees/" + stat;
        Collider[] wood;
        void Awake() => wood = GetComponentsInChildren<Collider>();
        public bool TryGetPunchPoint(Rider.RiderController rider, float reach, out Vector3 point)
        {
            point = default;
            if (!HasHangingFruit || rider == null) return false;
            Vector3 from = rider.transform.position + Vector3.up * (rider.Motor.Capsule.height * Mathf.Abs(rider.transform.lossyScale.y) * .7f);
            Vector3 direction = transform.position + Vector3.up * (from.y - transform.position.y) - from;
            if (direction.sqrMagnitude < .001f) return false;
            float nearest = reach;
            bool found = false;
            if (wood == null) wood = GetComponentsInChildren<Collider>();
            foreach (var collider in wood)
                if (collider.enabled && !collider.isTrigger && collider.Raycast(new Ray(from, direction.normalized), out var hit, nearest))
                { point = hit.point; nearest = hit.distance; found = true; }
            if (!found) return false;
            // Do not offer an interaction through another tree, wall or rock.
            Vector3 path = point - from;
            if (Physics.Raycast(from, path.normalized, out var obstruction, path.magnitude + .05f,
                rider.settings.environment, QueryTriggerInteraction.Ignore)
                && !obstruction.collider.transform.IsChildOf(transform)) return false;
            return true;
        }
        public bool KnockFruit(Vector3 riderPosition) => HasHangingFruit && fruit.Drop(riderPosition);
        void OnDrawGizmosSelected()
        {
            if (fruitAnchor == null) return;
            Gizmos.color = MantaStatStyle.Color(stat);
            Gizmos.DrawWireSphere(fruitAnchor.position, .65f);
        }
    }
}
