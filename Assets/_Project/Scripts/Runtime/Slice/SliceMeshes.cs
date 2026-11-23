using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Flat procedural meshes for the slice: bay plates and outlines, and the vehicles' roof arrow (the vehicles
    /// themselves are ToyVehicle). Every triangle is oriented against an explicit outward direction,
    /// so winding can't silently invert (the failure mode found in BusMeshGenerator).
    /// </summary>
    public static class SliceMeshes
    {

        private sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            private readonly List<int>[] subs;

            public Builder(int submeshes)
            {
                subs = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) subs[i] = new List<int>();
            }

            public int Add(Vector3 p) { V.Add(p); return V.Count - 1; }

            public void Tri(int sub, int a, int b, int c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), outward) < 0f) { int t = b; b = c; c = t; }
                subs[sub].Add(a); subs[sub].Add(b); subs[sub].Add(c);
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(V);
                m.subMeshCount = subs.Length;
                for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
                m.RecalculateNormals(); // vertices are duplicated per band, so creases stay crisp between bands
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Rounded-rectangle outline at height y, shrunk by inset; counter-clockwise seen from above.
        /// Straight edges are subdivided every `step` so window pillars can be cut along the long sides.</summary>
        private static List<Vector3> Ring(float hw, float hl, float r, float y, float inset, int seg = 5, float step = 0.12f)
        {
            var pts = new List<Vector3>();
            float w = hw - inset, l = hl - inset, rr = Mathf.Max(0.02f, r - inset);
            Vector2[] centres = { new Vector2(w - rr, l - rr), new Vector2(-(w - rr), l - rr), new Vector2(-(w - rr), -(l - rr)), new Vector2(w - rr, -(l - rr)) };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= seg; s++)
                {
                    float a = (c + s / (float)seg) * Mathf.PI * 0.5f;
                    pts.Add(new Vector3(centres[c].x + Mathf.Cos(a) * rr, y, centres[c].y + Mathf.Sin(a) * rr));
                }
                // Straight run from this corner arc's end to the next arc's start.
                Vector3 from = pts[pts.Count - 1];
                float a1 = (c + 1) * Mathf.PI * 0.5f;
                Vector2 nc = centres[(c + 1) % 4];
                var to = new Vector3(nc.x + Mathf.Cos(a1) * rr, y, nc.y + Mathf.Sin(a1) * rr);
                // Count from the un-inset dimensions so every ring of a body has the same number of points (Band pairs them by index).
                float run = 2f * Mathf.Max(0f, (c % 2 == 0 ? hw : hl) - r);
                int pieces = Mathf.Max(1, Mathf.RoundToInt(run / step));
                for (int k = 1; k < pieces; k++) pts.Add(Vector3.Lerp(from, to, k / (float)pieces));
            }
            return pts;
        }

        private static void Band(Builder b, int sub, List<Vector3> lower, List<Vector3> upper, System.Func<Vector3, int> pick = null)
        {
            int n = lower.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = b.Add(lower[i]), a1 = b.Add(lower[j]), b0 = b.Add(upper[i]), b1 = b.Add(upper[j]);
                Vector3 mid = (lower[i] + lower[j] + upper[i] + upper[j]) * 0.25f;
                Vector3 outward = new Vector3(mid.x, 0f, mid.z) + Vector3.up * ((upper[i].y - lower[i].y) < 0.02f ? 1f : 0f); // flat ledges face up
                int s = pick != null ? pick(mid) : sub;
                b.Tri(s, a0, b0, a1, outward);
                b.Tri(s, a1, b0, b1, outward);
            }
        }

        private static void Cap(Builder b, int sub, List<Vector3> ring, Vector3 normal)
        {
            Vector3 centre = Vector3.zero;
            foreach (Vector3 p in ring) centre += p;
            int c = b.Add(centre / ring.Count);
            int first = b.V.Count;
            foreach (Vector3 p in ring) b.Add(p);
            for (int i = 0; i < ring.Count; i++) b.Tri(sub, c, first + i, first + (i + 1) % ring.Count, normal);
        }

        /// <summary>Flat rounded rectangle (slot fill), facing up.</summary>
        public static Mesh RoundedPlate(float width, float length, float radius)
        {
            var b = new Builder(1);
            Cap(b, 0, Ring(width * 0.5f, length * 0.5f, radius, 0f, 0f, 6, 10f), Vector3.up);
            return b.Build("SliceRoundedPlate");
        }

        /// <summary>Flat rounded-rectangle outline (slot border), facing up.</summary>
        public static Mesh RoundedFrame(float width, float length, float radius, float thickness)
        {
            var b = new Builder(1);
            List<Vector3> outer = Ring(width * 0.5f, length * 0.5f, radius, 0f, 0f, 6, 10f), inner = Ring(width * 0.5f, length * 0.5f, radius, 0f, thickness, 6, 10f);
            for (int i = 0; i < outer.Count; i++)
            {
                int j = (i + 1) % outer.Count;
                int o0 = b.Add(outer[i]), o1 = b.Add(outer[j]), i0 = b.Add(inner[i]), i1 = b.Add(inner[j]);
                b.Tri(0, o0, i0, o1, Vector3.up);
                b.Tri(0, o1, i0, i1, Vector3.up);
            }
            return b.Build("SliceRoundedFrame");
        }

        /// <summary>Flat roof arrow pointing +Z, sized to the roof so it reads at a glance (the reference's arrows are large).</summary>
        public static Mesh Arrow(float width, float length)
        {
            float shaftW = width * 0.13f, headW = width * 0.33f, tail = -length * 0.30f, neck = length * 0.04f, tip = length * 0.30f;
            var b = new Builder(1);
            int s0 = b.Add(new Vector3(-shaftW, 0f, tail)), s1 = b.Add(new Vector3(shaftW, 0f, tail));
            int s2 = b.Add(new Vector3(shaftW, 0f, neck)), s3 = b.Add(new Vector3(-shaftW, 0f, neck));
            int h0 = b.Add(new Vector3(-headW, 0f, neck)), h1 = b.Add(new Vector3(headW, 0f, neck)), h2 = b.Add(new Vector3(0f, 0f, tip));
            b.Tri(0, s0, s3, s1, Vector3.up);
            b.Tri(0, s1, s3, s2, Vector3.up);
            b.Tri(0, h0, h2, h1, Vector3.up);
            return b.Build("SliceVehicleArrow");
        }
    }
}
