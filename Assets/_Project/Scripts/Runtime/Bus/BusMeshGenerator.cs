using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Procedural Mesh Generator for:
    /// - Top-Down Bus silhouettes (25x50 units, rx=5.5)
    /// - Tinted front visor & side window cutouts (#1e2430)
    /// - Forward-facing crisp white roof arrow
    /// - Angled Parking Bay slot borders (-30 deg)
    /// </summary>
    public static class BusMeshGenerator
    {
        // Mobile 3D World Scaled Constants (Exact Screen-Fit Parity)
        public const float BusWidth = 0.95f;
        public const float BusLength = 1.90f;
        public const float CornerRadius = 0.22f;
        public const float BusHeight = 0.25f;

        /// <summary>
        /// Creates a top-down rounded rectangle mesh for the bus body.
        /// </summary>
        public static Mesh CreateBusBodyMesh(float width = BusWidth, float length = BusLength, float radius = CornerRadius, float height = BusHeight)
        {
            Mesh mesh = new Mesh { name = "BusBodyMesh" };

            // Rounded rectangle in XZ plane with slight extrusion height
            int cornerSegments = 6;
            int totalOutline = cornerSegments * 4;
            Vector3[] outline = new Vector3[totalOutline];

            float hw = width * 0.5f;
            float hl = length * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(hw, hl));

            int pt = 0;
            // 4 corners: Top-Right, Bottom-Right, Bottom-Left, Top-Left
            Vector2[] centers = new Vector2[]
            {
                new Vector2(hw - r, hl - r),
                new Vector2(hw - r, -(hl - r)),
                new Vector2(-(hw - r), -(hl - r)),
                new Vector2(-(hw - r), hl - r)
            };

            for (int c = 0; c < 4; c++)
            {
                float startAngle = c * Mathf.PI * 0.5f;
                for (int s = 0; s < cornerSegments; s++)
                {
                    float angle = startAngle + (s / (float)cornerSegments) * (Mathf.PI * 0.5f);
                    outline[pt++] = new Vector3(
                        centers[c].x + Mathf.Cos(angle) * r,
                        0f,
                        centers[c].y + Mathf.Sin(angle) * r
                    );
                }
            }

            // Top Cap and Bottom Cap
            int vertCount = totalOutline * 2 + 2; // Bottom + Top + 2 centers
            Vector3[] verts = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];

            int centerBotIdx = totalOutline * 2;
            int centerTopIdx = totalOutline * 2 + 1;

            verts[centerBotIdx] = Vector3.zero;
            normals[centerBotIdx] = Vector3.down;
            uvs[centerBotIdx] = new Vector2(0.5f, 0.5f);

            verts[centerTopIdx] = new Vector3(0f, height, 0f);
            normals[centerTopIdx] = Vector3.up;
            uvs[centerTopIdx] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < totalOutline; i++)
            {
                // Bottom
                verts[i] = outline[i];
                normals[i] = Vector3.down;
                uvs[i] = new Vector2((outline[i].x / width) + 0.5f, (outline[i].z / length) + 0.5f);

                // Top
                verts[i + totalOutline] = outline[i] + Vector3.up * height;
                normals[i + totalOutline] = Vector3.up;
                uvs[i + totalOutline] = new Vector2((outline[i].x / width) + 0.5f, (outline[i].z / length) + 0.5f);
            }

            // Triangles: Bottom fan + Top fan + Side quads
            int sideTriCount = totalOutline * 6;
            int capTriCount = totalOutline * 3 * 2;
            int[] tris = new int[sideTriCount + capTriCount];
            int triIdx = 0;

            // Top cap fan
            for (int i = 0; i < totalOutline; i++)
            {
                int next = (i + 1) % totalOutline;
                tris[triIdx++] = centerTopIdx;
                tris[triIdx++] = i + totalOutline;
                tris[triIdx++] = next + totalOutline;
            }

            // Bottom cap fan
            for (int i = 0; i < totalOutline; i++)
            {
                int next = (i + 1) % totalOutline;
                tris[triIdx++] = centerBotIdx;
                tris[triIdx++] = next;
                tris[triIdx++] = i;
            }

            // Side quads
            for (int i = 0; i < totalOutline; i++)
            {
                int next = (i + 1) % totalOutline;
                int b0 = i;
                int b1 = next;
                int t0 = i + totalOutline;
                int t1 = next + totalOutline;

                tris[triIdx++] = b0;
                tris[triIdx++] = t0;
                tris[triIdx++] = b1;

                tris[triIdx++] = b1;
                tris[triIdx++] = t0;
                tris[triIdx++] = t1;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates forward-facing white roof arrow mesh.
        /// Based on prototype SVG path: M 12.5,17 L 18.5,24.5 L 14.8,24.5 L 14.8,34 Q 14.8,35 12.5,35 ...
        /// Centered on roof origin.
        /// </summary>
        public static Mesh CreateRoofArrowMesh(float scale = 0.035f)
        {
            Mesh mesh = new Mesh { name = "RoofArrowMesh" };

            // Normalized arrow points in XZ plane (Z points forward)
            Vector3[] pts = new Vector3[]
            {
                new Vector3(0f, 0f, 9f) * scale,       // Tip
                new Vector3(6f, 0f, 1.5f) * scale,     // Right head corner
                new Vector3(2.3f, 0f, 1.5f) * scale,   // Right shaft notch
                new Vector3(2.3f, 0f, -8f) * scale,    // Right tail
                new Vector3(-2.3f, 0f, -8f) * scale,   // Left tail
                new Vector3(-2.3f, 0f, 1.5f) * scale,  // Left shaft notch
                new Vector3(-6f, 0f, 1.5f) * scale     // Left head corner
            };

            // Triangulate arrow: head triangle + shaft rectangle
            Vector3[] verts = new Vector3[]
            {
                pts[0], pts[1], pts[2], pts[5], pts[6], // Head
                pts[5], pts[2], pts[3], pts[4]          // Shaft
            };

            int[] tris = new int[]
            {
                // Head fan
                0, 1, 2,
                0, 2, 5,
                0, 5, 6,
                // Shaft
                7, 8, 5,
                7, 5, 6 // Re-indexed correctly below
            };

            // Clean 7-vertex triangulation
            tris = new int[]
            {
                0, 1, 6,
                1, 2, 6,
                6, 2, 5,
                2, 3, 5,
                3, 4, 5
            };

            mesh.vertices = pts;
            mesh.triangles = tris;
            mesh.normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.up, Vector3.up, Vector3.up
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates dark tinted window visor strip mesh.
        /// </summary>
        public static Mesh CreateTintedVisorMesh(float width = 0.80f, float length = 0.35f)
        {
            Mesh mesh = new Mesh { name = "TintedVisorMesh" };
            float hw = width * 0.5f;
            float hl = length * 0.5f;

            Vector3[] verts = new Vector3[]
            {
                new Vector3(-hw, 0f, -hl),
                new Vector3(hw, 0f, -hl),
                new Vector3(-hw, 0f, hl),
                new Vector3(hw, 0f, hl)
            };

            int[] tris = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates rectangular parking bay border slot.
        /// </summary>
        public static Mesh CreateParkingSlotBorderMesh(float width = 0.95f, float length = 1.90f, float borderWidth = 0.08f)
        {
            Mesh mesh = new Mesh { name = "ParkingSlotBorderMesh" };

            float hwO = width * 0.5f;
            float hlO = length * 0.5f;
            float hwI = hwO - borderWidth;
            float hlI = hlO - borderWidth;

            Vector3[] verts = new Vector3[8];
            // Outer 4 corners
            verts[0] = new Vector3(-hwO, 0f, -hlO);
            verts[1] = new Vector3(hwO, 0f, -hlO);
            verts[2] = new Vector3(hwO, 0f, hlO);
            verts[3] = new Vector3(-hwO, 0f, hlO);
            // Inner 4 corners
            verts[4] = new Vector3(-hwI, 0f, -hlI);
            verts[5] = new Vector3(hwI, 0f, -hlI);
            verts[6] = new Vector3(hwI, 0f, hlI);
            verts[7] = new Vector3(-hwI, 0f, hlI);

            // 4 Quad borders
            int[] tris = new int[]
            {
                // Bottom
                0, 4, 1, 1, 4, 5,
                // Right
                1, 5, 2, 2, 5, 6,
                // Top
                2, 6, 3, 3, 6, 7,
                // Left
                3, 7, 0, 0, 7, 4
            };

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
