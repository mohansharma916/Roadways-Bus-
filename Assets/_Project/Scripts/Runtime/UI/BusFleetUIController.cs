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

        private AudioSource audioSource;
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

        private void Awake()
        {
            Instance = this;
            EnsureAudioSource();
            EnsureAssets();
            for (int i = 0; i < 9; i++) bayOccupants[i] = -1;
        }

        private void OnEnable()
        {
            Instance = this;
        }

        private void Update()
        {
            if (isBoardingActive || isInFlight) return;
            if (ChilbiFlowController.Instance == null) return;

            CheckBoarding();
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

            PlayBoardingPop();
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
            GameObject badgeObj = new GameObject("PassengerBadge", typeof(RectTransform), typeof(Text));
            badgeObj.transform.SetParent(travelerObj.transform, false);
            RectTransform bRt = badgeObj.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 0.5f);
            bRt.anchorMax = new Vector2(0.5f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.anchoredPosition = Vector2.zero;
            bRt.sizeDelta = new Vector2(60f, 30f);
            Text badgeText = badgeObj.GetComponent<Text>();
            badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            badgeText.fontSize = 20;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = Color.white;
            badgeText.text = "";
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

            parkedBusObjects[bayIndex] = travelerObj;
            isInFlight = false;
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

            // 2. Accelerate forward out of the bay along its tilted longitudinal axis
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

            totalClearedChilbis += 7;
            AndroidHaptics.HeavyThud();
            UpdateStatusHUD();

            // Re-enable depot bus after departure
            if (busIdx >= 0 && busIdx < depotBusRts.Count && depotBusRts[busIdx] != null)
            {
                depotBusRts[busIdx].gameObject.SetActive(true);
            }

            OnBusLeftBay?.Invoke(bayIndex, busIdx);
        }

        public void PlayBoardingPop()
        {
            if (popClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(popClip, 0.6f);
            }
        }

        public void UpdatePassengerCount(int bayIndex, int count, int max = 7)
        {
            GameObject busObj = parkedBusObjects[bayIndex];
            if (busObj == null) return;

            Transform badgeTransform = busObj.transform.Find("PassengerBadge");
            if (badgeTransform != null)
            {
                badgeTransform.gameObject.SetActive(true);
                Text t = badgeTransform.GetComponent<Text>();
                if (t != null) t.text = $"{count}/{max}";
            }
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
        #endregion

        #region Bay Unlocking & Status HUD
        [System.NonSerialized] private int totalClearedChilbis = 0;
        [System.NonSerialized] private Text statusText;

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

            bayUnlocked[bayIndex] = true;

            Image img = bayObj.GetComponent<Image>();
            if (img != null) img.sprite = standardSprite;

            Transform plus = bayObj.transform.Find("PlusText");
            if (plus != null) plus.gameObject.SetActive(false);

            if (audioSource != null && hornClips != null && hornClips.Length > 7 && hornClips[7] != null)
            {
                audioSource.PlayOneShot(hornClips[7], 0.75f);
            }
            AndroidHaptics.MediumClick();

            UpdateStatusHUD();
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

            if (totalClearedChilbis >= 56)
            {
                statusText.text = $"🏆 All Chilbis Boarded! Victory!";
                GameUIController gui = FindAnyObjectByType<GameUIController>();
                if (gui != null)
                {
                    gui.TriggerConfettiBurst(80);
                    gui.PlayCheerSound();
                }
                AndroidHaptics.HeavyThud();
                return;
            }

            if (isBoardingActive)
            {
                statusText.text = $"⚡ Boarding... ({totalClearedChilbis} Cleared)";
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
                    return;
                }
            }

            statusText.text = $"🌸 {totalClearedChilbis} Cleared • {openBays} Bays Free";
        }

        public void ResetGame()
        {
            StopAllCoroutines();
            isBoardingActive = false;
            isInFlight = false;

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
            AndroidHaptics.DoubleBuzz();
            UpdateStatusHUD();
        }
        #endregion
    }

    public static class AndroidHaptics
    {
        public static void Vibrate(long milliseconds = 20)
        {
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
