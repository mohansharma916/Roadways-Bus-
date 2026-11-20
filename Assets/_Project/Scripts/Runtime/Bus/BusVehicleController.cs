using System;
using UnityEngine;

namespace RoadwaysGame
{
    public enum BusState
    {
        InDepot,
        DrivingToBay,
        DockedInBay,
        ReturningToDepot,
        FullDeparting
    }

    /// <summary>
    /// Controls an individual top-down vehicle:
    /// - 3-Phase Hermite driving kinematics (velocity-tangent steering)
    /// - Forward-facing white roof arrow (auto-disappears upon docking)
    /// - Reverse back-out return to depot
    /// - Suspension squat on acceleration & dip on braking
    /// - Rejection blocked wiggle
    /// - Capacity counter (0/7)
    /// </summary>
    [ExecuteAlways]
    public class BusVehicleController : MonoBehaviour
    {
        [Header("Bus Properties")]
        [SerializeField] private ChilbiColorType colorType = ChilbiColorType.Cyan;
        [SerializeField] private BusState currentState = BusState.InDepot;
        [SerializeField] private int passengerCount = 0;
        [SerializeField] private int maxCapacity = GameConstants.CapacityPerBus; // 7

        [Header("Rig Elements")]
        [SerializeField] private Transform vehicleBody;
        [SerializeField] private MeshRenderer arrowRenderer;
        [SerializeField] private BoxCollider touchCollider;

        [Header("Depot Origin")]
        [SerializeField] private Vector3 depotSlotPosition;

        // Kinematic Animation Variables
        private Vector3 startPos;
        private Vector3 startTan;
        private Vector3 entryPos;
        private Vector3 entryTan;
        private Vector3 dockPos;
        private float dispatchTimer;
        private float dispatchDuration = 1.4f; // 1400ms matching prototype
        private ParkingBaySlot targetBaySlot;

        // Blocked Shake Variables
        private bool isShaking = false;
        private float shakeTimer = 0f;

        public ChilbiColorType ColorType => colorType;
        public BusState CurrentState => currentState;
        public int PassengerCount => passengerCount;
        public bool IsFull => passengerCount >= maxCapacity;
        public ParkingBaySlot TargetBaySlot => targetBaySlot;

        public event Action<BusVehicleController> OnBusDocked;
        public event Action<BusVehicleController> OnBusReturnedToDepot;

        private void Awake()
        {
            if (vehicleBody == null) BuildBusVisuals();
        }

        public void Initialize(ChilbiColorType type, Vector3 depotPos)
        {
            colorType = type;
            depotSlotPosition = depotPos;
            transform.localPosition = depotPos;
            transform.localRotation = Quaternion.identity;
            currentState = BusState.InDepot;
            passengerCount = 0;

            BuildBusVisuals();
            SetArrowVisibility(true);
        }

        public void PlayBlockedWiggle()
        {
            if (isShaking) return;
            isShaking = true;
            shakeTimer = 0f;
        }

        public void StartDispatch(ParkingBaySlot slot)
        {
            if (currentState != BusState.InDepot) return;

            targetBaySlot = slot;
            targetBaySlot.isOccupied = true;
            targetBaySlot.currentBus = this;

            currentState = BusState.DrivingToBay;
            dispatchTimer = 0f;
            SetArrowVisibility(true);

            startPos = transform.localPosition;
            dockPos = slot.centerPosition;
            entryPos = slot.entryWaypoint;

            // Hermite tangents
            float roadDist = Vector3.Distance(startPos, entryPos);
            startTan = Vector3.forward * (roadDist * 0.85f); // Strictly forward in arrow direction

            float rad = slot.angleDeg * Mathf.Deg2Rad;
            Vector3 bayDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            entryTan = bayDir * (roadDist * 0.85f);
        }

