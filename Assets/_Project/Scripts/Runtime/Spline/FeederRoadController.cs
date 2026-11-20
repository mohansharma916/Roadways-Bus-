using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Controls the Left Feeder entrance road, Right Feeder exit road,
    /// White Stop Bar queue marking at X = 30, and physics trigger colliders.
    /// </summary>
    [ExecuteAlways]
    public class FeederRoadController : MonoBehaviour
    {
        [Header("Feeder Spline References")]
        [SerializeField] private CatmullRomSpline leftFeederSpline;
        [SerializeField] private SplineArcLengthTable leftFeederLut;
        [SerializeField] private CatmullRomSpline rightFeederSpline;
        [SerializeField] private SplineArcLengthTable rightFeederLut;

        [Header("Road Widths")]
        [SerializeField] private float roadWidth = GameConstants.RoadWidth;
        [SerializeField] private float curbWidth = GameConstants.WhiteCurbWidth;
        [SerializeField] private float shoulderWidth = GameConstants.DirtShoulderWidth;

        [Header("Stop Bar Configuration")]
        [SerializeField] private float stopBarPositionX = GameConstants.StopBarPositionX; // 30.0f
        [SerializeField] private float stopBarWidth = 2.5f;

        [Header("Materials (Auto-Created if Null)")]
        [SerializeField] private Material asphaltMaterial;
        [SerializeField] private Material curbMaterial;
        [SerializeField] private Material shoulderMaterial;
        [SerializeField] private Material stopBarMaterial;

        public SplineArcLengthTable LeftFeederLut => leftFeederLut;
        public SplineArcLengthTable RightFeederLut => rightFeederLut;

        [ContextMenu("Build Feeder Roads")]
        public void BuildFeederRoads()
        {
            ClearFeederRoads();
            EnsureMaterials();
            SetupFeederSplines();
            BuildFeederMeshes(leftFeederLut, "LeftFeeder");
            BuildFeederMeshes(rightFeederLut, "RightFeeder");
            BuildStopBar();
            BuildTriggerColliders();
        }

        [ContextMenu("Clear Feeder Roads")]
        public void ClearFeederRoads()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Gen_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private void EnsureMaterials()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            if (asphaltMaterial == null)
                asphaltMaterial = new Material(unlitShader) { name = "M_FeederAsphalt", color = GameConstants.AsphaltColor };
            if (curbMaterial == null)
                curbMaterial = new Material(unlitShader) { name = "M_FeederCurb", color = GameConstants.WhiteCurbColor };
            if (shoulderMaterial == null)
                shoulderMaterial = new Material(unlitShader) { name = "M_FeederShoulder", color = GameConstants.DirtShoulderColor };
            if (stopBarMaterial == null)
                stopBarMaterial = new Material(unlitShader) { name = "M_FeederStopBar", color = Color.white };
        }

        private void SetupFeederSplines()
        {
            // Left Feeder: Curve from X = -50, Y = 102 to merge at X = 88, Y = 134
            if (leftFeederSpline == null)
            {
                GameObject lObj = new GameObject("LeftFeederSpline");
                lObj.transform.SetParent(transform, false);
                leftFeederSpline = lObj.AddComponent<CatmullRomSpline>();
                leftFeederLut = lObj.AddComponent<SplineArcLengthTable>();
            }

            Vector3[] leftPts = new Vector3[]
            {
                new Vector3(-60f, 0f, 102f),
                new Vector3(-50f, 0f, 102f),
                new Vector3(10f, 0f, 102f),
                new Vector3(50f, 0f, 112f),
                new Vector3(88f, 0f, 134f),
                new Vector3(105f, 0f, 150f)
            };
            leftFeederSpline.SetControlPoints(leftPts, false);
            leftFeederLut.RebuildTable();

            // Right Feeder: Curve from X = 412, Y = 134 to X = 510, Y = 102
            if (rightFeederSpline == null)
            {
                GameObject rObj = new GameObject("RightFeederSpline");
                rObj.transform.SetParent(transform, false);
                rightFeederSpline = rObj.AddComponent<CatmullRomSpline>();
                rightFeederLut = rObj.AddComponent<SplineArcLengthTable>();
            }

            Vector3[] rightPts = new Vector3[]
            {
                new Vector3(395f, 0f, 150f),
                new Vector3(412f, 0f, 134f),
                new Vector3(445f, 0f, 112f),
                new Vector3(480f, 0f, 102f),
                new Vector3(510f, 0f, 102f),
                new Vector3(520f, 0f, 102f)
            };
            rightFeederSpline.SetControlPoints(rightPts, false);
            rightFeederLut.RebuildTable();
        }

        private void BuildFeederMeshes(SplineArcLengthTable lut, string prefix)
        {
            if (lut == null || !lut.IsValid) return;

            GameObject group = new GameObject($"Gen_{prefix}_Meshes");
            group.transform.SetParent(transform, false);

            int segments = 60;
            float totalLen = lut.TotalLength;

            CreateOpenRibbon(group, $"{prefix}_Asphalt", lut, -roadWidth * 0.5f, roadWidth * 0.5f, 0.02f, asphaltMaterial, segments);
            CreateOpenRibbon(group, $"{prefix}_InnerCurb", lut, -roadWidth * 0.5f - curbWidth, -roadWidth * 0.5f, 0.025f, curbMaterial, segments);
            CreateOpenRibbon(group, $"{prefix}_OuterCurb", lut, roadWidth * 0.5f, roadWidth * 0.5f + curbWidth, 0.025f, curbMaterial, segments);
            CreateOpenRibbon(group, $"{prefix}_InnerShoulder", lut, -roadWidth * 0.5f - curbWidth - shoulderWidth, -roadWidth * 0.5f - curbWidth, 0.015f, shoulderMaterial, segments);
            CreateOpenRibbon(group, $"{prefix}_OuterShoulder", lut, roadWidth * 0.5f + curbWidth, roadWidth * 0.5f + curbWidth + shoulderWidth, 0.015f, shoulderMaterial, segments);
        }

        private void CreateOpenRibbon(GameObject parent, string name, SplineArcLengthTable lut, float offsetInner, float offsetOuter, float height, Material mat, int segments)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent.transform, false);

            int vertCount = (segments + 1) * 2;
            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int[] tris = new int[segments * 6];

            float totalLen = lut.TotalLength;

            for (int i = 0; i <= segments; i++)
            {
                float s = (i / (float)segments) * totalLen;
                lut.SampleAtDistance(s, out Vector3 pos, out Vector3 tan, out Vector3 norm);

                verts[i * 2] = pos + norm * offsetInner + Vector3.up * height;
                verts[i * 2 + 1] = pos + norm * offsetOuter + Vector3.up * height;

                uvs[i * 2] = new Vector2(0f, s / 20f);
                uvs[i * 2 + 1] = new Vector2(1f, s / 20f);
            }

            for (int i = 0; i < segments; i++)
            {
                int triIdx = i * 6;
                int i0 = i * 2;
                int i1 = (i + 1) * 2;
                int i2 = i * 2 + 1;
                int i3 = (i + 1) * 2 + 1;

                tris[triIdx + 0] = i0;
                tris[triIdx + 1] = i1;
                tris[triIdx + 2] = i2;

                tris[triIdx + 3] = i2;
                tris[triIdx + 4] = i1;
                tris[triIdx + 5] = i3;
            }

            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            MeshFilter mf = obj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = obj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        private void BuildStopBar()
        {
            // Stop Bar line at X = 30 across the feeder road (from Y = 76 to 144)
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bar.name = "Gen_FeederStopBar";
            bar.transform.SetParent(transform, false);

            float spanY = roadWidth;
            bar.transform.localPosition = new Vector3(stopBarPositionX, 0.04f, 107f);
            bar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bar.transform.localScale = new Vector3(stopBarWidth, spanY, 1f);

            Renderer r = bar.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = stopBarMaterial;

            Collider c = bar.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void BuildTriggerColliders()
        {
            // 1. Feeder merge trigger
            GameObject mergeTrigger = new GameObject("Gen_Trigger_FeederMerge");
            mergeTrigger.transform.SetParent(transform, false);
            mergeTrigger.transform.localPosition = new Vector3(88f, 0.5f, 134f);
            BoxCollider colMerge = mergeTrigger.AddComponent<BoxCollider>();
            colMerge.isTrigger = true;
            colMerge.size = new Vector3(15f, 2f, 15f);

            // 2. Feeder stop bar boundary trigger
            GameObject stopTrigger = new GameObject("Gen_Trigger_FeederStopLine");
            stopTrigger.transform.SetParent(transform, false);
            stopTrigger.transform.localPosition = new Vector3(stopBarPositionX, 0.5f, 107f);
            BoxCollider colStop = stopTrigger.AddComponent<BoxCollider>();
            colStop.isTrigger = true;
            colStop.size = new Vector3(4f, 2f, roadWidth);
        }
    }
}
