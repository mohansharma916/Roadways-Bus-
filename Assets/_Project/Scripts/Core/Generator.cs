using System;
using System.Collections.Generic;
using System.Numerics;

namespace RoadwaysGame.Core
{
    /// <summary>Tiny deterministic RNG (xorshift64) so a seed means the same level on every platform.</summary>
    public struct Rng
    {
        private ulong state;

        public Rng(int seed)
        {
            state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;
            if (state == 0) state = 1;
        }

        public uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            return (uint)(state >> 32);
        }

        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);
        public float Range(float min, float max) => min + (max - min) * NextFloat();
        public int Range(int minInclusive, int maxExclusive) => minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
    }

    public sealed class JamSpec
    {
        public int Seed;
        public int VehicleCount = 18;
        public float CoupeShare = 0.30f, BusShare = 0.30f; // rest are minivans; bigger vehicles mean bigger crowds
        public int[] Colours = { 0, 1, 2, 3, 4, 6 }; // red, yellow, blue, green, purple, pink
        public int QueueCount = 2;
        public int BayCount = 4;
        // Depot sized so a bus is ~1/6.5 of the screen width and the depot fills ~44 % of a 9:20 screen, as in the reference.
        public float MinX = -7.2f, MinY = -13.0f, MaxX = 7.2f, MaxY = 4.6f;
        public float Gap = 0.14f;          // minimum air between vehicles
        public float DiagonalChance = 0.2f; // share of 45-degree vehicles; the rest are axis-aligned
    }

    /// <summary>Random jams, kept only if well-formed and provably winnable. Offline tool: bake the output, don't regenerate on device.</summary>
    public static class JamGenerator
    {
        public static LevelDef Generate(JamSpec spec, int maxAttempts = 400)
        {
            var rng = new Rng(spec.Seed);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                VehicleDef[] vehicles = PlaceVehicles(spec, ref rng);
                if (vehicles == null) continue;
                var level = new LevelDef(vehicles, BuildQueues(vehicles, spec.QueueCount, ref rng), spec.BayCount,
                    spec.MinX, spec.MinY, spec.MaxX, spec.MaxY);
                if (level.Validate() != null || !Jam.IsGeometricallyClearable(level)) continue;
                if (Solver.Solve(level, out _) == null) continue;
                return level;
            }
            return null;
        }

        private static VehicleDef[] PlaceVehicles(JamSpec spec, ref Rng rng)
        {
            var placed = new List<VehicleDef>(spec.VehicleCount);
            for (int k = 0; k < spec.VehicleCount; k++)
            {
                VehicleKind kind = PickKind(ref rng, spec);
                int colour = spec.Colours[rng.Range(0, spec.Colours.Length)];
                bool ok = false;
                for (int tries = 0; tries < 300 && !ok; tries++)
                {
                    float heading = rng.NextFloat() < spec.DiagonalChance ? 45f + 90f * rng.Range(0, 4) : 90f * rng.Range(0, 4);
                    var pos = new Vector2(rng.Range(spec.MinX, spec.MaxX), rng.Range(spec.MinY, spec.MaxY));
                    // Round so the baked level reproduces exactly what was validated here.
                    pos = new Vector2(MathF.Round(pos.X, 2), MathF.Round(pos.Y, 2));
                    var v = new VehicleDef(kind, colour, pos, heading);
                    if (!Fits(v, placed, spec)) continue;
                    placed.Add(v);
                    ok = true;
                }
                if (!ok) return null;
            }
            return placed.ToArray();
        }

        private static bool Fits(VehicleDef v, List<VehicleDef> placed, JamSpec spec)
        {
            Obb f = v.Footprint();
            float ex = f.ExtentAlong(Vector2.UnitX), ey = f.ExtentAlong(Vector2.UnitY);
            if (f.Center.X - ex < spec.MinX || f.Center.X + ex > spec.MaxX || f.Center.Y - ey < spec.MinY || f.Center.Y + ey > spec.MaxY)
                return false;
            Obb padded = v.Footprint(-spec.Gap * 0.5f);
            foreach (VehicleDef o in placed)
                if (padded.Overlaps(o.Footprint(-spec.Gap * 0.5f))) return false;
            return true;
        }

        private static VehicleKind PickKind(ref Rng rng, JamSpec spec)
        {
            float r = rng.NextFloat();
            return r < spec.CoupeShare ? VehicleKind.Coupe : r < 1f - spec.BusShare ? VehicleKind.Minivan : VehicleKind.Bus;
        }

        /// <summary>One contiguous colour block per vehicle, shuffled, dealt alternately into the queues.</summary>
        private static int[][] BuildQueues(VehicleDef[] vehicles, int queueCount, ref Rng rng)
        {
            var order = new int[vehicles.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var queues = new List<int>[queueCount];
            for (int q = 0; q < queueCount; q++) queues[q] = new List<int>();
            for (int k = 0; k < order.Length; k++)
            {
                VehicleDef v = vehicles[order[k]];
                for (int p = 0; p < v.Capacity; p++) queues[k % queueCount].Add(v.Colour);
            }
            var result = new int[queueCount][];
            for (int q = 0; q < queueCount; q++) result[q] = queues[q].ToArray();
            return result;
        }
    }
}
