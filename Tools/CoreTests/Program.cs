using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using RoadwaysGame.Core;

// Usage: dotnet run            -> run tests
//        dotnet run -- --scan  -> search seeds for slice-worthy levels
//        dotnet run -- --bake N COUNT [cars] [ring=K] -> print seed N as C# level data
static class Program
{
    static int passed, failed;

    static void Check(bool ok, string name)
    {
        if (ok) passed++;
        else { failed++; Console.WriteLine("FAIL  " + name); }
    }

    static VehicleDef V(VehicleKind k, int colour, float x, float y, float heading) => new VehicleDef(k, colour, new Vector2(x, y), heading);

    static int[] Repeat(int colour, int n) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = colour; return a; }

    static int[] Concat(params int[][] parts) { int n = 0; foreach (var p in parts) n += p.Length; var a = new int[n]; int k = 0; foreach (var p in parts) foreach (var c in p) a[k++] = c; return a; }

    static LevelDef L(VehicleDef[] v, int[] passengers, int bays, int ring = 64) => new LevelDef(v, passengers, ring, bays, -6, -6, 6, 6);

    static int Main(string[] args)
    {
        int count = args.Length > 2 ? int.Parse(args[2]) : 18;
        bool cars = Array.IndexOf(args, "cars") >= 0; // car-only jams
        string ringArg = Array.Find(args, x => x.StartsWith("ring="));
        if (ringArg != null) ring = int.Parse(ringArg.Substring(5)); // places on the loop; default is JamSpec's
        if (args.Length > 0 && args[0] == "--scan") return Scan(args.Length > 1 ? int.Parse(args[1]) : 80, count, cars);
        if (args.Length > 1 && args[0] == "--bake") return Bake(int.Parse(args[1]), count, cars);

        TestGeometry();
        TestBlocking();
        TestValidation();
        TestSession();
        TestSolver();
        TestGenerator();
        TestSliceLevels();
        TestBoosters();
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    static void TestGeometry()
    {
        // Four kinds, smallest to largest, each carrying more than the last.
        VehicleKind[] bySize = { VehicleKind.Coupe, VehicleKind.BigCar, VehicleKind.Minivan, VehicleKind.Bus };
        int[] seats = { 4, 6, 10, 16 };
        for (int k = 0; k < bySize.Length; k++)
        {
            VehicleDef d = V(bySize[k], 0, 0, 0, 0);
            Check(d.Capacity == seats[k], $"{bySize[k]} carries {seats[k]} (got {d.Capacity})");
            if (k > 0) Check(d.Length > V(bySize[k - 1], 0, 0, 0, 0).Length, $"{bySize[k]} is longer than {bySize[k - 1]}");
        }

        var a = new Obb(Vector2.Zero, Vector2.UnitY, 0.5f, 0.5f);
        Check(a.Overlaps(a), "box overlaps itself");
        Check(!a.Overlaps(new Obb(new Vector2(2f, 0), Vector2.UnitY, 0.5f, 0.5f)), "separated boxes don't overlap");
        Check(!a.Overlaps(new Obb(new Vector2(1f, 0), Vector2.UnitY, 0.5f, 0.5f)), "touching boxes don't overlap");
        Vector2 diag = Vector2.Normalize(new Vector2(1, 1));
        Check(a.Overlaps(new Obb(new Vector2(1.1f, 0), diag, 0.5f, 0.5f)), "45-degree box poking in overlaps (SAT)");
        Check(!a.Overlaps(new Obb(new Vector2(1.3f, 0), diag, 0.5f, 0.5f)), "45-degree box clear of edge doesn't overlap");
        Check(a.Contains(new Vector2(0.7f, 0), 0.3f) && !a.Contains(new Vector2(0.9f, 0), 0.3f), "touch margin extends the hit box");
    }

    static void TestBlocking()
    {
        VehicleDef up = V(VehicleKind.Coupe, 0, 0, 0, 0);
        var lvl = L(new[] { up, V(VehicleKind.Coupe, 0, 0, 3, 90) }, Repeat(0, 8), 1);
        Check(Jam.FindBlocker(lvl, 3, 0) == 1, "vehicle in the arrow path blocks");
        Check(Jam.FindBlocker(lvl, 3, 1) == -1, "blocker itself drives out sideways");
        Check(Jam.FindBlocker(lvl, 1, 0) == -1, "path clears once the blocker has left");

        lvl = L(new[] { up, V(VehicleKind.Coupe, 0, 3, 3, 90) }, Repeat(0, 8), 1);
        Check(Jam.FindBlocker(lvl, 3, 0) == -1, "vehicle off to the side doesn't block");

        lvl = L(new[] { up, V(VehicleKind.Coupe, 0, 0, 5, 90), V(VehicleKind.Coupe, 0, 0, 2.5f, 90) }, Repeat(0, 12), 1);
        Check(Jam.FindBlocker(lvl, 7, 0) == 2, "nearest of two blockers is reported");

        lvl = L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 180), V(VehicleKind.Coupe, 0, 0, 3, 90) }, Repeat(0, 8), 1);
        Check(Jam.FindBlocker(lvl, 3, 0) == -1, "vehicle behind doesn't block");

        lvl = L(new[] { V(VehicleKind.Minivan, 0, 0, 0, 0), V(VehicleKind.Minivan, 0, 1.0f, 0.5f, 0) }, Repeat(0, 20), 1);
        Check(lvl.Validate() == null && Jam.FindBlocker(lvl, 3, 0) == -1, "parallel neighbour 5cm away doesn't block");

        lvl = L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 0), V(VehicleKind.Coupe, 0, 0, 3, 180) }, Repeat(0, 8), 1);
        Check(!Jam.IsGeometricallyClearable(lvl), "head-on pair is a deadlock");
        lvl = L(new[] { up, V(VehicleKind.Coupe, 0, 0, 3, 90) }, Repeat(0, 8), 1);
        Check(Jam.IsGeometricallyClearable(lvl), "simple chain is clearable");
    }

    static void TestValidation()
    {
        Check(L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 0), V(VehicleKind.Coupe, 0, 0.3f, 0, 0) }, Repeat(0, 8), 1).Validate() != null, "overlapping vehicles rejected");
        Check(L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 0) }, Repeat(0, 5), 1).Validate() != null, "seat/passenger mismatch rejected");
        Check(L(new[] { V(VehicleKind.Coupe, 0, 5.9f, 0, 0) }, Repeat(0, 4), 1).Validate() != null, "vehicle outside depot rejected");
        Check(L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 0) }, Repeat(1, 4), 1).Validate() != null, "passengers with no vehicle of their colour rejected");
        bool threw = false;
        try { new Session(L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 0) }, Repeat(0, 3), 1)); } catch (ArgumentException) { threw = true; }
        Check(threw, "session refuses an invalid level");
    }

    // One bay; a loop of 4 places holding 4 red, with 4 blue waiting on the side roads. Red coupe left, blue right, both free.
    static LevelDef OrderMatters(int ring = 4) => L(
        new[] { V(VehicleKind.Coupe, 0, -2, 0, 0), V(VehicleKind.Coupe, 2, 2, 0, 0) },
        Concat(Repeat(0, 4), Repeat(2, 4)), 1, ring);

    static void TestSession()
    {
        var s = new Session(OrderMatters());
        Check(s.RingSize == 4 && s.WaitingCount == 4 && s.ColourOf(s.RingPassenger(0)) == 0, "the loop holds the first 4; the rest wait on the side roads");
        Check(s.TryDispatch(1, out int bay) == DispatchResult.Ok && bay == 0, "dispatch reserves the lowest bay");
        Check(s.TryDispatch(0, out _) == DispatchResult.NoFreeBay, "no free bay is reported");
        Check(s.TryDispatch(1, out _) == DispatchResult.NotPresent, "a dispatched vehicle can't be dispatched again");
        Check(!s.IsGridlocked, "not gridlocked while a vehicle is still driving in");
        s.MarkArrived(0);
        Check(!s.TryBoardAt(0, out _) && s.RingPassenger(0) == 0, "a colour with no parked vehicle walks on past the exit");
        Check(s.IsGridlocked, "loop full of the wrong colour and the only bay taken: gridlock");

        s = new Session(OrderMatters());
        s.TryDispatch(0, out _);
        Check(!s.TryBoardAt(0, out _), "nobody boards a vehicle that hasn't parked yet");
        s.MarkArrived(0);
        int boardedNow = 0, departed = 0;
        BoardEvent last = default;
        for (int place = 3; place >= 0; place--)
            if (s.TryBoardAt(place, out BoardEvent e)) { boardedNow++; if (e.Departed) departed++; last = e; }
        Check(boardedNow == 4 && departed == 1 && last.Departed && last.Place == 0, "whoever passes the exit boards, in any order; departs on the last seat");
        Check(s.RingPassenger(2) == -1 && s.GetBayState(0) == BayState.Departing, "boarding leaves a gap; the full vehicle holds its bay while leaving");
        Check(s.TryDispatch(1, out _) == DispatchResult.NoFreeBay, "bay is unavailable while the full vehicle departs");
        Check(!s.TryEnterAt(0, 1, out _) && s.TryEnterAt(0, 0, out int first) && first == 4, "side road 0 goes first, into an empty place");
        Check(!s.TryEnterAt(0, 1, out _), "an occupied place takes nobody");
        Check(!s.TryEnterAt(1, 0, out _) && s.TryEnterAt(1, 1, out int second) && second == 5, "then side road 1: they take turns");
        s.ReleaseBay(0);
        Check(s.TryDispatch(1, out _) == DispatchResult.Ok, "released bay accepts the next vehicle");
        s.Settle();
        Check(s.IsWon && !s.IsGridlocked && s.Boarded == 8 && s.WaitingCount == 0, "level won when every passenger has boarded");

        var blocked = new Session(L(new[] { V(VehicleKind.Coupe, 2, 0, 0, 0), V(VehicleKind.Coupe, 0, 0, 2, 90) },
            Concat(Repeat(2, 4), Repeat(0, 4)), 2));
        Check(blocked.TryDispatch(0, out int blocker) == DispatchResult.Blocked && blocker == 1, "blocked dispatch names the blocker");
        Check(blocked.IsPresent(0), "blocked vehicle stays in the depot");
    }

    static void TestSolver()
    {
        int[] order = Solver.Solve(OrderMatters(), out bool exhausted);
        Check(order != null && !exhausted && order.Length == 2 && order[0] == 0, "solver finds the only winning order");
        var s = new Session(OrderMatters());
        foreach (int i in order ?? new int[0]) { s.TryDispatch(i, out _); s.Settle(); }
        Check(s.IsWon, "solver's order wins when replayed");

        var deadlock = L(new[] { V(VehicleKind.Coupe, 0, 0, 0, 0), V(VehicleKind.Coupe, 0, 0, 3, 180) }, Repeat(0, 8), 2);
        Check(Solver.Solve(deadlock, out exhausted) == null && !exhausted, "deadlocked level is proven unsolvable");

        Solver.AnalyseOpening(OrderMatters(), out int win, out int lose);
        Check(win == 1 && lose == 1, "opening analysis counts one winning and one losing first tap");
        // Why the loop is limited: with everyone on it, every colour keeps coming round, so nothing can be lost.
        Solver.AnalyseOpening(OrderMatters(ring: 8), out win, out lose);
        Check(win == 2 && lose == 0, "with everyone on the loop, any first tap wins");
    }

    static void TestGenerator()
    {
        int made = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            LevelDef lvl = JamGenerator.Generate(new JamSpec { Seed = seed });
            if (lvl == null) continue;
            made++;
            Check(lvl.Validate() == null, $"seed {seed}: generated level is well-formed");
            Check(Jam.IsGeometricallyClearable(lvl), $"seed {seed}: generated jam is clearable");
            Check(Solver.Solve(lvl, out _) != null, $"seed {seed}: generated level is winnable");
        }
        Check(made >= 9, $"generator succeeds for most seeds ({made}/12)");

        LevelDef x = JamGenerator.Generate(new JamSpec { Seed = 7 }), y = JamGenerator.Generate(new JamSpec { Seed = 7 });
        bool same = x != null && y != null && x.Vehicles.Length == y.Vehicles.Length;
        for (int i = 0; same && i < x.Vehicles.Length; i++)
            same = x.Vehicles[i].Position == y.Vehicles[i].Position && x.Vehicles[i].HeadingDeg == y.Vehicles[i].HeadingDeg;
        Check(same, "same seed, same level");
    }

    static void TestBoosters()
    {
        // VIP: one bay holding the wrong colour is gridlock; the VIP bay turns it into a playable state, once.
        var s = new Session(OrderMatters());
        s.TryDispatch(1, out _); s.Settle();
        Check(s.IsGridlocked, "VIP setup: wrong colour in the only bay is gridlock");
        Check(s.UnlockVipBay() && s.VipUnlocked && !s.IsGridlocked, "VIP unlock opens a spot and lifts the gridlock");
        Check(s.TryDispatch(0, out int vipBay) == DispatchResult.Ok && vipBay == s.VipBay, "next dispatch goes to the VIP bay");
        Check(!s.UnlockVipBay(), "VIP can only be unlocked once");
        s.Settle();
        Check(s.Boarded == 8 && s.IsWon, "red boards via the VIP bay, blue steps on and fills the parked blue vehicle: won");

        // Sort: the parked vehicle's colour comes off the side roads onto the loop, swapping with people nobody wants.
        s = new Session(OrderMatters());
        s.TryDispatch(1, out _); s.MarkArrived(0);                 // blue parked, loop full of red, blue waiting
        s.Arrange();
        Check(s.ColourOf(s.RingPassenger(0)) == 2 && s.ColourOf(s.WaitingPassenger(0)) == 0, "Sort swaps the parked vehicle's colour onto the loop");
        Check(s.TryBoardAt(0, out BoardEvent e) && e.Colour == 2, "boarding resumes after Sort");

        // Shuffle: everyone not yet boarded trades places; deterministic per seed; never creates or loses anyone.
        LevelDef big = SliceLevels.Get(1);
        var a = new Session(big); var b = new Session(big);
        a.Jumble(42); b.Jumble(42);
        bool twin = true, everyoneOnce = true;
        var seen = new bool[big.Passengers.Length];
        void Mark(int p) { if (p < 0) return; everyoneOnce &= !seen[p]; seen[p] = true; }
        for (int i = 0; i < a.RingSize; i++) { twin &= a.RingPassenger(i) == b.RingPassenger(i); Mark(a.RingPassenger(i)); }
        for (int k = 0; k < a.WaitingCount; k++) { twin &= a.WaitingPassenger(k) == b.WaitingPassenger(k); Mark(a.WaitingPassenger(k)); }
        foreach (bool x in seen) everyoneOnce &= x;
        Check(everyoneOnce, "Shuffle moves everyone, losing and duplicating nobody");
        Check(twin, "Shuffle is deterministic for a seed");
        a = new Session(OrderMatters());
        a.TryDispatch(0, out _); a.Settle();                        // 4 red boarded, 4 blue stepped on
        int before = a.Boarded;
        a.Jumble(7);
        Check(a.Boarded == before && a.WaitingCount == 0, "Shuffle leaves boarded passengers untouched");
    }

    static void TestSliceLevels()
    {
        // Pinned so a regenerated bake or a rules change can't silently change difficulty.
        int[] expectLosing = { 0, 0, 0 };    // a loop almost always lets you recover from one tap...
        int[] expectStuck = { 0, 14, 43 };   // ...so the real measure is how often a random tapper gets stuck (%)
        for (int i = 0; i < SliceLevels.Count; i++)
        {
            LevelDef lvl = SliceLevels.Get(i);
            Check(lvl.Validate() == null, $"slice level {i + 1} is well-formed");
            Check(Solver.Solve(lvl, out bool exhausted) != null && !exhausted, $"slice level {i + 1} is winnable");
            Solver.AnalyseOpening(lvl, out _, out int lose);
            Check(lose == expectLosing[i], $"slice level {i + 1} has {expectLosing[i]} losing openers (got {lose})");
            int stuck = RandomLossPercent(lvl);
            Check(stuck == expectStuck[i], $"slice level {i + 1}: random tapper stuck {expectStuck[i]}% (got {stuck}%)");
        }
    }

    static int ring = new JamSpec().RingCapacity;

    static JamSpec Spec(int seed, int count, bool cars) =>
        cars ? new JamSpec { Seed = seed, VehicleCount = count, RingCapacity = ring, CoupeShare = 1f, BigCarShare = 0f, BusShare = 0f }
             : new JamSpec { Seed = seed, VehicleCount = count, RingCapacity = ring };

    /// <summary>Difficulty with a loop: share of 200 games lost by a player who taps any vehicle that can leave, at random.</summary>
    static int RandomLossPercent(LevelDef lvl, int games = 200)
    {
        var rnd = new Random(1);
        var options = new System.Collections.Generic.List<int>();
        int losses = 0;
        for (int g = 0; g < games; g++)
        {
            var s = new Session(lvl);
            while (!s.IsWon && !s.IsGridlocked)
            {
                options.Clear();
                for (int i = 0; i < lvl.Vehicles.Length; i++)
                    if (s.IsPresent(i) && s.Clone().TryDispatch(i, out _) == DispatchResult.Ok) options.Add(i);
                if (options.Count == 0) break;
                s.TryDispatch(options[rnd.Next(options.Count)], out _);
                s.Settle();
            }
            if (!s.IsWon) losses++;
        }
        return losses * 100 / games;
    }

    static int Scan(int seeds, int count, bool cars)
    {
        Console.WriteLine($"ring={ring}");
        Console.WriteLine("seed  vehicles  riders  win/lose-openers  random-loss%  ms");
        for (int seed = 1; seed <= seeds; seed++)
        {
            var sw = Stopwatch.StartNew();
            LevelDef lvl = JamGenerator.Generate(Spec(seed, count, cars));
            if (lvl == null) { Console.WriteLine($"{seed,4}  (no level)"); continue; }
            Solver.AnalyseOpening(lvl, out int win, out int lose);
            int riders = lvl.Passengers.Length;
            Console.WriteLine($"{seed,4}  {lvl.Vehicles.Length,8}  {riders,6}  {win,7}/{lose,-8}  {RandomLossPercent(lvl),11}  {sw.ElapsedMilliseconds,5}");
        }
        return 0;
    }

    static int Bake(int seed, int count, bool cars)
    {
        LevelDef lvl = JamGenerator.Generate(Spec(seed, count, cars));
        if (lvl == null) { Console.Error.WriteLine("no level for seed " + seed); return 1; }
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"        // Generated by Tools/CoreTests `dotnet run -- --bake {seed} {count}{(cars ? " cars" : "")} ring={ring}`; verified well-formed, clearable and winnable.");
        sb.AppendLine("        new LevelDef(new[]");
        sb.AppendLine("        {");
        foreach (VehicleDef v in lvl.Vehicles)
            sb.AppendLine(string.Format(ci, "            new VehicleDef(VehicleKind.{0}, {1}, new Vector2({2:0.00}f, {3:0.00}f), {4:0}f),", v.Kind, v.Colour, v.Position.X, v.Position.Y, v.HeadingDeg));
        sb.AppendLine("        },");
        sb.AppendLine("        new[]");
        sb.AppendLine("        {");
        for (int i = 0; i < lvl.Passengers.Length; i += 40)
            sb.AppendLine("            " + string.Join(", ", new ArraySegment<int>(lvl.Passengers, i, Math.Min(40, lvl.Passengers.Length - i))) + ",");
        sb.AppendLine("        },");
        sb.AppendLine(string.Format(ci, "        {0}, {1}, {2}f, {3}f, {4}f, {5}f);", lvl.RingCapacity, lvl.BayCount, lvl.MinX, lvl.MinY, lvl.MaxX, lvl.MaxY));
        Console.Write(sb.ToString());
        return 0;
    }
}
