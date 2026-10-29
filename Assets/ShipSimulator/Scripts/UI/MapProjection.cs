using UnityEngine;

namespace ShipSimulator.UI
{
    public readonly struct MapProjection
    {
        public readonly Vector3 Center;
        public readonly float Heading, Scale;
        public readonly Vector2 Offset;
        public MapProjection(Vector3 center, float heading, float scale, Vector2 offset)
        { Center = center; Heading = heading; Scale = scale; Offset = offset; }
        public Vector2 Project(Vector3 world)
        {
            Vector3 local = Quaternion.Euler(0, -Heading, 0) * (world - Center);
            return new Vector2(local.x, local.z) * Scale + Offset;
        }
        public Vector3 Unproject(Vector2 pixel)
        {
            Vector2 delta = (pixel - Offset) / Scale;
            return Center + Quaternion.Euler(0, Heading, 0) * new Vector3(delta.x, 0, delta.y);
        }
        public float IconAngle(float heading) => Mathf.DeltaAngle(heading, Heading);
        public static float PredictionStep(float speed, int steps, float range) =>
            steps <= 0 || speed <= .1f ? 2.5f : Mathf.Min(2.5f, range / (speed * steps));
    }
}
