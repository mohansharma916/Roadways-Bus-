using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RoadwaysGame
{
    /// <summary>
    /// Master UI Controller for the 8 Top-Down Buses in the bottom panel:
    /// - 100% Parity with 3D_Showcase_Preview.html:
    ///   * 8 Matte top-down vehicles (Red, Yellow, Blue, Green, Purple, Orange, Pink, Cyan)
    ///   * Sized matching trapezium bay proportions (aspect 1:2)
    ///   * Tinted windshield, 8 side windows, rear window, yellow hood line, and forward white roof arrow
    ///   * Harmonic bus horn synthesis (392 Hz to 740 Hz)
    ///   * Touch / click dispatch into Trapezium bay with Cubic Hermite velocity-tangent steering
    ///   * Head-first bay entry, suspension settle, and roof arrow fade-out on dock
    ///   * Reverse return maneuver when tapped in bay
    /// </summary>
    [ExecuteAlways]
    public class BusFleetUIController : MonoBehaviour
    {
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
        [SerializeField] private float topPadding = 90f; // offset below bottom panel white border

        private static Sprite[] busSprites;
        private static Sprite arrowSprite;
        private static AudioClip[] hornClips;
        private static AudioClip warningClip;

        private AudioSource audioSource;
        private RectTransform canvasRt;
        private RectTransform targetBayRt;
        private GameObject dispatchedBus;
        private int activeOccupantIndex = -1;
        private bool isInFlight = false;

        private List<RectTransform> depotBusRts = new List<RectTransform>();

        private void Awake()
        {
            EnsureAudioSource();
            EnsureAssets();
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
        }

        private void OnBusClicked(int index)
        {
            if (isInFlight) return;

            // If another bus is already docked in bay, reject with warning shake and horn
            if (activeOccupantIndex != -1)
            {
                StartCoroutine(RejectShake(depotBusRts[index]));
                if (warningClip != null && audioSource != null) audioSource.PlayOneShot(warningClip, 0.7f);
                return;
            }

            // Dispatch bus to target bay
            StartCoroutine(DispatchRoutine(index));
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

        private IEnumerator DispatchRoutine(int index)
        {
            isInFlight = true;
            BusDefinition def = Fleet[index];

            // Play Harmonic Horn
            if (hornClips != null && hornClips.Length > index && hornClips[index] != null && audioSource != null)
            {
                audioSource.PlayOneShot(hornClips[index], 0.85f);
            }

            // Find target bay (Box_0 in TrapeziumBoxesContainer)
            if (targetBayRt == null)
            {
                GameObject bay0 = GameObject.Find("Box_0");
                if (bay0 != null) targetBayRt = bay0.GetComponent<RectTransform>();
            }

            if (targetBayRt == null || canvasRt == null)
            {
                isInFlight = false;
                yield break;
            }

            RectTransform origDepotRt = depotBusRts[index];
            Vector3 startWorldPos = origDepotRt.position;

            // Hide original depot bus
            origDepotRt.gameObject.SetActive(false);

            // Create traveler bus directly under Canvas for free flight
            GameObject travelerObj = new GameObject($"Traveler_{def.id}", typeof(RectTransform), typeof(Image), typeof(Button));
            travelerObj.transform.SetParent(canvasRt.transform, false);
            RectTransform trRt = travelerObj.GetComponent<RectTransform>();
            trRt.sizeDelta = busSize;
            trRt.position = startWorldPos;
            trRt.localEulerAngles = Vector3.zero;

            Image trImg = travelerObj.GetComponent<Image>();
            trImg.sprite = busSprites[index];

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

            Vector3 targetWorldPos = targetBayRt.position;
            float targetAngle = targetBayRt.localEulerAngles.z; // 30 degrees

            // Hermite trajectory calculation
            Vector3 p0 = startWorldPos;
            Vector3 p3 = targetWorldPos;
            float dist = Vector3.Distance(p0, p3);

            // Start tangent: straight UP (arrow direction)
            Vector3 m0 = Vector3.up * (dist * 0.9f);
            // End tangent: aligned with bay forward direction (+30 deg: (-sin 30, cos 30))
            float rad = targetAngle * Mathf.Deg2Rad;
            Vector3 bayDir = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);
            Vector3 m1 = bayDir * (dist * 0.85f);

            float duration = 1.35f;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float progress = Mathf.Clamp01(t);

                // Hermite interpolation
                float h00 = (1 + 2 * progress) * (1 - progress) * (1 - progress);
                float h10 = progress * (1 - progress) * (1 - progress);
                float h01 = progress * progress * (3 - 2 * progress);
                float h11 = progress * progress * (progress - 1);

                Vector3 curPos = h00 * p0 + h10 * m0 + h01 * p3 + h11 * m1;
                trRt.position = curPos;

                // Dynamic rotation: interpolate from 0 to 30 deg with velocity alignment
                float curAngle = Mathf.Lerp(0f, targetAngle, Mathf.SmoothStep(0f, 1f, progress));
                trRt.localEulerAngles = new Vector3(0f, 0f, curAngle);

                // Subtle startup squash & stretch
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
            float fadeDur = 0.28f;
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

            dispatchedBus = travelerObj;
            activeOccupantIndex = index;
            isInFlight = false;

            // Enable tapping parked bus to return to depot
            Button trBtn = travelerObj.GetComponent<Button>();
            trBtn.transition = Selectable.Transition.None;
            trBtn.onClick.AddListener(() => StartCoroutine(ReturnToDepotRoutine(index)));
        }

        private IEnumerator ReturnToDepotRoutine(int index)
        {
            if (isInFlight || dispatchedBus == null) yield break;
            isInFlight = true;

            GameObject travelerObj = dispatchedBus;
            RectTransform trRt = travelerObj.GetComponent<RectTransform>();
            RectTransform origDepotRt = depotBusRts[index];

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

            float duration = 0.9f;
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
            dispatchedBus = null;
            activeOccupantIndex = -1;
            isInFlight = false;
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

            if (arrowSprite == null)
            {
                arrowSprite = CreateArrowSprite(128, 256);
            }

            if (hornClips == null || hornClips.Length != Fleet.Length)
            {
                hornClips = new AudioClip[Fleet.Length];
                for (int i = 0; i < Fleet.Length; i++)
                {
                    hornClips[i] = GenerateHarmonicHorn(Fleet[i].hornFreq, 0.32f);
                }
                warningClip = GenerateWarningHorn();
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
                    // 1. Rounded rectangle SDF for main body
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

                    // Front windshield (top of vehicle: Y in [206, 240], X in [16, w - 17])
                    if (y >= 206 && y <= 240 && x >= 16 && x <= w - 17)
                    {
                        float wy = Mathf.Clamp(y, 214f, 232f);
                        float wx = Mathf.Clamp(x, 24f, w - 25f);
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(wx, wy)) <= 8.5f)
                        {
                            cur = tintWindow;
                        }
                    }

                    // Side windows (4 pairs on left and right)
                    int[] sideYs = new int[] { 54, 94, 134, 174 };
                    foreach (int sy in sideYs)
                    {
                        if (y >= sy && y <= sy + 28)
                        {
                            if ((x >= 8 && x <= 18) || (x >= w - 19 && x <= w - 9))
                            {
                                float wy = Mathf.Clamp(y, sy + 3, sy + 25);
                                float wx = (x <= 18) ? Mathf.Clamp(x, 11f, 15f) : Mathf.Clamp(x, w - 16f, w - 12f);
                                if (Vector2.Distance(new Vector2(x, y), new Vector2(wx, wy)) <= 3.5f)
                                {
                                    cur = tintWindow;
                                }
                            }
                        }
                    }

                    // Rear window (bottom: Y in [14, 24], X in [20, w - 21])
                    if (y >= 14 && y <= 24 && x >= 20 && x <= w - 21)
                    {
                        cur = tintWindow;
                    }

                    // Yellow hood line
                    if (hasHoodLine && y >= 194 && y <= 196 && x >= 16 && x <= w - 17)
                    {
                        cur = hoodCol;
                    }

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
            // Arrow pointing UP towards Y=256
            // Tip at (64, 175)
            // Left barb at (34, 135), Right barb at (94, 135)
            // Left inner at (52, 135), Right inner at (76, 135)
            // Stem bottom at Y = 82
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inside = false;

                    // Triangle head
                    if (y >= 135 && y <= 175)
                    {
                        float frac = (175f - y) / 40f; // 0 at tip, 1 at base
                        float halfW = frac * 30f;
                        if (Mathf.Abs(x - 64f) <= halfW) inside = true;
                    }

                    // Stem shaft
                    if (y >= 82 && y <= 135)
                    {
                        if (x >= 52 && x <= 76) inside = true;
                    }

                    // Rounded base
                    if (y >= 74 && y <= 86)
                    {
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(64f, 82f)) <= 12f) inside = true;
                    }

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
                // Attack & Release envelope
                if (t < 0.02f) env = t / 0.02f;
                else if (t > duration - 0.06f) env = (duration - t) / 0.06f;

                // Fundamental + Second Harmonic (sweet chord)
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
                float wave = Mathf.Sin(2f * Mathf.PI * 196f * t); // Low double-buzz G3
                data[i] = wave * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("WarningHorn", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion
    }
}
