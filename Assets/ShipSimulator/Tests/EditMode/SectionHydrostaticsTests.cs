using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class SectionHydrostaticsTests
    {
        // Exact plane clipping of the closed Part 10 surface by part11_hydrostatics.py: draft, trim over 135 m,
        // volume m3, LCB m, KB above keel m, waterplane m2, transverse inertia m4.
        private static readonly float[][] MeshReference =
        {
            new[] { 1.0f, 0f, 1762.99f, 0.2092f, 0.5168f, 1879.3f, 39846f },
            new[] { 2.2f, 0f, 4089.96f, -0.232f, 1.1364f, 1992.1f, 42748f },
            new[] { 3.5f, 0f, 6761.58f, -0.9084f, 1.8161f, 2104.8f, 45330f },
            new[] { 2.2f, 1.5f, 4085.27f, 6.3469f, 1.1715f, 1972.4f, 42633f },
            new[] { 2.2f, -1.5f, 4135.09f, -7.0204f, 1.1862f, 2033.6f, 43068f },
            new[] { 3.5f, -0.75f, 6793.53f, -3.2773f, 1.8306f, 2103.1f, 45204f }
        };

        private static VesselData Data() => VesselFixtures.Load(VesselFixtures.VolgoDonFile);

        [Test]
        public void ColumnIntegration_AgreesWithExactMeshClippingWithinDeclaredTolerances()
        {
            var sections = new SectionHydrostatics(Data());
            foreach (float[] r in MeshReference)
            {
                HydrostaticState s = sections.Integrate(r[0], r[1] / 135f);
                string label = $"draft {r[0]} trim {r[1]}";
                Assert.That(s.VolumeM3, Is.EqualTo(r[2]).Within(0.005f * r[2]), label);
                Assert.That(s.LcbM, Is.EqualTo(r[3]).Within(0.02f), label);
                Assert.That(s.KbM, Is.EqualTo(r[4]).Within(0.02f), label);
                Assert.That(s.WaterplaneAreaM2, Is.EqualTo(r[5]).Within(0.01f * r[5]), label);
                Assert.That(s.TransverseInertiaM4, Is.EqualTo(r[6]).Within(0.02f * r[6]), label);
            }
        }

        [Test]
        public void ClosedSurface_CapsAtThePart10Volume()
        {
            var sections = new SectionHydrostatics(Data());
            Assert.That(sections.Capped.VolumeM3, Is.EqualTo(11013.69f).Within(0.005f * 11013.69f));
            Assert.That(sections.Capped.WaterplaneAreaM2, Is.Zero);
        }

        [Test]
        public void Equilibrium_DisplacesTheMassWithBuoyancyAndGravityOnOneVertical()
        {
            VesselData data = Data();
            foreach (LoadingState state in Cases(data))
            {
                VesselParameters p = VesselParameters.Create(data, state);
                HydrostaticState e = p.Equilibrium;
                Assert.That(e.VolumeM3 * p.Rho, Is.EqualTo(p.Mass).Within(1e-4f * p.Mass));
                float offset = e.LcbM - p.CentreOfGravityX + (e.KbM - p.CentreOfGravityAboveKeel) * p.TrimSlope;
                Assert.That(offset, Is.EqualTo(0f).Within(1e-3f));
                Assert.That(p.HydrostaticRange, Is.Not.EqualTo(HydrostaticRange.Unsolved));
            }
        }

        [Test]
        public void DefaultFullLoad_FloatsNearEvenKeelBelowTheSelectedWaterline()
        {
            VesselParameters p = VesselParameters.Create(Data());
            Assert.That(p.HydrostaticRange, Is.EqualTo(HydrostaticRange.Supported));
            // 6750 t needs less than the 6825 m3 under the 3.53 m line, so the hull rides slightly higher.
            Assert.That(p.Draft, Is.InRange(3.48f, 3.51f));
            Assert.That(p.TrimM, Is.EqualTo(0f).Within(0.05f));
            Assert.That(new HydrostaticsModel(p).MetacentricHeightM, Is.InRange(4f, 8f));
        }

        [Test]
        public void LongitudinalBias_TrimsTowardTheHeavierEnd()
        {
            VesselData data = Data();
            LoadingState fore = LoadingModel.Initial(data, 0), aft = LoadingModel.Initial(data, 0);
            fore.massKg[0] = aft.massKg[1] = 2400000;
            VesselParameters bow = VesselParameters.Create(data, fore), stern = VesselParameters.Create(data, aft);
            Assert.That(bow.TrimM, Is.GreaterThan(1f));
            Assert.That(stern.TrimM, Is.LessThan(-1f));
            Assert.That(bow.DraftAt(60f), Is.GreaterThan(bow.DraftAt(-60f)));
            Assert.That(bow.Mass, Is.EqualTo(stern.Mass));
        }

        [Test]
        public void OutOfTableConditions_StayFiniteAndAreFlagged()
        {
            var sections = new SectionHydrostatics(Data());
            Assert.That(sections.Solve(1.2f * sections.Capped.VolumeM3, 0f, 2.5f, 135f, out float draft, out float slope,
                out HydrostaticState state), Is.EqualTo(HydrostaticRange.Unsolved));
            Assert.That(draft, Is.EqualTo(sections.CapHeightM));
            Assert.That(state.VolumeM3, Is.LessThanOrEqualTo(sections.Capped.VolumeM3));
            foreach (float[] input in new[] { new[] { float.NaN, 0f }, new[] { 4000f, float.NaN },
                new[] { -5f, 0f }, new[] { 0f, 0f } })
            {
                Assert.That(sections.Solve(input[0], input[1], 3f, 135f, out draft, out slope, out state),
                    Is.EqualTo(HydrostaticRange.Unsolved));
                Assert.That(float.IsNaN(draft) || float.IsNaN(slope) || float.IsNaN(state.VolumeM3), Is.False);
            }
            Assert.That(sections.Solve(100f, 0f, 3f, 135f, out draft, out _, out state),
                Is.EqualTo(HydrostaticRange.OutsideDeclaredRange));
            Assert.That(draft, Is.InRange(0f, 0.6f));
            Assert.That(sections.Solve(1750f, 30f, 3.53f, 135f, out _, out slope, out _),
                Is.EqualTo(HydrostaticRange.OutsideDeclaredRange));
            Assert.That(slope * 135f, Is.GreaterThan(1.5f));
        }

        [Test]
        public void Lookup_IgnoresDryAndNonFiniteImmersionAndSaturatesAtTheCap()
        {
            var sections = new SectionHydrostatics(Data());
            int middle = sections.StationCount / 2;
            foreach (float h in new[] { -1f, 0f, float.NaN, float.NegativeInfinity })
            {
                sections.Lookup(middle, 0, h, out float area, out float moment, out float breadth);
                Assert.That(area + moment + breadth, Is.Zero);
            }
            sections.Lookup(middle, 0, sections.CapHeightM, out float capArea, out _, out _);
            sections.Lookup(middle, 0, 100f, out float drowned, out _, out float drownedBreadth);
            sections.Lookup(middle, 0, float.PositiveInfinity, out float infinite, out _, out _);
            Assert.That(drowned, Is.EqualTo(capArea));
            Assert.That(infinite, Is.EqualTo(capArea));
            Assert.That(drownedBreadth, Is.Zero);
        }

        [Test]
        public void WarmStartedSolve_MatchesTheColdSolve()
        {
            VesselData data = Data();
            LoadingState state = LoadingModel.Initial(data, .5f);
            VesselParameters previous = VesselParameters.Create(data, state);
            state.massKg[2] = 30000;
            VesselParameters cold = VesselParameters.Create(data, state);
            VesselParameters warm = VesselParameters.Create(data, state, previous);
            Assert.That(warm.Draft, Is.EqualTo(cold.Draft).Within(1e-4f));
            Assert.That(warm.TrimSlope, Is.EqualTo(cold.TrimSlope).Within(1e-6f));
        }

        [Test]
        public void Geometry_IsBuiltOncePerVesselData()
        {
            VesselData data = Data();
            Assert.That(SectionHydrostatics.For(data), Is.SameAs(SectionHydrostatics.For(data)));
            Assert.That(VesselParameters.Create(data).Sections, Is.SameAs(VesselParameters.Create(data, 0f).Sections));
            Assert.That(SectionHydrostatics.For(Data()), Is.Not.SameAs(SectionHydrostatics.For(data)));
        }

        [Test]
        public void OtherVessels_KeepWallSidedPrismsAndLevelDraft()
        {
            foreach (string file in new[] { VesselFixtures.VolgoBaltFile, VesselFixtures.VolgoneftFile,
                VesselFixtures.MeteorFile, VesselFixtures.LuchFile, VesselFixtures.Kvlcc2File })
            {
                VesselData data = VesselFixtures.Load(file);
                foreach (float fraction in new[] { 0f, .5f, 1f })
                {
                    VesselParameters p = VesselParameters.Create(data, fraction);
                    Assert.That(p.Sections, Is.Null, file);
                    Assert.That(p.TrimSlope, Is.Zero, file);
                    Assert.That(p.DraftAt(50f), Is.EqualTo(p.Draft), file);
                    Assert.That(p.Draft, Is.EqualTo(data.dimensions.loadedDraftM * p.Mass / data.massProperties.loadedMassKg), file);
                }
            }
        }

        [Test]
        public void DisabledTable_FallsBackToTheWallSidedPrisms()
        {
            VesselData data = Data();
            data.hydrostatics.sectionTable.enabled = false;
            VesselParameters p = VesselParameters.Create(data);
            Assert.That(p.Sections, Is.Null);
            Assert.That(p.Draft, Is.EqualTo(data.dimensions.loadedDraftM).Within(1e-4f));
            Assert.That(new HydrostaticsModel(p).CellCount,
                Is.EqualTo(data.hydrostatics.stationCount * data.hydrostatics.stripsAcross));
        }

        [Test]
        public void Validator_RejectsMalformedSectionTables()
        {
            Assert.That(VesselDataValidator.TryValidate(Data(), out string error), Is.True, error);
            void Rejects(System.Action<VesselSectionTable> damage)
            {
                VesselData data = Data();
                damage(data.hydrostatics.sectionTable);
                Assert.That(VesselDataValidator.TryValidate(data, out _), Is.False);
            }
            Rejects(t => t.stations[5].localZ = t.stations[4].localZ);
            Rejects(t => t.stations[40].halfBreadthsM = new float[1]);
            Rejects(t => t.stations[40].halfBreadthsM[0] = t.stripHalfWidthM + 1f);
            Rejects(t => t.stations[40].halfBreadthsM[0] = float.NaN);
            Rejects(t => t.stations[40].heightsAboveKeelM = new[] { 2f, 1f });
            Rejects(t => t.stations[40].heightsAboveKeelM[0] = -0.1f);
            Rejects(t => t.halfStripsAcross = 0);
            Rejects(t => t.tableHeightStepM = 0f);
            Rejects(t => t.maximumMeanDraftM = t.capHeightAboveKeelM + 1f);
            Rejects(t => t.stations = new VesselSectionStation[2]);
        }

        [Test]
        public void TransfersBetweenCargoBallastAndStores_ConserveMass()
        {
            VesselData data = Data();
            LoadingState state = LoadingModel.Initial(data, 1);
            float lightship = data.massProperties.lightshipMassKg;
            state.commands[0] = -1;
            state.commands[2] = 1;
            state.commands[4] = -1;
            float previous = LoadingModel.Payload(state);
            for (int i = 0; i < 2000; i++)
            {
                float[] before = (float[])state.massKg.Clone();
                LoadingModel.Step(data, state, 1f, true);
                float delta = 0f;
                for (int c = 0; c < before.Length; c++) delta += state.massKg[c] - before[c];
                float payload = LoadingModel.Payload(state);
                Assert.That(payload - previous, Is.EqualTo(delta).Within(1f));
                Assert.That(payload, Is.LessThanOrEqualTo(LoadingModel.Budget(data) + .5f));
                previous = payload;
            }
            VesselParameters p = VesselParameters.Create(data, state);
            Assert.That(p.Mass, Is.EqualTo(lightship + LoadingModel.Payload(state)));
            Assert.That(p.Equilibrium.VolumeM3 * p.Rho, Is.EqualTo(p.Mass).Within(1e-4f * p.Mass));
        }

        [Test]
        public void ZeroDurationStep_LeavesInventoryUnchanged()
        {
            VesselData data = Data();
            LoadingState state = LoadingModel.Initial(data, .5f);
            state.commands[2] = 1;
            float[] before = (float[])state.massKg.Clone();
            Assert.That(LoadingModel.Step(data, state, 0f, true), Is.False);
            Assert.That(state.massKg, Is.EqualTo(before));
        }

        private static LoadingState[] Cases(VesselData data)
        {
            LoadingState ballast = LoadingModel.Initial(data, 0);
            ballast.massKg[2] = ballast.massKg[3] = 1100000;
            ballast.massKg[4] = 200000;
            LoadingState biased = LoadingModel.Initial(data, .5f);
            biased.massKg[0] += 500000;
            biased.massKg[1] -= 500000;
            return new[] { LoadingModel.Initial(data, 1), LoadingModel.Initial(data, .5f), LoadingModel.Initial(data, 0),
                ballast, biased };
        }
    }
}
