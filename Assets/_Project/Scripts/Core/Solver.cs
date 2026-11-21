using System.Collections.Generic;

namespace RoadwaysGame.Core
{
    /// <summary>Exhaustive search for a winning dispatch order. Used to guarantee every shipped level is winnable.</summary>
    public static class Solver
    {
        /// <returns>A winning dispatch order, or null. exhausted = true means the budget ran out, so "null" proves nothing.</returns>
        public static int[] Solve(Session start, out bool exhausted, int nodeBudget = 200_000)
        {
            var path = new List<int>();
            var dead = new HashSet<string>();
            int budget = nodeBudget;
            Session s = start.Clone();
            s.Settle();
            bool won = Search(s, path, dead, ref budget);
            exhausted = !won && budget <= 0;
            return won ? path.ToArray() : null;
        }

        public static int[] Solve(LevelDef level, out bool exhausted, int nodeBudget = 200_000) =>
            Solve(new Session(level), out exhausted, nodeBudget);

        private static bool Search(Session s, List<int> path, HashSet<string> dead, ref int budget)
        {
            if (s.IsWon) return true;
            if (budget-- <= 0) return false;
            string key = s.StateKey();
            if (dead.Contains(key)) return false;

            int n = s.Level.Vehicles.Length;
            // Try vehicles a queue front is waiting for first; it finds wins far sooner.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!s.IsPresent(i) || IsWanted(s, i) != (pass == 0)) continue;
                    Session next = s.Clone();
                    if (next.TryDispatch(i, out _) != DispatchResult.Ok) continue;
                    next.Settle();
                    path.Add(i);
                    if (Search(next, path, dead, ref budget)) return true;
                    path.RemoveAt(path.Count - 1);
                    if (budget <= 0) return false; // don't record states as dead when we simply ran out of time
                }
            }
            dead.Add(key);
            return false;
        }

        private static bool IsWanted(Session s, int vehicle)
        {
            int colour = s.Level.Vehicles[vehicle].Colour;
            for (int q = 0; q < s.Level.Queues.Length; q++)
            {
                int h = s.QueueHead(q);
                if (h < s.Level.Queues[q].Length && s.Level.Queues[q][h] == colour) return true;
            }
            return false;
        }

        /// <summary>How many of the opening taps lose — a cheap difficulty signal (0 = any order works).</summary>
        public static void AnalyseOpening(LevelDef level, out int winningOpeners, out int losingOpeners)
        {
            winningOpeners = 0;
            losingOpeners = 0;
            var root = new Session(level);
            for (int i = 0; i < level.Vehicles.Length; i++)
            {
                Session s = root.Clone();
                if (s.TryDispatch(i, out _) != DispatchResult.Ok) continue;
                if (Solve(s, out _) != null) winningOpeners++; else losingOpeners++;
            }
        }
    }
}
