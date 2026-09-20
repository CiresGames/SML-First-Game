using System;
using UnityEngine;

namespace MantaFlight
{
    [Serializable]
    public sealed class MantaImpactSettings
    {
        public bool enabled = true;
        [Min(0)] public float lookAheadDistance = 5;
        [Min(0)] public float lookAheadSeconds = 1.2f;
        [Min(.1f)] public float probeRadius = 1.6f;
        [Min(.1f)] public float escapeProbeDistance = 25;
        [Min(0)] public float cameraOffset = .9f;
        [Range(0, 20)] public float cameraYaw = 4;
        [Range(0, 20)] public float cameraRoll = 5;
        [Min(.01f)] public float cameraBlendIn = .15f;
        [Min(.01f)] public float cameraBlendOut = .5f;
        public AnimationCurve cameraEnvelope = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.35f, 1), new Keyframe(1, 0));
        public LayerMask layers = 1;
        [Tooltip("Empty accepts all tags. The layer must also match.")]
        public string requiredTag = "";
        [Min(0)] public float minimumSpeed = 18;
        [Tooltip("0 is a head-on impact. Larger angles allow more glancing contacts.")]
        [Range(0, 89)] public float maximumAngleFromNormal = 50;
        [Min(.1f)] public float duration = .85f;
        public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Range(.1f, 1)] public float minimumSpeedFactor = .88f;
        [Range(.1f, 1.5f)] public float exitSpeedFactor = 1f;
        public AnimationCurve slowdownCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(.3f, 1), new Keyframe(1, 0));
        [Min(0)] public float repeatCooldown = .75f;
        [Min(0)] public float inputLockTime = .08f;
        [Range(0, 1)] public float steeringInfluence = .2f;
        [Min(0)] public float steeringDegreesPerSecond = 25;
        [Range(0, 35)] public float maximumExitDeviation = 10;
        public bool brakeCanCancel = true;
        [Range(.1f, 1)] public float brakeCancelThreshold = .65f;
        [Range(0, .8f)] public float minimumOutwardDot = .25f;
        [Min(.1f)] public float normalAdaptation = 10;
        [Range(0, 80)] public float banking = 58;
        [Min(.1f)] public float releaseUprightSpeed = 180;

        public float RotationFraction(float progress)
        {
            if (progress <= 0) return 0;
            if (progress >= 1) return 1;
            if (rotationCurve == null || rotationCurve.length < 2) return Mathf.SmoothStep(0, 1, progress);
            float start = rotationCurve.Evaluate(0), range = rotationCurve.Evaluate(1) - start;
            return Mathf.Abs(range) < .0001f ? progress : Mathf.Clamp01((rotationCurve.Evaluate(progress) - start) / range);
        }
    }

    [Serializable]
    public sealed class MantaInputSettings
    {
        [Range(0, .8f)] public float stickDeadZone = .12f;
        [Range(.5f, 3)] public float sensitivityExponent = 1.4f;
        [Range(.001f, .2f)] public float mouseSensitivity = .035f;
        public bool invertPitch;
        public void Capture(MantaInput input)
        {
            stickDeadZone = input.stickDeadZone; sensitivityExponent = input.sensitivityExponent;
            mouseSensitivity = input.mouseSensitivity; invertPitch = input.invertPitch;
        }
        public void Apply(MantaInput input)
        {
            input.stickDeadZone = stickDeadZone; input.sensitivityExponent = sensitivityExponent;
            input.mouseSensitivity = mouseSensitivity; input.invertPitch = invertPitch;
        }
    }
}
