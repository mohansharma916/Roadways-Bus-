using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RoadwaysGame.Core
{
    public enum BayState : byte { Empty, Incoming, Docked, Departing }

    public enum DispatchResult : byte { Ok, Blocked, NoFreeBay, NotPresent }

    public readonly struct BoardEvent
    {
        public readonly int Queue, Passenger, Bay, Vehicle, Colour;
        public readonly bool Departed; // this passenger filled the vehicle

        public BoardEvent(int queue, int passenger, int bay, int vehicle, int colour, bool departed)
        {
            Queue = queue; Passenger = passenger; Bay = bay; Vehicle = vehicle; Colour = colour; Departed = departed;
        }
    }

    /// <summary>
    /// The rules of one play-through, independent of rendering. The view drives it:
    /// TryDispatch on tap, MarkArrived when a vehicle parks, StepBoarding at its own pace,
    /// ReleaseBay once a full vehicle has driven away.
    /// </summary>
    public sealed class Session
    {
        public readonly LevelDef Level;
        private ulong present;
        private readonly BayState[] bayState;
        private readonly int[] bayVehicle;
        private readonly int[] bayFill;
        private readonly int[] head;
        private readonly int[][] queues; // own copy: boosters may reorder the part still waiting
        private readonly int totalPassengers;
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
            head = new int[level.Queues.Length];
            queues = new int[level.Queues.Length][];
            for (int q = 0; q < queues.Length; q++) queues[q] = (int[])level.Queues[q].Clone();
            foreach (int[] q in level.Queues) totalPassengers += q.Length;
        }

        private Session(Session s)
        {
            Level = s.Level;
            present = s.present;
            bayState = (BayState[])s.bayState.Clone();
            bayVehicle = (int[])s.bayVehicle.Clone();
            bayFill = (int[])s.bayFill.Clone();
            head = (int[])s.head.Clone();
            queues = new int[s.queues.Length][];
            for (int q = 0; q < queues.Length; q++) queues[q] = (int[])s.queues[q].Clone();
            totalPassengers = s.totalPassengers;
            boarded = s.boarded;
            usableBays = s.usableBays;
        }

        public Session Clone() => new Session(this);

        public ulong Present => present;
        public bool IsPresent(int i) => (present & (1UL << i)) != 0;
        public BayState GetBayState(int bay) => bayState[bay];
        public int BayVehicle(int bay) => bayVehicle[bay];
        public int BayFill(int bay) => bayFill[bay];
        public int QueueHead(int queue) => head[queue];
        public int QueueLength(int queue) => queues[queue].Length;
        public int ColourAt(int queue, int index) => queues[queue][index];
        public int UsableBays => usableBays;
        public bool VipUnlocked => usableBays > Level.BayCount;
        /// <summary>Index of the VIP bay (the one beyond the level's regular bays).</summary>
        public int VipBay => Level.BayCount;
        public int Boarded => boarded;
        public int TotalPassengers => totalPassengers;
        public bool IsWon => boarded == totalPassengers;

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

        /// <summary>Boards one passenger if any queue front matches a docked vehicle (lowest queue, then lowest bay, first).</summary>
        public bool StepBoarding(out BoardEvent e)
        {
            for (int q = 0; q < head.Length; q++)
            {
                int[] queue = queues[q];
                if (head[q] >= queue.Length) continue;
                int colour = queue[head[q]];
                for (int b = 0; b < bayState.Length; b++)
                {
                    if (bayState[b] != BayState.Docked) continue;
                    VehicleDef v = Level.Vehicles[bayVehicle[b]];
                    if (v.Colour != colour) continue;

                    int passenger = head[q]++;
                    bayFill[b]++;
                    boarded++;
                    bool full = bayFill[b] == v.Capacity;
                    if (full) bayState[b] = BayState.Departing;
                    e = new BoardEvent(q, passenger, b, bayVehicle[b], colour, full);
                    return true;
                }
            }
            e = default;
            return false;
        }

        public void ReleaseBay(int bay)
        {
            if (bayState[bay] != BayState.Departing) return;
            bayState[bay] = BayState.Empty;
            bayVehicle[bay] = -1;
            bayFill[bay] = 0;
        }

        /// <summary>Terminal failure: every bay holds a parked vehicle that no queue front can board.</summary>
        public bool IsGridlocked
        {
            get
            {
                if (IsWon) return false;
                for (int b = 0; b < usableBays; b++)
                    if (bayState[b] != BayState.Docked) return false;
                for (int q = 0; q < head.Length; q++)
                {
                    if (head[q] >= queues[q].Length) continue;
                    int colour = queues[q][head[q]];
                    for (int b = 0; b < usableBays; b++)
                        if (Level.Vehicles[bayVehicle[b]].Colour == colour) return false;
                }
                return true;
            }
        }

        /// <summary>Resolves arrivals, boarding and departures instantly. For the solver and tests; the view paces these itself.</summary>
        public void Settle()
        {
            for (int b = 0; b < bayState.Length; b++) MarkArrived(b);
            while (StepBoarding(out _)) { }
            for (int b = 0; b < bayState.Length; b++) ReleaseBay(b);
        }

        // ---------------------------------------------------------------- boosters

        /// <summary>VIP booster: opens one extra bay for the rest of the level. False if already open.</summary>
        public bool UnlockVipBay()
        {
            if (VipUnlocked) return false;
            usableBays++;
            return true;
        }

        /// <summary>
        /// Arrange booster: in each queue, waiting passengers a parked (or arriving) vehicle can take move to the front,
        /// and the rest are grouped by colour. Stable within a colour. Returns perm[q][k] = the old index of whoever is now
        /// at index k, so the view can walk each person to their new place.
        /// </summary>
        public int[][] Arrange()
        {
            var wanted = new HashSet<int>();
            for (int b = 0; b < usableBays; b++)
                if (bayState[b] == BayState.Docked || bayState[b] == BayState.Incoming) wanted.Add(Level.Vehicles[bayVehicle[b]].Colour);
            return Reorder((q, waiting) =>
            {
                int[] colours = queues[q];
                var firstSeen = new List<int>();
                foreach (int i in waiting) if (!firstSeen.Contains(colours[i])) firstSeen.Add(colours[i]);
                return waiting.OrderBy(i => wanted.Contains(colours[i]) ? 0 : 1).ThenBy(i => firstSeen.IndexOf(colours[i])).ThenBy(i => i).ToList();
            });
        }

        /// <summary>Jumble booster: shuffles each queue's waiting passengers (deterministic per seed). Same contract as Arrange.</summary>
        public int[][] Jumble(int seed)
        {
            var rng = new Rng(seed);
            return Reorder((q, waiting) =>
            {
                var list = new List<int>(waiting);
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
                return list;
            });
        }

        /// <summary>Applies a new order to each queue's waiting part; `order(q, waitingIndices)` returns those indices reordered.</summary>
        private int[][] Reorder(Func<int, List<int>, List<int>> order)
        {
            var perms = new int[queues.Length][];
            for (int q = 0; q < queues.Length; q++)
            {
                int h = head[q], n = queues[q].Length;
                var waiting = new List<int>();
                for (int i = h; i < n; i++) waiting.Add(i);
                List<int> sorted = order(q, waiting);
                var perm = new int[n];
                for (int i = 0; i < h; i++) perm[i] = i;
                for (int k = 0; k < sorted.Count; k++) perm[h + k] = sorted[k];
                var old = (int[])queues[q].Clone();
                for (int k = 0; k < n; k++) queues[q][k] = old[perm[k]];
                perms[q] = perm;
            }
            return perms;
        }

        internal string StateKey()
        {
            var sb = new StringBuilder(96);
            sb.Append(present).Append('|').Append(usableBays).Append('|');
            foreach (int h in head) sb.Append(h).Append(',');
            sb.Append('|');
            for (int b = 0; b < bayState.Length; b++)
                sb.Append((int)bayState[b]).Append(':').Append(bayVehicle[b]).Append(':').Append(bayFill[b]).Append(';');
            return sb.ToString();
        }
    }
}
