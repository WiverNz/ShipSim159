using UnityEngine;
using UnityEngine.Audio;

namespace ShipSimulator.Audio
{
    public enum AudioBus { Engines, Water, Wind, Machinery, Signals }

    // The project's AudioMixer (Resources/Audio/ShipSimMixer, built by ShipAudioBuilder): one group per bus with a
    // user volume, and a low-pass on the first four that the wheelhouse muffling drives. Master volume and mute
    // stay on AudioListener.volume, which every source shares, mixer or not.
    public static class AudioMix
    {
        public const string ResourcePath = "Audio/ShipSimMixer";
        public const int BusCount = 5;
        private const float OpenCutoffHz = 22000f;
        private static readonly string[] Names = { "Engines", "Water", "Wind", "Machinery", "Signals" };
        // Estimated wheelhouse insulation, not an acoustic model of the 507B superstructure: engines arrive through
        // the structure with their low end intact, outside sounds lose their highs and some level.
        private static readonly float[] InteriorCutoffHz = { 1800f, 700f, 500f, 1200f, OpenCutoffHz };
        private static readonly float[] InteriorDb = { -3f, -10f, -14f, -8f, 0f };

        private static AudioMixer mixer;
        private static AudioMixerGroup[] groups;
        private static bool loaded;
        private static readonly float[] userVolume = { 1f, 1f, 1f, 1f, 1f };
        private static readonly float[] appliedDb = new float[BusCount];
        private static readonly float[] appliedCutoff = new float[BusCount];
        private static float interior;

        public static AudioMixer Mixer
        {
            get
            {
                Load();
                return mixer;
            }
        }

        public static float Interior => interior;
        public static string BusName(AudioBus bus) => Names[(int)bus];
        public static float UserVolume(AudioBus bus) => userVolume[(int)bus];

        public static AudioMixerGroup Group(AudioBus bus)
        {
            Load();
            return groups != null ? groups[(int)bus] : null;
        }

        public static void SetUserVolume(AudioBus bus, float linear)
        {
            userVolume[(int)bus] = Mathf.Clamp01(linear);
            Apply(true);
        }

        // 0 outside, 1 with the listener in the wheelhouse.
        public static void SetInterior(float blend)
        {
            interior = Mathf.Clamp01(blend);
            Apply(false);
        }

        public static void Apply(bool force)
        {
            Load();
            if (mixer == null) return;
            for (int i = 0; i < BusCount; i++)
            {
                float db = ToDb(userVolume[i]) + InteriorDb[i] * interior;
                float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoffHz), Mathf.Log(InteriorCutoffHz[i]), interior));
                if (force || Mathf.Abs(db - appliedDb[i]) > 0.05f)
                {
                    mixer.SetFloat(Names[i] + "Volume", db);
                    appliedDb[i] = db;
                }
                if (i < 4 && (force || Mathf.Abs(cutoff - appliedCutoff[i]) > 1f))
                {
                    mixer.SetFloat(Names[i] + "Cutoff", cutoff);
                    appliedCutoff[i] = cutoff;
                }
            }
        }

        public static float ToDb(float linear) => linear <= 1e-4f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(linear));

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            mixer = Resources.Load<AudioMixer>(ResourcePath);
            if (mixer == null) return;
            groups = new AudioMixerGroup[BusCount];
            for (int i = 0; i < BusCount; i++)
            {
                AudioMixerGroup[] found = mixer.FindMatchingGroups("Master/" + Names[i]);
                groups[i] = found.Length > 0 ? found[0] : null;
            }
        }
    }
}
