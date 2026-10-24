using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShipSimulator.Persistence
{
    [Serializable]
    public sealed class AnchorSave
    {
        public string id;
        public int phase;
        public int command;
        public float chainOutM;
        // World position of the chain shackle while the anchor is on the bottom.
        public Vector3 position;
        public float embedment;
    }

    [Serializable]
    public sealed class MooringLineSave
    {
        public string shipPointId;
        public string shorePointId;
        public float lengthM;
        public int command;
    }

    // Anchors and mooring lines. JsonUtility always creates this object, so present marks a save that has it;
    // saves without it restore the chain lengths from the deck machinery state.
    [Serializable]
    public sealed class GroundTackleSave
    {
        public bool present;
        public AnchorSave[] anchors = Array.Empty<AnchorSave>();
        public MooringLineSave[] lines = Array.Empty<MooringLineSave>();

        public void Validate()
        {
            if (!present) return;
            if (anchors == null || lines == null || anchors.Length > 4 || lines.Length > 32)
                throw new InvalidDataException("Invalid saved ground tackle.");
            var ids = new HashSet<string>();
            foreach (AnchorSave anchor in anchors)
            {
                if (anchor == null || string.IsNullOrEmpty(anchor.id) || anchor.id.Length > 32 || !ids.Add(anchor.id) ||
                    anchor.phase < 0 || anchor.phase > 3 || anchor.command < 0 || anchor.command > 3 ||
                    !Range(anchor.chainOutM, 0, 1000) || !Range(anchor.embedment, 0, 1) ||
                    !Range(anchor.position.x, -1e6f, 1e6f) || !Range(anchor.position.y, -1e4f, 1e4f) ||
                    !Range(anchor.position.z, -1e6f, 1e6f))
                    throw new InvalidDataException("Invalid saved anchor state.");
            }
            ids.Clear();
            foreach (MooringLineSave line in lines)
                if (line == null || string.IsNullOrEmpty(line.shipPointId) || string.IsNullOrEmpty(line.shorePointId) ||
                    line.shipPointId.Length > 64 || line.shorePointId.Length > 64 || !ids.Add(line.shipPointId) ||
                    !Range(line.lengthM, 0.1f, 1000) || line.command < 0 || line.command > 2)
                    throw new InvalidDataException("Invalid saved mooring line.");
        }

        private static bool Range(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
