using UnityEngine;

namespace MantaFlight
{
    public sealed class MantaManeuvers : MonoBehaviour
    {
        public MantaTrick Current { get; private set; }
        public float VisualRoll { get; private set; }
        public float Progress { get; private set; }
        public bool IsLooping => Current == MantaTrick.LoopForward || Current == MantaTrick.LoopBackward;
        public Vector3 EntryPosition { get; private set; }
        public Quaternion EntryHeading => entry;
        public Vector3 EntryVelocity { get; private set; }
        public float Duration => duration;
        public bool ControlsHeading => Current == MantaTrick.LoopForward || Current == MantaTrick.LoopBackward || Current == MantaTrick.Turnaround;
        public bool LocksSpeed => Current != MantaTrick.None;
        Quaternion entry;
        float elapsed, duration, cooldown, turnSign = 1;

        public bool TryStart(MantaTrick trick, MantaController controller, float steering = 0)
        {
            if (!controller.settings.Has(FlightPhase.AdvancedManeuvers) || Current != MantaTrick.None || cooldown > 0 || trick == MantaTrick.None) return false;
            Current = trick; elapsed = Progress = 0; entry = controller.Heading;
            EntryPosition = controller.GetComponent<Rigidbody>().position;
            EntryVelocity = controller.Velocity;
            turnSign = steering < -.1f ? -1 : 1;
            var s = controller.settings;
            duration = trick == MantaTrick.Turnaround ? s.turnaroundDuration :
                trick == MantaTrick.RollLeft || trick == MantaTrick.RollRight ? s.barrelDuration : s.loopDuration;
            return true;
        }
        public void Step(MantaController controller, float dt)
        {
            cooldown = Mathf.Max(0, cooldown - dt);
            if (Current == MantaTrick.None) return;
            float oldProgress = Progress;
            elapsed += dt; Progress = Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration));
            float t = Progress * Progress * (3 - 2 * Progress);
            switch (Current)
            {
                case MantaTrick.RollLeft: VisualRoll = 360 * t; break;
                case MantaTrick.RollRight: VisualRoll = -360 * t; break;
                case MantaTrick.LoopForward: controller.SetHeading(entry * Quaternion.AngleAxis(360 * t, Vector3.right)); break;
                case MantaTrick.LoopBackward: controller.SetHeading(entry * Quaternion.AngleAxis(-360 * t, Vector3.right)); break;
                case MantaTrick.Turnaround:
                    controller.SetHeading(Quaternion.AngleAxis(180 * t * turnSign, Vector3.up) * entry);
                    VisualRoll = -turnSign * 72 * Mathf.Sin(t * Mathf.PI);
                    controller.ScaleSpeed(Mathf.Pow(controller.settings.turnaroundSpeedRetention, Progress - oldProgress));
                    break;
            }
            if (Progress >= 1)
            {
                if (Current == MantaTrick.LoopForward || Current == MantaTrick.LoopBackward) controller.SetHeading(entry);
                Current = MantaTrick.None; VisualRoll = 0; cooldown = controller.settings.maneuverCooldown;
            }
        }
        public void Cancel() { Current = MantaTrick.None; VisualRoll = elapsed = Progress = cooldown = 0; }
    }
}
