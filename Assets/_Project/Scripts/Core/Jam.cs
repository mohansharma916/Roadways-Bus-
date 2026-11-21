using System.Numerics;

namespace RoadwaysGame.Core
{
    /// <summary>Vehicle-jam geometry: may a vehicle drive straight out along its arrow?</summary>
    public static class Jam
    {
        public static ulong AllMask(int n) => n >= 64 ? ulong.MaxValue : (1UL << n) - 1;

        /// <summary>Nearest present vehicle in i's exit path, or -1 if the path is clear.</summary>
        public static int FindBlocker(LevelDef level, ulong present, int i)
        {
            VehicleDef v = level.Vehicles[i];
            Vector2 f = v.Forward;
            float reach = level.SweepReach;
            // The swept lane starts at the vehicle's own rear, so anything overlapping it is ahead or alongside-ahead.
            var lane = new Obb(v.Position + f * (reach * 0.5f), f, v.Length * 0.5f + reach * 0.5f, v.Width * 0.5f - LevelDef.Clearance);

            int best = -1;
            float bestT = float.MaxValue;
            for (int j = 0; j < level.Vehicles.Length; j++)
            {
                if (j == i || (present & (1UL << j)) == 0) continue;
                Obb other = level.Vehicles[j].Footprint(LevelDef.Clearance);
                if (!lane.Overlaps(other)) continue;
                float t = Vector2.Dot(other.Center - v.Position, f) - other.ExtentAlong(f); // nearest point of the blocker
                if (t < bestT) { bestT = t; best = j; }
            }
            return best;
        }

        /// <summary>True if some removal order clears every vehicle, ignoring bays and queues.
        /// Removing a vehicle never blocks another, so the greedy fixpoint is exact.</summary>
        public static bool IsGeometricallyClearable(LevelDef level)
        {
            ulong present = AllMask(level.Vehicles.Length);
            bool progress = true;
            while (present != 0 && progress)
            {
                progress = false;
                for (int i = 0; i < level.Vehicles.Length; i++)
                {
                    if ((present & (1UL << i)) == 0 || FindBlocker(level, present, i) >= 0) continue;
                    present &= ~(1UL << i);
                    progress = true;
                }
            }
            return present == 0;
        }
    }
}
