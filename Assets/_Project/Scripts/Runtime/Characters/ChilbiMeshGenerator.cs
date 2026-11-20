using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Procedural Mesh Generator for the 2.5D Chilbi Rig:
    /// - Continuous Candy Bean Body (Dome Head + Torso seamless silhouette, w=17, h=27, rx=8.5)
    /// - Pelvic stepping legs with rounded shoe feet
    /// - Curved bipedal arms with smooth rounded elbow bends (w=3.6)
    /// - Ground contact oval shadow
    /// </summary>
    public static class ChilbiMeshGenerator
    {
        /// <summary>
        /// Builds the organic candy bean body mesh (normalized centered at pelvic origin).
        /// </summary>
        public static Mesh CreateCandyBeanMesh(float width = 1.7f, float height = 2.7f, float depth = 1.2f, int radialSegments = 16, int heightSegments = 12)
        {
            Mesh mesh = new Mesh { name = "ChilbiCandyBeanMesh" };

            int vertCount = (radialSegments + 1) * (heightSegments + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            float radiusX = width * 0.5f;
            float radiusZ = depth * 0.5f;

            for (int y = 0; y <= heightSegments; y++)
            {
                float v = (float)y / heightSegments;
                // Vertical profile: pill shape with dome on top (v > 0.6) and rounded bottom (v < 0.2)
                float phi = (v - 0.5f) * Mathf.PI;
                float localY = (v - 0.5f) * height;

                float profileRadius;
                if (v > 0.6f)
                {
                    float domeT = (v - 0.6f) / 0.4f;
                    profileRadius = Mathf.Cos(domeT * Mathf.PI * 0.5f);
                }
                else if (v < 0.2f)
                {
                    float baseT = (0.2f - v) / 0.2f;
                    profileRadius = Mathf.Cos(baseT * Mathf.PI * 0.35f);
                }
                else
                {
                    profileRadius = 1.0f;
                }

                for (int r = 0; r <= radialSegments; r++)
                {
                    float u = (float)r / radialSegments;
                    float theta = u * Mathf.PI * 2f;

                    float cosTheta = Mathf.Cos(theta);
                    float sinTheta = Mathf.Sin(theta);

                    float x = cosTheta * radiusX * profileRadius;
                    float z = sinTheta * radiusZ * profileRadius;

                    int idx = y * (radialSegments + 1) + r;
                    verts[idx] = new Vector3(x, localY, z);
                    normals[idx] = new Vector3(cosTheta, Mathf.Sin(phi) * 0.5f, sinTheta).normalized;
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int triCount = heightSegments * radialSegments * 6;
            int[] tris = new int[triCount];
            int triIdx = 0;

            for (int y = 0; y < heightSegments; y++)
            {
                for (int r = 0; r < radialSegments; r++)
                {
                    int i0 = y * (radialSegments + 1) + r;
                    int i1 = (y + 1) * (radialSegments + 1) + r;
                    int i2 = y * (radialSegments + 1) + (r + 1);
                    int i3 = (y + 1) * (radialSegments + 1) + (r + 1);

                    tris[triIdx++] = i0;
                    tris[triIdx++] = i1;
                    tris[triIdx++] = i2;

                    tris[triIdx++] = i2;
                    tris[triIdx++] = i1;
                    tris[triIdx++] = i3;
                }
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds a rounded limb mesh (used for legs and curved arms).
        /// </summary>
        public static Mesh CreateLimbMesh(float length = 1.0f, float radius = 0.18f, bool addShoeFoot = false)
        {
            Mesh mesh = new Mesh { name = addShoeFoot ? "ChilbiLegMesh" : "ChilbiArmMesh" };

            int segments = 10;
            int radial = 8;
            int vertCount = (segments + 1) * (radial + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            for (int s = 0; s <= segments; s++)
            {
                float t = (float)s / segments;
                float y = -t * length; // Hangs downward from joint pivot at origin (0, 0, 0)
                float r = radius * (1f - (t * 0.15f)); // Subtle taper

                // Slight forward foot toe extrusion at the bottom if foot
                float toeOffsetZ = (addShoeFoot && t > 0.7f) ? (t - 0.7f) * 0.4f : 0f;

                for (int j = 0; j <= radial; j++)
                {
                    float u = (float)j / radial;
                    float angle = u * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    int idx = s * (radial + 1) + j;
                    verts[idx] = new Vector3(cos * r, y, sin * r + toeOffsetZ);
                    normals[idx] = new Vector3(cos, 0f, sin).normalized;
                    uvs[idx] = new Vector2(u, t);
                }
            }

            int[] tris = new int[segments * radial * 6];
            int triIdx = 0;
            for (int s = 0; s < segments; s++)
            {
                for (int j = 0; j < radial; j++)
                {
                    int i0 = s * (radial + 1) + j;
                    int i1 = (s + 1) * (radial + 1) + j;
                    int i2 = s * (radial + 1) + (j + 1);
                    int i3 = (s + 1) * (radial + 1) + (j + 1);

                    tris[triIdx++] = i0;
                    tris[triIdx++] = i1;
                    tris[triIdx++] = i2;

                    tris[triIdx++] = i2;
                    tris[triIdx++] = i1;
                    tris[triIdx++] = i3;
                }
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Builds ground contact shadow oval.
        /// </summary>
        public static Mesh CreateOvalShadowMesh(float radiusX = 0.85f, float radiusZ = 0.45f, int segments = 24)
        {
            Mesh mesh = new Mesh { name = "ChilbiShadowMesh" };

            Vector3[] verts = new Vector3[segments + 1];
            Vector2[] uvs = new Vector2[segments + 1];
            int[] tris = new int[segments * 3];

            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radiusX;
                float z = Mathf.Sin(angle) * radiusZ;

                verts[i + 1] = new Vector3(x, 0f, z);
                uvs[i + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);

                int next = (i + 1) % segments;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = next + 1;
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
