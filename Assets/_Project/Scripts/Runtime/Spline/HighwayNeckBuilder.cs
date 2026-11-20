using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Builds the Flared Highway Neck polygon bridging the circuit's bottom straightaway
    /// down into the top boundary of the Trapezium Parking Bays.
    /// Includes asphalt polygon, white boundary curbs, and tan dirt shoulders.
    /// </summary>
    [ExecuteAlways]
    public class HighwayNeckBuilder : MonoBehaviour
    {
        [Header("Materials (Auto-Created if Null)")]
        [SerializeField] private Material asphaltMaterial;
        [SerializeField] private Material curbMaterial;
        [SerializeField] private Material shoulderMaterial;

        [ContextMenu("Build Highway Neck")]
        public void BuildHighwayNeck()
        {
            ClearHighwayNeck();
            EnsureMaterials();
            BuildAsphaltNeck();
            BuildNeckCurbs();
            BuildNeckShoulders();
            BuildExitTrigger();
        }

        [ContextMenu("Clear Highway Neck")]
        public void ClearHighwayNeck()
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
                asphaltMaterial = new Material(unlitShader) { name = "M_NeckAsphalt", color = GameConstants.AsphaltColor };
            if (curbMaterial == null)
                curbMaterial = new Material(unlitShader) { name = "M_NeckCurb", color = GameConstants.WhiteCurbColor };
            if (shoulderMaterial == null)
                shoulderMaterial = new Material(unlitShader) { name = "M_NeckShoulder", color = GameConstants.DirtShoulderColor };
        }

        private void BuildAsphaltNeck()
        {
            // Left curve: (175, 252) -> C(155, 274)(120, 300) -> (90, 320)
            // Bottom line: (90, 320) -> (410, 320)
            // Right curve: (410, 320) -> C(380, 300)(345, 274) -> (325, 252)
            // Top line: (325, 252) -> (175, 252)
            int steps = 24;
            List<Vector3> leftCurve = SampleCubicBezier(
                new Vector3(175f, 0f, 252f),
                new Vector3(155f, 0f, 274f),
                new Vector3(120f, 0f, 300f),
                new Vector3(90f, 0f, 320f),
                steps
            );

            List<Vector3> rightCurve = SampleCubicBezier(
                new Vector3(325f, 0f, 252f),
                new Vector3(345f, 0f, 274f),
                new Vector3(380f, 0f, 300f),
                new Vector3(410f, 0f, 320f),
                steps
            );

            GameObject neckObj = new GameObject("Gen_HighwayNeck_Asphalt");
            neckObj.transform.SetParent(transform, false);

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            for (int i = 0; i <= steps; i++)
            {
                Vector3 pL = leftCurve[i] + Vector3.up * 0.021f;
                Vector3 pR = rightCurve[i] + Vector3.up * 0.021f;

                verts.Add(pL);
                verts.Add(pR);
            }

            for (int i = 0; i < steps; i++)
            {
                int v0 = i * 2;
                int v1 = (i + 1) * 2;
                int v2 = i * 2 + 1;
                int v3 = (i + 1) * 2 + 1;

                tris.Add(v0);
                tris.Add(v1);
                tris.Add(v2);

                tris.Add(v2);
                tris.Add(v1);
                tris.Add(v3);
            }

            Mesh mesh = new Mesh { name = "HighwayNeckAsphaltMesh" };
            mesh.vertices = verts.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();

            MeshFilter mf = neckObj.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = neckObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = asphaltMaterial;
        }

        private void BuildNeckCurbs()
        {
            float curbW = 3.5f;
            int steps = 24;

            GameObject curbsGroup = new GameObject("Gen_HighwayNeck_Curbs");
            curbsGroup.transform.SetParent(transform, false);

            // Left Curb
            List<Vector3> leftCurve = SampleCubicBezier(
                new Vector3(175f, 0f, 252f),
                new Vector3(155f, 0f, 274f),
                new Vector3(120f, 0f, 300f),
                new Vector3(90f, 0f, 320f),
                steps
            );
            BuildStripAlongCurve(curbsGroup, "LeftNeckCurb", leftCurve, -curbW, 0.026f, curbMaterial);

            // Right Curb
            List<Vector3> rightCurve = SampleCubicBezier(
                new Vector3(325f, 0f, 252f),
                new Vector3(345f, 0f, 274f),
                new Vector3(380f, 0f, 300f),
                new Vector3(410f, 0f, 320f),
                steps
            );
            BuildStripAlongCurve(curbsGroup, "RightNeckCurb", rightCurve, curbW, 0.026f, curbMaterial);
        }

        private void BuildNeckShoulders()
        {
            float shoulderW = 18f;
            int steps = 24;

            GameObject shouldersGroup = new GameObject("Gen_HighwayNeck_Shoulders");
            shouldersGroup.transform.SetParent(transform, false);

            // Left Shoulder
            List<Vector3> leftCurve = SampleCubicBezier(
                new Vector3(175f, 0f, 252f),
                new Vector3(155f, 0f, 274f),
                new Vector3(120f, 0f, 300f),
                new Vector3(90f, 0f, 320f),
                steps
            );
            BuildStripAlongCurve(shouldersGroup, "LeftNeckShoulder", leftCurve, -shoulderW, 0.016f, shoulderMaterial);

            // Right Shoulder
            List<Vector3> rightCurve = SampleCubicBezier(
                new Vector3(325f, 0f, 252f),
                new Vector3(345f, 0f, 274f),
                new Vector3(380f, 0f, 300f),
                new Vector3(410f, 0f, 320f),
                steps
            );
            BuildStripAlongCurve(shouldersGroup, "RightNeckShoulder", rightCurve, shoulderW, 0.016f, shoulderMaterial);
        }

        private void BuildExitTrigger()
        {
            GameObject exitTrigger = new GameObject("Gen_Trigger_HighwayExit");
            exitTrigger.transform.SetParent(transform, false);
            exitTrigger.transform.localPosition = new Vector3(250f, 0.5f, 280f);
            BoxCollider colExit = exitTrigger.AddComponent<BoxCollider>();
            colExit.isTrigger = true;
            colExit.size = new Vector3(80f, 2f, 20f);
        }

        private List<Vector3> SampleCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int steps)
        {
            List<Vector3> points = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float u = (float)i / steps;
                float mt = 1f - u;
                float mt2 = mt * mt;
                float mt3 = mt2 * mt;
                float u2 = u * u;
                float u3 = u2 * u;

                Vector3 pos = mt3 * p0 + 3f * mt2 * u * p1 + 3f * mt * u2 * p2 + u3 * p3;
                points.Add(pos);
            }
            return points;
        }

        private void BuildStripAlongCurve(GameObject parent, string name, List<Vector3> curve, float widthOffset, float height, Material mat)
        {
            GameObject strip = new GameObject(name);
            strip.transform.SetParent(parent.transform, false);

            int count = curve.Count;
            Vector3[] verts = new Vector3[count * 2];
            int[] tris = new int[(count - 1) * 6];

            for (int i = 0; i < count; i++)
            {
                Vector3 tan;
                if (i == 0) tan = (curve[1] - curve[0]).normalized;
                else if (i == count - 1) tan = (curve[count - 1] - curve[count - 2]).normalized;
                else tan = (curve[i + 1] - curve[i - 1]).normalized;

                Vector3 norm = new Vector3(-tan.z, 0f, tan.x).normalized;

                verts[i * 2] = curve[i] + Vector3.up * height;
                verts[i * 2 + 1] = curve[i] + norm * widthOffset + Vector3.up * height;
            }

            for (int i = 0; i < count - 1; i++)
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
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            MeshFilter mf = strip.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = strip.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }
    }
}
