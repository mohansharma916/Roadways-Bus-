using System;
using System.Collections;
using RoadwaysGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoadwaysGame.Slice
{
    /// <summary>
    /// The Home screen: a small 3D stage (the bus parked on a beach road with the next level on its sign, passengers
    /// queuing beside it, the sea behind) and its UI (coins, settings, Play, tab bar). The stage stands far off to the side
    /// of the level, so neither camera ever sees the other scene. SliceGame shows it at start and when the player taps Home.
    /// </summary>
    public sealed class SliceHome : MonoBehaviour
    {
        public static readonly Vector3 Stage = new Vector3(120f, 0f, 0f);
        private const float Pitch = 22f, Fov = 36f;
        private const float FrameWidth = 3.6f; // world units kept across the screen, whatever its aspect

        private static readonly Color Green = new Color32(0x35, 0xC8, 0x4A, 0xFF), TabBlue = new Color32(0x3D, 0x8B, 0xFF, 0xFF);
        private static readonly Color TabSelected = new Color32(0x74, 0xB4, 0xFF, 0xFF), Navy = new Color32(0x2E, 0x34, 0x48, 0xFF);

        private RectTransform ui;
        private TextMeshProUGUI coins, toast;
        private TextMeshPro sign;
        private Transform[] queue;
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

        /// <summary>Points the camera at the stage, keeping FrameWidth across the screen for any aspect ratio.</summary>
        public void PlaceCamera(Camera cam, float aspect)
        {
            cam.fieldOfView = Fov;
            float halfWidth = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * aspect;
            Quaternion look = Quaternion.Euler(Pitch, 0f, 0f);
            Vector3 target = Stage + new Vector3(-0.5f, 0.1f, 1.7f); // aimed up the road, so the bus sits low, just above Play
            cam.transform.SetPositionAndRotation(target - look * Vector3.forward * (FrameWidth * 0.5f / halfWidth), look);
        }

        // ---------------------------------------------------------------- 3D stage

        private void BuildStage()
        {
            var stage = new GameObject("HomeStage").transform;
            stage.SetParent(transform, false);
            stage.position = Stage;

            // Sand up to the shoreline at z = 10, the sea beyond; a road runs out to it with a pavement for the queue.
            SliceWorld.Slab(stage, "Sand", new Vector3(0f, -0.05f, 0.5f), new Vector3(30f, 0.1f, 19f), SliceWorld.Lit(SliceWorld.Sand, 0.05f));
            SliceWorld.Slab(stage, "Sea", new Vector3(0f, -0.04f, 25f), new Vector3(60f, 0.02f, 30f), SliceWorld.Sea(Stage.z + 10f));
            SliceWorld.Slab(stage, "Road", new Vector3(0.35f, 0.01f, 0.5f), new Vector3(2.6f, 0.02f, 19f), SliceWorld.Lit(SliceWorld.Road, 0.15f));
            Material line = SliceWorld.Lit(SliceWorld.Curb, 0.3f);
            SliceWorld.Slab(stage, "EdgeLine", new Vector3(1.57f, 0.025f, 0.5f), new Vector3(0.08f, 0.01f, 19f), line);
            SliceWorld.Slab(stage, "Kerb", new Vector3(-0.92f, 0.04f, 0.5f), new Vector3(0.1f, 0.08f, 19f), line);
            SliceWorld.Slab(stage, "Pavement", new Vector3(-1.7f, 0.02f, 0.5f), new Vector3(1.5f, 0.04f, 19f), SliceWorld.Lit(new Color32(0xF6, 0xE7, 0xC8, 0xFF), 0.1f));
            SliceWorld.Palm(stage, new Vector3(-3.1f, 0f, 4.5f));
            SliceWorld.Palm(stage, new Vector3(3.4f, 0f, 6.5f));
            SliceWorld.Umbrella(stage, new Vector3(2.9f, 0f, 2.2f), new Color32(0xFF, 0x6B, 0x6B, 0xFF));

            // The bus faces the camera, with the next level on a sign across its front.
            Transform bus = ToyVehicle.Build(VehicleKind.Bus, new Color32(0x2F, 0x7B, 0xFF, 0xFF), stage, out float roofTop);
            bus.localPosition = new Vector3(0.35f, 0f, 0.6f);
            bus.localRotation = Quaternion.Euler(0f, 180f, 0f);
            float front = new VehicleDef(VehicleKind.Bus, 0, default, 0f).Length * 0.5f;
            Transform board = Prop(bus, ToyVehicle.RoundedBox(new Vector3(0.98f, 0.24f, 0.04f), 0.04f), SliceWorld.Lit(Color.white, 0.4f),
                new Vector3(0f, roofTop - 0.2f, front + 0.03f));
            sign = SliceWorld.WorldText(board, "LEVEL 1", 0.17f, Navy);
            sign.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // TextMesh Pro reads from its -Z side; the sign faces the bus's +Z

            // Passengers queue on the pavement beside the bus, in the game's colours.
            Mesh bean = ChilbiMeshGenerator.CreateCandyBeanMesh(0.5f, 0.8f, 0.43f);
            int[] colours = { 2, 0, 4, 1, 3, 6, 2, 0, 1, 4 };
            queue = new Transform[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                queue[i] = Prop(stage, bean, SliceWorld.Lit(ColorPaletteDatabase.GetPrimaryColor((ChilbiColorType)colours[i]), 0.55f),
                    new Vector3(-1.55f + (i % 2) * 0.12f, 0.4f, 0.2f + i * 0.48f));
            }
        }

        private static Transform Prop(Transform parent, Mesh mesh, Material m, Vector3 pos)
        {
            var go = new GameObject("Prop", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            return go.transform;
        }

        private void Update()
        {
            if (!ui.gameObject.activeSelf) return;
            for (int i = 0; i < queue.Length; i++) // waiting passengers bounce on the spot, out of step
            {
                Vector3 p = queue[i].localPosition;
                queue[i].localPosition = new Vector3(p.x, 0.4f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f + i * 1.3f)) * 0.07f, p.z);
            }
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
