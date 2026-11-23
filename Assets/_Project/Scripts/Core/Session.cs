using System;
using System.Collections.Generic;
using System.Text;

namespace RoadwaysGame.Core
{
    public enum BayState : byte { Empty, Incoming, Docked, Departing }

    public enum DispatchResult : byte { Ok, Blocked, NoFreeBay, NotPresent }

    public readonly struct BoardEvent
    {
        public readonly int Passenger, Place, Bay, Vehicle, Colour;
        public readonly bool Departed; // this passenger filled the vehicle

        public BoardEvent(int passenger, int place, int bay, int vehicle, int colour, bool departed)
        {
            Passenger = passenger; Place = place; Bay = bay; Vehicle = vehicle; Colour = colour; Departed = departed;
        }
    }

    /// <summary>
    /// The rules of one play-through, independent of rendering. Passengers walk a loop of RingCapacity places, with no
    /// front or back; whoever passes the exit boards a parked vehicle of their colour if one has a seat, otherwise walks
    /// on. Everyone else waits on two side roads, which take turns sending the next person onto the loop whenever an
    /// empty place passes their junction. The view drives it: TryDispatch on tap, MarkArrived when a vehicle parks,
    /// TryBoardAt / TryEnterAt as places pass the exit / a junction, ReleaseBay once a full vehicle has driven away.
    /// </summary>
    public sealed class Session
    {
        public readonly LevelDef Level;
        private ulong present;
        private readonly BayState[] bayState;
        private readonly int[] bayVehicle;
        private readonly int[] bayFill;
        private readonly int[] ring;         // passenger id at each loop place, -1 = empty
        private readonly List<int> waiting;  // passenger ids on the side roads, next to step on first
        private int entered;                 // people who have stepped on from the side roads: decides whose turn it is
        private int boarded, usableBays;

        public Session(LevelDef level)
        {
            string problem = level.Validate();
            if (problem != null) throw new ArgumentException("Invalid level: " + problem);
            Level = level;
            present = Jam.AllMask(level.Vehicles.Length);
            // One spare bay beyond BayCount: the VIP spot, usable only after UnlockVipBay().
            bayState = new BayState[level.BayCount + 1];
            bayVehicle = new int[level.BayCount + 1];
            bayFill = new int[level.BayCount + 1];
            for (int b = 0; b < bayVehicle.Length; b++) bayVehicle[b] = -1;
            usableBays = level.BayCount;
            ring = new int[level.RingCapacity];
            waiting = new List<int>();
            for (int i = 0; i < ring.Length; i++) ring[i] = i < level.Passengers.Length ? i : -1;
            for (int i = ring.Length; i < level.Passengers.Length; i++) waiting.Add(i);
        }

        private Session(Session s)
        {
            Level = s.Level;
            present = s.present;
            bayState = (BayState[])s.bayState.Clone();
            bayVehicle = (int[])s.bayVehicle.Clone();
            bayFill = (int[])s.bayFill.Clone();
            ring = (int[])s.ring.Clone();
            waiting = new List<int>(s.waiting);
            entered = s.entered;
            boarded = s.boarded;
            usableBays = s.usableBays;
        }

        public Session Clone() => new Session(this);

        public ulong Present => present;
        public bool IsPresent(int i) => (present & (1UL << i)) != 0;
        public BayState GetBayState(int bay) => bayState[bay];
        public int BayVehicle(int bay) => bayVehicle[bay];
        public int BayFill(int bay) => bayFill[bay];
        public int UsableBays => usableBays;
        public bool VipUnlocked => usableBays > Level.BayCount;
        /// <summary>Index of the VIP bay (the one beyond the level's regular bays).</summary>
        public int VipBay => Level.BayCount;
        public int Boarded => boarded;
        public int TotalPassengers => Level.Passengers.Length;
        public bool IsWon => boarded == TotalPassengers;

