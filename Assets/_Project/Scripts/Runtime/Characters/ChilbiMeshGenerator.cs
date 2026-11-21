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
        /// Builds the sculpted 3D vinyl figurine mesh with spherical head, neck crease,
        /// rounded shoulders, and baked crevice ambient occlusion in vertex colors.
        /// </summary>
        public static Mesh CreateCandyBeanMesh(float width = 1.7f, float height = 2.7f, float depth = 1.3f, int radialSegments = 18, int heightSegments = 16)
        {
            Mesh mesh = new Mesh { name = "ChilbiSculptedFigurineMesh" };

            int vertCount = (radialSegments + 1) * (heightSegments + 1);
            Vector3[] verts = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Color[] colors = new Color[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            float radiusX = width * 0.5f;
            float radiusZ = depth * 0.5f;

            for (int y = 0; y <= heightSegments; y++)
            {
                float v = (float)y / heightSegments; // 0.0 (feet) to 1.0 (head crown)
                float localY = (v - 0.5f) * height;

                // Profile curve for a collectible vinyl figurine (Fall Guys / Lego style):
                // v > 0.68: Spherical Head Dome
                // v in [0.54, 0.68]: Neck Crease (pinch inward)
                // v in [0.30, 0.54]: Rounded Shoulders & Chest (widen outward)
                // v in [0.08, 0.30]: Torso / Hips
                // v < 0.08: Rounded Pelvic Base
                float profileRadius;
                float verticalAO;

                if (v >= 0.68f)
                {
                    // Spherical head dome
                    float t = (v - 0.68f) / 0.32f; // 0 to 1
                    profileRadius = 0.88f * Mathf.Cos(t * Mathf.PI * 0.5f);
                    verticalAO = Mathf.Lerp(0.85f, 1.0f, t); // Brightest at top crown
                }
                else if (v >= 0.54f)
                {
                    // Neck indentation
                    float t = (v - 0.54f) / 0.14f;
                    float sinPinch = Mathf.Sin(t * Mathf.PI);
                    profileRadius = Mathf.Lerp(1.10f, 0.88f, t) - (sinPinch * 0.18f); // Pinches to 0.70
                    verticalAO = Mathf.Lerp(0.55f, 0.85f, t) - (sinPinch * 0.35f); // Deep neck shadow
                }
                else if (v >= 0.28f)
                {
                    // Shoulders & Chest
                    float t = (v - 0.28f) / 0.26f;
                    profileRadius = Mathf.Lerp(1.0f, 1.15f, Mathf.Sin(t * Mathf.PI * 0.5f));
                    verticalAO = Mathf.Lerp(0.65f, 0.85f, t);
                }
                else
                {
                    // Lower Torso & Pelvis
                    float t = v / 0.28f;
                    profileRadius = Mathf.Lerp(0.65f, 1.0f, Mathf.Sin(t * Mathf.PI * 0.5f));
                    verticalAO = Mathf.Lerp(0.48f, 0.65f, t); // Shaded base
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

                    // Lateral Crevice AO: characters in a 7-abreast row touch on left (cos < 0) and right (cos > 0)
                    // We darken lateral sides (|cosTheta| > 0.6) so touching neighbors have distinct shadow borders!
                    float lateralCrevice = 1.0f - (Mathf.Abs(cosTheta) * 0.28f * (1.0f - Mathf.Abs(v - 0.5f)));
                    float finalAO = Mathf.Clamp01(verticalAO * lateralCrevice);

                    colors[idx] = new Color(finalAO, finalAO, finalAO, 1.0f);

                    // Calculate smooth normals with dome rounding
                    float ny = (v > 0.68f) ? Mathf.Sin(((v - 0.68f) / 0.32f) * Mathf.PI * 0.5f) : 0f;
                    if (v < 0.12f) ny = -Mathf.Cos((v / 0.12f) * Mathf.PI * 0.5f) * 0.6f;
                    normals[idx] = new Vector3(cosTheta * 0.8f, ny, sinTheta * 0.8f).normalized;

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
            mesh.colors = colors;
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
