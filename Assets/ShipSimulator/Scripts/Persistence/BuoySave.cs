using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShipSimulator.Persistence
{
    [Serializable]
    public sealed class BuoySave
    {
        public string id;
        public Vector3 position, velocity, angularVelocity;
        public Quaternion rotation = Quaternion.identity;

        public static void Validate(BuoySave[] states)
        {
            if (states == null) return;
            if (states.Length > 512) throw new InvalidDataException("Too many saved buoys.");
            var ids = new HashSet<string>();
            foreach (BuoySave state in states)
            {
                if (state == null || string.IsNullOrEmpty(state.id) || state.id.Length > 200 || !ids.Add(state.id))
                    throw new InvalidDataException("Invalid or duplicate buoy id.");
                Check(state.position.sqrMagnitude, 3e12f);
                Check(state.velocity.sqrMagnitude, 1e6f);
                Check(state.angularVelocity.sqrMagnitude, 10000);
                Quaternion q = state.rotation;
                float norm = q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w;
                Check(norm, 1.01f);
                if (norm < 0.99f) throw new InvalidDataException("Invalid buoy rotation.");
            }
        }

        private static void Check(float value, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value > maximum)
                throw new InvalidDataException("Invalid buoy state.");
        }
    }
}
