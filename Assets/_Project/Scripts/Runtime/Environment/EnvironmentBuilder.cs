using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Procedural Cherry Blossom Park Environment Generator.
    /// Strictly complies with the art direction:
    /// - Clean pastel pink ground plane (#fff1f2).
    /// - Concentric translucent circular pathway decals (#f2d5db, alpha 0.35).
    /// - Zen garden pebble & stone accents placed around the perimeter.
    /// - STRICT ZERO TREES, ZERO LEAF PARTICLES, ZERO CLUTTER ON LAWN.
    /// </summary>
    [ExecuteAlways]
    public class EnvironmentBuilder : MonoBehaviour
    {
        [Header("Ground Dimensions")]
        [SerializeField] private float groundWidth = 80f;
        [SerializeField] private float groundLength = 120f;

        [Header("Pathways Configuration")]
        [SerializeField] private int concentricRingCount = 3;
        [SerializeField] private float innerRingRadius = 14f;
        [SerializeField] private float ringStepRadius = 10f;
        [SerializeField] private float pathwayRibbonWidth = 1.6f;

        [Header("Perimeter Stones")]
        [SerializeField] private int stoneCount = 16;
        [SerializeField] private float stonePerimeterRadius = 38f;

        [Header("Materials (Auto-Generated if Null)")]
        [SerializeField] private Material lawnMaterial;
        [SerializeField] private Material pathwayMaterial;
        [SerializeField] private Material stoneMaterial;

        private void Start()
        {
            if (transform.childCount == 0)
            {
                BuildEnvironment();
            }
        }

        [ContextMenu("Build Environment")]
        public void BuildEnvironment()
        {
            ClearExistingGeometry();
            EnsureMaterials();
            BuildLawnGround();
            BuildConcentricPathways();
            BuildZenGardenStones();
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

            if (lawnMaterial == null)
            {
                lawnMaterial = new Material(unlitShader)
                {
                    name = "M_ParkLawn",
                    color = GameConstants.LawnColor
                };
            }

            if (pathwayMaterial == null)
            {
                pathwayMaterial = new Material(unlitShader)
                {
                    name = "M_ConcentricPathways",
                    color = GameConstants.LawnPathwayColor
                };
                // Enable alpha blending
                pathwayMaterial.SetFloat("_Surface", 1); // Transparent
                pathwayMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                pathwayMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                pathwayMaterial.SetInt("_ZWrite", 0);
                pathwayMaterial.renderQueue = 3000;
            }

            if (stoneMaterial == null)
            {
                stoneMaterial = new Material(unlitShader)
                {
                    name = "M_ZenStones",
                    color = new Color(0.92f, 0.88f, 0.90f) // Soft pearl pinkish-grey
                };
            }
        }

        private void BuildLawnGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "ParkGroundQuad";
            ground.transform.SetParent(transform, false);
            ground.transform.localPosition = new Vector3(0f, 0f, 0f);
            ground.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ground.transform.localScale = new Vector3(groundWidth, groundLength, 1f);

            Renderer r = ground.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = lawnMaterial;

            Collider c = ground.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void BuildConcentricPathways()
        {
            GameObject pathwaysGroup = new GameObject("ConcentricPathways");
            pathwaysGroup.transform.SetParent(transform, false);

            for (int ring = 0; ring < concentricRingCount; ring++)
            {
                float radius = innerRingRadius + (ring * ringStepRadius);
                GameObject ringObj = CreateCircleRibbon(radius, pathwayRibbonWidth, 64);
                ringObj.name = $"PathwayRing_{ring + 1}";
                ringObj.transform.SetParent(pathwaysGroup.transform, false);
                ringObj.transform.localPosition = new Vector3(0f, 0.01f + (ring * 0.002f), 0f); // Slight Y offset to avoid Z-fighting
            }
        }

        private GameObject CreateCircleRibbon(float radius, float ribbonWidth, int segments)
        {
            GameObject ribbonObj = new GameObject();
            MeshFilter mf = ribbonObj.AddComponent<MeshFilter>();
            MeshRenderer mr = ribbonObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = pathwayMaterial;

            Mesh mesh = new Mesh { name = "PathwayRibbonMesh" };
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];

            float rInner = radius - (ribbonWidth * 0.5f);
            float rOuter = radius + (ribbonWidth * 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * rInner, 0f, sin * rInner);
                vertices[i * 2 + 1] = new Vector3(cos * rOuter, 0f, sin * rOuter);

                int next = (i + 1) % segments;
                int triIndex = i * 6;

                triangles[triIndex + 0] = i * 2;
                triangles[triIndex + 1] = (next * 2);
                triangles[triIndex + 2] = i * 2 + 1;

                triangles[triIndex + 3] = i * 2 + 1;
                triangles[triIndex + 4] = (next * 2);
                triangles[triIndex + 5] = (next * 2) + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            return ribbonObj;
        }

        private void BuildZenGardenStones()
        {
            GameObject stonesGroup = new GameObject("ZenGardenStones");
            stonesGroup.transform.SetParent(transform, false);

            for (int i = 0; i < stoneCount; i++)
            {
                float angle = (i / (float)stoneCount) * Mathf.PI * 2f;
                // Oval dispersion along perimeter
                float x = Mathf.Cos(angle) * (stonePerimeterRadius * 0.75f);
                float z = Mathf.Sin(angle) * (stonePerimeterRadius * 1.15f);

                GameObject pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                pebble.name = $"ZenPebble_{i + 1}";
                pebble.transform.SetParent(stonesGroup.transform, false);

                // Varied organic flattened pebble scaling
                float baseScale = Random.Range(1.2f, 2.4f);
                pebble.transform.localScale = new Vector3(baseScale, baseScale * 0.45f, baseScale * 0.85f);
                pebble.transform.localPosition = new Vector3(x, pebble.transform.localScale.y * 0.35f, z);
                pebble.transform.localRotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));

                Renderer r = pebble.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = stoneMaterial;

                Collider c = pebble.GetComponent<Collider>();
                if (c != null) DestroyImmediate(c);
            }
        }
    }
}
