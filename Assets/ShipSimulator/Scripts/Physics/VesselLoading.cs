using System;
using System.IO;
using UnityEngine;

namespace ShipSimulator.Physics
{
    [Serializable]
    public sealed class VesselLoading
    {
        public bool enabled;
        public Vector3 lightshipCentreLocalM;
        public float propellerAxisHeightAboveKeelM;
        public float maximumTransferSpeedMps;
        public bool estimated;
        public LoadCompartment[] compartments;
    }

    [Serializable]
    public sealed class LoadCompartment
    {
        public string id;
        public string kind;
        public float capacityKg;
        public float initialKg;
        public float transferKgPerSecond;
        public Vector3 centreLocalM;
        public Vector3 sizeM;
    }

    [Serializable]
    public sealed class LoadingState
    {
        public bool present;
        public string[] compartmentIds;
        public float[] massKg;
        public float[] commands;

        public LoadingState Copy() => new LoadingState
        {
            present = present, compartmentIds = (string[])compartmentIds.Clone(),
            massKg = (float[])massKg.Clone(), commands = (float[])commands.Clone()
        };

        public void Validate()
        {
            if (!present || compartmentIds == null || massKg == null || commands == null || massKg.Length == 0 ||
                massKg.Length > 32 || massKg.Length != commands.Length || massKg.Length != compartmentIds.Length)
                throw new InvalidDataException("Invalid loading state arrays.");
            for (int i = 0; i < massKg.Length; i++)
                if (string.IsNullOrWhiteSpace(compartmentIds[i]) || !Finite(massKg[i]) || massKg[i] < 0 ||
                    !Finite(commands[i]) || Mathf.Abs(commands[i]) > 1)
                    throw new InvalidDataException("Invalid compartment mass or transfer command.");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class LoadingModel
    {
        public static bool Supported(VesselData data) => data.loading != null && data.loading.enabled;
        public static LoadingState Initial(VesselData data, float fraction = 1)
        {
            var state = new LoadingState
            {
                present = true,
                compartmentIds = new string[data.loading.compartments.Length],
                massKg = new float[data.loading.compartments.Length],
                commands = new float[data.loading.compartments.Length]
            };
            for (int i = 0; i < state.massKg.Length; i++)
            {
                state.compartmentIds[i] = data.loading.compartments[i].id;
                state.massKg[i] = data.loading.compartments[i].initialKg * Mathf.Clamp01(fraction);
            }
            return state;
        }

        public static float Payload(LoadingState state)
        {
            float mass = 0;
            foreach (float value in state.massKg) mass += value;
            return mass;
        }

        public static float Budget(VesselData data) => Mathf.Min(data.massProperties.deadweightKg,
            data.massProperties.loadedMassKg - data.massProperties.lightshipMassKg);

        public static void Validate(VesselData data, LoadingState state)
        {
            state.Validate();
            if (!Supported(data) || state.massKg.Length != data.loading.compartments.Length)
                throw new InvalidDataException("Loading state does not match this vessel.");
            for (int i = 0; i < state.massKg.Length; i++)
                if (state.compartmentIds[i] != data.loading.compartments[i].id ||
                    state.massKg[i] > data.loading.compartments[i].capacityKg + .1f)
                    throw new InvalidDataException("Compartment identity changed or capacity exceeded.");
            if (Payload(state) > Budget(data) + .5f)
                throw new InvalidDataException("Cargo, ballast and stores exceed the shared deadweight budget.");
        }

        // Withdraw first, then admit only what the shared displacement budget permits.
        public static bool Step(VesselData data, LoadingState state, float dt, bool cargoAccessible)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return false;
            bool changed = false;
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < state.massKg.Length; i++)
            {
                LoadCompartment c = data.loading.compartments[i];
                float command = state.commands[i];
                if ((command < 0) != (pass == 0) || command == 0 || (c.kind == "cargo" && !cargoAccessible)) continue;
                float delta = command * c.transferKgPerSecond * dt;
                if (delta > 0) delta = Mathf.Min(delta, Mathf.Max(0, Budget(data) - Payload(state)));
                float next = Mathf.Clamp(state.massKg[i] + delta, 0, c.capacityKg);
                changed |= next != state.massKg[i];
                state.massKg[i] = next;
            }
            return changed;
        }

        public static void MassProperties(VesselData data, LoadingState state, out Vector3 centre, out Vector3 inertia)
        {
            float light = data.massProperties.lightshipMassKg;
            centre = light * data.loading.lightshipCentreLocalM;
            for (int i = 0; i < state.massKg.Length; i++)
                centre += state.massKg[i] * data.loading.compartments[i].centreLocalM;
            centre /= light + Payload(state);
            VesselMassProperties m = data.massProperties;
            inertia = light * new Vector3(m.pitchGyrationRadiusM * m.pitchGyrationRadiusM,
                m.yawGyrationRadiusM * m.yawGyrationRadiusM, m.rollGyrationRadiusM * m.rollGyrationRadiusM);
            inertia += Parallel(light, data.loading.lightshipCentreLocalM - centre);
            for (int i = 0; i < state.massKg.Length; i++)
            {
                LoadCompartment c = data.loading.compartments[i];
                Vector3 s = c.sizeM;
                inertia += state.massKg[i] / 12 * new Vector3(s.y * s.y + s.z * s.z,
                    s.x * s.x + s.z * s.z, s.x * s.x + s.y * s.y);
                inertia += Parallel(state.massKg[i], c.centreLocalM - centre);
            }
        }

        private static Vector3 Parallel(float mass, Vector3 d) => mass *
            new Vector3(d.y * d.y + d.z * d.z, d.x * d.x + d.z * d.z, d.x * d.x + d.y * d.y);
    }
}
