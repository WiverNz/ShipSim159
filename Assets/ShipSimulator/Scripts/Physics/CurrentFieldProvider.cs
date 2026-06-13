using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    public enum CurrentCompositionMode
    {
        Additive,
        Override,
        MaxMagnitude
    }

    [Serializable]
    public struct CurrentRegionData
    {
        public Vector3 center;
        public Vector3 size;
        public Vector3 velocityMps;
        public float blendDistanceM;
        public CurrentCompositionMode compositionMode;
        public int priority;
    }

    public sealed class CurrentFieldProvider : MonoBehaviour
    {
        [SerializeField] private Vector3 baseCurrentMps = new Vector3(0f, 0f, 0.35f);
        [SerializeField] private CurrentRegionData[] regions = Array.Empty<CurrentRegionData>();
        [SerializeField, Range(0f, 1.5f)] private float dischargeMultiplier = 1f;

        public void Configure(Vector3 baseVelocity, CurrentRegionData[] currentRegions)
        {
            baseCurrentMps = baseVelocity;
            regions = currentRegions ?? Array.Empty<CurrentRegionData>();
        }

        public void SetDischargeMultiplier(float value)
        {
            dischargeMultiplier = Mathf.Clamp(value, 0f, 1.5f);
        }

        public Vector3 Sample(Vector3 worldPosition)
        {
            Vector3 current = baseCurrentMps;
            int overridePriority = int.MinValue;
            foreach (CurrentRegionData region in regions)
            {
                float weight = RegionWeight(region, worldPosition);
                if (weight <= 0f) continue;
                Vector3 contribution = region.velocityMps * weight;
                switch (region.compositionMode)
                {
                    case CurrentCompositionMode.Additive:
                        current += contribution;
                        break;
                    case CurrentCompositionMode.Override:
                        if (region.priority < overridePriority) break;
                        current = Vector3.Lerp(current, region.velocityMps, weight);
                        overridePriority = region.priority;
                        break;
                    case CurrentCompositionMode.MaxMagnitude:
                        if (contribution.sqrMagnitude > current.sqrMagnitude)
                            current = contribution;
                        break;
                }
            }
            return current * dischargeMultiplier;
        }

        private static float RegionWeight(CurrentRegionData region, Vector3 point)
        {
            Vector3 half = region.size * 0.5f;
            Vector3 delta = point - region.center;
            float outsideX = Mathf.Abs(delta.x) - half.x;
            float outsideY = Mathf.Abs(delta.y) - half.y;
            float outsideZ = Mathf.Abs(delta.z) - half.z;
            float outside = Mathf.Max(outsideX, Mathf.Max(outsideY, outsideZ));
            if (outside > 0f) return 0f;
            float edgeDistance = Mathf.Min(
                half.x - Mathf.Abs(delta.x),
                Mathf.Min(half.y - Mathf.Abs(delta.y), half.z - Mathf.Abs(delta.z)));
            return region.blendDistanceM <= 0f
                ? 1f
                : Mathf.Clamp01(edgeDistance / region.blendDistanceM);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
            foreach (CurrentRegionData region in regions)
            {
                Gizmos.DrawWireCube(region.center, region.size);
                Gizmos.DrawRay(region.center, region.velocityMps * 20f);
            }
        }
    }
}
