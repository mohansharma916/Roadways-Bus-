using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Procedural Plain Cherry Blossom Park Generator (Strictly NO trees, NO leaves).
    /// Generates pure landscape park geometry: soft sakura lawns, terraced mounds,
    /// blush stone winding paths, reflective lotus water canal/pond, pink quartz pebbles,
    /// cherry blossom arched footbridge, and tiered rose ceramic clay fountain.
    /// Free of all cars, characters, huts, and items.
    /// </summary>
    public class PlainCherryBlossomParkGenerator : MonoBehaviour
    {
        [Header("Park Dimensions")]
        public float parkRadius = 36f;

        [Header("Cherry Blossom Color Palette")]
        public Color lawnColor = new Color(0.99f, 0.91f, 0.95f);       // #fce7f3 Soft Sakura Lawn
        public Color lawnDeepColor = new Color(0.98f, 0.81f, 0.91f);   // #fbcfe8 Tiered Lawn
        public Color blushPathColor = new Color(0.99f, 0.80f, 0.83f);  // #fecdd3 Blush Stone Path
        public Color pathEdgeColor = new Color(0.96f, 0.45f, 0.71f);   // #f472b6 Rose Curbs
        public Color bridgeWoodColor = new Color(0.98f, 0.44f, 0.52f); // #fb7185 Sakura Wood Pink
        public Color marshmallowColor = new Color(1.0f, 0.96f, 0.97f);  // #fff5f7 Marshmallow White
        public Color fountainClayColor = new Color(0.96f, 0.25f, 0.37f);// #f43f5e Rose Clay Basin
        public Color pondWaterColor = new Color(0.99f, 0.91f, 0.95f, 0.85f); // Reflective Pink Water

        private void Start()
        {
            BuildPlainPark();
            SetupTopDownCamera();
        }

        [ContextMenu("Build Plain Park")]
        public void BuildPlainPark()
        {
            // Clear existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            BuildLawnBase();
            BuildTerracedMounds();
            BuildLotusPond();
            BuildArchedFootbridge();
            BuildCentralFountain();
            BuildMeanderingPaths();
        }

        [ContextMenu("Setup Top-Down Camera")]
        public void SetupTopDownCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 75f, 0);
                cam.transform.rotation = Quaternion.Euler(90f, 0, 0);
                cam.fieldOfView = 55f;
            }
        }

        private void BuildLawnBase()
        {
            GameObject baseLawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseLawn.name = "SakuraBaseLawn";
            baseLawn.transform.SetParent(transform, false);
            baseLawn.transform.localPosition = new Vector3(0, -0.3f, 0);
            baseLawn.transform.localScale = new Vector3(parkRadius * 2, 0.6f, parkRadius * 2);
            ApplyColor(baseLawn, lawnColor, 0.65f);
        }

        private void BuildTerracedMounds()
        {
            GameObject moundsRoot = new GameObject("TerracedGardenMounds");
            moundsRoot.transform.SetParent(transform, false);

            var moundDefs = new (Vector3 pos, float radius, float height, Color color)[]
            {
                (new Vector3(-15f, 0.25f, -12f), 9.0f, 0.5f, lawnDeepColor),
                (new Vector3(-16f, 0.65f, -13f), 5.5f, 0.8f, pathEdgeColor),
                (new Vector3(16f, 0.22f, -12f), 8.5f, 0.45f, lawnDeepColor),
                (new Vector3(17f, 0.62f, -13f), 5.0f, 0.8f, pathEdgeColor),
                (new Vector3(16f, 0.2f, 13f), 9.0f, 0.4f, lawnDeepColor),
                (new Vector3(-16f, 0.22f, 14f), 8.0f, 0.45f, lawnDeepColor),
                (new Vector3(-18f, 0.18f, 1f), 7.0f, 0.35f, lawnDeepColor),
                (new Vector3(19f, 0.18f, 1f), 6.5f, 0.35f, lawnDeepColor)
            };

            foreach (var m in moundDefs)
            {
                GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mound.name = "GardenMound";
                mound.transform.SetParent(moundsRoot.transform, false);
                mound.transform.localPosition = m.pos;
                mound.transform.localScale = new Vector3(m.radius * 2, m.height, m.radius * 2);
                ApplyColor(mound, m.color, 0.6f);
            }
        }

        private void BuildLotusPond()
        {
            GameObject pondRoot = new GameObject("LotusPondWater");
            pondRoot.transform.SetParent(transform, false);

            // Circular basin
            GameObject pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pond.name = "PondSurface";
            pond.transform.SetParent(pondRoot.transform, false);
            pond.transform.localPosition = new Vector3(2f, 0.05f, -3f);
            pond.transform.localScale = new Vector3(14.4f, 0.1f, 14.4f);
            ApplyColor(pond, pondWaterColor, 0.1f, true);

            // Stream arm
            GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stream.name = "StreamArm";
            stream.transform.SetParent(pondRoot.transform, false);
            stream.transform.localPosition = new Vector3(2f, 0.05f, -9f);
            stream.transform.localScale = new Vector3(4.2f, 0.1f, 14f);
            ApplyColor(stream, pondWaterColor, 0.1f, true);

            // Pink quartz river pebbles (strictly NO leaves)
            int stoneCount = 36;
            for (int i = 0; i < stoneCount; i++)
            {
                float angle = (i / (float)stoneCount) * Mathf.PI * 2f;
                float r = 7.3f + Mathf.Sin(i * 4.2f) * 0.4f;
                Vector3 stonePos = new Vector3(2f + Mathf.Cos(angle) * r, 0.15f, -3f + Mathf.Sin(angle) * r);

                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = $"Pebble_{i}";
                stone.transform.SetParent(pondRoot.transform, false);
                stone.transform.localPosition = stonePos;
                stone.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
                ApplyColor(stone, i % 2 == 0 ? blushPathColor : lawnDeepColor, 0.4f);
            }
        }

        private void BuildArchedFootbridge()
        {
            GameObject bridgeRoot = new GameObject("CherryBlossomArchedBridge");
            bridgeRoot.transform.SetParent(transform, false);
            bridgeRoot.transform.localPosition = new Vector3(2f, 0, -3f);

            int archSteps = 24;
            for (int i = 0; i < archSteps; i++)
            {
                float a1 = (i / (float)archSteps) * Mathf.PI;
                float a2 = ((i + 1) / (float)archSteps) * Mathf.PI;
                float p1x = Mathf.Cos(a1) * 4.5f;
                float p1y = Mathf.Sin(a1) * 1.45f;
                float p2x = Mathf.Cos(a2) * 4.5f;
                float p2y = Mathf.Sin(a2) * 1.45f;

                Vector3 mid = new Vector3((p1x + p2x) * 0.5f, (p1y + p2y) * 0.5f + 0.06f, 0);
                float angleDeg = Mathf.Atan2(p2y - p1y, p2x - p1x) * Mathf.Rad2Deg;

                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = $"BridgePlank_{i}";
                plank.transform.SetParent(bridgeRoot.transform, false);
                plank.transform.localPosition = mid;
                plank.transform.localRotation = Quaternion.Euler(0, 0, angleDeg);
                plank.transform.localScale = new Vector3(0.6f, 0.12f, 2.5f);
                ApplyColor(plank, bridgeWoodColor, 0.35f);

                // Railing posts
                foreach (float zOffset in new float[] { -1.2f, 1.2f })
                {
                    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    post.name = "RailingPost";
                    post.transform.SetParent(bridgeRoot.transform, false);
                    post.transform.localPosition = mid + new Vector3(0, 0.45f, zOffset);
                    post.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
                    ApplyColor(post, marshmallowColor, 0.25f);
                }
            }
        }

        private void BuildCentralFountain()
        {
            GameObject fountainRoot = new GameObject("TieredBlossomFountain");
            fountainRoot.transform.SetParent(transform, false);
            fountainRoot.transform.localPosition = new Vector3(0, 0, 7.5f);

            // Plaza base
            GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            plaza.name = "FountainPlaza";
            plaza.transform.SetParent(fountainRoot.transform, false);
            plaza.transform.localPosition = new Vector3(0, 0.03f, 0);
            plaza.transform.localScale = new Vector3(11.6f, 0.06f, 11.6f);
            ApplyColor(plaza, blushPathColor, 0.45f);

            // Bottom Basin
            GameObject basin1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basin1.name = "BasinTier1";
            basin1.transform.SetParent(fountainRoot.transform, false);
            basin1.transform.localPosition = new Vector3(0, 0.32f, 0);
            basin1.transform.localScale = new Vector3(6.8f, 0.55f, 6.8f);
            ApplyColor(basin1, fountainClayColor, 0.3f);

            // Water Tier 1
            GameObject water1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water1.name = "WaterTier1";
            water1.transform.SetParent(fountainRoot.transform, false);
            water1.transform.localPosition = new Vector3(0, 0.55f, 0);
            water1.transform.localScale = new Vector3(6.4f, 0.1f, 6.4f);
            ApplyColor(water1, pondWaterColor, 0.1f, true);

            // Stem 1
            GameObject stem1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem1.name = "Stem1";
            stem1.transform.SetParent(fountainRoot.transform, false);
            stem1.transform.localPosition = new Vector3(0, 0.9f, 0);
            stem1.transform.localScale = new Vector3(1.2f, 0.9f, 1.2f);
            ApplyColor(stem1, marshmallowColor, 0.25f);

            // Middle Basin Tier 2
            GameObject basin2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basin2.name = "BasinTier2";
            basin2.transform.SetParent(fountainRoot.transform, false);
            basin2.transform.localPosition = new Vector3(0, 1.45f, 0);
            basin2.transform.localScale = new Vector3(3.8f, 0.38f, 3.8f);
            ApplyColor(basin2, fountainClayColor, 0.3f);

            // Water Tier 2
            GameObject water2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water2.name = "WaterTier2";
            water2.transform.SetParent(fountainRoot.transform, false);
            water2.transform.localPosition = new Vector3(0, 1.6f, 0);
            water2.transform.localScale = new Vector3(3.6f, 0.08f, 3.6f);
            ApplyColor(water2, pondWaterColor, 0.1f, true);

            // Top Spout Dome
            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "BubblingWaterDome";
            dome.transform.SetParent(fountainRoot.transform, false);
            dome.transform.localPosition = new Vector3(0, 2.1f, 0);
            dome.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            ApplyColor(dome, pondWaterColor, 0.1f, true);
        }

        private void BuildMeanderingPaths()
        {
            GameObject pathsRoot = new GameObject("MeanderingBlushPaths");
            pathsRoot.transform.SetParent(transform, false);

            var pathConfigs = new (Vector3 pos, Vector3 size, float rotY)[]
            {
                (new Vector3(-6.5f, 0.02f, 6.5f), new Vector3(3.2f, 0.04f, 16f), 17f),
                (new Vector3(1.2f, 0.02f, 1.8f), new Vector3(3.0f, 0.04f, 12f), -8.5f),
                (new Vector3(8.5f, 0.02f, -4.5f), new Vector3(2.8f, 0.04f, 16f), 45f),
                (new Vector3(-4.0f, 0.02f, -10.0f), new Vector3(2.8f, 0.04f, 14f), -35f),
                (new Vector3(8.0f, 0.02f, 9.5f), new Vector3(2.8f, 0.04f, 14f), -25f),
                (new Vector3(-14.0f, 0.02f, 0.0f), new Vector3(3.4f, 0.04f, 22f), 0f)
            };

            foreach (var p in pathConfigs)
            {
                GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
                path.name = "BlushStonePath";
                path.transform.SetParent(pathsRoot.transform, false);
                path.transform.localPosition = p.pos;
                path.transform.localRotation = Quaternion.Euler(0, p.rotY, 0);
                path.transform.localScale = p.size;
                ApplyColor(path, blushPathColor, 0.45f);
            }
        }

        private void ApplyColor(GameObject obj, Color col, float roughness = 0.5f, bool transparent = false)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = col;
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 1.0f - roughness);
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 1.0f - roughness);
                r.material = mat;
            }
        }
    }
}
