using System;
using System.IO;
using UnityEngine;

namespace ShipSimulator.Persistence
{
    [Serializable]
    public sealed class VoyageSave
    {
        public int version = 1;
        public string scene;
        public string savedUtc;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float throttle;
        public float actualThrottle;
        public float rudder;
        public float rudderAngle;
        // Per-engine state; older saves without it restore throttle and actualThrottle on every engine.
        public float[] engineCommands;
        public float[] shaftRps;
        public float simulationScale = 1f;
        public bool night;
        public float windDirection;
        public float windSpeed;
        public float rain;
        public float fog;
        public float waterLevel;
        public float discharge = 1f;
        public CameraSave camera = new CameraSave();
        public MissionSave mission = new MissionSave();
        public GroundingSave grounding = new GroundingSave();

        public static bool IsVoyageScene(string name) =>
            name == "RiverTrainingScene" || name == "GorodetsTrainingScene";

        public void Validate()
        {
            if (version != 1) throw new InvalidDataException("This save uses an unsupported version.");
            if (!IsVoyageScene(scene)) throw new InvalidDataException("The saved scenario is unavailable.");
            if (!DateTime.TryParse(savedUtc, out _)) throw new InvalidDataException("The save date is missing or invalid.");
            if (camera == null || mission == null || grounding == null)
                throw new InvalidDataException("The save is incomplete.");
            Check(position.x, -1000000f, 1000000f); Check(position.y, -1000000f, 1000000f); Check(position.z, -1000000f, 1000000f);
            Check(velocity.magnitude, 0f, 1000f); Check(angularVelocity.magnitude, 0f, 100f);
            Check(rotation.x, -1f, 1f); Check(rotation.y, -1f, 1f);
            Check(rotation.z, -1f, 1f); Check(rotation.w, -1f, 1f);
            float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            Check(norm, 0.99f, 1.01f);
            Check(throttle, -1f, 1f); Check(actualThrottle, -1f, 1f);
            Check(rudder, -1f, 1f); Check(rudderAngle, -90f, 90f);
            if (engineCommands != null)
            {
                Check(engineCommands.Length, 0f, 8f);
                foreach (float command in engineCommands) Check(command, -1f, 1f);
            }
            if (shaftRps != null)
            {
                Check(shaftRps.Length, 0f, 8f);
                foreach (float rps in shaftRps) Check(rps, -100f, 100f);
            }
            if (simulationScale != 1f && simulationScale != 2f && simulationScale != 4f)
                throw new InvalidDataException("Invalid simulation speed.");
            Check(windDirection, 0f, 360f); Check(windSpeed, 0f, 100f);
            Check(rain, 0f, 1f); Check(fog, 0f, 1f);
            Check(waterLevel, -100f, 100f); Check(discharge, 0f, 1.5f);
            Check(camera.view, 0f, 8f); Check(camera.yaw, -1000000f, 1000000f);
            Check(camera.pitch, 0f, 90f); Check(camera.distance, 1f, 1000f);
            Check(mission.phase, 0f, 8f); Check(mission.score, 0f, 100f);
            Check(mission.outsideSeconds, 0f, float.MaxValue); Check(mission.overspeedSeconds, 0f, float.MaxValue);
            Check(mission.leadingIntegral, 0f, float.MaxValue); Check(mission.controlPenalty, 0f, float.MaxValue);
            Check(mission.previousRudder, -1f, 1f);
            Check(grounding.state, 0f, 4f); Check(grounding.damage, 0f, float.MaxValue);
        }

        private static void Check(float value, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new InvalidDataException($"The save contains an invalid state value ({value}; expected {min} to {max}).");
        }
    }

    [Serializable]
    public sealed class CameraSave
    {
        public int view;
        public float yaw;
        public float pitch = 30f;
        public float distance = 80f;
    }

    [Serializable]
    public sealed class MissionSave
    {
        public int phase;
        public float score = 100f;
        public float outsideSeconds;
        public float overspeedSeconds;
        public float leadingIntegral;
        public float previousRudder;
        public float controlPenalty;
    }

    [Serializable]
    public sealed class GroundingSave
    {
        public int state;
        public float damage;
    }

    public static class VoyageSaveStore
    {
        public static string DefaultPath => Path.Combine(Application.persistentDataPath, "voyage.json");

        public static VoyageSave Read(string path)
        {
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("The save file is too large.");
            VoyageSave save = JsonUtility.FromJson<VoyageSave>(File.ReadAllText(path));
            if (save == null) throw new InvalidDataException("The save is empty.");
            save.Validate();
            return save;
        }

        public static void Write(string path, VoyageSave save)
        {
            save.Validate();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            // Replace only after the complete new file has been written.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
