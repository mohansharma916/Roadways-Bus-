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

        [System.NonSerialized] private VehicleArchetype archetype = VehicleArchetype.Minivan;
        public VehicleArchetype Archetype => archetype;

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

        public void Initialize(ChilbiColorType type, Vector3 depotPos, VehicleArchetype vehArchetype = VehicleArchetype.Minivan)
        {
            colorType = type;
            archetype = vehArchetype;
            depotSlotPosition = depotPos;
            transform.localPosition = depotPos;
            transform.localRotation = Quaternion.identity;
            currentState = BusState.InDepot;
            passengerCount = 0;

            BuildBusVisuals();
            SetArrowVisibility(true);
        }

        public void SetArchetype(VehicleArchetype arch)
        {
            archetype = arch;
            BuildBusVisuals();
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
                float offsetX = Mathf.Sin(t * Mathf.PI * 4f) * 0.25f * (1f - t);
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
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? unlitShader;
            Color primary = ColorPaletteDatabase.GetPrimaryColor(colorType);

            BusMeshGenerator.GetDimensions(archetype, out float width, out float length, out float height, out _, out int cap);
            maxCapacity = cap;

            // Vehicle Body Root
            GameObject bodyObj = new GameObject("VehicleBody");
            bodyObj.transform.SetParent(transform, false);
            vehicleBody = bodyObj.transform;

            // 0. Soft Contact Blob Drop Shadow (underneath chassis on ground)
            GameObject shadowObj = new GameObject("ChassisDropShadow");
            shadowObj.transform.SetParent(vehicleBody, false);
            shadowObj.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            MeshFilter mfShadow = shadowObj.AddComponent<MeshFilter>();
            mfShadow.sharedMesh = BusMeshGenerator.CreateContactShadowMesh(archetype);
            MeshRenderer mrShadow = shadowObj.AddComponent<MeshRenderer>();
            Material shadowMat = new Material(unlitShader) { name = "M_VehicleDropShadow" };
            shadowMat.SetFloat("_Surface", 1); // Transparent
            shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shadowMat.SetInt("_ZWrite", 0);
            shadowMat.renderQueue = 2999;
            mrShadow.sharedMaterial = shadowMat;

            // 1. 4 Black Wheels under Chassis
            GameObject wheelsObj = new GameObject("VehicleWheels");
            wheelsObj.transform.SetParent(vehicleBody, false);
            MeshFilter mfWheels = wheelsObj.AddComponent<MeshFilter>();
            mfWheels.sharedMesh = BusMeshGenerator.CreateWheelMeshes(archetype);
            MeshRenderer mrWheels = wheelsObj.AddComponent<MeshRenderer>();
            Material wheelMat = new Material(litShader) { name = "M_VehicleRubber" };
            wheelMat.color = new Color32(0x1C, 0x22, 0x2A, 0xFF);
            if (wheelMat.HasProperty("_Smoothness")) wheelMat.SetFloat("_Smoothness", 0.35f);
            if (wheelMat.HasProperty("_Metallic")) wheelMat.SetFloat("_Metallic", 0.1f);
            mrWheels.sharedMaterial = wheelMat;

            // 2. Chamfered Vehicle Hull (URP Lit, glossy enamel clearcoat)
            GameObject hullObj = new GameObject("VehicleHull");
            hullObj.transform.SetParent(vehicleBody, false);
            MeshFilter mfHull = hullObj.AddComponent<MeshFilter>();
            mfHull.sharedMesh = BusMeshGenerator.CreateChamferedBodyMesh(archetype);
            MeshRenderer mrHull = hullObj.AddComponent<MeshRenderer>();
            Material bodyMat = new Material(litShader) { name = $"M_VehicleBody_{colorType}" };
            bodyMat.color = primary;
            if (bodyMat.HasProperty("_Smoothness")) bodyMat.SetFloat("_Smoothness", 0.72f);
            if (bodyMat.HasProperty("_Metallic")) bodyMat.SetFloat("_Metallic", 0.08f);
            mrHull.sharedMaterial = bodyMat;

            // 3. Tinted Glossy Glass Windows (URP Lit, dark reflective tint)
            GameObject windowsObj = new GameObject("VehicleWindows");
            windowsObj.transform.SetParent(vehicleBody, false);
            MeshFilter mfWindows = windowsObj.AddComponent<MeshFilter>();
            mfWindows.sharedMesh = BusMeshGenerator.CreateWindowMesh(archetype);
            MeshRenderer mrWindows = windowsObj.AddComponent<MeshRenderer>();
            Material glassMat = new Material(litShader) { name = "M_VehicleGlass" };
            glassMat.color = new Color32(0x14, 0x1C, 0x28, 0xFF);
            if (glassMat.HasProperty("_Smoothness")) glassMat.SetFloat("_Smoothness", 0.95f);
            if (glassMat.HasProperty("_Metallic")) glassMat.SetFloat("_Metallic", 0.25f);
            mrWindows.sharedMaterial = glassMat;

            // 4. Crisp White Roof Arrow (centered on roof)
            GameObject arrowObj = new GameObject("RoofArrow");
            arrowObj.transform.SetParent(vehicleBody, false);
            arrowObj.transform.localPosition = new Vector3(0f, height + 0.005f, 0f);
            MeshFilter mfArrow = arrowObj.AddComponent<MeshFilter>();
            mfArrow.sharedMesh = BusMeshGenerator.CreateRoofArrowMesh(0.038f);
            arrowRenderer = arrowObj.AddComponent<MeshRenderer>();
            Material arrowMat = new Material(litShader) { name = "M_VehicleRoofArrow" };
            arrowMat.color = Color.white;
            if (arrowMat.HasProperty("_Smoothness")) arrowMat.SetFloat("_Smoothness", 0.85f);
            arrowRenderer.sharedMaterial = arrowMat;

            // 5. Touch Box Collider sized to archetype
            touchCollider = GetComponent<BoxCollider>();
            if (touchCollider == null) touchCollider = gameObject.AddComponent<BoxCollider>();
            touchCollider.size = new Vector3(width, 0.9f, length);
            touchCollider.center = new Vector3(0f, 0.45f, 0f);
        }
    }
}
