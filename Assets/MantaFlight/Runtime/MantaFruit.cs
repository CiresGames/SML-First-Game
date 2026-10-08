using UnityEngine;

namespace MantaFlight
{
    /// <summary>Unique persistent exploration reward. IDs must stay stable after shipping.</summary>
    public sealed class MantaFruit : MonoBehaviour
    {
        public string fruitId;
        public MantaStat stat;
        public MantaProgression companion;
        public float collectRadius = 3;
        public Transform fruitVisual;
        public bool Dropped { get; private set; }
        public bool Landed { get; private set; }
        public Rigidbody Body { get; private set; }
        Material ownedMaterial;
        PhysicsMaterial ownedPhysics;
        bool frozen;
        Vector3 heldVelocity, heldAngularVelocity;
        float poll;
        void Update()
        {
            if (companion == null) companion = FindFirstObjectByType<MantaProgression>();
            if (companion == null || companion.Mount == null || companion.Mount.rider == null) return;
            if (companion.Data.collectedFruits.Contains(fruitId)) { gameObject.SetActive(false); return; }
            if (fruitVisual && !Dropped) { fruitVisual.localPosition = Vector3.up * (.4f + Mathf.Sin(Time.time * 2) * .15f); fruitVisual.Rotate(0, Time.deltaTime * 35, 0); }
            poll -= Time.deltaTime; if (poll > 0) return; poll = .15f;
            if (companion.GetComponent<MantaInput>().MenuOpen || companion.Failing || (Dropped && !Landed)) return;
            var rider = companion.Mount.rider;
            float distance = Vector3.Distance(rider.transform.position, transform.position);
            if (rider.Attached) distance = Mathf.Min(distance, Vector3.Distance(companion.transform.position, transform.position));
            if (distance <= collectRadius && companion.CollectFruit(fruitId, stat)) gameObject.SetActive(false);
        }
        public bool Drop(Vector3 towardRider)
        {
            if (Dropped || !gameObject.activeInHierarchy || (companion != null && companion.Data.collectedFruits.Contains(fruitId))) return false;
            Dropped = true;
            if (fruitVisual)
            {
                transform.position += transform.TransformVector(fruitVisual.localPosition);
                fruitVisual.localPosition = Vector3.zero;
            }
            var collider = gameObject.AddComponent<SphereCollider>(); collider.radius = .48f;
            ownedPhysics = new PhysicsMaterial("Fruit bounce") { bounciness = .35f, dynamicFriction = .5f, staticFriction = .6f,
                bounceCombine = PhysicsMaterialCombine.Maximum };
            collider.sharedMaterial = ownedPhysics;
            Body = gameObject.AddComponent<Rigidbody>(); Body.mass = .7f; Body.linearDamping = .15f; Body.angularDamping = .1f;
            Body.interpolation = RigidbodyInterpolation.Interpolate; Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            float height = Mathf.Max(1, transform.position.y - towardRider.y);
            float fallTime = Mathf.Sqrt(2 * height / Mathf.Max(.1f, Mathf.Abs(Physics.gravity.y)));
            Vector3 sideways = Vector3.ProjectOnPlane(towardRider - transform.position, Vector3.up);
            Body.linearVelocity = Vector3.ClampMagnitude(sideways / fallTime, 8) + Vector3.up * .5f;
            Body.angularVelocity = new Vector3(3, 5, 2);
            return true;
        }
        void OnCollisionEnter(Collision collision)
        {
            foreach (var contact in collision.contacts) if (contact.normal.y > .45f) { Landed = true; break; }
        }
        void FixedUpdate()
        {
            if (Body == null || companion == null) return;
            bool pause = companion.GetComponent<MantaInput>().MenuOpen;
            if (pause == frozen) return;
            if (pause) { heldVelocity = Body.linearVelocity; heldAngularVelocity = Body.angularVelocity; Body.isKinematic = true; }
            else { Body.isKinematic = false; Body.linearVelocity = heldVelocity; Body.angularVelocity = heldAngularVelocity; }
            frozen = pause;
        }
        void OnDestroy() { if (ownedMaterial) Destroy(ownedMaterial); if (ownedPhysics) Destroy(ownedPhysics); }
        public static void CreateExplorationRewards(MantaProgression companion)
        {
            // Designer-authored placements take precedence. Runtime seeds keep existing scenes playable without rewriting them.
            if (FindObjectsByType<MantaFruit>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) return;
            var root = new GameObject("Manta exploration fruits");
            root.transform.SetParent(companion.transform.parent, true);
            int mask = companion.Controller.settings.environmentMask;
            if (companion.Mount && companion.Mount.rider) mask |= companion.Mount.rider.settings.environment;
            int placed = 0;
            for (int index = 0; index < 10; index++)
            {
                string id = "manta-island-fruit-" + index;
                var stat = (MantaStat)(index % 5);
                var treePrefab = Resources.Load<GameObject>(MantaFruitTree.ResourcePath(stat));
                if (treePrefab == null) { Debug.LogError("Missing fruit tree prefab for " + stat); continue; }
                for (int attempt = 0; attempt < 18; attempt++)
                {
                    float angle = index * 2.39996f + attempt * .17f;
                    float radius = 140 + index * 58 + attempt * 13;
                    Vector3 origin = companion.transform.position + new Vector3(Mathf.Cos(angle) * radius, 1800, Mathf.Sin(angle) * radius);
                    if (!Physics.Raycast(origin, Vector3.down, out var hit, 4000, mask, QueryTriggerInteraction.Ignore)
                        || Vector3.Angle(hit.normal, Vector3.up) > 45) continue;
                    // Leave room for the giant trunk, and reject terrain too steep for its root footprint.
                    if (Vector3.Angle(hit.normal, Vector3.up) > 25) continue;
                    if (Physics.CheckCapsule(hit.point + Vector3.up * 2, hit.point + Vector3.up * 24,
                        1.5f, mask, QueryTriggerInteraction.Ignore)) continue;
                    Quaternion rotation = Quaternion.Euler(0, index * 137.5f, 0);
                    var template = treePrefab.GetComponent<MantaFruitTree>();
                    Vector3 anchorOffset = treePrefab.transform.TransformPoint(template.fruitAnchor.localPosition) - treePrefab.transform.position;
                    Vector3 position = hit.point + rotation * anchorOffset;
                    if (Physics.CheckSphere(position, 1.5f, mask, QueryTriggerInteraction.Ignore)) continue;
                    var tree = Instantiate(treePrefab, hit.point, rotation, root.transform);
                    tree.name = stat + " Fruit Tree " + (index / 5 + 1);
                    var reward = tree.AddComponent<MantaProgressionReward>(); reward.companion = companion;
                    reward.rewardId = "fruit-grove-" + index; reward.radius = 24;
                    // Collected fruit stays gone, while the colored landmark survives reloads.
                    if (companion.Data.collectedFruits.Contains(id)) break;
                    var go = new GameObject(((MantaStat)(index % 5)) + " Fruit " + (index / 5 + 1));
                    go.transform.SetParent(root.transform, false); go.transform.position = position;
                    var fruit = go.AddComponent<MantaFruit>(); fruit.fruitId = id; fruit.stat = stat; fruit.companion = companion;
                    tree.GetComponent<MantaFruitTree>().fruit = fruit;
                    var model = Resources.Load<GameObject>(MantaStatStyle.Get(fruit.stat).resource);
                    if (model == null) { Debug.LogError("Missing Blender fruit model for " + fruit.stat); Destroy(go); break; }
                    var visual = Instantiate(model, go.transform, false); visual.name = MantaStatStyle.Get(fruit.stat).shape + " fruit";
                    visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one; fruit.fruitVisual = visual.transform;
                    Color color = MantaStatStyle.Color(fruit.stat);
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null)
                    {
                        fruit.ownedMaterial = new Material(shader); fruit.ownedMaterial.name = fruit.stat + " Fruit";
                        fruit.ownedMaterial.color = color; fruit.ownedMaterial.SetFloat("_Smoothness", .35f);
                        fruit.ownedMaterial.EnableKeyword("_EMISSION"); fruit.ownedMaterial.SetColor("_EmissionColor", color * .12f);
                        foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = fruit.ownedMaterial;
                    }
                    var light = go.AddComponent<Light>(); light.color = color; light.range = 7; light.intensity = 2; light.shadows = LightShadows.None;
                    placed++; break;
                }
            }
            Debug.Log($"Manta progression: placed {placed} uncollected exploration fruits (maximum two per stat).");
        }
    }
}
