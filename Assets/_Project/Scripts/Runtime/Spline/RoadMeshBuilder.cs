using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Procedural Road Mesh Extruder.
    /// Builds:
    /// - 82-unit Asphalt Road Surface (#525769)
    /// - 2-unit White Curb Strips (#e6e9ee) on inner and outer edges
    /// - 11-unit Tan Dirt Shoulders (#cbb27a) flanking exterior curbs
    /// - White dashed centerline ribbon (#ffffff, alpha 0.65)
    /// - Central grass island infill (#fff1f2)
    /// </summary>
    [RequireComponent(typeof(CatmullRomSpline), typeof(SplineArcLengthTable))]
    [ExecuteAlways]
    public class RoadMeshBuilder : MonoBehaviour
    {
        [Header("Road Dimensions")]
        [SerializeField] private float roadWidth = GameConstants.RoadWidth;
        [SerializeField] private float curbWidth = GameConstants.WhiteCurbWidth;
        [SerializeField] private float shoulderWidth = GameConstants.DirtShoulderWidth;
        [SerializeField] private float centerLineWidth = 1.2f;
        [SerializeField] private int extrusionSegments = 300;

        [Header("Materials (Auto-Created if Null)")]
        [SerializeField] private Material asphaltMaterial;
        [SerializeField] private Material curbMaterial;
        [SerializeField] private Material shoulderMaterial;
        [SerializeField] private Material dashedLineMaterial;
        [SerializeField] private Material islandLawnMaterial;

        private CatmullRomSpline spline;
        private SplineArcLengthTable lut;

        private void Awake()
        {
            spline = GetComponent<CatmullRomSpline>();
            lut = GetComponent<SplineArcLengthTable>();
        }

        [ContextMenu("Build Road Network Meshes")]
        public void BuildRoadNetworkMeshes()
        {
            if (spline == null) spline = GetComponent<CatmullRomSpline>();
            if (lut == null) lut = GetComponent<SplineArcLengthTable>();
            if (lut != null) lut.RebuildTable();

            ClearGeneratedMeshes();
            EnsureMaterials();

            BuildRoadRibbon();
            BuildCurbRibbons();
            BuildShoulderRibbons();
            BuildDashedCenterline();

            if (spline.IsClosedLoop)
            {
                BuildCentralIsland();
            }
        }

        [ContextMenu("Clear Road Meshes")]
        public void ClearGeneratedMeshes()
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
            {
                asphaltMaterial = new Material(unlitShader) { name = "M_TrackAsphalt", color = GameConstants.AsphaltColor };
                asphaltMaterial.SetInt("_Cull", 0);
            }
            else
            {
                asphaltMaterial.color = GameConstants.AsphaltColor;
            }

            if (curbMaterial == null)
            {
                curbMaterial = new Material(unlitShader) { name = "M_TrackCurb", color = GameConstants.WhiteCurbColor };
                curbMaterial.SetInt("_Cull", 0);
            }
            else
            {
                curbMaterial.color = GameConstants.WhiteCurbColor;
            }

            if (shoulderMaterial == null)
            {
                shoulderMaterial = new Material(unlitShader) { name = "M_TrackShoulder", color = GameConstants.DirtShoulderColor };
                shoulderMaterial.SetInt("_Cull", 0);
            }
            else
            {
                shoulderMaterial.color = GameConstants.DirtShoulderColor;
            }

            if (dashedLineMaterial == null)
            {
                dashedLineMaterial = new Material(unlitShader)
                {
                    name = "M_DashedCenterline",
                    color = new Color(1f, 1f, 1f, 0.65f)
                };
                dashedLineMaterial.SetFloat("_Surface", 1);
                dashedLineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dashedLineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dashedLineMaterial.SetInt("_ZWrite", 0);
                dashedLineMaterial.SetInt("_Cull", 0);
                dashedLineMaterial.renderQueue = 3000;
            }

            if (islandLawnMaterial == null)
            {
                islandLawnMaterial = new Material(unlitShader) { name = "M_CentralIslandLawn", color = GameConstants.LawnColor };
                islandLawnMaterial.SetInt("_Cull", 0);
            }
            else
            {
                islandLawnMaterial.color = GameConstants.LawnColor;
            }
        }

        private void BuildRoadRibbon()
        {
            CreateRibbonMesh("Gen_AsphaltSurface", -roadWidth * 0.5f, roadWidth * 0.5f, 0.02f, asphaltMaterial);
        }

        private void BuildCurbRibbons()
        {
            // Inner curb
            float innerStart = -roadWidth * 0.5f - curbWidth;
            float innerEnd = -roadWidth * 0.5f;
            CreateRibbonMesh("Gen_InnerCurb", innerStart, innerEnd, 0.025f, curbMaterial);

            // Outer curb
            float outerStart = roadWidth * 0.5f;
            float outerEnd = roadWidth * 0.5f + curbWidth;
            CreateRibbonMesh("Gen_OuterCurb", outerStart, outerEnd, 0.025f, curbMaterial);
        }

        private void BuildShoulderRibbons()
        {
            // Inner shoulder
            float innerStart = -roadWidth * 0.5f - curbWidth - shoulderWidth;
            float innerEnd = -roadWidth * 0.5f - curbWidth;
            CreateRibbonMesh("Gen_InnerShoulder", innerStart, innerEnd, 0.015f, shoulderMaterial);

            // Outer shoulder
            float outerStart = roadWidth * 0.5f + curbWidth;
            float outerEnd = roadWidth * 0.5f + curbWidth + shoulderWidth;
            CreateRibbonMesh("Gen_OuterShoulder", outerStart, outerEnd, 0.015f, shoulderMaterial);
        }

        private void BuildDashedCenterline()
        {
            float totalLen = lut.TotalLength;
            float dashLen = 10f;
            float gapLen = 8f;
            float period = dashLen + gapLen;

            int dashCount = Mathf.FloorToInt(totalLen / period);
            GameObject centerlineGroup = new GameObject("Gen_DashedCenterline");
            centerlineGroup.transform.SetParent(transform, false);

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            float halfW = centerLineWidth * 0.5f;

            for (int d = 0; d < dashCount; d++)
            {
                float sStart = d * period;
                float sEnd = sStart + dashLen;
                int subSteps = 6;

                int baseVert = verts.Count;

                for (int j = 0; j <= subSteps; j++)
                {
                    float s = Mathf.Lerp(sStart, sEnd, (float)j / subSteps);
                    lut.SampleAtDistance(s, out Vector3 pos, out Vector3 tan, out Vector3 norm);

                    Vector3 pL = pos - norm * halfW + Vector3.up * 0.035f;
                    Vector3 pR = pos + norm * halfW + Vector3.up * 0.035f;

                    verts.Add(pL);
                    verts.Add(pR);
                }

                for (int j = 0; j < subSteps; j++)
                {
                    int v0 = baseVert + j * 2;
                    int v1 = baseVert + (j + 1) * 2;
                    int v2 = baseVert + j * 2 + 1;
                    int v3 = baseVert + (j + 1) * 2 + 1;

                    tris.Add(v0);
                    tris.Add(v1);
                    tris.Add(v2);

                    tris.Add(v2);
                    tris.Add(v1);
                    tris.Add(v3);
                }
            }

            Mesh mesh = new Mesh { name = "CenterlineMesh" };
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();

            MeshFilter mf = centerlineGroup.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = centerlineGroup.AddComponent<MeshRenderer>();
            mr.sharedMaterial = dashedLineMaterial;
        }

        private void BuildCentralIsland()
        {
            // Fills the closed inner perimeter of the inner curb
            GameObject islandObj = new GameObject("Gen_CentralGrassIsland");
            islandObj.transform.SetParent(transform, false);

            List<Vector3> innerBoundary = new List<Vector3>();
            float innerOffset = -roadWidth * 0.5f - curbWidth - shoulderWidth;

            for (int i = 0; i < extrusionSegments; i++)
            {
                float s = (i / (float)extrusionSegments) * lut.TotalLength;
                lut.SampleAtDistance(s, out Vector3 pos, out Vector3 tan, out Vector3 norm);
                innerBoundary.Add(pos + norm * innerOffset + Vector3.up * 0.01f);
            }

            // Triangulate center fan
            Vector3 center = Vector3.zero;
            foreach (var p in innerBoundary) center += p;
            center /= Mathf.Max(1, innerBoundary.Count);

            List<Vector3> verts = new List<Vector3> { center };
            verts.AddRange(innerBoundary);

            List<int> tris = new List<int>();
            int count = innerBoundary.Count;

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                tris.Add(0);
                tris.Add(i + 1);
                tris.Add(next + 1);
            }

            Mesh mesh = new Mesh { name = "CentralIslandMesh" };
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();

            MeshFilter mf = islandObj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = islandObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = islandLawnMaterial;
        }

        private GameObject CreateRibbonMesh(string objName, float offsetInner, float offsetOuter, float heightOffset, Material mat)
        {
            GameObject ribbonObj = new GameObject(objName);
            ribbonObj.transform.SetParent(transform, false);

            bool isClosed = spline.IsClosedLoop;
            int n = extrusionSegments;
            int vertCount = (n + (isClosed ? 0 : 1)) * 2;

            Vector3[] verts = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            int triCount = n * 6;
            int[] tris = new int[triCount];

            float totalLen = lut.TotalLength;

            for (int i = 0; i < (isClosed ? n : n + 1); i++)
            {
                float s = (i / (float)n) * totalLen;
                lut.SampleAtDistance(s, out Vector3 pos, out Vector3 tan, out Vector3 norm);

                Vector3 pInner = pos + norm * offsetInner + Vector3.up * heightOffset;
                Vector3 pOuter = pos + norm * offsetOuter + Vector3.up * heightOffset;

                verts[i * 2] = pInner;
                verts[i * 2 + 1] = pOuter;

                float v = s / 20f;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % (isClosed ? n : n + 1);
                int triIdx = i * 6;

                int i0 = i * 2;
                int i1 = next * 2;
                int i2 = i * 2 + 1;
                int i3 = next * 2 + 1;

                tris[triIdx + 0] = i0;
                tris[triIdx + 1] = i1;
                tris[triIdx + 2] = i2;

                tris[triIdx + 3] = i2;
                tris[triIdx + 4] = i1;
                tris[triIdx + 5] = i3;
            }

            Mesh mesh = new Mesh { name = objName + "_Mesh" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            MeshFilter mf = ribbonObj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = ribbonObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            return ribbonObj;
        }
    }
}
