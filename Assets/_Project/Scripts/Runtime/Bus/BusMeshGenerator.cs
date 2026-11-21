using UnityEngine;

namespace RoadwaysGame
{
    public enum VehicleArchetype
    {
        CoupeCar,   // 4 passengers: Vintage rounded beetle/coupe styling
        Minivan,    // 7 passengers: Medium camper/van with rounded roof
        TransitBus  // 10 passengers: Long aerodynamic commuter transit bus
    }

    /// <summary>
    /// Procedural HD Mesh Generator for 3D Vehicles:
    /// - 3 Distinct Vehicle Archetypes: Coupe Car (4 seats), Minivan (7 seats), Transit Bus (10 seats)
    /// - Chamfered/filleted roof & side bevels that catch bright specular light
    /// - Tinted glossy glass windows (windshield, rear, side strips)
    /// - 4 Recessed black rubber wheels
    /// - Soft contact drop-shadow mesh beneath the chassis
    /// - Embossed 3D forward-facing white roof arrow
    /// - Angled Parking Bay slot borders (-30 deg)
    /// </summary>
    public static class BusMeshGenerator
    {
        // Dimensions per Archetype
        public static void GetDimensions(VehicleArchetype archetype, out float width, out float length, out float height, out float cornerRadius, out int capacity)
        {
            switch (archetype)
            {
                case VehicleArchetype.CoupeCar:
                    width = 0.88f;
                    length = 1.35f;
                    height = 0.42f;
                    cornerRadius = 0.28f;
                    capacity = 4;
                    break;
                case VehicleArchetype.TransitBus:
                    width = 1.05f;
                    length = 2.45f;
                    height = 0.52f;
                    cornerRadius = 0.24f;
                    capacity = 10;
                    break;
                case VehicleArchetype.Minivan:
                default:
                    width = 0.95f;
                    length = 1.90f;
                    height = 0.46f;
                    cornerRadius = 0.22f;
                    capacity = 7;
                    break;
            }
        }

        // Backward-compatible baseline constants
        public const float BusWidth = 0.95f;
        public const float BusLength = 1.90f;
        public const float CornerRadius = 0.22f;
        public const float BusHeight = 0.46f;

        /// <summary>
        /// Creates a high-definition vehicle body mesh with filleted chamfers.
        /// </summary>
        public static Mesh CreateChamferedBodyMesh(VehicleArchetype archetype)
        {
            GetDimensions(archetype, out float width, out float length, out float height, out float radius, out _);
            Mesh mesh = new Mesh { name = $"VehicleBodyMesh_{archetype}" };

            bool isCoupe = archetype == VehicleArchetype.CoupeCar;
            float hw = width * 0.5f;
            float hl = length * 0.5f;
            float r = Mathf.Min(radius, Mathf.Min(hw, hl));

            // Generate rounded outline in XZ plane
            int cornerSegments = 6;
            int totalOutline = cornerSegments * 4;
            Vector3[] outline = new Vector3[totalOutline];

            Vector2[] centers = new Vector2[]
            {
                new Vector2(hw - r, hl - r),
                new Vector2(hw - r, -(hl - r)),
                new Vector2(-(hw - r), -(hl - r)),
                new Vector2(-(hw - r), hl - r)
            };

            int pt = 0;
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

            // Height tiers: Bottom (0.06), Waist (height * 0.65), Bevel (height * 0.90), Roof Top (height)
            float yBottom = 0.06f;
            float yWaist = height * 0.62f;
            float yBevel = height * 0.88f;
            float yTop = height;

            float bevelInset = 0.065f; // Chamfer inward step

            // Vertices: 4 rings of totalOutline + 2 center caps
            int ringVerts = totalOutline;
            int totalVerts = ringVerts * 4 + 2;
            Vector3[] verts = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];

            int centerBotIdx = ringVerts * 4;
            int centerTopIdx = ringVerts * 4 + 1;

            verts[centerBotIdx] = new Vector3(0f, yBottom, 0f);
            normals[centerBotIdx] = Vector3.down;
            uvs[centerBotIdx] = new Vector2(0.5f, 0.5f);

