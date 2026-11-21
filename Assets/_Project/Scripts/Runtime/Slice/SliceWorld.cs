using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Procedural beach world for the vertical slice, laid out after the Bus Fever Party! reference:
    /// depot (near, bottom of screen) → apron with 8 bays → horseshoe queue with two exit gates → beach and sea (far).
    /// World units on the XZ ground plane; +Z is up the screen.
    /// </summary>
    public static class SliceWorld
    {
        // Depot bounds come from the level (x ±4.6, z -5.2..3.6); vehicles reach the apron along these side lanes.
        public const float LaneX = 5.35f;
        public const float FloorHalfWidth = 5.95f, FloorMinZ = -6.3f;
        public const float ApronMinZ = 4.2f, ApronMaxZ = 9.25f;
        public const float BayZ = 6.55f, BayYawDeg = 12f, BayPitch = 1.42f, BayWidth = 1.22f, BayLength = 2.75f;
        public const int SlotCount = 8;          // VIP + 4 standard + 3 locked, as in the reference
        public const int FirstPlayableSlot = 1;  // core bay b sits in physical slot b + 1
        public const float GateX = 1.15f, GateZ = 9.35f;
        public const float BandHalfWidth = 0.95f;
        public const float ShorelineZ = 17.6f;

        public static readonly Color Sand = new Color32(0xF3, 0xD0, 0x8A, 0xFF);
        public static readonly Color DepotFloor = new Color32(0xC4, 0xCA, 0xD8, 0xFF);
        public static readonly Color Asphalt = new Color32(0x5E, 0x64, 0x72, 0xFF);
        public static readonly Color Paving = new Color32(0xD6, 0xD3, 0xCE, 0xFF);

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        public static Material Lit(Color c, float smoothness = 0.15f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + smoothness;
            if (Materials.TryGetValue(key, out Material m) && m != null) return m;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(lit) { name = "Slice_" + key, color = c };
            m.SetFloat("_Smoothness", smoothness);
            Materials[key] = m;
            return m;
        }

        public static Vector3 SlotCenter(int slot) => new Vector3((slot - (SlotCount - 1) * 0.5f) * BayPitch, 0f, BayZ);
        public static Quaternion SlotRotation => Quaternion.Euler(0f, BayYawDeg, 0f);
        public static Vector3 BayCenter(int coreBay) => SlotCenter(coreBay + FirstPlayableSlot);

        /// <summary>Queue centrelines, front first: each arm runs from its gate out along the bottom and up the side.</summary>
        public static SlicePath[] QueuePaths()
        {
            var left = new List<Vector3>
            {
                new Vector3(-0.95f, 0f, 10.35f), new Vector3(-2.8f, 0f, 10.4f), new Vector3(-4.3f, 0f, 10.9f),
                new Vector3(-5.1f, 0f, 12.2f), new Vector3(-5.2f, 0f, 13.8f), new Vector3(-4.8f, 0f, 15.4f), new Vector3(-4.2f, 0f, 16.9f),
            };
            var right = new List<Vector3>();
            foreach (Vector3 p in left) right.Add(new Vector3(-p.x, p.y, p.z));
            return new[] { new SlicePath(left), new SlicePath(right) };
        }

        public static Vector3 GateFor(SlicePath path) => new Vector3(path.PointAt(0f).x, 0f, GateZ);

        public static void BuildEnvironment(Transform root, SlicePath[] queues)
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(root, false);

            // Low warm key light: the "golden hour" lever from VISUAL_TARGET §5 that keeps this beach distinct from the reference's midday.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(env, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.87f, 0.70f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.82f, 0.93f);
            RenderSettings.ambientEquatorColor = new Color(0.97f, 0.87f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.78f, 0.64f, 0.45f);

            Slab(env, "Sand", new Vector3(0f, -0.05f, (ShorelineZ - 40f) * 0.5f), new Vector3(90f, 0.1f, ShorelineZ + 40f), Lit(Sand, 0.05f));
            BuildSea(env);
            Slab(env, "DepotFloor", new Vector3(0f, 0.01f, (FloorMinZ + ApronMinZ) * 0.5f), new Vector3(FloorHalfWidth * 2f, 0.02f, ApronMinZ - FloorMinZ), Lit(DepotFloor, 0.2f));
            Slab(env, "Apron", new Vector3(0f, 0.015f, (ApronMinZ + ApronMaxZ) * 0.5f), new Vector3(FloorHalfWidth * 2f, 0.03f, ApronMaxZ - ApronMinZ), Lit(Asphalt, 0.1f));
            Slab(env, "ApronEdge", new Vector3(0f, 0.035f, ApronMinZ), new Vector3(FloorHalfWidth * 2f, 0.01f, 0.09f), Lit(Color.white, 0.3f));
            for (int slot = 0; slot < SlotCount; slot++) BuildSlot(env, slot);
            foreach (SlicePath q in queues) BuildBand(env, q);
            Slab(env, "GateIsland", new Vector3(0f, 0.09f, 10.35f), new Vector3(0.5f, 0.18f, BandHalfWidth * 2f), Lit(Color.white, 0.3f));
            BuildProps(env);
        }

        private static void BuildSea(Transform env)
        {
            Shader ocean = Shader.Find("Roadways/OceanWater");
            Material m = ocean != null ? new Material(ocean) : Lit(new Color32(0x1F, 0xB6, 0xD0, 0xFF), 0.8f);
            if (ocean != null) m.SetFloat("_ShorelineZ", ShorelineZ);
            Slab(env, "Sea", new Vector3(0f, -0.06f, ShorelineZ + 30f - 0.6f), new Vector3(90f, 0.02f, 60f), m);
        }

        private static void BuildSlot(Transform env, int slot)
        {
            bool vip = slot == 0, locked = slot > 4;
            Color border = vip ? new Color32(0xFF, 0xB5, 0x2E, 0xFF) : locked ? new Color32(0x4C, 0xD0, 0x7D, 0xFF) : new Color32(0xD4, 0xD8, 0xE0, 0xFF);
            Color fill = vip ? new Color32(0x66, 0x60, 0x52, 0xFF) : locked ? new Color32(0x50, 0x6E, 0x60, 0xFF) : new Color32(0x50, 0x56, 0x63, 0xFF);

            var s = new GameObject("Slot" + slot).transform;
            s.SetParent(env, false);
            s.localPosition = SlotCenter(slot);
            s.localRotation = SlotRotation;
            const float t = 0.08f, y = 0.045f;
            Slab(s, "Fill", new Vector3(0f, 0.035f, 0f), new Vector3(BayWidth, 0.01f, BayLength), Lit(fill, 0.1f));
            Slab(s, "L", new Vector3(-BayWidth * 0.5f, y, 0f), new Vector3(t, 0.02f, BayLength), Lit(border, 0.3f));
            Slab(s, "R", new Vector3(BayWidth * 0.5f, y, 0f), new Vector3(t, 0.02f, BayLength), Lit(border, 0.3f));
            Slab(s, "T", new Vector3(0f, y, BayLength * 0.5f), new Vector3(BayWidth + t, 0.02f, t), Lit(border, 0.3f));
            Slab(s, "B", new Vector3(0f, y, -BayLength * 0.5f), new Vector3(BayWidth + t, 0.02f, t), Lit(border, 0.3f));
            if (vip) Label(s, "VIP", border, 0.32f, 90f);
            if (locked) Label(s, "+", border, 0.55f, 0f);
        }

        private static void Label(Transform parent, string text, Color colour, float height, float spin)
        {
            var go = new GameObject("Label", typeof(TextMesh));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, spin, 0f);
            var tm = go.GetComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = height * 10f / tm.fontSize; // TextMesh glyph height ≈ characterSize × fontSize / 10
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = colour;
        }

        /// <summary>Paved band the crowd stands on, with low walls and an opening (the gate) on the apron side of the front.</summary>
        private static void BuildBand(Transform env, SlicePath path)
        {
            float end = path.Length;
            AddMesh(env, "Band", Ribbon(path, -0.6f, end, BandHalfWidth, -BandHalfWidth, 0.025f), Lit(Paving, 0.15f));
            // Which side faces the apron (-Z) at the front decides where the gate gap goes.
            float apronSide = Vector3.Dot(path.LeftAt(0f), Vector3.back) > 0f ? 1f : -1f;
            AddMesh(env, "WallOuterFront", Wall(path, 1.0f, end, BandHalfWidth * apronSide), Lit(Color.white, 0.35f));
            AddMesh(env, "WallInner", Wall(path, -0.6f, end, -BandHalfWidth * apronSide), Lit(Color.white, 0.35f));
            Vector3 cap = path.PointAt(end);
            var capWall = Slab(env, "TailCap", cap + Vector3.up * 0.09f, new Vector3(BandHalfWidth * 2f, 0.18f, 0.14f), Lit(Color.white, 0.35f));
            capWall.rotation = Quaternion.LookRotation(path.TangentAt(end));
        }

        private static Mesh Ribbon(SlicePath path, float s0, float s1, float left, float right, float y, float step = 0.2f)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int n = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / step) + 1);
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Lerp(s0, s1, i / (float)(n - 1));
                Vector3 p = path.PointAt(s) + Vector3.up * y, side = path.LeftAt(s);
                verts.Add(p + side * left);
                verts.Add(p + side * right);
                if (i == 0) continue;
                int b = verts.Count - 4;
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
            return Finish(verts, tris);
        }

        /// <summary>Box-section wall swept along the path at a sideways offset: top face plus both sides.</summary>
        private static Mesh Wall(SlicePath path, float s0, float s1, float offset, float halfWidth = 0.07f, float height = 0.18f, float step = 0.2f)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int n = Mathf.Max(2, Mathf.CeilToInt((s1 - s0) / step) + 1);
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Lerp(s0, s1, i / (float)(n - 1));
                Vector3 side = path.LeftAt(s), c = path.PointAt(s) + side * offset;
                Vector3 l = c + side * halfWidth, r = c - side * halfWidth, up = Vector3.up * height;
                verts.Add(l); verts.Add(l + up); verts.Add(r + up); verts.Add(r);
                if (i == 0) continue;
                int b = verts.Count - 8;
                for (int f = 0; f < 3; f++)
                {
                    int a0 = b + f, a1 = b + f + 1, c0 = b + 4 + f, c1 = b + 4 + f + 1;
                    tris.Add(a0); tris.Add(c0); tris.Add(a1);
                    tris.Add(a1); tris.Add(c0); tris.Add(c1);
                }
            }
            return Finish(verts, tris);
        }

        private static Mesh Finish(List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void BuildProps(Transform env)
        {
            Umbrella(env, new Vector3(-7.1f, 0f, 13.4f), new Color32(0xFF, 0x6B, 0x6B, 0xFF));
            Umbrella(env, new Vector3(7.2f, 0f, 12.3f), new Color32(0x4F, 0xC3, 0xF7, 0xFF));
            Slab(env, "TowelA", new Vector3(-6.7f, 0.01f, 11.1f), new Vector3(0.75f, 0.02f, 1.35f), Lit(new Color32(0xFF, 0xD1, 0x66, 0xFF))).rotation = Quaternion.Euler(0f, -14f, 0f);
            Slab(env, "TowelB", new Vector3(6.8f, 0.01f, 14.7f), new Vector3(0.75f, 0.02f, 1.35f), Lit(new Color32(0x06, 0xD6, 0xA0, 0xFF))).rotation = Quaternion.Euler(0f, 18f, 0f);
            Primitive(env, PrimitiveType.Sphere, "BeachBall", new Vector3(-6.1f, 0.18f, 15.6f), Vector3.one * 0.36f, Quaternion.identity, Lit(new Color32(0xEF, 0x47, 0x6F, 0xFF), 0.6f));
            Primitive(env, PrimitiveType.Capsule, "Surfboard", new Vector3(6.6f, 0.05f, 10.3f), new Vector3(0.45f, 0.75f, 0.06f), Quaternion.Euler(90f, 28f, 0f), Lit(new Color32(0x11, 0x8A, 0xB2, 0xFF), 0.7f));
            Palm(env, new Vector3(-7.6f, 0f, 16.3f));
            Palm(env, new Vector3(7.9f, 0f, 16.9f));
        }

        private static void Umbrella(Transform env, Vector3 at, Color canopy)
        {
            Primitive(env, PrimitiveType.Cylinder, "UmbrellaPole", at + Vector3.up * 0.8f, new Vector3(0.07f, 0.8f, 0.07f), Quaternion.identity, Lit(Color.white, 0.4f));
            Primitive(env, PrimitiveType.Sphere, "UmbrellaCanopy", at + Vector3.up * 1.6f, new Vector3(1.9f, 0.42f, 1.9f), Quaternion.identity, Lit(canopy, 0.35f));
        }

        private static void Palm(Transform env, Vector3 at)
        {
            Primitive(env, PrimitiveType.Cylinder, "PalmTrunk", at + new Vector3(0.15f, 1.2f, 0f), new Vector3(0.22f, 1.2f, 0.22f), Quaternion.Euler(0f, 0f, -7f), Lit(new Color32(0xA9, 0x7C, 0x50, 0xFF)));
            for (int i = 0; i < 6; i++)
                Primitive(env, PrimitiveType.Sphere, "PalmLeaf", at + new Vector3(0.3f, 2.35f, 0f) + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0.7f, -0.15f, 0f),
                    new Vector3(1.5f, 0.08f, 0.42f), Quaternion.Euler(0f, i * 60f, -18f), Lit(new Color32(0x2A, 0x9D, 0x5C, 0xFF), 0.3f));
        }

        public static Transform Slab(Transform parent, string name, Vector3 pos, Vector3 size, Material m) =>
            Primitive(parent, PrimitiveType.Cube, name, pos, size, Quaternion.identity, m);

        private static Transform Primitive(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Quaternion rot, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
        }
    }
}
