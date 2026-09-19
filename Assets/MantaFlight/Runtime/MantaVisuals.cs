using UnityEngine;

namespace MantaFlight
{
    public sealed class MantaVisuals : MonoBehaviour
    {
        public Transform visualRoot, leftWing, rightWing, leftTip, rightTip, rider, tail;
        public TrailRenderer[] trails;
        MantaController controller;
        MantaManeuvers maneuvers;
        float clock;
        Vector3 visualPositionVelocity;
        void Awake() { controller = GetComponent<MantaController>(); maneuvers = GetComponent<MantaManeuvers>(); }
        void OnEnable() { controller.Respawned += ClearTrails; }
        void OnDisable() { if (controller != null) controller.Respawned -= ClearTrails; }
        void ClearTrails()
        {
            if (trails != null) foreach (var trail in trails) trail.Clear();
            visualRoot.localPosition = Vector3.zero; visualRoot.localRotation = Quaternion.identity;
            visualPositionVelocity = Vector3.zero;
        }
        void LateUpdate() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (controller.Paused || GetComponent<MantaInput>().MenuOpen) return;
            var s = controller.settings;
            bool polish = s.Has(FlightPhase.Polish);
            float intensity = polish ? s.animationIntensity : 0;
            clock += dt * s.wingFrequency * Mathf.Lerp(1.2f, .7f, controller.Speed01) * Mathf.PI * 2;
            float wave = Mathf.Sin(clock);
            float breath = wave * .12f * intensity;
            Vector3 desiredPosition = maneuvers.VisualPositionOffset + new Vector3(0, breath, 0);
            visualRoot.localPosition = maneuvers.IsLooping ? desiredPosition : Vector3.SmoothDamp(visualRoot.localPosition,
                desiredPosition, ref visualPositionVelocity, maneuvers.VisualRecoveryTime, Mathf.Infinity, dt);
            if (maneuvers.IsLooping) visualPositionVelocity = Vector3.zero;
            Quaternion wanted = maneuvers.VisualRotationOffset * Quaternion.Euler(Mathf.Clamp(-controller.Acceleration * .13f, -4, 4) * intensity, 0,
                controller.Bank + maneuvers.VisualRoll);
            visualRoot.localRotation = !maneuvers.IsLooping && maneuvers.Current != MantaTrick.RollLeft && maneuvers.Current != MantaTrick.RollRight
                ? Quaternion.RotateTowards(visualRoot.localRotation, wanted, maneuvers.VisualReturnSpeed * dt) : wanted;
            float flap = wave * s.wingAmplitude * Mathf.Lerp(1, .45f, controller.Speed01) * intensity;
            float flex = Mathf.Clamp(controller.Pitch * .1f, -7, 7) * intensity;
            leftWing.localRotation = Quaternion.Euler(0, 0, -flap - flex);
            rightWing.localRotation = Quaternion.Euler(0, 0, flap + flex);
            leftTip.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(clock - .65f) * s.wingAmplitude * intensity * .65f);
            rightTip.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock - .65f) * s.wingAmplitude * intensity * .65f);
            rider.localRotation = Quaternion.Euler(Mathf.Clamp(controller.Acceleration * .6f, -10, 10) * intensity, 0, -controller.Bank * .18f * intensity);
            tail.localRotation = Quaternion.Euler(wave * 4 * intensity, Mathf.Sin(clock - .8f) * 8 * intensity, 0);
            if (trails != null) foreach (var trail in trails)
            {
                trail.emitting = s.Has(FlightPhase.SpeedAndCamera) && controller.Speed01 > .45f;
                trail.widthMultiplier = .09f * s.speedEffectsIntensity * controller.Speed01;
            }
        }
    }
}
