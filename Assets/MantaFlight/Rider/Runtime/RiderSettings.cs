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
        public float pitchRate = 65, bankRate = 95, yawRate = 42, pitchLimit = 78, bankLimit = 65;
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
        public float jumpUpSpeed = 7, jumpForwardSpeed = 6, velocityInheritance = .85f;
        public float blendDuration = .3f, scoopRadius = 2.4f, maximumRelativeSpeed = 7, gracePeriod = 1;
        public float predictionTime = .6f, approachBehind = 10, approachBelow = 2, matchDistance = 16;
        public float callSpeed = 55, callAcceleration = 35, callTurnRate = 140;
        public float callTimeout = 16, passTimeout = 5, retryDuration = 1.2f, maximumCallDistance = 600;
        public float landedMountDistance = 4, landingOffset = 5, hoverHeight = 2.2f;
    }
}
