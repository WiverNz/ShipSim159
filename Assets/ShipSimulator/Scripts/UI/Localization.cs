using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ShipSimulator.UI
{
    public static class Localization
    {
        [Serializable] public sealed class Entry { public string key; public string value; }
        [Serializable] public sealed class Catalogue { public Entry[] entries; }
        public static readonly string[] Languages = { "en", "ru", "de", "es" };
        public static readonly string[] NativeNames = { "English", "Русский", "Deutsch", "Español" };
        public static event Action Changed;
        private static readonly Dictionary<string, Dictionary<string, string>> tables = new();
        private static readonly Dictionary<string, string> sourceKeys = new();
        private static readonly HashSet<string> missing = new();
        public static string Language => VoyageSettings.Language;
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language);

        private static void EnsureLoaded()
        {
            if (tables.Count != 0) return;
            foreach (string language in Languages)
            {
                var table = new Dictionary<string, string>();
                var asset = Resources.Load<TextAsset>("Localization/" + language);
                if (asset != null)
                    foreach (Entry entry in JsonUtility.FromJson<Catalogue>(asset.text).entries)
                    {
                        table.Add(entry.key, entry.value);
                        if (language == "en") sourceKeys.TryAdd(entry.value, entry.key);
                    }
                tables.Add(language, table);
            }
        }

        public static string Get(string key, params object[] arguments)
        {
            EnsureLoaded();
            if (!tables[Language].TryGetValue(key, out string value))
            {
                if (missing.Add(Language + ":" + key) && (Application.isEditor || Debug.isDebugBuild))
                    Debug.LogWarning("Missing translation: " + Language + ":" + key);
                if (!tables["en"].TryGetValue(key, out value)) return "[" + key + "]";
            }
            return arguments.Length == 0 ? value : string.Format(Culture, value, arguments);
        }

        // Adapts canonical English authored in existing scenes and UI builders to stable catalogue keys.
        // Never pass an already formatted sentence here; dynamic values use FormatSource.
        public static string Source(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            EnsureLoaded();
            return sourceKeys.TryGetValue(source, out string key) ? Get(key) : source;
        }

        public static string FormatSource(string template, params object[] arguments)
        {
            EnsureLoaded();
            return sourceKeys.TryGetValue(template, out string key)
                ? Get(key, arguments) : string.Format(Culture, template, arguments);
        }

        internal static void NotifyChanged() => Changed?.Invoke();
    }
}
