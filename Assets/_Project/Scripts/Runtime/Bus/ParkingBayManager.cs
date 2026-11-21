using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    public enum BayType
    {
        Standard, // White border
        VIP,      // Gold border
        Unlock    // Green border
    }

    [System.Serializable]
    public class ParkingBaySlot
    {
        public int bayIndex;
        public BayType bayType;
        public Vector3 centerPosition;
        public Vector3 entryWaypoint;
        public float angleDeg = GameConstants.ParkingBayAngleDeg; // -30.0f
        public bool isOccupied;
        public bool isLocked;
        public BusVehicleController currentBus;
    }

    /// <summary>
    /// Manages the Trapezium Terminal Panel and all 9 Angled Parking Bays (-30 deg).
    /// </summary>
    [ExecuteAlways]
    public class ParkingBayManager : MonoBehaviour
    {
        [Header("Bay Configuration")]
        [SerializeField] private int totalBays = GameConstants.TotalParkingBays; // 9
        [SerializeField] private float bayWidth = 0.95f;
        [SerializeField] private float bayLength = 1.90f;
        [SerializeField] private float baySpacing = 0.28f;
        [SerializeField] private float bayAngleDeg = GameConstants.ParkingBayAngleDeg; // -30.0f
        [SerializeField] private float entryCorridorDist = 2.0f;

        [Header("Base Center Position in Unity World Space")]
        [SerializeField] private Vector3 firstBayCenter = new Vector3(-4.48f, 0f, 19.5f);

        [Header("Materials (Auto-Created if Null)")]
        [SerializeField] private Material panelMaterial;
        [SerializeField] private Material standardBorderMaterial;
        [SerializeField] private Material vipBorderMaterial;
        [SerializeField] private Material unlockBorderMaterial;

        [Header("Runtime Bay Slots (9)")]
        [SerializeField] private List<ParkingBaySlot> slots = new List<ParkingBaySlot>();

        public int TotalBays => totalBays;
        public IReadOnlyList<ParkingBaySlot> Slots => slots;

        private void Start()
        {
            BuildTrapeziumTerminal();
        }

        [ContextMenu("Build Trapezium Terminal")]
        public void BuildTrapeziumTerminal()
        {
            ClearTerminal();
            EnsureMaterials();
            BuildTrapeziumPanel();
            BuildParkingBaySlots();
        }

        [ContextMenu("Clear Terminal")]
        public void ClearTerminal()
        {
            slots.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private void EnsureMaterials()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");

            if (panelMaterial == null)
                panelMaterial = new Material(unlitShader) { name = "M_TrapeziumPanel", color = GameConstants.AsphaltColor };
            else
                panelMaterial.color = GameConstants.AsphaltColor;

            if (standardBorderMaterial == null)
                standardBorderMaterial = new Material(unlitShader) { name = "M_BayStandardBorder", color = GameConstants.WhiteCurbColor };
            else
                standardBorderMaterial.color = GameConstants.WhiteCurbColor;

            if (vipBorderMaterial == null)
                vipBorderMaterial = new Material(unlitShader) { name = "M_BayVIPBorder", color = new Color(1.0f, 0.78f, 0.05f) }; // Gold #ffc048
            else
                vipBorderMaterial.color = new Color(1.0f, 0.78f, 0.05f);

            if (unlockBorderMaterial == null)
                unlockBorderMaterial = new Material(unlitShader) { name = "M_BayUnlockBorder", color = new Color(0.18f, 0.85f, 0.44f) }; // Emerald green #2ecc71
            else
                unlockBorderMaterial.color = new Color(0.18f, 0.85f, 0.44f);
        }

        private void BuildTrapeziumPanel()
        {
            GameObject panelObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panelObj.name = "Gen_TrapeziumFloorPanel";
            panelObj.transform.SetParent(transform, false);

            // Clean dark slate asphalt apron hosting all 9 bays
            panelObj.transform.localPosition = new Vector3(0f, 0.005f, 19.8f);
            panelObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            panelObj.transform.localScale = new Vector3(13.5f, 4.2f, 1f);

            Renderer r = panelObj.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = panelMaterial;

            Collider c = panelObj.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
        }

        private void BuildParkingBaySlots()
        {
            slots.Clear();
            GameObject baysGroup = new GameObject("Gen_ParkingBaySlots");
            baysGroup.transform.SetParent(transform, false);

            Mesh borderMesh = BusMeshGenerator.CreateParkingSlotBorderMesh(bayWidth, bayLength, 0.08f);
            Mesh plusBadgeMesh = CreatePlusMesh();
            Mesh vipBadgeMesh = CreateVipBadgeMesh();

            // Unit direction vector pointing INTO the -30 deg bay: (sin(-30), 0, cos(-30)) = (-0.5, 0, 0.866025)
            float rad = bayAngleDeg * Mathf.Deg2Rad;
            Vector3 bayForward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            // Step vector between adjacent bays in the single horizontal row
            float effectiveSpacing = 1.12f;

            for (int i = 0; i < totalBays; i++)
            {
                BayType bType = BayType.Standard;
                Material bMat = standardBorderMaterial;
                bool locked = false;

                if (i == 0)
                {
                    bType = BayType.VIP;
                    bMat = vipBorderMaterial;
                }
                else if (i >= 6)
                {
                    bType = BayType.Unlock;
                    bMat = unlockBorderMaterial;
                    locked = true;
                }

                Vector3 center = firstBayCenter + new Vector3(i * effectiveSpacing, 0f, 0f);
                Vector3 entryMouth = center - bayForward * entryCorridorDist;

                ParkingBaySlot slot = new ParkingBaySlot
                {
                    bayIndex = i,
                    bayType = bType,
                    centerPosition = center,
                    entryWaypoint = entryMouth,
                    angleDeg = bayAngleDeg,
                    isOccupied = false,
                    isLocked = locked
                };
                slots.Add(slot);

                // Slot visual GameObject
                GameObject slotObj = new GameObject($"BaySlot_{i}_{bType}");
                slotObj.transform.SetParent(baysGroup.transform, false);
                slotObj.transform.localPosition = center + Vector3.up * 0.015f;
                slotObj.transform.localRotation = Quaternion.Euler(0f, bayAngleDeg, 0f);

                MeshFilter mf = slotObj.AddComponent<MeshFilter>();
                mf.sharedMesh = borderMesh;
                MeshRenderer mr = slotObj.AddComponent<MeshRenderer>();
                mr.sharedMaterial = bMat;

                // 3D Badges: VIP text for Bay 0, '+' sign for Unlock bays (6-8)
                if (bType == BayType.VIP)
                {
                    GameObject vipBadgeObj = new GameObject("VIPBadge");
                    vipBadgeObj.transform.SetParent(slotObj.transform, false);
                    vipBadgeObj.transform.localPosition = new Vector3(0f, 0.005f, 0f);
                    MeshFilter vmf = vipBadgeObj.AddComponent<MeshFilter>();
                    vmf.sharedMesh = vipBadgeMesh;
                    MeshRenderer vmr = vipBadgeObj.AddComponent<MeshRenderer>();
                    vmr.sharedMaterial = vipBorderMaterial;
                }
                else if (bType == BayType.Unlock)
                {
                    GameObject plusBadgeObj = new GameObject("PlusBadge");
                    plusBadgeObj.transform.SetParent(slotObj.transform, false);
                    plusBadgeObj.transform.localPosition = new Vector3(0f, 0.005f, 0f);
                    MeshFilter pmf = plusBadgeObj.AddComponent<MeshFilter>();
                    pmf.sharedMesh = plusBadgeMesh;
                    MeshRenderer pmr = plusBadgeObj.AddComponent<MeshRenderer>();
                    pmr.sharedMaterial = unlockBorderMaterial;
                }
            }
        }

        private Mesh CreatePlusMesh()
        {
            Mesh mesh = new Mesh { name = "BayPlusBadgeMesh" };
            float hL = 0.16f; // half arm length
            float hW = 0.045f; // half bar width

            Vector3[] verts = new Vector3[8];
            // Horizontal bar
            verts[0] = new Vector3(-hL, 0f, -hW);
            verts[1] = new Vector3(hL, 0f, -hW);
            verts[2] = new Vector3(hL, 0f, hW);
            verts[3] = new Vector3(-hL, 0f, hW);
            // Vertical bar
            verts[4] = new Vector3(-hW, 0f, -hL);
            verts[5] = new Vector3(hW, 0f, -hL);
            verts[6] = new Vector3(hW, 0f, hL);
            verts[7] = new Vector3(-hW, 0f, hL);

            int[] tris = new int[]
            {
                0, 2, 1, 0, 3, 2,
                4, 6, 5, 4, 7, 6
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

        private Mesh CreateVipBadgeMesh()
        {
            Mesh mesh = new Mesh { name = "BayVIPBadgeMesh" };
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            // Helper to add a flat quad strip
            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int baseIdx = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 0); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            }

            float w = 0.04f; // stroke width

            // 'V'
            AddQuad(new Vector3(-0.30f, 0f, 0.18f), new Vector3(-0.25f, 0f, 0.18f), new Vector3(-0.18f, 0f, -0.18f), new Vector3(-0.23f, 0f, -0.18f));
            AddQuad(new Vector3(-0.23f, 0f, -0.18f), new Vector3(-0.18f, 0f, -0.18f), new Vector3(-0.10f, 0f, 0.18f), new Vector3(-0.15f, 0f, 0.18f));

            // 'I'
            AddQuad(new Vector3(-0.02f, 0f, -0.18f), new Vector3(0.03f, 0f, -0.18f), new Vector3(0.03f, 0f, 0.18f), new Vector3(-0.02f, 0f, 0.18f));

            // 'P'
            AddQuad(new Vector3(0.12f, 0f, -0.18f), new Vector3(0.16f, 0f, -0.18f), new Vector3(0.16f, 0f, 0.18f), new Vector3(0.12f, 0f, 0.18f));
            AddQuad(new Vector3(0.16f, 0f, 0.14f), new Vector3(0.26f, 0f, 0.14f), new Vector3(0.26f, 0f, 0.18f), new Vector3(0.16f, 0f, 0.18f));
            AddQuad(new Vector3(0.22f, 0f, -0.02f), new Vector3(0.26f, 0f, -0.02f), new Vector3(0.26f, 0f, 0.18f), new Vector3(0.22f, 0f, 0.18f));
            AddQuad(new Vector3(0.16f, 0f, -0.02f), new Vector3(0.26f, 0f, -0.02f), new Vector3(0.26f, 0f, 0.02f), new Vector3(0.16f, 0f, 0.02f));

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            Vector3[] normals = new Vector3[verts.Count];
            for (int n = 0; n < normals.Length; n++) normals[n] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        public ParkingBaySlot GetFirstAvailableBay()
        {
            foreach (var slot in slots)
            {
                if (!slot.isOccupied && !slot.isLocked)
                {
                    return slot;
                }
            }
            return null;
        }

        public ParkingBaySlot GetSlot(int index)
        {
            if (index >= 0 && index < slots.Count) return slots[index];
            return null;
        }
    }
}
