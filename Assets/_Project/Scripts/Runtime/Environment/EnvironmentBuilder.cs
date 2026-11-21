using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Procedural Tropical Beach Resort Environment Generator (Phase 4).
    /// Faithfully reproduces the commercial benchmark visual style:
    /// - Animated crystal turquoise ocean with gentle waves & shoreline foam line (Z = -44 to -110, Y = 0 to 220).
    /// - Warm sun-baked golden sand beach terrain (#f5cd79) surrounding the circuit (Z = -44 to -6, Y = 220 to 650).
    /// - Cool clean studio floor (#dcdde1) under the parking bays and depot (Z = -6 to 28, Y = 650 to 1600).
    /// - Low-poly 3D resort props: Curved palm trees, striped beach umbrellas,
    ///   sun loungers, surfboards, and lifebuoys outside roadway boundaries.
    /// </summary>
    [ExecuteAlways]
    public class EnvironmentBuilder : MonoBehaviour
    {
        [Header("Ground Dimensions (Legacy Serialized for IL2CPP Parity)")]
        [SerializeField] private float groundWidth = 80f;
        [SerializeField] private float groundLength = 120f;

        [Header("Pathways Configuration (Legacy Serialized for IL2CPP Parity)")]
        [SerializeField] private int concentricRingCount = 3;
        [SerializeField] private float innerRingRadius = 14f;
        [SerializeField] private float ringStepRadius = 10f;
        [SerializeField] private float pathwayRibbonWidth = 1.6f;

        [Header("Perimeter Stones (Legacy Serialized for IL2CPP Parity)")]
        [SerializeField] private int stoneCount = 16;
        [SerializeField] private float stonePerimeterRadius = 38f;

        [Header("Materials (Legacy Serialized for IL2CPP Parity)")]
        [SerializeField] private Material lawnMaterial;
        [SerializeField] private Material pathwayMaterial;
        [SerializeField] private Material stoneMaterial;

        // Phase 4 NonSerialized Runtime Dimensions & Materials (Zero IL2CPP layout changes)
        [System.NonSerialized] private float terrainWidth = 85f;
        [System.NonSerialized] private float shorelineZ = -44.0f;
        [System.NonSerialized] private float oceanTopZ = -110.0f;
        [System.NonSerialized] private float baysDividerZ = -6.0f;
        [System.NonSerialized] private float depotBottomZ = 28.0f;

        [System.NonSerialized] private Material oceanMaterial;
        [System.NonSerialized] private Material sandMaterial;
        [System.NonSerialized] private Material depotFloorMaterial;
        [System.NonSerialized] private Material foamRibbonMaterial;
        [System.NonSerialized] private Material palmTrunkMaterial;
        [System.NonSerialized] private Material palmFrondMaterial;
        [System.NonSerialized] private Material umbrellaBlueMaterial;
        [System.NonSerialized] private Material umbrellaYellowMaterial;
        [System.NonSerialized] private Material propWhiteMaterial;
        [System.NonSerialized] private Material propRedMaterial;
        [System.NonSerialized] private Material surfboardMaterial;

        private void Start()
        {
            BuildEnvironment();
        }

        [ContextMenu("Build Environment")]
        public void BuildEnvironment()
        {
            ClearExistingGeometry();
            EnsureMaterials();
            BuildOceanWater();
            BuildBeachSandGround();
            BuildDepotFloor();
            BuildShorelineFoamRibbon();
            BuildResortProps();
        }

        [ContextMenu("Clear Environment")]
        public void ClearExistingGeometry()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }
        }

        private void EnsureMaterials()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? unlitShader;
            Shader oceanShader = Shader.Find("Roadways/OceanWater") ?? unlitShader;

            if (oceanMaterial == null || oceanMaterial.shader != oceanShader)
            {
                oceanMaterial = new Material(oceanShader) { name = "M_ResortOceanWater" };
            }
            oceanMaterial.color = new Color32(0x02, 0x88, 0xD1, 0xFF); // Cyan-blue ocean
            if (oceanMaterial.HasProperty("_ShorelineZ")) oceanMaterial.SetFloat("_ShorelineZ", shorelineZ);

            if (sandMaterial == null)
            {
                sandMaterial = new Material(litShader)
                {
                    name = "M_ResortBeachSand",
                    color = GameConstants.LawnColor
                };
            }
            sandMaterial.color = GameConstants.LawnColor;

            if (depotFloorMaterial == null)
            {
                depotFloorMaterial = new Material(litShader)
                {
                    name = "M_ResortDepotFloor",
                    color = GameConstants.DepotFloorColor
                };
            }
            depotFloorMaterial.color = GameConstants.DepotFloorColor;

            if (foamRibbonMaterial == null)
            {
                foamRibbonMaterial = new Material(unlitShader)
                {
                    name = "M_ShorelineFoam",
                    color = new Color(1f, 1f, 1f, 0.95f)
                };
            }

            if (palmTrunkMaterial == null)
                palmTrunkMaterial = new Material(litShader) { name = "M_PalmTrunk", color = new Color32(0x79, 0x55, 0x48, 0xFF) };
            if (palmFrondMaterial == null)
                palmFrondMaterial = new Material(litShader) { name = "M_PalmFronds", color = new Color32(0x27, 0xAE, 0x60, 0xFF) };
            if (umbrellaBlueMaterial == null)
                umbrellaBlueMaterial = new Material(litShader) { name = "M_UmbrellaBlue", color = new Color32(0x29, 0x80, 0xB9, 0xFF) };
            if (umbrellaYellowMaterial == null)
                umbrellaYellowMaterial = new Material(litShader) { name = "M_UmbrellaYellow", color = new Color32(0xF3, 0x9C, 0x12, 0xFF) };
            if (propWhiteMaterial == null)
                propWhiteMaterial = new Material(litShader) { name = "M_PropWhite", color = new Color32(0xEC, 0xF0, 0xF1, 0xFF) };
            if (propRedMaterial == null)
                propRedMaterial = new Material(litShader) { name = "M_PropRed", color = new Color32(0xE7, 0x4C, 0x3C, 0xFF) };
            if (surfboardMaterial == null)
                surfboardMaterial = new Material(litShader) { name = "M_Surfboard", color = new Color32(0xFF, 0x76, 0x75, 0xFF) };
        }

        private void BuildOceanWater()
        {
            GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ocean.name = "ResortOceanQuad";
            ocean.transform.SetParent(transform, false);
            // Covers from shoreline (Z = -44.0) to top of screen horizon (Z = -110.0)
            float oceanLen = Mathf.Abs(oceanTopZ - shorelineZ);
            float oceanCenterZ = (shorelineZ + oceanTopZ) * 0.5f;
            ocean.transform.localPosition = new Vector3(0f, -0.04f, oceanCenterZ);
            ocean.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ocean.transform.localScale = new Vector3(terrainWidth, oceanLen, 1f);

            Renderer r = ocean.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = oceanMaterial;
            Collider c = ocean.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void BuildBeachSandGround()
        {
            GameObject beach = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beach.name = "ResortBeachSandQuad";
            beach.transform.SetParent(transform, false);
            // Covers from shoreline (Z = -44.0) down to bays/terminal (Z = -6.0)
            float beachLen = Mathf.Abs(baysDividerZ - shorelineZ);
            float beachCenterZ = (shorelineZ + baysDividerZ) * 0.5f;
            beach.transform.localPosition = new Vector3(0f, -0.06f, beachCenterZ);
            beach.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            beach.transform.localScale = new Vector3(terrainWidth, beachLen, 1f);

            Renderer r = beach.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = sandMaterial;
            Collider c = beach.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void BuildDepotFloor()
        {
            GameObject depot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            depot.name = "ResortDepotFloorQuad";
            depot.transform.SetParent(transform, false);
            // Covers from bays (Z = -6.0) down to depot bottom (Z = 28.0)
            float depotLen = Mathf.Abs(depotBottomZ - baysDividerZ);
            float depotCenterZ = (baysDividerZ + depotBottomZ) * 0.5f;
            depot.transform.localPosition = new Vector3(0f, -0.05f, depotCenterZ);
            depot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            depot.transform.localScale = new Vector3(terrainWidth, depotLen, 1f);

            Renderer r = depot.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = depotFloorMaterial;
            Collider c = depot.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void BuildShorelineFoamRibbon()
        {
            GameObject foamObj = new GameObject("ShorelineFoamRibbon");
            foamObj.transform.SetParent(transform, false);
            foamObj.transform.localPosition = new Vector3(0f, -0.03f, shorelineZ);

            MeshFilter mf = foamObj.AddComponent<MeshFilter>();
            MeshRenderer mr = foamObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = foamRibbonMaterial;

            int segs = 48;
            Vector3[] verts = new Vector3[(segs + 1) * 2];
            int[] tris = new int[segs * 6];
            float halfW = terrainWidth * 0.5f;

            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float x = Mathf.Lerp(-halfW, halfW, t);
                float waveOffset = Mathf.Sin(t * 12f) * 0.45f + Mathf.Cos(t * 22f) * 0.20f;
                verts[i * 2 + 0] = new Vector3(x, 0f, waveOffset - 0.65f);
                verts[i * 2 + 1] = new Vector3(x, 0f, waveOffset + 0.55f);

                if (i < segs)
                {
                    int b = i * 2;
                    tris[i * 6 + 0] = b;
                    tris[i * 6 + 1] = b + 1;
                    tris[i * 6 + 2] = b + 2;

                    tris[i * 6 + 3] = b + 1;
                    tris[i * 6 + 4] = b + 3;
                    tris[i * 6 + 5] = b + 2;
                }
            }

            Mesh mesh = new Mesh { name = "ShorelineFoamMesh" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
        }

        private void BuildResortProps()
        {
            GameObject propsGroup = new GameObject("ResortLifeProps");
            propsGroup.transform.SetParent(transform, false);

            // Left Beach Shoreline Props (X < -8, Z between -38 and -12)
            CreatePalmTree(propsGroup.transform, new Vector3(-11.5f, 0f, -36f), 1.25f, 18f);
            CreatePalmTree(propsGroup.transform, new Vector3(-14.0f, 0f, -22f), 1.05f, -22f);
            CreateBeachUmbrella(propsGroup.transform, new Vector3(-10.2f, 0f, -18f), umbrellaBlueMaterial, 12f);
            CreateSunLounger(propsGroup.transform, new Vector3(-9.2f, 0f, -16f), umbrellaBlueMaterial, 28f);
            CreateSurfboard(propsGroup.transform, new Vector3(-10.8f, 0f, -30f), 32f);

            // Right Beach Shoreline Props (X > 8, Z between -38 and -12)
            CreatePalmTree(propsGroup.transform, new Vector3(11.5f, 0f, -35f), 1.25f, -15f);
            CreateBeachUmbrella(propsGroup.transform, new Vector3(10.2f, 0f, -18f), umbrellaYellowMaterial, -14f);
            CreateSunLounger(propsGroup.transform, new Vector3(9.2f, 0f, -16f), umbrellaYellowMaterial, -25f);
            CreateLifebuoy(propsGroup.transform, new Vector3(8.5f, 0f, -14f));
            CreateSurfboard(propsGroup.transform, new Vector3(10.8f, 0f, -28f), -28f);
        }

        private void CreatePalmTree(Transform parent, Vector3 position, float scale, float leanAngle)
        {
            GameObject tree = new GameObject("PalmTree");
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localScale = Vector3.one * scale;

            float segH = 0.85f;
            Vector3 currPos = Vector3.zero;
            for (int s = 0; s < 4; s++)
            {
                GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = $"TrunkSeg_{s}";
                seg.transform.SetParent(tree.transform, false);
                float radius = Mathf.Lerp(0.24f, 0.14f, s / 3f);
                seg.transform.localScale = new Vector3(radius * 2f, segH * 0.5f, radius * 2f);
                float leanOffset = Mathf.Sin((s + 1) * 0.4f) * 0.18f;
                Vector3 nextPos = currPos + new Vector3(Mathf.Sin(leanAngle * Mathf.Deg2Rad) * leanOffset, segH, Mathf.Cos(leanAngle * Mathf.Deg2Rad) * leanOffset * 0.5f);
                seg.transform.localPosition = (currPos + nextPos) * 0.5f;
                seg.transform.localRotation = Quaternion.Euler(leanAngle * 0.35f * (s + 1), 0f, -leanAngle * 0.25f);
                currPos = nextPos;

                Renderer r = seg.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = palmTrunkMaterial;
                Collider c = seg.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
            }

            GameObject crown = new GameObject("CrownFronds");
            crown.transform.SetParent(tree.transform, false);
            crown.transform.localPosition = currPos;

            Mesh frondMesh = CreatePalmFrondMesh();
            for (int f = 0; f < 6; f++)
            {
                float rot = f * 60f + (f % 2 == 0 ? 0f : 12f);
                GameObject frondObj = new GameObject($"Frond_{f}");
                frondObj.transform.SetParent(crown.transform, false);
                frondObj.transform.localRotation = Quaternion.Euler(18f, rot, -22f);

                MeshFilter mf = frondObj.AddComponent<MeshFilter>();
                mf.sharedMesh = frondMesh;
                MeshRenderer mr = frondObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = palmFrondMaterial;
            }
        }

        private Mesh CreatePalmFrondMesh()
        {
            Mesh mesh = new Mesh { name = "PalmFrondMesh" };
            Vector3[] verts = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(-0.35f, 0.18f, 1.1f),
                new Vector3(0.35f, 0.18f, 1.1f),
                new Vector3(0f, -0.25f, 2.3f),
                new Vector3(0f, 0.25f, 1.1f)
            };
            int[] tris = new int[]
            {
                0, 1, 4,
                0, 4, 2,
                4, 1, 3,
                4, 3, 2
            };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateBeachUmbrella(Transform parent, Vector3 position, Material canopyMat, float tiltAngle)
        {
            GameObject umbrella = new GameObject("BeachUmbrella");
            umbrella.transform.SetParent(parent, false);
            umbrella.transform.localPosition = position;
            umbrella.transform.localRotation = Quaternion.Euler(tiltAngle, 45f, tiltAngle * 0.5f);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(umbrella.transform, false);
            pole.transform.localScale = new Vector3(0.06f, 1.15f, 0.06f);
            pole.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            Renderer poleR = pole.GetComponent<Renderer>();
            if (poleR != null) poleR.sharedMaterial = propWhiteMaterial;
            Collider poleC = pole.GetComponent<Collider>();
            if (poleC != null) DestroyImmediate(poleC);

            GameObject canopy = new GameObject("Canopy");
            canopy.transform.SetParent(umbrella.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 2.25f, 0f);

            MeshFilter mf = canopy.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateCanopyConeMesh(1.35f, 0.45f, 8);
            MeshRenderer mr = canopy.AddComponent<MeshRenderer>();
            mr.sharedMaterial = canopyMat;
        }

        private Mesh CreateCanopyConeMesh(float radius, float height, int sides)
        {
            Mesh mesh = new Mesh { name = "CanopyConeMesh" };
            Vector3[] verts = new Vector3[sides + 2];
            int[] tris = new int[sides * 3];

            verts[0] = new Vector3(0f, height, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            verts[sides + 1] = new Vector3(0f, 0f, 0f);

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = next + 1;
                tris[i * 3 + 2] = i + 1;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateSunLounger(Transform parent, Vector3 position, Material cushionMat, float yRot)
        {
            GameObject lounger = new GameObject("SunLounger");
            lounger.transform.SetParent(parent, false);
            lounger.transform.localPosition = position;
            lounger.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "BedFrame";
            frame.transform.SetParent(lounger.transform, false);
            frame.transform.localScale = new Vector3(0.75f, 0.12f, 1.85f);
            frame.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            Renderer frR = frame.GetComponent<Renderer>();
            if (frR != null) frR.sharedMaterial = propWhiteMaterial;
            Collider frC = frame.GetComponent<Collider>();
            if (frC != null) DestroyImmediate(frC);

            GameObject headrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headrest.name = "Headrest";
            headrest.transform.SetParent(lounger.transform, false);
            headrest.transform.localScale = new Vector3(0.68f, 0.10f, 0.65f);
            headrest.transform.localPosition = new Vector3(0f, 0.22f, -0.55f);
            headrest.transform.localRotation = Quaternion.Euler(24f, 0f, 0f);
            Renderer hrR = headrest.GetComponent<Renderer>();
            if (hrR != null) hrR.sharedMaterial = cushionMat;
            Collider hrC = headrest.GetComponent<Collider>();
            if (hrC != null) DestroyImmediate(hrC);

            GameObject mattress = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mattress.name = "Mattress";
            mattress.transform.SetParent(lounger.transform, false);
            mattress.transform.localScale = new Vector3(0.68f, 0.08f, 1.15f);
            mattress.transform.localPosition = new Vector3(0f, 0.14f, 0.30f);
            Renderer mR = mattress.GetComponent<Renderer>();
            if (mR != null) mR.sharedMaterial = cushionMat;
            Collider mC = mattress.GetComponent<Collider>();
            if (mC != null) DestroyImmediate(mC);
        }

        private void CreateSurfboard(Transform parent, Vector3 position, float yRot)
        {
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Surfboard";
            board.transform.SetParent(parent, false);
            board.transform.localPosition = position + new Vector3(0f, 0.65f, 0f);
            board.transform.localRotation = Quaternion.Euler(34f, yRot, 12f);
            board.transform.localScale = new Vector3(0.42f, 1.75f, 0.08f);

            Renderer r = board.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = surfboardMaterial;
            Collider c = board.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void CreateLifebuoy(Transform parent, Vector3 position)
        {
            GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            buoy.name = "Lifebuoy";
            buoy.transform.SetParent(parent, false);
            buoy.transform.localPosition = position + new Vector3(0f, 0.06f, 0f);
            buoy.transform.localScale = new Vector3(0.68f, 0.12f, 0.68f);

            Renderer r = buoy.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = propRedMaterial;
            Collider c = buoy.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }
    }
}
