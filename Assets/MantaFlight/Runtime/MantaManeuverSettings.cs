using System;
using UnityEngine;

namespace MantaFlight
{
    public enum LoopSizeMode { Duration, NominalRadius }

    [Serializable]
    public sealed class MantaLoopSettings
    {
        [Min(.1f)] public float visualRadius = 1.5f;
        [Min(.01f)] public float visualRecoveryTime = .3f;
        public LoopSizeMode sizeMode = LoopSizeMode.Duration;
        [Min(.3f)] public float duration = 2.8f;
        [Tooltip("In radius mode, duration is derived from this nominal radius and entry speed. Curvature still varies with the rotation curve.")]
        [Min(2)] public float nominalRadius = 16;
        [Min(.3f)] public float minimumDuration = 1.2f;
        [Min(.3f)] public float maximumDuration = 5;
        [Tooltip("Progress -> rotation fraction. Endpoints are normalized and progression is constrained to stay monotonic.")]
        public AnimationCurve rotationCurve = new AnimationCurve(
            new Keyframe(0, 0, .12f, .12f), new Keyframe(.18f, .045f, .55f, .55f),
            new Keyframe(.5f, .5f, 2.25f, 2.25f), new Keyframe(.82f, .955f, .55f, .55f), new Keyframe(1, 1, .12f, .12f));
        [Range(.5f, 1.5f)] public float anticipationSpeedFactor = .96f;
        [Range(.01f, .45f)] public float anticipationFraction = .16f;
        [Range(0, .6f)] public float middleSpeedBoost = .22f;
        public AnimationCurve speedBoostCurve = new AnimationCurve(
            new Keyframe(0, 0), new Keyframe(.18f, 0), new Keyframe(.5f, 1), new Keyframe(.82f, 0), new Keyframe(1, 0));
        [Range(.5f, 1.5f)] public float exitSpeedFactor = 1;
        [Min(0)] public float inputLockTime = .22f;
        [Range(0, 1)] public float steeringInfluence = .25f;
        [Min(0)] public float steeringDegreesPerSecond = 35;
        [Range(0, 40)] public float maximumExitDeviation = 12;
        public AnimationCurve steeringCurve = AnimationCurve.EaseInOut(.2f, 0, .8f, 1);
        public bool brakeCanCancel = true;
        [Range(.1f, 1)] public float brakeCancelThreshold = .65f;
        [Min(.1f)] public float releaseUprightSpeed = 160;

        public float ResolveDuration(float entrySpeed)
        {
            float seconds = sizeMode == LoopSizeMode.NominalRadius
                ? 2 * Mathf.PI * Mathf.Max(2, nominalRadius) / Mathf.Max(1, entrySpeed) : duration;
            return Mathf.Clamp(seconds, Mathf.Max(.3f, minimumDuration), Mathf.Max(minimumDuration, maximumDuration));
        }
        public float RotationFraction(float progress)
        {
            if (progress <= 0) return 0;
            if (progress >= 1) return 1;
            if (rotationCurve == null || rotationCurve.length < 2) return Mathf.SmoothStep(0, 1, progress);
            float start = rotationCurve.Evaluate(0), range = rotationCurve.Evaluate(1) - start;
            return Mathf.Abs(range) < .0001f ? progress : Mathf.Clamp01((rotationCurve.Evaluate(progress) - start) / range);
        }
        public float SpeedFactor(float progress)
        {
            float anticipation = Mathf.Clamp(anticipationFraction, .01f, .45f);
            float baseFactor = progress <= anticipation
                ? Mathf.Lerp(1, anticipationSpeedFactor, Mathf.SmoothStep(0, 1, progress / anticipation))
                : Mathf.Lerp(anticipationSpeedFactor, exitSpeedFactor, Mathf.SmoothStep(0, 1, (progress - anticipation) / (1 - anticipation)));
            // The envelope guarantees continuous entry/exit even when a hand-edited boost curve has non-zero endpoints.
            float envelope = Mathf.SmoothStep(0, 1, progress / anticipation) * Mathf.SmoothStep(0, 1, (1 - progress) / anticipation);
            return baseFactor + Mathf.Max(0, middleSpeedBoost) * Mathf.Clamp01(speedBoostCurve == null ? 0 : speedBoostCurve.Evaluate(progress)) * envelope;
        }
    }

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
    public sealed class MantaLoopCameraSettings
    {
        [Min(1)] public float distance = 11;
        public Vector3 offset = new Vector3(0, 1.5f, 0);
        public Vector3 lookOffset = new Vector3(0, -.6f, 1);
        [Min(.01f)] public float positionLag = .09f;
        [Min(.1f)] public float rotationDamping = 5;
        [Range(-15, 15)] public float fovChange = 2;
        [Min(.01f)] public float blendIn = .35f;
        [Min(.01f)] public float blendOut = .55f;
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
