using System.Collections.Generic;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Publishes the vessel track to RiverWater.shader, which draws the Kelvin wake, bow wave
    // and propeller wash. Presentation only: nothing here feeds back into the physics.
    [RequireComponent(typeof(ShipPhysicsController))]
    public sealed class ShipWakeController : MonoBehaviour
    {
        // Estimated visual scale, not a validated wave height model.
        [SerializeField, Range(0f, 0.2f)] private float waveAmplitude = 0.07f;
        [SerializeField, Range(1, 4)] private int waterMeshDivisions = 3;

        private static readonly int PointsId = Shader.PropertyToID("_WakePoints");
        private static readonly int InfoId = Shader.PropertyToID("_WakeInfo");
        private static readonly int CountId = Shader.PropertyToID("_WakeCount");
        private static readonly int BoundsId = Shader.PropertyToID("_WakeBounds");
        private static readonly int ShipId = Shader.PropertyToID("_WakeShip");
        private static readonly int HullId = Shader.PropertyToID("_WakeHull");
        private static readonly int AmplitudeId = Shader.PropertyToID("_WakeAmplitude");

        private readonly ShipWakeTrack track = new ShipWakeTrack();
        private readonly Vector4[] points = new Vector4[ShipWakeTrack.Capacity];
        private readonly Vector4[] info = new Vector4[ShipWakeTrack.Capacity];
        private readonly List<(MeshFilter filter, Mesh original, Mesh refined)> refinedWater =
            new List<(MeshFilter, Mesh, Mesh)>();
        private ShipPhysicsController ship;

        private void Awake()
        {
            ship = GetComponent<ShipPhysicsController>();
        }

        private void Start()
        {
            RefineWaterMeshes();
            if (GetComponent<RiverShoreProfile>() == null) gameObject.AddComponent<RiverShoreProfile>();
        }

        private void LateUpdate()
        {
            if (ship == null || ship.Data == null) return;

            Shader.SetGlobalVectorArray("_PreviousWakePoints", points);
            Shader.SetGlobalVectorArray("_PreviousWakeInfo", info);
            Shader.SetGlobalFloat("_PreviousWakeCount", Shader.GetGlobalFloat(CountId));
            Shader.SetGlobalVector("_PreviousWakeBounds", Shader.GetGlobalVector(BoundsId));
            Shader.SetGlobalVector("_PreviousWakeShip", Shader.GetGlobalVector(ShipId));
            Shader.SetGlobalVector("_PreviousWakeHull", Shader.GetGlobalVector(HullId));
            Shader.SetGlobalFloat("_PreviousWakeAmplitude", Shader.GetGlobalFloat(AmplitudeId));

            Vector3 heading = transform.forward;
            Vector2 forward = new Vector2(heading.x, heading.z);
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            float length = ship.Data.dimensions.lengthOverallM;
            float beam = ship.Data.dimensions.beamOverallM;
            Vector2 center = new Vector2(transform.position.x, transform.position.z);
            Vector3 water = ship.RelativeWaterVelocity;
            float speed = Vector2.Dot(new Vector2(water.x, water.z), forward);
            float wash = Mathf.Abs(ship.ActualThrottle);
            Vector3 current = ship.EffectiveCurrentMps;

            track.Record(center + forward * (length * 0.5f), center - forward * (length * 0.5f),
                speed, wash, new Vector2(current.x, current.z), Time.deltaTime);
            int count = track.Write(length, points, info, out Vector4 bounds);

            Shader.SetGlobalVectorArray(PointsId, points);
            Shader.SetGlobalVectorArray(InfoId, info);
            Shader.SetGlobalFloat(CountId, count);
            Shader.SetGlobalVector(BoundsId, bounds);
            Shader.SetGlobalVector(ShipId, new Vector4(center.x, center.y, forward.x, forward.y));
            Shader.SetGlobalVector(HullId, new Vector4(length * 0.5f, beam * 0.5f, speed, wash));
            Shader.SetGlobalFloat(AmplitudeId, waveAmplitude);
        }

        // The stored river meshes are too coarse to displace waves a few metres long, so the
        // vertex density is raised for this session only.
        private void RefineWaterMeshes()
        {
            if (waterMeshDivisions <= 1) return;
            foreach (RiverPlanarReflection water in FindObjectsByType<RiverPlanarReflection>())
            {
                MeshFilter filter = water.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Mesh refined = WaterMeshRefiner.Subdivide(filter.sharedMesh, waterMeshDivisions);
                if (refined == null) continue;
                refinedWater.Add((filter, filter.sharedMesh, refined));
                filter.sharedMesh = refined;
            }
        }

        private void OnDisable()
        {
            track.Clear();
            Shader.SetGlobalFloat(CountId, 0f);
            Shader.SetGlobalFloat("_PreviousWakeCount", 0f);
        }

        private void OnDestroy()
        {
            foreach ((MeshFilter filter, Mesh original, Mesh refined) in refinedWater)
            {
                if (filter != null && filter.sharedMesh == refined) filter.sharedMesh = original;
                Destroy(refined);
            }
            refinedWater.Clear();
        }
    }
}
