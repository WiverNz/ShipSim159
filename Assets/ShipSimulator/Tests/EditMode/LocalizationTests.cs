using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class LocalizationTests
    {
        private string original;
        [SetUp] public void Setup() => original = VoyageSettings.Language;
        [TearDown] public void Cleanup() => VoyageSettings.SetLanguage(original);

        [Test]
        public void PanelSettings_HaveTranslatedCaptionsInEveryLanguage()
        {
            var english = Read("en");
            foreach (string caption in VoyageSettings.HudPanelLabels)
                Assert.That(english.Values, Does.Contain(caption));
            foreach (string language in Localization.Languages)
            {
                VoyageSettings.SetLanguage(language);
                foreach (string caption in VoyageSettings.HudPanelLabels)
                    Assert.That(Localization.Source(caption), Is.Not.Empty);
            }
        }

        [Test]
        public void CataloguesHaveMatchingKeysFormatsAndRichText()
        {
            var english = Read("en");
            Assert.That(english.Count, Is.GreaterThan(350));
            foreach (string language in Localization.Languages)
            {
                var table = Read(language);
                CollectionAssert.AreEquivalent(english.Keys, table.Keys, language);
                foreach (var entry in table)
                {
                    Assert.That(entry.Value, Is.Not.Empty, language + ":" + entry.Key);
                    CollectionAssert.AreEquivalent(Tokens(english[entry.Key], @"\{\d+[^}]*\}"),
                        Tokens(entry.Value, @"\{\d+[^}]*\}"), language + ":" + entry.Key);
                    CollectionAssert.AreEqual(Tokens(english[entry.Key], @"</?(?:color|size|b)[^>]*>"),
                        Tokens(entry.Value, @"</?(?:color|size|b)[^>]*>"), language + ":" + entry.Key);
                }
            }
        }

        [Test]
        public void SelectionPersistsAndInvalidPreferenceFallsBackToEnglish()
        {
            foreach (string language in Localization.Languages)
            {
                VoyageSettings.SetLanguage(language);
                Assert.That(PlayerPrefs.GetString("ShipSim159.Settings.Language"), Is.EqualTo(language));
                Assert.That(VoyageSettings.Language, Is.EqualTo(language));
            }
            PlayerPrefs.SetString("ShipSim159.Settings.Language", "invalid");
            Assert.That(VoyageSettings.Language, Is.EqualTo("en"));
            VoyageSettings.SetLanguage("ru");
            VoyageSettings.SetLanguage("invalid");
            Assert.That(VoyageSettings.Language, Is.EqualTo("en"));
        }

        [Test]
        public void StoredMessageReformatsAfterLanguageChangesWithoutChangingArguments()
        {
            var message = new LocalizedMessage("Mission complete. Score {0:F0}/100", 87);
            VoyageSettings.SetLanguage("en");
            Assert.That(message.ToString(), Is.EqualTo("Mission complete. Score 87/100"));
            VoyageSettings.SetLanguage("ru");
            Assert.That(message.ToString(), Is.EqualTo("Рейс завершён. Баллы 87/100"));
            VoyageSettings.SetLanguage("de");
            Assert.That(message.ToString(), Does.Contain("Punkte 87/100"));
        }

        [Test]
        public void FormattingUsesChosenLanguageWithoutMutatingProcessCulture()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            VoyageSettings.SetLanguage("de");
            Assert.That(Localization.FormatSource("{0:0.0}", 1.5f), Is.EqualTo("1,5"));
            Assert.That(CultureInfo.CurrentCulture, Is.SameAs(before));
            Assert.That(Localization.Source("volgodon-507b"), Is.EqualTo("volgodon-507b"));
        }

        [Test]
        public void MissingKeyIsVisibleAndDiagnosed()
        {
            VoyageSettings.SetLanguage("en");
            string key = "test.missing." + Guid.NewGuid().ToString("N");
            LogAssert.Expect(LogType.Warning, "Missing translation: en:" + key);
            Assert.That(Localization.Get(key), Is.EqualTo("[" + key + "]"));
        }

        [Test]
        public void MissingTranslationUsesEnglishTemplateAndKeepsArguments()
        {
            VoyageSettings.SetLanguage("es");
            string key = "test.fallback." + Guid.NewGuid().ToString("N");
            Localization.Source("Language");
            var field = typeof(Localization).GetField("tables",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var tables = (Dictionary<string, Dictionary<string, string>>)field.GetValue(null);
            tables["en"].Add(key, "Mission complete. Score {0:F0}/100");
            try
            {
                LogAssert.Expect(LogType.Warning, "Missing translation: es:" + key);
                Assert.That(Localization.Get(key, 72), Is.EqualTo("Mission complete. Score 72/100"));
            }
            finally { tables["en"].Remove(key); }
        }

        [Test]
        public void ShippedFontContainsAllCatalogueCharacters()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string characters = string.Concat(Localization.Languages.SelectMany(l => Read(l).Values));
            characters = Regex.Replace(characters, "<[^>]+>", "");
            string unique = new string(characters.Where(c => !char.IsControl(c)).Distinct().ToArray());
            font.RequestCharactersInTexture(unique, 20, FontStyle.Normal);
            foreach (char character in unique)
                Assert.That(font.HasCharacter(character), Is.True, "Missing glyph U+" + ((int)character).ToString("X4"));
        }

        private static string[] Tokens(string text, string pattern) =>
            Regex.Matches(text, pattern).Cast<Match>().Select(m => m.Value).ToArray();
        private static Dictionary<string, string> Read(string language) =>
            JsonUtility.FromJson<Localization.Catalogue>(Resources.Load<TextAsset>("Localization/" + language).text)
                .entries.ToDictionary(e => e.key, e => e.value);
    }
}
