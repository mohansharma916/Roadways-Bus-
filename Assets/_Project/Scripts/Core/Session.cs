using System;
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
        private readonly int totalPassengers;
        private int boarded;

        public Session(LevelDef level)
        {
            string problem = level.Validate();
            if (problem != null) throw new ArgumentException("Invalid level: " + problem);
            Level = level;
            present = Jam.AllMask(level.Vehicles.Length);
            bayState = new BayState[level.BayCount];
            bayVehicle = new int[level.BayCount];
            bayFill = new int[level.BayCount];
            for (int b = 0; b < bayVehicle.Length; b++) bayVehicle[b] = -1;
            head = new int[level.Queues.Length];
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
            totalPassengers = s.totalPassengers;
            boarded = s.boarded;
        }

        public Session Clone() => new Session(this);

        public ulong Present => present;
        public bool IsPresent(int i) => (present & (1UL << i)) != 0;
        public BayState GetBayState(int bay) => bayState[bay];
        public int BayVehicle(int bay) => bayVehicle[bay];
        public int BayFill(int bay) => bayFill[bay];
        public int QueueHead(int queue) => head[queue];
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
            int bay = Array.IndexOf(bayState, BayState.Empty);
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
                int[] queue = Level.Queues[q];
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
                for (int b = 0; b < bayState.Length; b++)
                    if (bayState[b] != BayState.Docked) return false;
                for (int q = 0; q < head.Length; q++)
                {
                    if (head[q] >= Level.Queues[q].Length) continue;
                    int colour = Level.Queues[q][head[q]];
                    for (int b = 0; b < bayState.Length; b++)
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

        internal string StateKey()
        {
            var sb = new StringBuilder(64);
            sb.Append(present).Append('|');
            foreach (int h in head) sb.Append(h).Append(',');
            sb.Append('|');
            for (int b = 0; b < bayState.Length; b++)
                sb.Append((int)bayState[b]).Append(':').Append(bayVehicle[b]).Append(':').Append(bayFill[b]).Append(';');
            return sb.ToString();
        }
    }
}