            verts[centerTopIdx] = new Vector3(0f, yTop, 0f);
            normals[centerTopIdx] = Vector3.up;
            uvs[centerTopIdx] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < totalOutline; i++)
            {
                Vector3 p = outline[i];
                Vector3 n = new Vector3(p.x, 0f, p.z).normalized;

                // Ring 0: Bottom chassis (yBottom)
                int r0 = i;
                verts[r0] = new Vector3(p.x, yBottom, p.z);
                normals[r0] = (n + Vector3.down * 0.3f).normalized;
                uvs[r0] = new Vector2(p.x / width + 0.5f, p.z / length + 0.5f);

                // Ring 1: Waist line (yWaist)
                int r1 = i + ringVerts;
                verts[r1] = new Vector3(p.x, yWaist, p.z);
                normals[r1] = n;
                uvs[r1] = new Vector2(p.x / width + 0.5f, p.z / length + 0.5f);

                // Ring 2: Chamfer shoulder (yBevel, slightly inset)
                int r2 = i + ringVerts * 2;
                Vector3 pBevel = p - n * (bevelInset * 0.5f);
                verts[r2] = new Vector3(pBevel.x, yBevel, pBevel.z);
                normals[r2] = (n + Vector3.up * 0.7f).normalized; // Rounded chamfer normal
                uvs[r2] = new Vector2(pBevel.x / width + 0.5f, pBevel.z / length + 0.5f);

                // Ring 3: Roof edge (yTop, fully inset)
                int r3 = i + ringVerts * 3;
                Vector3 pTop = p - n * bevelInset;
                verts[r3] = new Vector3(pTop.x, yTop, pTop.z);
                normals[r3] = Vector3.up;
                uvs[r3] = new Vector2(pTop.x / width + 0.5f, pTop.z / length + 0.5f);
            }

            // Triangles: 3 quad wall strips + top/bottom fans
            int quadCount = ringVerts * 3;
            int fanCount = ringVerts * 2;
            int totalTris = (quadCount * 6) + (fanCount * 3);
            int[] tris = new int[totalTris];
            int tri = 0;

            // Strip 0: Bottom to Waist
            for (int i = 0; i < ringVerts; i++)
            {
                int next = (i + 1) % ringVerts;
                int b0 = i;
                int b1 = next;
                int t0 = i + ringVerts;
                int t1 = next + ringVerts;

                tris[tri++] = b0; tris[tri++] = t0; tris[tri++] = b1;
                tris[tri++] = b1; tris[tri++] = t0; tris[tri++] = t1;
            }

            // Strip 1: Waist to Chamfer Shoulder
            for (int i = 0; i < ringVerts; i++)
            {
                int next = (i + 1) % ringVerts;
                int b0 = i + ringVerts;
                int b1 = next + ringVerts;
                int t0 = i + ringVerts * 2;
                int t1 = next + ringVerts * 2;

                tris[tri++] = b0; tris[tri++] = t0; tris[tri++] = b1;
                tris[tri++] = b1; tris[tri++] = t0; tris[tri++] = t1;
            }

            // Strip 2: Chamfer Shoulder to Roof Edge
            for (int i = 0; i < ringVerts; i++)
            {
                int next = (i + 1) % ringVerts;
                int b0 = i + ringVerts * 2;
                int b1 = next + ringVerts * 2;
                int t0 = i + ringVerts * 3;
                int t1 = next + ringVerts * 3;

                tris[tri++] = b0; tris[tri++] = t0; tris[tri++] = b1;
                tris[tri++] = b1; tris[tri++] = t0; tris[tri++] = t1;
            }

            // Top Cap Fan
            for (int i = 0; i < ringVerts; i++)
            {
                int next = (i + 1) % ringVerts;
                tris[tri++] = centerTopIdx;
                tris[tri++] = i + ringVerts * 3;
                tris[tri++] = next + ringVerts * 3;
            }

