using System;
using UnityEngine;
namespace MantaFlight.Rider.Editor
{
    // Synchronous editor checks explicitly step physics; MovePosition is committed by simulation,
    // not by writing the rendered Transform. Restore project simulation/interpolation afterwards.
    public sealed class RiderPhysicsTestScope : IDisposable
    {
        readonly SimulationMode previous;
        readonly Rigidbody body;
        readonly RigidbodyInterpolation interpolation;
        public RiderPhysicsTestScope(RiderController rider)
        {
            previous = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            body = rider.mount.GetComponent<Rigidbody>(); interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;
        }
        public void Dispose() { Physics.simulationMode = previous; if (body != null) body.interpolation = interpolation; }
        public static void Step(float dt) { Physics.SyncTransforms(); Physics.Simulate(dt); }
    }
}
