using System;
using System.IO;

namespace ShipSimulator.Persistence
{
    [Serializable]
    public sealed class MechanismSave
    {
        public int hatchTarget;
        public float opening;
        public float parking;
        public float anchorRelease;
        public bool anchorTarget;
        public float portAngle;
        public float starboardAngle;

        public void Validate()
        {
            if (hatchTarget < 0 || hatchTarget > 3 || !Range(opening, 0, 1) ||
                !Range(parking, -1, 1) || !Range(anchorRelease, 0, 1) ||
                !Range(portAngle, 0, 360) || !Range(starboardAngle, 0, 360) ||
                (Math.Abs(parking) > .0001f && opening != 1))
                throw new InvalidDataException("Invalid saved deck machinery state.");
        }

        private static bool Range(float value, float min, float max) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;
    }
}
