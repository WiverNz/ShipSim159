using UnityEngine;
using UnityEngine.Audio;

namespace ShipSimulator.Audio
{
    // One looping layer on its own AudioSource. Volume and pitch glide towards the targets the soundscape sets from
    // the simulation, so a step in state never becomes a step in sound. A silent voice is stopped, which frees its
    // real voice; it restarts at a seeded random point of the loop so two engines never play in phase.
    public sealed class LoopVoice
    {
        private const float SilentVolume = 0.0005f;
        private readonly AudioSource source;
        private readonly System.Random random;
        private float targetVolume, targetPitch = 1f;

        public float Volume { get; private set; }
        public float Pitch { get; private set; } = 1f;
        public float TargetVolume => targetVolume;
        public AudioSource Source => source;
        public bool Playing => source != null && source.isPlaying;

        public LoopVoice(Transform parent, string name, AudioClip clip, AudioMixerGroup group, float minDistance,
            float spread, int priority, bool spatial = true)
        {
            var holder = new GameObject(name) { hideFlags = HideFlags.DontSave };
            holder.transform.SetParent(parent, false);
            source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group;
            source.priority = priority;
            source.volume = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = 1500f;
            source.spread = spread;
            // The cameras ride with the vessel, so Doppler here would only report camera cuts and orbiting.
            source.dopplerLevel = 0f;
            random = new System.Random(name.GetHashCode());
        }

        public void Set(float volume, float pitch)
        {
            targetVolume = Mathf.Clamp(float.IsFinite(volume) ? volume : 0f, 0f, 1f);
            targetPitch = Mathf.Clamp(float.IsFinite(pitch) ? pitch : 1f, 0.05f, 3f);
        }

        // Real seconds since the last tick; attack and release are the volume time constants.
        public void Tick(float dt, float attack, float release, float pitchTime)
        {
            if (source == null || source.clip == null) return;
            float tau = targetVolume > Volume ? attack : release;
            Volume += (targetVolume - Volume) * Approach(dt, tau);
            Pitch += (targetPitch - Pitch) * Approach(dt, pitchTime);
            if (Volume < SilentVolume && targetVolume <= 0f)
            {
                Volume = 0f;
                if (source.isPlaying) source.Stop();
            }
            else if (!source.isPlaying)
            {
                source.timeSamples = random.Next(0, Mathf.Max(1, source.clip.samples));
                source.Play();
            }
            source.volume = Volume;
            source.pitch = Pitch;
        }

        // After a restore the pitch takes the restored state at once; the level glides to it from where it was,
        // so a playing layer is never cut off.
        public void Resync()
        {
            Pitch = targetPitch;
            if (source != null) source.pitch = Pitch;
        }

        // Disabled with its vessel: silent and stopped.
        public void Silence()
        {
            Volume = 0f;
            if (source == null) return;
            source.volume = 0f;
            if (source.isPlaying) source.Stop();
        }

        public void Dispose()
        {
            if (source != null) Object.Destroy(source.gameObject);
        }

        private static float Approach(float dt, float tau) => tau <= 0f ? 1f : 1f - Mathf.Exp(-dt / tau);
    }
}
