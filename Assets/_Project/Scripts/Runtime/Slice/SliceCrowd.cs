using System;
using System.Collections;
using RoadwaysGame.Core;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// The crowd. The loop's places travel round the ring road at a steady walking pace, two abreast, and each person on
    /// the loop walks with their place; everyone else waits in rows on the two side roads. Whenever a place passes the
    /// exit or a side road's junction, the crowd asks the session what happens there (board / step on), so the rules stay
    /// in Core. Everyone not boarding just heads for wherever the session says they are, which also animates the boosters.
    /// </summary>
    public sealed class SliceCrowd : MonoBehaviour
    {
        private const int LoopLanes = 2, RoadLanes = 3;
        private const float LoopSpeed = 3.5f;          // world units per second round the loop: a lap in ~10 s
        private const float LaneSpacing = 0.6f, RoadRowSpacing = 0.5f, RoadLaneSpacing = 0.55f;
        private const float CatchUpSpeed = 5f;         // stepping on / shuffling forward; faster than the loop so they catch their place
        private const float RoadFrontGap = 1.6f;       // the front row waits this far back from the junction, off the ring
        private const float WalkSpeed = 2.6f, BodyHeight = 0.8f;

        private Session session;
        private Action<BoardEvent> onBoard;
        private SlicePath loop;
        private SlicePath[] roads;
        private float loopLength, rowSpacing, travelled;
        private readonly float[] junctionAt = new float[2];
        private Transform[] people, bodies;
        private float[] phase;
        private bool[] walkingOff;
        private readonly int[] roadRank = new int[2];

        /// <summary>onBoard runs when someone leaves the loop to board; the caller walks them to the vehicle with Board().</summary>
        public void Build(Session s, Action<BoardEvent> boarded)
        {
            session = s;
            onBoard = boarded;
            loop = SliceWorld.LoopPath();
            roads = SliceWorld.FeederPaths();
            loopLength = loop.Length;
            rowSpacing = loopLength / Mathf.Ceil(s.RingSize / (float)LoopLanes);
            for (int r = 0; r < roads.Length; r++) junctionAt[r] = NearestArc(loop, roads[r].PointAt(roads[r].Length));

            Mesh bean = ChilbiMeshGenerator.CreateCandyBeanMesh(0.5f, BodyHeight, 0.43f);
            int n = s.TotalPassengers;
            people = new Transform[n];
            bodies = new Transform[n];
            phase = new float[n];
            walkingOff = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var root = new GameObject("Passenger").transform;
                root.SetParent(transform, false);
                var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                body.SetParent(root, false);
                body.localPosition = Vector3.up * (BodyHeight * 0.5f);
                body.GetComponent<MeshFilter>().sharedMesh = bean;
                body.GetComponent<MeshRenderer>().sharedMaterial = SliceWorld.Lit(ColorPaletteDatabase.GetPrimaryColor((ChilbiColorType)s.ColourOf(i)), 0.55f);
                people[i] = root;
                bodies[i] = body;
                phase[i] = UnityEngine.Random.value * 6.28f;
            }
            Follow(0f, snap: true);
        }

        private void Update()
        {
            float before = travelled;
            travelled += LoopSpeed * Time.deltaTime;
            int rows = Mathf.CeilToInt(session.RingSize / (float)LoopLanes);
            for (int row = 0; row < rows; row++)
            {
                float from = Mathf.Repeat(row * rowSpacing + before, loopLength), to = Mathf.Repeat(row * rowSpacing + travelled, loopLength);
                bool atExit = Passed(from, to, 0f);
                for (int lane = 0; lane < LoopLanes; lane++)
                {
                    int place = row * LoopLanes + lane;
                    if (place >= session.RingSize) break;
                    if (atExit && session.TryBoardAt(place, out BoardEvent e)) { walkingOff[e.Passenger] = true; onBoard(e); }
                    for (int r = 0; r < roads.Length; r++)
                        if (Passed(from, to, junctionAt[r])) session.TryEnterAt(place, r, out _);
                }
            }
            Follow(Time.deltaTime, snap: false);
        }

        /// <summary>Moves everyone still on the loop or a side road toward where the session has them.</summary>
        private void Follow(float dt, bool snap)
        {
            float t = Time.time;
            for (int place = 0; place < session.RingSize; place++)
            {
                int p = session.RingPassenger(place);
                if (p >= 0) Move(p, PlacePosition(place), dt, snap, t, alwaysWalking: true);
            }
            roadRank[0] = roadRank[1] = 0;
            for (int k = 0; k < session.WaitingCount; k++)
            {
                int road = session.SideRoadOf(k);
                Move(session.WaitingPassenger(k), RoadPosition(road, roadRank[road]++), dt, snap, t, alwaysWalking: false);
            }
        }

        private void Move(int p, Vector3 target, float dt, bool snap, float t, bool alwaysWalking)
        {
            if (walkingOff[p]) return;
            Transform root = people[p];
            float gap = (target - root.position).magnitude;
            // People on the loop keep pace with their place; anyone further off (stepping on, shuffling) catches up quickly.
            root.position = snap || gap <= LoopSpeed * dt * 1.5f ? target : Vector3.MoveTowards(root.position, target, CatchUpSpeed * dt);
            Bob(bodies[p], alwaysWalking || gap > 0.02f, t + phase[p]);
        }

        private Vector3 PlacePosition(int place)
        {
            int row = place / LoopLanes, lane = place % LoopLanes;
            float s = Mathf.Repeat(row * rowSpacing + travelled, loopLength);
            return loop.PointAt(s) + loop.LeftAt(s) * ((lane - (LoopLanes - 1) * 0.5f) * LaneSpacing);
        }

        private Vector3 RoadPosition(int road, int rank)
        {
            SlicePath path = roads[road];
            float s = path.Length - RoadFrontGap - (rank / RoadLanes) * RoadRowSpacing; // past the start it extends straight on, off-screen
            return path.PointAt(s) + path.LeftAt(s) * ((rank % RoadLanes - (RoadLanes - 1) * 0.5f) * RoadLaneSpacing);
        }

        /// <summary>Did an arc position moving from `from` to `to` (both wrapped to the loop) pass `mark` this frame?</summary>
        private static bool Passed(float from, float to, float mark) =>
            from <= to ? from < mark && mark <= to : mark > from || mark <= to;

        private static float NearestArc(SlicePath path, Vector3 point)
        {
            float best = 0f, bestDist = float.MaxValue;
            for (float s = 0f; s < path.Length; s += 0.05f)
            {
                float d = (path.PointAt(s) - point).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = s; }
            }
            return best;
        }

        private static void Bob(Transform body, bool moving, float t) =>
            body.localPosition = Vector3.up * (BodyHeight * 0.5f + (moving ? Mathf.Abs(Mathf.Sin(t * 16f)) * 0.06f : 0f));

        /// <summary>Walks a boarder out of the exit and hops them into the vehicle; onArrive runs as they land.</summary>
        public void Board(BoardEvent e, Vector3 door, Action onArrive)
        {
            Transform p = people[e.Passenger];
            var gate = new Vector3(Mathf.Clamp(p.position.x, -1f, 1f), 0f, SliceWorld.GateZ);
            StartCoroutine(Walk(p, bodies[e.Passenger], gate, door, onArrive));
        }

        private IEnumerator Walk(Transform p, Transform body, Vector3 gate, Vector3 door, Action onArrive)
        {
            while ((p.position - gate).sqrMagnitude > 0.0025f)
            {
                p.position = Vector3.MoveTowards(p.position, gate, WalkSpeed * 1.8f * Time.deltaTime);
                Bob(body, true, Time.time * 1.3f);
                yield return null;
            }
            Vector3 from = p.position;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.32f)
            {
                p.position = Vector3.Lerp(from, door, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.45f);
                yield return null;
            }
            p.gameObject.SetActive(false);
            onArrive?.Invoke();
        }
    }
}