        public void ReturnToDepot()
        {
            if (currentState != BusState.DockedInBay) return;

            if (targetBaySlot != null)
            {
                targetBaySlot.isOccupied = false;
                targetBaySlot.currentBus = null;
            }

            currentState = BusState.ReturningToDepot;
            dispatchTimer = 0f;
            SetArrowVisibility(true); // Restore roof arrow upon return

            startPos = transform.localPosition;
            dockPos = depotSlotPosition;
            entryPos = targetBaySlot != null ? targetBaySlot.entryWaypoint : startPos;

            float roadDist = Vector3.Distance(entryPos, dockPos);
            float rad = (targetBaySlot != null ? targetBaySlot.angleDeg : -30f) * Mathf.Deg2Rad;
            Vector3 bayDir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            startTan = -bayDir * (roadDist * 0.85f); // Reverse out of bay
            entryTan = Vector3.forward * (roadDist * 0.85f);
        }

        private void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.0166f;

            if (isShaking)
            {
                UpdateBlockedShake(dt);
            }

            if (currentState == BusState.DrivingToBay)
            {
                UpdateDispatchDrive(dt);
            }
            else if (currentState == BusState.ReturningToDepot)
            {
                UpdateReturnDrive(dt);
            }
        }

        private void UpdateDispatchDrive(float dt)
        {
            dispatchTimer += dt;
            float progress = Mathf.Clamp01(dispatchTimer / dispatchDuration);

            Vector3 curPos;
            float curHeading;
            Vector3 scale = Vector3.one;

            if (progress <= 0.65f)
            {
                // Phase 1: Hermite Roadway Drive (steering follows velocity tangent)
                float u = progress / 0.65f;
                float ease = u * u * (3f - 2f * u); // smoothstep

                HermiteSample sample = HermiteKinematics.Evaluate(startPos, startTan, entryPos, entryTan, ease);
                curPos = sample.position;
                curHeading = sample.headingAngleDeg;

                // Acceleration forward stretch
                if (u < 0.22f)
                {
                    float squat = Mathf.Sin((u / 0.22f) * Mathf.PI) * 0.035f;
                    scale = new Vector3(1f - squat * 0.6f, 1f, 1f + squat);
                }
            }
            else if (progress <= 0.94f)
            {
                // Phase 2: Straight corridor penetration into bay at -30 deg
                float u = (progress - 0.65f) / 0.29f;
                float ease = 1f - Mathf.Pow(1f - u, 2.4f); // ease out

                curPos = Vector3.Lerp(entryPos, dockPos, ease);
                curHeading = targetBaySlot != null ? targetBaySlot.angleDeg : -30f;

                // Braking nose dip
                float dip = Mathf.Sin(u * Mathf.PI) * 0.03f;
                scale = new Vector3(1f + dip * 0.7f, 1f, 1f - dip);
            }
            else
            {
                // Phase 3: Final braking rebound settle
                float u = (progress - 0.94f) / 0.06f;
                curPos = dockPos;
                curHeading = targetBaySlot != null ? targetBaySlot.angleDeg : -30f;

                float rebound = Mathf.Sin(u * Mathf.PI) * 0.015f;
                scale = new Vector3(1f - rebound, 1f, 1f + rebound);
            }

            transform.localPosition = curPos;
            transform.localRotation = Quaternion.Euler(0f, curHeading, 0f);
            if (vehicleBody != null) vehicleBody.localScale = scale;

            if (progress >= 1f)
            {
                // Arrived & Docked
                transform.localPosition = dockPos;
                transform.localRotation = Quaternion.Euler(0f, targetBaySlot != null ? targetBaySlot.angleDeg : -30f, 0f);
                if (vehicleBody != null) vehicleBody.localScale = Vector3.one;

                currentState = BusState.DockedInBay;
                SetArrowVisibility(false); // Roof arrow disappears upon docking!
                OnBusDocked?.Invoke(this);
            }
        }

        private void UpdateReturnDrive(float dt)
        {
            dispatchTimer += dt;
            float progress = Mathf.Clamp01(dispatchTimer / (dispatchDuration * 0.85f));

            float ease = Mathf.SmoothStep(0f, 1f, progress);
            transform.localPosition = Vector3.Lerp(startPos, dockPos, ease);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, ease);