        public int RingSize => ring.Length;
        /// <summary>Passenger id at a loop place, or -1 if it is empty.</summary>
        public int RingPassenger(int place) => ring[place];
        public int WaitingCount => waiting.Count;
        /// <summary>The k-th person waiting (0 = next to step on).</summary>
        public int WaitingPassenger(int k) => waiting[k];
        /// <summary>The side roads take turns, so the k-th person waiting stands on side road (entered + k) % 2.</summary>
        public int SideRoadOf(int k) => (entered + k) % 2;
        public int ColourOf(int passenger) => Level.Passengers[passenger];

        /// <summary>On Blocked, detail is the blocking vehicle; on Ok, the bay reserved for it.</summary>
        public DispatchResult TryDispatch(int vehicle, out int detail)
        {
            detail = -1;
            if (!IsPresent(vehicle)) return DispatchResult.NotPresent;
            int blocker = Jam.FindBlocker(Level, present, vehicle);
            if (blocker >= 0) { detail = blocker; return DispatchResult.Blocked; }
            int bay = -1;
            for (int b = 0; b < usableBays && bay < 0; b++) if (bayState[b] == BayState.Empty) bay = b;
            if (bay < 0) return DispatchResult.NoFreeBay;

            present &= ~(1UL << vehicle);
            bayState[bay] = BayState.Incoming;
            bayVehicle[bay] = vehicle;
            bayFill[bay] = 0;
            detail = bay;
            return DispatchResult.Ok;
        }

        public void MarkArrived(int bay)
        {
            if (bayState[bay] == BayState.Incoming) bayState[bay] = BayState.Docked;
        }

        /// <summary>A loop place is passing the exit: its passenger boards a parked vehicle of their colour (lowest bay first)
        /// if there is one, and the place becomes empty. Otherwise they walk on.</summary>
        public bool TryBoardAt(int place, out BoardEvent e)
        {
            int p = ring[place];
            int bay = p >= 0 ? DockedBayFor(ColourOf(p)) : -1;
            if (bay < 0) { e = default; return false; }
            ring[place] = -1;
            bayFill[bay]++;
            boarded++;
            VehicleDef v = Level.Vehicles[bayVehicle[bay]];
            bool full = bayFill[bay] == v.Capacity;
            if (full) bayState[bay] = BayState.Departing;
            e = new BoardEvent(p, place, bay, bayVehicle[bay], v.Colour, full);
            return true;
        }

        /// <summary>A loop place is passing a side road's junction: if it is empty and it is that side road's turn, the next
        /// person waiting steps onto it.</summary>
        public bool TryEnterAt(int place, int sideRoad, out int passenger)
        {
            passenger = -1;
            if (ring[place] >= 0 || waiting.Count == 0 || SideRoadOf(0) != sideRoad) return false;
            passenger = waiting[0];
            waiting.RemoveAt(0);
            entered++;
            ring[place] = passenger;
            return true;
        }

        public void ReleaseBay(int bay)
        {
            if (bayState[bay] != BayState.Departing) return;
            bayState[bay] = BayState.Empty;
            bayVehicle[bay] = -1;
            bayFill[bay] = 0;
        }

        /// <summary>Something will still happen without a tap: someone on the loop has a parked vehicle of their colour,
        /// or an empty place will let someone on from a side road.</summary>
        public bool CanProgress
        {
            get
            {
                bool gap = false;
                foreach (int p in ring)
                {
                    if (p < 0) gap = true;
                    else if (DockedBayFor(ColourOf(p)) >= 0) return true;
                }
                return gap && waiting.Count > 0;
            }
        }

        /// <summary>Terminal failure: every bay holds a parked vehicle whose colour isn't on the loop, and the loop is full
        /// (or the side roads are empty), so nobody can board and nobody new can step on.</summary>
        public bool IsGridlocked
        {
            get
            {
                if (IsWon) return false;
                for (int b = 0; b < usableBays; b++)
                    if (bayState[b] != BayState.Docked) return false;
                return !CanProgress;
            }
        }

