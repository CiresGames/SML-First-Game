using UnityEngine;

namespace MantaFlight
{
    public enum MantaRewardKind { Landmark, AerialChallenge, Journey, Story }
    public sealed class MantaProgressionReward : MonoBehaviour
    {
        [Tooltip("Stable unique ID; each reward can be earned only once per companion save.")]
        public string rewardId;
        public MantaRewardKind kind;
        public MantaStat trainingStat;
        public MantaProgression companion;
        [Min(1)] public float radius = 12;
        [Min(0)] public float minimumChallengeSpeed = 35;
        float poll;
        void Update()
        {
            poll -= Time.deltaTime; if (poll > 0) return; poll = .25f;
            if (companion == null) companion = FindFirstObjectByType<MantaProgression>();
            if (companion == null || companion.Mount == null || companion.Mount.rider == null || string.IsNullOrWhiteSpace(rewardId)) return;
            if (companion.Failing || companion.GetComponent<MantaInput>().MenuOpen) return;
            if (Vector3.Distance(companion.Mount.rider.transform.position, transform.position) > radius) return;
            if (kind == MantaRewardKind.AerialChallenge && (!companion.Mount.rider.Mounted || companion.Controller.Speed < minimumChallengeSpeed)) return;
            Grant();
        }
        public void Grant()
        {
            if (companion == null || string.IsNullOrWhiteSpace(rewardId)) return;
            switch (kind)
            {
                case MantaRewardKind.Landmark: companion.DiscoverLocation(rewardId); break;
                case MantaRewardKind.AerialChallenge: companion.CompleteChallenge(rewardId, trainingStat); break;
                case MantaRewardKind.Journey: companion.CompleteJourney(rewardId); break;
                case MantaRewardKind.Story: companion.RecordStoryEvent(rewardId); break;
            }
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, radius); }
    }
}
