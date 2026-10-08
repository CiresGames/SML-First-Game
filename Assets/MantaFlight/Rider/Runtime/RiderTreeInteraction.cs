using TMPro;
using UnityEngine;

namespace MantaFlight.Rider
{
    [DisallowMultipleComponent]
    public sealed class RiderTreeInteraction : MonoBehaviour
    {
        public const float PunchDuration = .9f, ImpactTime = .34f;
        public RiderController rider;
        public float interactionReach = 2;
        public bool IsBusy { get; private set; }
        public bool PlayingPunch { get; private set; }
        public float PunchTime { get; private set; }
        public MantaFruitTree Target { get; private set; }
        float approachTime;
        bool struck;
        Vector3 punchPoint;
        GameObject canvas;
        TextMeshProUGUI hint;

        public MantaFruitTree Nearby(out Vector3 point)
        {
            point = default;
            if (rider == null || rider.State != RiderState.Grounded || rider.Paused) return null;
            MantaFruitTree closest = null;
            float distance = interactionReach;
            foreach (var tree in FindObjectsByType<MantaFruitTree>(FindObjectsSortMode.None))
                if (tree.TryGetPunchPoint(rider, distance, out var candidate))
                {
                    distance = Vector3.Distance(rider.transform.position + Vector3.up * (rider.Motor.Capsule.height * Mathf.Abs(rider.transform.lossyScale.y) * .7f), candidate);
                    closest = tree; point = candidate;
                }
            return closest;
        }
        public bool Tick(RiderCommand command, float dt)
        {
            if (!IsBusy && command.context)
            {
                Target = Nearby(out punchPoint);
                if (Target != null)
                {
                    IsBusy = true; approachTime = PunchTime = 0; struck = PlayingPunch = false;
                    rider.CommandWheel.Cancel(); rider.LandingTarget.CancelPreview(); rider.Motor.SetCrouched(false);
                }
            }
            if (!IsBusy) return false;
            if (rider.Paused) return true;
            if (Target == null || rider.State != RiderState.Grounded || (!struck && !Target.HasHangingFruit)) { Cancel(); return false; }
            Vector3 forward = Vector3.ProjectOnPlane(punchPoint - rider.transform.position, Vector3.up).normalized;
            if (forward.sqrMagnitude > .01f)
                rider.transform.rotation = Quaternion.RotateTowards(rider.transform.rotation, Quaternion.LookRotation(forward), 720 * dt);
            if (!PlayingPunch)
            {
                approachTime += dt;
                float standDistance = .62f * Mathf.Abs(rider.transform.lossyScale.z);
                float distance = Vector3.ProjectOnPlane(punchPoint - rider.transform.position, Vector3.up).magnitude;
                rider.Motor.Velocity = forward * Mathf.Min(2.5f, Mathf.Max(0, distance - standDistance) / Mathf.Max(.001f, dt)) + Vector3.down * 2;
                rider.Motor.Move(dt);
                if (!rider.Motor.Grounded || approachTime > 1.5f) { Cancel(); return true; }
                if (distance <= standDistance + .06f) { PlayingPunch = true; PunchTime = 0; }
            }
            else
            {
                rider.Motor.GroundMove(default, rider.transform.forward, dt, true);
                if (!rider.Motor.Grounded) { Cancel(); return true; }
                PunchTime += dt;
                if (!struck && PunchTime >= ImpactTime)
                {
                    struck = true;
                    // Recheck the real surface at impact; an interrupted approach must not release a distant fruit.
                    if (Target.TryGetPunchPoint(rider, 1.3f, out _)) Target.KnockFruit(rider.transform.position);
                }
                if (PunchTime >= PunchDuration) Cancel();
            }
            return true;
        }
        public void Cancel() { IsBusy = PlayingPunch = false; Target = null; PunchTime = 0; }
        void OnDisable() { Cancel(); if (canvas) canvas.SetActive(false); }
        void LateUpdate()
        {
            if (rider == null) return;
            if (hint == null)
            {
                canvas = new GameObject("Tree interaction hint", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                canvas.transform.SetParent(transform, false);
                canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 25;
                var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var label = new GameObject("Punch tree", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(canvas.transform, false);
                hint = label.GetComponent<TextMeshProUGUI>(); hint.fontSize = 26; hint.alignment = TextAlignmentOptions.Center; hint.raycastTarget = false;
                hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(.5f, 0);
                hint.rectTransform.anchoredPosition = new Vector2(0, 150); hint.rectTransform.sizeDelta = new Vector2(800, 60);
            }
            var nearby = IsBusy ? Target : Nearby(out _);
            canvas.SetActive(!rider.Paused && rider.State == RiderState.Grounded && nearby != null);
            if (nearby != null)
            {
                hint.color = MantaStatStyle.Color(nearby.stat);
                hint.text = IsBusy ? (PlayingPunch ? "PUNCH!" : "Approaching tree…") : "X / Square / F • Punch tree";
            }
        }
        void OnDestroy() { if (canvas) Destroy(canvas); }
    }
}
