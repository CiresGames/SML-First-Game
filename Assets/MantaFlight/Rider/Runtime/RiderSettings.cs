using System;
using UnityEngine;
namespace MantaFlight.Rider
{
    [CreateAssetMenu(menuName = "Manta/Rider settings")]
    public sealed class RiderSettings : ScriptableObject
    {
        public GroundFeel ground = new GroundFeel();
        public GlideFeel glide = new GlideFeel();
        public RiderCameraFeel camera = new RiderCameraFeel();
        public MountFeel mount = new MountFeel();
        public LayerMask environment = 1;
        public bool secondJumpDeploys;
        [Min(0)] public float inputDeadZone = .12f;
        [Min(.1f)] public float inputExponent = 1.35f;
        public float mouseLookSensitivity = .08f, stickLookSensitivity = 100;
        public float resetBelowHeight = -70;
        void OnValidate()
        {
            inputDeadZone = Mathf.Clamp(inputDeadZone,0,.9f);
            ground.radius = Mathf.Max(.1f,ground.radius);
            ground.crouchHeight = Mathf.Max(ground.radius*2,ground.crouchHeight);
            ground.standingHeight = Mathf.Max(ground.crouchHeight,ground.standingHeight);
            ground.gravity = Mathf.Max(1,ground.gravity);ground.jumpHeight = Mathf.Max(0,ground.jumpHeight);
            ground.rollDuration = Mathf.Max(.1f,ground.rollDuration);ground.rollDistance = Mathf.Max(0,ground.rollDistance);
            glide.stallSpeed = Mathf.Max(1,glide.stallSpeed);glide.recoverSpeed = Mathf.Max(glide.stallSpeed+1,glide.recoverSpeed);
            glide.maximumSpeed = Mathf.Max(glide.recoverSpeed+1,glide.maximumSpeed);
            glide.stallAngle = Mathf.Clamp(glide.stallAngle, 10, 80);
            glide.stallHysteresis = Mathf.Clamp(glide.stallHysteresis, 0, glide.stallAngle - 1);
            glide.neutralTrimResponse = Mathf.Max(0, glide.neutralTrimResponse);
            mount.callAcceleration = Mathf.Max(1, mount.callAcceleration); mount.callSpeed = Mathf.Max(1, mount.callSpeed);
            mount.approachResponse = Mathf.Max(.1f, mount.approachResponse);
            mount.followResponse = Mathf.Max(.1f, mount.followResponse); mount.followSwayPeriod = Mathf.Max(.1f, mount.followSwayPeriod);
            mount.maximumRelativeSpeed = Mathf.Max(1, mount.maximumRelativeSpeed); mount.scoopRadius = Mathf.Max(.1f, mount.scoopRadius);
            glide.deployDuration = Mathf.Max(.01f,glide.deployDuration);glide.retractDuration = Mathf.Max(.01f,glide.retractDuration);
            camera.blendDuration = Mathf.Max(.01f,camera.blendDuration);camera.lag = Mathf.Max(.01f,camera.lag);
            camera.landingPulseDuration = Mathf.Max(.01f,camera.landingPulseDuration);
            mount.dismountDuration = Mathf.Max(.01f,mount.dismountDuration);mount.blendDuration = Mathf.Max(.01f,mount.blendDuration);
        }
    }
    [Serializable] public sealed class GroundFeel
    {
        public float walkSpeed = 3.2f, runSpeed = 6.5f, crouchSpeed = 1.7f;
        public float acceleration = 14, deceleration = 20, turnRate = 210;
        public float gravity = 27, jumpHeight = 1.15f, airAcceleration = 3, terminalSpeed = 65;
        public float standingHeight = 1.8f, crouchHeight = 1.05f, radius = .28f, groundProbe = .12f;
        public float rollDuration = .65f, rollDistance = 4.5f, rollCooldown = .7f;
        public float landingRecovery = .18f, hardLandingRecovery = .65f;
        public float forcedRollImpact = 12, forcedRollSpeed = 15, hardLandingImpact = 28;
        public float gentleLandingAngle = 40, landingShake = .1f;
    }
    [Serializable] public sealed class GlideFeel
    {
        public float gravity = 18, liftCoefficient = .075f, baseDrag = .012f, inducedDrag = .016f;
        public float trimAngle = 9, stallAngle = 35, stallSpeed = 9, recoverSpeed = 13;
        public float maximumSpeed = 65, targetGlideRatio = 3.2f, minimumSink = 1.2f, diveAcceleration = 5;
        [Tooltip("Pitch speed in degrees/second. Forward input dives; inversion is applied by RiderInput.")]
        [Min(1)] public float pitchRate = 90;
        [Tooltip("Speed at which the wingsuit rolls towards the requested bank (degrees/second).")]
        [Min(1)] public float bankRate = 145;
        [Tooltip("Bank-induced heading/trajectory turn speed (degrees/second).")]
        [Min(0)] public float yawRate = 70;
        [Range(1,89)] public float pitchLimit = 78;
        [Tooltip("Maximum wing bank. Higher values allow tighter turns.")]
        [Range(1,85)] public float bankLimit = 72;
        [Tooltip("Neutral pitch assistance: aligns the wings with the airflow and seeks the configured glide ratio. Zero disables it. Never retracts the wingsuit.")]
        [Min(0)] public float neutralTrimResponse = 3;
        [Tooltip("Incidence margin required before recovering a stall; prevents rapid stall flickering.")]
        [Range(0,15)] public float stallHysteresis = 5;
        public float inputSmoothing = 4, stallRecoveryTime = .3f, stallLiftFraction = .08f;
        public float minimumDeployHeight = 5, minimumAirTime = .35f, minimumDownSpeed = 3;
        public float deployDuration = .4f, retractDuration = .25f;
        public float windVolume = .12f, windMinPitch = .65f, windMaxPitch = 1.6f;
    }
    [Serializable] public sealed class RiderCameraFeel
    {
        public float groundDistance = 5, groundHeight = 1.4f, fallDistance = 6;
        public float glideDistance = 6, speedPullback = 5, lag = .13f, rotationDamping = 6;
        public float minFOV = 60, maxFOV = 80, blendDuration = .45f;
        public float bankFraction = .14f, maximumRoll = 8, lookLimit = 7, lookRecenter = 4;
        public float verticalFallbackStart = .82f, verticalFallbackEnd = .97f, lowSpeedFallback = 5;
        public float collisionRadius = .25f, landingPulseDuration = .25f, landingPulseFrequency = 28;
    }
    [Serializable] public sealed class MountFeel
    {
        public float dismountHeight = 4, dismountSpeed = 2, sideDistance = 3, dismountDuration = .45f;
        public float hoverBrakeThreshold = .8f, hoverDeceleration = 18, hoverEntrySpeed = 16;
        [Tooltip("Minimum world-up launch velocity for a jump-off, even when the manta is diving (m/s).")]
        [Min(0)] public float jumpUpSpeed = 10;
        [Tooltip("Outward launch speed relative to the manta; the safest clear direction is chosen (m/s).")]
        [Min(0)] public float jumpForwardSpeed = 12;
        [Range(0,1)] public float velocityInheritance = .85f;
        [Tooltip("Minimum duration to ignore only rider/manta collision pairs after leaving (seconds). Pairs stay ignored while still overlapping.")]
        [Min(0)] public float jumpCollisionIgnoreDuration = .35f;
        [Tooltip("Extra clearance around the rider capsule before manta collisions are restored (metres).")]
        [Min(0)] public float jumpSeparationClearance = .15f;
        [Tooltip("Length of the capsule sweep used to select a clear outward jump direction.")]
        [Min(.1f)] public float jumpProbeDistance = 3;
        [Tooltip("Space above the manta's collision surface at the start of an upward jump.")]
        [Min(.01f)] public float jumpStartClearance = .08f;
        public float blendDuration = .3f;
        [Tooltip("Capture radius around the saddle; the swept relative path also counts.")]
        [Min(.1f)] public float scoopRadius = 4;
        [Tooltip("Maximum relative approach speed for a safe automatic catch (m/s).")]
        [Min(1)] public float maximumRelativeSpeed = 16;
        public float gracePeriod = 1;
        [Tooltip("Maximum trajectory prediction horizon in seconds; reduced as the manta closes in.")]
        [Min(0)] public float predictionTime = 1.2f;
        public float approachBehind = 10, approachBelow = 2, matchDistance = 22;
        [Tooltip("Maximum autonomous speed. Keep above rider maximum glide speed to allow catches.")]
        [Min(1)] public float callSpeed = 95;
        [Tooltip("Autonomous acceleration and braking, including speed matching near the saddle.")]
        [Min(1)] public float callAcceleration = 65;
        [Tooltip("Autonomous velocity and body turn rate (degrees/second).")]
        [Min(1)] public float callTurnRate = 220;
        [Tooltip("Position error response used by follow, intercept and landing. Larger values close the gap faster.")]
        [Min(.1f)] public float approachResponse = 2.5f;
        public float callTimeout = 16, passTimeout = 5, retryDuration = 1.2f, maximumCallDistance = 600;
        public float landedMountDistance = 4, landingOffset = 5, hoverHeight = 2.2f;
        [Header("Companion follow")]
        [Tooltip("Trailing distance when the rider is unmounted. Grounded landed manta resumes following beyond this distance.")]
        [Min(1)] public float followDistance = 16;
        [Min(0)] public float followHeight = 6;
        [Tooltip("Smoothing of the trailing direction. Avoids rigid snapping when the rider turns.")]
        [Min(.1f)] public float followResponse = 1.8f;
        [Tooltip("Time constant smoothing rider position samples before physics following. Smaller values reduce lag.")]
        [Min(.01f)] public float followTargetSmoothing = .08f;
        [Tooltip("Small lateral/upward idle motion in metres, added to the follow target.")]
        [Min(0)] public float followSway = 1.2f;
        [Min(.1f)] public float followSwayPeriod = 5;
        [Header("Ground landing target")]
        [Tooltip("Maximum ground slope accepted for a landing, measured from world up.")]
        [Range(0,75)] public float maximumLandingSlope = 40;
        [Tooltip("Aim ray length and maximum target distance from the rider.")]
        [Min(1)] public float landingAimRange = 70;
        [Tooltip("Downward fallback ray height above the point in front of the rider.")]
        [Min(1)] public float landingProbeHeight = 25;
        [Tooltip("Transparent sphere radius for the landing indicator.")]
        [Min(.1f)] public float landingIndicatorRadius = 1.5f;
        [Tooltip("Position tolerance to settle at the target.")]
        [Min(.1f)] public float landingTolerance = .7f;
    }
}
