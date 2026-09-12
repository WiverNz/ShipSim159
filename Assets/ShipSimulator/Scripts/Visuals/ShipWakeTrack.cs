using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Vessel track in the water frame, packed for the wake section of RiverWater.shader.
    // Packed point 0 is the bow, point 1 the stern, then stern history from newest to oldest.
    public sealed class ShipWakeTrack
    {
        // Must match WAKE_CAPACITY in RiverWater.shader.
        public const int Capacity = 48;
        public const float SampleSpacingM = 10f;
        public const float MaxSampleIntervalS = 4f;
        public const float TeleportDistanceM = 40f;

        private const int HistoryCapacity = Capacity - 2;

        private readonly Vector2[] positions = new Vector2[HistoryCapacity];
        private readonly float[] times = new float[HistoryCapacity];
        private readonly float[] speeds = new float[HistoryCapacity];
        private readonly float[] washes = new float[HistoryCapacity];
        private int newest = -1;
        private int count;
        private float clock;
        private bool hasPose;
        private Vector2 bow;
        private Vector2 stern;
        private float speed;
        private float wash;

        public int HistoryCount => count;

        public void Clear()
        {
            count = 0;
            newest = -1;
            hasPose = false;
        }

        public void Record(Vector2 bowPosition, Vector2 sternPosition, float forwardSpeedMps,
            float propellerWash, Vector2 currentMps, float deltaTime)
        {
            // A reset or a loaded voyage must not draw a wake across the jump.
            if (hasPose && (sternPosition - stern).magnitude > TeleportDistanceM) Clear();

            clock += deltaTime;
            Vector2 drift = currentMps * deltaTime;
            for (int i = 0; i < count; i++) positions[Index(i)] += drift;

            bow = bowPosition;
            stern = sternPosition;
            speed = forwardSpeedMps;
            wash = propellerWash;
            hasPose = true;

            if (count > 0 &&
                (sternPosition - positions[newest]).magnitude < SampleSpacingM &&
                clock - times[newest] < MaxSampleIntervalS)
                return;

            newest = (newest + 1) % HistoryCapacity;
            positions[newest] = sternPosition;
            times[newest] = clock;
            speeds[newest] = forwardSpeedMps;
            washes[newest] = propellerWash;
            count = Mathf.Min(count + 1, HistoryCapacity);
        }

        // points: xz, distance behind the bow along the wake, age in seconds.
        // info: forward speed through water, propeller wash. Returns the packed count.
        public int Write(float hullLengthM, Vector4[] points, Vector4[] info, out Vector4 bounds)
        {
            bounds = Vector4.zero;
            if (!hasPose) return 0;

            points[0] = new Vector4(bow.x, bow.y, 0f, 0f);
            info[0] = new Vector4(speed, 0f, 0f, 0f);
            points[1] = new Vector4(stern.x, stern.y, hullLengthM, 0f);
            info[1] = new Vector4(speed, wash, 0f, 0f);

            Vector2 min = Vector2.Min(bow, stern);
            Vector2 max = Vector2.Max(bow, stern);
            Vector2 previous = stern;
            float distance = hullLengthM;
            for (int i = 0; i < count; i++)
            {
                int index = Index(i);
                Vector2 position = positions[index];
                float age = clock - times[index];
                // Waves already emitted keep travelling as if the vessel held its speed, so a
                // stopped vessel's wake keeps spreading instead of freezing in place.
                distance = Mathf.Max(distance + (position - previous).magnitude,
                    hullLengthM + Mathf.Max(0f, speeds[index]) * age);
                points[i + 2] = new Vector4(position.x, position.y, distance, age);
                info[i + 2] = new Vector4(speeds[index], washes[index], 0f, 0f);
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position);
                previous = position;
            }

            bounds = new Vector4(min.x, min.y, max.x, max.y);
            return count + 2;
        }

        private int Index(int age)
        {
            return (newest - age + HistoryCapacity) % HistoryCapacity;
        }
    }
}
