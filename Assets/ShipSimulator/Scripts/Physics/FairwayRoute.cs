using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    [Serializable]
    public struct FairwayRouteSample
    {
        public Vector3 position;
        public float leftWidthM;
        public float rightWidthM;
        public float centerDepthM;
        public float leftEdgeDepthM;
        public float rightEdgeDepthM;
        public float speedLimitMps;
    }

    public readonly struct FairwayQuery
    {
        public FairwayQuery(Vector3 position, Vector3 tangent, Vector3 right,
            float routeDistanceM, float lateralOffsetM, FairwayRouteSample sample)
        {
            Position = position;
            Tangent = tangent;
            Right = right;
            RouteDistanceM = routeDistanceM;
            LateralOffsetM = lateralOffsetM;
            Sample = sample;
        }

        public Vector3 Position { get; }
        public Vector3 Tangent { get; }
        public Vector3 Right { get; }
        public float RouteDistanceM { get; }
        public float LateralOffsetM { get; }
        public FairwayRouteSample Sample { get; }
    }

    public sealed class FairwayRoute : MonoBehaviour
    {
        [SerializeField] private FairwayRouteSample[] samples = Array.Empty<FairwayRouteSample>();
        private float[] cumulativeDistances = Array.Empty<float>();

        public float LengthM => cumulativeDistances.Length == 0
            ? 0f
            : cumulativeDistances[cumulativeDistances.Length - 1];
        public int SampleCount => samples?.Length ?? 0;

        public void Configure(FairwayRouteSample[] routeSamples)
        {
            samples = routeSamples ?? Array.Empty<FairwayRouteSample>();
            RebuildDistances();
        }

        private void Awake()
        {
            RebuildDistances();
        }

        private void OnValidate()
        {
            RebuildDistances();
        }

        public FairwayQuery Query(Vector3 worldPosition)
        {
            if (samples == null || samples.Length == 0)
                return new FairwayQuery(worldPosition, Vector3.forward, Vector3.right,
                    0f, 0f, default);
            if (samples.Length == 1)
                return BuildQuery(0, 0f, worldPosition);

            float bestSqrDistance = float.PositiveInfinity;
            int bestSegment = 0;
            float bestT = 0f;
            for (int i = 0; i < samples.Length - 1; i++)
            {
                Vector3 start = samples[i].position;
                Vector3 segment = samples[i + 1].position - start;
                segment.y = 0f;
                float lengthSqr = segment.sqrMagnitude;
                float t = lengthSqr > 0.001f
                    ? Mathf.Clamp01(Vector3.Dot(worldPosition - start, segment) / lengthSqr)
                    : 0f;
                Vector3 nearest = start + segment * t;
                float sqrDistance = HorizontalSqrDistance(worldPosition, nearest);
                if (sqrDistance >= bestSqrDistance) continue;
                bestSqrDistance = sqrDistance;
                bestSegment = i;
                bestT = t;
            }
            return BuildQuery(bestSegment, bestT, worldPosition);
        }

        public FairwayQuery QueryDistance(float routeDistanceM)
        {
            if (samples == null || samples.Length == 0)
                return new FairwayQuery(Vector3.zero, Vector3.forward, Vector3.right,
                    0f, 0f, default);
            if (samples.Length == 1)
                return BuildQuery(0, 0f, samples[0].position);

            float distance = Mathf.Clamp(routeDistanceM, 0f, LengthM);
            for (int i = 0; i < cumulativeDistances.Length - 1; i++)
            {
                if (distance > cumulativeDistances[i + 1]) continue;
                float segmentLength = cumulativeDistances[i + 1] - cumulativeDistances[i];
                float t = segmentLength > 0.001f
                    ? (distance - cumulativeDistances[i]) / segmentLength
                    : 0f;
                return BuildQuery(i, t, Vector3.Lerp(
                    samples[i].position, samples[i + 1].position, t));
            }
            return BuildQuery(samples.Length - 2, 1f,
                samples[samples.Length - 1].position);
        }

        private FairwayQuery BuildQuery(int segmentIndex, float t, Vector3 worldPosition)
        {
            int nextIndex = Mathf.Min(segmentIndex + 1, samples.Length - 1);
            FairwayRouteSample sample = Lerp(samples[segmentIndex], samples[nextIndex], t);
            Vector3 tangent = samples[nextIndex].position - samples[segmentIndex].position;
            tangent.y = 0f;
            tangent = tangent.sqrMagnitude > 0.001f ? tangent.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Vector3 nearest = Vector3.Lerp(
                samples[segmentIndex].position, samples[nextIndex].position, t);
            float lateral = Vector3.Dot(worldPosition - nearest, right);
            float routeDistance = cumulativeDistances.Length > segmentIndex
                ? cumulativeDistances[segmentIndex] +
                  Vector3.Distance(samples[segmentIndex].position, nearest)
                : 0f;
            return new FairwayQuery(nearest, tangent, right, routeDistance, lateral, sample);
        }

        private void RebuildDistances()
        {
            if (samples == null)
            {
                cumulativeDistances = Array.Empty<float>();
                return;
            }
            cumulativeDistances = new float[samples.Length];
            for (int i = 1; i < samples.Length; i++)
                cumulativeDistances[i] = cumulativeDistances[i - 1] +
                    Vector3.Distance(samples[i - 1].position, samples[i].position);
        }

        private static FairwayRouteSample Lerp(
            FairwayRouteSample a, FairwayRouteSample b, float t)
        {
            return new FairwayRouteSample
            {
                position = Vector3.Lerp(a.position, b.position, t),
                leftWidthM = Mathf.Lerp(a.leftWidthM, b.leftWidthM, t),
                rightWidthM = Mathf.Lerp(a.rightWidthM, b.rightWidthM, t),
                centerDepthM = Mathf.Lerp(a.centerDepthM, b.centerDepthM, t),
                leftEdgeDepthM = Mathf.Lerp(a.leftEdgeDepthM, b.leftEdgeDepthM, t),
                rightEdgeDepthM = Mathf.Lerp(a.rightEdgeDepthM, b.rightEdgeDepthM, t),
                speedLimitMps = Mathf.Lerp(a.speedLimitMps, b.speedLimitMps, t)
            };
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
