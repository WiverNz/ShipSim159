using System;
using UnityEngine;

namespace ShipSimulator.Audio
{
    // One vessel's sound choices: which clips of the shared library it plays (Assets/ShipSimulator/Audio/Library),
    // how loud and how far they carry, and the listening zone of its wheelhouse. A layer without a clip is off.
    // What the vessel is (cylinders, gearing, blades, machinery drives) comes from its data, never from here; the
    // reference rates below describe the clips. Built by ShipAudioBuilder; levels are estimated listening choices.
    [CreateAssetMenu(menuName = "Ship Simulator/Vessel Sound Profile")]
    public sealed class VesselSoundProfile : ScriptableObject
    {
        [Serializable]
        public sealed class Layer
        {
            public AudioClip clip;
            [Range(0, 1)] public float volume = 1f;
            [Min(0.1f)] public float minDistance = 10f;
            [Range(0, 360)] public float spread = 30f;
        }

        [Header("Main engines (one set per engine)")]
        public Layer firingLow = new Layer();
        public Layer firingHigh = new Layer();
        public Layer engineRoom = new Layer();
        public Layer engineExterior = new Layer();
        public Layer startingAirBrake = new Layer();
        public AudioClip startingAir;
        // The firing clips: firings per second at pitch 1, and the engine they were made for. They play only for an
        // engine of that cylinder count and cycle whose engine-to-shaft speed ratio is known.
        public float firingLowHz = 5f;
        public float firingHighHz = 15f;
        public int firingClipCylinders = 6;
        public int firingClipStrokes = 4;
        // The low firing layer hands over to the high one across this band of firing rate.
        public Vector2 firingCrossfadeHz = new Vector2(7.5f, 11.5f);

        [Header("Propellers and water")]
        public Layer propellerWash = new Layer();
        // Blade passes per second at pitch 1, and the blade count the clip was made for.
        public Layer bladeRate = new Layer();
        public float bladeClipHz = 20f;
        public int bladeClipBlades = 4;
        public Layer hullFlow = new Layer();
        public Layer hullLap = new Layer();
        public Layer bowWave = new Layer();
        // Speed through the water at which the hull flow layer is at full level.
        public float fullFlowSpeedMps = 5f;

        [Header("Wind (non-positional, around the listener)")]
        public Layer windSteady = new Layer();
        public Layer windStrong = new Layer();

        [Header("Deck machinery: drives named in the vessel data pick these")]
        public Layer hydraulicPump = new Layer();
        public Layer electricMotor = new Layer();
        public Layer coverRolling = new Layer();
        public AudioClip hydraulicStart, hydraulicStop, electricMotorStart, electricMotorStop;
        public AudioClip[] heavyImpact = Array.Empty<AudioClip>();
        public AudioClip[] metalClunk = Array.Empty<AudioClip>();

        [Header("Ground tackle")]
        public Layer chainHeave = new Layer();
        public Layer chainRun = new Layer();
        public Layer lineStrain = new Layer();
        public AudioClip[] lineCreak = Array.Empty<AudioClip>();
        public AudioClip lineParted, chainParted;
        // Links per second clanking in the heave clip, and the link pitch (about four chain diameters).
        public float recordedLinksPerSecond = 1.05f;
        public float linkPitchDiameters = 4f;

        [Header("Wheelhouse (vessel-root coordinates); zero extents: no interior to listen from")]
        public Vector3 wheelhouseCentre;
        public Vector3 wheelhouseHalfExtents;

        [Header("One-shots")]
        [Range(0, 1)] public float eventVolume = 0.9f;
        [Min(1)] public int poolSize = 8;
    }
}
