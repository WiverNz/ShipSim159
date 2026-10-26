using System;
using System.IO;
using UnityEngine;

namespace ShipSimulator.Persistence
{
    [Serializable]
    public sealed class PushUnitSave
    {
        public bool present;
        public string bargeId;
        public bool coupled;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public void Validate()
        {
            if (!present) return;
            if (bargeId != "mn52-t77" || !Finite(position, 1000000) || !Finite(velocity, 1000) ||
                !Finite(angularVelocity, 100)) throw new InvalidDataException("Invalid pushed vessel state.");
            float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (float.IsNaN(norm) || float.IsInfinity(norm) || Mathf.Abs(norm - 1) > .01f)
                throw new InvalidDataException("Invalid pushed vessel rotation.");
        }

        private static bool Finite(Vector3 value, float limit) =>
            !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude) &&
            Mathf.Abs(value.x) <= limit && Mathf.Abs(value.y) <= limit && Mathf.Abs(value.z) <= limit;
    }
}