            // Bottom Cap Fan
            for (int i = 0; i < ringVerts; i++)
            {
                int next = (i + 1) % ringVerts;
                tris[tri++] = centerBotIdx;
                tris[tri++] = next;
                tris[tri++] = i;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates 3D dark glossy glass window meshes (windshield, rear, and side windows).
        /// </summary>
        public static Mesh CreateWindowMesh(VehicleArchetype archetype)
        {
            GetDimensions(archetype, out float width, out float length, out float height, out _, out _);
            Mesh mesh = new Mesh { name = $"VehicleWindows_{archetype}" };

            float hw = (width * 0.5f) - 0.02f;
            float hl = (length * 0.5f) - 0.08f;
            float yWinBot = height * 0.45f;
            float yWinTop = height * 0.78f;

            // Windshield (front), Rear window (rear), Left & Right side glass
            // Front windshield
            float zFront = hl * 0.65f;
            float zFrontTop = hl * 0.35f;

            // Simple multi-quad window cage
            Vector3[] verts = new Vector3[16];
            Vector3[] normals = new Vector3[16];
            Vector2[] uvs = new Vector2[16];

            // 1. Front Windshield (curved forward tilt)
            verts[0] = new Vector3(-hw * 0.85f, yWinBot, zFront);
            verts[1] = new Vector3(hw * 0.85f, yWinBot, zFront);
            verts[2] = new Vector3(hw * 0.75f, yWinTop, zFrontTop);
            verts[3] = new Vector3(-hw * 0.75f, yWinTop, zFrontTop);

            Vector3 nFront = Vector3.forward + Vector3.up * 0.5f;
            for (int i = 0; i < 4; i++) normals[i] = nFront.normalized;

            // 2. Rear Window (rear tilt)
            float zRear = -hl * 0.82f;
            float zRearTop = -hl * 0.55f;
            verts[4] = new Vector3(hw * 0.80f, yWinBot, zRear);
            verts[5] = new Vector3(-hw * 0.80f, yWinBot, zRear);
            verts[6] = new Vector3(-hw * 0.70f, yWinTop, zRearTop);
            verts[7] = new Vector3(hw * 0.70f, yWinTop, zRearTop);

            Vector3 nRear = Vector3.back + Vector3.up * 0.5f;
            for (int i = 4; i < 8; i++) normals[i] = nRear.normalized;

            // 3. Right Side Windows
            verts[8] = new Vector3(hw + 0.005f, yWinBot, -hl * 0.50f);
            verts[9] = new Vector3(hw + 0.005f, yWinBot, hl * 0.30f);
            verts[10] = new Vector3(hw * 0.90f, yWinTop, hl * 0.20f);
            verts[11] = new Vector3(hw * 0.90f, yWinTop, -hl * 0.42f);

            for (int i = 8; i < 12; i++) normals[i] = Vector3.right;

            // 4. Left Side Windows
            verts[12] = new Vector3(-hw - 0.005f, yWinBot, hl * 0.30f);
            verts[13] = new Vector3(-hw - 0.005f, yWinBot, -hl * 0.50f);
            verts[14] = new Vector3(-hw * 0.90f, yWinTop, -hl * 0.42f);
            verts[15] = new Vector3(-hw * 0.90f, yWinTop, hl * 0.20f);

            for (int i = 12; i < 16; i++) normals[i] = Vector3.left;

            int[] tris = new int[]
            {
                // Front
                0, 2, 1, 0, 3, 2,
                // Rear
                4, 6, 5, 4, 7, 6,
                // Right
                8, 10, 9, 8, 11, 10,
                // Left
                12, 14, 13, 12, 15, 14
            };

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates 4 recessed black rubber wheels under the chassis.
        /// </summary>
        public static Mesh CreateWheelMeshes(VehicleArchetype archetype)
        {
            GetDimensions(archetype, out float width, out float length, out _, out _, out _);
            Mesh mesh = new Mesh { name = $"VehicleWheels_{archetype}" };

            float hw = width * 0.48f;
            float hl = length * 0.36f;
            float wheelRadius = 0.12f;
            float wheelWidth = 0.09f;

            // 4 Wheel positions: Front-Right, Rear-Right, Rear-Left, Front-Left
            Vector3[] wheelCenters = new Vector3[]
            {
                new Vector3(hw, wheelRadius, hl),
                new Vector3(hw, wheelRadius, -hl),
                new Vector3(-hw, wheelRadius, -hl),
                new Vector3(-hw, wheelRadius, hl)
            };

            int segments = 8;
            int vertsPerWheel = segments * 2;
            Vector3[] verts = new Vector3[vertsPerWheel * 4];
            Vector3[] normals = new Vector3[verts.Length];
            int[] tris = new int[segments * 6 * 4];

            int vIdx = 0;
            int tIdx = 0;

            for (int w = 0; w < 4; w++)
            {
                Vector3 c = wheelCenters[w];
                float sideSign = c.x > 0 ? 1f : -1f;
                int baseV = vIdx;

                for (int s = 0; s < segments; s++)
                {
                    float angle = (s / (float)segments) * Mathf.PI * 2f;
                    float y = Mathf.Sin(angle) * wheelRadius;
                    float z = Mathf.Cos(angle) * wheelRadius;

                    // Outer vertex
                    verts[vIdx] = new Vector3(c.x, c.y + y, c.z + z);
                    normals[vIdx] = Vector3.right * sideSign;
                    vIdx++;

                    // Inner vertex
                    verts[vIdx] = new Vector3(c.x - sideSign * wheelWidth, c.y + y, c.z + z);
                    normals[vIdx] = (Vector3.up * Mathf.Sin(angle) + Vector3.forward * Mathf.Cos(angle)).normalized;
                    vIdx++;
                }

                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    int o0 = baseV + s * 2;
                    int i0 = baseV + s * 2 + 1;
                    int o1 = baseV + next * 2;
                    int i1 = baseV + next * 2 + 1;

                    if (sideSign > 0)
                    {
                        tris[tIdx++] = o0; tris[tIdx++] = i0; tris[tIdx++] = o1;
                        tris[tIdx++] = o1; tris[tIdx++] = i0; tris[tIdx++] = i1;
                    }
                    else
                    {
                        tris[tIdx++] = o0; tris[tIdx++] = o1; tris[tIdx++] = i0;
                        tris[tIdx++] = o1; tris[tIdx++] = i1; tris[tIdx++] = i0;
                    }
                }
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates a soft contact drop shadow mesh positioned on the tarmac ground (Y = 0.005).
        /// Gradient alpha with vertex colors: dark core (alpha ~0.50) fading to 0 at perimeter.
        /// </summary>
        public static Mesh CreateContactShadowMesh(VehicleArchetype archetype)
        {
            GetDimensions(archetype, out float width, out float length, out _, out _, out _);
            Mesh mesh = new Mesh { name = $"VehicleShadowMesh_{archetype}" };

            float hwInner = width * 0.46f;
            float hlInner = length * 0.48f;
            float hwOuter = width * 0.62f;
            float hlOuter = length * 0.58f;

            int segments = 16;
            int totalVerts = segments * 2 + 1; // Center, inner ring, outer ring
            Vector3[] verts = new Vector3[totalVerts];
            Color[] colors = new Color[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];

            int centerIdx = 0;
            verts[centerIdx] = Vector3.zero;
            colors[centerIdx] = new Color(0f, 0f, 0f, 0.52f);
            normals[centerIdx] = Vector3.up;

            for (int s = 0; s < segments; s++)
            {
                float angle = (s / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Inner ring (contact shadow core)
                int inIdx = 1 + s;
                verts[inIdx] = new Vector3(cos * hwInner, 0f, sin * hlInner);
                colors[inIdx] = new Color(0f, 0f, 0f, 0.38f);
                normals[inIdx] = Vector3.up;

                // Outer ring (soft feather edge)
                int outIdx = 1 + segments + s;
                verts[outIdx] = new Vector3(cos * hwOuter, 0f, sin * hlOuter);
                colors[outIdx] = new Color(0f, 0f, 0f, 0.0f); // Fully transparent
                normals[outIdx] = Vector3.up;
            }

            int[] tris = new int[segments * 3 + segments * 6];
            int tIdx = 0;

            // Center fan
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                tris[tIdx++] = centerIdx;
                tris[tIdx++] = 1 + s;
                tris[tIdx++] = 1 + next;
            }

            // Outer feather ring
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int in0 = 1 + s;
                int in1 = 1 + next;
                int out0 = 1 + segments + s;
                int out1 = 1 + segments + next;

                tris[tIdx++] = in0; tris[tIdx++] = out0; tris[tIdx++] = in1;
                tris[tIdx++] = in1; tris[tIdx++] = out0; tris[tIdx++] = out1;
            }

            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Creates forward-facing crisp white 3D roof arrow mesh with bevel thickness.
        /// </summary>
        public static Mesh CreateRoofArrowMesh(float scale = 0.038f)
        {
            Mesh mesh = new Mesh { name = "RoofArrowMesh" };

            Vector3[] pts = new Vector3[]
            {
                new Vector3(0f, 0f, 9f) * scale,       // 0: Tip
                new Vector3(5.5f, 0f, 2.0f) * scale,   // 1: Right head corner
                new Vector3(2.2f, 0f, 2.0f) * scale,   // 2: Right shaft notch
                new Vector3(2.2f, 0f, -8f) * scale,    // 3: Right tail
                new Vector3(-2.2f, 0f, -8f) * scale,   // 4: Left tail
                new Vector3(-2.2f, 0f, 2.0f) * scale,  // 5: Left shaft notch
                new Vector3(-5.5f, 0f, 2.0f) * scale   // 6: Left head corner
            };

            int[] tris = new int[]
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
        /// Backward-compatible baseline bus body mesh.
        /// </summary>
        public static Mesh CreateBusBodyMesh(float width = BusWidth, float length = BusLength, float radius = CornerRadius, float height = BusHeight)
        {
            return CreateChamferedBodyMesh(VehicleArchetype.Minivan);
        }

        /// <summary>
        /// Backward-compatible tinted visor mesh.
        /// </summary>
        public static Mesh CreateTintedVisorMesh(float width = 0.80f, float length = 0.35f)
        {
            return CreateWindowMesh(VehicleArchetype.Minivan);
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
            verts[0] = new Vector3(-hwO, 0f, -hlO);
            verts[1] = new Vector3(hwO, 0f, -hlO);
            verts[2] = new Vector3(hwO, 0f, hlO);
            verts[3] = new Vector3(-hwO, 0f, hlO);
            verts[4] = new Vector3(-hwI, 0f, -hlI);
            verts[5] = new Vector3(hwI, 0f, -hlI);
            verts[6] = new Vector3(hwI, 0f, hlI);
            verts[7] = new Vector3(-hwI, 0f, hlI);

            int[] tris = new int[]
            {
                0, 4, 1, 1, 4, 5,
                1, 5, 2, 2, 5, 6,
                2, 6, 3, 3, 6, 7,
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
