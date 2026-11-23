using System;
using System.Collections;
using RoadwaysGame.Core;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// The two queues, packed four abreast along the band's arms (chunky enough that a full queue runs up the arm). Passengers shuffle forward as the
    /// queue front advances, and boarders walk out of their arm's gate and hop into the vehicle.
    /// </summary>
    public sealed class SliceCrowd : MonoBehaviour
    {
        private const int Lanes = 4;
        private const float RowSpacing = 0.44f, LaneSpacing = 0.5f, FrontOffset = 0.35f;
        private const float WalkSpeed = 2.6f, BodyHeight = 0.8f;

        private Session session;
        private SlicePath[] paths;
        private Transform[][] people, bodies;
        private float[][] phase;

        public void Build(Session s, SlicePath[] queuePaths)
        {
            session = s;
            paths = queuePaths;
            Mesh bean = ChilbiMeshGenerator.CreateCandyBeanMesh(0.5f, BodyHeight, 0.43f);
            int queues = s.Level.Queues.Length;
            people = new Transform[queues][];
            bodies = new Transform[queues][];
            phase = new float[queues][];
            for (int q = 0; q < queues; q++)
            {
                int count = s.QueueLength(q);
                people[q] = new Transform[count];
                bodies[q] = new Transform[count];
                phase[q] = new float[count];
                for (int i = 0; i < count; i++)
                {
                    var root = new GameObject("Passenger").transform;
                    root.SetParent(transform, false);
                    root.position = Slot(q, i);
                    var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    body.SetParent(root, false);
                    body.localPosition = Vector3.up * (BodyHeight * 0.5f);
                    body.GetComponent<MeshFilter>().sharedMesh = bean;
                    body.GetComponent<MeshRenderer>().sharedMaterial = SliceWorld.Lit(ColorPaletteDatabase.GetPrimaryColor((ChilbiColorType)s.ColourAt(q, i)), 0.55f);
                    people[q][i] = root;
                    bodies[q][i] = body;
                    phase[q][i] = UnityEngine.Random.value * 6.28f;
                }
            }
        }

        /// <summary>Ground position of the r-th waiting passenger (0 = front) in queue q.</summary>
        private Vector3 Slot(int q, int r)
        {
            float s = FrontOffset + (r / Lanes) * RowSpacing;
            return paths[q].PointAt(s) + paths[q].LeftAt(s) * ((r % Lanes - (Lanes - 1) * 0.5f) * LaneSpacing);
        }

        private void Update()
        {
            float step = WalkSpeed * Time.deltaTime, t = Time.time;
            for (int q = 0; q < people.Length; q++)
            {
                int head = session.QueueHead(q);
                for (int i = head; i < people[q].Length; i++)
                {
                    Transform p = people[q][i];
                    Vector3 target = Slot(q, i - head);
                    bool moving = (target - p.position).sqrMagnitude > 0.0004f;
                    p.position = Vector3.MoveTowards(p.position, target, step);
                    Bob(bodies[q][i], moving, t + phase[q][i]);
                }
            }
        }

        private static void Bob(Transform body, bool moving, float t) =>
            body.localPosition = Vector3.up * (BodyHeight * 0.5f + (moving ? Mathf.Abs(Mathf.Sin(t * 16f)) * 0.06f : 0f));

        /// <summary>After a booster reorders a queue: perm[q][k] is the old index of whoever is now at k. People then walk to their new places.</summary>
        public void ApplyPermutation(int[][] perm)
        {
            for (int q = 0; q < perm.Length; q++)
            {
                Transform[] p = (Transform[])people[q].Clone(), b = (Transform[])bodies[q].Clone();
                float[] ph = (float[])phase[q].Clone();
                for (int k = 0; k < perm[q].Length; k++)
                {
                    people[q][k] = p[perm[q][k]];
                    bodies[q][k] = b[perm[q][k]];
                    phase[q][k] = ph[perm[q][k]];
                }
            }
        }

        public void Board(BoardEvent e, Vector3 door, Action onArrive) =>
            StartCoroutine(Walk(people[e.Queue][e.Passenger], bodies[e.Queue][e.Passenger], SliceWorld.GateFor(paths[e.Queue]), door, onArrive));

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
