using UnityEngine;

namespace MantaFlight
{
    // Ownership marker for transient geometry. Never saved into the scene or a build.
    [AddComponentMenu("")]
    public sealed class MantaGeneratedCloudRoot : MonoBehaviour
    {
        public MantaCloudDistribution owner;
    }
}
