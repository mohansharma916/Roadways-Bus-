using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Generates alternating red (#e74c3c) and white (#ffffff) racing curb blocks
    /// along the 5 key inner apexes of the racing circuit.
    /// </summary>
    [RequireComponent(typeof(CatmullRomSpline), typeof(SplineArcLengthTable))]
    [ExecuteAlways]
    public class ApexKerbGenerator : MonoBehaviour
    {
        [System.Serializable]
        public struct ApexDefinition
        {
            public string name;
            [Range(0f, 1f)] public float normalizedPosition; // Parameter u along spline
            public float arcLengthSpan;                       // Total arc length of curb
            public int blockCount;                            // Number of alternating red/white teeth
        }

        [Header("Kerb Dimensions")]
        [SerializeField] private float kerbRadialWidth = 6.5f;
        [SerializeField] private float kerbHeight = 0.04f;

        [Header("5 Inner Apex Locations")]
        [SerializeField] private List<ApexDefinition> apexes = new List<ApexDefinition>()
        {
            new ApexDefinition { name = "ChicaneDip", normalizedPosition = 0.50f, arcLengthSpan = 38f, blockCount = 10 },
            new ApexDefinition { name = "TopRightCrest", normalizedPosition = 0.38f, arcLengthSpan = 32f, blockCount = 8 },
            new ApexDefinition { name = "TopLeftCrest", normalizedPosition = 0.64f, arcLengthSpan = 32f, blockCount = 8 },
            new ApexDefinition { name = "LeftHairpin", normalizedPosition = 0.82f, arcLengthSpan = 40f, blockCount = 10 },
            new ApexDefinition { name = "RightApex", normalizedPosition = 0.22f, arcLengthSpan = 36f, blockCount = 10 }
        };

        [Header("Materials (Auto-Created if Null)")]
        [SerializeField] private Material redKerbMaterial;
        [SerializeField] private Material whiteKerbMaterial;

        private SplineArcLengthTable lut;

        private void Awake()
        {
            lut = GetComponent<SplineArcLengthTable>();
        }

        [ContextMenu("Build Apex Kerbs")]
        public void BuildApexKerbs()
        {
            if (lut == null) lut = GetComponent<SplineArcLengthTable>();
            if (lut == null || !lut.IsValid) return;

            ClearApexKerbs();
            EnsureMaterials();

            float totalLength = lut.TotalLength;
            float roadHalfWidth = GameConstants.RoadWidth * 0.5f;
            float innerEdgeOffset = -roadHalfWidth;

            GameObject rootGroup = new GameObject("Gen_ApexKerbs");
            rootGroup.transform.SetParent(transform, false);

            foreach (var apex in apexes)
            {
                GameObject apexObj = new GameObject("Apex_" + apex.name);
                apexObj.transform.SetParent(rootGroup.transform, false);

                float centerDist = apex.normalizedPosition * totalLength;
                float startDist = centerDist - apex.arcLengthSpan * 0.5f;
                float blockSpan = apex.arcLengthSpan / Mathf.Max(1, apex.blockCount);

                for (int b = 0; b < apex.blockCount; b++)
                {
                    float s0 = startDist + b * blockSpan;
                    float s1 = s0 + blockSpan;
                    Material mat = (b % 2 == 0) ? redKerbMaterial : whiteKerbMaterial;

                    GameObject blockObj = CreateKerbSegment($"Block_{b}", s0, s1, innerEdgeOffset, kerbRadialWidth, kerbHeight, mat);
                    blockObj.transform.SetParent(apexObj.transform, false);
                }
            }
        }

        [ContextMenu("Clear Apex Kerbs")]
        public void ClearApexKerbs()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Gen_ApexKerbs"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private void EnsureMaterials()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            if (redKerbMaterial == null)
            {
                redKerbMaterial = new Material(unlitShader) { name = "M_ApexKerbRed", color = GameConstants.ApexKerbRed };
            }
            if (whiteKerbMaterial == null)
            {
                whiteKerbMaterial = new Material(unlitShader) { name = "M_ApexKerbWhite", color = GameConstants.ApexKerbWhite };
            }
        }

        private GameObject CreateKerbSegment(string blockName, float s0, float s1, float edgeOffset, float radialWidth, float height, Material mat)
        {
            GameObject block = new GameObject(blockName);
            int subSteps = 6;
            int vertCount = (subSteps + 1) * 2;

            Vector3[] verts = new Vector3[vertCount];
            int[] tris = new int[subSteps * 6];

            for (int j = 0; j <= subSteps; j++)
            {
                float s = Mathf.Lerp(s0, s1, (float)j / subSteps);
                lut.SampleAtDistance(s, out Vector3 pos, out Vector3 tan, out Vector3 norm);

                Vector3 pBase = pos + norm * edgeOffset + Vector3.up * height;
                Vector3 pOuter = pos + norm * (edgeOffset - radialWidth) + Vector3.up * height;

                verts[j * 2] = pBase;
                verts[j * 2 + 1] = pOuter;
            }

            for (int j = 0; j < subSteps; j++)
            {
                int triIdx = j * 6;
                int i0 = j * 2;
                int i1 = (j + 1) * 2;
                int i2 = j * 2 + 1;
                int i3 = (j + 1) * 2 + 1;

                tris[triIdx + 0] = i0;
                tris[triIdx + 1] = i1;
                tris[triIdx + 2] = i2;

                tris[triIdx + 3] = i2;
                tris[triIdx + 4] = i1;
                tris[triIdx + 5] = i3;
            }

            Mesh mesh = new Mesh { name = blockName + "_Mesh" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            MeshFilter mf = block.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = block.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            return block;
        }
    }
}
