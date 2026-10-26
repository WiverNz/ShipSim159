using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ShipSimulator.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Builds the project AudioMixer and one sound profile per catalogue vessel over the shared clip library, then
    // installs the soundscape on every vessel prefab. Unity has no public API for authoring mixers, so the mixer is
    // made through the editor's own internal controller classes by reflection; a missing member fails the build
    // with the names that do exist.
    public static class ShipAudioBuilder
    {
        public const string MixerPath = "Assets/ShipSimulator/Resources/" + AudioMix.ResourcePath + ".mixer";
        public const string AudioRoot = "Assets/ShipSimulator/Audio";
        public const string LibraryFolder = AudioRoot + "/Library";
        public const string ProfilesFolder = AudioRoot + "/Profiles";
        public const string VolgoDonId = "volgodon-507b";
        public const string LuchId = "luch-14352";
        private static readonly string[] FilteredBuses = { "Engines", "Water", "Wind", "Machinery" };

        public static string ProfilePath(string vesselId) => $"{ProfilesFolder}/{vesselId}.asset";

        [MenuItem("Ship Simulator/Build Ship Audio")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            // Applies ShipAudioImportRules even to clips imported before the rules compiled.
            AssetDatabase.ImportAsset(LibraryFolder, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            BuildMixer();
            BuildProfiles();
            InstallAll();
            AssetDatabase.SaveAssets();
        }

        // Batch entry: exits 0 with SHIP_AUDIO_BUILD|PASS.
        public static void Run()
        {
            try
            {
                Build();
                Debug.Log("SHIP_AUDIO_BUILD|PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("SHIP_AUDIO_BUILD|FAIL|" + error);
                EditorApplication.Exit(1);
            }
        }

        // Adds or reconfigures the soundscape of a vessel root from the profile named by its VesselLayout id.
        // Vessel builders call this before saving their prefab, so a rebuild keeps the sound.
        public static bool InstallSoundscape(GameObject root)
        {
            string id = root.GetComponent<ShipSimulator.Visuals.VesselLayout>()?.VesselId;
            var profile = string.IsNullOrEmpty(id) ? null : AssetDatabase.LoadAssetAtPath<VesselSoundProfile>(ProfilePath(id));
            if (profile == null) return false;
            var soundscape = root.GetComponent<VesselSoundscape>() ?? root.AddComponent<VesselSoundscape>();
            soundscape.Configure(profile);
            return true;
        }

        public static void InstallAll()
        {
            foreach (ShipSimulator.Physics.VesselCatalogue.Entry entry in ShipSimulator.Physics.VesselCatalogue.Load().Entries)
            {
                string path = AssetDatabase.GetAssetPath(entry.prefab);
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (entry.id == VolgoDonId) VolgoDonAudioIntegration.AddPoints(contents);
                    if (!InstallSoundscape(contents)) throw new InvalidOperationException("No sound profile for " + entry.id);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        // ---- Mixer ----

        public static void BuildMixer()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath) != null)
            {
                VerifyMixer();
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type controllerType = Need(editor.GetType("UnityEditor.Audio.AudioMixerController"), "AudioMixerController");
            Type effectType = Need(editor.GetType("UnityEditor.Audio.AudioMixerEffectController"), "AudioMixerEffectController");
            Type groupPathType = Need(editor.GetType("UnityEditor.Audio.AudioGroupParameterPath"), "AudioGroupParameterPath");
            Type effectPathType = Need(editor.GetType("UnityEditor.Audio.AudioEffectParameterPath"), "AudioEffectParameterPath");

            object controller = Call(controllerType, null, "CreateMixerControllerAtPath", MixerPath);
            object master = Get(controller, "masterGroup");
            object snapshot = Get(controller, "TargetSnapshot");
            foreach (string bus in Enum.GetNames(typeof(AudioBus)))
            {
                object group = Call(controllerType, controller, "CreateNewGroup", bus, false);
                Call(controllerType, controller, "AddChildToParent", group, master);
                GUID volume = (GUID)Call(group.GetType(), group, "GetGUIDForVolume");
                Expose(controller, controllerType, Activator.CreateInstance(groupPathType, group, volume), volume, bus + "Volume");
                if (!FilteredBuses.Contains(bus)) continue;
                object effect = Activator.CreateInstance(effectType, "Lowpass Simple");
                Call(effectType, effect, "PreallocateGUIDs");
                AssetDatabase.AddObjectToAsset((Object)effect, (Object)controller);
                object[] effects = (object[])Get(group, "effects");
                Call(group.GetType(), group, "InsertEffect", effect, effects.Length);
                GUID cutoff = (GUID)Call(effectType, effect, "GetGUIDForParameter", "Cutoff freq");
                Call(effectType, effect, "SetValueForParameter", controller, snapshot, "Cutoff freq", 22000f);
                Expose(controller, controllerType, Activator.CreateInstance(effectPathType, group, effect, cutoff), cutoff, bus + "Cutoff");
            }
            EditorUtility.SetDirty((Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);
            VerifyMixer();
        }

        public static void VerifyMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) throw new InvalidOperationException("No mixer at " + MixerPath);
            foreach (string bus in Enum.GetNames(typeof(AudioBus)))
            {
                if (mixer.FindMatchingGroups("Master/" + bus).Length != 1) throw new InvalidOperationException("Mixer group missing: " + bus);
                if (!mixer.GetFloat(bus + "Volume", out _)) throw new InvalidOperationException("Exposed parameter missing: " + bus + "Volume");
                if (FilteredBuses.Contains(bus) && !mixer.GetFloat(bus + "Cutoff", out _))
                    throw new InvalidOperationException("Exposed parameter missing: " + bus + "Cutoff");
            }
        }

        // The controller names a new exposed parameter itself and keeps the list sorted; rename it by its GUID.
        private static void Expose(object controller, Type controllerType, object path, GUID guid, string name)
        {
            Call(controllerType, controller, "AddExposedParameter", path);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo property = Need(controllerType.GetProperty("exposedParameters", flags), "exposedParameters");
            Array parameters = (Array)property.GetValue(controller);
            for (int i = 0; i < parameters.Length; i++)
            {
                object parameter = parameters.GetValue(i);
                FieldInfo id = Need(parameter.GetType().GetField("guid", flags), "ExposedAudioParameter.guid");
                if (!guid.Equals(id.GetValue(parameter))) continue;
                Need(parameter.GetType().GetField("name", flags), "ExposedAudioParameter.name").SetValue(parameter, name);
                parameters.SetValue(parameter, i);
                property.SetValue(controller, parameters);
                return;
            }
            throw new InvalidOperationException("Exposed parameter not found after adding it: " + name);
        }

        private static object Call(Type type, object target, string name, params object[] args)
        {
            MethodInfo method = Find(type, name, args) ?? throw Missing(type, name);
            return method.Invoke(target, args);
        }

        private static MethodInfo Find(Type type, string name, object[] args) =>
            type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length &&
                    m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i]) ||
                        p.ParameterType == args[i].GetType()).All(ok => ok));

        private static object Get(object target, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo property = target.GetType().GetProperty(name, flags);
            if (property != null) return property.GetValue(target);
            FieldInfo field = target.GetType().GetField(name, flags);
            if (field != null) return field.GetValue(target);
            throw Missing(target.GetType(), name);
        }

        private static T Need<T>(T value, string what) where T : class =>
            value ?? throw new MissingMemberException("Unity editor audio API changed: " + what + " not found.");

        private static Exception Missing(Type type, string name) =>
            new MissingMemberException($"Unity editor audio API changed: {type.Name}.{name} not found. Members: " +
                string.Join(", ", type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Select(m => m.Name).Distinct().OrderBy(n => n)));

        // ---- Profiles ----

        // Every vessel shares the library clips; each profile only chooses among them. Layers for equipment a vessel
        // does not have, or whose facts its data does not establish, get no clip here or are switched off by the
        // soundscape from the data. Levels and distances are estimated listening choices, repeated by a rebuild.
        public static void BuildProfiles()
        {
            Directory.CreateDirectory(ProfilesFolder);
            foreach (ShipSimulator.Physics.VesselCatalogue.Entry entry in ShipSimulator.Physics.VesselCatalogue.Load().Entries)
            {
                VesselSoundProfile profile = Profile(entry.id);
                Shared(profile);
                if (entry.id == VolgoDonId) VolgoDon(profile);
                // The real 14352 is driven by a waterjet (Luch14352_Sources.md); the wash clip carries the recorded
                // ferry's propeller and engine tones, so it would sound like the propeller the model only stands in for.
                if (entry.id == LuchId || entry.id == "serpukhov-r96" || entry.id == "serpukhov-mn52") profile.propellerWash.clip = null;
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
        }

        private static VesselSoundProfile Profile(string id)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VesselSoundProfile>(ProfilePath(id));
            if (profile != null)
            {
                // A moved or renamed asset keeps its old object name; the soundscape and checks know it by its id.
                if (profile.name != id) profile.name = id;
                return profile;
            }
            profile = ScriptableObject.CreateInstance<VesselSoundProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath(id));
            return profile;
        }

        // Diesel engines, propellers, a hull in the water and wind: every catalogue vessel has these.
        private static void Shared(VesselSoundProfile profile)
        {
            Layer(profile.engineRoom, "Engines/Loop_DieselEngineRoom", 0.5f, 12f, 60f);
            Layer(profile.engineExterior, "Engines/Loop_DieselExterior", 0.7f, 18f, 40f);
            Layer(profile.propellerWash, "Propulsion/Loop_PropellerWash", 0.75f, 14f, 90f);
            Layer(profile.hullFlow, "Water/Loop_HullFlow", 0.6f, 25f, 140f);
            Layer(profile.hullLap, "Water/Loop_HullLap", 0.35f, 15f, 120f);
            Layer(profile.bowWave, "Water/Loop_BowWave", 0.6f, 15f, 90f);
            Layer(profile.windSteady, "Wind/Loop_WindSteady", 0.45f, 1f, 0f);
            Layer(profile.windStrong, "Wind/Loop_WindStrong", 0.5f, 1f, 0f);
        }

        // The 507B's own equipment: a six-cylinder four-stroke direct-reversing engine, four-bladed propellers, the
        // hatch covers, the ground tackle, and the wheelhouse its navigator view sits in.
        private static void VolgoDon(VesselSoundProfile profile)
        {
            Layer(profile.firingLow, "Engines/Loop_DieselFiring_6Cyl4Stroke_5Hz", 0.55f, 14f, 60f);
            Layer(profile.firingHigh, "Engines/Loop_DieselFiring_6Cyl4Stroke_15Hz", 0.55f, 14f, 60f);
            profile.firingLowHz = 5f;
            profile.firingHighHz = 15f;
            profile.firingClipCylinders = 6;
            profile.firingClipStrokes = 4;
            profile.firingCrossfadeHz = new Vector2(7.5f, 11.5f);
            Layer(profile.startingAirBrake, "Engines/Loop_StartingAirBrake", 0.5f, 8f, 30f);
            profile.startingAir = Clip("Engines/Shot_StartingAir");
            Layer(profile.bladeRate, "Propulsion/Loop_BladeRate_4Blade_20Hz", 0.35f, 10f, 60f);
            profile.bladeClipHz = 20f;
            profile.bladeClipBlades = 4;
            Layer(profile.hydraulicPump, "Machinery/Loop_HydraulicPump", 0.6f, 8f, 60f);
            Layer(profile.electricMotor, "Machinery/Loop_ElectricMotor", 0.6f, 8f, 40f);
            Layer(profile.coverRolling, "Machinery/Loop_SteelWheelsRolling", 0.6f, 10f, 90f);
            profile.hydraulicStart = Clip("Machinery/Shot_HydraulicStart");
            profile.hydraulicStop = Clip("Machinery/Shot_HydraulicStop");
            profile.electricMotorStart = Clip("Machinery/Shot_ElectricMotorStart");
            profile.electricMotorStop = Clip("Machinery/Shot_ElectricMotorStop");
            profile.heavyImpact = Enumerable.Range(1, 4).Select(i => Clip("Impacts/Shot_HeavySteelImpact" + i)).ToArray();
            profile.metalClunk = Enumerable.Range(1, 4).Select(i => Clip("Impacts/Shot_MetalClunk" + i)).ToArray();
            Layer(profile.chainHeave, "Anchor/Loop_ChainOverGypsy", 0.8f, 10f, 40f);
            Layer(profile.chainRun, "Anchor/Loop_ChainRunning", 0.8f, 12f, 40f);
            profile.chainParted = Clip("Anchor/Shot_ChainParted");
            Layer(profile.lineStrain, "Mooring/Loop_LineStrain", 0.6f, 6f, 30f);
            profile.lineCreak = Enumerable.Range(1, 3).Select(i => Clip("Mooring/Shot_LineCreak" + i)).ToArray();
            profile.lineParted = Clip("Mooring/Shot_LineParted");
            profile.wheelhouseCentre = new Vector3(0f, 9.6f, -43.5f);
            profile.wheelhouseHalfExtents = new Vector3(6f, 1.6f, 3.2f);
        }

        private static void Layer(VesselSoundProfile.Layer layer, string clip, float volume, float minDistance, float spread)
        {
            layer.clip = Clip(clip);
            layer.volume = volume;
            layer.minDistance = minDistance;
            layer.spread = spread;
        }

        // A library clip by its path under Library, without extension.
        public static AudioClip Clip(string name) =>
            AssetDatabase.LoadAssetAtPath<AudioClip>(LibraryFolder + "/" + name + ".wav") ??
            throw new FileNotFoundException("Missing library clip " + LibraryFolder + "/" + name + ".wav; see Art/Source/Audio/build_ship_audio.py.");
    }
}
