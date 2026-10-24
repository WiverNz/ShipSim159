using UnityEngine;
using UnityEngine.Audio;

namespace ShipSimulator.Audio
{
    // A fixed set of sources for one-shot events. A new event takes a free source or the one that started
    // longest ago, so a burst of events can never exceed the pool or allocate.
    public sealed class OneShotPool
    {
        private readonly AudioSource[] sources;
        private readonly float[] started;

        public int Size => sources.Length;
        public int Played { get; private set; }

        public OneShotPool(Transform parent, int size, float minDistance, int priority, AudioMixerGroup group)
        {
            sources = new AudioSource[size];
            started = new float[size];
            for (int i = 0; i < size; i++)
            {
                var holder = new GameObject("OneShot_" + i) { hideFlags = HideFlags.DontSave };
                holder.transform.SetParent(parent, false);
                AudioSource source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = minDistance;
                source.maxDistance = 1500f;
                source.dopplerLevel = 0f;
                source.priority = priority;
                source.outputAudioMixerGroup = group;
                sources[i] = source;
                started[i] = float.NegativeInfinity;
            }
        }

        public int Active
        {
            get
            {
                int count = 0;
                foreach (AudioSource source in sources) if (source != null && source.isPlaying) count++;
                return count;
            }
        }

        // Plays at a point that moves with the vessel; now is real time, for choosing the oldest voice.
        public void Play(AudioClip clip, Transform at, Vector3 localOffset, AudioMixerGroup group, float volume, float pitch, float now)
        {
            if (clip == null || at == null) return;
            int chosen = -1;
            float oldest = float.PositiveInfinity;
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                if (!sources[i].isPlaying) { chosen = i; break; }
                if (started[i] < oldest) { oldest = started[i]; chosen = i; }
            }
            if (chosen < 0) return;
            AudioSource source = sources[chosen];
            source.Stop();
            source.transform.SetParent(at, false);
            source.transform.localPosition = localOffset;
            source.clip = clip;
            source.outputAudioMixerGroup = group;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.Play();
            started[chosen] = now;
            Played++;
        }

        public void Dispose()
        {
            foreach (AudioSource source in sources) if (source != null) Object.Destroy(source.gameObject);
        }

        public void StopAll()
        {
            foreach (AudioSource source in sources) if (source != null) source.Stop();
        }
    }
}