            if (progress >= 1f)
            {
                transform.localPosition = depotSlotPosition;
                transform.localRotation = Quaternion.identity;
                currentState = BusState.InDepot;
                targetBaySlot = null;
                SetArrowVisibility(true);
                OnBusReturnedToDepot?.Invoke(this);
            }
        }

        private void UpdateBlockedShake(float dt)
        {
            shakeTimer += dt;
            float dur = 0.35f;

            if (shakeTimer < dur)
            {
                float t = shakeTimer / dur;
                // -4 -> +4 -> -3 -> 0
                float offsetX = Mathf.Sin(t * Mathf.PI * 4f) * 4f * (1f - t);
                transform.localPosition = depotSlotPosition + Vector3.right * offsetX;
            }
            else
            {
                transform.localPosition = depotSlotPosition;
                isShaking = false;
            }
        }

        public void SetArrowVisibility(bool visible)
        {
            if (arrowRenderer != null)
            {
                arrowRenderer.enabled = visible;
            }
        }

        public void BoardPassenger()
        {
            passengerCount = Mathf.Clamp(passengerCount + 1, 0, maxCapacity);
        }

        [ContextMenu("Build Bus Visuals")]
        public void BuildBusVisuals()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Color primary = ColorPaletteDatabase.GetPrimaryColor(colorType);

            // Vehicle Body Root
            GameObject bodyObj = new GameObject("VehicleBody");
            bodyObj.transform.SetParent(transform, false);
            vehicleBody = bodyObj.transform;

            // 1. Bus Hull Mesh (25x50 units, rx=5.5)
            GameObject hullObj = new GameObject("BusHull");
            hullObj.transform.SetParent(vehicleBody, false);
            MeshFilter mfHull = hullObj.AddComponent<MeshFilter>();
            mfHull.sharedMesh = BusMeshGenerator.CreateBusBodyMesh(GameConstants.BusWidth, GameConstants.BusLength, GameConstants.BusCornerRadius, 3.5f);
            MeshRenderer mrHull = hullObj.AddComponent<MeshRenderer>();
            mrHull.sharedMaterial = new Material(unlitShader) { name = "M_BusMatteBody", color = primary };

            // 2. Front Windshield Visor (#1e2430)
            GameObject visorObj = new GameObject("FrontVisor");
            visorObj.transform.SetParent(vehicleBody, false);
            visorObj.transform.localPosition = new Vector3(0f, 3.52f, 16f); // Near front
            MeshFilter mfVisor = visorObj.AddComponent<MeshFilter>();
            mfVisor.sharedMesh = BusMeshGenerator.CreateTintedVisorMesh(21f, 9f);
            MeshRenderer mrVisor = visorObj.AddComponent<MeshRenderer>();
            mrVisor.sharedMaterial = new Material(unlitShader) { name = "M_BusVisor", color = new Color32(0x1E, 0x24, 0x30, 0xFF) };

            // 3. Crisp White Roof Arrow (centered on roof)
            GameObject arrowObj = new GameObject("RoofArrow");
            arrowObj.transform.SetParent(vehicleBody, false);
            arrowObj.transform.localPosition = new Vector3(0f, 3.53f, -2f);
            MeshFilter mfArrow = arrowObj.AddComponent<MeshFilter>();
            mfArrow.sharedMesh = BusMeshGenerator.CreateRoofArrowMesh(0.85f);
            arrowRenderer = arrowObj.AddComponent<MeshRenderer>();
            arrowRenderer.sharedMaterial = new Material(unlitShader) { name = "M_BusRoofArrow", color = Color.white };

            // 4. Touch Box Collider
            touchCollider = GetComponent<BoxCollider>();
            if (touchCollider == null) touchCollider = gameObject.AddComponent<BoxCollider>();
            touchCollider.size = new Vector3(GameConstants.BusWidth, 5f, GameConstants.BusLength);
            touchCollider.center = new Vector3(0f, 2.5f, 0f);
        }
    }
}
