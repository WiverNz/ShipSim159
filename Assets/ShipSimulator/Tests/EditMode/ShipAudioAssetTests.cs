using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShipSimulator.Audio;
using ShipSimulator.Editor;
using ShipSimulator.UI;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class ShipAudioAssetTests
    {
        private static readonly string Project = Path.GetDirectoryName(Application.dataPath);
        private static readonly string ManifestPath = Path.Combine(Project, "Art/Source/Audio/audio-manifest.json");
        private static readonly string SourcesPath = Path.Combine(Project, "Art/Source/Audio/sources.json");
        private static readonly string Library = Path.Combine(Project, ShipAudioBuilder.LibraryFolder);
        private static readonly string[] VesselIds = { "volgodon-507b", "volgobalt-295ar", "volgoneft-1577", "meteor-342u", "luch-14352" };

        [Serializable] private sealed class Manifest { public Entry[] files; }

        [Serializable]
        private sealed class Entry
        {
            public string file, purpose, kind, status, edits;
            public int sample_rate;
            public Source[] sources;
        }

        [Serializable]
        private sealed class Source
        {
            public string id, title, author, url, license, license_url, preview_url, sha256;
            public bool attribution_required;
        }

        private static Manifest Load() => JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));

        [Test]
        public void EveryGameFile_HasAManifestEntryWithLicenseAndStatus_AndViceVersa()
        {
            Manifest manifest = Load();
            var onDisk = Directory.GetFiles(Library, "*.wav", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(Library, f).Replace('\\', '/')).OrderBy(n => n).ToArray();
            var listed = manifest.files.Select(e => e.file).OrderBy(n => n).ToArray();
            Assert.That(listed, Is.EqualTo(onDisk), "Manifest and game folder list the same files.");
            string registry = File.ReadAllText(SourcesPath);
            foreach (Entry entry in manifest.files)
            {
                Assert.That(entry.purpose, Is.Not.Empty, entry.file);
                Assert.That(entry.edits, Is.Not.Empty, entry.file);
                Assert.That(entry.status, Does.StartWith("analogue recording").Or.StartWith("synthesized approximation")
                    .Or.StartWith("recording of the phenomenon"), entry.file + ": no recording here is of the 507B itself.");
                Assert.That(entry.status, Does.Not.Contain("507B recording"));
                if (entry.status.StartsWith("synthesized")) continue;
                Assert.That(entry.sources, Is.Not.Empty, entry.file);
                foreach (Source source in entry.sources)
                {
                    Assert.That(source.license_url, Is.EqualTo("https://creativecommons.org/publicdomain/zero/1.0/"), entry.file);
                    Assert.That(source.author, Is.Not.Empty);
                    Assert.That(source.url, Does.StartWith("https://freesound.org/people/" + source.author + "/sounds/" + source.id));
                    Assert.That(source.sha256, Has.Length.EqualTo(64));
                    Assert.That(registry, Does.Contain(source.sha256), "The source registry pins the same file.");
                    Assert.That(File.Exists(Path.Combine(Project, "Art/Source/Audio/Sources/freesound-" + source.id + ".ogg")),
                        Is.True, "The source recording is kept.");
                    Assert.That(File.Exists(Path.Combine(Project, "Art/Source/Audio/LicenseSnapshots/freesound-" + source.id + ".html")),
                        Is.True, "The license page as checked is kept.");
                }
            }
        }

        // Read straight from the WAV files, independent of the import settings.
        [Test]
        public void GameFiles_AreCleanMonoWithHeadroom_AndLoopsAreSeamless()
        {
            foreach (Entry entry in Load().files)
            {
                float[][] channels = ReadWav(Path.Combine(Library, entry.file), out int rate);
                Assert.That(rate, Is.EqualTo(entry.sample_rate), entry.file);
                Assert.That(channels.Length, Is.EqualTo(entry.file == "Wind/Loop_WindSteady.wav" ? 2 : 1), entry.file + " channels");
                foreach (float[] samples in channels)
                {
                    float peak = samples.Max(Mathf.Abs), mean = samples.Average();
                    Assert.That(peak, Is.LessThanOrEqualTo(Mathf.Pow(10f, -0.95f / 20f)), entry.file + ": at least 1 dB below full scale.");
                    Assert.That(peak, Is.GreaterThan(0.05f), entry.file + " is not silent.");
                    Assert.That(Mathf.Abs(mean), Is.LessThan(1e-3f), entry.file + ": no DC offset.");
                    if (entry.kind != "loop") continue;
                    var steps = new float[samples.Length - 1];
                    for (int i = 1; i < samples.Length; i++) steps[i - 1] = Mathf.Abs(samples[i] - samples[i - 1]);
                    Array.Sort(steps);
                    float typical = steps[(int)(0.99f * (steps.Length - 1))];
                    Assert.That(Mathf.Abs(samples[0] - samples[samples.Length - 1]), Is.LessThanOrEqualTo(1.5f * typical),
                        entry.file + ": the loop seam is no larger than the signal's own steps.");
                }
            }
        }

        [Test]
        public void ImportSettings_SuitEachClip()
        {
            foreach (Entry entry in Load().files)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(ShipAudioBuilder.LibraryFolder + "/" + entry.file);
                Assert.That(importer, Is.Not.Null, entry.file);
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.CompressedInMemory), entry.file);
                Assert.That(settings.preloadAudioData, Is.True, entry.file);
                Assert.That(settings.compressionFormat, Is.EqualTo(entry.kind == "loop" ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM),
                    entry.file);
                Assert.That(importer.forceToMono, Is.False, entry.file + ": downmixing would also normalize.");
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ShipAudioBuilder.LibraryFolder + "/" + entry.file);
                Assert.That(clip, Is.Not.Null, entry.file + " imports.");
            }
        }

        [Test]
        public void Mixer_HasEveryBusAndItsExposedParameters()
        {
            Assert.DoesNotThrow(ShipAudioBuilder.VerifyMixer);
            Assert.That(Resources.Load(AudioMix.ResourcePath), Is.Not.Null, "Loadable at runtime.");
        }

        [Test]
        public void Library_HasNoDuplicateFiles_AndNoClipOutsideIt()
        {
            string audio = Path.GetFullPath(Path.Combine(Project, ShipAudioBuilder.AudioRoot));
            string library = Path.GetFullPath(Library) + Path.DirectorySeparatorChar;
            string[] all = Directory.GetFiles(audio, "*.wav", SearchOption.AllDirectories).Select(Path.GetFullPath).ToArray();
            Assert.That(all, Has.Length.EqualTo(36));
            Assert.That(all.All(f => f.StartsWith(library)), Is.True, "Every shared clip lives in the library.");
            Assert.That(Directory.Exists(Path.Combine(audio, "VolgoDon507B")), Is.False, "The old vessel folder is gone.");
            var hashes = new Dictionary<string, string>();
            using (var sha = System.Security.Cryptography.SHA256.Create())
                foreach (string file in all)
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file)));
                    Assert.That(hashes.ContainsKey(hash), Is.False, file + " duplicates " + (hashes.TryGetValue(hash, out string other) ? other : ""));
                    hashes[hash] = file;
                }
        }

        [Test]
        public void EveryVessel_HasAProfile_ThatUsesOnlyLibraryClips()
        {
            foreach (string id in VesselIds)
            {
                var profile = AssetDatabase.LoadAssetAtPath<VesselSoundProfile>(ShipAudioBuilder.ProfilePath(id));
                Assert.That(profile, Is.Not.Null, id);
                foreach (AudioClip clip in Clips(profile))
                    Assert.That(AssetDatabase.GetAssetPath(clip), Does.StartWith(ShipAudioBuilder.LibraryFolder + "/"), id + ": " + clip.name);
                Assert.That(profile.windSteady.clip, Is.Not.Null, id + " hears the wind.");
                Assert.That(profile.engineRoom.clip, Is.Not.Null, id + " hears its diesels.");
                bool volgoDon = id == ShipAudioBuilder.VolgoDonId;
                Assert.That(profile.firingLow.clip != null, Is.EqualTo(volgoDon), id + ": firing clips only where the engine is known.");
                Assert.That(profile.chainRun.clip != null && profile.hydraulicPump.clip != null, Is.EqualTo(volgoDon), id + ": machinery only where modelled.");
                Assert.That(profile.wheelhouseHalfExtents.sqrMagnitude > 0f, Is.EqualTo(volgoDon), id + ": wheelhouse filter only with an interior.");
            }
            var volgo = AssetDatabase.LoadAssetAtPath<VesselSoundProfile>(ShipAudioBuilder.ProfilePath(ShipAudioBuilder.VolgoDonId));
            var shots = new List<AudioClip>
            {
                volgo.startingAir, volgo.hydraulicStart, volgo.hydraulicStop, volgo.electricMotorStart, volgo.electricMotorStop,
                volgo.lineParted, volgo.chainParted
            };
            shots.AddRange(volgo.heavyImpact);
            shots.AddRange(volgo.metalClunk);
            shots.AddRange(volgo.lineCreak);
            Assert.That(shots, Has.None.Null);
            Assert.That(Clips(volgo).Distinct().Count(), Is.EqualTo(36), "The 507B uses every library clip.");
        }

        [Test]
        public void EveryVesselPrefab_CarriesItsOwnProfile_AndOnlyThe507BHasAudioPoints()
        {
            foreach (string id in VesselIds)
            {
                var prefab = ShipSimulator.Physics.VesselCatalogue.Load().Find(id).prefab;
                var sound = prefab.GetComponent<VesselSoundscape>();
                Assert.That(sound, Is.Not.Null, id);
                Assert.That(AssetDatabase.GetAssetPath(sound.Profile), Is.EqualTo(ShipAudioBuilder.ProfilePath(id)), id);
                Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty, id + ": sources are made at runtime, not saved.");
            }
            var volgoDon = AssetDatabase.LoadAssetAtPath<GameObject>(VolgoDonMechanismIntegration.PrefabPath);
            Transform points = volgoDon.GetComponentsInChildren<Transform>(true).First(t => t.name == VesselSoundscape.PointsName);
            foreach (string name in new[] { "Engine_Port", "Engine_Starboard", "Exhaust_Port", "Exhaust_Starboard", "Propeller_Port_Audio",
                "Propeller_Starboard_Audio", "Water_Bow", "Hull_Port", "Hull_Starboard", "HatchMachinery_Aft", "HatchMachinery_Fore",
                "Windlass_Port", "Windlass_Starboard", "Capstan_Stern" })
                Assert.That(points.Find(name), Is.Not.Null, name);
        }

        // Only facts with a source go into the data; every other vessel leaves them unset and its layers off.
        [Test]
        public void EquipmentFacts_AreSetOnlyWhereEstablished()
        {
            string folder = Path.Combine(Application.dataPath, "ShipSimulator/Data/Vessels");
            foreach (string file in new[] { "VolgoDon507B", "VolgoBalt295AR", "Volgoneft1577", "Meteor342U", "Luch14352" })
            {
                var data = JsonUtility.FromJson<ShipSimulator.Physics.VesselData>(File.ReadAllText(Path.Combine(folder, file + ".json")));
                Assert.That(ShipSimulator.Physics.VesselDataValidator.TryValidate(data, out string error), Is.True, error);
                bool volgoDon = file == "VolgoDon507B";
                Assert.That(data.engine.cylinders, Is.EqualTo(volgoDon ? 6 : 0), file);
                Assert.That(data.engine.strokesPerCycle, Is.EqualTo(volgoDon ? 4 : 0), file);
                Assert.That(data.engine.engineRpmPerShaftRpm, Is.EqualTo(volgoDon ? 1f : 0f), file);
                Assert.That(data.engine.directReversingAirStart, Is.EqualTo(volgoDon), file);
                Assert.That(data.propeller.bladeCount, Is.EqualTo(volgoDon ? 4 : 0), file);
            }
            var bad = JsonUtility.FromJson<ShipSimulator.Physics.VesselData>(File.ReadAllText(Path.Combine(folder, "VolgoDon507B.json")));
            bad.engine.strokesPerCycle = 3;
            Assert.That(ShipSimulator.Physics.VesselDataValidator.TryValidate(bad, out _), Is.False);
            bad.engine.strokesPerCycle = 4;
            bad.groundTackle.mooringWinchDrive = "steam";
            Assert.That(ShipSimulator.Physics.VesselDataValidator.TryValidate(bad, out _), Is.False);
        }

        private static IEnumerable<AudioClip> Clips(VesselSoundProfile profile)
        {
            var layers = new[]
            {
                profile.firingLow, profile.firingHigh, profile.engineRoom, profile.engineExterior, profile.startingAirBrake,
                profile.propellerWash, profile.bladeRate, profile.hullFlow, profile.hullLap, profile.bowWave, profile.windSteady,
                profile.windStrong, profile.hydraulicPump, profile.electricMotor, profile.coverRolling, profile.chainHeave,
                profile.chainRun, profile.lineStrain
            };
            var clips = layers.Select(l => l.clip).ToList();
            clips.AddRange(new[] { profile.startingAir, profile.hydraulicStart, profile.hydraulicStop, profile.electricMotorStart,
                profile.electricMotorStop, profile.lineParted, profile.chainParted });
            clips.AddRange(profile.heavyImpact);
            clips.AddRange(profile.metalClunk);
            clips.AddRange(profile.lineCreak);
            return clips.Where(c => c != null);
        }

        [Test]
        public void Settings_MuteAndBusVolumes_PersistAndApply()
        {
            float volume = VoyageSettings.Volume, water = VoyageSettings.BusVolume(AudioBus.Water);
            bool muted = VoyageSettings.Muted;
            float listener = AudioListener.volume;
            try
            {
                VoyageSettings.SetVolume(0.6f);
                VoyageSettings.SetMuted(true);
                Assert.That(AudioListener.volume, Is.Zero);
                VoyageSettings.SetMuted(false);
                Assert.That(AudioListener.volume, Is.EqualTo(0.6f).Within(1e-5f), "Unmuting restores the master volume.");
                VoyageSettings.SetBusVolume(AudioBus.Water, 0.25f);
                Assert.That(VoyageSettings.BusVolume(AudioBus.Water), Is.EqualTo(0.25f).Within(1e-5f));
                Assert.That(AudioMix.UserVolume(AudioBus.Water), Is.EqualTo(0.25f).Within(1e-5f));
            }
            finally
            {
                VoyageSettings.SetVolume(volume);
                VoyageSettings.SetMuted(muted);
                VoyageSettings.SetBusVolume(AudioBus.Water, water);
                AudioListener.volume = listener;
            }
        }

        // PCM 16-bit WAV, as the Part 15 pipeline writes it.
        private static float[][] ReadWav(string path, out int rate)
        {
            byte[] data = File.ReadAllBytes(path);
            Assert.That(System.Text.Encoding.ASCII.GetString(data, 0, 4), Is.EqualTo("RIFF"), path);
            int position = 12, channels = 0, bits = 0;
            rate = 0;
            while (position + 8 <= data.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(data, position, 4);
                int size = BitConverter.ToInt32(data, position + 4);
                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(data, position + 10);
                    rate = BitConverter.ToInt32(data, position + 12);
                    bits = BitConverter.ToInt16(data, position + 22);
                }
                else if (id == "data")
                {
                    Assert.That(bits, Is.EqualTo(16), path);
                    int frames = size / (2 * channels);
                    var result = new float[channels][];
                    for (int c = 0; c < channels; c++) result[c] = new float[frames];
                    for (int i = 0; i < frames; i++)
                        for (int c = 0; c < channels; c++)
                            result[c][i] = BitConverter.ToInt16(data, position + 8 + 2 * (i * channels + c)) / 32768f;
                    return result;
                }
                position += 8 + size + (size & 1);
            }
            throw new InvalidDataException("No data chunk in " + path);
        }
    }
}
