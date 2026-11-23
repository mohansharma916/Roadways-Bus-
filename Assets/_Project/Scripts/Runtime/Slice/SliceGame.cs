using System;
using System.Collections;
using System.Collections.Generic;
using RoadwaysGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// Vertical slice: one Core.Session per level, rendered as a 3D beach scene with the reference's HUD.
    /// Owns input, routing, boarding pace, boosters, pause, camera framing and HUD. All rules live in RoadwaysGame.Core.
    /// </summary>
    public sealed class SliceGame : MonoBehaviour
    {
        private const float CameraPitch = 60f, CameraFov = 30f; // tilted enough that vehicle sides and windows show
        private const float TapMargin = 0.32f;        // world units around each footprint: ~7dp, on top of the body itself
        private const float DriveSpeed = 10f, DepartSpeed = 9f;
        private const float CanvasWidth = 1080f;      // HUD reference width; the canvas scales with screen width
        private const float BoosterBarUnits = 264f;   // booster buttons' 30 offset + 220 height + a small gap, in canvas units
        private const float TopBarUnits = 200f;       // restart/pause buttons' 40 offset + 150 height + a small gap


        private Camera cam;
        private Transform level;
        private Session session;
        private SliceVehicle[] vehicles, docked;
        private SliceCrowd crowd;
        private SlicePath[] queuePaths;
        private int levelIndex, walking;
        private bool ended, paused;
        private Vector2Int framedFor;

        // Progress kept on the device: the level to play next, and coins.
        private const string LevelKey = "progress.level", CoinsKey = "progress.coins";
        private const int CoinsPerWin = 10; // shortcut: a flat reward until there is an economy to balance it against

        private SliceHome home;
        private GameObject homeSettings;
        private RectTransform gameHud;
        private bool atHome;
        private RectTransform safeRoot;
        private TextMeshProUGUI levelLabel, bannerTitle, bannerButtonLabel;
        private GameObject banner, pausePanel;
        private readonly int[] boosterCount = new int[3];
        private readonly Image[] boosterFace = new Image[3];
        private readonly TextMeshProUGUI[] boosterBadge = new TextMeshProUGUI[3];
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
            cam.farClipPlane = 150f;

            queuePaths = SliceWorld.QueuePaths();
            SliceWorld.BuildEnvironment(transform, queuePaths);
            BuildHud();
            ShowHome();
        }

        private static int SavedLevel => Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, 0), 0, SliceLevels.Count - 1);

        /// <summary>Leaves the level, if one is running, for the Home screen.</summary>
        private void ShowHome()
        {
            StopAllCoroutines();
            SetPaused(false);
            if (level != null) Destroy(level.gameObject);
            level = null;
            session = null;
            atHome = ended = true;
            banner.SetActive(false);
            gameHud.gameObject.SetActive(false);
            home.Show(SavedLevel, PlayerPrefs.GetInt(CoinsKey, 0));
            Frame(Screen.width, Screen.height, Screen.safeArea);
        }

        private void LoadLevel(int index)
        {
            StopAllCoroutines();
            SetPaused(false);
            atHome = false;
            home.Hide();
            homeSettings.SetActive(false);
            gameHud.gameObject.SetActive(true);
            if (level != null) Destroy(level.gameObject);
            levelIndex = index;
            ended = false;
            walking = 0;

            session = new Session(SliceLevels.Get(index));
            docked = new SliceVehicle[session.Level.BayCount + 1]; // + the VIP bay
            level = new GameObject("Level").transform;
            level.SetParent(transform, false);
            vehicles = new SliceVehicle[session.Level.Vehicles.Length];
            for (int i = 0; i < vehicles.Length; i++) vehicles[i] = SliceVehicle.Create(session.Level.Vehicles[i], i, level);
            crowd = new GameObject("Crowd").AddComponent<SliceCrowd>();
            crowd.transform.SetParent(level, false);
            crowd.Build(session, OnBoard);

            for (int b = 0; b < 3; b++) boosterCount[b] = 1;
            RefreshBoosters();
            levelLabel.text = "Level " + (index + 1);
            banner.SetActive(false);
            Frame(Screen.width, Screen.height, Screen.safeArea);
        }

        private void Update()
        {
            if (Screen.width != framedFor.x || Screen.height != framedFor.y) Frame(Screen.width, Screen.height, Screen.safeArea);
            if (atHome) return;
            // Android Back arrives as Escape: it pauses rather than quitting (audit §B-2).
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !ended) SetPaused(!paused);
            if (paused) return;
            if (!ended) HandleTap();
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
            if (!new Plane(Vector3.up, new Vector3(0f, 0.4f, 0f)).Raycast(ray, out float enter)) return -1;
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

        private Vector3 BayPos(int bay) => SliceWorld.BayCenter(bay, session.Level.BayCount);

        private List<Vector3> RouteToBay(SliceVehicle v, int bay)
        {
            LevelDef L = session.Level;
            Vector3 p = v.transform.position, f = v.transform.forward;
            bool upward = f.z > 0.5f;
            // Upward exits only need to clear the depot's top edge; others leave fully before turning onto a side lane.
            Vector3 exit = p + f * ExitDistance(p, f, upward ? 0.45f : v.Def.Length * 0.5f + 0.25f, L);
            Vector3 bayPos = BayPos(bay), bayFwd = SliceWorld.SlotRotation * Vector3.forward;
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

        private List<Vector3> DepartRoute(int bay)
        {
            Vector3 c = BayPos(bay), f = SliceWorld.SlotRotation * Vector3.forward;
            float roadZ = SliceWorld.ApronMaxZ - 0.55f;
            return new List<Vector3> { c, c + f * 2f, new Vector3(c.x + 1.8f, 0f, roadZ), new Vector3(16f, 0f, roadZ) };
        }

        // ---------------------------------------------------------------- boarding & end

        /// <summary>Someone left the loop at the exit for a parked vehicle of their colour: walk them over and count them in.</summary>
        private void OnBoard(BoardEvent e)
        {
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
            bus.Undock();
            yield return bus.Drive(DepartRoute(bay), DepartSpeed, null, null);
            Destroy(bus.gameObject);
            docked[bay] = null;
            session.ReleaseBay(bay);
        }

        private void CheckEnd()
        {
            if (ended || walking > 0) return;
            for (int b = 0; b < session.UsableBays; b++)
            {
                BayState st = session.GetBayState(b);
                if (st == BayState.Incoming || st == BayState.Departing) return; // still moving
            }
            if (session.IsWon) ShowBanner(true);
            else if (session.IsGridlocked) ShowBanner(false);
        }

        // ---------------------------------------------------------------- boosters & pause

        private void UseBooster(int which)
        {
            if (ended || paused || boosterCount[which] <= 0) return;
            switch (which)
            {
                case 0: // VIP: one extra spot for the rest of the level
                    if (!session.UnlockVipBay()) return;
                    break;
                case 1: // Arrange: whoever a parked vehicle can take steps to the front
                    session.Arrange(); // the crowd walks everyone to their new place
                    break;
                case 2: // Jumble: shuffle the waiting crowd
                    session.Jumble(UnityEngine.Random.Range(1, int.MaxValue));
                    break;
            }
            boosterCount[which]--;
            RefreshBoosters();
        }

        private void RefreshBoosters()
        {
            for (int b = 0; b < 3; b++)
            {
                bool available = boosterCount[b] > 0;
                boosterFace[b].color = available ? Hud.Blue : new Color32(0x9A, 0xA3, 0xB5, 0xFF);
                boosterBadge[b].text = boosterCount[b].ToString();
                boosterBadge[b].transform.parent.gameObject.SetActive(available);
            }
        }

        private void GoHome() => ShowHome();

        private void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f; // every slice animation runs on scaled time, so this stops the whole board
            if (pausePanel != null) pausePanel.SetActive(value);
        }

        // ---------------------------------------------------------------- verification hooks (editor bridge)

        public void LoadLevelForTest(int index) => LoadLevel(index);
        public void GoHomeForTest() => GoHome();
        public void UseBoosterForTest(int which) => UseBooster(which);
        public void SetPausedForTest(bool value) => SetPaused(value);

        /// <summary>Plays the current level with the solver's winning order, through the real screen-tap path.
        /// speed > 1 runs the whole game faster (the loop takes ~10 s a lap), for verification only.</summary>
        public void StartAutoplay(float pause = 0.15f, float speed = 1f) => StartCoroutine(Autoplay(pause, speed));

        private IEnumerator Autoplay(float pause, float speed)
        {
            Time.timeScale = speed;
            int[] order = Solver.Solve(session, out bool exhausted);
            Debug.Log(order == null ? $"[Slice] Autoplay: no solution (exhausted={exhausted})" : "[Slice] Autoplay order: " + string.Join(",", order));
            if (order == null) yield break;
            foreach (int i in order)
            {
                yield return new WaitUntil(Quiescent);
                yield return new WaitForSeconds(pause);
                Vector3 screen = cam.WorldToScreenPoint(vehicles[i].transform.position + Vector3.up * 0.4f);
                TapAt(screen);
                if (session.IsPresent(i)) Debug.LogWarning($"[Slice] Autoplay tap on vehicle {i} did not dispatch it");
            }
            yield return new WaitUntil(() => ended);
            Time.timeScale = 1f;
        }

        /// <summary>Nothing moving, and nothing more can board or step on: the state the solver reasons about.</summary>
        private bool Quiescent()
        {
            if (walking > 0) return false;
            foreach (SliceVehicle v in vehicles) if (v != null && v.Busy) return false;
            for (int b = 0; b < session.UsableBays; b++)
            {
                BayState st = session.GetBayState(b);
                if (st == BayState.Incoming || st == BayState.Departing) return false;
            }
            return !session.CanProgress;
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

        // ---------------------------------------------------------------- camera

        /// <summary>Fits depot-to-ring into the space between the top buttons and the booster bar, for any aspect ratio,
        /// anchored to the bottom.</summary>
        private void Frame(int width, int height, Rect safe)
        {
            framedFor = new Vector2Int(width, height);
            if (atHome) { home.PlaceCamera(cam, width / (float)height); return; }
            cam.fieldOfView = CameraFov;
            // The buttons scale with screen width, so their share of the height depends on the aspect ratio.
            float topMargin = (height - safe.yMax + TopBarUnits * width / CanvasWidth) / height, sideMargin = 0.0f;
            float bottomMargin = (safe.yMin + BoosterBarUnits * width / CanvasWidth) / height;

            var b = new Bounds();
            b.SetMinMax(new Vector3(-SliceWorld.FloorHalfWidth, 0f, SliceWorld.FloorMinZ), new Vector3(SliceWorld.FloorHalfWidth, 0.6f, SliceWorld.FrameTopZ));
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);

            Quaternion rot = Quaternion.Euler(CameraPitch, 0f, 0f);
            Vector3 dir = rot * Vector3.forward, target = b.center;
            cam.transform.rotation = rot;
            float dist = 50f;
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

            var canvasGo = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // rendered by the game camera, so camera captures include the HUD
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, 1920f); // 1 unit ≈ 1/3 dp on a 360dp-wide phone
            scaler.matchWidthOrHeight = 0f;

            safeRoot = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
            safeRoot.SetParent(canvasGo.transform, false);
            ApplySafeArea();
            gameHud = new GameObject("GameHud", typeof(RectTransform)).GetComponent<RectTransform>();
            gameHud.SetParent(safeRoot, false);
            Hud.Stretch(gameHud);

            // Top row, as in the reference: restart · level · pause. 150 units = 50 dp touch targets.
            Button restart = Hud.ChunkyButton(gameHud, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(150f, 150f), out _);
            Hud.Icon(restart.transform, Icons.Restart(), 96f);
            restart.onClick.AddListener(() => LoadLevel(levelIndex));
            Button pause = Hud.ChunkyButton(gameHud, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(150f, 150f), out _);
            Hud.Icon(pause.transform, Icons.Pause(), 84f);
            pause.onClick.AddListener(() => { if (!ended) SetPaused(true); });
            levelLabel = Hud.Text(gameHud, "Level 1", 96, new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(620f, 140f));

            // Booster bar, bottom centre.
            string[] names = { "VIP", "Sort", "Shuffle" }; // own names, not the reference's
            Sprite[] icons = { Icons.Star(), Icons.Arrange(), Icons.Ball() };
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                Button btn = Hud.ChunkyButton(gameHud, new Vector2(0.5f, 0f), new Vector2((i - 1) * 330f, 30f), new Vector2(270f, 220f), out boosterFace[i]);
                Image ic = Hud.Icon(btn.transform, icons[i], 130f);
                ic.rectTransform.anchoredPosition = new Vector2(0f, 30f);
                TextMeshProUGUI label = Hud.Text(btn.transform, names[i], 54, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(260f, 70f));
                label.rectTransform.pivot = new Vector2(0.5f, 0f);
                // Count badge, top-right corner (the reference's red/green bubbles).
                var badge = new GameObject("Badge", typeof(Image)).GetComponent<Image>();
                badge.transform.SetParent(btn.transform, false);
                badge.sprite = Hud.Rounded;
                badge.type = Image.Type.Sliced;
                badge.color = new Color32(0xF0, 0x3E, 0x3E, 0xFF);
                badge.raycastTarget = false;
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 1f);
                badge.rectTransform.sizeDelta = new Vector2(84f, 84f);
                badge.rectTransform.anchoredPosition = new Vector2(-8f, -8f);
                boosterBadge[i] = Hud.Text(badge.transform, "1", 52, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(84f, 84f));
                btn.onClick.AddListener(() => UseBooster(which));
            }

            pausePanel = SettingsPopup.Build(canvasGo.transform, close: () => SetPaused(false), home: GoHome, restart: () => LoadLevel(levelIndex));
            pausePanel.SetActive(false);

            banner = Overlay(canvasGo.transform, "", "", () => LoadLevel(session.IsWon ? (levelIndex + 1) % SliceLevels.Count : levelIndex), out bannerTitle, out bannerButtonLabel);
            banner.SetActive(false);

            home = new GameObject("Home").AddComponent<SliceHome>();
            home.transform.SetParent(transform, false);
            home.Build(safeRoot, play: () => LoadLevel(SavedLevel), settings: () => homeSettings.SetActive(true));
            homeSettings = SettingsPopup.Build(canvasGo.transform, close: () => homeSettings.SetActive(false), home: null, restart: null);
            homeSettings.SetActive(false);
        }

        private GameObject Overlay(Transform canvas, string title, string action, UnityEngine.Events.UnityAction onAction, out TextMeshProUGUI titleText, out TextMeshProUGUI actionText)
        {
            var dim = new GameObject("Overlay", typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(canvas, false);
            dim.color = new Color(0.05f, 0.08f, 0.15f, 0.5f);
            Hud.Stretch(dim.rectTransform);
            var card = new GameObject("Card", typeof(Image)).GetComponent<Image>();
            card.transform.SetParent(dim.transform, false);
            card.sprite = Hud.Rounded;
            card.type = Image.Type.Sliced;
            card.color = new Color32(0xFF, 0xF8, 0xEF, 0xFF);
            card.rectTransform.sizeDelta = new Vector2(820f, 760f);
            titleText = Hud.Text(card.rectTransform, title, 110, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(780f, 160f));
            titleText.color = new Color32(0x3A, 0x2A, 0x3E, 0xFF);
            titleText.outlineWidth = 0f;
            Button go = Hud.ChunkyButton(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 180f), out _, new Color32(0x2E, 0xCC, 0x71, 0xFF));
            actionText = Hud.Text(go.transform, action, 70, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(540f, 160f));
            go.onClick.AddListener(onAction);
            return dim.gameObject;
        }

        private void ShowBanner(bool won)
        {
            ended = true;
            Debug.Log($"[Slice] Level {levelIndex + 1} {(won ? "complete" : "gridlocked")}: {session.Boarded}/{session.TotalPassengers} boarded");
            if (won)
            {
                PlayerPrefs.SetInt(LevelKey, (levelIndex + 1) % SliceLevels.Count);
                PlayerPrefs.SetInt(CoinsKey, PlayerPrefs.GetInt(CoinsKey, 0) + CoinsPerWin);
                PlayerPrefs.Save();
            }
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
    }
}
