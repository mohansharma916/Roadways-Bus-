using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoadwaysGame.Slice
{
    /// <summary>Saved player settings, on by default. The slice has no music, sound or vibration yet; these are stored for when it does.</summary>
    public static class GameSettings
    {
        public static bool Music { get => Get("music"); set => Set("music", value); }
        public static bool Sound { get => Get("sound"); set => Set("sound", value); }
        public static bool Vibration { get => Get("vibration"); set => Set("vibration", value); }

        private static bool Get(string key) => PlayerPrefs.GetInt("settings." + key, 1) == 1;

        private static void Set(string key, bool on)
        {
            PlayerPrefs.SetInt("settings." + key, on ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>The pause popup: a "Settings" card with a close button, Music / Sound / Vibration switches, and Home / Restart.</summary>
    internal static class SettingsPopup
    {
        private static readonly Color Frame = new Color32(0xF8, 0xC5, 0x48, 0xFF), FrameLip = new Color32(0xD9, 0x96, 0x1C, 0xFF);
        private static readonly Color Panel = new Color32(0xFF, 0xF6, 0xE0, 0xFF), RowsBox = new Color32(0xEF, 0xE2, 0xC0, 0xFF);
        private static readonly Color32 Ink = new Color32(0xA8, 0x7E, 0x3C, 0xFF), Brown = new Color32(0x6B, 0x3A, 0x12, 0xFF);
        private static readonly Color SwitchOn = new Color32(0x2E, 0xCC, 0x5A, 0xFF), SwitchOff = new Color32(0xB8, 0x9E, 0x62, 0xFF);
        private static readonly Color Orange = new Color32(0xFF, 0xA4, 0x1C, 0xFF), Green = new Color32(0x3D, 0xBE, 0x4B, 0xFF), CloseRed = new Color32(0xFF, 0x74, 0x2E, 0xFF);

        /// <summary>Builds the popup (inactive) under the canvas. Dims and blocks the game behind it while shown. With no home or
        /// restart action (on the Home screen) it has just the switches.</summary>
        public static GameObject Build(Transform canvas, Action close, Action home, Action restart)
        {
            var dim = new GameObject("SettingsPopup", typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(canvas, false);
            dim.color = new Color(0.05f, 0.08f, 0.15f, 0.55f);
            Hud.Stretch(dim.rectTransform);

            // Gold frame with a darker lip underneath, like the buttons.
            bool actions = home != null || restart != null;
            float panelHeight = actions ? 730f : 520f;
            RectTransform card = Hud.Box(dim.transform, "Card", FrameLip, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(880f, panelHeight + 220f));
            RectTransform face = Hud.Box(card, "Face", Frame, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.offsetMin = new Vector2(0f, 18f);
            face.offsetMax = Vector2.zero;

            TextMeshProUGUI title = Hud.Text(face, "Settings", 112, new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(620f, 150f));
            title.outlineColor = Brown;
            title.outlineWidth = 0.3f;

            Button shut = Hud.ChunkyButton(face, new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(128f, 128f), out _, CloseRed);
            Hud.Icon(shut.transform, Icons.Cross(new Color32(255, 255, 255, 255)), 64f);
            shut.onClick.AddListener(() => close());

            RectTransform panel = Hud.Box(face, "Panel", Panel, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(800f, panelHeight));
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 40f);
            RectTransform rows = Hud.Box(panel, "Rows", RowsBox, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(740f, 460f));
            rows.pivot = new Vector2(0.5f, 1f);
            rows.anchoredPosition = new Vector2(0f, -30f);

            Row(rows, 0, "Music", Icons.Music(Ink), GameSettings.Music, on => GameSettings.Music = on);
            Row(rows, 1, "Sound", Icons.Speaker(Ink), GameSettings.Sound, on => GameSettings.Sound = on);
            Row(rows, 2, "Vibration", Icons.Vibrate(Ink), GameSettings.Vibration, on => GameSettings.Vibration = on);

            if (home != null) ActionButton(panel, "Home", -190f, Orange, home);
            if (restart != null) ActionButton(panel, "Restart", 190f, Green, restart);
            return dim.gameObject;
        }

        private static void Row(RectTransform rows, int index, string name, Sprite icon, bool on, Action<bool> changed)
        {
            float y = -80f - index * 150f;
            Image ic = Hud.Icon(rows, icon, 104f);
            ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            ic.rectTransform.anchoredPosition = new Vector2(-260f, y);
            TextMeshProUGUI label = Hud.Text(rows, name, 66, new Vector2(0.5f, 1f), new Vector2(-30f, y), new Vector2(300f, 100f));
            label.alignment = TextAlignmentOptions.Left;
            label.color = Ink;
            label.outlineWidth = 0f;
            Switch(rows, new Vector2(232f, y), on, changed);
        }

        /// <summary>Pill switch: green with a tick and the knob right when on; tan with a cross and the knob left when off.</summary>
        private static void Switch(RectTransform parent, Vector2 pos, bool on, Action<bool> changed)
        {
            RectTransform pill = Hud.Box(parent, "Switch", SwitchOff, new Vector2(0.5f, 1f), pos, new Vector2(196f, 90f));
            Image pillImage = pill.GetComponent<Image>();
            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = pillImage;
            RectTransform knob = Hud.Box(pill, "Knob", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            Image mark = Hud.Icon(pill, null, 46f);
            Sprite tick = Icons.Check(new Color32(255, 255, 255, 255)), cross = Icons.Cross(new Color32(255, 255, 255, 255));
            void Show()
            {
                pillImage.color = on ? SwitchOn : SwitchOff;
                knob.anchoredPosition = new Vector2(on ? 52f : -52f, 0f);
                mark.sprite = on ? tick : cross;
                mark.rectTransform.anchoredPosition = new Vector2(on ? -48f : 48f, 0f);
            }
            Show();
            button.onClick.AddListener(() => { on = !on; changed(on); Show(); });
        }

        private static void ActionButton(RectTransform panel, string text, float x, Color colour, Action onClick)
        {
            Button b = Hud.ChunkyButton(panel, new Vector2(0.5f, 0f), new Vector2(x, 44f), new Vector2(340f, 150f), out _, colour);
            TextMeshProUGUI label = Hud.Text(b.transform, text, 72, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(320f, 140f));
            label.outlineColor = Color.Lerp(colour, Color.black, 0.45f);
            b.onClick.AddListener(() => onClick());
        }
    }
}
