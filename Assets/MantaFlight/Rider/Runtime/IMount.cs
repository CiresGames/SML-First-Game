using UnityEngine;
namespace MantaFlight.Rider
{
    public interface IMount
    {
        Transform Seat { get; }
        Vector3 Velocity { get; }
        bool CanDismount(out Vector3 groundPosition);
        void Dismount();
        void CallToPosition(Vector3 position, Vector3 velocity, bool airborne);
        void CancelCall();
        void AttachRider();
    }
}
