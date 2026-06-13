using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    public enum RiverBottomType
    {
        Silt,
        Sand,
        Rock
    }

    [Serializable]
    public struct BathymetryHazard
    {
        public Vector3 center;
        public Vector2 sizeM;
        public float depthReductionM;
        public RiverBottomType bottomType;
    }

    public readonly struct BathymetrySample
    {
        public BathymetrySample(float depthM, RiverBottomType bottomType)
        {
            DepthM = depthM;
            BottomType = bottomType;
        }

        public float DepthM { get; }
        public RiverBottomType BottomType { get; }
    }

    public sealed class ScenarioBathymetry : MonoBehaviour
    {
        [SerializeField] private FairwayRoute route;
        [SerializeField] private float waterLevelOffsetM;
        [SerializeField] private float outsideDepthM = 1.2f;
        [SerializeField] private BathymetryHazard[] hazards = Array.Empty<BathymetryHazard>();

        public float WaterLevelOffsetM => waterLevelOffsetM;

        public void Configure(FairwayRoute fairwayRoute, BathymetryHazard[] localHazards)
        {
            route = fairwayRoute;
            hazards = localHazards ?? Array.Empty<BathymetryHazard>();
        }

        public void SetWaterLevelOffset(float value)
        {
            waterLevelOffsetM = value;
        }

        public BathymetrySample Sample(Vector3 worldPosition)
        {
            if (route == null)
                return new BathymetrySample(FairwayModel.DepthAt(worldPosition),
                    RiverBottomType.Sand);

            FairwayQuery query = route.Query(worldPosition);
            float offset = query.LateralOffsetM;
            float edgeWidth = offset >= 0f
                ? Mathf.Max(1f, query.Sample.rightWidthM)
                : Mathf.Max(1f, query.Sample.leftWidthM);
            float edgeDepth = offset >= 0f
                ? query.Sample.rightEdgeDepthM
                : query.Sample.leftEdgeDepthM;
            float normalized = Mathf.Abs(offset) / edgeWidth;
            float depth = normalized <= 1f
                ? Mathf.Lerp(query.Sample.centerDepthM, edgeDepth,
                    normalized * normalized * (3f - 2f * normalized))
                : Mathf.Lerp(edgeDepth, outsideDepthM,
                    Mathf.Clamp01((normalized - 1f) / 0.7f));
            RiverBottomType bottom = RiverBottomType.Sand;

            foreach (BathymetryHazard hazard in hazards)
            {
                Vector2 half = hazard.sizeM * 0.5f;
                float x = Mathf.Abs(worldPosition.x - hazard.center.x) /
                    Mathf.Max(half.x, 0.1f);
                float z = Mathf.Abs(worldPosition.z - hazard.center.z) /
                    Mathf.Max(half.y, 0.1f);
                float normalizedDistance = Mathf.Max(x, z);
                if (normalizedDistance >= 1f) continue;
                float blend = 1f - normalizedDistance;
                depth -= hazard.depthReductionM * blend * blend;
                bottom = hazard.bottomType;
            }

            return new BathymetrySample(Mathf.Max(0.1f, depth + waterLevelOffsetM), bottom);
        }
    }
}
