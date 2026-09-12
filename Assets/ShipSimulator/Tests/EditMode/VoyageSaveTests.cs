using System;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class VoyageSaveTests
    {
        private string directory;
        private string path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ShipSimSaveTests", Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "voyage.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void Save_RoundTripsMotionEnvironmentAndMission_AndKeepsPreviousBackup()
        {
            VoyageSave original = ValidSave();
            original.position = new Vector3(12, 3, 456);
            original.velocity = new Vector3(0.3f, 0, 2);
            original.actualThrottle = 0.42f;
            original.rudderAngle = -12;
            original.night = true;
            original.rain = 0.7f;
            original.mission.phase = 4;
            original.mission.score = 82;
            original.mission.outsideSeconds = 15;
            original.grounding.damage = 2;
            VoyageSaveStore.Write(path, original);
            VoyageSave loaded = VoyageSaveStore.Read(path);
            Assert.That(loaded.position, Is.EqualTo(original.position));
            Assert.That(loaded.velocity, Is.EqualTo(original.velocity));
            Assert.That(loaded.actualThrottle, Is.EqualTo(0.42f));
            Assert.That(loaded.rudderAngle, Is.EqualTo(-12));
            Assert.That(loaded.night, Is.True);
            Assert.That(loaded.rain, Is.EqualTo(0.7f));
            Assert.That(loaded.mission.score, Is.EqualTo(82));
            Assert.That(loaded.mission.outsideSeconds, Is.EqualTo(15));
            Assert.That(loaded.grounding.damage, Is.EqualTo(2));
            loaded.position.z = 999;
            VoyageSaveStore.Write(path, loaded);
            Assert.That(VoyageSaveStore.Read(path).position.z, Is.EqualTo(999));
            Assert.That(VoyageSaveStore.Read(path + ".bak").position.z, Is.EqualTo(456));
        }

        [Test]
        public void InvalidSave_DoesNotReplaceExistingVoyage()
        {
            VoyageSave save = ValidSave();
            VoyageSaveStore.Write(path, save);
            save.position.x = float.NaN;
            Assert.Throws<InvalidDataException>(() => VoyageSaveStore.Write(path, save));
            Assert.That(VoyageSaveStore.Read(path).position.x, Is.Zero);
        }

        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"version\":99}")]
        public void CorruptOrUnsupportedFile_IsRejected(string json)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, json);
            Assert.Catch(() => VoyageSaveStore.Read(path));
        }

        [Test]
        public void InvalidSceneOrRotation_IsRejected()
        {
            VoyageSave save = ValidSave();
            save.scene = "SampleScene";
            Assert.Throws<InvalidDataException>(() => save.Validate());
            save.scene = "RiverTrainingScene";
            save.rotation = new Quaternion(0, 0, 0, 0);
            Assert.Throws<InvalidDataException>(() => save.Validate());
        }

        private static VoyageSave ValidSave() => new VoyageSave
        { scene = "GorodetsTrainingScene", savedUtc = DateTime.UtcNow.ToString("O") };
    }
}
