using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Procedural toy-style vehicle meshes for the slice: rounded-box body with an inset window band and bevelled roof,
    /// four wheels, and a large roof arrow. Every triangle is oriented against an explicit outward direction,
    /// so winding can't silently invert (the failure mode found in BusMeshGenerator).
    /// </summary>
    public static class SliceMeshes
    {
        public const int BodySubmesh = 0, GlassSubmesh = 1;

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

        /// <summary>Rounded-rectangle outline at height y, shrunk by inset; counter-clockwise seen from above.</summary>
        private static List<Vector3> Ring(float hw, float hl, float r, float y, float inset, int seg = 5)
        {
            var pts = new List<Vector3>();
            float w = hw - inset, l = hl - inset, rr = Mathf.Max(0.02f, r - inset);
            Vector2[] centres = { new Vector2(w - rr, l - rr), new Vector2(-(w - rr), l - rr), new Vector2(-(w - rr), -(l - rr)), new Vector2(w - rr, -(l - rr)) };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = (c + s / (float)seg) * Mathf.PI * 0.5f;
                    pts.Add(new Vector3(centres[c].x + Mathf.Cos(a) * rr, y, centres[c].y + Mathf.Sin(a) * rr));
                }
            return pts;
        }

        private static void Band(Builder b, int sub, List<Vector3> lower, List<Vector3> upper)
        {
            int n = lower.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = b.Add(lower[i]), a1 = b.Add(lower[j]), b0 = b.Add(upper[i]), b1 = b.Add(upper[j]);
                Vector3 mid = (lower[i] + lower[j] + upper[i] + upper[j]) * 0.25f;
                Vector3 outward = new Vector3(mid.x, 0f, mid.z) + Vector3.up * ((upper[i].y - lower[i].y) < 0.02f ? 1f : 0f); // flat ledges face up
                b.Tri(sub, a0, b0, a1, outward);
                b.Tri(sub, a1, b0, b1, outward);
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

        /// <summary>Body (submesh 0) and window band (submesh 1), footprint width × length, roof at height.</summary>
        public static Mesh Body(float width, float length, float height)
        {
            float hw = width * 0.5f, hl = length * 0.5f, r = Mathf.Min(hw, hl) * 0.42f;
            float waist = height * 0.52f, glassTop = height * 0.84f;
            var b = new Builder(2);
            List<Vector3> bottom = Ring(hw, hl, r, 0.09f, 0.04f), skirt = Ring(hw, hl, r, 0.14f, 0f), shoulder = Ring(hw, hl, r, waist, 0f);
            List<Vector3> glassLow = Ring(hw, hl, r, waist, 0.035f), glassHigh = Ring(hw, hl, r, glassTop, 0.035f);
            List<Vector3> roofEdge = Ring(hw, hl, r, glassTop, -0.005f), roofLip = Ring(hw, hl, r, height * 0.93f, 0.01f), roof = Ring(hw, hl, r, height, 0.08f);
            Band(b, BodySubmesh, bottom, skirt);
            Band(b, BodySubmesh, skirt, shoulder);
            Band(b, BodySubmesh, shoulder, glassLow);       // ledge where the windows step in
            Band(b, GlassSubmesh, glassLow, glassHigh);
            Band(b, BodySubmesh, glassHigh, roofEdge);      // roof overhang underside lip
            Band(b, BodySubmesh, roofEdge, roofLip);
            Band(b, BodySubmesh, roofLip, roof);            // bevel into the roof
            Cap(b, BodySubmesh, roof, Vector3.up);
            Cap(b, BodySubmesh, bottom, Vector3.down);
            return b.Build("SliceVehicleBody");
        }

        /// <summary>Four wheels with axles along X, tucked under the body and peeking out at the sides.</summary>
        public static Mesh Wheels(float width, float length)
        {
            const int seg = 12;
            float radius = Mathf.Clamp(width * 0.17f, 0.12f, 0.18f), tread = 0.12f;
            var b = new Builder(1);
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                float xOuter = sx * (width * 0.5f + 0.015f), xInner = xOuter - sx * tread;
                var centre = new Vector3(0f, radius, sz * (length * 0.5f - radius - 0.14f));
                var outer = new List<int>(); var inner = new List<int>();
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    Vector3 rim = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * radius;
                    outer.Add(b.Add(centre + rim + Vector3.right * xOuter));
                    inner.Add(b.Add(centre + rim + Vector3.right * xInner));
                }
                for (int i = 0; i < seg; i++)
                {
                    int j = (i + 1) % seg;
                    Vector3 mid = (b.V[outer[i]] + b.V[outer[j]]) * 0.5f;
                    Vector3 outward = new Vector3(0f, mid.y - centre.y, mid.z - centre.z);
                    b.Tri(0, outer[i], inner[i], outer[j], outward);
                    b.Tri(0, outer[j], inner[i], inner[j], outward);
                }
                int hub = b.Add(centre + Vector3.right * xOuter);
                for (int i = 0; i < seg; i++) b.Tri(0, hub, outer[i], outer[(i + 1) % seg], Vector3.right * sx);
            }
            return b.Build("SliceVehicleWheels");
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
