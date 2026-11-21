using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoadwaysGame
{
    /// <summary>
    /// Master UI Controller for the 8 Top-Down Buses in the bottom panel:
    /// - 100% Parity with 3D_Showcase_Preview.html:
    ///   * 8 Matte top-down vehicles (Red, Yellow, Blue, Green, Purple, Orange, Pink, Cyan)
    ///   * Multi-Bay Parking: Dispatches to any of the 9 Trapezium bays (Box_0 to Box_8)
    ///   * Real-time bay occupancy tracking and simultaneous multi-bus docking
    ///   * Cubic Hermite velocity-tangent steering with dynamic angle interpolation to 30 deg
    ///   * Head-first bay entry, suspension settle, and roof arrow fade-out on dock
    ///   * Tap parked bus to return to depot, or board matching Chilbis and depart screen
    /// </summary>
    [ExecuteAlways]
    public class BusFleetUIController : MonoBehaviour
    {
        public static BusFleetUIController Instance { get; private set; }

        public struct BusDefinition
        {
            public string id;
            public string name;
            public Color32 bodyColor;
            public float hornFreq;
            public bool hasHoodLine;
            public BusDefinition(string id, string name, Color32 c, float freq, bool hood = false)
            {
                this.id = id;
                this.name = name;
                this.bodyColor = c;
                this.hornFreq = freq;
                this.hasHoodLine = hood;
            }
        }

        public static readonly BusDefinition[] Fleet = new BusDefinition[]
        {
            new BusDefinition("bus-red", "Crimson Express", new Color32(0xC0, 0x39, 0x2B, 0xFF), 440f),
            new BusDefinition("bus-yellow", "Amber School Buggy", new Color32(0xF3, 0x9C, 0x12, 0xFF), 392f, true),
            new BusDefinition("bus-blue", "Cobalt Commuter", new Color32(0x25, 0x48, 0xAD, 0xFF), 494f),
            new BusDefinition("bus-green", "Emerald Eco", new Color32(0x20, 0xBF, 0x6B, 0xFF), 523f),
            new BusDefinition("bus-purple", "Royal Violet", new Color32(0x88, 0x54, 0xD0, 0xFF), 587f),
            new BusDefinition("bus-orange", "Tangerine Transit", new Color32(0xE6, 0x7E, 0x22, 0xFF), 622f),
            new BusDefinition("bus-pink", "Bubblegum Shuttle", new Color32(0xE8, 0x43, 0x93, 0xFF), 659f),
            new BusDefinition("bus-cyan", "Cyan Deck-Master", new Color32(0x0F, 0xBC, 0xF9, 0xFF), 740f)
        };

        [Header("Bus Dimensions")]
        [SerializeField] private Vector2 busSize = new Vector2(64f, 128f);
        [SerializeField] private float busSpacing = 114f;
        [SerializeField] private float topPadding = 90f;

        private static Sprite[] busSprites;
        private static Sprite arrowSprite;
        private static AudioClip[] hornClips;
        private static AudioClip warningClip;
        private static AudioClip popClip;
        private static AudioClip airBrakeClip;
        private static AudioClip engineRumbleClip;
        private static AudioClip ambientClip;

        private AudioSource audioSource;
        private AudioSource ambientAudioSource;
        private RectTransform canvasRt;

        // 9 Trapezium Bay Slots
        private RectTransform[] bayRts = new RectTransform[9];
        private int[] bayOccupants = new int[9]; // -1 if empty, bus index if occupied
        private GameObject[] parkedBusObjects = new GameObject[9];
        private bool[] bayUnlocked = new bool[9] { true, true, true, true, true, true, false, false, false }; // 5 standard + 1 VIP unlocked

        private bool isInFlight = false;
        private bool isBoardingActive = false;
        private List<RectTransform> depotBusRts = new List<RectTransform>();

        public int[] BayOccupants => bayOccupants;
        public GameObject[] ParkedBusObjects => parkedBusObjects;
        public bool[] BayUnlocked => bayUnlocked;
        public bool IsBoardingActive => isBoardingActive;

        public event Action<int, int> OnBusDockedInBay; // (bayIndex, busIndex)
        public event Action<int, int> OnBusLeftBay;     // (bayIndex, busIndex)

        [System.NonSerialized] private int recallCount = 3;
        [System.NonSerialized] private int shuffleCount = 2;
        [System.NonSerialized] private GameObject ftuePointerObj;
        [System.NonSerialized] private RectTransform ftuePointerRt;
        [System.NonSerialized] private Text ftuePointerText;
        [System.NonSerialized] private float idleTimer = 0f;

        public int RecallCount => recallCount;
        public int ShuffleCount => shuffleCount;

        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt("Roadways_SfxEnabled", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("Roadways_SfxEnabled", value ? 1 : 0);
                PlayerPrefs.Save();
                if (Instance != null) Instance.ApplyAudioMutes();
            }
        }

        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt("Roadways_MusicEnabled", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("Roadways_MusicEnabled", value ? 1 : 0);
                PlayerPrefs.Save();
                if (Instance != null) Instance.ApplyAudioMutes();
            }
        }

        public static bool HapticsEnabled
        {
            get => PlayerPrefs.GetInt("Roadways_HapticsEnabled", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("Roadways_HapticsEnabled", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public void ApplyAudioMutes()
        {
            if (audioSource != null) audioSource.mute = !SfxEnabled;
            if (ambientAudioSource != null)
            {
                ambientAudioSource.mute = !MusicEnabled;
                if (MusicEnabled && !ambientAudioSource.isPlaying && ambientAudioSource.clip != null)
                {
                    ambientAudioSource.Play();
                }
            }
            if (GameUIController.Instance != null && GameUIController.Instance.AudioSource != null)
            {
                GameUIController.Instance.AudioSource.mute = !SfxEnabled;
            }
        }

        public Transform GetCanvasTransform()
        {
            if (canvasRt != null) return canvasRt;
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null) { canvasRt = c.GetComponent<RectTransform>(); return canvasRt; }
            if (transform.root != null && transform.root.name == "MainGameCanvas") { canvasRt = transform.root.GetComponent<RectTransform>(); return canvasRt; }
            Transform found = transform.root.Find("MainGameCanvas");
            if (found != null) { canvasRt = found.GetComponent<RectTransform>(); return canvasRt; }
            GameObject go = GameObject.Find("MainGameCanvas");
            if (go != null) { canvasRt = go.GetComponent<RectTransform>(); return canvasRt; }
            return null;
        }

        private void Awake()
        {
            Instance = this;
            topPadding = 110f;
            if (PlayerPrefs.GetInt("Roadways_FTUE_Completed", 0) == 0)
            {
                currentLevel = 1;
                PlayerPrefs.SetInt("Roadways_Level", 1);
                PlayerPrefs.Save();
            }
            else
            {
                currentLevel = PlayerPrefs.GetInt("Roadways_Level", 1);
            }
            highScore = PlayerPrefs.GetInt("Roadways_HighScore", 0);
            recallCount = PlayerPrefs.GetInt("Roadways_RecallCount", 3);
            shuffleCount = PlayerPrefs.GetInt("Roadways_ShuffleCount", 2);
            GetCanvasTransform();
            EnsureAudioSource();
            EnsureAssets();
            ApplyAudioMutes();
            for (int i = 0; i < 9; i++) bayOccupants[i] = -1;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void Start()
        {
            RefreshBayUnlocks();
            UpdateStatusHUD();
        }

        private void Update()
        {
            if (isBoardingActive || isInFlight)
            {
                HideFTUEPointer();
                return;
            }
            if (ChilbiFlowController.Instance == null) return;

            CheckBoarding();
            UpdateFTUEGuide();
        }

        private void EnsureFTUEPointer(Transform parent)
        {
            if (ftuePointerObj != null) return;

            ftuePointerObj = new GameObject("FTUE_Pointer", typeof(RectTransform));
            ftuePointerObj.transform.SetParent(parent, false);
            ftuePointerRt = ftuePointerObj.GetComponent<RectTransform>();
            ftuePointerRt.anchorMin = new Vector2(0.5f, 0.5f);
            ftuePointerRt.anchorMax = new Vector2(0.5f, 0.5f);
            ftuePointerRt.pivot = new Vector2(0.5f, 0f);
            ftuePointerRt.sizeDelta = new Vector2(140f, 48f);

            // 1. Text Pill: "TAP TO DOCK"
            GameObject pillObj = new GameObject("Pill", typeof(RectTransform), typeof(Image));
            pillObj.transform.SetParent(ftuePointerObj.transform, false);
            RectTransform pRt = pillObj.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 1f);
            pRt.anchorMax = new Vector2(0.5f, 1f);
            pRt.pivot = new Vector2(0.5f, 1f);
            pRt.anchoredPosition = new Vector2(0f, 0f);
            pRt.sizeDelta = new Vector2(140f, 26f);

            Image pImg = pillObj.GetComponent<Image>();
            pImg.sprite = GameUIController.CreatePillSprite(32, 16, 8);
            pImg.type = Image.Type.Sliced;
            pImg.color = new Color32(0xFF, 0xD7, 0x00, 0xFF); // Solid Gold

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(pillObj.transform, false);
            RectTransform tRt = textObj.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = Vector2.zero;

            ftuePointerText = textObj.GetComponent<Text>();
            ftuePointerText.font = GameUIController.DefaultFont;
            ftuePointerText.fontSize = 13;
            ftuePointerText.fontStyle = FontStyle.Bold;
            ftuePointerText.alignment = TextAnchor.MiddleCenter;
            ftuePointerText.color = new Color32(0x2C, 0x3E, 0x50, 0xFF);
            ftuePointerText.raycastTarget = false;
            ftuePointerText.text = "TAP TO DOCK";

            // 2. Pointing Down Arrow (procedural arrowSprite rotated 180 degrees)
            GameObject arrowObj = new GameObject("PointerArrow", typeof(RectTransform), typeof(Image));
            arrowObj.transform.SetParent(ftuePointerObj.transform, false);
            RectTransform aRt = arrowObj.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0.5f, 0f);
            aRt.anchorMax = new Vector2(0.5f, 0f);
            aRt.pivot = new Vector2(0.5f, 0f);
            aRt.anchoredPosition = new Vector2(0f, 0f);
            aRt.sizeDelta = new Vector2(24f, 20f);
            aRt.localEulerAngles = new Vector3(0f, 0f, 180f); // Rotate 180 to point down

            Image aImg = arrowObj.GetComponent<Image>();
            aImg.sprite = arrowSprite;
            aImg.color = new Color32(0xFF, 0xD7, 0x00, 0xFF);
            aImg.raycastTarget = false;

            ftuePointerObj.SetActive(false);
        }

        private void UpdateFTUEGuide()
        {
            bool isFTUELevel = currentLevel == 1 && PlayerPrefs.GetInt("Roadways_FTUE_Completed", 0) == 0;

            idleTimer += Time.unscaledDeltaTime;
            bool isIdleHint = idleTimer > 6.0f;

            if (!isFTUELevel && !isIdleHint)
            {
                HideFTUEPointer();
                return;
            }

            int frontRow = ChilbiFlowController.Instance != null ? ChilbiFlowController.Instance.GetFrontRowIndex() : -1;
            if (frontRow < 0)
            {
                HideFTUEPointer();
                return;
            }

            int matchColor = ChilbiFlowController.Instance.GetRowColorTheme(frontRow);
            if (matchColor < 0 || matchColor >= depotBusRts.Count)
            {
                HideFTUEPointer();
                return;
            }

            RectTransform targetBusRt = depotBusRts[matchColor];
            if (targetBusRt == null || !targetBusRt.gameObject.activeSelf || IsBusAlreadyDispatched(matchColor))
            {
                HideFTUEPointer();
                return;
            }

            if (ftuePointerObj == null)
            {
                EnsureFTUEPointer(targetBusRt.parent);
            }

            if (ftuePointerObj != null)
            {
                if (!ftuePointerObj.activeSelf) ftuePointerObj.SetActive(true);
                ftuePointerObj.transform.SetAsLastSibling();

                float bob = Mathf.Sin(Time.unscaledTime * 5.5f) * 4f;
                ftuePointerRt.anchoredPosition = new Vector2(targetBusRt.anchoredPosition.x, 66f + bob);

                // Gentle pulsing beacon on target bus
                float pulse = 1.0f + Mathf.PingPong(Time.unscaledTime * 1.2f, 0.08f);
                targetBusRt.localScale = new Vector3(pulse, pulse, 1f);

                if (ftuePointerText != null)
                {
                    ftuePointerText.text = isFTUELevel ? "TAP TO DOCK" : "MATCHING";
                }
            }
        }

        private void HideFTUEPointer()
        {
            if (ftuePointerObj != null && ftuePointerObj.activeSelf)
            {
                ftuePointerObj.SetActive(false);
            }
            for (int i = 0; i < depotBusRts.Count; i++)
            {
                if (depotBusRts[i] != null && depotBusRts[i].localScale != Vector3.one)
                {
                    depotBusRts[i].localScale = Vector3.one;
                }
            }
        }

        public void CheckBoarding()
        {
            if (isBoardingActive || isInFlight) return;
            if (ChilbiFlowController.Instance == null) return;

            int frontRow = ChilbiFlowController.Instance.GetFrontRowIndex();
            if (frontRow == -1) return;

            int rowColor = ChilbiFlowController.Instance.GetRowColorTheme(frontRow);

            for (int b = 0; b < 9; b++)
            {
                if (bayOccupants[b] == rowColor && parkedBusObjects[b] != null)
                {
                    StartCoroutine(ExecuteBoardingSequence(frontRow, b, rowColor));
                    return;
                }
            }
        }

        private IEnumerator ExecuteBoardingSequence(int row, int bayIndex, int colorIndex)
        {
            isBoardingActive = true;
            HideFTUEPointer();

            if (currentLevel == 1)
            {
                Vector3 neckPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.52f, 0f);
                StartCoroutine(FloatingScoreRoutine(neckPos, "🌟 PERFECT MATCH!", new Color(1f, 0.85f, 0.2f)));
            }

            List<ChilbiFlowController.ChilbiInstance> rowChilbis = ChilbiFlowController.Instance.GetRowInstances(row);
            if (rowChilbis == null || rowChilbis.Count == 0)
            {
                isBoardingActive = false;
                yield break;
            }

            for (int i = 0; i < rowChilbis.Count; i++) rowChilbis[i].isBoarding = true;

            GameObject busObj = parkedBusObjects[bayIndex];
            if (busObj == null)
            {
                isBoardingActive = false;
                yield break;
            }

            Vector3 targetBusPos = busObj.transform.position;
            Transform roadPanelTransform = ChilbiFlowController.Instance.transform;
            Vector3 neckEntryWorld = roadPanelTransform.TransformPoint(new Vector3(0f, 160f, 0f));
            Vector3 neckExitWorld = roadPanelTransform.TransformPoint(new Vector3(0f, 0f, 0f));

            int totalPassengers = rowChilbis.Count;

            for (int p = 0; p < totalPassengers; p++)
            {
                int passengerNum = p + 1;
                ChilbiFlowController.ChilbiInstance kid = rowChilbis[p];
                StartCoroutine(AnimateSinglePassenger(kid, neckEntryWorld, neckExitWorld, targetBusPos, bayIndex, passengerNum, totalPassengers));
                yield return new WaitForSeconds(0.08f);
            }

            yield return new WaitForSeconds(1.3f);

            yield return StartCoroutine(DepartFullBusRoutine(bayIndex));

            ChilbiFlowController.Instance.RecycleRow(row);
            isBoardingActive = false;

            yield return new WaitForSeconds(0.2f);
            CheckBoarding();
        }

        private IEnumerator AnimateSinglePassenger(ChilbiFlowController.ChilbiInstance kid, Vector3 neckEntry, Vector3 neckExit, Vector3 busPos, int bayIndex, int passengerNum, int maxPassengers)
        {
            RectTransform kRt = kid.root;
            if (kRt == null) yield break;

            Vector3 startPos = kRt.position;

            float runSpeed = 280f;
            float hopHeight = 35f;
            float hopDuration = 0.32f;

            float dist1 = Vector3.Distance(startPos, neckEntry);
            float dur1 = Mathf.Max(0.12f, dist1 / runSpeed);
            float t1 = 0f;
            while (t1 < 1f)
            {
                t1 += Time.unscaledDeltaTime / dur1;
                kRt.position = Vector3.Lerp(startPos, neckEntry, Mathf.Clamp01(t1));
                yield return null;
            }

            float dist2 = Vector3.Distance(neckEntry, neckExit);
            float dur2 = Mathf.Max(0.12f, dist2 / runSpeed);
            float t2 = 0f;
            while (t2 < 1f)
            {
                t2 += Time.unscaledDeltaTime / dur2;
                kRt.position = Vector3.Lerp(neckEntry, neckExit, Mathf.Clamp01(t2));
                yield return null;
            }

            float t3 = 0f;
            Vector3 hopStart = neckExit;
            while (t3 < 1f)
            {
                t3 += Time.unscaledDeltaTime / hopDuration;
                float p = Mathf.Clamp01(t3);
                Vector3 groundPos = Vector3.Lerp(hopStart, busPos, p);
                float arc = 4f * hopHeight * p * (1f - p);
                kRt.position = groundPos + new Vector3(0f, arc, 0f);

                if (p > 0.7f)
                {
                    float scale = Mathf.Lerp(1f, 0.4f, (p - 0.7f) / 0.3f);
                    kRt.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            PlayBoardingPop(passengerNum);
            AndroidHaptics.LightClick();
            UpdatePassengerCount(bayIndex, passengerNum, maxPassengers);

            kRt.gameObject.SetActive(false);
        }

        private void EnsureAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }

            if (ambientAudioSource == null)
            {
                Transform existing = transform.Find("ZenAmbientMusic");
                if (existing != null)
                {
                    ambientAudioSource = existing.GetComponent<AudioSource>();
                }
                else
                {
                    GameObject ambObj = new GameObject("ZenAmbientMusic");
                    ambObj.transform.SetParent(transform);
                    ambientAudioSource = ambObj.AddComponent<AudioSource>();
                }
                ambientAudioSource.loop = true;
                ambientAudioSource.playOnAwake = false;
                ambientAudioSource.spatialBlend = 0f;
                ambientAudioSource.volume = 0.16f;
            }

            if (ambientClip == null) ambientClip = GenerateZenAmbientMelody();
            if (ambientAudioSource.clip == null && ambientClip != null)
            {
                ambientAudioSource.clip = ambientClip;
                ambientAudioSource.Play();
            }
        }

        public void BuildFleet(RectTransform canvas)
        {
            this.canvasRt = canvas;
            EnsureAudioSource();
            EnsureAssets();

            for (int i = 0; i < 9; i++) bayOccupants[i] = -1;

            // Find all 9 bay transforms
            FindBayTransforms();

            // Clear previous fleet container if exists
            Transform existing = transform.Find("BusFleetRowContainer");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            depotBusRts.Clear();

            GameObject rowObj = new GameObject("BusFleetRowContainer", typeof(RectTransform));
            rowObj.transform.SetParent(transform, false);
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -topPadding);
            rowRt.sizeDelta = new Vector2(1040f, 160f);

            int n = Fleet.Length;
            float startX = -((n - 1) * 0.5f) * busSpacing;

            for (int i = 0; i < n; i++)
            {
                int busIdx = i;
                BusDefinition def = Fleet[i];

                GameObject busObj = new GameObject($"Bus_{def.id}", typeof(RectTransform), typeof(Image), typeof(Button));
                busObj.transform.SetParent(rowObj.transform, false);
                RectTransform bRt = busObj.GetComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0.5f, 0.5f);
                bRt.anchorMax = new Vector2(0.5f, 0.5f);
                bRt.pivot = new Vector2(0.5f, 0.5f);
                bRt.anchoredPosition = new Vector2(startX + i * busSpacing, 0f);
                bRt.sizeDelta = busSize;

                Image bImg = busObj.GetComponent<Image>();
                bImg.sprite = busSprites[i];
                bImg.type = Image.Type.Simple;

                // Child White Roof Arrow
                GameObject arrowObj = new GameObject("RoofArrow", typeof(RectTransform), typeof(Image));
                arrowObj.transform.SetParent(busObj.transform, false);
                RectTransform aRt = arrowObj.GetComponent<RectTransform>();
                aRt.anchorMin = new Vector2(0.5f, 0.5f);
                aRt.anchorMax = new Vector2(0.5f, 0.5f);
                aRt.pivot = new Vector2(0.5f, 0.5f);
                aRt.anchoredPosition = Vector2.zero;
                aRt.sizeDelta = busSize;
                Image aImg = arrowObj.GetComponent<Image>();
                aImg.sprite = arrowSprite;
                aImg.raycastTarget = false;

                Button btn = busObj.GetComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => OnBusClicked(busIdx));

                depotBusRts.Add(bRt);
            }

            UpdateStatusHUD();
        }

        private void FindBayTransforms()
        {
            for (int i = 0; i < 9; i++)
            {
                GameObject bayObj = GameObject.Find($"TrapeziumBox_{i}");
                if (bayObj != null) bayRts[i] = bayObj.GetComponent<RectTransform>();
            }
        }

        public int GetFirstAvailableBay()
        {
            FindBayTransforms();
            for (int i = 0; i < 9; i++)
            {
                if (bayUnlocked[i] && bayOccupants[i] == -1 && bayRts[i] != null)
                {
                    return i;
                }
            }
            return -1;
        }

        public bool IsBusAlreadyDispatched(int busIndex)
        {
            for (int i = 0; i < 9; i++)
            {
                if (bayOccupants[i] == busIndex) return true;
            }
            return false;
        }

        public int FindBayWithBus(int busIndex)
        {
            for (int i = 0; i < 9; i++)
            {
                if (bayOccupants[i] == busIndex) return i;
            }
            return -1;
        }

        private void OnBusClicked(int busIndex)
        {
            if (isInFlight) return;
            if (IsBusAlreadyDispatched(busIndex)) return;

            idleTimer = 0f;
            HideFTUEPointer();

            int targetBay = GetFirstAvailableBay();
            if (targetBay == -1)
            {
                // All unlocked bays are full -> trigger rejection shake and warning horn
                StartCoroutine(RejectShake(depotBusRts[busIndex]));
                if (warningClip != null && audioSource != null) audioSource.PlayOneShot(warningClip, 0.7f);
                AndroidHaptics.DoubleBuzz();
                UpdateStatusHUD();
                return;
            }

            AndroidHaptics.LightClick();
            StartCoroutine(DispatchRoutine(busIndex, targetBay));
        }

        private IEnumerator RejectShake(RectTransform rt)
        {
            Vector2 origPos = rt.anchoredPosition;
            float dur = 0.28f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(elapsed * 45f) * 6f;
                rt.anchoredPosition = origPos + new Vector2(offset, 0f);
                yield return null;
            }
            rt.anchoredPosition = origPos;
        }

        private IEnumerator DispatchRoutine(int busIndex, int bayIndex)
        {
            isInFlight = true;
            BusDefinition def = Fleet[busIndex];

            // Play Harmonic Horn
            if (hornClips != null && hornClips.Length > busIndex && hornClips[busIndex] != null && audioSource != null)
            {
                audioSource.PlayOneShot(hornClips[busIndex], 0.85f);
            }

            FindBayTransforms();
            RectTransform targetBayRt = bayRts[bayIndex];
            if (targetBayRt == null || canvasRt == null)
            {
                isInFlight = false;
                yield break;
            }

            // Reserve bay slot
            bayOccupants[bayIndex] = busIndex;

            RectTransform origDepotRt = depotBusRts[busIndex];
            Vector3 startWorldPos = origDepotRt.position;

            // Hide original depot bus
            origDepotRt.gameObject.SetActive(false);

            // Create traveler bus directly under Canvas for free flight
            GameObject travelerObj = new GameObject($"Traveler_{def.id}_Bay{bayIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
            travelerObj.transform.SetParent(canvasRt.transform, false);
            RectTransform trRt = travelerObj.GetComponent<RectTransform>();
            trRt.sizeDelta = busSize;
            trRt.position = startWorldPos;
            trRt.localEulerAngles = Vector3.zero;

            Image trImg = travelerObj.GetComponent<Image>();
            trImg.sprite = busSprites[busIndex];

            // Roof Arrow child
            GameObject arrowObj = new GameObject("RoofArrow", typeof(RectTransform), typeof(Image));
            arrowObj.transform.SetParent(travelerObj.transform, false);
            RectTransform aRt = arrowObj.GetComponent<RectTransform>();
            aRt.anchorMin = new Vector2(0.5f, 0.5f);
            aRt.anchorMax = new Vector2(0.5f, 0.5f);
            aRt.pivot = new Vector2(0.5f, 0.5f);
            aRt.anchoredPosition = Vector2.zero;
            aRt.sizeDelta = busSize;
            Image aImg = arrowObj.GetComponent<Image>();
            aImg.sprite = arrowSprite;
            aImg.raycastTarget = false;

            // Passenger Counter Container (badge shown when passengers board)
            GameObject badgeObj = new GameObject("PassengerBadge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(travelerObj.transform, false);
            RectTransform bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0.5f);
            bRt.anchorMax = new Vector2(0.5f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = new Vector2(0f, -2f);
            bRt.sizeDelta = new Vector2(56f, 26f);

            Image bImg = badgeObj.GetComponent<Image>();
            bImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color(0.12f, 0.16f, 0.24f, 0.90f);
            bImg.raycastTarget = false;

            GameObject bTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            bTextObj.transform.SetParent(badgeObj.transform, false);
            RectTransform btRt = bTextObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.sizeDelta = Vector2.zero;

            Text badgeText = bTextObj.GetComponent<Text>();
            badgeText.font = GameUIController.DefaultFont;
            badgeText.fontSize = 17;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = Color.white;
            badgeText.text = "0/7";
            badgeText.raycastTarget = false;

            badgeObj.SetActive(false);

            Vector3 targetWorldPos = targetBayRt.position;
            float targetAngle = targetBayRt.localEulerAngles.z; // 30 degrees

            // Hermite trajectory calculation
            Vector3 p0 = startWorldPos;
            Vector3 p3 = targetWorldPos;
            float dist = Vector3.Distance(p0, p3);

            Vector3 m0 = Vector3.up * (dist * 0.9f);
            float rad = targetAngle * Mathf.Deg2Rad;
            Vector3 bayDir = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);
            Vector3 m1 = bayDir * (dist * 0.85f);

            float duration = 1.25f;
            float t = 0f;
            PlayEngineRumble();

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float progress = Mathf.Clamp01(t);

                float h00 = (1 + 2 * progress) * (1 - progress) * (1 - progress);
                float h10 = progress * (1 - progress) * (1 - progress);
                float h01 = progress * progress * (3 - 2 * progress);
                float h11 = progress * progress * (progress - 1);

                Vector3 curPos = h00 * p0 + h10 * m0 + h01 * p3 + h11 * m1;
                trRt.position = curPos;

                float curAngle = Mathf.Lerp(0f, targetAngle, Mathf.SmoothStep(0f, 1f, progress));
                trRt.localEulerAngles = new Vector3(0f, 0f, curAngle);

                if (progress < 0.25f)
                {
                    float p = progress / 0.25f;
                    trRt.localScale = new Vector3(Mathf.Lerp(1f, 0.96f, p), Mathf.Lerp(1f, 1.04f, p), 1f);
                }
                else if (progress > 0.85f)
                {
                    float p = (progress - 0.85f) / 0.15f;
                    trRt.localScale = new Vector3(Mathf.Lerp(0.96f, 1f, p), Mathf.Lerp(1.04f, 1f, p), 1f);
                }

                yield return null;
            }

            trRt.position = targetWorldPos;
            trRt.localEulerAngles = new Vector3(0f, 0f, targetAngle);
            trRt.localScale = Vector3.one;

            // Fade out roof arrow on dock
            float fadeDur = 0.25f;
            float fadeElapsed = 0f;
            Color arrowCol = aImg.color;
            while (fadeElapsed < fadeDur)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                arrowCol.a = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDur);
                aImg.color = arrowCol;
                yield return null;
            }
            arrowObj.SetActive(false);
            badgeObj.SetActive(true);

            parkedBusObjects[bayIndex] = travelerObj;
            lastDispatchedBayIndex = bayIndex;
            isInFlight = false;
            PlayAirBrake();
            AndroidHaptics.MediumClick();
            UpdateStatusHUD();

            // Enable tapping parked bus to return to depot
            int capturedBusIdx = busIndex;
            int capturedBayIdx = bayIndex;
            Button trBtn = travelerObj.GetComponent<Button>();
            trBtn.transition = Selectable.Transition.None;
            trBtn.onClick.AddListener(() => OnParkedBusClicked(capturedBusIdx, capturedBayIdx));

            OnBusDockedInBay?.Invoke(bayIndex, busIndex);
        }

        private void OnParkedBusClicked(int busIndex, int bayIndex)
        {
            if (isInFlight) return;
            StartCoroutine(ReturnToDepotRoutine(busIndex, bayIndex));
        }

        public IEnumerator ReturnToDepotRoutine(int busIndex, int bayIndex)
        {
            if (isInFlight) yield break;
            GameObject travelerObj = parkedBusObjects[bayIndex];
            if (travelerObj == null) yield break;

            isInFlight = true;

            RectTransform trRt = travelerObj.GetComponent<RectTransform>();
            RectTransform origDepotRt = depotBusRts[busIndex];

            // Re-show arrow
            Transform arrowTransform = travelerObj.transform.Find("RoofArrow");
            if (arrowTransform != null)
            {
                arrowTransform.gameObject.SetActive(true);
                Image aImg = arrowTransform.GetComponent<Image>();
                Color col = aImg.color;
                col.a = 1f;
                aImg.color = col;
            }

            Vector3 startPos = trRt.position;
            Vector3 endPos = origDepotRt.position;
            float startAngle = trRt.localEulerAngles.z;

            float duration = 0.85f;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                trRt.position = Vector3.Lerp(startPos, endPos, progress);
                trRt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startAngle, 0f, progress));
                yield return null;
            }

            // Restore depot bus and destroy traveler
            origDepotRt.gameObject.SetActive(true);
            Destroy(travelerObj);
            parkedBusObjects[bayIndex] = null;
            bayOccupants[bayIndex] = -1;
            isInFlight = false;
            AndroidHaptics.LightClick();
            UpdateStatusHUD();

            OnBusLeftBay?.Invoke(bayIndex, busIndex);
        }

        public IEnumerator DepartFullBusRoutine(int bayIndex)
        {
            GameObject busObj = parkedBusObjects[bayIndex];
            if (busObj == null) yield break;

            int busIdx = bayOccupants[bayIndex];
            RectTransform bRt = busObj.GetComponent<RectTransform>();

            // 1. Honk celebration
            if (hornClips != null && busIdx >= 0 && busIdx < hornClips.Length && hornClips[busIdx] != null && audioSource != null)
            {
                audioSource.PlayOneShot(hornClips[busIdx], 1.0f);
            }

            // 2. Combo streak & score award
            float now = Time.unscaledTime;
            if (now - lastDepartureTime < 8.5f)
            {
                comboStreak++;
            }
            else
            {
                comboStreak = 1;
            }
            lastDepartureTime = now;
            if (comboStreak > maxCombo) maxCombo = comboStreak;

            int departurePts = 100 * comboStreak;
            bool isVipBay = (bayIndex == 5);
            if (isVipBay)
            {
                departurePts *= 2;
            }
            currentScore += departurePts;
            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt("Roadways_HighScore", highScore);
                PlayerPrefs.Save();
            }
            totalClearedChilbis += 7;

            string comboMsg = isVipBay ? $"👑 VIP x2! +{departurePts}" : (comboStreak > 1 ? $"COMBO x{comboStreak}! +{departurePts}" : $"+{departurePts}");
            Color scoreCol = isVipBay ? new Color(1f, 0.85f, 0.2f) : (comboStreak > 1 ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 0.9f, 0.4f));
            StartCoroutine(FloatingScoreRoutine(bRt.position + new Vector3(0f, 40f, 0f), comboMsg, scoreCol));

            // 3. Accelerate forward out of the bay along its tilted longitudinal axis
            float targetAngle = bRt.localEulerAngles.z;
            float rad = targetAngle * Mathf.Deg2Rad;
            Vector3 exitDir = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);

            Vector3 startPos = bRt.position;
            Vector3 exitPos = startPos + exitDir * 900f; // off screen

            float dur = 0.95f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float p = Mathf.Pow(Mathf.Clamp01(t), 2.2f); // accelerate forward
                bRt.position = Vector3.Lerp(startPos, exitPos, p);
                yield return null;
            }

            // Clean up
            Destroy(busObj);
            parkedBusObjects[bayIndex] = null;
            bayOccupants[bayIndex] = -1;

            AndroidHaptics.HeavyThud();
            UpdateStatusHUD();

            // Re-enable depot bus after departure
            if (busIdx >= 0 && busIdx < depotBusRts.Count && depotBusRts[busIdx] != null)
            {
                depotBusRts[busIdx].gameObject.SetActive(true);
            }

            OnBusLeftBay?.Invoke(bayIndex, busIdx);

            // Win condition check
            if (totalClearedChilbis >= LevelTargetChilbis)
            {
                ShowVictoryModal();
            }
        }

        public void PlayBoardingPop(int passengerNum = 1)
        {
            if (popClip != null && audioSource != null)
            {
                audioSource.pitch = 0.95f + (passengerNum * 0.08f);
                audioSource.PlayOneShot(popClip, 0.65f);
                audioSource.pitch = 1.0f;
            }
        }

        public void UpdatePassengerCount(int bayIndex, int count, int max = 7)
        {
            GameObject busObj = parkedBusObjects[bayIndex];
            if (busObj == null) return;

            Transform badgeTransform = busObj.transform.Find("PassengerBadge");
            bool isVipBay = (bayIndex == 5);
            if (badgeTransform != null)
            {
                badgeTransform.gameObject.SetActive(true);
                Image bImg = badgeTransform.GetComponent<Image>();
                Transform tObj = badgeTransform.Find("Text");
                Text t = tObj != null ? tObj.GetComponent<Text>() : badgeTransform.GetComponent<Text>();

                if (count >= max)
                {
                    if (t != null) t.text = isVipBay ? "VIP FULL! ⭐️" : "FULL! ⭐️";
                    if (bImg != null) bImg.color = isVipBay ? new Color(1f, 0.84f, 0.0f, 1f) : new Color(0.95f, 0.65f, 0.12f, 0.95f);
                }
                else
                {
                    if (t != null) t.text = isVipBay ? $"VIP {count}/{max}" : $"{count}/{max}";
                    if (bImg != null) bImg.color = isVipBay ? new Color(0.85f, 0.65f, 0.05f, 0.95f) : new Color(0.12f, 0.16f, 0.24f, 0.90f);
                }

                StartCoroutine(PunchBadge(badgeTransform));
            }

            int passengerPts = isVipBay ? 20 : 10;
            currentScore += passengerPts;
            if (currentScore > highScore)
            {
                highScore = currentScore;
                PlayerPrefs.SetInt("Roadways_HighScore", highScore);
                PlayerPrefs.Save();
            }
            string popText = isVipBay ? "+20 VIP" : "+10";
            StartCoroutine(FloatingScoreRoutine(busObj.transform.position + new Vector3(0f, 25f, 0f), popText, isVipBay ? new Color(1f, 0.85f, 0.2f) : new Color(0.2f, 0.9f, 1f)));
            UpdateStatusHUD();
        }

        private IEnumerator PunchBadge(Transform badgeTrans)
        {
            float elapsed = 0f;
            float dur = 0.22f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = elapsed / dur;
                float scale = 1f + 0.35f * Mathf.Sin(p * Mathf.PI);
                if (badgeTrans != null) badgeTrans.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (badgeTrans != null) badgeTrans.localScale = Vector3.one;
        }

        private IEnumerator FloatingScoreRoutine(Vector3 worldPos, string text, Color color)
        {
            Transform canvasTrans = GetCanvasTransform();
            if (canvasTrans == null) yield break;

            GameObject scoreObj = new GameObject("FloatingScore", typeof(RectTransform), typeof(Text), typeof(Outline));
            scoreObj.transform.SetParent(canvasTrans, false);
            scoreObj.transform.position = worldPos;

            Text t = scoreObj.GetComponent<Text>();
            t.font = GameUIController.DefaultFont;
            t.fontSize = 26;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.text = text;

            Outline ol = scoreObj.GetComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.6f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            float elapsed = 0f;
            float duration = 0.85f;
            Vector3 start = scoreObj.transform.position;
            Vector3 target = start + new Vector3(0f, 70f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                scoreObj.transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, p));
                Color c = color;
                c.a = Mathf.Lerp(1f, 0f, p * p);
                t.color = c;
                yield return null;
            }

            Destroy(scoreObj);
        }

        #region Procedural Asset Generation
        private static void EnsureAssets()
        {
            if (busSprites == null || busSprites.Length != Fleet.Length)
            {
                busSprites = new Sprite[Fleet.Length];
                for (int i = 0; i < Fleet.Length; i++)
                {
                    busSprites[i] = CreateBusSprite(128, 256, Fleet[i].bodyColor, Fleet[i].hasHoodLine);
                }
            }

            if (arrowSprite == null) arrowSprite = CreateArrowSprite(128, 256);

            if (hornClips == null || hornClips.Length != Fleet.Length)
            {
                hornClips = new AudioClip[Fleet.Length];
                for (int i = 0; i < Fleet.Length; i++)
                {
                    hornClips[i] = GenerateHarmonicHorn(Fleet[i].hornFreq, 0.32f);
                }
                warningClip = GenerateWarningHorn();
                popClip = GeneratePopSound();
            }

            if (airBrakeClip == null) airBrakeClip = GenerateAirBrakeSound();
            if (engineRumbleClip == null) engineRumbleClip = GenerateEngineRumbleSound();
            if (ambientClip == null) ambientClip = GenerateZenAmbientMelody();
        }

        private static Sprite CreateBusSprite(int w, int h, Color32 bodyColor, bool hasHoodLine)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] px = new Color32[w * h];
            float rx = 28f;
            float xMin = 5f, xMax = w - 6f;
            float yMin = 5f, yMax = h - 6f;

            Color32 tintWindow = new Color32(0x1E, 0x24, 0x30, 240);
            Color32 hoodCol = new Color32(0x1E, 0x24, 0x30, 110);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Clamp(x, xMin + rx, xMax - rx);
                    float cy = Mathf.Clamp(y, yMin + rx, yMax - rx);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(rx - d + 0.5f);

                    if (alpha <= 0.001f)
                    {
                        px[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color32 cur = bodyColor;

                    // Front windshield
                    if (y >= 206 && y <= 240 && x >= 16 && x <= w - 17)
                    {
                        float wy = Mathf.Clamp(y, 214f, 232f);
                        float wx = Mathf.Clamp(x, 24f, w - 25f);
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(wx, wy)) <= 8.5f) cur = tintWindow;
                    }

                    // Side windows
                    int[] sideYs = new int[] { 54, 94, 134, 174 };
                    foreach (int sy in sideYs)
                    {
                        if (y >= sy && y <= sy + 28)
                        {
                            if ((x >= 8 && x <= 18) || (x >= w - 19 && x <= w - 9))
                            {
                                float wy = Mathf.Clamp(y, sy + 3, sy + 25);
                                float wx = (x <= 18) ? Mathf.Clamp(x, 11f, 15f) : Mathf.Clamp(x, w - 16f, w - 12f);
                                if (Vector2.Distance(new Vector2(x, y), new Vector2(wx, wy)) <= 3.5f) cur = tintWindow;
                            }
                        }
                    }

                    // Rear window
                    if (y >= 14 && y <= 24 && x >= 20 && x <= w - 21) cur = tintWindow;

                    // Yellow hood line
                    if (hasHoodLine && y >= 194 && y <= 196 && x >= 16 && x <= w - 17) cur = hoodCol;

                    cur.a = (byte)(alpha * 255f);
                    px[y * w + x] = cur;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateArrowSprite(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside = false;
                    if (y >= 135 && y <= 175)
                    {
                        float frac = (175f - y) / 40f;
                        float halfW = frac * 30f;
                        if (Mathf.Abs(x - 64f) <= halfW) inside = true;
                    }
                    if (y >= 82 && y <= 135 && x >= 52 && x <= 76) inside = true;
                    if (y >= 74 && y <= 86 && Vector2.Distance(new Vector2(x, y), new Vector2(64f, 82f)) <= 12f) inside = true;

                    px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        private static AudioClip GenerateHarmonicHorn(float baseFreq, float duration)
        {
            int sampleRate = 22050;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f;
                if (t < 0.02f) env = t / 0.02f;
                else if (t > duration - 0.06f) env = (duration - t) / 0.06f;

                float wave = Mathf.Sin(2f * Mathf.PI * baseFreq * t) * 0.7f +
                             Mathf.Sin(2f * Mathf.PI * (baseFreq * 1.5f) * t) * 0.3f;
                data[i] = wave * env * 0.45f;
            }

            AudioClip clip = AudioClip.Create($"Horn_{baseFreq}", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateWarningHorn()
        {
            int sampleRate = 22050;
            float duration = 0.22f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = (t > duration - 0.04f) ? (duration - t) / 0.04f : 1f;
                float wave = Mathf.Sin(2f * Mathf.PI * 196f * t);
                data[i] = wave * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("WarningHorn", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GeneratePopSound()
        {
            int sampleRate = 22050;
            float duration = 0.08f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (t / duration);
                float freq = Mathf.Lerp(300f, 750f, t / duration);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("BoardingPop", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateAirBrakeSound()
        {
            int sampleRate = 22050;
            float duration = 0.32f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];
            System.Random rand = new System.Random(42);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                float hissEnv = Mathf.Clamp01(1f - (t / 0.28f));
                hissEnv = hissEnv * hissEnv;
                float hiss = noise * hissEnv * 0.35f;

                float click = 0f;
                if (t >= 0.16f && t <= 0.22f)
                {
                    float tc = t - 0.16f;
                    float clickEnv = Mathf.Exp(-tc * 85f);
                    click = Mathf.Sin(2f * Mathf.PI * 880f * tc) * clickEnv * 0.7f;
                }

                data[i] = Mathf.Clamp(hiss + click, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("AirBrake", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateEngineRumbleSound()
        {
            int sampleRate = 22050;
            float duration = 0.42f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float p = t / duration;
                float env = p < 0.15f ? p / 0.15f : (1f - p) * 0.9f;

                float freq = Mathf.Lerp(65f, 175f, p * p);
                float fundamental = Mathf.Sin(2f * Mathf.PI * freq * t);
                float harmonic = 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                float sub = 0.25f * Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t);

                data[i] = (fundamental + harmonic + sub) * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("EngineRumble", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayAirBrake()
        {
            if (airBrakeClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(airBrakeClip, 0.75f);
            }
        }

        public void PlayEngineRumble()
        {
            if (engineRumbleClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(engineRumbleClip, 0.65f);
            }
        }

        private static AudioClip GenerateZenAmbientMelody()
        {
            int sampleRate = 22050;
            float duration = 12.0f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            // 4 pastel chords: C major, Am, F, G
            float[][] chords = new float[][]
            {
                new float[] { 261.63f, 329.63f, 392.00f, 523.25f }, // C
                new float[] { 220.00f, 261.63f, 329.63f, 440.00f }, // Am
                new float[] { 174.61f, 220.00f, 261.63f, 349.23f }, // F
                new float[] { 196.00f, 246.94f, 293.66f, 392.00f }  // G
            };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                int measure = Mathf.Clamp((int)(t / 3.0f), 0, 3);
                float measureTime = t % 3.0f;

                // Soft bell-like envelope
                float env = Mathf.Sin((measureTime / 3.0f) * Mathf.PI);
                env = Mathf.Pow(Mathf.Clamp01(env), 0.75f);

                float sample = 0f;
                float[] notes = chords[measure];
                for (int n = 0; n < notes.Length; n++)
                {
                    float freq = notes[n];
                    float phase = 2f * Mathf.PI * freq * t;
                    float vibrato = Mathf.Sin(2f * Mathf.PI * 4.5f * t) * 0.4f;
                    float wave = Mathf.Sin(phase + vibrato);
                    float harmonic = 0.22f * Mathf.Sin(2f * phase);
                    sample += (wave + harmonic) * (0.10f / notes.Length);
                }

                // Global loop edge fade
                float edgeFade = 1f;
                if (t < 0.25f) edgeFade = t / 0.25f;
                else if (t > duration - 0.25f) edgeFade = (duration - t) / 0.25f;

                data[i] = sample * env * edgeFade;
            }

            AudioClip clip = AudioClip.Create("ZenAmbientMelody", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion

        #region Bay Unlocking, Scoring, Gridlock & Victory HUD
        [System.NonSerialized] private int totalClearedChilbis = 0;
        [System.NonSerialized] private int currentScore = 0;
        [System.NonSerialized] private int comboStreak = 0;
        [System.NonSerialized] private int maxCombo = 1;
        [System.NonSerialized] private float lastDepartureTime = -10f;
        [System.NonSerialized] private Text statusText;
        [System.NonSerialized] private GameObject victoryModalObj;
        [System.NonSerialized] private GameObject gridlockBannerObj;
        [System.NonSerialized] private GameObject gridlockModalObj;
        [System.NonSerialized] private Text gridlockRecallBtnText;
        [System.NonSerialized] private Text gridlockShuffleBtnText;
        [System.NonSerialized] private int currentLevel = 1;
        [System.NonSerialized] private int highScore = 0;
        [System.NonSerialized] private int lastDispatchedBayIndex = -1;

        public int CurrentLevel => currentLevel;
        public int HighScore => highScore;
        public int TotalClearedChilbis => totalClearedChilbis;
        public int TotalScore => currentScore;
        public int ComboStreak => comboStreak;

        public int LevelTargetChilbis
        {
            get
            {
                switch (currentLevel)
                {
                    case 1: return 28;  // 4 rows
                    case 2: return 56;  // 8 rows
                    case 3: return 84;  // 12 rows
                    case 4: return 112; // 16 rows
                    default: return 140;
                }
            }
        }
        public int OpenBaysCount
        {
            get
            {
                int open = 0;
                for (int i = 0; i < 9; i++)
                {
                    if (bayUnlocked[i] && bayOccupants[i] < 0) open++;
                }
                return open;
            }
        }

        private void FindStatusText()
        {
            if (statusText != null) return;
            GameObject stObj = GameObject.Find("StatusText");
            if (stObj != null) statusText = stObj.GetComponent<Text>();
        }

        public void UnlockBay(int bayIndex, GameObject bayObj, Sprite standardSprite)
        {
            if (bayIndex < 0 || bayIndex >= 9) return;
            if (bayUnlocked[bayIndex]) return;

            int reqLevel = (bayIndex == 6) ? 3 : (bayIndex == 7 ? 5 : 8);
            if (currentLevel < reqLevel)
            {
                AndroidHaptics.DoubleBuzz();
                if (warningClip != null && audioSource != null && SfxEnabled) audioSource.PlayOneShot(warningClip, 0.7f);
                Vector3 centerPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, 0f);
                StartCoroutine(FloatingScoreRoutine(centerPos, $"🔒 Bay {bayIndex} Unlocks at Level {reqLevel}!", new Color(1f, 0.4f, 0.4f)));
                return;
            }

            bayUnlocked[bayIndex] = true;

            Image img = bayObj.GetComponent<Image>();
            if (img != null) img.sprite = standardSprite;

            Transform plus = bayObj.transform.Find("PlusText");
            if (plus != null) plus.gameObject.SetActive(false);

            if (audioSource != null && hornClips != null && hornClips.Length > 7 && hornClips[7] != null && SfxEnabled)
            {
                audioSource.PlayOneShot(hornClips[7], 0.75f);
            }
            AndroidHaptics.MediumClick();

            SetGridlockWarning(false);
            UpdateStatusHUD();
        }

        public void RefreshBayUnlocks()
        {
            bayUnlocked[0] = true;
            bayUnlocked[1] = true;
            bayUnlocked[2] = true;
            bayUnlocked[3] = true;
            bayUnlocked[4] = true;
            bayUnlocked[5] = true; // VIP
            bayUnlocked[6] = currentLevel >= 3;
            bayUnlocked[7] = currentLevel >= 5;
            bayUnlocked[8] = currentLevel >= 8;

            FindBayTransforms();
            for (int i = 6; i < 9; i++)
            {
                if (bayRts[i] == null) continue;
                GameObject bayObj = bayRts[i].gameObject;
                Transform plus = bayObj.transform.Find("PlusText");
                Text pt = plus != null ? plus.GetComponent<Text>() : null;
                Image img = bayObj.GetComponent<Image>();

                if (bayUnlocked[i])
                {
                    if (plus != null) plus.gameObject.SetActive(false);
                    if (img != null) img.sprite = GameUIController.CreateRoundedBoxSprite(68, 136, 16, 5, Color.white, new Color32(0x62, 0x64, 0x79, 0xFF));
                }
                else
                {
                    if (plus != null)
                    {
                        plus.gameObject.SetActive(true);
                        int req = (i == 6) ? 3 : (i == 7 ? 5 : 8);
                        if (pt != null)
                        {
                            pt.fontSize = 22;
                            pt.text = $"🔒\nL{req}";
                            pt.color = new Color32(0xE7, 0x4C, 0x3C, 0xFF);
                        }
                    }
                    if (img != null)
                    {
                        img.sprite = GameUIController.CreateRoundedBoxSprite(68, 136, 16, 5, new Color32(0xE7, 0x4C, 0x3C, 0x88), new Color32(0x3B, 0x42, 0x52, 0xCC));
                    }
                }
            }
        }

        public void SetLevel(int level)
        {
            currentLevel = level;
            PlayerPrefs.SetInt("Roadways_Level", currentLevel);
            PlayerPrefs.Save();
            ResetGame();
            if (ChilbiFlowController.Instance != null)
            {
                ChilbiFlowController.Instance.SetLevel(currentLevel);
                ChilbiFlowController.Instance.ResetParade();
            }
            RefreshBayUnlocks();
            UpdateStatusHUD();
            if (GameUIController.Instance != null)
            {
                GameUIController.Instance.UpdateTitle($"⭐ LEVEL {currentLevel}  •  👑 HIGH: {highScore}");
            }
        }

        public void ShowFloatingTip(string message)
        {
            Vector3 centerPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, 0f);
            StartCoroutine(FloatingScoreRoutine(centerPos, message, new Color(1f, 0.85f, 0.3f)));
        }

        public void SetGridlockWarning(bool show)
        {
            if (show)
            {
                if (gridlockModalObj == null)
                {
                    CreateGridlockRecoveryModal();
                }

                if (gridlockModalObj != null)
                {
                    RefreshGridlockModalButtons();
                    if (!gridlockModalObj.activeSelf)
                    {
                        gridlockModalObj.SetActive(true);
                        AndroidHaptics.DoubleBuzz();
                        if (warningClip != null && audioSource != null && SfxEnabled) audioSource.PlayOneShot(warningClip, 0.8f);
                    }
                }
            }
            else
            {
                if (gridlockModalObj != null && gridlockModalObj.activeSelf)
                {
                    gridlockModalObj.SetActive(false);
                }
                if (gridlockBannerObj != null && gridlockBannerObj.activeSelf)
                {
                    gridlockBannerObj.SetActive(false);
                }
            }
        }

        private void CreateGridlockRecoveryModal()
        {
            Transform canvasTrans = GetCanvasTransform();
            if (canvasTrans == null) return;

            gridlockModalObj = new GameObject("GridlockRecoveryModal", typeof(RectTransform), typeof(Image));
            gridlockModalObj.transform.SetParent(canvasTrans, false);
            RectTransform gmRt = gridlockModalObj.GetComponent<RectTransform>();
            gmRt.anchorMin = Vector2.zero;
            gmRt.anchorMax = Vector2.one;
            gmRt.sizeDelta = Vector2.zero;

            Image bgImg = gridlockModalObj.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.75f);

            // Dialog Card
            GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(gridlockModalObj.transform, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(580f, 480f);

            Image cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = GameUIController.CreatePillSprite(128, 128, 28);
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color32(0xFF, 0xFA, 0xFA, 0xFF);

            // Warning Badge
            GameObject badgeObj = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(cardObj.transform, false);
            RectTransform bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchoredPosition = new Vector2(0f, 185f);
            bRt.sizeDelta = new Vector2(460f, 48f);
            Image bImg = badgeObj.GetComponent<Image>();
            bImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color32(0xE7, 0x4C, 0x3C, 0xFF);

            GameObject bTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            bTextObj.transform.SetParent(badgeObj.transform, false);
            RectTransform btRt = bTextObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.sizeDelta = Vector2.zero;
            Text bt = bTextObj.GetComponent<Text>();
            bt.font = GameUIController.DefaultFont;
            bt.fontSize = 22;
            bt.fontStyle = FontStyle.Bold;
            bt.alignment = TextAnchor.MiddleCenter;
            bt.color = Color.white;
            bt.text = "⚠️ GRIDLOCK DETECTED!";

            // Subtitle Description
            GameObject descObj = new GameObject("Desc", typeof(RectTransform), typeof(Text));
            descObj.transform.SetParent(cardObj.transform, false);
            RectTransform dRt = descObj.GetComponent<RectTransform>();
            dRt.anchoredPosition = new Vector2(0f, 115f);
            dRt.sizeDelta = new Vector2(520f, 65f);
            Text dText = descObj.GetComponent<Text>();
            dText.font = GameUIController.DefaultFont;
            dText.fontSize = 17;
            dText.alignment = TextAnchor.MiddleCenter;
            dText.lineSpacing = 1.25f;
            dText.color = new Color32(0x55, 0x5F, 0x72, 0xFF);
            dText.text = "All parking bays are full and none match the parade!\nChoose an emergency rescue option to continue:";

            // Option 1: Emergency Recall
            GameObject recallBtnObj = new GameObject("RecallRescueBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            recallBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform rcRt = recallBtnObj.GetComponent<RectTransform>();
            rcRt.anchoredPosition = new Vector2(0f, 42f);
            rcRt.sizeDelta = new Vector2(480f, 54f);
            Image rcImg = recallBtnObj.GetComponent<Image>();
            rcImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            rcImg.type = Image.Type.Sliced;
            rcImg.color = new Color32(0x34, 0x49, 0x5E, 0xFF);

            Button rcBtn = recallBtnObj.GetComponent<Button>();
            rcBtn.onClick.AddListener(() =>
            {
                gridlockModalObj.SetActive(false);
                if (recallCount <= 0)
                {
                    recallCount = 1; // grant free emergency rescue
                    PlayerPrefs.SetInt("Roadways_RecallCount", recallCount);
                    PlayerPrefs.Save();
                    if (GameUIController.Instance != null) GameUIController.Instance.UpdateBoosterLabels();
                }
                RecallLastBus();
            });

            GameObject rcTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            rcTextObj.transform.SetParent(recallBtnObj.transform, false);
            RectTransform rctRt = rcTextObj.GetComponent<RectTransform>();
            rctRt.anchorMin = Vector2.zero;
            rctRt.anchorMax = Vector2.one;
            rctRt.sizeDelta = Vector2.zero;
            gridlockRecallBtnText = rcTextObj.GetComponent<Text>();
            gridlockRecallBtnText.font = GameUIController.DefaultFont;
            gridlockRecallBtnText.fontSize = 18;
            gridlockRecallBtnText.fontStyle = FontStyle.Bold;
            gridlockRecallBtnText.alignment = TextAnchor.MiddleCenter;
            gridlockRecallBtnText.color = Color.white;

            // Option 2: Emergency Shuffle
            GameObject shuffleBtnObj = new GameObject("ShuffleRescueBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            shuffleBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform shRt = shuffleBtnObj.GetComponent<RectTransform>();
            shRt.anchoredPosition = new Vector2(0f, -28f);
            shRt.sizeDelta = new Vector2(480f, 54f);
            Image shImg = shuffleBtnObj.GetComponent<Image>();
            shImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            shImg.type = Image.Type.Sliced;
            shImg.color = new Color32(0x8E, 0x44, 0xAD, 0xFF);

            Button shBtn = shuffleBtnObj.GetComponent<Button>();
            shBtn.onClick.AddListener(() =>
            {
                gridlockModalObj.SetActive(false);
                if (shuffleCount <= 0)
                {
                    shuffleCount = 1; // grant free emergency rescue
                    PlayerPrefs.SetInt("Roadways_ShuffleCount", shuffleCount);
                    PlayerPrefs.Save();
                    if (GameUIController.Instance != null) GameUIController.Instance.UpdateBoosterLabels();
                }
                ShuffleParade();
            });

            GameObject shTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            shTextObj.transform.SetParent(shuffleBtnObj.transform, false);
            RectTransform shtRt = shTextObj.GetComponent<RectTransform>();
            shtRt.anchorMin = Vector2.zero;
            shtRt.anchorMax = Vector2.one;
            shtRt.sizeDelta = Vector2.zero;
            gridlockShuffleBtnText = shTextObj.GetComponent<Text>();
            gridlockShuffleBtnText.font = GameUIController.DefaultFont;
            gridlockShuffleBtnText.fontSize = 18;
            gridlockShuffleBtnText.fontStyle = FontStyle.Bold;
            gridlockShuffleBtnText.alignment = TextAnchor.MiddleCenter;
            gridlockShuffleBtnText.color = Color.white;

            // Option 3: Restart Level
            GameObject restartBtnObj = new GameObject("RestartBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            restartBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform rstRt = restartBtnObj.GetComponent<RectTransform>();
            rstRt.anchoredPosition = new Vector2(0f, -98f);
            rstRt.sizeDelta = new Vector2(340f, 46f);
            Image rstImg = restartBtnObj.GetComponent<Image>();
            rstImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            rstImg.type = Image.Type.Sliced;
            rstImg.color = new Color32(0x7F, 0x8C, 0x8D, 0xFF);

            Button rstBtn = restartBtnObj.GetComponent<Button>();
            rstBtn.onClick.AddListener(() =>
            {
                gridlockModalObj.SetActive(false);
                ResetGame();
                if (ChilbiFlowController.Instance != null) ChilbiFlowController.Instance.ResetParade();
                UpdateStatusHUD();
            });

            GameObject rstTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            rstTextObj.transform.SetParent(restartBtnObj.transform, false);
            RectTransform rsttRt = rstTextObj.GetComponent<RectTransform>();
            rsttRt.anchorMin = Vector2.zero;
            rsttRt.anchorMax = Vector2.one;
            rsttRt.sizeDelta = Vector2.zero;
            Text rstText = rstTextObj.GetComponent<Text>();
            rstText.font = GameUIController.DefaultFont;
            rstText.fontSize = 17;
            rstText.fontStyle = FontStyle.Bold;
            rstText.alignment = TextAnchor.MiddleCenter;
            rstText.color = Color.white;
            rstText.text = "↺ Restart Level";

            // Tip Footer
            GameObject tipObj = new GameObject("Tip", typeof(RectTransform), typeof(Text));
            tipObj.transform.SetParent(cardObj.transform, false);
            RectTransform tpRt = tipObj.GetComponent<RectTransform>();
            tpRt.anchoredPosition = new Vector2(0f, -162f);
            tpRt.sizeDelta = new Vector2(500f, 38f);
            Text tpText = tipObj.GetComponent<Text>();
            tpText.font = GameUIController.DefaultFont;
            tpText.fontSize = 14;
            tpText.fontStyle = FontStyle.Italic;
            tpText.alignment = TextAnchor.MiddleCenter;
            tpText.color = new Color32(0x95, 0xA5, 0xA6, 0xFF);
            tpText.text = "💡 Tip: Prioritize front-row colors to keep bays open!";
        }

        private void RefreshGridlockModalButtons()
        {
            if (gridlockRecallBtnText != null)
            {
                gridlockRecallBtnText.text = recallCount > 0 ? $"↺ RECALL LAST BUS  ({recallCount} Left)" : "↺ FREE EMERGENCY RECALL  (+1 Free)";
            }
            if (gridlockShuffleBtnText != null)
            {
                gridlockShuffleBtnText.text = shuffleCount > 0 ? $"🔀 SHUFFLE PARADE  ({shuffleCount} Left)" : "🔀 FREE EMERGENCY SHUFFLE  (+1 Free)";
            }
        }

        public void ShowVictoryModal()
        {
            if (victoryModalObj != null)
            {
                victoryModalObj.SetActive(true);
                return;
            }

            Transform canvasTrans = GetCanvasTransform();
            if (canvasTrans == null) return;

            victoryModalObj = new GameObject("VictoryModal", typeof(RectTransform), typeof(Image));
            victoryModalObj.transform.SetParent(canvasTrans, false);
            RectTransform vmRt = victoryModalObj.GetComponent<RectTransform>();
            vmRt.anchorMin = Vector2.zero;
            vmRt.anchorMax = Vector2.one;
            vmRt.sizeDelta = Vector2.zero;

            Image bgImg = victoryModalObj.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.65f);

            // Card
            GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(victoryModalObj.transform, false);
            RectTransform cardRt = cardObj.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(500f, 380f);

            Image cardImg = cardObj.GetComponent<Image>();
            cardImg.sprite = GameUIController.CreatePillSprite(128, 128, 28);
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color32(0xFF, 0xFA, 0xFA, 0xFF);

            // Title
            GameObject titleObj = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(cardObj.transform, false);
            RectTransform tRt = titleObj.GetComponent<RectTransform>();
            tRt.anchoredPosition = new Vector2(0f, 130f);
            tRt.sizeDelta = new Vector2(460f, 50f);
            Text tText = titleObj.GetComponent<Text>();
            tText.font = GameUIController.DefaultFont;
            tText.fontSize = 28;
            tText.fontStyle = FontStyle.Bold;
            tText.alignment = TextAnchor.MiddleCenter;
            tText.color = new Color32(0x2C, 0x3E, 0x50, 0xFF);
            if (currentLevel == 1)
            {
                PlayerPrefs.SetInt("Roadways_FTUE_Completed", 1);
                PlayerPrefs.Save();
                tText.text = "🏆 TUTORIAL COMPLETE! 🏆";
            }
            else
            {
                tText.text = $"🏆 LEVEL {currentLevel} COMPLETE! 🏆";
            }

            // Stars
            GameObject starsObj = new GameObject("Stars", typeof(RectTransform), typeof(Text));
            starsObj.transform.SetParent(cardObj.transform, false);
            RectTransform sRt = starsObj.GetComponent<RectTransform>();
            sRt.anchoredPosition = new Vector2(0f, 75f);
            sRt.sizeDelta = new Vector2(300f, 50f);
            Text sText = starsObj.GetComponent<Text>();
            sText.font = GameUIController.DefaultFont;
            sText.fontSize = 36;
            sText.alignment = TextAnchor.MiddleCenter;
            sText.color = new Color(1f, 0.82f, 0.1f);
            sText.text = "⭐️ ⭐️ ⭐️";

            // Stats
            GameObject statsObj = new GameObject("Stats", typeof(RectTransform), typeof(Text));
            statsObj.transform.SetParent(cardObj.transform, false);
            RectTransform stRt = statsObj.GetComponent<RectTransform>();
            stRt.anchoredPosition = new Vector2(0f, 0f);
            stRt.sizeDelta = new Vector2(440f, 80f);
            Text stText = statsObj.GetComponent<Text>();
            stText.font = GameUIController.DefaultFont;
            stText.fontSize = 20;
            stText.alignment = TextAnchor.MiddleCenter;
            stText.lineSpacing = 1.3f;
            stText.color = new Color32(0x55, 0x5F, 0x72, 0xFF);
            stText.text = $"{totalClearedChilbis} Chilbis Boarded!\nScore: <b>{currentScore}</b>  •  High: <b>{highScore}</b>\nMax Combo: <b>x{maxCombo}</b>";

            // Replay Button (Left)
            GameObject replayBtnObj = new GameObject("ReplayBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            replayBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform rbRt = replayBtnObj.GetComponent<RectTransform>();
            rbRt.anchoredPosition = new Vector2(-115f, -115f);
            rbRt.sizeDelta = new Vector2(190f, 54f);

            Image rbImg = replayBtnObj.GetComponent<Image>();
            rbImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            rbImg.type = Image.Type.Sliced;
            rbImg.color = new Color32(0x4A, 0x69, 0xBD, 0xFF);

            Button rBtn = replayBtnObj.GetComponent<Button>();
            rBtn.onClick.AddListener(() =>
            {
                victoryModalObj.SetActive(false);
                ResetGame();
                if (ChilbiFlowController.Instance != null) ChilbiFlowController.Instance.ResetParade();
                if (GameUIController.Instance != null)
                {
                    GameUIController.Instance.PlayCheerSound();
                    GameUIController.Instance.TriggerConfettiBurst(60);
                }
                UpdateStatusHUD();
            });

            GameObject rTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            rTextObj.transform.SetParent(replayBtnObj.transform, false);
            RectTransform rbtRt = rTextObj.GetComponent<RectTransform>();
            rbtRt.anchorMin = Vector2.zero;
            rbtRt.anchorMax = Vector2.one;
            rbtRt.sizeDelta = Vector2.zero;
            Text rText = rTextObj.GetComponent<Text>();
            rText.font = GameUIController.DefaultFont;
            rText.fontSize = 20;
            rText.fontStyle = FontStyle.Bold;
            rText.alignment = TextAnchor.MiddleCenter;
            rText.color = Color.white;
            rText.text = "↺ REPLAY";

            // Next Level Button (Right)
            GameObject nextBtnObj = new GameObject("NextLevelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            nextBtnObj.transform.SetParent(cardObj.transform, false);
            RectTransform nbRt = nextBtnObj.GetComponent<RectTransform>();
            nbRt.anchoredPosition = new Vector2(115f, -115f);
            nbRt.sizeDelta = new Vector2(210f, 54f);

            Image nbImg = nextBtnObj.GetComponent<Image>();
            nbImg.sprite = GameUIController.CreatePillSprite(64, 32, 16);
            nbImg.type = Image.Type.Sliced;
            nbImg.color = new Color32(0x27, 0xAE, 0x60, 0xFF);

            Button nBtn = nextBtnObj.GetComponent<Button>();
            nBtn.onClick.AddListener(() =>
            {
                currentLevel++;
                PlayerPrefs.SetInt("Roadways_Level", currentLevel);
                PlayerPrefs.Save();
                victoryModalObj.SetActive(false);
                ResetGame();
                if (ChilbiFlowController.Instance != null)
                {
                    ChilbiFlowController.Instance.SetLevel(currentLevel);
                    ChilbiFlowController.Instance.ResetParade();
                }
                if (GameUIController.Instance != null)
                {
                    GameUIController.Instance.PlayCheerSound();
                    GameUIController.Instance.TriggerConfettiBurst(80);
                }
                if (currentLevel == 3) ShowFloatingTip("🎉 MILESTONE: Bay 6 Unlocked!");
                else if (currentLevel == 5) ShowFloatingTip("🎉 MILESTONE: Bay 7 Unlocked!");
                else if (currentLevel == 8) ShowFloatingTip("🎉 MILESTONE: Bay 8 Unlocked!");
                UpdateStatusHUD();
            });

            GameObject nTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            nTextObj.transform.SetParent(nextBtnObj.transform, false);
            RectTransform nbtRt = nTextObj.GetComponent<RectTransform>();
            nbtRt.anchorMin = Vector2.zero;
            nbtRt.anchorMax = Vector2.one;
            nbtRt.sizeDelta = Vector2.zero;
            Text nText = nTextObj.GetComponent<Text>();
            nText.font = GameUIController.DefaultFont;
            nText.fontSize = 20;
            nText.fontStyle = FontStyle.Bold;
            nText.alignment = TextAnchor.MiddleCenter;
            nText.color = Color.white;
            nText.text = "NEXT LEVEL ➔";

            // Replenish booster inventory (+1 each, up to 5 max)
            recallCount = Mathf.Min(recallCount + 1, 5);
            shuffleCount = Mathf.Min(shuffleCount + 1, 5);
            PlayerPrefs.SetInt("Roadways_RecallCount", recallCount);
            PlayerPrefs.SetInt("Roadways_ShuffleCount", shuffleCount);
            PlayerPrefs.Save();
            if (GameUIController.Instance != null) GameUIController.Instance.UpdateBoosterLabels();

            // Trigger celebration
            if (GameUIController.Instance != null)
            {
                GameUIController.Instance.TriggerConfettiBurst(80);
                GameUIController.Instance.PlayCheerSound();
            }
            AndroidHaptics.HeavyThud();
        }

        public void RecallLastBus()
        {
            if (isBoardingActive || isInFlight) return;

            if (recallCount <= 0)
            {
                AndroidHaptics.DoubleBuzz();
                if (warningClip != null && audioSource != null && SfxEnabled) audioSource.PlayOneShot(warningClip, 0.7f);
                Vector3 centerPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, 0f);
                StartCoroutine(FloatingScoreRoutine(centerPos, "No Recalls Left!", new Color(1f, 0.4f, 0.4f)));
                return;
            }

            int targetBay = -1;
            if (lastDispatchedBayIndex >= 0 && bayOccupants[lastDispatchedBayIndex] >= 0)
            {
                targetBay = lastDispatchedBayIndex;
            }

            if (targetBay < 0)
            {
                for (int i = 8; i >= 0; i--)
                {
                    if (bayOccupants[i] >= 0)
                    {
                        targetBay = i;
                        break;
                    }
                }
            }

            if (targetBay >= 0)
            {
                recallCount--;
                PlayerPrefs.SetInt("Roadways_RecallCount", recallCount);
                PlayerPrefs.Save();
                if (GameUIController.Instance != null) GameUIController.Instance.UpdateBoosterLabels();

                int busIdx = bayOccupants[targetBay];
                StartCoroutine(ReturnToDepotRoutine(busIdx, targetBay));
                Vector3 bayPos = parkedBusObjects[targetBay] != null ? parkedBusObjects[targetBay].transform.position : new Vector3(Screen.width * 0.5f, Screen.height * 0.4f, 0f);
                StartCoroutine(FloatingScoreRoutine(bayPos + new Vector3(0f, 40f, 0f), $"↺ RECALLED ({recallCount} left)", new Color(0.3f, 0.7f, 1f)));
                AndroidHaptics.LightClick();
            }
            else
            {
                AndroidHaptics.DoubleBuzz();
                if (warningClip != null && audioSource != null && SfxEnabled) audioSource.PlayOneShot(warningClip, 0.7f);
            }
        }

        public void ShuffleParade()
        {
            if (isBoardingActive || isInFlight) return;

            if (shuffleCount <= 0)
            {
                AndroidHaptics.DoubleBuzz();
                if (warningClip != null && audioSource != null && SfxEnabled) audioSource.PlayOneShot(warningClip, 0.7f);
                Vector3 centerPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, 0f);
                StartCoroutine(FloatingScoreRoutine(centerPos, "No Shuffles Left!", new Color(1f, 0.4f, 0.4f)));
                return;
            }

            if (ChilbiFlowController.Instance != null)
            {
                shuffleCount--;
                PlayerPrefs.SetInt("Roadways_ShuffleCount", shuffleCount);
                PlayerPrefs.Save();
                if (GameUIController.Instance != null) GameUIController.Instance.UpdateBoosterLabels();

                ChilbiFlowController.Instance.ShuffleRows();
                AndroidHaptics.MediumClick();
                if (audioSource != null && hornClips != null && hornClips.Length > 0 && SfxEnabled)
                {
                    audioSource.PlayOneShot(hornClips[UnityEngine.Random.Range(0, hornClips.Length)], 0.7f);
                }
                Vector3 centerPos = new Vector3(Screen.width * 0.5f, Screen.height * 0.55f, 0f);
                StartCoroutine(FloatingScoreRoutine(centerPos, $"🔀 SHUFFLED! ({shuffleCount} left)", new Color(0.85f, 0.45f, 1f)));
                UpdateStatusHUD();
            }
        }

        public void UpdateStatusHUD()
        {
            FindStatusText();
            if (statusText == null) return;

            int openBays = 0;
            for (int i = 0; i < 9; i++)
            {
                if (bayUnlocked[i] && bayOccupants[i] == -1) openBays++;
            }

            if (totalClearedChilbis >= LevelTargetChilbis)
            {
                statusText.text = $"🏆 Level {currentLevel} Complete! Victory!";
                return;
            }

            if (isBoardingActive)
            {
                statusText.text = $"⚡ Boarding... ({totalClearedChilbis} Cleared)";
                SetGridlockWarning(false);
                return;
            }

            if (openBays == 0)
            {
                int frontRow = ChilbiFlowController.Instance != null ? ChilbiFlowController.Instance.GetFrontRowIndex() : -1;
                int frontColor = frontRow >= 0 ? ChilbiFlowController.Instance.GetRowColorTheme(frontRow) : -1;
                bool hasMatch = false;
                for (int b = 0; b < 9; b++)
                {
                    if (bayOccupants[b] == frontColor) { hasMatch = true; break; }
                }

                if (!hasMatch)
                {
                    statusText.text = $"⚠️ Gridlock! Tap bus to recall";
                    SetGridlockWarning(true);
                    return;
                }
            }

            SetGridlockWarning(false);
            statusText.text = $"⭐ Lvl {currentLevel} • 🌸 {totalClearedChilbis}/{LevelTargetChilbis} • 🅿️ {openBays} Free • {currentScore} pts";
            if (GameUIController.Instance != null)
            {
                GameUIController.Instance.UpdateTitle($"⭐ LEVEL {currentLevel}  •  👑 HIGH: {highScore}");
            }
        }

        public void ResetGame()
        {
            StopAllCoroutines();
            isBoardingActive = false;
            isInFlight = false;
            idleTimer = 0f;
            HideFTUEPointer();

            for (int i = 0; i < 9; i++)
            {
                if (parkedBusObjects[i] != null)
                {
                    Destroy(parkedBusObjects[i]);
                    parkedBusObjects[i] = null;
                }
                bayOccupants[i] = -1;
            }

            for (int i = 0; i < depotBusRts.Count; i++)
            {
                if (depotBusRts[i] != null)
                {
                    depotBusRts[i].gameObject.SetActive(true);
                    depotBusRts[i].localScale = Vector3.one;
                }
            }

            totalClearedChilbis = 0;
            currentScore = 0;
            comboStreak = 0;
            if (victoryModalObj != null) victoryModalObj.SetActive(false);
            SetGridlockWarning(false);
            RefreshBayUnlocks();

            AndroidHaptics.DoubleBuzz();
            UpdateStatusHUD();
        }
        #endregion
    }

    public static class AndroidHaptics
    {
        public static void Vibrate(long milliseconds = 20)
        {
            if (!BusFleetUIController.HapticsEnabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                Handheld.Vibrate();
            }
            catch (System.Exception) {}
#endif
        }

        public static void LightClick() => Vibrate(12);
        public static void MediumClick() => Vibrate(24);
        public static void HeavyThud() => Vibrate(48);
        public static void DoubleBuzz() => Vibrate(75);
    }
}
