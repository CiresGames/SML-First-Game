using UnityEngine;
using UnityEngine.InputSystem;

namespace MantaFlight
{
    /// <summary>Ground pickup/drop adapter. Excess cargo blocks takeoff until removed.</summary>
    public sealed class MantaCargo : MonoBehaviour
    {
        [Min(0)] public float kilograms = 25;
        public MantaProgression companion;
        public bool Carried { get; private set; }
        Transform previousParent;
        Collider[] colliders;
        bool[] colliderStates;
        Rigidbody body;
        bool wasKinematic, usedGravity;
        Vector3 originalScale;
        static int handledFrame = -1;
        void Update()
        {
            if (companion == null) companion = FindFirstObjectByType<MantaProgression>();
            if (companion == null || companion.GetComponent<MantaInput>().MenuOpen || companion.Failing) return;
            if (Keyboard.current?.bKey.wasPressedThisFrame != true) return;
            if (handledFrame == Time.frameCount) return;
            if (Carried) { if (Drop()) handledFrame = Time.frameCount; return; }
            if (companion.GetComponentInChildren<MantaCargo>() != null || !companion.Accessible || companion.Mount == null || !companion.Mount.Landed) return;
            var rider = companion.Mount.rider;
            float distance = Vector3.Distance(rider.transform.position, transform.position);
            if (distance > 4) return;
            foreach (var other in FindObjectsByType<MantaCargo>(FindObjectsSortMode.None))
                if (other != this && Vector3.Distance(rider.transform.position, other.transform.position) < distance) return;
            if (PickUp()) handledFrame = Time.frameCount;
        }
        public bool PickUp()
        {
            if (Carried || companion == null || !companion.Accessible || companion.Mount == null || !companion.Mount.Landed
                || companion.cargoKilograms > 0 || !companion.SetCargo(kilograms)) return false;
            previousParent = transform.parent; originalScale = transform.lossyScale;
            colliders = GetComponentsInChildren<Collider>(); colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++) { colliderStates[i] = colliders[i].enabled; colliders[i].enabled = false; }
            body = GetComponent<Rigidbody>();
            if (body) { wasKinematic = body.isKinematic; usedGravity = body.useGravity; body.isKinematic = true; body.useGravity = false; }
            transform.SetParent(companion.transform, true); transform.localPosition = Vector3.down * 1.5f;
            Carried = true; return true;
        }
        public bool Drop()
        {
            if (!Carried || companion == null || companion.Mount == null || !companion.Mount.Landed) return false;
            int mask = companion.Mount.rider.settings.environment;
            Vector3 target = Vector3.zero;
            bool clear = false;
            for (int side = 0; side < 4; side++)
            {
                Vector3 direction = Quaternion.Euler(0, side * 90, 0) * companion.transform.right;
                Vector3 origin = companion.transform.position + direction * 4 + Vector3.up * 5;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, 15, mask, QueryTriggerInteraction.Ignore)) continue;
                target = hit.point + Vector3.up * (originalScale.y * .5f + .15f);
                if (Vector3.Distance(target, companion.Mount.rider.transform.position) < originalScale.magnitude * .5f + 1) continue;
                if (Physics.CheckBox(target, originalScale * .5f, Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) continue;
                clear = true; break;
            }
            if (!clear) return false;
            transform.SetParent(previousParent, true); transform.SetPositionAndRotation(target, Quaternion.identity);
            for (int i = 0; i < colliders.Length; i++) if (colliders[i]) colliders[i].enabled = colliderStates[i];
            if (body) { body.isKinematic = wasKinematic; body.useGravity = usedGravity; }
            companion.SetCargo(0); Carried = false; return true;
        }
        void OnDestroy() { if (Carried && companion != null) companion.SetCargo(0); }
        public static void CreateStarterCargo(MantaProgression companion, Vector3 ground)
        {
            if (FindObjectsByType<MantaCargo>(FindObjectsSortMode.None).Length > 0) return;
            for (int i = 0; i < 2; i++)
            {
                Vector3 origin = ground + Vector3.forward * (5 + i * 4) + Vector3.up * 5;
                int mask = companion.Mount.rider.settings.environment;
                if (!Physics.Raycast(origin, Vector3.down, out var hit, 12, mask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 point = hit.point + Vector3.up * .8f;
                if (Physics.CheckBox(point, Vector3.one * .7f, Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) continue;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = i == 0 ? "Cargo • 25 kg" : "Cargo • 90 kg";
                go.transform.position = point; go.transform.localScale = Vector3.one * 1.4f;
                var cargo = go.AddComponent<MantaCargo>(); cargo.kilograms = i == 0 ? 25 : 90; cargo.companion = companion;
            }
        }
    }
}
