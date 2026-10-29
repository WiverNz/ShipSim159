using UnityEngine;

namespace ShipSimulator.UI
{
    public sealed class MapChartData : ScriptableObject
    {
        public float northHeading;
        // Paired endpoints are the same exposed shoreline edges used by the terrain builder.
        public Vector3[] shoreSegments;
    }
}
