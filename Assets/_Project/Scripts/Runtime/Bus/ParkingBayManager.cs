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
            if (slots == null || slots.Count == 0 || transform.childCount == 0)
            {
                BuildTrapeziumTerminal();
            }
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
            if (standardBorderMaterial == null)
                standardBorderMaterial = new Material(unlitShader) { name = "M_BayStandardBorder", color = Color.white };
            if (vipBorderMaterial == null)
                vipBorderMaterial = new Material(unlitShader) { name = "M_BayVIPBorder", color = new Color(1f, 0.84f, 0f) }; // Gold #ffd700
            if (unlockBorderMaterial == null)
                unlockBorderMaterial = new Material(unlitShader) { name = "M_BayUnlockBorder", color = new Color(0.18f, 0.80f, 0.44f) }; // Green #2ecc71
        }

        private void BuildTrapeziumPanel()
        {
            GameObject panelObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panelObj.name = "Gen_TrapeziumFloorPanel";
            panelObj.transform.SetParent(transform, false);

            // Panel covering terminal bay and fleet depot area
            panelObj.transform.localPosition = new Vector3(0f, 0.005f, 22.5f);
            panelObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            panelObj.transform.localScale = new Vector3(14.0f, 10.0f, 1f);

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

            // Unit direction vector pointing INTO the -30 deg bay: (sin(-30), 0, cos(-30)) = (-0.5, 0, 0.866025)
            float rad = bayAngleDeg * Mathf.Deg2Rad;
            Vector3 bayForward = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            // Step vector between adjacent bays in the single horizontal row
            // Width projected perpendicular + spacing
            float effectiveSpacing = 1.12f;

            for (int i = 0; i < totalBays; i++)
            {
                BayType bType = BayType.Standard;
                Material bMat = standardBorderMaterial;
                bool locked = false;

                if (i == 5)
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
            }
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
