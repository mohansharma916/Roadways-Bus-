using System;
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
        [Header("Display Mode")]
        [Tooltip("When enabled, hides Ring Road, Parking Lot, Buses, and Chilbis to show ONLY Cherry Blossom Park.")]
        [SerializeField] private bool parkOnlyMode = true;

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
            bottomPanelColor = new Color32(0xD3, 0xDA, 0xED, 0xFF);
            trapeziumColor = new Color32(0x52, 0x57, 0x69, 0xFF);
            ApplyDisplayMode();
            BuildUI();
        }

        private void OnEnable()
        {
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
                // Top-Left Watermark Pill
                watermarkPill = new GameObject("WatermarkPill", typeof(RectTransform), typeof(Image));
                watermarkPill.transform.SetParent(headerContainer.transform, false);
                RectTransform wmRt = watermarkPill.GetComponent<RectTransform>();
                wmRt.anchorMin = new Vector2(0f, 1f);
                wmRt.anchorMax = new Vector2(0f, 1f);
                wmRt.pivot = new Vector2(0f, 1f);
                wmRt.anchoredPosition = new Vector2(10f, 0f);
                wmRt.sizeDelta = new Vector2(520f, 62f);

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
                titleText.text = "430 Chilbis Parade • Sakura Park";

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

                // Replay Button
                replayBtnObj = new GameObject("ReplayButton", typeof(RectTransform), typeof(Image), typeof(Button));
                replayBtnObj.transform.SetParent(headerContainer.transform, false);
                RectTransform btnRt = replayBtnObj.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 1f);
                btnRt.anchorMax = new Vector2(1f, 1f);
                btnRt.pivot = new Vector2(1f, 1f);
                btnRt.anchoredPosition = new Vector2(-10f, 0f);
                btnRt.sizeDelta = new Vector2(230f, 62f);

                Image btnImg = replayBtnObj.GetComponent<Image>();
                btnImg.sprite = pillSprite;
                btnImg.type = Image.Type.Sliced;
                btnImg.color = new Color32(0x27, 0xAE, 0x60, 0xFA);

                replayButton = replayBtnObj.GetComponent<Button>();
                replayButton.targetGraphic = btnImg;
                ColorBlock cb = replayButton.colors;
                cb.highlightedColor = new Color32(0x2E, 0xCC, 0x71, 0xFF);
                cb.pressedColor = new Color32(0x1E, 0x84, 0x49, 0xFF);
                replayButton.colors = cb;
                replayButton.onClick.RemoveAllListeners();
                replayButton.onClick.AddListener(OnReplayClicked);

                GameObject btnTextObj = new GameObject("BtnText", typeof(RectTransform), typeof(Text));
                btnTextObj.transform.SetParent(replayBtnObj.transform, false);
                RectTransform btnTextRt = btnTextObj.GetComponent<RectTransform>();
                btnTextRt.anchorMin = Vector2.zero;
                btnTextRt.anchorMax = Vector2.one;
                btnTextRt.sizeDelta = Vector2.zero;

                replayBtnText = btnTextObj.GetComponent<Text>();
                replayBtnText.font = defaultFont;
                replayBtnText.fontSize = 22;
                replayBtnText.fontStyle = FontStyle.Bold;
                replayBtnText.alignment = TextAnchor.MiddleCenter;
                replayBtnText.color = Color.white;
                replayBtnText.text = "Replay Cheer";
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

        private void EnsureFont()
        {
            if (defaultFont == null)
            {
                try
                {
                    defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch { }

                if (defaultFont == null)
                {
                    try
                    {
                        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                    catch { }
                }

                if (defaultFont == null)
                {
                    try
                    {
                        defaultFont = Font.CreateDynamicFontFromOSFont(new string[] { "Arial", "Roboto", "Helvetica", "sans-serif" }, 24);
                    }
                    catch { }
                }
            }
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
                if (clearedChilbis > 0)
                {
                    statusText.text = $"{clearedChilbis} Cleared • {totalChilbis - clearedChilbis} Left" + (dockedBusesCount > 0 ? $" | {dockedBusesCount}/9 Bays" : "");
                }
                else
                {
                    statusText.text = $"{circuitChilbis} on Track • {queuedChilbis} Queued" + (dockedBusesCount > 0 ? $" | {dockedBusesCount}/9 Bays" : "");
                }
            }
        }

        private void OnReplayClicked()
        {
            Debug.Log("<color=#2ecc71>[GameUI] Replay Cheer clicked! Triggering parade cheer & sprint...</color>");
            if (formationManager == null) formationManager = FindAnyObjectByType<ChilbiFormationManager>();
            if (formationManager != null)
            {
                formationManager.InitializeFormation();
            }
        }

        private static Sprite CreatePillSprite(int width, int height, int radius)
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
    }
}
