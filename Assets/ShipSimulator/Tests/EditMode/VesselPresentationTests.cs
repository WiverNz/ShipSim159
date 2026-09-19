using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    // What the pilot sees of a vessel: the wave it raises, the state of its lift and the radar prediction.
    // These are estimated presentation models, not validated wave heights or stopping distances.
    public sealed class VesselPresentationTests
    {
        private const float Reference = 0.00255f;

        private static float Scale(string file, float speedMps)
        {
            VesselData data = VesselFixtures.Load(file);
            return ShipWakeController.AmplitudeScale(
                data.massProperties.loadedMassKg / data.hydrostatics.waterDensityKgM3,
                data.dimensions.lengthOverallM,
                SupportModel.Immersion(data.support, speedMps),
                Reference);
        }

        [Test]
        public void WakeAmplitude_IsUnchangedForTheVesselItWasTunedOn()
        {
            Assert.That(Scale(VesselFixtures.VolgoDonFile, 5f), Is.EqualTo(1f).Within(0.05f));
            // The other loaded cargo ships stay near it: shorter and fuller for their length means a
            // slightly larger wave at the same speed, which is the behaviour being modelled.
            Assert.That(Scale(VesselFixtures.VolgoneftFile, 5f), Is.InRange(0.85f, 1.35f));
            Assert.That(Scale(VesselFixtures.VolgoBaltFile, 5f), Is.InRange(0.85f, 1.35f));
        }

        [Test]
        public void WakeAmplitude_FallsWhenAFastCraftRisesOntoItsFoils()
        {
            float hullborne = Scale(VesselFixtures.MeteorFile, 2f);
            float foilborne = Scale(VesselFixtures.MeteorFile, 18f);

            Assert.That(hullborne, Is.LessThan(Scale(VesselFixtures.VolgoDonFile, 5f)),
                "A 53 t craft does not raise a loaded cargo ship's wave.");
            Assert.That(foilborne, Is.LessThan(0.25f * hullborne));
            // The shader raises the crest by amplitude * speed squared / g, so at 18 m/s the tuned 0.07
            // alone would build a 2.3 m wall of water behind a passenger craft.
            Assert.That(0.07f * foilborne * 18f * 18f / 9.81f, Is.LessThan(0.6f));
        }

        [Test]
        public void WakeAmplitude_StaysWithinBoundsForUnknownHullForms()
        {
            Assert.That(ShipWakeController.AmplitudeScale(1e9f, 100f, 1f, Reference), Is.EqualTo(2f));
            Assert.That(ShipWakeController.AmplitudeScale(1f, 100f, 1f, Reference), Is.EqualTo(0.2f));
            Assert.That(ShipWakeController.AmplitudeScale(6750f, 0f, 0.5f, Reference), Is.EqualTo(0.5f));
        }

        [Test]
        public void SupportStatus_NamesTheStateOnlyForCraftThatLift()
        {
            VesselData cargo = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            VesselSupport meteor = VesselFixtures.Load(VesselFixtures.MeteorFile).support;
            VesselSupport luch = VesselFixtures.Load(VesselFixtures.LuchFile).support;

            Assert.That(ShipTelemetryUI.FormatSupportStatus(cargo.support, 5f), Is.Empty);
            Assert.That(ShipTelemetryUI.FormatSupportStatus(meteor, 2f), Does.Contain("HULLBORNE"));
            Assert.That(ShipTelemetryUI.FormatSupportStatus(meteor, 18f), Does.Contain("FOILBORNE 100%"));
            Assert.That(ShipTelemetryUI.FormatSupportStatus(luch, 11f), Does.Contain("CUSHIONBORNE"));
            Assert.That(ShipTelemetryUI.FormatSupportStatus(
                meteor, 0.5f * (meteor.takeoffSpeedMps + meteor.fullSupportSpeedMps)),
                Does.Contain("FOILBORNE 50%"));
        }

        [Test]
        public void SingleEngineHelp_DoesNotNameSeparatePortAndStarboardEngines()
        {
            string controls = ShipTelemetryUI.FormatEngineControls(1);
            Assert.That(controls, Does.Contain("TELEGRAPH"));
            Assert.That(controls, Does.Not.Contain("PORT").And.Not.Contain("STBD"));
            Assert.That(ShipTelemetryUI.FormatEngineControls(2), Does.Contain("PORT").And.Contain("STBD"));
        }

        [Test]
        public void RadarPrediction_KeepsThePredictedPathInsideTheRadarRange()
        {
            const int steps = 10;
            float cargo = ShipTelemetryUI.RadarPredictionStepSeconds(5f, steps);
            float fast = ShipTelemetryUI.RadarPredictionStepSeconds(18f, steps);

            Assert.That(cargo, Is.EqualTo(2.5f), "A river cargo ship keeps the full 25 s horizon.");
            Assert.That(fast * steps * 18f * 0.82f - 82f, Is.LessThanOrEqualTo(142.001f),
                "The final point clears the actual radar viewport edge, including an 8 px margin.");
            Assert.That(ShipTelemetryUI.RadarPredictionStepSeconds(0f, steps), Is.EqualTo(2.5f));
            Assert.That(ShipTelemetryUI.RadarPredictionStepSeconds(18f, 0), Is.EqualTo(2.5f));
        }
    }
}
