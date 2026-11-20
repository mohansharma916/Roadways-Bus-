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
    /// - Top-left glassmorphic Watermark Pill: "430 Chilbis Parade • Sakura Park"
    /// - Dynamic Formation Status Pill: "420 on Track • 10 Queued" / "X Cleared • Y Left"
    /// - Top-right interactive Replay / Cheer Button: "Replay Cheer"
    /// - Clean typography without emoji dependencies for universal crisp font rendering
    /// </summary>
    [ExecuteAlways]
    public class GameUIController : MonoBehaviour
    {
        [Header("UI State (Dynamic)")]
        [SerializeField] private int clearedChilbis = 0;
        [SerializeField] private int totalChilbis = GameConstants.TotalChilbis; // 430
        [SerializeField] private int circuitChilbis = GameConstants.CircuitMaxChilbis; // 420
        [SerializeField] private int queuedChilbis = 10;
        [SerializeField] private int dockedBusesCount = 0;

        [Header("UI References")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private CanvasScaler canvasScaler;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text replayBtnText;
        [SerializeField] private Button replayButton;

        private static Font defaultFont;
        private ChilbiFormationManager formationManager;
        private BusFleetController fleetController;
        private float lastUpdateTime = 0f;

        public int ClearedChilbis => clearedChilbis;
        public int TotalChilbis => totalChilbis;

        private void Awake()
        {
            BuildUI();
        }

        private void OnEnable()
        {
            if (mainCanvas == null || transform.Find("MainGameCanvas") == null)
            {
                BuildUI();
            }
        }

        private void Start()
        {
            formationManager = FindAnyObjectByType<ChilbiFormationManager>();
            fleetController = FindAnyObjectByType<BusFleetController>();
            UpdateStatusDisplay();
        }

        private void Update()
        {
            if (Time.unscaledTime - lastUpdateTime > 0.25f)
            {
                lastUpdateTime = Time.unscaledTime;
                RefreshState();
            }
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
            canvasScaler.matchWidthOrHeight = 0f; // Match width for rock-solid portrait fit

            GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = canvasObj.AddComponent<GraphicRaycaster>();

            // Generate rounded pill sprite with 24px smooth corner radii
            Sprite pillSprite = CreatePillSprite(128, 64, 24);

            // 1. Top Header Container (Safe area top padding: 48px down from screen top)
            GameObject headerContainer = new GameObject("HeaderContainer", typeof(RectTransform));
            headerContainer.transform.SetParent(canvasObj.transform, false);
            RectTransform headerRt = headerContainer.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -48f);
            headerRt.sizeDelta = new Vector2(-40f, 160f);

            // 2. Top-Left Watermark Pill
            GameObject watermarkPill = new GameObject("WatermarkPill", typeof(RectTransform), typeof(Image));
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
            titleText.fontSize = 24;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color32(0x2C, 0x3E, 0x50, 0xFF);
            titleText.text = "430 Chilbis Parade • Sakura Park";

            // 3. Status Pill (Directly below Watermark)
            GameObject statusPill = new GameObject("StatusPill", typeof(RectTransform), typeof(Image));
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
            stImg.color = new Color(0.96f, 0.86f, 0.88f, 0.92f); // Soft Sakura Blush

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

            // 4. Top-Right Replay / Cheer Button
            GameObject replayBtnObj = new GameObject("ReplayButton", typeof(RectTransform), typeof(Image), typeof(Button));
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
            btnImg.color = new Color32(0x27, 0xAE, 0x60, 0xFA); // Polished Green #27ae60

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
            if (statusText != null)
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
    }
}
