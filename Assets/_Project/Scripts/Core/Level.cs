using System;
using System.Collections.Generic;
using System.Numerics;

namespace RoadwaysGame.Core
{
    /// <summary>Vehicle body type, smallest to largest: Coupe (car) &lt; BigCar &lt; Minivan &lt; Bus. Values are baked into levels, so only append.</summary>
    public enum VehicleKind : byte { Coupe = 0, Minivan = 1, Bus = 2, BigCar = 3 }

    public readonly struct VehicleDef
    {
        public readonly VehicleKind Kind;
        public readonly int Colour;        // ChilbiColorType index
        public readonly Vector2 Position;  // footprint centre
        public readonly float HeadingDeg;  // 0 = +Y (screen-up), 90 = +X; same as Unity's Euler(0, h, 0)

        public VehicleDef(VehicleKind kind, int colour, Vector2 position, float headingDeg)
        {
            Kind = kind;
            Colour = colour;
            Position = position;
            HeadingDeg = headingDeg;
        }

        // Footprints match the Slice ToyVehicle models, so logic and meshes agree.
        public float Length => Kind switch { VehicleKind.Coupe => 1.35f, VehicleKind.BigCar => 1.62f, VehicleKind.Minivan => 1.90f, _ => 2.45f };
        public float Width => Kind switch { VehicleKind.Coupe => 0.88f, VehicleKind.BigCar => 0.92f, VehicleKind.Minivan => 0.95f, _ => 1.05f };
        // Bigger vehicles carry more: that variety is what makes choosing which vehicle to free first matter, and fills the ring.
        public int Capacity => Kind switch { VehicleKind.Coupe => 4, VehicleKind.BigCar => 6, VehicleKind.Minivan => 10, _ => 16 };

        public Vector2 Forward
        {
            get
            {
                float r = HeadingDeg * (MathF.PI / 180f);
                return new Vector2(MathF.Sin(r), MathF.Cos(r));
            }
        }

        /// <summary>Body rectangle; positive shrink makes it smaller, negative inflates it.</summary>
        public Obb Footprint(float shrink = 0f) => new Obb(Position, Forward, Length * 0.5f - shrink, Width * 0.5f - shrink);
    }

    public sealed class LevelDef
    {
        /// <summary>Shrink applied to every footprint in collision tests, so vehicles authored edge-to-edge don't count as touching.</summary>
        public const float Clearance = 0.03f;

        public readonly VehicleDef[] Vehicles;
        /// <summary>Colour per passenger. The first RingCapacity start on the walking loop (in order round it from the exit);
        /// the rest wait on the side roads, in the order they will step on.</summary>
        public readonly int[] Passengers;
        public readonly int RingCapacity; // places on the loop
        public readonly int BayCount;
        public readonly float MinX, MinY, MaxX, MaxY; // depot bounds

        public LevelDef(VehicleDef[] vehicles, int[] passengers, int ringCapacity, int bayCount, float minX, float minY, float maxX, float maxY)
        {
            Vehicles = vehicles;
            Passengers = passengers;
            RingCapacity = ringCapacity;
            BayCount = bayCount;
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }

        /// <summary>Long enough that a swept exit path always leaves the depot.</summary>
        public float SweepReach => (MaxX - MinX) + (MaxY - MinY);

        /// <summary>Null when the level is well-formed, otherwise the first problem found.</summary>
        public string Validate()
        {
            if (Vehicles.Length == 0 || Vehicles.Length > 64) return "vehicle count must be 1..64";
            if (BayCount < 1) return "need at least one bay";
            if (RingCapacity < 1) return "the loop needs at least one place";

            for (int i = 0; i < Vehicles.Length; i++)
            {
                Obb f = Vehicles[i].Footprint();
                float ex = f.ExtentAlong(Vector2.UnitX), ey = f.ExtentAlong(Vector2.UnitY);
                if (f.Center.X - ex < MinX || f.Center.X + ex > MaxX || f.Center.Y - ey < MinY || f.Center.Y + ey > MaxY)
                    return $"vehicle {i} is outside the depot";
                for (int j = i + 1; j < Vehicles.Length; j++)
                    if (Vehicles[i].Footprint(Clearance).Overlaps(Vehicles[j].Footprint(Clearance)))
                        return $"vehicles {i} and {j} overlap";
            }

            // Every passenger needs a seat and every seat a passenger, or the level can't end cleanly.
            var seats = new Dictionary<int, int>();
            foreach (VehicleDef v in Vehicles) seats[v.Colour] = (seats.TryGetValue(v.Colour, out int s) ? s : 0) + v.Capacity;
            var riders = new Dictionary<int, int>();
            foreach (int c in Passengers) riders[c] = (riders.TryGetValue(c, out int r) ? r : 0) + 1;
            foreach (var kv in seats)
                if (!riders.TryGetValue(kv.Key, out int r) || r != kv.Value) return $"colour {kv.Key}: {kv.Value} seats but {(riders.TryGetValue(kv.Key, out int n) ? n : 0)} passengers";
            foreach (var kv in riders)
                if (!seats.ContainsKey(kv.Key)) return $"colour {kv.Key}: passengers but no vehicle";
            return null;
        }
    }
}
