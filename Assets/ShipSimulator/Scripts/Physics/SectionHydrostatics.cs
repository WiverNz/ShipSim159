using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ShipSimulator.Physics
{
    public enum HydrostaticRange
    {
        Supported,
        // Solved, but the mean draft or trim lies outside the range the table was checked over.
        OutsideDeclaredRange,
        // No floating position within the table: overload beyond the capped volume, or no convergence.
        Unsolved
    }

    public struct HydrostaticState
    {
        public float VolumeM3;
        // Longitudinal positions are body z, forward positive; KB is above the keel.
        public float LcbM;
        public float KbM;
        public float WaterplaneAreaM2;
        public float LcfM;
        public float TransverseInertiaM4;
        // About the centre of flotation.
        public float LongitudinalInertiaM4;
        internal double WaterplaneFirstMoment;
        internal double WaterplaneSecondMoment;
    }

    // Hull geometry from the vessel's offset table, built once per vessel data and shared by every loading
    // condition. The hull is divided into vertical columns: trapezoidal stations along the length and strips
    // across the breadth. Each column tabulates its immersed area and keel moment against immersion on a
    // uniform grid, integrated exactly from the piecewise-linear offsets. Static equilibrium and the runtime
    // buoyancy forces read the same tables, so the ship settles where the solver says it floats.
    public sealed class SectionHydrostatics
    {
        private const int MaximumIterations = 40;
        private static readonly ConditionalWeakTable<VesselData, SectionHydrostatics> Cache =
            new ConditionalWeakTable<VesselData, SectionHydrostatics>();

        private readonly float[] stationZ;
        private readonly float[] weights;
        private readonly float[] stripX;
        private readonly float[] area;
        private readonly float[] moment;
        private readonly int halfStrips;
        private readonly int count;
        private readonly float step;

        public VesselSectionTable Table { get; }
        public float CapHeightM { get; }
        public int StationCount => stationZ.Length;
        public int StripCount => stripX.Length;
        public HydrostaticState Capped { get; }

        public static bool Supported(VesselData data) =>
            data.hydrostatics != null && data.hydrostatics.sectionTable != null && data.hydrostatics.sectionTable.enabled;

        // Construction integrates every column, so reloading parameters during a transfer reuses it.
        public static SectionHydrostatics For(VesselData data) => Cache.GetValue(data, d => new SectionHydrostatics(d));

        public SectionHydrostatics(VesselData data)
        {
            Table = data.hydrostatics.sectionTable;
            VesselSectionStation[] stations = Table.stations;
            halfStrips = Table.halfStripsAcross;
            CapHeightM = Table.capHeightAboveKeelM;
            count = Mathf.RoundToInt(CapHeightM / Table.tableHeightStepM) + 1;
            step = CapHeightM / (count - 1);

            stationZ = new float[stations.Length];
            weights = new float[stations.Length];
            for (int i = 0; i < stations.Length; i++) stationZ[i] = stations[i].localZ;
            for (int i = 0; i + 1 < stations.Length; i++)
            {
                float half = 0.5f * (stationZ[i + 1] - stationZ[i]);
                weights[i] += half;
                weights[i + 1] += half;
            }

            float width = Table.stripHalfWidthM / halfStrips;
            stripX = new float[2 * halfStrips];
            for (int j = 0; j < stripX.Length; j++) stripX[j] = -Table.stripHalfWidthM + (j + 0.5f) * width;

            area = new float[stations.Length * halfStrips * count];
            moment = new float[area.Length];
            var breaks = new List<double>();
            for (int i = 0; i < stations.Length; i++)
                for (int k = 0; k < halfStrips; k++)
                    BuildColumn(stations[i], k * width, (k + 1) * width, (i * halfStrips + k) * count, breaks);
            Capped = Integrate(CapHeightM, 0f);
        }

        public float StationZ(int station) => stationZ[station];
        public float Weight(int station) => weights[station];
        public float StripX(int strip) => stripX[strip];

        // Area and keel moment of one column's section below immersion h, and its waterline breadth.
        // Immersion above the cap adds nothing: the surface ends at the deck.
        public void Lookup(int station, int strip, float h, out float columnArea, out float columnMoment, out float breadth)
        {
            if (!(h > 0f))
            {
                columnArea = columnMoment = breadth = 0f;
                return;
            }
            int mirrored = strip < halfStrips ? halfStrips - 1 - strip : strip - halfStrips;
            float u = Mathf.Min(h, CapHeightM) / step;
            int k = Mathf.Min((int)u, count - 2);
            float t = u - k;
            int index = (station * halfStrips + mirrored) * count + k;
            float a0 = area[index], a1 = area[index + 1];
            columnArea = a0 + t * (a1 - a0);
            columnMoment = moment[index] + t * (moment[index + 1] - moment[index]);
            breadth = h >= CapHeightM ? 0f : (a1 - a0) / step;
        }

        // Immersion at body z is draft + trimSlope z; positive slope puts the bow down.
        public HydrostaticState Integrate(float draft, float trimSlope)
        {
            double v = 0, mz = 0, mk = 0, awp = 0, fz = 0, ix = 0, iz = 0;
            for (int i = 0; i < stationZ.Length; i++)
            {
                float z = stationZ[i];
                float h = draft + trimSlope * z;
                double w = weights[i];
                for (int j = 0; j < stripX.Length; j++)
                {
                    Lookup(i, j, h, out float a, out float m, out float b);
                    v += w * a;
                    mz += w * a * z;
                    mk += w * m;
                    awp += w * b;
                    fz += w * b * z;
                    ix += w * b * stripX[j] * stripX[j];
                    iz += w * b * z * z;
                }
            }
            double lcf = awp > 0 ? fz / awp : 0;
            return new HydrostaticState
            {
                VolumeM3 = (float)v,
                LcbM = v > 0 ? (float)(mz / v) : 0f,
                KbM = v > 0 ? (float)(mk / v) : 0f,
                WaterplaneAreaM2 = (float)awp,
                LcfM = (float)lcf,
                TransverseInertiaM4 = (float)ix,
                LongitudinalInertiaM4 = (float)(iz - awp * lcf * lcf),
                WaterplaneFirstMoment = fz,
                WaterplaneSecondMoment = iz
            };
        }

        // Draft at body z = 0 and trim slope that displace the volume with buoyancy and gravity on one vertical:
        // (LCB - LCG) + (KB - KG) slope = 0 for a ship trimmed by slope. Newton on the same column tables.
        // A nearby previous solution as the start point saves most iterations during a transfer.
        public HydrostaticRange Solve(float volume, float lcg, float kg, float lpp,
            out float draft, out float trimSlope, out HydrostaticState state,
            float startDraft = float.NaN, float startSlope = 0f)
        {
            draft = 0f;
            trimSlope = 0f;
            state = default;
            if (!(volume > 0f) || float.IsNaN(lcg) || float.IsInfinity(lcg) || float.IsNaN(kg) || float.IsInfinity(kg))
                return HydrostaticRange.Unsolved;
            if (volume >= Capped.VolumeM3)
            {
                draft = CapHeightM;
                state = Capped;
                return HydrostaticRange.Unsolved;
            }

            float maximumSlope = 2f * CapHeightM / lpp;
            bool warm = startDraft > 0f && startDraft < CapHeightM && Mathf.Abs(startSlope) < maximumSlope;
            draft = warm ? startDraft : Mathf.Clamp(volume / Mathf.Max(Capped.WaterplaneAreaM2, 1f), 0.05f, CapHeightM);
            trimSlope = warm ? startSlope : 0f;
            for (int iteration = 0; iteration < MaximumIterations; iteration++)
            {
                state = Integrate(draft, trimSlope);
                double f1 = state.VolumeM3 - volume;
                double f2 = state.VolumeM3 * (state.LcbM - lcg) + state.VolumeM3 * (state.KbM - kg) * trimSlope;
                if (Math.Abs(f1) < 1e-5 * volume && Math.Abs(f2) < 1e-5 * volume)
                    return Classify(draft, trimSlope, lpp);
                double a = state.WaterplaneAreaM2, b = state.WaterplaneFirstMoment, d = state.WaterplaneSecondMoment;
                double determinant = a * d - b * b;
                if (a < 1e-3 || determinant <= 1e-9 * a * d)
                {
                    // A dry or fully drowned waterplane gives no slope: step the draft toward the volume.
                    draft = Mathf.Clamp(draft + (f1 > 0 ? -0.25f : 0.25f), 0.01f, CapHeightM);
                    continue;
                }
                double dDraft = (d * f1 - b * f2) / determinant;
                double dSlope = (a * f2 - b * f1) / determinant;
                draft = Mathf.Clamp(draft - Mathf.Clamp((float)dDraft, -0.5f, 0.5f), 0.01f, CapHeightM);
                trimSlope = Mathf.Clamp(trimSlope - Mathf.Clamp((float)dSlope, -0.01f, 0.01f), -maximumSlope, maximumSlope);
            }
            state = Integrate(draft, trimSlope);
            return HydrostaticRange.Unsolved;
        }

        private HydrostaticRange Classify(float draft, float trimSlope, float lpp)
        {
            bool inside = draft >= Table.minimumMeanDraftM && draft <= Table.maximumMeanDraftM &&
                Mathf.Abs(trimSlope * lpp) <= Table.maximumTrimM;
            return inside ? HydrostaticRange.Supported : HydrostaticRange.OutsideDeclaredRange;
        }

        private void BuildColumn(VesselSectionStation station, float xa, float xb, int offset, List<double> breaks)
        {
            float[] heights = station.heightsAboveKeelM;
            float[] breadths = station.halfBreadthsM;
            breaks.Clear();
            for (int n = 0; n < heights.Length; n++) breaks.Add(heights[n]);
            for (int n = 0; n + 1 < heights.Length; n++)
            {
                double b0 = breadths[n], b1 = breadths[n + 1];
                for (int side = 0; side < 2; side++)
                {
                    double edge = side == 0 ? xa : xb;
                    if ((b0 - edge) * (b1 - edge) < 0)
                        breaks.Add(heights[n] + (edge - b0) / (b1 - b0) * (heights[n + 1] - heights[n]));
                }
            }
            breaks.Sort();

            double a = 0, m = 0;
            int next = 0;
            area[offset] = moment[offset] = 0f;
            for (int k = 1; k < count; k++)
            {
                double lo = (k - 1) * (double)step, hi = k * (double)step;
                while (next < breaks.Count && breaks[next] <= lo) next++;
                double start = lo;
                while (next < breaks.Count && breaks[next] < hi)
                {
                    Piece(heights, breadths, xa, xb, start, breaks[next], ref a, ref m);
                    start = breaks[next++];
                }
                Piece(heights, breadths, xa, xb, start, hi, ref a, ref m);
                area[offset + k] = (float)a;
                moment[offset + k] = (float)m;
            }
        }

        // The strip overlap is linear on a piece, so the trapezoid gives its area and Simpson its keel moment.
        private static void Piece(float[] heights, float[] breadths, double xa, double xb, double p, double q,
            ref double a, ref double m)
        {
            if (q <= p) return;
            double mid = 0.5 * (p + q);
            double op = Overlap(heights, breadths, mid, p, xa, xb);
            double oq = Overlap(heights, breadths, mid, q, xa, xb);
            a += 0.5 * (op + oq) * (q - p);
            m += (q - p) / 6.0 * ((2 * p + q) * op + (p + 2 * q) * oq);
        }

        // Uses the offset segment that contains mid, so a step in breadth stays on its own side.
        private static double Overlap(float[] heights, float[] breadths, double mid, double z, double xa, double xb)
        {
            if (heights.Length < 2 || mid < heights[0] || mid > heights[heights.Length - 1]) return 0;
            for (int n = 0; n + 1 < heights.Length; n++)
            {
                double h0 = heights[n], h1 = heights[n + 1];
                if (h0 <= mid && mid <= h1 && h1 > h0)
                {
                    double b = breadths[n] + (z - h0) / (h1 - h0) * (breadths[n + 1] - breadths[n]);
                    return Math.Max(0, Math.Min(xb, b) - xa);
                }
            }
            return 0;
        }
    }
}
