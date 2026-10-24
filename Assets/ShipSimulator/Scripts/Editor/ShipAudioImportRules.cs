using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    // Import settings for the vessel audio under Assets/ShipSimulator/Audio, set from the file name so a reimport
    // always lands on them. Loops (Loop_*) stay Vorbis-compressed in memory: about a tenth of their PCM size,
    // decoded per playing voice, which the soundscape keeps to a few tens. One-shots (Shot_*) use ADPCM, which
    // decodes far more cheaply for short overlapping events. Everything preloads with the scene, so the first event
    // never waits on disk; no clip is long enough to justify streaming. The pipeline writes positional layers in
    // mono already; Force To Mono stays off because Unity also normalizes when it downmixes, which would undo the
    // authored relative levels. The steady wind is played non-positionally and keeps its stereo width.
    public sealed class ShipAudioImportRules : AssetPostprocessor
    {
        public const string Root = "Assets/ShipSimulator/Audio/";
        public const float LoopQuality = 0.5f;

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool loop = name.StartsWith("Loop_");
            importer.forceToMono = false;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.preloadAudioData = true;
            settings.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            settings.quality = loop ? LoopQuality : 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