        /// <summary>Resolves arrivals, boarding, stepping on and departures instantly, until nothing more can happen. For the
        /// solver and tests; the view paces these itself. Which place passes first doesn't change the outcome: boarding
        /// only ever frees places and lets more people on, so the end state is the same in any order.</summary>
        public void Settle()
        {
            for (int b = 0; b < bayState.Length; b++) MarkArrived(b);
            for (bool changed = true; changed;)
            {
                changed = false;
                for (int i = 0; i < ring.Length; i++)
                {
                    if (TryBoardAt(i, out _)) changed = true;
                    if (ring[i] < 0 && waiting.Count > 0 && TryEnterAt(i, SideRoadOf(0), out _)) changed = true;
                }
            }
            for (int b = 0; b < bayState.Length; b++) ReleaseBay(b);
        }

        /// <summary>True if some loop passenger has this colour.</summary>
        public bool OnRing(int colour)
        {
            foreach (int p in ring) if (p >= 0 && ColourOf(p) == colour) return true;
            return false;
        }

        private int DockedBayFor(int colour)
        {
            for (int b = 0; b < bayState.Length; b++)
                if (bayState[b] == BayState.Docked && Level.Vehicles[bayVehicle[b]].Colour == colour) return b;
            return -1;
        }

        // ---------------------------------------------------------------- boosters (they move people, never add or remove any)

        /// <summary>VIP booster: opens one extra bay for the rest of the level. False if already open.</summary>
        public bool UnlockVipBay()
        {
            if (VipUnlocked) return false;
            usableBays++;
            return true;
        }

        /// <summary>Sort booster: people on the side roads whom a parked (or arriving) vehicle can take swap places with
        /// people on the loop whom none can, nearest the front of the side roads first.</summary>
        public void Arrange()
        {
            var wanted = new HashSet<int>();
            for (int b = 0; b < usableBays; b++)
                if (bayState[b] == BayState.Docked || bayState[b] == BayState.Incoming) wanted.Add(Level.Vehicles[bayVehicle[b]].Colour);
            int k = 0;
            for (int i = 0; i < ring.Length; i++)
            {
                if (ring[i] < 0 || wanted.Contains(ColourOf(ring[i]))) continue;
                while (k < waiting.Count && !wanted.Contains(ColourOf(waiting[k]))) k++;
                if (k == waiting.Count) return;
                (ring[i], waiting[k]) = (waiting[k], ring[i]);
                k++;
            }
        }

        /// <summary>Shuffle booster: everyone not yet boarded trades places at random, across the loop and the side roads;
        /// empty places stay empty. Deterministic per seed.</summary>
        public void Jumble(int seed)
        {
            var people = new List<int>();
            foreach (int p in ring) if (p >= 0) people.Add(p);
            people.AddRange(waiting);
            var rng = new Rng(seed);
            for (int i = people.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                (people[i], people[j]) = (people[j], people[i]);
            }
            int next = 0;
            for (int i = 0; i < ring.Length; i++) if (ring[i] >= 0) ring[i] = people[next++];
            for (int k = 0; k < waiting.Count; k++) waiting[k] = people[next++];
        }

        /// <summary>Solver memo key. Loop order doesn't affect the outcome (see Settle), so the loop counts as colour totals;
        /// the side roads count as their exact order.</summary>
        internal string StateKey()
        {
            var sb = new StringBuilder(160);
            sb.Append(present).Append('|').Append(usableBays).Append('|');
            for (int b = 0; b < bayState.Length; b++)
                sb.Append((int)bayState[b]).Append(':').Append(bayVehicle[b]).Append(':').Append(bayFill[b]).Append(';');
            var counts = new SortedDictionary<int, int>();
            foreach (int p in ring) if (p >= 0) counts[ColourOf(p)] = counts.TryGetValue(ColourOf(p), out int n) ? n + 1 : 1;
            foreach (var kv in counts) sb.Append(kv.Key).Append('x').Append(kv.Value).Append(',');
            sb.Append('|');
            foreach (int p in waiting) sb.Append((char)('a' + ColourOf(p)));
            return sb.ToString();
        }
    }
}
