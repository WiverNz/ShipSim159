#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShipSimulator.Tests
{
    // Fixed drift and manoeuvre cases for the 507B prefab, stepped by hand. Each run writes its tracks to
    // Logs/Part14/trajectory-latest.json, so the Part 14 report can compare the vessel with anchors stowed and
    // no lines against the track recorded before ground tackle existed.
    public sealed class GroundTackleTrajectoryTests
    {
        private const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";
        private const float Dt = 0.02f;
        private SimulationMode previousMode;
        private GameObject vessel;

        [Serializable]
        private sealed class Track
        {
            public string name;
            public List<Vector3> positions = new List<Vector3>();
            public List<float> headings = new List<float>();
            public Vector3 finalVelocity;
        }

        [Serializable]
        private sealed class Record
        {
            public string recordedUtc;
            public List<Track> tracks = new List<Track>();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (vessel != null) Object.Destroy(vessel);
            UnityEngine.Physics.simulationMode = previousMode;
            yield return null;
        }

        [UnityTest]
        public IEnumerator DriftAndManoeuvreTracks_AreRecorded()
        {
            var record = new Record { recordedUtc = DateTime.UtcNow.ToString("O") };
            record.tracks.Add(Run("drift, anchors stowed", false, ship =>
            {
                ship.SetAmbientCurrent(new Vector3(0, 0, 0.5f));
                ship.SetWindVelocity(new Vector3(-12, 0, 0));
            }));
            record.tracks.Add(Run("drift, anchors let go", true, ship =>
            {
                ship.SetAmbientCurrent(new Vector3(0, 0, 0.5f));
                ship.SetWindVelocity(new Vector3(-12, 0, 0));
            }));
            record.tracks.Add(Run("half ahead, half starboard helm", false, ship =>
            {
                ship.SetAmbientCurrent(Vector3.zero);
                ship.SetThrottleCommand(0.65f);
                ship.SetRudderCommand(0.5f);
            }));
            Directory.CreateDirectory("Logs/Part14");
            File.WriteAllText("Logs/Part14/trajectory-latest.json", JsonUtility.ToJson(record, true));
            foreach (Track track in record.tracks)
            {
                Assert.That(float.IsFinite(track.positions[^1].x) && float.IsFinite(track.positions[^1].z), track.name);
                Debug.Log($"TRAJECTORY|{track.name}|{track.positions[^1].x:F4}|{track.positions[^1].z:F4}|{track.headings[^1]:F4}");
            }
            yield return null;
        }

        private Track Run(string name, bool letGo, Action<ShipPhysicsController> orders)
        {
            if (vessel != null) Object.DestroyImmediate(vessel);
            vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var ship = vessel.GetComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            ship.SetDepthProvider(_ => 8f);
            ship.Grounding?.Configure(ship, null);
            ship.RestoreVoyage(new ShipSimulator.Persistence.VoyageSave { position = Vector3.zero });
            for (int i = 0; i < 1500; i++) Step(ship);
            orders(ship);
            if (letGo) ship.SetAnchorsReleased(true);
            var track = new Track { name = name };
            VesselMechanisms rig = ship.Mechanisms;
            for (int i = 1; i <= 15000; i++)
            {
                Step(ship);
                if (letGo && rig != null) rig.Advance(Dt, 0, 0, 0);
                if (i % 500 != 0) continue;
                track.positions.Add(ship.Body.position);
                track.headings.Add(ship.Body.rotation.eulerAngles.y);
            }
            track.finalVelocity = ship.Body.linearVelocity;
            return track;
        }

        private static void Step(ShipPhysicsController ship)
        {
            ship.Simulate(Dt);
            UnityEngine.Physics.Simulate(Dt);
        }
    }
}
#endif
