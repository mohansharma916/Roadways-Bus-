using System;
using System.Collections;
using RoadwaysGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// The Home screen: a beach-town street made in Blender (Tools/Blender/models.py, HomeStage) with the tour bus at a bus
    /// stop, the next level on its sign, passengers queuing at the kerb, shops, palms and the sea beyond; and its UI (coins,
    /// settings, Play, tab bar). The stage stands far off to the side of the level, so neither camera ever sees the other
    /// scene. SliceGame shows it at start and when the player taps Home.
    /// </summary>
    public sealed class SliceHome : MonoBehaviour
    {
        public static readonly Vector3 Stage = new Vector3(120f, 0f, 0f);
        /// <summary>Home is lit from behind the camera so faces show; levels light from the far side (SliceWorld.SunRotation).</summary>
        public static readonly Quaternion SunRotation = Quaternion.Euler(44f, 30f, 0f);
        private const float HalfWidthTan = 18f / 30f; // the Blender camera: 30 mm lens on a 36 mm sensor fitted to the width
        private static readonly int[] QueueColours = { 2, 0, 4, 1, 3, 6, 2, 0, 1, 4 }; // ChilbiColorType per passenger

        private static readonly Color Green = new Color32(0x35, 0xC8, 0x4A, 0xFF), TabBlue = new Color32(0x3D, 0x8B, 0xFF, 0xFF);
        private static readonly Color TabSelected = new Color32(0x74, 0xB4, 0xFF, 0xFF), Navy = new Color32(0x2E, 0x34, 0x48, 0xFF);

        private RectTransform ui;
        private TextMeshProUGUI coins, toast;
        private TextMeshPro sign;
        private Transform camPos, camLook;
        private Transform[] queue;
        private Vector3[] queueRest;
        private Coroutine toastRoutine;

        public void Build(RectTransform safeRoot, Action play, Action settings)
        {
            BuildStage();
            BuildUi(safeRoot, play, settings);
        }

        public void Show(int level, int coinCount)
        {
            ui.gameObject.SetActive(true);
            sign.text = "LEVEL " + (level + 1);
            coins.text = coinCount.ToString();
        }

        public void Hide() => ui.gameObject.SetActive(false);

        /// <summary>The Blender camera, keeping its width across the screen for any aspect ratio.</summary>
        public void PlaceCamera(Camera cam, float aspect)
        {
            cam.fieldOfView = 2f * Mathf.Atan(HalfWidthTan / aspect) * Mathf.Rad2Deg;
            cam.transform.position = camPos.position;
            cam.transform.LookAt(camLook.position);
        }

        // ---------------------------------------------------------------- 3D stage

        private void BuildStage()
        {
            // Laid out in Blender looking +Y; turned round here so the camera looks +Z, as the level camera does.
            Transform stage = SliceModels.Spawn("HomeStage", transform, (r, role) =>
                (role == "Shirt" || role == "Cap") && r.name.StartsWith("Queue")
                    ? ColorPaletteDatabase.GetPrimaryColor((ChilbiColorType)QueueColours[int.Parse(r.name.Substring(5))])
                    : (Color?)null);
            stage.SetPositionAndRotation(Stage, Quaternion.Euler(0f, 180f, 0f));
            Renderer sea = stage.Find("Sea").GetComponent<Renderer>();
            sea.sharedMaterial = SliceWorld.Sea(sea.bounds.min.z); // animated water, its foam line at the sea wall

            camPos = stage.Find("CameraPos");
            camLook = stage.Find("CameraLook");
            sign = SliceWorld.WorldText(stage.Find("SignAnchor"), "LEVEL 1", 0.1f, Navy);
            sign.transform.rotation = Quaternion.identity; // the bus faces the camera, which looks +Z

            queue = new Transform[QueueColours.Length];
            queueRest = new Vector3[queue.Length];
            for (int i = 0; i < queue.Length; i++)
            {
                queue[i] = stage.Find("Queue" + i);
                queueRest[i] = queue[i].localPosition;
            }
        }

        private void Update()
        {
            if (!ui.gameObject.activeSelf) return;
            for (int i = 0; i < queue.Length; i++) // waiting passengers bounce on the spot, out of step
                queue[i].localPosition = queueRest[i] + Vector3.up * (Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f + i * 1.3f)) * 0.05f);
        }

        // ---------------------------------------------------------------- UI

        private void BuildUi(RectTransform safeRoot, Action play, Action settings)
        {
            ui = new GameObject("HomeUi", typeof(RectTransform)).GetComponent<RectTransform>();
            ui.SetParent(safeRoot, false);
            Hud.Stretch(ui);

            // Coins, top-left.
            RectTransform pill = Hud.Box(ui, "Coins", new Color(1f, 1f, 1f, 0.95f), new Vector2(0f, 1f), new Vector2(110f, -58f), new Vector2(300f, 104f));
            pill.pivot = new Vector2(0f, 1f);
            pill.anchoredPosition = new Vector2(110f, -58f);
            coins = Hud.Text(pill, "0", 64, new Vector2(0.5f, 0.5f), new Vector2(26f, 2f), new Vector2(230f, 100f));
            coins.color = Navy;
            coins.outlineWidth = 0f;
            Image coin = Hud.Icon(ui, Icons.Coin(), 136f);
            coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0f, 1f);
            coin.rectTransform.anchoredPosition = new Vector2(108f, -110f);

            // Settings, top-right.
            Button gear = Hud.ChunkyButton(ui, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(150f, 150f), out _);
            Hud.Icon(gear.transform, Icons.Gear(new Color32(255, 255, 255, 255)), 100f);
            gear.onClick.AddListener(() => settings());

            // Play.
            Button go = Hud.ChunkyButton(ui, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(640f, 210f), out _, Green);
            TextMeshProUGUI label = Hud.Text(go.transform, "Play", 132, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(600f, 190f));
            label.outlineColor = new Color32(0x1C, 0x7A, 0x2B, 0xFF);
            label.outlineWidth = 0.3f;
            go.onClick.AddListener(() => play());

            // Tab bar: Shop · Home · Ranks. Only Home exists so far.
            RectTransform bar = Hud.Box(ui, "TabBar", TabBlue, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 230f));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, -30f); // runs off the bottom edge, so its lower corners don't show
            Tab(bar, -360f, Icons.Shop(), null, false, () => Toast("Coming soon"));
            Tab(bar, 0f, Icons.House(new Color32(255, 255, 255, 255)), "Home", true, null);
            Tab(bar, 360f, Icons.Podium(), null, false, () => Toast("Coming soon"));

            toast = Hud.Text(ui, "", 64, new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(800f, 100f));
            toast.gameObject.SetActive(false);
        }

        private static void Tab(RectTransform bar, float x, Sprite icon, string name, bool selected, Action onClick)
        {
            RectTransform tab = Hud.Box(bar, name ?? "Tab", selected ? TabSelected : TabBlue, new Vector2(0.5f, 0f),
                new Vector2(x, selected ? 30f : 30f), new Vector2(330f, selected ? 250f : 200f));
            tab.pivot = new Vector2(0.5f, 0f);
            tab.anchoredPosition = new Vector2(x, 30f);
            Image ic = Hud.Icon(tab, icon, selected ? 150f : 140f);
            ic.rectTransform.anchoredPosition = new Vector2(0f, selected ? 22f : 0f);
            if (name != null) Hud.Text(tab, name, 58, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(300f, 80f));
            if (onClick == null) return;
            Button b = tab.gameObject.AddComponent<Button>();
            b.targetGraphic = tab.GetComponent<Image>();
            b.onClick.AddListener(() => onClick());
        }

        private void Toast(string text)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ShowToast(text));
        }

        private IEnumerator ShowToast(string text)
        {
            toast.text = text;
            toast.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1.4f);
            toast.gameObject.SetActive(false);
        }
    }
}
