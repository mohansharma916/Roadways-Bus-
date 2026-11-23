using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Procedural beach world for the vertical slice, laid out after the Bus Fever Party! reference:
    /// depot (near, bottom of screen) → apron with 8 bays → ring road with the crowd on it → beach and sea.
    /// World units on the XZ ground plane; +Z is up the screen. Scaled so a bus is ~1/6.5 of the screen width.
    /// </summary>
    public static class SliceWorld
    {
        // Depot bounds come from the level (x ±7.2, z -13..4.6); vehicles reach the apron along these side lanes.
        public const float LaneX = 7.95f;
        public const float FloorHalfWidth = 8.6f, FloorMinZ = -13.8f;
        public const float ApronMinZ = 5.3f, ApronMaxZ = 10.7f;
        public const float BayZ = 7.85f, BayYawDeg = 12f, BayPitch = 1.8f, BayWidth = 1.5f, BayLength = 3.6f;
        public const int SlotCount = 8;          // VIP + 4 standard + 3 locked, as in the reference
        public const float GateZ = 11.05f;
        public const float BandHalfWidth = 1.15f;
        public const float ShorelineZ = 24.5f;
        public const float FrameTopZ = 21.4f;    // top of the ring road: the camera keeps the whole ring in view

        public static readonly Color Sand = new Color32(0xF4, 0xD9, 0xA0, 0xFF);
        // Albedo, not screen colour: the slice's sun + ambient brighten it ~1.6x (linear), so it renders as #D3DAED.
        public static readonly Color DepotFloor = new Color32(0xAA, 0xB1, 0xC2, 0xFF);
        public static readonly Color Asphalt = new Color32(0x50, 0x54, 0x5E, 0xFF);
        public static readonly Color Road = new Color32(0x52, 0x57, 0x69, 0xFF);  // the CircuitRoad design's asphalt
        public static readonly Color Curb = new Color32(0xE6, 0xE9, 0xEE, 0xFF);
        public static readonly Color WallColour = new Color32(0xF4, 0xF5, 0xF7, 0xFF);

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        private static TMP_FontAsset uiFont;

        /// <summary>Lilita One (SIL OFL, licence in Resources/Fonts) as a TextMesh Pro SDF asset, made by Roadways > Slice >
        /// Set Up Sharp Text; null falls back to TMP's default font.</summary>
        public static TMP_FontAsset UiFont => uiFont != null ? uiFont : uiFont = Resources.Load<TMP_FontAsset>("Fonts/LilitaOne-Regular SDF");

        /// <summary>Centred world-space TextMesh Pro label, sharp at any distance; height is roughly the glyph height in world units.</summary>
        public static TextMeshPro WorldText(Transform parent, string text, float height, Color colour, float outline = 0f)
        {
            var t = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshPro>();
            t.transform.SetParent(parent, false);
            if (UiFont != null) t.font = UiFont;
            t.fontSize = height * 10f; // 3D TextMesh Pro: 10 pt is about 1 world unit
            t.rectTransform.sizeDelta = new Vector2(20f, 4f) * height;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = colour;
            t.text = text;
            if (outline > 0f) { t.outlineWidth = outline; t.outlineColor = new Color32(0x1F, 0x2E, 0x5A, 0xFF); }
            return t;
        }

        public static Material Lit(Color c, float smoothness = 0.15f)
        {
            string key = ColorUtility.ToHtmlStringRGBA(c) + smoothness;
            if (Materials.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Slice_" + key, color = c };
            m.SetFloat("_Smoothness", smoothness);
            Materials[key] = m;
            return m;
        }

        public static Vector3 SlotCenter(int slot) => new Vector3((slot - (SlotCount - 1) * 0.5f) * BayPitch, 0f, BayZ);
        public static Quaternion SlotRotation => Quaternion.Euler(0f, BayYawDeg, 0f);

        /// <summary>Core bays 0..regular-1 sit in the standard slots 1..4; the VIP bay (index == regular) sits in slot 0.</summary>
        public static Vector3 BayCenter(int coreBay, int regularBays) => SlotCenter(coreBay >= regularBays ? 0 : coreBay + 1);

        /// <summary>Queue centrelines, front first. Each queue is one half of the ring road (CircuitRoad shape: flat bottom,
        /// rounded sides, a dip in the top middle): from its exit at the bottom centre, round the side, to the top dip.
        /// Together the two halves close the loop.</summary>
        public static SlicePath[] QueuePaths()
        {
            var left = new List<Vector3>
            {
                new Vector3(-0.9f, 0f, 12.4f), new Vector3(-3.5f, 0f, 12.4f), new Vector3(-5.6f, 0f, 12.6f), new Vector3(-6.8f, 0f, 13.7f),
                new Vector3(-7.0f, 0f, 15.6f), new Vector3(-6.6f, 0f, 17.6f), new Vector3(-5.4f, 0f, 19.4f), new Vector3(-3.6f, 0f, 20.1f),
                new Vector3(-1.6f, 0f, 18.6f), new Vector3(0f, 0f, 18.6f), // level at the join, so the two halves meet without a crack
            };
            var right = new List<Vector3>();
            foreach (Vector3 p in left) right.Add(new Vector3(-p.x, p.y, p.z));
            return new[] { new SlicePath(left), new SlicePath(right) };
        }

        /// <summary>Decorative feeder roads running in from the screen edges to the ring's upper sides, as in CircuitRoad.</summary>
        private static SlicePath[] FeederPaths()
        {
            var left = new List<Vector3> { new Vector3(-13.5f, 0f, 22.6f), new Vector3(-10.6f, 0f, 20.6f), new Vector3(-8.2f, 0f, 18.2f), new Vector3(-6.9f, 0f, 16.8f) };
            var right = new List<Vector3>();
            foreach (Vector3 p in left) right.Add(new Vector3(-p.x, p.y, p.z));
            return new[] { new SlicePath(left), new SlicePath(right) };
        }

        public static Vector3 GateFor(SlicePath path) => new Vector3(path.PointAt(0f).x, 0f, GateZ);

        public static void BuildEnvironment(Transform root, SlicePath[] queues)
        {
            var env = new GameObject("Environment").transform;
            env.SetParent(root, false);
            SetupLighting(env);

            Slab(env, "Sand", new Vector3(0f, -0.05f, (ShorelineZ - 40f) * 0.5f), new Vector3(90f, 0.1f, ShorelineZ + 40f), Lit(Sand, 0.05f));
            BuildSea(env);
            // Runs well past both screen edges, down under the booster bar and up beside the apron, so no sand shows there.
            const float floorBottom = -40f;
            Slab(env, "DepotFloor", new Vector3(0f, 0.01f, (floorBottom + ApronMaxZ) * 0.5f), new Vector3(60f, 0.02f, ApronMaxZ - floorBottom), Lit(DepotFloor, 0f));
            Slab(env, "Apron", new Vector3(0f, 0.015f, (ApronMinZ + ApronMaxZ) * 0.5f), new Vector3(FloorHalfWidth * 2f, 0.03f, ApronMaxZ - ApronMinZ), Lit(Asphalt, 0.12f));
            Slab(env, "ApronEdge", new Vector3(0f, 0.035f, ApronMinZ), new Vector3(FloorHalfWidth * 2f, 0.012f, 0.12f), Lit(WallColour, 0.3f));
            for (int slot = 0; slot < SlotCount; slot++) BuildSlot(env, slot);
            BuildRing(env, queues);
            BuildProps(env);
        }

        /// <summary>Sun and ambient shared by the slice and the vehicle preview.</summary>
        public static void SetupLighting(Transform env)
        {
            // Neutral bright daylight, as in the reference; sun from behind-left so shadows fall toward the camera's lower right.
            // Kept well off the camera's mirror angle: at (58°, 155°) every flat roof reflected it and bleached the arrows out.
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(env, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.93f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.45f;
            sun.transform.rotation = Quaternion.Euler(50f, 125f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.86f, 0.9f, 0.97f);
            RenderSettings.ambientEquatorColor = new Color(0.93f, 0.91f, 0.88f);
            RenderSettings.ambientGroundColor = new Color(0.78f, 0.74f, 0.66f);
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
            Color border = vip ? new Color32(0xFF, 0xB6, 0x27, 0xFF) : locked ? new Color32(0x4C, 0xD0, 0x7D, 0xFF) : new Color32(0xC9, 0xCD, 0xD4, 0xFF);
            Color fill = vip ? new Color32(0x4A, 0x47, 0x3E, 0xFF) : locked ? new Color32(0x48, 0x60, 0x55, 0xFF) : new Color32(0x43, 0x47, 0x4F, 0xFF);

            var s = new GameObject("Slot" + slot).transform;
            s.SetParent(env, false);
            s.localPosition = SlotCenter(slot);
            s.localRotation = SlotRotation;
            AddMesh(s, "Fill", SliceMeshes.RoundedPlate(BayWidth, BayLength, 0.22f), Lit(fill, 0.1f)).localPosition = Vector3.up * 0.034f;
            AddMesh(s, "Frame", SliceMeshes.RoundedFrame(BayWidth, BayLength, 0.22f, 0.085f), Lit(border, 0.3f)).localPosition = Vector3.up * 0.038f;
            if (vip) Label(s, "VIP", border, 0.5f, 90f);
            if (locked) Label(s, "+", border, 0.95f, 0f);
        }

        private static void Label(Transform parent, string text, Color colour, float height, float spin)
        {
            TextMeshPro t = WorldText(parent, text, height, colour);
            t.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            t.transform.localRotation = Quaternion.Euler(90f, spin, 0f); // lying on the bay, readable from above
        }

        /// <summary>The ring road the crowd stands on, with white curbs and a dashed centre line. Feeders and the exit neck sit
        /// slightly lower, so the ring's surface covers them where they join.</summary>
        private static void BuildRing(Transform env, SlicePath[] queues)
        {
            Material road = Lit(Road, 0.15f), curb = Lit(Curb, 0.3f);
            SlicePath[] feeders = FeederPaths(); // feeders[i] joins queues[i]'s side of the ring
            foreach (SlicePath f in feeders) Lane(env, f, 0f, f.Length, 0.016f, road, curb, 0f, Vector2.zero);
            float front = queues[0].PointAt(0f).z;
            Slab(env, "ExitNeck", new Vector3(0f, 0.016f, (ApronMaxZ + front) * 0.5f), new Vector3(3.0f, 0.012f, front - ApronMaxZ + 0.2f), road);
            // Each half runs a little past both ends, so the halves overlap and the loop closes; the outer curb starts
            // 0.6 along, leaving the exit open at the bottom centre, and stops where the feeder joins so the roads connect.
            for (int i = 0; i < queues.Length; i++)
                Lane(env, queues[i], -1.0f, queues[i].Length + 0.05f, 0.03f, road, curb, 0.6f, FeederMouth(queues[i], feeders[i]));
        }

        private static float OuterSide(SlicePath ring) => Vector3.Dot(ring.LeftAt(0f), Vector3.back) > 0f ? 1f : -1f; // the side facing the apron at the front

        /// <summary>Arc-length span (x..y) of the ring's outer curb that the feeder road covers: centred where the feeder's
        /// centreline crosses the curb, as wide as the road measured along the curb at that crossing angle.</summary>
        private static Vector2 FeederMouth(SlicePath ring, SlicePath feeder)
        {
            float outer = OuterSide(ring), bestS = 0f, bestT = 0f, best = float.MaxValue;
            for (float s = 0f; s <= ring.Length; s += 0.05f)
            {
                Vector3 edge = ring.PointAt(s) + ring.LeftAt(s) * (outer * BandHalfWidth);
                for (float t = 0f; t <= feeder.Length; t += 0.05f)
                {
                    float d = (feeder.PointAt(t) - edge).sqrMagnitude;
                    if (d < best) { best = d; bestS = s; bestT = t; }
                }
            }
            float sin = Mathf.Abs(Vector3.Cross(ring.TangentAt(bestS), feeder.TangentAt(bestT)).y);
            float half = BandHalfWidth / Mathf.Max(sin, 0.3f) + 0.15f;
            return new Vector2(bestS - half, bestS + half);
        }

        /// <summary>Road surface, curbs and dashes along a path. The outer curb runs from outerCurbStart, skipping gap (x..y) if set.</summary>
        private static void Lane(Transform env, SlicePath p, float s0, float s1, float y, Material road, Material curb, float outerCurbStart, Vector2 gap)
        {
            float outer = OuterSide(p), h = BandHalfWidth;
            AddMesh(env, "Road", Ribbon(p, s0, s1, h, -h, y), road);
            float from = Mathf.Max(s0, outerCurbStart);
            if (gap.y > gap.x)
            {
                AddMesh(env, "CurbOuter", Ribbon(p, from, gap.x, outer * (h + 0.1f), outer * (h - 0.04f), y + 0.004f), curb);
                from = gap.y;
            }
            AddMesh(env, "CurbOuter", Ribbon(p, from, s1, outer * (h + 0.1f), outer * (h - 0.04f), y + 0.004f), curb);
            AddMesh(env, "CurbInner", Ribbon(p, s0, s1, -outer * (h - 0.04f), -outer * (h + 0.1f), y + 0.004f), curb);
            AddMesh(env, "Dashes", Dashes(p, Mathf.Max(s0, 0f), s1, y + 0.004f), curb);
        }

        /// <summary>Dashed centre line: 0.45-long dashes every 0.9, in one mesh.</summary>
        private static Mesh Dashes(SlicePath path, float s0, float s1, float y)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (float s = s0; s + 0.45f <= s1; s += 0.9f)
            {
                int b = verts.Count;
                foreach (float d in new[] { s, s + 0.45f })
                {
                    Vector3 c = path.PointAt(d) + Vector3.up * y, side = path.LeftAt(d) * 0.05f;
                    verts.Add(c + side);
                    verts.Add(c - side);
                }
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1); // same winding as Ribbon
                tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
            }
            return Finish(verts, tris);
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
            // Inside the ring's hole and beside it on the sand. Kept toward the hole's bottom: seen from the 60° camera,
            // the umbrella canopies stand 1.8 up and would otherwise cover the top of the ring.
            Umbrella(env, new Vector3(-2.7f, 0f, 14.2f), new Color32(0xFF, 0x6B, 0x6B, 0xFF));
            Umbrella(env, new Vector3(2.9f, 0f, 14.0f), new Color32(0x4F, 0xC3, 0xF7, 0xFF));
            Slab(env, "TowelA", new Vector3(-0.9f, 0.01f, 14.9f), new Vector3(1.0f, 0.02f, 1.8f), Lit(new Color32(0x4F, 0xC3, 0xF7, 0xFF))).rotation = Quaternion.Euler(0f, -14f, 0f);
            Slab(env, "TowelB", new Vector3(4.4f, 0.01f, 15.6f), new Vector3(1.0f, 0.02f, 1.8f), Lit(new Color32(0xFF, 0xD1, 0x66, 0xFF))).rotation = Quaternion.Euler(0f, 18f, 0f);
            Primitive(env, PrimitiveType.Sphere, "BeachBall", new Vector3(0.7f, 0.24f, 16.0f), Vector3.one * 0.48f, Quaternion.identity, Lit(new Color32(0xEF, 0x47, 0x6F, 0xFF), 0.6f));
            Primitive(env, PrimitiveType.Capsule, "Surfboard", new Vector3(-4.4f, 0.06f, 15.4f), new Vector3(0.55f, 0.95f, 0.07f), Quaternion.Euler(90f, 28f, 0f), Lit(new Color32(0x11, 0x8A, 0xB2, 0xFF), 0.7f));
            Palm(env, new Vector3(-9.6f, 0f, 14.6f));
            Palm(env, new Vector3(9.8f, 0f, 15.2f));
        }

        private static void Umbrella(Transform env, Vector3 at, Color canopy)
        {
            Primitive(env, PrimitiveType.Cylinder, "UmbrellaPole", at + Vector3.up * 0.9f, new Vector3(0.08f, 0.9f, 0.08f), Quaternion.identity, Lit(Color.white, 0.4f));
            Primitive(env, PrimitiveType.Sphere, "UmbrellaCanopy", at + Vector3.up * 1.8f, new Vector3(2.2f, 0.5f, 2.2f), Quaternion.identity, Lit(canopy, 0.35f));
        }

        private static void Palm(Transform env, Vector3 at)
        {
            Primitive(env, PrimitiveType.Cylinder, "PalmTrunk", at + new Vector3(0.15f, 1.3f, 0f), new Vector3(0.26f, 1.3f, 0.26f), Quaternion.Euler(0f, 0f, -7f), Lit(new Color32(0xA9, 0x7C, 0x50, 0xFF)));
            for (int i = 0; i < 6; i++)
                Primitive(env, PrimitiveType.Sphere, "PalmLeaf", at + new Vector3(0.3f, 2.55f, 0f) + Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0.8f, -0.15f, 0f),
                    new Vector3(1.8f, 0.09f, 0.5f), Quaternion.Euler(0f, i * 60f, -18f), Lit(new Color32(0x2A, 0x9D, 0x5C, 0xFF), 0.3f));
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

        private static Transform AddMesh(Transform parent, string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }
    }
}
