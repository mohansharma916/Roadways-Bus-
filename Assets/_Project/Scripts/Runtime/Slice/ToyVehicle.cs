using System.Collections.Generic;
using RoadwaysGame.Core;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>The depot vehicles, made in Blender (Tools/Blender/models.py, Toy{Kind}) on their Core footprints. +Z is forward.</summary>
    public static class ToyVehicle
    {
        /// <summary>Builds the model under parent with its body in the given colour; roofTop is the roof's height, where the arrow goes.</summary>
        public static Transform Build(VehicleKind kind, Color body, Transform parent, out float roofTop)
        {
            // A plain root, so callers can wobble and squash it without touching the imported model's own transform.
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(parent, false);
            Transform model = SliceModels.Spawn("Toy" + kind, root, (_, role) => role == "Paint" ? body : (Color?)null);
            roofTop = model.GetComponentInChildren<Renderer>().bounds.max.y - root.position.y;
            return root;
        }

        /// <summary>
        /// Box of the given size with edges and corners rounded to radius r, centred on the origin. The top shrinks to
        /// topScaleX/topScaleZ of the bottom and slides back by topShiftZ, for slanted glass. Faces are welded so
        /// shading is smooth over the rounded edges, and every triangle is wound to face outward.
        /// </summary>
        public static Mesh RoundedBox(Vector3 size, float r, float topScaleX = 1f, float topScaleZ = 1f, float topShiftZ = 0f, int arc = 3)
        {
            Vector3 h = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f);
            Vector3 inner = h - Vector3.one * r;
            float[][] samples = { Samples(h.x, r, arc), Samples(h.y, r, arc), Samples(h.z, r, arc) };

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var weld = new Dictionary<Vector3Int, int>();
            int Vertex(Vector3 cube)
            {
                Vector3 c = new Vector3(Mathf.Clamp(cube.x, -inner.x, inner.x), Mathf.Clamp(cube.y, -inner.y, inner.y), Mathf.Clamp(cube.z, -inner.z, inner.z));
                Vector3 p = c + (cube - c).normalized * r;
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 1e4f), Mathf.RoundToInt(p.y * 1e4f), Mathf.RoundToInt(p.z * 1e4f));
                if (weld.TryGetValue(key, out int i)) return i;
                verts.Add(p);
                return weld[key] = verts.Count - 1;
            }

            for (int axis = 0; axis < 3; axis++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                float[] su = samples[ua], sv = samples[va];
                var id = new int[su.Length, sv.Length];
                for (int i = 0; i < su.Length; i++)
                for (int j = 0; j < sv.Length; j++)
                {
                    Vector3 cube = Vector3.zero;
                    cube[axis] = sign * h[axis];
                    cube[ua] = su[i];
                    cube[va] = sv[j];
                    id[i, j] = Vertex(cube);
                }
                Vector3 outward = Vector3.zero;
                outward[axis] = sign;
                for (int i = 0; i < su.Length - 1; i++)
                for (int j = 0; j < sv.Length - 1; j++)
                {
                    Tri(verts, tris, id[i, j], id[i + 1, j], id[i + 1, j + 1], outward);
                    Tri(verts, tris, id[i, j], id[i + 1, j + 1], id[i, j + 1], outward);
                }
            }

            for (int i = 0; i < verts.Count; i++) // taper after winding is fixed, so orientation can't flip
            {
                Vector3 v = verts[i];
                float t = (v.y + h.y) / (2f * h.y);
                verts[i] = new Vector3(v.x * Mathf.Lerp(1f, topScaleX, t), v.y, v.z * Mathf.Lerp(1f, topScaleZ, t) + topShiftZ * t);
            }

            var mesh = new Mesh { name = "RoundedBox" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Coordinates along one axis: an arc's worth of samples into each rounded edge, evenly spaced in angle.</summary>
        private static float[] Samples(float half, float r, int arc)
        {
            var s = new float[2 * arc + 2];
            for (int i = 0; i <= arc; i++)
            {
                float off = r * Mathf.Tan(Mathf.PI * 0.25f * (1f - i / (float)arc)); // r·tan(45°…0°) projects to even angles
                s[i] = -(half - r) - off;
                s[s.Length - 1 - i] = (half - r) + off;
            }
            return s;
        }

        private static void Tri(List<Vector3> v, List<int> tris, int a, int b, int c, Vector3 outward)
        {
            if (a == b || b == c || a == c) return; // welded corners collapse some quads
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), outward) < 0f) { int t = b; b = c; c = t; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }
    }
}
