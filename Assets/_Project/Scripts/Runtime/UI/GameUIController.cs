using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace RoadwaysGame
{
    /// <summary>
    /// Master UI Controller for Roadways Bus Game:
    /// - Screen-space overlay canvas scaled for portrait mobile screens (1080x1920 reference)
    /// - Safe Area / Notch adaptation for tall 9:16 to 20:9 Android screens
    /// - Supports "Cherry Blossom Park Only" isolated view mode (hiding Road Network, Buses, and Chilbis)
    /// - Sleek glassmorphic Watermark Pill: "Cherry Blossom Park"
    /// </summary>
    [ExecuteAlways]
    public class GameUIController : MonoBehaviour
    {
        public static GameUIController Instance { get; private set; }

        [Header("Display Mode")]
        [Tooltip("When enabled, hides Ring Road, Parking Lot, Buses, and Chilbis to show ONLY Cherry Blossom Park.")]
        [SerializeField] private bool parkOnlyMode = false;

        [Header("Bottom Panel Specifications")]
        [Tooltip("Covers 50% of height from bottom, full width, filled with soft blue-grey (#d3daed), 2px white top border only.")]
        [SerializeField] private bool showBottomPanel = true;
        [SerializeField] private float bottomPanelHeightRatio = 0.50f; // 50% height
        [SerializeField] private Color bottomPanelColor = new Color32(0xD3, 0xDA, 0xED, 0xFF); // Soft blue-grey (#d3daed)
        [SerializeField] private float topBorderThickness = 2.0f; // 2px
        [SerializeField] private Color topBorderColor = Color.white;

        [Header("Trapezium Specifications")]
        [Tooltip("Trapezium positioned directly above bottom panel, with wide upper width, rounded top corners, and submerged bottom corners.")]
        [SerializeField] private bool showTrapezium = true;
        [SerializeField] private float trapeziumHeight = 240f;
        [SerializeField] private float trapeziumTopWidthRatio = 0.82f;
        [SerializeField] private float trapeziumBottomWidthRatio = 1.16f;
        [SerializeField] private float trapeziumCornerRadius = 32f;
        [SerializeField] private Color trapeziumColor = new Color32(0x52, 0x57, 0x69, 0xFF); // Asphalt/Apron grey (#525769)

        [Header("Circuit Road Specifications")]
        [Tooltip("Procedural racing road circuit positioned directly above the trapezium.")]
        [SerializeField] private bool showRoadCircuit = true;
        [SerializeField] private float roadCircuitHeight = 660f;
        [SerializeField] private Sprite roadCircuitSprite;

        [Header("UI State (Dynamic)")]
        [SerializeField] private int clearedChilbis = 0;
        [SerializeField] private int totalChilbis = GameConstants.TotalChilbis; // 430
        [SerializeField] private int circuitChilbis = GameConstants.CircuitMaxChilbis; // 420
        [SerializeField] private int queuedChilbis = 10;
        [SerializeField] private int dockedBusesCount = 0;

        [Header("UI References")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private GameObject watermarkPill;
        [SerializeField] private GameObject statusPill;
        [SerializeField] private GameObject replayBtnObj;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text replayBtnText;
        [SerializeField] private Button replayButton;

        [System.NonSerialized] private GameObject settingsModalObj;
        [System.NonSerialized] private GameObject campaignMapModalObj;
        [System.NonSerialized] private GameObject mapButtonObj;
        [System.NonSerialized] private GameObject boosterDockObj;
        [System.NonSerialized] private Text recallBtnText;
        [System.NonSerialized] private Text shuffleBtnText;
        [System.NonSerialized] private Text sfxToggleText;
        [System.NonSerialized] private Text musicToggleText;
        [System.NonSerialized] private Text hapticsToggleText;
        [System.NonSerialized] private Image sfxToggleImg;
        [System.NonSerialized] private Image musicToggleImg;
        [System.NonSerialized] private Image hapticsToggleImg;

        public AudioSource AudioSource => GetComponent<AudioSource>();

        private static Font defaultFont;
        private ChilbiFormationManager formationManager;
        private BusFleetController fleetController;
        private float lastUpdateTime = 0f;

        public bool ParkOnlyMode
        {
            get => parkOnlyMode;
            set
            {
                parkOnlyMode = value;
                ApplyDisplayMode();
                BuildUI();
            }
        }

        private void Awake()
        {
            Instance = this;
            bottomPanelColor = new Color32(0xD3, 0xDA, 0xED, 0xFF);
            trapeziumColor = new Color32(0x52, 0x57, 0x69, 0xFF);
            showRoadCircuit = true;
            ApplyDisplayMode();
            BuildUI();
        }

        private void OnEnable()
        {
            Instance = this;
            ApplyDisplayMode();
            if (mainCanvas == null || transform.Find("MainGameCanvas") == null)
            {
                BuildUI();
            }
        }

        private void Start()
        {
            ApplyDisplayMode();
            formationManager = FindAnyObjectByType<ChilbiFormationManager>();
            fleetController = FindAnyObjectByType<BusFleetController>();
            UpdateStatusDisplay();
        }

        private void Update()
        {
            if (Time.unscaledTime - lastUpdateTime > 0.25f)
            {
                lastUpdateTime = Time.unscaledTime;
                ApplyDisplayMode();
                if (!parkOnlyMode)
                {
                    RefreshState();
                }
            }
        }

        /// <summary>
        /// Toggles or applies visibility of Ring Road, Parking Lot, Buses, and Chilbis.
        /// </summary>
        public void ApplyDisplayMode()
        {
            GameObject roadNetwork = GameObject.Find("--- [ROAD_NETWORK] ---");
            GameObject busSystem = GameObject.Find("--- [BUS_SYSTEM] ---");
            GameObject characters = GameObject.Find("--- [CHARACTERS] ---");

            bool showOtherSystems = !parkOnlyMode;

            if (roadNetwork != null && roadNetwork.activeSelf != showOtherSystems)
            {
                roadNetwork.SetActive(showOtherSystems);
            }
            if (busSystem != null && busSystem.activeSelf != showOtherSystems)
            {
                busSystem.SetActive(showOtherSystems);
            }
            if (characters != null && characters.activeSelf != showOtherSystems)
            {
                characters.SetActive(showOtherSystems);
            }
        }

        [ContextMenu("Toggle Park Only Mode")]
        public void ToggleParkOnlyMode()
        {
            ParkOnlyMode = !ParkOnlyMode;
        }

        private void RefreshState()
        {
            if (fleetController == null) fleetController = FindAnyObjectByType<BusFleetController>();
            if (fleetController != null && fleetController.Buses != null)
            {
                int docked = 0;
                for (int i = 0; i < fleetController.Buses.Count; i++)
                {
                    var bus = fleetController.Buses[i];
                    if (bus != null && bus.CurrentState == BusState.DockedInBay)
                    {
                        docked++;
                    }
                }
                dockedBusesCount = docked;
            }

            UpdateStatusDisplay();
        }

        [ContextMenu("Build Full UI")]
        public void BuildUI()
        {
            bottomPanelColor = new Color32(0xD3, 0xDA, 0xED, 0xFF);
            trapeziumColor = new Color32(0x52, 0x57, 0x69, 0xFF);
            showRoadCircuit = true;
            EnsureFont();
            EnsureEventSystem();

            Transform canvasTrans = transform.Find("MainGameCanvas");
            GameObject canvasObj;
            if (canvasTrans == null)
            {
                canvasObj = new GameObject("MainGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObj.transform.SetParent(transform, false);
            }
            else
            {
                canvasObj = canvasTrans.gameObject;
            }

            // Clean existing children under canvas
            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(canvasObj.transform.GetChild(i).gameObject);
                else DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            mainCanvas = canvasObj.GetComponent<Canvas>();
            if (mainCanvas == null) mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            mainCanvas.sortingOrder = 100;

            canvasScaler = canvasObj.GetComponent<CanvasScaler>();
            if (canvasScaler == null) canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasScaler.matchWidthOrHeight = 0f; // Match width for portrait fit

            GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvasObj.AddComponent<GraphicRaycaster>();

            // Generate rounded pill sprite with 24px smooth corner radii
            Sprite pillSprite = CreatePillSprite(128, 64, 24);

            // 1. Trapezium Panel (directly above bottom panel, wide upper width, rounded top corners, submerged bottom corners)
            if (showTrapezium)
            {
                GameObject trapObj = new GameObject("TrapeziumPanel", typeof(RectTransform), typeof(Image));
                trapObj.transform.SetParent(canvasObj.transform, false);
                RectTransform trapRt = trapObj.GetComponent<RectTransform>();
                trapRt.anchorMin = new Vector2(0.5f, bottomPanelHeightRatio);
                trapRt.anchorMax = new Vector2(0.5f, bottomPanelHeightRatio);
                trapRt.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center so it sits directly on top of 50% line
                trapRt.anchoredPosition = Vector2.zero;
                trapRt.sizeDelta = new Vector2(1080f, trapeziumHeight);

                Image trapImg = trapObj.GetComponent<Image>();
                trapImg.sprite = CreateTrapeziumSprite(1080, Mathf.RoundToInt(trapeziumHeight), trapeziumTopWidthRatio, trapeziumBottomWidthRatio, trapeziumCornerRadius);
                trapImg.type = Image.Type.Simple;
                trapImg.color = trapeziumColor;

                // 9 Angled Rectangular Parking Bays inside Trapezium (matching HTML 3D_Showcase_Preview.html)
                GameObject baysContainer = new GameObject("TrapeziumBoxesContainer", typeof(RectTransform));
                baysContainer.transform.SetParent(trapObj.transform, false);
                RectTransform bcRt = baysContainer.GetComponent<RectTransform>();
                bcRt.anchorMin = new Vector2(0.5f, 0.5f);
                bcRt.anchorMax = new Vector2(0.5f, 0.5f);
                bcRt.pivot = new Vector2(0.5f, 0.5f);
                bcRt.anchoredPosition = Vector2.zero;
                bcRt.sizeDelta = new Vector2(1080f, trapeziumHeight);

                Color32 bayBg = new Color32(0x62, 0x64, 0x79, 0xFF); // #626479
                Color32 whiteBorder = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // #ffffff
                Color32 vipBorder = new Color32(0xFF, 0xD7, 0x00, 0xFF); // #ffd700
                Color32 greenBorder = new Color32(0x2E, 0xCC, 0x71, 0xFF); // #2ecc71

                Sprite standardBoxSprite = CreateRoundedBoxSprite(68, 136, 16, 5, whiteBorder, bayBg);
                Sprite vipBoxSprite = CreateRoundedBoxSprite(68, 136, 16, 5, vipBorder, bayBg);
                Sprite greenBoxSprite = CreateRoundedBoxSprite(68, 136, 16, 5, greenBorder, bayBg);

                float baySpacing = 92f;
                for (int i = 0; i < 9; i++)
                {
                    GameObject bayObj = new GameObject($"TrapeziumBox_{i}", typeof(RectTransform), typeof(Image));
                    bayObj.transform.SetParent(baysContainer.transform, false);
                    RectTransform bayRt = bayObj.GetComponent<RectTransform>();
                    bayRt.anchorMin = new Vector2(0.5f, 0.5f);
                    bayRt.anchorMax = new Vector2(0.5f, 0.5f);
                    bayRt.pivot = new Vector2(0.5f, 0.5f);
                    bayRt.anchoredPosition = new Vector2((i - 4) * baySpacing, 0f);
                    bayRt.sizeDelta = new Vector2(68f, 136f);
                    bayRt.localEulerAngles = new Vector3(0f, 0f, 30f); // Tilted 30 degrees counter-clockwise (matches CSS rotate(-30deg))

                    Image bayImg = bayObj.GetComponent<Image>();
                    bayImg.type = Image.Type.Simple;

                    if (i < 5)
                    {
                        // 5 Standard bays (white border)
                        bayImg.sprite = standardBoxSprite;
                    }
                    else if (i == 5)
                    {
                        // VIP bay (gold border + "VIP" text)
                        bayImg.sprite = vipBoxSprite;

                        GameObject vipTextObj = new GameObject("VIPText", typeof(RectTransform), typeof(Text));
                        vipTextObj.transform.SetParent(bayObj.transform, false);
                        RectTransform vtRt = vipTextObj.GetComponent<RectTransform>();
                        vtRt.anchorMin = Vector2.zero;
                        vtRt.anchorMax = Vector2.one;
                        vtRt.sizeDelta = Vector2.zero;

                        Text vt = vipTextObj.GetComponent<Text>();
                        vt.font = defaultFont;
                        vt.fontSize = 24;
                        vt.fontStyle = FontStyle.Bold;
                        vt.alignment = TextAnchor.MiddleCenter;
                        vt.color = vipBorder;
                        vt.text = "VIP";
                    }
                    else
                    {
                        // 3 Unlock bays (green border + "+" text)
                        bayImg.sprite = greenBoxSprite;

                        GameObject plusTextObj = new GameObject("PlusText", typeof(RectTransform), typeof(Text));
                        plusTextObj.transform.SetParent(bayObj.transform, false);
                        RectTransform ptRt = plusTextObj.GetComponent<RectTransform>();
                        ptRt.anchorMin = Vector2.zero;
                        ptRt.anchorMax = Vector2.one;
                        ptRt.sizeDelta = Vector2.zero;

                        Text pt = plusTextObj.GetComponent<Text>();
                        pt.font = defaultFont;
                        pt.fontSize = 46;
                        pt.fontStyle = FontStyle.Bold;
                        pt.alignment = TextAnchor.MiddleCenter;
                        pt.color = greenBorder;
                        pt.text = "+";

                        int bayIdx = i;
                        Button bayBtn = bayObj.AddComponent<Button>();
                        bayBtn.transition = Selectable.Transition.None;
                        bayBtn.onClick.AddListener(() => {
                            if (BusFleetUIController.Instance != null) {
                                BusFleetUIController.Instance.UnlockBay(bayIdx, bayObj, standardBoxSprite);
                            }
                        });
                    }
                }
            }

            // 2. Bottom 50% Soft Blue-Grey Rectangular Panel (#d3daed, full width, 2px white top border only)
            if (showBottomPanel)
            {
                GameObject bottomPanelObj = new GameObject("BottomGreyPanel", typeof(RectTransform), typeof(Image));
                bottomPanelObj.transform.SetParent(canvasObj.transform, false);
                RectTransform bpRt = bottomPanelObj.GetComponent<RectTransform>();
                bpRt.anchorMin = new Vector2(0f, 0f);
                bpRt.anchorMax = new Vector2(1f, bottomPanelHeightRatio); // 50% height from bottom
                bpRt.pivot = new Vector2(0.5f, 0f);
                bpRt.offsetMin = Vector2.zero;
                bpRt.offsetMax = Vector2.zero;

                Image bpImg = bottomPanelObj.GetComponent<Image>();
                bpImg.color = bottomPanelColor;

                // 2px solid white border visible ONLY on the top side
                GameObject topBorderObj = new GameObject("TopBorderLine", typeof(RectTransform), typeof(Image));
                topBorderObj.transform.SetParent(bottomPanelObj.transform, false);
                RectTransform tbRt = topBorderObj.GetComponent<RectTransform>();
                tbRt.anchorMin = new Vector2(0f, 1f);
                tbRt.anchorMax = new Vector2(1f, 1f);
                tbRt.pivot = new Vector2(0.5f, 1f);
                tbRt.anchoredPosition = Vector2.zero;
                tbRt.sizeDelta = new Vector2(0f, topBorderThickness); // 2px thickness

                Image tbImg = topBorderObj.GetComponent<Image>();
                tbImg.color = topBorderColor;

                // 8 Top-Down Buses Fleet Row (100% Parity with 3D_Showcase_Preview.html)
                BusFleetUIController busFleet = bottomPanelObj.AddComponent<BusFleetUIController>();
                busFleet.BuildFleet(canvasObj.GetComponent<RectTransform>());

                CreateBoosterDock(bottomPanelObj.transform, pillSprite);
            }

            // 3. Road Network Circuit (positioned directly above the trapezium)
            if (showRoadCircuit)
            {
                Sprite circuitSprite = GetRoadCircuitSprite();
                Debug.Log($"<color=#3498db>[GameUIController] RoadCircuitPanel: showRoadCircuit={showRoadCircuit}, sprite={(circuitSprite != null ? circuitSprite.name : "null")}</color>");
                if (circuitSprite != null)
                {
                    GameObject roadObj = new GameObject("RoadCircuitPanel", typeof(RectTransform), typeof(Image));
                    roadObj.transform.SetParent(canvasObj.transform, false);
                    RectTransform roadRt = roadObj.GetComponent<RectTransform>();
                    roadRt.anchorMin = new Vector2(0.5f, bottomPanelHeightRatio);
                    roadRt.anchorMax = new Vector2(0.5f, bottomPanelHeightRatio);
                    roadRt.pivot = new Vector2(0.5f, 0f); // Pivot at bottom center so it sits directly on top of trapezium
                    roadRt.anchoredPosition = new Vector2(0f, trapeziumHeight);
                    roadRt.sizeDelta = new Vector2(1080f, roadCircuitHeight);

                    Image roadImg = roadObj.GetComponent<Image>();
                    roadImg.sprite = circuitSprite;
                    roadImg.type = Image.Type.Simple;
                    roadImg.color = Color.white;

                    // Attach ChilbiFlowController to animate Chilbi parade along the ring road
                    ChilbiFlowController flowController = roadObj.AddComponent<ChilbiFlowController>();
                    flowController.CreateFormation();
                }
            }

            // 2. Top Header Container (Safe area top padding: 48px down from screen top)
            GameObject headerContainer = new GameObject("HeaderContainer", typeof(RectTransform));
            headerContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform headerRt = headerContainer.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -48f);
            headerRt.sizeDelta = new Vector2(-40f, 160f);

            if (parkOnlyMode)
            {
                // ==================== PARK ONLY MODE UI ====================
                // Elegant Centered Frosted Glass Pill: "Cherry Blossom Park"
                watermarkPill = new GameObject("WatermarkPill", typeof(RectTransform), typeof(Image));
                watermarkPill.transform.SetParent(headerContainer.transform, false);
                RectTransform wmRt = watermarkPill.GetComponent<RectTransform>();
                wmRt.anchorMin = new Vector2(0.5f, 1f);
                wmRt.anchorMax = new Vector2(0.5f, 1f);
                wmRt.pivot = new Vector2(0.5f, 1f);
                wmRt.anchoredPosition = new Vector2(0f, 0f);
                wmRt.sizeDelta = new Vector2(560f, 66f);

                Image wmImg = watermarkPill.GetComponent<Image>();
                wmImg.sprite = pillSprite;
                wmImg.type = Image.Type.Sliced;
                wmImg.color = new Color(1f, 1f, 1f, 0.94f); // 94% frosted white

                GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
                titleObj.transform.SetParent(watermarkPill.transform, false);
                RectTransform titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchorMin = Vector2.zero;
                titleRt.anchorMax = Vector2.one;
                titleRt.sizeDelta = Vector2.zero;
                titleRt.anchoredPosition = Vector2.zero;

                titleText = titleObj.GetComponent<Text>();
                titleText.font = defaultFont;
                titleText.fontSize = 26;
                titleText.fontStyle = FontStyle.Bold;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = new Color32(0x2C, 0x3E, 0x50, 0xFF);
                titleText.text = "Cherry Blossom Park";

                statusPill = null;
                statusText = null;
                replayButton = null;
                replayBtnObj = null;
            }
            else
            {
                // ==================== FULL GAME MODE UI ====================
                // Top-Left Watermark Pill (Level & High Score)
                watermarkPill = new GameObject("WatermarkPill", typeof(RectTransform), typeof(Image));
                watermarkPill.transform.SetParent(headerContainer.transform, false);
                RectTransform wmRt = watermarkPill.GetComponent<RectTransform>();
                wmRt.anchorMin = new Vector2(0f, 1f);
                wmRt.anchorMax = new Vector2(0f, 1f);
                wmRt.pivot = new Vector2(0f, 1f);
                wmRt.anchoredPosition = new Vector2(10f, 0f);
                wmRt.sizeDelta = new Vector2(540f, 62f);

                Image wmImg = watermarkPill.GetComponent<Image>();
                wmImg.sprite = pillSprite;
                wmImg.type = Image.Type.Sliced;
                wmImg.color = new Color(1f, 1f, 1f, 0.94f);

                GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
                titleObj.transform.SetParent(watermarkPill.transform, false);
                RectTransform titleRt = titleObj.GetComponent<RectTransform>();
                titleRt.anchorMin = Vector2.zero;
                titleRt.anchorMax = Vector2.one;
                titleRt.sizeDelta = Vector2.zero;
                titleRt.anchoredPosition = Vector2.zero;

                titleText = titleObj.GetComponent<Text>();
                titleText.font = defaultFont;
                titleText.fontSize = 24;
                titleText.fontStyle = FontStyle.Bold;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = new Color32(0x2C, 0x3E, 0x50, 0xFF);
                int initLvl = PlayerPrefs.GetInt("Roadways_Level", 1);
                int initHi = PlayerPrefs.GetInt("Roadways_HighScore", 0);
                titleText.text = $"⭐ LEVEL {initLvl}  •  👑 HIGH: {initHi}";

                Button wmBtn = watermarkPill.AddComponent<Button>();
                wmBtn.transition = Selectable.Transition.None;
                wmBtn.onClick.AddListener(() => ShowCampaignMapModal(true));

                // Status Pill
                statusPill = new GameObject("StatusPill", typeof(RectTransform), typeof(Image));
                statusPill.transform.SetParent(headerContainer.transform, false);
                RectTransform stRt = statusPill.GetComponent<RectTransform>();
                stRt.anchorMin = new Vector2(0f, 1f);
                stRt.anchorMax = new Vector2(0f, 1f);
                stRt.pivot = new Vector2(0f, 1f);
                stRt.anchoredPosition = new Vector2(10f, -70f);
                stRt.sizeDelta = new Vector2(440f, 52f);

                Image stImg = statusPill.GetComponent<Image>();
                stImg.sprite = pillSprite;
                stImg.type = Image.Type.Sliced;
                stImg.color = new Color(0.96f, 0.86f, 0.88f, 0.92f);

                GameObject statusTextObj = new GameObject("StatusText", typeof(RectTransform), typeof(Text));
                statusTextObj.transform.SetParent(statusPill.transform, false);
                RectTransform stTextRt = statusTextObj.GetComponent<RectTransform>();
                stTextRt.anchorMin = Vector2.zero;
                stTextRt.anchorMax = Vector2.one;
                stTextRt.sizeDelta = Vector2.zero;

                statusText = statusTextObj.GetComponent<Text>();
                statusText.font = defaultFont;
                statusText.fontSize = 21;
                statusText.fontStyle = FontStyle.Bold;
                statusText.alignment = TextAnchor.MiddleCenter;
                statusText.color = new Color32(0x3B, 0x42, 0x52, 0xFF);
                statusText.text = "420 on Track • 10 Queued";

                // Settings & Pause Button (Top-Right)
                GameObject settingsBtnObj = new GameObject("SettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
                settingsBtnObj.transform.SetParent(headerContainer.transform, false);
                RectTransform setRt = settingsBtnObj.GetComponent<RectTransform>();
                setRt.anchorMin = new Vector2(1f, 1f);
                setRt.anchorMax = new Vector2(1f, 1f);
                setRt.pivot = new Vector2(1f, 1f);
                setRt.anchoredPosition = new Vector2(-10f, 0f);
                setRt.sizeDelta = new Vector2(64f, 62f);

                Image setImg = settingsBtnObj.GetComponent<Image>();
                setImg.sprite = pillSprite;
                setImg.type = Image.Type.Sliced;
                setImg.color = new Color32(0x2C, 0x3E, 0x50, 0xF5);

                Button setBtn = settingsBtnObj.GetComponent<Button>();
                setBtn.onClick.AddListener(() => ShowSettingsModal(true));

                GameObject setIconObj = new GameObject("Icon", typeof(RectTransform), typeof(Text));
                setIconObj.transform.SetParent(settingsBtnObj.transform, false);
                RectTransform setIconRt = setIconObj.GetComponent<RectTransform>();
                setIconRt.anchorMin = Vector2.zero;
                setIconRt.anchorMax = Vector2.one;
                setIconRt.sizeDelta = Vector2.zero;

                Text setIconText = setIconObj.GetComponent<Text>();
                setIconText.font = defaultFont;
                setIconText.fontSize = 28;
                setIconText.alignment = TextAnchor.MiddleCenter;
                setIconText.color = Color.white;
                setIconText.raycastTarget = false;
                setIconText.text = "⚙️";

                // Campaign Saga Map Button (Top-Right, next to Settings)
                mapButtonObj = new GameObject("MapButton", typeof(RectTransform), typeof(Image), typeof(Button));
                mapButtonObj.transform.SetParent(headerContainer.transform, false);
                RectTransform mapRt = mapButtonObj.GetComponent<RectTransform>();
                mapRt.anchorMin = new Vector2(1f, 1f);
                mapRt.anchorMax = new Vector2(1f, 1f);
                mapRt.pivot = new Vector2(1f, 1f);
                mapRt.anchoredPosition = new Vector2(-82f, 0f);
                mapRt.sizeDelta = new Vector2(64f, 62f);

                Image mapImg = mapButtonObj.GetComponent<Image>();
                mapImg.sprite = pillSprite;
                mapImg.type = Image.Type.Sliced;
                mapImg.color = new Color32(0x27, 0xAE, 0x60, 0xF5);

                Button mapBtn = mapButtonObj.GetComponent<Button>();
                mapBtn.onClick.AddListener(() => ShowCampaignMapModal(true));

                GameObject mapIconObj = new GameObject("Icon", typeof(RectTransform), typeof(Text));
                mapIconObj.transform.SetParent(mapButtonObj.transform, false);
                RectTransform mapIconRt = mapIconObj.GetComponent<RectTransform>();
                mapIconRt.anchorMin = Vector2.zero;
                mapIconRt.anchorMax = Vector2.one;
                mapIconRt.sizeDelta = Vector2.zero;

                Text mapIconText = mapIconObj.GetComponent<Text>();
                mapIconText.font = defaultFont;
                mapIconText.fontSize = 28;
                mapIconText.alignment = TextAnchor.MiddleCenter;
                mapIconText.color = Color.white;
                mapIconText.raycastTarget = false;
                mapIconText.text = "🗺️";

                replayBtnObj = null;
                replayButton = null;
                replayBtnText = null;
            }
        }

        public void UpdateTitle(string newTitle)
        {
            if (titleText != null)
            {
                titleText.text = newTitle;
            }
        }

        private void EnsureEventSystem()
        {
            var eventSystem = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                GameObject esObj = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<InputSystemUIInputModule>();
#else
                esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
                esObj.transform.SetParent(transform, false);
            }
        }

        public static Font DefaultFont { get { EnsureFontStatic(); return defaultFont; } }

        public static void EnsureFontStatic()
        {
            if (defaultFont == null)
            {
                try { defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
                if (defaultFont == null) { try { defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } }
                if (defaultFont == null) { try { defaultFont = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Roboto", "Helvetica", "sans-serif" }, 24); } catch { } }
            }
        }

        private void EnsureFont()
        {
            EnsureFontStatic();
        }

        public void UpdateProgress(int cleared, int remaining)
        {
            clearedChilbis = cleared;
            totalChilbis = remaining + cleared;
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            if (statusText != null && !parkOnlyMode)
            {
                if (titleText != null)
                {
                    int curLvl = PlayerPrefs.GetInt("Roadways_Level", 1);
                    int hiScore = PlayerPrefs.GetInt("Roadways_HighScore", 0);
                    titleText.text = $"⭐ LEVEL {curLvl}  •  👑 HIGH: {hiScore}";
                }

                if (BusFleetUIController.Instance != null)
                {
                    statusText.text = $"🌸 {BusFleetUIController.Instance.TotalClearedChilbis} Cleared • 🅿️ {BusFleetUIController.Instance.OpenBaysCount} Free • {BusFleetUIController.Instance.TotalScore} pts";
                }
                else if (clearedChilbis > 0)
                {
                    statusText.text = $"{clearedChilbis} Cleared • {totalChilbis - clearedChilbis} Left" + (dockedBusesCount > 0 ? $" | {dockedBusesCount}/9 Bays" : "");
                }
                else
                {
                    statusText.text = $"{circuitChilbis} on Track • {queuedChilbis} Queued" + (dockedBusesCount > 0 ? $" | {dockedBusesCount}/9 Bays" : "");
                }
            }
        }

        [System.NonSerialized] private AudioClip cheerClip;

        public void PlayCheerSound()
        {
            if (!BusFleetUIController.SfxEnabled) return;
            if (cheerClip == null)
            {
                cheerClip = Resources.Load<AudioClip>("Audio/chilbi_cheer");
            }
            if (cheerClip != null)
            {
                AudioSource src = GetComponent<AudioSource>();
                if (src == null) src = gameObject.AddComponent<AudioSource>();
                src.PlayOneShot(cheerClip, 0.9f);
            }
        }

        public void CreateBoosterDock(Transform parent, Sprite pillSprite)
        {
            if (boosterDockObj != null) Destroy(boosterDockObj);

            boosterDockObj = new GameObject("BoosterDock", typeof(RectTransform));
            boosterDockObj.transform.SetParent(parent, false);
            RectTransform dockRt = boosterDockObj.GetComponent<RectTransform>();
            dockRt.anchorMin = new Vector2(0.5f, 1f);
            dockRt.anchorMax = new Vector2(0.5f, 1f);
            dockRt.pivot = new Vector2(0.5f, 1f);
            dockRt.anchoredPosition = new Vector2(0f, -16f);
            dockRt.sizeDelta = new Vector2(620f, 58f);

            // 1. Recall Button (Left side of dock)
            GameObject recBtnObj = new GameObject("Booster_Recall", typeof(RectTransform), typeof(Image), typeof(Button));
            recBtnObj.transform.SetParent(boosterDockObj.transform, false);
            RectTransform recRt = recBtnObj.GetComponent<RectTransform>();
            recRt.anchorMin = new Vector2(0.5f, 0.5f);
            recRt.anchorMax = new Vector2(0.5f, 0.5f);
            recRt.pivot = new Vector2(0.5f, 0.5f);
            recRt.anchoredPosition = new Vector2(-155f, 0f);
            recRt.sizeDelta = new Vector2(290f, 54f);

            Image recImg = recBtnObj.GetComponent<Image>();
            recImg.sprite = pillSprite;
            recImg.type = Image.Type.Sliced;
            recImg.color = new Color32(0x2C, 0x3E, 0x50, 0xF5);

            Button recBtn = recBtnObj.GetComponent<Button>();
            recBtn.onClick.AddListener(() =>
            {
                if (BusFleetUIController.Instance != null) BusFleetUIController.Instance.RecallLastBus();
            });

            GameObject recTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            recTextObj.transform.SetParent(recBtnObj.transform, false);
            RectTransform recTextRt = recTextObj.GetComponent<RectTransform>();
            recTextRt.anchorMin = Vector2.zero;
            recTextRt.anchorMax = Vector2.one;
            recTextRt.sizeDelta = Vector2.zero;
            recallBtnText = recTextObj.GetComponent<Text>();
            recallBtnText.font = DefaultFont;
            recallBtnText.fontSize = 20;
            recallBtnText.fontStyle = FontStyle.Bold;
            recallBtnText.alignment = TextAnchor.MiddleCenter;
            recallBtnText.color = Color.white;
            recallBtnText.raycastTarget = false;

            // 2. Shuffle Button (Right side of dock)
            GameObject shufBtnObj = new GameObject("Booster_Shuffle", typeof(RectTransform), typeof(Image), typeof(Button));
            shufBtnObj.transform.SetParent(boosterDockObj.transform, false);
            RectTransform shufRt = shufBtnObj.GetComponent<RectTransform>();
            shufRt.anchorMin = new Vector2(0.5f, 0.5f);
            shufRt.anchorMax = new Vector2(0.5f, 0.5f);
            shufRt.pivot = new Vector2(0.5f, 0.5f);
            shufRt.anchoredPosition = new Vector2(155f, 0f);
            shufRt.sizeDelta = new Vector2(290f, 54f);

            Image shufImg = shufBtnObj.GetComponent<Image>();
            shufImg.sprite = pillSprite;
            shufImg.type = Image.Type.Sliced;
            shufImg.color = new Color32(0x8E, 0x44, 0xAD, 0xF5);

            Button shufBtn = shufBtnObj.GetComponent<Button>();
            shufBtn.onClick.AddListener(() =>
            {
                if (BusFleetUIController.Instance != null) BusFleetUIController.Instance.ShuffleParade();
            });

            GameObject shufTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            shufTextObj.transform.SetParent(shufBtnObj.transform, false);
            RectTransform shufTextRt = shufTextObj.GetComponent<RectTransform>();
            shufTextRt.anchorMin = Vector2.zero;
            shufTextRt.anchorMax = Vector2.one;
            shufTextRt.sizeDelta = Vector2.zero;
            shuffleBtnText = shufTextObj.GetComponent<Text>();
            shuffleBtnText.font = DefaultFont;
            shuffleBtnText.fontSize = 20;
            shuffleBtnText.fontStyle = FontStyle.Bold;
            shuffleBtnText.alignment = TextAnchor.MiddleCenter;
            shuffleBtnText.color = Color.white;
            shuffleBtnText.raycastTarget = false;

            UpdateBoosterLabels();
        }

        public void UpdateBoosterLabels()
        {
            int recCount = BusFleetUIController.Instance != null ? BusFleetUIController.Instance.RecallCount : 3;
            int shufCount = BusFleetUIController.Instance != null ? BusFleetUIController.Instance.ShuffleCount : 2;

            if (recallBtnText != null)
            {
                recallBtnText.text = $"↺ RECALL ({recCount})";
            }
            if (shuffleBtnText != null)
            {
                shuffleBtnText.text = $"🔀 SHUFFLE ({shufCount})";
            }
        }

        public void ShowSettingsModal(bool show)
        {
            if (!show)
            {
                if (settingsModalObj != null) settingsModalObj.SetActive(false);
                return;
            }

            if (settingsModalObj == null)
            {
                CreateSettingsModal();
            }

            RefreshSettingsToggles();
            settingsModalObj.SetActive(true);
            settingsModalObj.transform.SetAsLastSibling();
            AndroidHaptics.LightClick();
        }

        public void ShowCampaignMapModal(bool show)
        {
            if (!show)
            {
                if (campaignMapModalObj != null) campaignMapModalObj.SetActive(false);
                return;
            }

            if (campaignMapModalObj == null)
            {
                CreateCampaignMapModal();
            }
            else
            {
                RefreshCampaignMap();
            }

            campaignMapModalObj.SetActive(true);
            campaignMapModalObj.transform.SetAsLastSibling();
            AndroidHaptics.LightClick();
        }

        private void CreateCampaignMapModal()
        {
            Transform canvasTrans = mainCanvas != null ? mainCanvas.transform : transform.Find("MainGameCanvas");
            if (canvasTrans == null) return;

            Sprite pillSprite = CreatePillSprite(128, 64, 24);

            // Fullscreen Backdrop
            campaignMapModalObj = new GameObject("Modal_CampaignMap", typeof(RectTransform), typeof(Image));
            campaignMapModalObj.transform.SetParent(canvasTrans, false);
            RectTransform bgRt = campaignMapModalObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            Image bgImg = campaignMapModalObj.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);

            // Modal Card
            GameObject cardObj = new GameObject("MapCard", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(campaignMapModalObj.transform, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(740f, 860f);

            Image cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = CreateRoundedBoxSprite(740, 860, 36, 4, new Color32(0xF1, 0xC4, 0x0F, 0xFF), new Color32(0x1E, 0x27, 0x2E, 0xFC));
            cardImg.type = Image.Type.Simple;

            // Close button (Top-Right of card)
            GameObject closeBtnObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform cRt = closeBtnObj.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(1f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(1f, 1f);
            cRt.anchoredPosition = new Vector2(-16f, -16f);
            cRt.sizeDelta = new Vector2(52f, 52f);

            Image cImg = closeBtnObj.GetComponent<Image>();
            cImg.sprite = pillSprite;
            cImg.type = Image.Type.Sliced;
            cImg.color = new Color32(0xE7, 0x4C, 0x3C, 0xFF);

            Button cBtn = closeBtnObj.GetComponent<Button>();
            cBtn.onClick.AddListener(() => {
                ShowCampaignMapModal(false);
                AndroidHaptics.LightClick();
            });

            GameObject cTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            cTextObj.transform.SetParent(closeBtnObj.transform, false);
            RectTransform ctRt = cTextObj.GetComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero;
            ctRt.anchorMax = Vector2.one;
            ctRt.sizeDelta = Vector2.zero;
            Text ct = cTextObj.GetComponent<Text>();
            ct.font = DefaultFont;
            ct.fontSize = 24;
            ct.fontStyle = FontStyle.Bold;
            ct.alignment = TextAnchor.MiddleCenter;
            ct.color = Color.white;
            ct.text = "✕";

            // Header Title
            GameObject titleObj = new GameObject("ModalTitle", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -28f);
            titleRt.sizeDelta = new Vector2(600f, 50f);

            Text t = titleObj.GetComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = 30;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color32(0xFF, 0xD7, 0x00, 0xFF);
            t.text = "🗺️ SAKURA PARK SAGA";

            // Subtitle
            GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            subObj.transform.SetParent(cardObj.transform, false);
            RectTransform subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -76f);
            subRt.sizeDelta = new Vector2(680f, 32f);

            Text subT = subObj.GetComponent<Text>();
            subT.font = DefaultFont;
            subT.fontSize = 18;
            subT.alignment = TextAnchor.MiddleCenter;
            subT.color = new Color32(0xD2, 0xDA, 0xE2, 0xFF);
            subT.text = "🌸 20 Levels  •  3 Scenic Zones  •  Bay Milestones";

            // Grid Container (for 20 level nodes: 4 cols x 5 rows)
            GameObject gridObj = new GameObject("LevelGrid", typeof(RectTransform));
            gridObj.transform.SetParent(cardObj.transform, false);
            RectTransform gridRt = gridObj.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0.5f, 1f);
            gridRt.anchorMax = new Vector2(0.5f, 1f);
            gridRt.pivot = new Vector2(0.5f, 1f);
            gridRt.anchoredPosition = new Vector2(0f, -118f);
            gridRt.sizeDelta = new Vector2(680f, 520f);

            BuildLevelGridNodes(gridObj.transform, pillSprite);

            // Milestone Banner Strip (Below grid)
            GameObject msObj = new GameObject("MilestoneStrip", typeof(RectTransform), typeof(Image));
            msObj.transform.SetParent(cardObj.transform, false);
            RectTransform msRt = msObj.GetComponent<RectTransform>();
            msRt.anchorMin = new Vector2(0.5f, 1f);
            msRt.anchorMax = new Vector2(0.5f, 1f);
            msRt.pivot = new Vector2(0.5f, 1f);
            msRt.anchoredPosition = new Vector2(0f, -652f);
            msRt.sizeDelta = new Vector2(660f, 44f);

            Image msImg = msObj.GetComponent<Image>();
            msImg.sprite = pillSprite;
            msImg.type = Image.Type.Sliced;
            msImg.color = new Color32(0x2C, 0x3E, 0x50, 0xF0);

            GameObject msTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            msTextObj.transform.SetParent(msObj.transform, false);
            RectTransform mstRt = msTextObj.GetComponent<RectTransform>();
            mstRt.anchorMin = Vector2.zero;
            mstRt.anchorMax = Vector2.one;
            mstRt.sizeDelta = Vector2.zero;
            Text mst = msTextObj.GetComponent<Text>();
            mst.font = DefaultFont;
            mst.fontSize = 17;
            mst.fontStyle = FontStyle.Bold;
            mst.alignment = TextAnchor.MiddleCenter;
            mst.color = new Color32(0xF1, 0xC4, 0x0F, 0xFF);
            mst.text = "🎁 Unlocks: Bay 6 (Lvl 3)  •  Bay 7 (Lvl 5)  •  Bay 8 (Lvl 8)";

            // Bottom Continue / Play Button
            GameObject playBtnObj = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            playBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform playRt = playBtnObj.GetComponent<RectTransform>();
            playRt.anchorMin = new Vector2(0.5f, 0f);
            playRt.anchorMax = new Vector2(0.5f, 0f);
            playRt.pivot = new Vector2(0.5f, 0f);
            playRt.anchoredPosition = new Vector2(0f, 32f);
            playRt.sizeDelta = new Vector2(560f, 62f);

            Image playImg = playBtnObj.GetComponent<Image>();
            playImg.sprite = pillSprite;
            playImg.type = Image.Type.Sliced;
            playImg.color = new Color32(0x27, 0xAE, 0x60, 0xFF);

            Button playBtn = playBtnObj.GetComponent<Button>();
            playBtn.onClick.AddListener(() => {
                ShowCampaignMapModal(false);
                AndroidHaptics.MediumClick();
            });

            GameObject playTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            playTextObj.transform.SetParent(playBtnObj.transform, false);
            RectTransform playtRt = playTextObj.GetComponent<RectTransform>();
            playtRt.anchorMin = Vector2.zero;
            playtRt.anchorMax = Vector2.one;
            playtRt.sizeDelta = Vector2.zero;
            Text playT = playTextObj.GetComponent<Text>();
            playT.font = DefaultFont;
            playT.fontSize = 24;
            playT.fontStyle = FontStyle.Bold;
            playT.alignment = TextAnchor.MiddleCenter;
            playT.color = Color.white;
            int curLvl = PlayerPrefs.GetInt("Roadways_Level", 1);
            playT.text = $"▶ CONTINUE LEVEL {curLvl}";
        }

        private void BuildLevelGridNodes(Transform parent, Sprite pillSprite)
        {
            int currentLvl = PlayerPrefs.GetInt("Roadways_Level", 1);
            float colWidth = 155f;
            float rowHeight = 96f;
            float startX = -232.5f;

            for (int i = 0; i < 20; i++)
            {
                int lvl = i + 1;
                int col = i % 4;
                int row = i / 4;

                float x = startX + col * colWidth;
                float y = -(row * rowHeight + 48f);

                GameObject nodeObj = new GameObject($"Level_{lvl}", typeof(RectTransform), typeof(Image), typeof(Button));
                nodeObj.transform.SetParent(parent, false);
                RectTransform nRt = nodeObj.GetComponent<RectTransform>();
                nRt.anchoredPosition = new Vector2(x, y);
                nRt.sizeDelta = new Vector2(138f, 78f);

                Image nImg = nodeObj.GetComponent<Image>();
                nImg.sprite = pillSprite;
                nImg.type = Image.Type.Sliced;

                bool isCompleted = lvl < currentLvl;
                bool isActive = lvl == currentLvl;
                bool isLocked = lvl > currentLvl;

                if (isActive)
                {
                    nImg.color = new Color32(0xF1, 0xC4, 0x0F, 0xFF); // Gold
                }
                else if (isCompleted)
                {
                    nImg.color = new Color32(0x27, 0xAE, 0x60, 0xFF); // Green
                }
                else
                {
                    nImg.color = new Color32(0x34, 0x49, 0x5E, 0xDD); // Slate grey locked
                }

                GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(nodeObj.transform, false);
                RectTransform tRt = textObj.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.sizeDelta = Vector2.zero;

                Text t = textObj.GetComponent<Text>();
                t.font = DefaultFont;
                t.alignment = TextAnchor.MiddleCenter;
                t.lineSpacing = 1.1f;

                string badgeTag = "";
                if (lvl == 3) badgeTag = "\n🅿️ Bay 6";
                else if (lvl == 5) badgeTag = "\n🅿️ Bay 7";
                else if (lvl == 8) badgeTag = "\n🅿️ Bay 8";

                if (isActive)
                {
                    t.fontSize = 20;
                    t.fontStyle = FontStyle.Bold;
                    t.color = new Color32(0x1E, 0x27, 0x2E, 0xFF);
                    t.text = $"★ L{lvl} ★{badgeTag}";
                }
                else if (isCompleted)
                {
                    t.fontSize = 18;
                    t.fontStyle = FontStyle.Bold;
                    t.color = Color.white;
                    t.text = $"L{lvl} ⭐⭐⭐{badgeTag}";
                }
                else
                {
                    t.fontSize = 17;
                    t.fontStyle = FontStyle.Normal;
                    t.color = new Color32(0xBD, 0xC3, 0xC7, 0xFF);
                    t.text = $"🔒 L{lvl}{badgeTag}";
                }

                int selectedLvl = lvl;
                Button btn = nodeObj.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (selectedLvl <= currentLvl)
                    {
                        PlayerPrefs.SetInt("Roadways_Level", selectedLvl);
                        PlayerPrefs.Save();
                        ShowCampaignMapModal(false);
                        if (BusFleetUIController.Instance != null)
                        {
                            BusFleetUIController.Instance.SetLevel(selectedLvl);
                        }
                        AndroidHaptics.MediumClick();
                    }
                    else
                    {
                        AndroidHaptics.DoubleBuzz();
                        if (BusFleetUIController.Instance != null)
                        {
                            BusFleetUIController.Instance.ShowFloatingTip($"Complete Level {selectedLvl - 1} first!");
                        }
                    }
                });
            }
        }

        private void RefreshCampaignMap()
        {
            if (campaignMapModalObj == null) return;
            Transform grid = campaignMapModalObj.transform.Find("MapCard/LevelGrid");
            if (grid != null)
            {
                foreach (Transform child in grid)
                {
                    Destroy(child.gameObject);
                }
                BuildLevelGridNodes(grid, CreatePillSprite(128, 64, 24));
            }
            Transform playBtnText = campaignMapModalObj.transform.Find("MapCard/PlayButton/Text");
            if (playBtnText != null)
            {
                Text pt = playBtnText.GetComponent<Text>();
                if (pt != null)
                {
                    int curLvl = PlayerPrefs.GetInt("Roadways_Level", 1);
                    pt.text = $"▶ CONTINUE LEVEL {curLvl}";
                }
            }
        }

        private void CreateSettingsModal()
        {
            Transform canvasTrans = mainCanvas != null ? mainCanvas.transform : transform.Find("MainGameCanvas");
            if (canvasTrans == null) return;

            Sprite pillSprite = CreatePillSprite(128, 64, 24);

            // Fullscreen Backdrop
            settingsModalObj = new GameObject("Modal_PauseSettings", typeof(RectTransform), typeof(Image));
            settingsModalObj.transform.SetParent(canvasTrans, false);
            RectTransform bgRt = settingsModalObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            Image bgImg = settingsModalObj.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.72f);

            // Modal Card
            GameObject cardObj = new GameObject("SettingsCard", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(settingsModalObj.transform, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(740f, 820f);

            Image cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = CreateRoundedBoxSprite(740, 820, 36, 4, new Color32(0x4B, 0xCF, 0xFA, 0xFF), new Color32(0x1E, 0x27, 0x2E, 0xFC));
            cardImg.type = Image.Type.Simple;

            // Header Title
            GameObject titleObj = new GameObject("ModalTitle", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -32f);
            titleRt.sizeDelta = new Vector2(680f, 60f);

            Text t = titleObj.GetComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = 32;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color32(0xFF, 0xD7, 0x00, 0xFF);
            t.text = "⚙️ SETTINGS & PAUSE";

            // Subtitle / Level Info
            GameObject subObj = new GameObject("LevelInfo", typeof(RectTransform), typeof(Text));
            subObj.transform.SetParent(cardObj.transform, false);
            RectTransform subRt = subObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -96f);
            subRt.sizeDelta = new Vector2(680f, 40f);

            Text subT = subObj.GetComponent<Text>();
            subT.font = DefaultFont;
            subT.fontSize = 20;
            subT.alignment = TextAnchor.MiddleCenter;
            subT.color = new Color32(0xD2, 0xDA, 0xE2, 0xFF);
            int curLvl = PlayerPrefs.GetInt("Roadways_Level", 1);
            int hiScore = PlayerPrefs.GetInt("Roadways_HighScore", 0);
            subT.text = $"🌸 Cherry Blossom Park  •  Level {curLvl}  •  Best: {hiScore}";

            // Toggle 1: Sound FX
            CreateSettingToggleRow(cardObj.transform, pillSprite, -170f, "🔊 Sound FX", () =>
            {
                BusFleetUIController.SfxEnabled = !BusFleetUIController.SfxEnabled;
                RefreshSettingsToggles();
                AndroidHaptics.LightClick();
            }, out sfxToggleText, out sfxToggleImg);

            // Toggle 2: Zen Music
            CreateSettingToggleRow(cardObj.transform, pillSprite, -260f, "🎵 Zen Music", () =>
            {
                BusFleetUIController.MusicEnabled = !BusFleetUIController.MusicEnabled;
                RefreshSettingsToggles();
                AndroidHaptics.LightClick();
            }, out musicToggleText, out musicToggleImg);

            // Toggle 3: Native Haptics
            CreateSettingToggleRow(cardObj.transform, pillSprite, -350f, "📳 Haptics", () =>
            {
                BusFleetUIController.HapticsEnabled = !BusFleetUIController.HapticsEnabled;
                RefreshSettingsToggles();
                AndroidHaptics.LightClick();
            }, out hapticsToggleText, out hapticsToggleImg);

            // Button: Replay Cheer
            GameObject cheerBtnObj = new GameObject("Btn_ReplayCheer", typeof(RectTransform), typeof(Image), typeof(Button));
            cheerBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform cheerRt = cheerBtnObj.GetComponent<RectTransform>();
            cheerRt.anchorMin = new Vector2(0.5f, 1f);
            cheerRt.anchorMax = new Vector2(0.5f, 1f);
            cheerRt.pivot = new Vector2(0.5f, 1f);
            cheerRt.anchoredPosition = new Vector2(0f, -460f);
            cheerRt.sizeDelta = new Vector2(560f, 62f);

            Image cheerImg = cheerBtnObj.GetComponent<Image>();
            cheerImg.sprite = pillSprite;
            cheerImg.type = Image.Type.Sliced;
            cheerImg.color = new Color32(0x9B, 0x59, 0xB6, 0xFF);

            Button cheerBtn = cheerBtnObj.GetComponent<Button>();
            cheerBtn.onClick.AddListener(() =>
            {
                PlayCheerSound();
                TriggerConfettiBurst(50);
                AndroidHaptics.MediumClick();
            });

            GameObject cheerTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            cheerTextObj.transform.SetParent(cheerBtnObj.transform, false);
            RectTransform ctRt = cheerTextObj.GetComponent<RectTransform>();
            ctRt.anchorMin = Vector2.zero;
            ctRt.anchorMax = Vector2.one;
            ctRt.sizeDelta = Vector2.zero;
            Text ct = cheerTextObj.GetComponent<Text>();
            ct.font = DefaultFont;
            ct.fontSize = 22;
            ct.fontStyle = FontStyle.Bold;
            ct.alignment = TextAnchor.MiddleCenter;
            ct.color = Color.white;
            ct.text = "🎉 Replay Chilbi Cheer";

            // Button: Restart Level
            GameObject rstBtnObj = new GameObject("Btn_RestartLevel", typeof(RectTransform), typeof(Image), typeof(Button));
            rstBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform rstRt = rstBtnObj.GetComponent<RectTransform>();
            rstRt.anchorMin = new Vector2(0.5f, 1f);
            rstRt.anchorMax = new Vector2(0.5f, 1f);
            rstRt.pivot = new Vector2(0.5f, 1f);
            rstRt.anchoredPosition = new Vector2(0f, -540f);
            rstRt.sizeDelta = new Vector2(560f, 62f);

            Image rstImg = rstBtnObj.GetComponent<Image>();
            rstImg.sprite = pillSprite;
            rstImg.type = Image.Type.Sliced;
            rstImg.color = new Color32(0xE7, 0x4C, 0x3C, 0xFF);

            Button rstBtn = rstBtnObj.GetComponent<Button>();
            rstBtn.onClick.AddListener(() =>
            {
                ShowSettingsModal(false);
                if (BusFleetUIController.Instance != null) BusFleetUIController.Instance.ResetGame();
            });

            GameObject rstTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            rstTextObj.transform.SetParent(rstBtnObj.transform, false);
            RectTransform rtRt = rstTextObj.GetComponent<RectTransform>();
            rtRt.anchorMin = Vector2.zero;
            rtRt.anchorMax = Vector2.one;
            rtRt.sizeDelta = Vector2.zero;
            Text rt = rstTextObj.GetComponent<Text>();
            rt.font = DefaultFont;
            rt.fontSize = 22;
            rt.fontStyle = FontStyle.Bold;
            rt.alignment = TextAnchor.MiddleCenter;
            rt.color = Color.white;
            rt.text = "↺ Restart Level";

            // Button: Resume Game
            GameObject resBtnObj = new GameObject("Btn_Resume", typeof(RectTransform), typeof(Image), typeof(Button));
            resBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform resRt = resBtnObj.GetComponent<RectTransform>();
            resRt.anchorMin = new Vector2(0.5f, 1f);
            resRt.anchorMax = new Vector2(0.5f, 1f);
            resRt.pivot = new Vector2(0.5f, 1f);
            resRt.anchoredPosition = new Vector2(0f, -640f);
            resRt.sizeDelta = new Vector2(560f, 76f);

            Image resImg = resBtnObj.GetComponent<Image>();
            resImg.sprite = pillSprite;
            resImg.type = Image.Type.Sliced;
            resImg.color = new Color32(0x2E, 0xCC, 0x71, 0xFF);

            Button resBtn = resBtnObj.GetComponent<Button>();
            resBtn.onClick.AddListener(() =>
            {
                ShowSettingsModal(false);
                AndroidHaptics.LightClick();
            });

            GameObject resTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            resTextObj.transform.SetParent(resBtnObj.transform, false);
            RectTransform restRt = resTextObj.GetComponent<RectTransform>();
            restRt.anchorMin = Vector2.zero;
            restRt.anchorMax = Vector2.one;
            restRt.sizeDelta = Vector2.zero;
            Text rest = resTextObj.GetComponent<Text>();
            rest.font = DefaultFont;
            rest.fontSize = 26;
            rest.fontStyle = FontStyle.Bold;
            rest.alignment = TextAnchor.MiddleCenter;
            rest.color = Color.white;
            rest.text = "▶ RESUME GAME";
        }

        private void CreateSettingToggleRow(Transform parent, Sprite pillSprite, float yPos, string labelText, UnityEngine.Events.UnityAction onToggle, out Text stateText, out Image buttonImg)
        {
            GameObject rowObj = new GameObject($"Row_{labelText}", typeof(RectTransform));
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, yPos);
            rowRt.sizeDelta = new Vector2(580f, 64f);

            // Label
            GameObject lblObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblObj.transform.SetParent(rowObj.transform, false);
            RectTransform lRt = lblObj.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 0.5f);
            lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = new Vector2(10f, 0f);
            lRt.sizeDelta = new Vector2(340f, 50f);

            Text lbl = lblObj.GetComponent<Text>();
            lbl.font = DefaultFont;
            lbl.fontSize = 24;
            lbl.fontStyle = FontStyle.Bold;
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.color = Color.white;
            lbl.text = labelText;

            // Toggle Button
            GameObject btnObj = new GameObject("ToggleBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(rowObj.transform, false);
            RectTransform bRt = btnObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(1f, 0.5f);
            bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-10f, 0f);
            bRt.sizeDelta = new Vector2(160f, 52f);

            buttonImg = btnObj.GetComponent<Image>();
            buttonImg.sprite = pillSprite;
            buttonImg.type = Image.Type.Sliced;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(onToggle);

            GameObject tObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tObj.transform.SetParent(btnObj.transform, false);
            RectTransform tRt = tObj.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = Vector2.zero;

            stateText = tObj.GetComponent<Text>();
            stateText.font = DefaultFont;
            stateText.fontSize = 20;
            stateText.fontStyle = FontStyle.Bold;
            stateText.alignment = TextAnchor.MiddleCenter;
            stateText.color = Color.white;
        }

        private void RefreshSettingsToggles()
        {
            bool sfx = BusFleetUIController.SfxEnabled;
            bool music = BusFleetUIController.MusicEnabled;
            bool haptics = BusFleetUIController.HapticsEnabled;

            if (sfxToggleText != null) sfxToggleText.text = sfx ? "ON" : "OFF";
            if (sfxToggleImg != null) sfxToggleImg.color = sfx ? new Color32(0x2E, 0xCC, 0x71, 0xFF) : new Color32(0x7F, 0x8C, 0x8D, 0xFF);

            if (musicToggleText != null) musicToggleText.text = music ? "ON" : "OFF";
            if (musicToggleImg != null) musicToggleImg.color = music ? new Color32(0x2E, 0xCC, 0x71, 0xFF) : new Color32(0x7F, 0x8C, 0x8D, 0xFF);

            if (hapticsToggleText != null) hapticsToggleText.text = haptics ? "ON" : "OFF";
            if (hapticsToggleImg != null) hapticsToggleImg.color = haptics ? new Color32(0x2E, 0xCC, 0x71, 0xFF) : new Color32(0x7F, 0x8C, 0x8D, 0xFF);
        }

        public void TriggerConfettiBurst(int count = 60)
        {
            StartCoroutine(ConfettiRoutine(count));
        }

        private System.Collections.IEnumerator ConfettiRoutine(int count)
        {
            Transform cTrans = mainCanvas != null ? mainCanvas.transform : transform.Find("MainGameCanvas");
            if (cTrans == null) yield break;

            GameObject container = new GameObject("ConfettiContainer", typeof(RectTransform));
            container.transform.SetParent(cTrans, false);
            RectTransform crt = container.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero;

            Color[] confettiColors = new Color[] {
                new Color32(0xE8, 0x43, 0x93, 0xFF), // Pink
                new Color32(0xF1, 0xC4, 0x0F, 0xFF), // Gold
                new Color32(0x2E, 0xCC, 0x71, 0xFF), // Green
                new Color32(0x34, 0x98, 0xDB, 0xFF), // Blue
                new Color32(0x9B, 0x59, 0xB6, 0xFF), // Purple
                new Color32(0xE6, 0x7E, 0x22, 0xFF), // Orange
                new Color32(0x00, 0xCE, 0xC9, 0xFF)  // Cyan
            };

            List<RectTransform> pieces = new List<RectTransform>();
            List<Vector2> velocities = new List<Vector2>();
            List<float> rotSpeeds = new List<float>();

            for (int i = 0; i < count; i++)
            {
                GameObject chip = new GameObject($"Confetti_{i}", typeof(RectTransform), typeof(Image));
                chip.transform.SetParent(container.transform, false);
                RectTransform rt = chip.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(UnityEngine.Random.Range(10f, 18f), UnityEngine.Random.Range(14f, 26f));
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-340f, 340f), UnityEngine.Random.Range(10f, 60f));

                Image img = chip.GetComponent<Image>();
                img.color = confettiColors[i % confettiColors.Length];
                img.raycastTarget = false;

                pieces.Add(rt);
                velocities.Add(new Vector2(UnityEngine.Random.Range(-70f, 70f), -UnityEngine.Random.Range(220f, 480f)));
                rotSpeeds.Add(UnityEngine.Random.Range(-240f, 240f));
            }

            float elapsed = 0f;
            float duration = 3.5f;
            while (elapsed < duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                for (int i = 0; i < pieces.Count; i++)
                {
                    if (pieces[i] == null) continue;
                    Vector2 pos = pieces[i].anchoredPosition;
                    Vector2 v = velocities[i];
                    v.x += Mathf.Sin(elapsed * 4f + i) * 60f * dt;
                    pos += v * dt;
                    pieces[i].anchoredPosition = pos;
                    pieces[i].Rotate(0f, 0f, rotSpeeds[i] * dt);

                    if (elapsed > duration - 1f)
                    {
                        Image img = pieces[i].GetComponent<Image>();
                        if (img != null)
                        {
                            Color c = img.color;
                            c.a = Mathf.Clamp01(duration - elapsed);
                            img.color = c;
                        }
                    }
                }
                yield return null;
            }

            Destroy(container);
        }

        private void OnReplayClicked()
        {
            Debug.Log("<color=#2ecc71>[GameUI] Replay clicked! Resetting game & triggering cheer...</color>");
            if (BusFleetUIController.Instance != null) BusFleetUIController.Instance.ResetGame();
            if (ChilbiFlowController.Instance != null) ChilbiFlowController.Instance.ResetParade();
            PlayCheerSound();
            TriggerConfettiBurst(60);
            AndroidHaptics.DoubleBuzz();
        }

        public static Sprite CreatePillSprite(int width, int height, int radius)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "Gen_PillSpriteTex";
            Color[] colors = new Color[width * height];

            float r = radius;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cx = x < r ? r : (x > width - 1 - r ? width - 1 - r : x);
                    float cy = y < r ? r : (y > height - 1 - r ? height - 1 - r : y);
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                    if (dist <= r - 1.5f)
                    {
                        colors[y * width + x] = Color.white;
                    }
                    else if (dist <= r)
                    {
                        float alpha = Mathf.Clamp01(r - dist);
                        colors[y * width + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        colors[y * width + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            Vector4 border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite CreateTrapeziumSprite(int width, int height, float topRatio, float bottomRatio, float radius)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "Gen_TrapeziumSpriteTex";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] colors = new Color[width * height];

            float W_t = width * topRatio;
            float W_b = width * bottomRatio;
            float X_bl = (width - W_b) * 0.5f;
            float X_tl = (width - W_t) * 0.5f;
            float X_tr = (width + W_t) * 0.5f;
            float X_br = (width + W_b) * 0.5f;

            float dx = X_bl - X_tl;
            float dy = -height;
            float L = Mathf.Sqrt(dx * dx + dy * dy);
            float ux = dx / L;
            float uy = dy / L;
            float nx = -uy;
            float ny = ux;

            float R = radius;
            float C_yl = height - R;
            float C_xl = X_tl + R * (1.0f + ny) / nx;
            float C_yr = C_yl;
            float C_xr = width - C_xl;

            float T_yl = height - R * (1.0f + ny);

            for (int y = 0; y < height; y++)
            {
                float d_t = (float)(height - y);
                for (int x = 0; x < width; x++)
                {
                    float d_sl = (x - X_tl) * nx + (y - height) * ny;
                    float d_sr = (X_tr - x) * nx + (y - height) * ny;

                    float D;
                    if (x <= C_xl && y >= T_yl)
                    {
                        float p_xl = x - C_xl;
                        float p_yl = y - C_yl;
                        D = R - Mathf.Sqrt(p_xl * p_xl + p_yl * p_yl);
                    }
                    else if (x >= C_xr && y >= T_yl)
                    {
                        float p_xr = x - C_xr;
                        float p_yr = y - C_yr;
                        D = R - Mathf.Sqrt(p_xr * p_xr + p_yr * p_yr);
                    }
                    else
                    {
                        D = Mathf.Min(d_t, Mathf.Min(d_sl, d_sr));
                    }

                    float alpha = Mathf.Clamp01(D + 0.5f);
                    colors[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
        }

        public static Sprite CreateRoundedBoxSprite(int width, int height, int cornerRadius, int borderWidth, Color32 borderColor, Color32 fillColor)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = $"Gen_Box_{borderColor.r}_{borderColor.g}_{borderColor.b}";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * height];
            Color32 clear = new Color32(0, 0, 0, 0);

            float rOut = cornerRadius;
            float rIn = Mathf.Max(0f, cornerRadius - borderWidth);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Compute distance from outer rounded rect edges
                    float dxOut = Mathf.Max(0f, Mathf.Abs(x - (width - 1) * 0.5f) - ((width - 1) * 0.5f - rOut));
                    float dyOut = Mathf.Max(0f, Mathf.Abs(y - (height - 1) * 0.5f) - ((height - 1) * 0.5f - rOut));
                    float distOut = Mathf.Sqrt(dxOut * dxOut + dyOut * dyOut);
                    float alphaOut = Mathf.Clamp01(rOut - distOut + 0.5f);

                    if (alphaOut <= 0.001f)
                    {
                        pixels[y * width + x] = clear;
                        continue;
                    }

                    // Compute distance from inner fill rounded rect edges
                    float innerW = (width - 1) * 0.5f - borderWidth;
                    float innerH = (height - 1) * 0.5f - borderWidth;
                    float dxIn = Mathf.Max(0f, Mathf.Abs(x - (width - 1) * 0.5f) - (innerW - rIn));
                    float dyIn = Mathf.Max(0f, Mathf.Abs(y - (height - 1) * 0.5f) - (innerH - rIn));
                    float distIn = Mathf.Sqrt(dxIn * dxIn + dyIn * dyIn);
                    float alphaIn = Mathf.Clamp01(rIn - distIn + 0.5f);

                    // Blend border and fill
                    Color c = Color.Lerp((Color)borderColor, (Color)fillColor, alphaIn);
                    c.a *= alphaOut;
                    pixels[y * width + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        public Sprite GetRoadCircuitSprite()
        {
            if (roadCircuitSprite != null) return roadCircuitSprite;

            // 1. Try loading from Resources
            roadCircuitSprite = Resources.Load<Sprite>("CircuitRoad");
            if (roadCircuitSprite != null) return roadCircuitSprite;

            // 2. Try loading PNG bytes directly
            string[] possiblePaths = new string[]
            {
                System.IO.Path.Combine(Application.dataPath, "_Project/Textures/UI/CircuitRoad.png"),
                System.IO.Path.Combine(Application.dataPath, "_Project/Resources/CircuitRoad.png")
            };

            foreach (var path in possiblePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.name = "Loaded_CircuitRoadTex";
                        tex.filterMode = FilterMode.Bilinear;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        roadCircuitSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
                        return roadCircuitSprite;
                    }
                }
            }

            return null;
        }
    }
}
