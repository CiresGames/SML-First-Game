using UnityEngine;

namespace MantaFlight
{
    public sealed class MantaVisuals : MonoBehaviour
    {
        public Transform visualRoot, leftWing, rightWing, leftTip, rightTip, rider, tail;
        public TrailRenderer[] trails;
        MantaController controller;
        MantaManeuvers maneuvers;
        Rider.MantaMountAdapter mountService;
        float clock;
        Vector3 visualPositionVelocity;
        void Awake() { controller = GetComponent<MantaController>(); maneuvers = GetComponent<MantaManeuvers>(); mountService = GetComponent<Rider.MantaMountAdapter>(); }
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
            bool autonomous = mountService != null && mountService.Mode != Rider.MantaServiceState.Piloted;
            if ((!autonomous && controller.Paused) || GetComponent<MantaInput>().MenuOpen) return;
            float bank = autonomous ? mountService.AutonomousBank : controller.Bank;
            float acceleration = autonomous ? 0 : controller.Acceleration;
            var s = controller.settings;
            bool polish = s.Has(FlightPhase.Polish);
            float intensity = polish ? s.animationIntensity : 0;
            clock += dt * s.wingFrequency * Mathf.Lerp(1.2f, .7f, controller.Speed01) * Mathf.PI * 2;
            float wave = Mathf.Sin(clock);
            float breath = wave * .12f * intensity;
            visualRoot.localPosition = Vector3.SmoothDamp(visualRoot.localPosition, new Vector3(0, breath, 0),
                ref visualPositionVelocity, .3f, Mathf.Infinity, dt);
            Quaternion wanted = maneuvers.VisualRotationOffset * Quaternion.Euler(Mathf.Clamp(-acceleration * .13f, -4, 4) * intensity, 0,
                bank + maneuvers.VisualRoll);
            visualRoot.localRotation = maneuvers.Current != MantaTrick.RollLeft && maneuvers.Current != MantaTrick.RollRight
                ? Quaternion.RotateTowards(visualRoot.localRotation, wanted, 160 * dt) : wanted;
            float flap = wave * s.wingAmplitude * Mathf.Lerp(1, .45f, controller.Speed01) * intensity;
            float flex = Mathf.Clamp(controller.Pitch * .1f, -7, 7) * intensity;
            leftWing.localRotation = Quaternion.Euler(0, 0, -flap - flex);
            rightWing.localRotation = Quaternion.Euler(0, 0, flap + flex);
            leftTip.localRotation = Quaternion.Euler(0, 0, -Mathf.Sin(clock - .65f) * s.wingAmplitude * intensity * .65f);
            rightTip.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(clock - .65f) * s.wingAmplitude * intensity * .65f);
            rider.localRotation = Quaternion.Euler(Mathf.Clamp(acceleration * .6f, -10, 10) * intensity, 0, -bank * .18f * intensity);
            tail.localRotation = Quaternion.Euler(wave * 4 * intensity, Mathf.Sin(clock - .8f) * 8 * intensity, 0);
            if (trails != null) foreach (var trail in trails)
            {
                trail.emitting = s.Has(FlightPhase.SpeedAndCamera) && controller.Speed01 > .45f;
                trail.widthMultiplier = .09f * s.speedEffectsIntensity * controller.Speed01;
            }
        }
    }
}
