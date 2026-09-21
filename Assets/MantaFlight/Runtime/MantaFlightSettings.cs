using UnityEngine;

namespace MantaFlight
{
    public enum FlightPhase { BasicFlight, SpeedAndCamera, AdvancedManeuvers, Polish }

    [CreateAssetMenu(menuName = "Manta/Flight settings")]
    public sealed class MantaFlightSettings : ScriptableObject
    {
        [Header("Iteration — enable one layer at a time")]
        public FlightPhase phase = FlightPhase.BasicFlight;
        [Header("Speed (metres / second)")]
        [Min(0)] public float minimumSpeed = 12;
        [Min(1)] public float cruiseSpeed = 28;
        [Min(1)] public float maximumSpeed = 64;
        [Min(1)] public float diveMaximumSpeed = 90;
        [Min(0)] public float acceleration = 19;
        [Min(0)] public float deceleration = 30;
        [Min(0)] public float cruiseRelaxation = 2.2f;
        public AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0, 1, 1, .3f);
        [Header("Direction (degrees / second)")]
        [Min(1)] public float pitchSpeed = 65;
        [Min(1)] public float yawSpeed = 70;
        [Range(10, 89)] public float pitchLimit = 82;
        [Min(.1f)] public float turnAcceleration = 7;
        [Min(.1f)] public float turnDamping = 5;
        [Min(.1f)] public float     momentumResponse = 4.5f;
        [Min(1)] public float rollSpeed = 150;
        [Range(0, 85)] public float maximumBanking = 48;
        [Min(.1f)] public float bankingSmoothing = 5;
        [Header("Energy & advanced maneuvers")]
        [Min(0)] public float diveAcceleration = 24;
        [Min(0)] public float climbDeceleration = 10;
        [Min(1)] public float tightTurnMultiplier = 2.3f;
        [Min(0)] public float tightTurnDrag = 7;
        [Min(.2f)] public float barrelDuration = .85f;
        [Header("Manual turnaround")]
        [Min(.3f)] public float turnaroundDuration = 1.25f;
        [Range(.5f, 1)] public float turnaroundSpeedRetention = .85f;
        [Min(0)] public float maneuverCooldown = .25f;
        [Header("Collision")]
        public LayerMask environmentMask = 1;
        [Min(.1f)] public float collisionRadius = 1.4f;
        [Min(0)] public float collisionSkin = .12f;
        [UnityEngine.Serialization.FormerlySerializedAs("impactTurn")]
        public MantaImpactSettings swerve = new MantaImpactSettings();
        public MantaImpactSettings impactTurn => swerve; // Compatibility with existing editor integrations.
        [Header("Input profile — exported with runtime tuning")]
        public bool useProfileInput;
        public MantaInputSettings input = new MantaInputSettings();
        [Header("Camera")]
        [Range(40, 100)] public float minimumFOV = 58;
        [Range(40, 110)] public float maximumFOV = 76;
        [Min(1)] public float cameraDistance = 13;
        [Min(0)] public float speedCameraDistance = 5;
        public float cameraHeight = 4.2f;
        [Min(.01f)] public float cameraLag = .16f;
        [Min(.1f)] public float cameraRotationDamping = 5;
        [Min(0)] public float cameraAnticipation = .18f;
        [Range(0, .5f)] public float cameraBankFraction = .12f;
        [Min(.1f)] public float cameraCollisionRadius = .45f;
        [Header("Living creature")]
        [Range(0, 5)] public float animationIntensity = 1;
        [Range(0, 50)] public float wingAmplitude = 12;
        [Range(.1f, 3)] public float wingFrequency = .65f;
        [Range(0, 1)] public float speedEffectsIntensity = .65f;

        public bool Has(FlightPhase layer) => phase >= layer;
        public static float Damp(float response, float dt) => 1 - Mathf.Exp(-Mathf.Max(0, response) * dt);
        void OnValidate()
        {
            maximumSpeed = Mathf.Max(minimumSpeed + 1, maximumSpeed);
            cruiseSpeed = Mathf.Clamp(cruiseSpeed, minimumSpeed, maximumSpeed);
            diveMaximumSpeed = Mathf.Max(maximumSpeed, diveMaximumSpeed);
            maximumFOV = Mathf.Max(minimumFOV, maximumFOV);
        }
    }
}
