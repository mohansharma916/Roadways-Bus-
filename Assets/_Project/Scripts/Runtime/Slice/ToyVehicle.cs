using System.Collections.Generic;
using RoadwaysGame.Core;
using UnityEngine;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Chunky toy-style vehicles built from rounded boxes: lower body, slanted glass cabin, body-coloured roof,
    /// window pillars, wheels with hubcaps, lights and bumpers. One builder, four proportions, each sized to its
    /// Core footprint (VehicleDef.Width x Length) so what you see is what blocks. +Z is forward.
    /// </summary>
    public static class ToyVehicle
    {
        /// <summary>Proportions of one kind. The glass top shrinks to TopX/TopZ and slides back by Shift, which slants the windscreen.</summary>
        private sealed class Shape
        {
            public float Clearance, BodyHeight;            // body bottom above the ground; body height
            public float CabinLength, CabinZ, CabinHeight; // glass box
            public float TopX = 1f, TopZ = 1f, Shift;
            public float Wheel, FrontAxle, RearAxle;
            public int Pillars;                            // window pillars per side
        }

        private static Shape ShapeOf(VehicleKind kind) => kind switch
        {
            VehicleKind.Coupe => new Shape { Clearance = 0.12f, BodyHeight = 0.31f, CabinLength = 0.80f, CabinZ = -0.08f, CabinHeight = 0.27f,
                TopX = 0.84f, TopZ = 0.70f, Shift = -0.05f, Wheel = 0.17f, FrontAxle = 0.42f, RearAxle = -0.42f },
            VehicleKind.BigCar => new Shape { Clearance = 0.14f, BodyHeight = 0.34f, CabinLength = 1.10f, CabinZ = -0.12f, CabinHeight = 0.30f,
                TopX = 0.90f, TopZ = 0.82f, Shift = -0.04f, Wheel = 0.20f, FrontAxle = 0.52f, RearAxle = -0.52f, Pillars = 1 },
            VehicleKind.Minivan => new Shape { Clearance = 0.13f, BodyHeight = 0.34f, CabinLength = 1.46f, CabinZ = -0.14f, CabinHeight = 0.40f,
                TopX = 0.92f, TopZ = 0.88f, Shift = -0.07f, Wheel = 0.19f, FrontAxle = 0.62f, RearAxle = -0.62f, Pillars = 2 },
            _ => new Shape { Clearance = 0.14f, BodyHeight = 0.40f, CabinLength = 2.28f, CabinZ = -0.02f, CabinHeight = 0.42f, // bus
                TopX = 0.97f, TopZ = 0.97f, Shift = -0.01f, Wheel = 0.21f, FrontAxle = 0.80f, RearAxle = -0.74f, Pillars = 5 },
        };

        /// <summary>Builds the model under parent; roofTop is the roof's height, where the arrow goes.</summary>
        public static Transform Build(VehicleKind kind, Color body, Transform parent, out float roofTop)
        {
            var def = new VehicleDef(kind, 0, default, 0f);
            float w = def.Width, l = def.Length;
            Shape s = ShapeOf(kind);
            var root = new GameObject(kind.ToString()).transform;
            root.SetParent(parent, false);

            Material paint = SliceWorld.Lit(body, 0.55f);
            Material glass = SliceWorld.Lit(new Color32(0x22, 0x30, 0x48, 0xFF), 0.92f);
            Material trim = SliceWorld.Lit(new Color32(0xE4, 0xE7, 0xEC, 0xFF), 0.5f);

            float bodyTop = s.Clearance + s.BodyHeight, cabinWidth = w - 0.12f;
            Part(root, "Body", RoundedBox(new Vector3(w - 0.02f, s.BodyHeight, l), Mathf.Min(0.16f, s.BodyHeight * 0.42f)), paint,
                new Vector3(0f, s.Clearance + s.BodyHeight * 0.5f, 0f));
            float cabinY = bodyTop - 0.045f + s.CabinHeight * 0.5f, cabinTop = cabinY + s.CabinHeight * 0.5f;
            Part(root, "Cabin", RoundedBox(new Vector3(cabinWidth, s.CabinHeight, s.CabinLength), 0.1f, s.TopX, s.TopZ, s.Shift), glass,
                new Vector3(0f, cabinY, s.CabinZ));
            // Sized to the glass's top so the roof caps the cabin flush instead of overhanging like a lid.
            Part(root, "Roof", RoundedBox(new Vector3(cabinWidth * s.TopX - 0.02f, 0.075f, s.CabinLength * s.TopZ - 0.01f), 0.035f), paint,
                new Vector3(0f, cabinTop + 0.005f, s.CabinZ + s.Shift));
            roofTop = cabinTop + 0.0425f;

            // Pillars split the glass band into windows; they lean in with the glass's taper.
            if (s.Pillars > 0)
            {
                Mesh pillar = RoundedBox(new Vector3(0.05f, s.CabinHeight - 0.06f, 0.07f), 0.02f);
                float midLength = s.CabinLength * Mathf.Lerp(1f, s.TopZ, 0.5f), midX = cabinWidth * 0.5f * Mathf.Lerp(1f, s.TopX, 0.5f);
                float lean = Mathf.Atan2(cabinWidth * 0.5f * (1f - s.TopX), s.CabinHeight) * Mathf.Rad2Deg;
                for (int i = 1; i <= s.Pillars; i++)
                {
                    float z = s.CabinZ + s.Shift * 0.5f + (i / (float)(s.Pillars + 1) - 0.5f) * midLength;
                    for (int side = -1; side <= 1; side += 2)
                        Part(root, "Pillar", pillar, paint, new Vector3(side * midX, cabinY, z)).localRotation = Quaternion.Euler(0f, 0f, side * lean);
                }
            }

            Mesh lamp = RoundedBox(new Vector3(0.17f, 0.08f, 0.05f), 0.024f);
            Material head = SliceWorld.Lit(new Color32(0xFF, 0xF4, 0xD2, 0xFF), 0.9f), tail = SliceWorld.Lit(new Color32(0xD9, 0x2B, 0x2B, 0xFF), 0.8f);
            Mesh bumper = RoundedBox(new Vector3(w - 0.16f, 0.07f, 0.06f), 0.03f);
            float lampY = s.Clearance + s.BodyHeight * 0.7f;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(root, "Headlight", lamp, head, new Vector3(side * (w * 0.5f - 0.19f), lampY, l * 0.5f - 0.015f));
                Part(root, "Taillight", lamp, tail, new Vector3(side * (w * 0.5f - 0.19f), lampY, -l * 0.5f + 0.015f));
                Part(root, "Bumper", bumper, trim, new Vector3(0f, s.Clearance + 0.1f, side * (l * 0.5f - 0.005f)));
            }

            Material rubber = SliceWorld.Lit(new Color32(0x2A, 0x2D, 0x34, 0xFF), 0.25f);
            Material hub = SliceWorld.Lit(new Color32(0xF2, 0xF3, 0xF5, 0xFF), 0.6f);
            const float tyre = 0.15f;
            float r = s.Wheel, x = w * 0.5f - tyre * 0.5f - 0.005f;
            foreach (float z in new[] { s.FrontAxle, s.RearAxle })
                for (int side = -1; side <= 1; side += 2)
                {
                    Wheel(root, rubber, new Vector3(side * x, r, z), new Vector3(r * 2f, tyre * 0.5f, r * 2f));
                    Wheel(root, hub, new Vector3(side * x, r, z), new Vector3(r * 1.05f, tyre * 0.5f + 0.004f, r * 1.05f));
                }
            return root;
        }

        private static Transform Part(Transform root, string name, Mesh mesh, Material m, Vector3 pos)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        private static void Wheel(Transform root, Material m, Vector3 pos, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // axis along local Y, turned to point sideways
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Wheel";
            go.transform.SetParent(root, false);
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
