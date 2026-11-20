using System.Collections.Generic;
using UnityEngine;

namespace RoadwaysGame
{
    /// <summary>
    /// Master Fleet Controller for the 8 Top-Down Buses:
    /// - Spawns 8 buses matching the 8 master color themes in the bottom depot row
    /// - Handles touch/pointer input raycasting
    /// - Performs path clearance check (is target bay occupied? is another bus in transit?)
    /// - Triggers rejection shake + warning horn if blocked
    /// - Dispatches bus along 3-phase Hermite driving path if clear
    /// - Handles reverse return to depot when docked bus is tapped
    /// </summary>
    [ExecuteAlways]
    public class BusFleetController : MonoBehaviour
    {
        [Header("Depot Layout")]
        [SerializeField] private float depotOriginZ = 520.0f;
        [SerializeField] private float busSpacing = 34.0f;
        [SerializeField] private Vector3 depotCenter = new Vector3(250.0f, 0f, 520.0f);

        [Header("References")]
        [SerializeField] private ParkingBayManager bayManager;
        [SerializeField] private TrackNetworkManager trackNetwork;

        [Header("Runtime Fleet (8 Buses)")]
        [SerializeField] private List<BusVehicleController> buses = new List<BusVehicleController>();

        private bool isAnyBusInFlight = false;
        private Camera mainCamera;

        public bool IsAnyBusInFlight => isAnyBusInFlight;
        public IReadOnlyList<BusVehicleController> Buses => buses;

        private void Start()
        {
            if (buses == null || buses.Count == 0 || transform.childCount == 0)
            {
                BuildDepotFleet();
            }
            mainCamera = Camera.main;
        }

        [ContextMenu("Build Depot Fleet")]
        public void BuildDepotFleet()
        {
            ClearFleet();

            if (bayManager == null) bayManager = FindObjectOfType<ParkingBayManager>();
            if (trackNetwork == null) trackNetwork = FindObjectOfType<TrackNetworkManager>();

            buses.Clear();
            int totalColors = ColorPaletteDatabase.Themes.Length; // 8
            float startX = depotCenter.x - ((totalColors - 1) * busSpacing * 0.5f);

            for (int i = 0; i < totalColors; i++)
            {
                ChilbiColorType type = (ChilbiColorType)i;
                Vector3 depotPos = new Vector3(startX + i * busSpacing, 0f, depotOriginZ);

                GameObject busObj = new GameObject($"Bus_{type}");
                busObj.transform.SetParent(transform, false);

                BusVehicleController controller = busObj.AddComponent<BusVehicleController>();
                controller.Initialize(type, depotPos);

                controller.OnBusDocked += HandleBusDocked;
                controller.OnBusReturnedToDepot += HandleBusReturned;

                buses.Add(controller);
            }

            Debug.Log($"<color=#2ecc71>[BusFleetController] Created depot fleet with 8 buses.</color>");
        }

        [ContextMenu("Clear Fleet")]
        public void ClearFleet()
        {
            buses.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                HandlePointerInput();
            }
        }

        private void HandlePointerInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (mainCamera == null) mainCamera = Camera.main;
                if (mainCamera == null) return;

                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    BusVehicleController clickedBus = hit.collider.GetComponentInParent<BusVehicleController>();
                    if (clickedBus != null)
                    {
                        OnBusTapped(clickedBus);
                    }
                }
            }
        }

        public void OnBusTapped(BusVehicleController bus)
        {
            if (bus == null) return;

            if (bus.CurrentState == BusState.InDepot)
            {
                // Clearance Verification:
                // 1. Is another bus currently in flight?
                if (isAnyBusInFlight)
                {
                    bus.PlayBlockedWiggle();
                    Debug.Log($"<color=#e74c3c>[BusFleet] Blocked: Another bus is already in transit!</color>");
                    return;
                }

                // 2. Is target parking bay available?
                if (bayManager == null) bayManager = FindObjectOfType<ParkingBayManager>();
                ParkingBaySlot targetSlot = bayManager != null ? bayManager.GetFirstAvailableBay() : null;

                if (targetSlot == null)
                {
                    bus.PlayBlockedWiggle();
                    Debug.Log($"<color=#e74c3c>[BusFleet] Blocked: All parking bays are occupied!</color>");
                    return;
                }

                // Path is clear: dispatch bus!
                isAnyBusInFlight = true;
                bus.StartDispatch(targetSlot);
                Debug.Log($"<color=#2ecc71>[BusFleet] Dispatched {bus.ColorType} bus to Bay {targetSlot.bayIndex}!</color>");
            }
            else if (bus.CurrentState == BusState.DockedInBay)
            {
                // Clicked docked bus -> Return to depot in reverse!
                if (!isAnyBusInFlight)
                {
                    isAnyBusInFlight = true;
                    bus.ReturnToDepot();
                    Debug.Log($"<color=#3498db>[BusFleet] Returning {bus.ColorType} bus back to depot.</color>");
                }
            }
        }

        private void HandleBusDocked(BusVehicleController bus)
        {
            isAnyBusInFlight = false;
            Debug.Log($"<color=#2ecc71>[BusFleet] Bus {bus.ColorType} safely docked in bay!</color>");
        }

        private void HandleBusReturned(BusVehicleController bus)
        {
            isAnyBusInFlight = false;
            Debug.Log($"<color=#3498db>[BusFleet] Bus {bus.ColorType} returned to depot slot.</color>");
        }
    }
}
