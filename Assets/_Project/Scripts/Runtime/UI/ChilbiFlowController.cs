using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoadwaysGame
{
    /// <summary>
    /// Master UI Controller for Chilbi character flow along the ring road circuit.
    /// Parity with 3D_Showcase_Preview.html:
    /// - 7 Chilbis abreast per row spanning the full 112px road width with zero gaps
    /// - Single-color rows cycling across 8 master candy themes
    /// - Constant linear speed along the exact 1936px Catmull-Rom / Bezier circuit loop
    /// - Upright 2.5D side-view posture with independent waddling bounce, tilt, limb swing, and ground shadow
    /// - Facing direction (facingX = dx >= 0 ? 1 : -1)
    /// </summary>
    [ExecuteAlways]
    public class ChilbiFlowController : MonoBehaviour
    {
        [Header("Formation Settings")]
        [SerializeField] private int rowCount = 5; // 5 rows = 35 Chilbis
        [SerializeField] private float walkSpeed = 95f; // px/s on 1080 canvas
        [SerializeField] private float rowSpacing = 36f; // distance between rows along track
        [SerializeField] private float laneSpacing = 12.5f; // spacing between the 7 Chilbis in each row
        [SerializeField] private float baseScale = 1.0f;

        [Header("Animation Dynamics")]
        [SerializeField] private float waddleFrequency = 7.5f; // Hz
        [SerializeField] private float bounceAmount = 3.5f; // vertical bob
        [SerializeField] private float tiltAngle = 5.0f; // degrees
        [SerializeField] private float legSwingAngle = 22.0f; // degrees
        [SerializeField] private float armSwingAngle = 26.0f; // degrees

        public struct ChilbiTheme
        {
            public Color32 body;
            public Color32 limb;
            public Color32 foot;
            public ChilbiTheme(Color32 b, Color32 l, Color32 f) { body = b; limb = l; foot = f; }
        }

        public static readonly ChilbiTheme[] Themes = new ChilbiTheme[]
        {
            new ChilbiTheme(new Color32(0xEE, 0x52, 0x53, 0xFF), new Color32(0xC0, 0x39, 0x2B, 0xFF), new Color32(0x96, 0x11, 0x33, 0xFF)), // Red
            new ChilbiTheme(new Color32(0xFE, 0xD3, 0x30, 0xFF), new Color32(0xF3, 0x9C, 0x12, 0xFF), new Color32(0xD3, 0x54, 0x00, 0xFF)), // Yellow
            new ChilbiTheme(new Color32(0x1E, 0x90, 0xFF, 0xFF), new Color32(0x29, 0x80, 0xB9, 0xFF), new Color32(0x0C, 0x24, 0x61, 0xFF)), // Blue
            new ChilbiTheme(new Color32(0x00, 0xB8, 0x94, 0xFF), new Color32(0x27, 0xAE, 0x60, 0xFF), new Color32(0x11, 0x7A, 0x65, 0xFF)), // Green
            new ChilbiTheme(new Color32(0x88, 0x54, 0xD0, 0xFF), new Color32(0x6C, 0x5C, 0xE7, 0xFF), new Color32(0x34, 0x1F, 0x97, 0xFF)), // Purple
            new ChilbiTheme(new Color32(0xE6, 0x7E, 0x22, 0xFF), new Color32(0xD3, 0x54, 0x00, 0xFF), new Color32(0xB9, 0x4A, 0x00, 0xFF)), // Orange
            new ChilbiTheme(new Color32(0xE8, 0x43, 0x93, 0xFF), new Color32(0xD6, 0x30, 0x31, 0xFF), new Color32(0xA5, 0x1E, 0x37, 0xFF)), // Pink
            new ChilbiTheme(new Color32(0x0F, 0xBC, 0xF9, 0xFF), new Color32(0x0A, 0xBD, 0xE3, 0xFF), new Color32(0x00, 0x62, 0x66, 0xFF))  // Cyan
        };

        private struct BezierSeg
        {
            public Vector2 p0, p1, p2, p3;
            public BezierSeg(Vector2 a, Vector2 b, Vector2 c, Vector2 d) { p0 = a; p1 = b; p2 = c; p3 = d; }
            public void Eval(float t, out float x, out float y, out float dx, out float dy)
            {
                float mt = 1f - t;
                float mt2 = mt * mt;
                float mt3 = mt2 * mt;
                float t2 = t * t;
                float t3 = t2 * t;

                x = mt3 * p0.x + 3f * mt2 * t * p1.x + 3f * mt * t2 * p2.x + t3 * p3.x;
                y = mt3 * p0.y + 3f * mt2 * t * p1.y + 3f * mt * t2 * p2.y + t3 * p3.y;

                dx = 3f * mt2 * (p1.x - p0.x) + 6f * mt * t * (p2.x - p1.x) + 3f * t2 * (p3.x - p2.x);
                dy = 3f * mt2 * (p1.y - p0.y) + 6f * mt * t * (p2.y - p1.y) + 3f * t2 * (p3.y - p2.y);
            }
        }

        private struct Sample
        {
            public float x, y, dx, dy, dist;
        }

        private Sample[] lut;
        private float totalLength;

        private class ChilbiInstance
        {
            public RectTransform root;
            public RectTransform bodyGroup;
            public RectTransform legNear;
            public RectTransform legFar;
            public RectTransform armNear;
            public RectTransform armFar;
            public Image shadowImg;
            public int row;
            public int col;
            public float laneD;
            public float phase;
        }

        private List<ChilbiInstance> instances = new List<ChilbiInstance>();
        private GameObject layerObj;
        private float currentDistance = 0f;
        private float walkCyclePhase = 0f;

        private static Sprite shadowSprite;
        private static Sprite[] bodySprites;
        private static Sprite[] limbSprites;
        private static Sprite[] footSprites;

        private void Awake()
        {
            BuildSplineLut();
            EnsureSprites();
            CreateFormation();
        }

        private void OnEnable()
        {
            if (lut == null || lut.Length == 0) BuildSplineLut();
            if (instances.Count == 0) CreateFormation();
        }

        private void BuildSplineLut()
        {
            BezierSeg[] segments = new BezierSeg[]
            {
                new BezierSeg(new Vector2(540f, 500f), new Vector2(613.3f, 500f), new Vector2(686.7f, 500f), new Vector2(760f, 500f)),
                new BezierSeg(new Vector2(760f, 500f), new Vector2(850f, 500f), new Vector2(900f, 410f), new Vector2(900f, 310f)),
                new BezierSeg(new Vector2(900f, 310f), new Vector2(900f, 210f), new Vector2(820f, 135f), new Vector2(720f, 135f)),
                new BezierSeg(new Vector2(720f, 135f), new Vector2(650f, 135f), new Vector2(600f, 225f), new Vector2(540f, 225f)),
                new BezierSeg(new Vector2(540f, 225f), new Vector2(480f, 225f), new Vector2(430f, 135f), new Vector2(360f, 135f)),
                new BezierSeg(new Vector2(360f, 135f), new Vector2(260f, 135f), new Vector2(180f, 210f), new Vector2(180f, 310f)),
                new BezierSeg(new Vector2(180f, 310f), new Vector2(180f, 410f), new Vector2(230f, 500f), new Vector2(320f, 500f)),
                new BezierSeg(new Vector2(320f, 500f), new Vector2(393.3f, 500f), new Vector2(466.7f, 500f), new Vector2(540f, 500f))
            };

            int nSamples = 1000;
            lut = new Sample[nSamples];
            totalLength = 0f;
            float prevX = 540f, prevY = 500f;

            for (int i = 0; i < nSamples; i++)
            {
                float u = (float)i / nSamples * segments.Length;
                int segIdx = Mathf.FloorToInt(u) % segments.Length;
                float t = u - Mathf.Floor(u);

                segments[segIdx].Eval(t, out float x, out float y, out float dx, out float dy);
                if (i > 0)
                {
                    totalLength += Mathf.Sqrt((x - prevX) * (x - prevX) + (y - prevY) * (y - prevY));
                }
                lut[i] = new Sample { x = x, y = y, dx = dx, dy = dy, dist = totalLength };
                prevX = x;
                prevY = y;
            }

            totalLength += Mathf.Sqrt((lut[0].x - prevX) * (lut[0].x - prevX) + (lut[0].y - prevY) * (lut[0].y - prevY));
        }

        private void SampleTrack(float s, out float x, out float y, out float dx, out float dy)
        {
            s = ((s % totalLength) + totalLength) % totalLength;
            int low = 0, high = lut.Length - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (lut[mid].dist < s) low = mid + 1;
                else high = mid - 1;
            }

            int idx1 = Mathf.Clamp(low, 0, lut.Length - 1);
            int idx0 = (idx1 - 1 + lut.Length) % lut.Length;
            Sample p0 = lut[idx0];
            Sample p1 = lut[idx1];
            float span = p1.dist - p0.dist;
            if (span <= 0f) span = totalLength / lut.Length;
            float frac = Mathf.Clamp01((s - p0.dist) / span);

            x = Mathf.Lerp(p0.x, p1.x, frac);
            y = Mathf.Lerp(p0.y, p1.y, frac);
            dx = Mathf.Lerp(p0.dx, p1.dx, frac);
            dy = Mathf.Lerp(p0.dy, p1.dy, frac);
        }

        private void EnsureSprites()
        {
            if (shadowSprite == null) shadowSprite = CreateShadowSprite(40, 16);

            if (bodySprites == null || bodySprites.Length != Themes.Length)
            {
                bodySprites = new Sprite[Themes.Length];
                limbSprites = new Sprite[Themes.Length];
                footSprites = new Sprite[Themes.Length];
                for (int t = 0; t < Themes.Length; t++)
                {
                    bodySprites[t] = CreateBodySprite(24, 38, Themes[t].body);
                    limbSprites[t] = CreateLimbSprite(8, 16, Themes[t].limb);
                    footSprites[t] = CreateLimbSprite(8, 16, Themes[t].foot);
                }
            }
        }

        public void CreateFormation()
        {
            EnsureSprites();

            // Clear previous layer if exists
            Transform existing = transform.Find("ChilbiLayer");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            instances.Clear();

            layerObj = new GameObject("ChilbiLayer", typeof(RectTransform));
            layerObj.transform.SetParent(transform, false);
            RectTransform lRt = layerObj.GetComponent<RectTransform>();
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.pivot = new Vector2(0.5f, 0f);
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = Vector2.zero;

            int colsPerRow = 7;
            for (int r = 0; r < rowCount; r++)
            {
                int themeIdx = r % Themes.Length;
                Sprite bodySp = bodySprites[themeIdx];
                Sprite limbSp = limbSprites[themeIdx];
                Sprite footSp = footSprites[themeIdx];

                for (int c = 0; c < colsPerRow; c++)
                {
                    float laneD = (c - 3) * laneSpacing; // 7 lanes: -37.5 to +37.5
                    float phase = r * 0.38f + c * 0.45f;

                    GameObject rootObj = new GameObject($"Chilbi_r{r}_c{c}", typeof(RectTransform));
                    rootObj.transform.SetParent(layerObj.transform, false);
                    RectTransform rootRt = rootObj.GetComponent<RectTransform>();
                    rootRt.pivot = new Vector2(0.5f, 0.2f);
                    rootRt.sizeDelta = new Vector2(32f, 44f);

                    // 1. Ground Shadow
                    GameObject shadowObj = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
                    shadowObj.transform.SetParent(rootObj.transform, false);
                    RectTransform sRt = shadowObj.GetComponent<RectTransform>();
                    sRt.anchoredPosition = new Vector2(0f, -4f);
                    sRt.sizeDelta = new Vector2(28f, 10f);
                    Image sImg = shadowObj.GetComponent<Image>();
                    sImg.sprite = shadowSprite;
                    sImg.raycastTarget = false;

                    // 2. Body Group (Bobs and Tilts)
                    GameObject bodyGroupObj = new GameObject("BodyGroup", typeof(RectTransform));
                    bodyGroupObj.transform.SetParent(rootObj.transform, false);
                    RectTransform bgRt = bodyGroupObj.GetComponent<RectTransform>();
                    bgRt.anchoredPosition = Vector2.zero;
                    bgRt.sizeDelta = new Vector2(32f, 44f);

                    // Far Arm (Connected at shoulder 3.5, 24)
                    GameObject faObj = new GameObject("FarArm", typeof(RectTransform), typeof(Image));
                    faObj.transform.SetParent(bodyGroupObj.transform, false);
                    RectTransform faRt = faObj.GetComponent<RectTransform>();
                    faRt.pivot = new Vector2(0.5f, 0.85f);
                    faRt.anchoredPosition = new Vector2(4f, 22f);
                    faRt.sizeDelta = new Vector2(6.5f, 14f);
                    Image faImg = faObj.GetComponent<Image>();
                    faImg.sprite = limbSp;
                    faImg.raycastTarget = false;

                    // Far Leg (Connected at hip 3, 10)
                    GameObject flObj = new GameObject("FarLeg", typeof(RectTransform), typeof(Image));
                    flObj.transform.SetParent(bodyGroupObj.transform, false);
                    RectTransform flRt = flObj.GetComponent<RectTransform>();
                    flRt.pivot = new Vector2(0.5f, 0.85f);
                    flRt.anchoredPosition = new Vector2(3f, 8f);
                    flRt.sizeDelta = new Vector2(6.5f, 13f);
                    Image flImg = flObj.GetComponent<Image>();
                    flImg.sprite = footSp;
                    flImg.raycastTarget = false;

                    // Candy Bean Body (Torso + Head in one seamless shape)
                    GameObject bodyObj = new GameObject("BeanBody", typeof(RectTransform), typeof(Image));
                    bodyObj.transform.SetParent(bodyGroupObj.transform, false);
                    RectTransform bRt = bodyObj.GetComponent<RectTransform>();
                    bRt.anchoredPosition = new Vector2(0f, 18f);
                    bRt.sizeDelta = new Vector2(20f, 32f);
                    Image bImg = bodyObj.GetComponent<Image>();
                    bImg.sprite = bodySp;
                    bImg.raycastTarget = false;

                    // Near Leg (Connected at hip -3, 10)
                    GameObject nlObj = new GameObject("NearLeg", typeof(RectTransform), typeof(Image));
                    nlObj.transform.SetParent(bodyGroupObj.transform, false);
                    RectTransform nlRt = nlObj.GetComponent<RectTransform>();
                    nlRt.pivot = new Vector2(0.5f, 0.85f);
                    nlRt.anchoredPosition = new Vector2(-3f, 8f);
                    nlRt.sizeDelta = new Vector2(6.5f, 13f);
                    Image nlImg = nlObj.GetComponent<Image>();
                    nlImg.sprite = footSp;
                    nlImg.raycastTarget = false;

                    // Near Arm (Connected at shoulder -3.5, 24)
                    GameObject naObj = new GameObject("NearArm", typeof(RectTransform), typeof(Image));
                    naObj.transform.SetParent(bodyGroupObj.transform, false);
                    RectTransform naRt = naObj.GetComponent<RectTransform>();
                    naRt.pivot = new Vector2(0.5f, 0.85f);
                    naRt.anchoredPosition = new Vector2(-4f, 22f);
                    naRt.sizeDelta = new Vector2(6.5f, 14f);
                    Image naImg = naObj.GetComponent<Image>();
                    naImg.sprite = limbSp;
                    naImg.raycastTarget = false;

                    instances.Add(new ChilbiInstance
                    {
                        root = rootRt,
                        bodyGroup = bgRt,
                        legNear = nlRt,
                        legFar = flRt,
                        armNear = naRt,
                        armFar = faRt,
                        shadowImg = sImg,
                        row = r,
                        col = c,
                        laneD = laneD,
                        phase = phase
                    });
                }
            }
        }

        private void Update()
        {
            if (instances.Count == 0) return;

            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            currentDistance += walkSpeed * dt;
            walkCyclePhase += dt * waddleFrequency * Mathf.PI * 2f;

            for (int i = 0; i < instances.Count; i++)
            {
                ChilbiInstance w = instances[i];
                float s = currentDistance - (w.row * rowSpacing);
                SampleTrack(s, out float x, out float y, out float dx, out float dy);

                float cLen = Mathf.Sqrt(dx * dx + dy * dy);
                if (cLen < 0.001f) cLen = 1f;

                // SVG tangent (dx, dy). In Unity RectTransform:
                // X matches SVG (0 is center, offset 540)
                // Y is inverted (Unity Y up = 660 - SVG y)
                // Tangent in Unity: (dx, -dy). Perpendicular normal to the left: (dy, dx).
                float nx = dy / cLen;
                float ny = dx / cLen;

                float px = (x - 540f) + (w.laneD * nx);
                float py = (660f - y) + (w.laneD * ny);

                float facingX = dx >= 0f ? 1f : -1f;

                w.root.anchoredPosition = new Vector2(px, py);
                w.root.localScale = new Vector3(facingX * baseScale, baseScale, 1f);

                // Walking Waddle Animation
                float wPhase = walkCyclePhase + w.phase;
                float bounce = Mathf.Abs(Mathf.Sin(wPhase)) * bounceAmount;
                float tilt = Mathf.Sin(wPhase) * tiltAngle;
                float legAngle = Mathf.Sin(wPhase) * legSwingAngle;
                float armAngle = Mathf.Sin(wPhase) * armSwingAngle;

                w.bodyGroup.anchoredPosition = new Vector2(0f, bounce);
                w.bodyGroup.localEulerAngles = new Vector3(0f, 0f, tilt);

                w.legNear.localEulerAngles = new Vector3(0f, 0f, legAngle);
                w.legFar.localEulerAngles = new Vector3(0f, 0f, -legAngle);

                w.armNear.localEulerAngles = new Vector3(0f, 0f, -armAngle);
                w.armFar.localEulerAngles = new Vector3(0f, 0f, armAngle);
            }
        }

        #region Procedural Sprite Generation
        private static Sprite CreateShadowSprite(int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = "Gen_Chilbi_Shadow";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * height];
            float rx = (width - 1) * 0.5f;
            float ry = (height - 1) * 0.5f;
            float cx = rx, cy = ry;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x - cx) / rx;
                    float ny = (y - cy) / ry;
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * width + x] = new Color32(15, 20, 35, (byte)(a * 90));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateBodySprite(int width, int height, Color32 color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = $"Gen_Chilbi_Body_{color.r}_{color.g}_{color.b}";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * height];
            float r = (width - 1) * 0.5f;
            float cx = r;
            float cyBot = r;
            float cyTop = height - 1 - r;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cy = (y < cyBot) ? cyBot : (y > cyTop ? cyTop : y);
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);

                    if (a <= 0.001f)
                    {
                        pixels[y * width + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Vertical candy gradient (slightly brighter on head)
                    float tY = (float)y / height;
                    byte cr = (byte)Mathf.Clamp(color.r + (tY * 30 - 15), 0, 255);
                    byte cg = (byte)Mathf.Clamp(color.g + (tY * 30 - 15), 0, 255);
                    byte cb = (byte)Mathf.Clamp(color.b + (tY * 30 - 15), 0, 255);

                    // Gloss highlight on head (x: 4..11, y: 26..34)
                    float hx = (x - 7f) / 3.5f;
                    float hy = (y - 30f) / 2.5f;
                    float hd = Mathf.Sqrt(hx * hx + hy * hy);
                    if (hd < 1.0f)
                    {
                        float hAlpha = (1f - hd) * 0.65f;
                        cr = (byte)Mathf.Lerp(cr, 255, hAlpha);
                        cg = (byte)Mathf.Lerp(cg, 255, hAlpha);
                        cb = (byte)Mathf.Lerp(cb, 255, hAlpha);
                    }

                    // Cute profile eye (x: 17..20, y: 22..27)
                    float ex = (x - 18.5f) / 1.5f;
                    float ey = (y - 24.5f) / 2.5f;
                    float ed = Mathf.Sqrt(ex * ex + ey * ey);
                    if (ed < 1.0f)
                    {
                        // Twinkle pupil at top right
                        float tx = x - 19f;
                        float ty = y - 26f;
                        if (tx * tx + ty * ty < 0.8f)
                        {
                            cr = cg = cb = 255;
                        }
                        else
                        {
                            cr = cg = cb = 30;
                        }
                    }

                    pixels[y * width + x] = new Color32(cr, cg, cb, (byte)(a * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateLimbSprite(int width, int height, Color32 color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.name = $"Gen_Chilbi_Limb_{color.r}_{color.g}_{color.b}";
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = new Color32[width * height];
            float r = (width - 1) * 0.5f;
            float cx = r;
            float cyBot = r;
            float cyTop = height - 1 - r;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float cy = (y < cyBot) ? cyBot : (y > cyTop ? cyTop : y);
                    float dx = x - cx;
                    float dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);

                    pixels[y * width + x] = new Color32(color.r, color.g, color.b, (byte)(a * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
        #endregion
    }
}
