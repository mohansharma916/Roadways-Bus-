using System.Collections;
using System.Collections.Generic;
using RoadwaysGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Vertical slice: one Core.Session per level, rendered as a 3D beach scene. Owns input, routing,
    /// boarding pace, camera framing and a minimal HUD. All rules live in RoadwaysGame.Core.
    /// </summary>
    public sealed class SliceGame : MonoBehaviour
    {
        private const float CameraPitch = 64f, CameraFov = 34f; // near top-down, like the reference; portrait fit is width-limited
        private const float TapMargin = 0.22f;        // world units added around each vehicle's footprint for touch
        private const float BoardInterval = 0.085f;   // seconds between passengers leaving a queue
        private const float DriveSpeed = 8f, DepartSpeed = 7f;

        private Camera cam;
        private Transform level;
        private Session session;
        private SliceVehicle[] vehicles;
        private SliceVehicle[] docked;
        private SliceCrowd crowd;
        private SlicePath[] queuePaths;
        private int levelIndex, walking;
        private float boardTimer;
        private bool ended;
        private Vector2Int framedFor;

        private Font font;
        private RectTransform safeRoot;
        private Text levelLabel, bannerTitle, bannerButtonLabel;
        private GameObject banner;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private void Start()
        {
            Application.targetFrameRate = 60; // the editor otherwise runs uncapped; 60 is also the device target (NFR-1)
            cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.fieldOfView = CameraFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x8F, 0xD9, 0xEE, 0xFF);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;

            queuePaths = SliceWorld.QueuePaths();
            SliceWorld.BuildEnvironment(transform, queuePaths);
            BuildHud();
            LoadLevel(0);
        }

        private void LoadLevel(int index)
        {
            StopAllCoroutines();
            if (level != null) Destroy(level.gameObject);
            levelIndex = index;
            ended = false;
            walking = 0;
            boardTimer = 0f;

            session = new Session(SliceLevels.Get(index));
            docked = new SliceVehicle[session.Level.BayCount];
            level = new GameObject("Level").transform;
            level.SetParent(transform, false);
            vehicles = new SliceVehicle[session.Level.Vehicles.Length];
            for (int i = 0; i < vehicles.Length; i++) vehicles[i] = SliceVehicle.Create(session.Level.Vehicles[i], i, level);
            crowd = new GameObject("Crowd").AddComponent<SliceCrowd>();
            crowd.transform.SetParent(level, false);
            crowd.Build(session, queuePaths);

            levelLabel.text = "Level " + (index + 1);
            banner.SetActive(false);
            Frame(Screen.width, Screen.height, Screen.safeArea);
        }

        /// <summary>Re-frames for an explicit output size (editor captures render off-screen at the phone's resolution).
        /// The next Update re-frames for the real screen.</summary>
        public void FrameFor(int width, int height)
        {
            cam.aspect = width / (float)height;
            Frame(width, height, new Rect(0f, 0f, width, height));
            cam.ResetAspect();
            framedFor = new Vector2Int(-1, -1);
        }

        private void Update()
        {
            if (Screen.width != framedFor.x || Screen.height != framedFor.y) Frame(Screen.width, Screen.height, Screen.safeArea);
            if (!ended) HandleTap();
            PaceBoarding();
            CheckEnd();
        }

        // ---------------------------------------------------------------- input

        private void HandleTap()
        {
            Vector2 pos;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) pos = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) pos = Mouse.current.position.ReadValue();
            else return;
            TapAt(pos);
        }

        private void TapAt(Vector2 pos)
        {
            if (IsOverUi(pos)) return; // never let a HUD tap fall through to a vehicle behind it

            int i = PickVehicle(pos);
            if (i < 0 || vehicles[i].Busy) return;
            SliceVehicle v = vehicles[i];
            switch (session.TryDispatch(i, out int detail))
            {
                case DispatchResult.Ok:
                    int bay = detail;
                    StartCoroutine(v.Drive(RouteToBay(v, bay), DriveSpeed, SliceWorld.SlotRotation, () =>
                    {
                        session.MarkArrived(bay);
                        docked[bay] = v;
                        v.Dock(v.Def.Capacity);
                    }));
                    break;
                case DispatchResult.Blocked:
                    StartCoroutine(v.Nudge(GapTo(i, detail)));
                    StartCoroutine(vehicles[detail].Flinch());
                    break;
                case DispatchResult.NoFreeBay:
                    StartCoroutine(v.Nudge(0.12f));
                    break;
            }
        }

        private bool IsOverUi(Vector2 screen)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, uiHits);
            return uiHits.Count > 0;
        }

        private int PickVehicle(Vector2 screen)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, new Vector3(0f, 0.3f, 0f)).Raycast(ray, out float enter)) return -1;
            Vector3 hit = ray.GetPoint(enter);
            var p = new System.Numerics.Vector2(hit.x, hit.z);
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < vehicles.Length; i++)
            {
                if (!session.IsPresent(i)) continue;
                VehicleDef d = session.Level.Vehicles[i];
                if (!d.Footprint().Contains(p, TapMargin)) continue;
                float dist = System.Numerics.Vector2.Distance(d.Position, p);
                if (dist < bestDist) { bestDist = dist; best = i; }
            }
            return best;
        }

        /// <summary>Free space between a vehicle's nose and the blocker, so the bump visibly stops at it.</summary>
        private float GapTo(int i, int blocker)
        {
            VehicleDef v = session.Level.Vehicles[i], b = session.Level.Vehicles[blocker];
            float t = System.Numerics.Vector2.Dot(b.Position - v.Position, v.Forward) - b.Footprint().ExtentAlong(v.Forward) - v.Length * 0.5f;
            return Mathf.Max(0.05f, t);
        }

        // ---------------------------------------------------------------- routing

        private List<Vector3> RouteToBay(SliceVehicle v, int bay)
        {
            LevelDef L = session.Level;
            Vector3 p = v.transform.position, f = v.transform.forward;
            bool upward = f.z > 0.5f;
            // Upward exits only need to clear the depot's top edge; others leave fully before turning onto a side lane.
            Vector3 exit = p + f * ExitDistance(p, f, upward ? 0.45f : v.Def.Length * 0.5f + 0.25f, L);
            Vector3 bayPos = SliceWorld.BayCenter(bay), bayFwd = SliceWorld.SlotRotation * Vector3.forward;
            Vector3 mouth = bayPos - bayFwd * (SliceWorld.BayLength * 0.5f + 0.55f);

            var route = new List<Vector3> { p, exit };
            if (exit.z < L.MaxY + 0.3f)
            {
                float side = Mathf.Abs(exit.x) > 0.3f ? Mathf.Sign(exit.x) : Mathf.Sign(mouth.x + 0.001f);
                route.Add(new Vector3(side * SliceWorld.LaneX, 0f, exit.z));
                route.Add(new Vector3(side * SliceWorld.LaneX, 0f, L.MaxY + 0.55f));
            }
            route.Add(mouth);
            route.Add(bayPos);
            return route;
        }

        private static float ExitDistance(Vector3 p, Vector3 f, float beyond, LevelDef L)
        {
            float best = float.MaxValue;
            if (f.x > 1e-3f) best = Mathf.Min(best, (L.MaxX + beyond - p.x) / f.x);
            if (f.x < -1e-3f) best = Mathf.Min(best, (L.MinX - beyond - p.x) / f.x);
            if (f.z > 1e-3f) best = Mathf.Min(best, (L.MaxY + beyond - p.z) / f.z);
            if (f.z < -1e-3f) best = Mathf.Min(best, (L.MinY - beyond - p.z) / f.z);
            return Mathf.Max(best, 0.5f);
        }

        private static List<Vector3> DepartRoute(int bay)
        {
            Vector3 c = SliceWorld.BayCenter(bay), f = SliceWorld.SlotRotation * Vector3.forward;
            float roadZ = SliceWorld.ApronMaxZ - 0.5f;
            return new List<Vector3> { c, c + f * 1.9f, new Vector3(c.x + 1.6f, 0f, roadZ), new Vector3(14f, 0f, roadZ) };
        }

        // ---------------------------------------------------------------- boarding & end

        private void PaceBoarding()
        {
            boardTimer -= Time.deltaTime;
            if (boardTimer > 0f || !session.StepBoarding(out BoardEvent e)) return;
            boardTimer = BoardInterval;
            SliceVehicle bus = docked[e.Bay];
            walking++;
            crowd.Board(e, bus.DoorPosition, () =>
            {
                walking--;
                bus.AddPassenger();
                if (e.Departed) StartCoroutine(Depart(e.Bay, bus));
            });
        }

        private IEnumerator Depart(int bay, SliceVehicle bus)
        {
            yield return new WaitForSeconds(0.15f);
            yield return bus.Drive(DepartRoute(bay), DepartSpeed, null, null);
            Destroy(bus.gameObject);
            docked[bay] = null;
            session.ReleaseBay(bay);
        }

        private void CheckEnd()
        {
            if (ended || walking > 0) return;
            for (int b = 0; b < session.Level.BayCount; b++)
            {
                BayState st = session.GetBayState(b);
                if (st == BayState.Incoming || st == BayState.Departing) return; // still moving
            }
            if (session.IsWon) ShowBanner(true);
            else if (session.IsGridlocked) ShowBanner(false);
        }

        // ---------------------------------------------------------------- verification hooks (editor bridge)

        public void LoadLevelForTest(int index) => LoadLevel(index);

        /// <summary>Plays the current level with the solver's winning order, through the real screen-tap path.</summary>
        public void StartAutoplay(float pause = 0.15f) => StartCoroutine(Autoplay(pause));

        private IEnumerator Autoplay(float pause)
        {
            int[] order = Solver.Solve(session, out bool exhausted);
            Debug.Log(order == null ? $"[Slice] Autoplay: no solution (exhausted={exhausted})" : "[Slice] Autoplay order: " + string.Join(",", order));
            if (order == null) yield break;
            foreach (int i in order)
            {
                yield return new WaitUntil(Quiescent);
                yield return new WaitForSeconds(pause);
                Vector3 screen = cam.WorldToScreenPoint(vehicles[i].transform.position + Vector3.up * 0.3f);
                TapAt(screen);
                if (session.IsPresent(i)) Debug.LogWarning($"[Slice] Autoplay tap on vehicle {i} did not dispatch it");
            }
        }

        /// <summary>Nothing moving and nobody able to board: the state the solver reasons about.</summary>
        private bool Quiescent()
        {
            if (walking > 0) return false;
            foreach (SliceVehicle v in vehicles) if (v != null && v.Busy) return false;
            for (int b = 0; b < session.Level.BayCount; b++)
            {
                BayState st = session.GetBayState(b);
                if (st == BayState.Incoming || st == BayState.Departing) return false;
            }
            return !session.Clone().StepBoarding(out _);
        }

        // ---------------------------------------------------------------- camera

        /// <summary>Fits the whole board (depot to horseshoe) into the space between the HUD bands, for any aspect ratio.</summary>
        private void Frame(int width, int height, Rect safe)
        {
            framedFor = new Vector2Int(width, height);
            float topMargin = 1f - safe.yMax / height + 0.09f, bottomMargin = safe.yMin / height + 0.03f, sideMargin = 0.02f;

            var b = new Bounds();
            // Top bound stops short of the arm tails so the queue runs off-screen, as in the reference.
            b.SetMinMax(new Vector3(-SliceWorld.FloorHalfWidth, 0f, SliceWorld.FloorMinZ), new Vector3(SliceWorld.FloorHalfWidth, 0.6f, 14.2f));
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);

            Quaternion rot = Quaternion.Euler(CameraPitch, 0f, 0f);
            Vector3 dir = rot * Vector3.forward, target = b.center;
            cam.transform.rotation = rot;
            float dist = 40f;
            for (int iter = 0; iter < 80; iter++)
            {
                cam.transform.position = target - dir * dist;
                float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
                foreach (Vector3 c in corners)
                {
                    Vector3 v = cam.WorldToViewportPoint(c);
                    minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                    minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                }
                float allowedH = 1f - topMargin - bottomMargin, allowedW = 1f - 2f * sideMargin;
                float fit = Mathf.Max((maxY - minY) / allowedH, (maxX - minX) / allowedW);
                // Anchor the board to the bottom (thumb zone); any spare height goes to sea and sky at the top, not empty sand.
                target += Vector3.forward * ((minY - bottomMargin) * dist * 0.5f);
                dist *= Mathf.Lerp(1f, fit, 0.5f);
            }
            if (safeRoot != null) ApplySafeArea();
        }

        // ---------------------------------------------------------------- HUD

        private void BuildHud()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // rendered by the game camera, so camera captures include the HUD
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); // 1 unit ≈ 1/3 dp on a 360dp-wide phone
            scaler.matchWidthOrHeight = 0f;

            safeRoot = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safeRoot.SetParent(canvasGo.transform, false);
            ApplySafeArea();

            Sprite rounded = RoundedSprite();
            // 150 units = 50 dp: comfortably above Android's 48 dp minimum touch target.
            Button restart = MakeButton(safeRoot, rounded, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(150f, 150f), new Color32(0x3D, 0x8B, 0xFF, 0xFF));
            Image icon = new GameObject("Icon", typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(restart.transform, false);
            icon.sprite = RestartIconSprite();
            icon.raycastTarget = false;
            icon.rectTransform.sizeDelta = new Vector2(96f, 96f);
            restart.onClick.AddListener(() => LoadLevel(levelIndex));

            levelLabel = MakeText(safeRoot, "Level 1", 72, new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(600f, 120f));

            banner = new GameObject("Banner", typeof(Image)).gameObject;
            banner.transform.SetParent(canvasGo.transform, false);
            var dim = banner.GetComponent<Image>();
            dim.color = new Color(0.05f, 0.08f, 0.15f, 0.45f);
            Stretch(dim.rectTransform);
            var card = new GameObject("Card", typeof(Image)).GetComponent<Image>();
            card.transform.SetParent(banner.transform, false);
            card.sprite = rounded;
            card.type = Image.Type.Sliced;
            card.color = new Color32(0xFF, 0xF8, 0xEF, 0xFF);
            card.rectTransform.sizeDelta = new Vector2(820f, 560f);
            bannerTitle = MakeText(card.rectTransform, "", 84, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(760f, 140f));
            bannerTitle.color = new Color32(0x3A, 0x2A, 0x3E, 0xFF);
            bannerTitle.GetComponent<Outline>().enabled = false;
            Button next = MakeButton(card.rectTransform, rounded, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(560f, 170f), new Color32(0x2E, 0xCC, 0x71, 0xFF));
            bannerButtonLabel = MakeText(next.transform, "", 64, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540f, 150f));
            next.onClick.AddListener(() => LoadLevel(session.IsWon ? (levelIndex + 1) % SliceLevels.Count : levelIndex));
            banner.SetActive(false);
        }

        private void ShowBanner(bool won)
        {
            ended = true;
            Debug.Log($"[Slice] Level {levelIndex + 1} {(won ? "complete" : "gridlocked")}: {session.Boarded}/{session.TotalPassengers} boarded");
            bannerTitle.text = won ? "Level complete!" : "Out of spots!";
            bannerButtonLabel.text = won ? "Next level" : "Try again";
            banner.SetActive(true);
        }

        private void ApplySafeArea()
        {
            Rect s = Screen.safeArea;
            safeRoot.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private Text MakeText(Transform parent, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject("Text", typeof(Text), typeof(Outline)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = box;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            var o = t.GetComponent<Outline>();
            o.effectColor = new Color(0.1f, 0.15f, 0.3f, 0.85f);
            o.effectDistance = new Vector2(4f, -4f);
            return t;
        }

        private static Button MakeButton(Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, Vector2 size, Color colour)
        {
            var img = new GameObject("Button", typeof(Image), typeof(Button)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = anchor;
            img.rectTransform.pivot = new Vector2(anchor.x == 0f ? 0f : anchor.x == 1f ? 1f : 0.5f, anchor.y == 1f ? 1f : anchor.y == 0f ? 0f : 0.5f);
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = colour;
            var b = img.GetComponent<Button>();
            var colours = b.colors;
            colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); // visible pressed state
            b.colors = colours;
            return b;
        }

        private static Sprite RoundedSprite()
        {
            const int n = 128, r = 40;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                float a = Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) + 0.5f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
        }

        /// <summary>Circular arrow drawn in code — no emoji (audit §E-2).</summary>
        private static Sprite RestartIconSprite()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            Vector2 c = new Vector2(n * 0.5f, n * 0.5f), head = c + new Vector2(Mathf.Cos(50f * Mathf.Deg2Rad), Mathf.Sin(50f * Mathf.Deg2Rad)) * 38f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f), d = p - c;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                float ring = (ang > 70f && ang < 340f) ? Mathf.Clamp01(9f - Mathf.Abs(d.magnitude - 38f) + 0.5f) : 0f;
                // Arrowhead: triangle at the arc's open end, pointing along the direction of travel.
                Vector2 q = p - head;
                float arrow = (q.y < 4f && q.y > -22f && Mathf.Abs(q.x) < (q.y + 22f) * 0.85f) ? 1f : 0f;
                byte a = (byte)(Mathf.Max(ring, arrow) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
