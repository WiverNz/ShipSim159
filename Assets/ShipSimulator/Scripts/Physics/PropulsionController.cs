using UnityEngine;

namespace ShipSimulator.Physics
{
    public sealed class PropulsionController : MonoBehaviour
    {
        public float ActualThrottle { get; private set; }
        public Vector3 LastForce { get; private set; }

        public void Step(Rigidbody body, VesselData data, float command, float dt)
        {
            float response = command < ActualThrottle ? data.engine.asternResponseSeconds : data.engine.aheadResponseSeconds;
            ActualThrottle = Mathf.MoveTowards(ActualThrottle, command, dt / Mathf.Max(0.1f, response));
            float maximum = ActualThrottle >= 0f ? data.propeller.maxAheadThrustN : data.propeller.maxAsternThrustN;
            LastForce = transform.forward * (ActualThrottle * maximum * data.propeller.count * data.calibration.thrustMultiplier);
            body.AddForceAtPosition(LastForce, transform.position, ForceMode.Force);
        }
    }
}

