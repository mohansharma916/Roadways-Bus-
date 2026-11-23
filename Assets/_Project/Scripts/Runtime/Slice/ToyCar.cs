using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Chunky toy-style car built from rounded boxes: lower body, slanted glass cabin, body-coloured roof,
    /// four wheels with hubcaps, lights and bumpers. Sized to the Core coupe footprint (0.88 x 1.35); +Z is forward.
    /// </summary>
    public static class ToyCar
    {
        public const float Width = 0.88f, Length = 1.35f, RoofTop = 0.7f;

        public static Transform Build(Color body, Transform parent)
        {
            var car = new GameObject("ToyCar").transform;
            car.SetParent(parent, false);

            Material paint = SliceWorld.Lit(body, 0.55f);
            Material glass = SliceWorld.Lit(new Color32(0x22, 0x30, 0x48, 0xFF), 0.92f);
            Material trim = SliceWorld.Lit(new Color32(0xE4, 0xE7, 0xEC, 0xFF), 0.5f);

            // Lower body: bottom at 0.12 so the wheels show beneath it.
            Part(car, "Body", RoundedBox(new Vector3(0.86f, 0.31f, Length), 0.13f), paint, new Vector3(0f, 0.275f, 0f));
            // Cabin glass narrows toward the top, which slants the windscreen and rear window.
            Part(car, "Cabin", RoundedBox(new Vector3(0.76f, 0.27f, 0.80f), 0.1f, 0.84f, 0.7f, -0.05f), glass, new Vector3(0f, 0.52f, -0.08f));
            // Sized to the glass's top so the roof caps the cabin flush instead of overhanging like a lid.
            Part(car, "Roof", RoundedBox(new Vector3(0.62f, 0.075f, 0.55f), 0.035f), paint, new Vector3(0f, 0.66f, -0.125f));

            Mesh lamp = RoundedBox(new Vector3(0.17f, 0.08f, 0.05f), 0.024f);
            Material head = SliceWorld.Lit(new Color32(0xFF, 0xF4, 0xD2, 0xFF), 0.9f), tail = SliceWorld.Lit(new Color32(0xD9, 0x2B, 0x2B, 0xFF), 0.8f);
            Mesh bumper = RoundedBox(new Vector3(0.72f, 0.07f, 0.06f), 0.03f);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(car, "Headlight", lamp, head, new Vector3(s * 0.25f, 0.34f, Length * 0.5f - 0.015f));
                Part(car, "Taillight", lamp, tail, new Vector3(s * 0.25f, 0.34f, -Length * 0.5f + 0.015f));
                Part(car, "Bumper", bumper, trim, new Vector3(0f, 0.22f, s * (Length * 0.5f - 0.005f)));
            }

            Material rubber = SliceWorld.Lit(new Color32(0x2A, 0x2D, 0x34, 0xFF), 0.25f);
            Material hub = SliceWorld.Lit(new Color32(0xF2, 0xF3, 0xF5, 0xFF), 0.6f);
            const float r = 0.17f, tyre = 0.15f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? 1f : -1f) * (Width * 0.5f - tyre * 0.5f - 0.005f), z = (i < 2 ? 1f : -1f) * 0.42f;
                Wheel(car, rubber, new Vector3(x, r, z), new Vector3(r * 2f, tyre * 0.5f, r * 2f));
                Wheel(car, hub, new Vector3(x, r, z), new Vector3(r * 1.05f, tyre * 0.5f + 0.004f, r * 1.05f));
            }
            return car;
        }

        private static void Part(Transform car, string name, Mesh mesh, Material m, Vector3 pos)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(car, false);
            go.transform.localPosition = pos;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }

        private static void Wheel(Transform car, Material m, Vector3 pos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // axis along local Y, turned to point sideways
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Wheel";
            go.transform.SetParent(car, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
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
