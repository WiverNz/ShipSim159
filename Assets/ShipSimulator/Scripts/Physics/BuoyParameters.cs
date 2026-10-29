using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    // Estimated small steel river mark, not a certified buoy specification.
    [Serializable]
    public sealed class BuoyParameters
    {
        public float massKg, floatRadiusM, floatCenterY;
        public float heaveStiffnessNPerM, heaveDampingNsPerM;
        public float dragNPerMps, dragNPerMpsSquared;
        public float mooringSlackM, mooringStiffnessNPerM, mooringDampingNsPerM, mooringMaxForceN;
        public float rightingAcceleration, angularDamping;

        public static BuoyParameters Load() => JsonUtility.FromJson<BuoyParameters>(
            Resources.Load<TextAsset>("BuoyPhysics").text);
    }
}
