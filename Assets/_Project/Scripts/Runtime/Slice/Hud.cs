using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoadwaysGame.Slice
{
    /// <summary>Shared HUD building blocks: chunky rounded buttons, TextMesh Pro labels, code-drawn icons.</summary>
    internal static class Hud
    {
        public static readonly Color Blue = new Color32(0x3D, 0x8B, 0xFF, 0xFF), BlueDark = new Color32(0x26, 0x5F, 0xC8, 0xFF);

        private static Sprite rounded;
        /// <summary>White rounded square, 9-sliced: the shape of every button, card and pill.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = RoundedSprite();

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>TextMesh Pro label: drawn from a distance field, so it stays sharp at any size, with a real outline.</summary>
        public static TextMeshProUGUI Text(Transform parent, string text, int size, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var t = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = anchor;
            t.rectTransform.anchoredPosition = pos;
            t.rectTransform.sizeDelta = box;
            if (SliceWorld.UiFont != null) t.font = SliceWorld.UiFont;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = Color.white;
            t.text = text;
            t.raycastTarget = false;
            t.outlineWidth = 0.24f;
            t.outlineColor = new Color32(0x1F, 0x2E, 0x5A, 0xFF);
            return t;
        }

        /// <summary>Rounded button with a darker lip underneath, the reference's chunky "pressable" look.</summary>
        public static Button ChunkyButton(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, out Image face, Color? colour = null)
        {
            Color top = colour ?? Blue;
            var lip = new GameObject("Button", typeof(Image), typeof(Button)).GetComponent<Image>();
            lip.transform.SetParent(parent, false);
            lip.rectTransform.anchorMin = lip.rectTransform.anchorMax = anchor;
            lip.rectTransform.pivot = new Vector2(anchor.x == 0f ? 0f : anchor.x == 1f ? 1f : 0.5f, anchor.y == 1f ? 1f : anchor.y == 0f ? 0f : 0.5f);
            lip.rectTransform.anchoredPosition = pos;
            lip.rectTransform.sizeDelta = size;
            lip.sprite = Rounded;
            lip.type = Image.Type.Sliced;
            lip.color = colour.HasValue ? Color.Lerp(top, Color.black, 0.25f) : BlueDark;
            face = new GameObject("Face", typeof(Image)).GetComponent<Image>();
            face.transform.SetParent(lip.transform, false);
            face.sprite = Rounded;
            face.type = Image.Type.Sliced;
            face.color = top;
            face.raycastTarget = false;
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = new Vector2(0f, 14f); // lip shows below the face
            face.rectTransform.offsetMax = Vector2.zero;
            var b = lip.GetComponent<Button>();
            b.targetGraphic = face;
            var colours = b.colors;
            colours.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f); // visible pressed state
            b.colors = colours;
            return b;
        }

        /// <summary>Rounded panel in a colour, anchored and sized.</summary>
        public static RectTransform Box(Transform parent, string name, Color colour, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = new GameObject(name, typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = Hud.Rounded;
            img.type = Image.Type.Sliced;
            img.color = colour;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = anchor;
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = size;
            return img.rectTransform;
        }

        public static Image Icon(Transform parent, Sprite sprite, float size)
        {
            var img = new GameObject("Icon", typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.anchoredPosition = new Vector2(0f, 7f);
            return img;
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
    }

    /// <summary>HUD icons drawn in code at 256px with 4x supersampling (shapes are defined in 128-px coordinates) — no emoji (audit §E-2), no art pipeline needed.</summary>
    internal static class Icons
    {
        private static Sprite Draw(Func<Vector2, Color32?> shade)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                for (int s = 0; s < 4; s++)
                {
                    Color32? c = shade(new Vector2(x + 0.25f + (s & 1) * 0.5f, y + 0.25f + (s >> 1) * 0.5f) * (128f / n));
                    if (!c.HasValue) continue;
                    r += c.Value.r; g += c.Value.g; b += c.Value.b; a += 1f;
                }
                px[y * n + x] = a == 0f ? new Color32(0, 0, 0, 0) : new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / 4f * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        public static Sprite Restart() => Draw(p =>
        {
            Vector2 c = new Vector2(64f, 64f), d = p - c;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            if (ang > 70f && ang < 340f && Mathf.Abs(d.magnitude - 38f) < 9f) return White;
            Vector2 head = c + new Vector2(Mathf.Cos(50f * Mathf.Deg2Rad), Mathf.Sin(50f * Mathf.Deg2Rad)) * 38f, q = p - head;
            return q.y < 4f && q.y > -22f && Mathf.Abs(q.x) < (q.y + 22f) * 0.85f ? White : (Color32?)null;
        });

        public static Sprite Pause() => Draw(p =>
            (Mathf.Abs(p.x - 46f) < 12f || Mathf.Abs(p.x - 82f) < 12f) && Mathf.Abs(p.y - 64f) < 40f ? White : (Color32?)null);

        public static Sprite Star() => Draw(p =>
        {
            Vector2 d = p - new Vector2(64f, 62f);
            float ang = Mathf.Atan2(d.x, d.y), sector = Mathf.Repeat(ang, Mathf.PI * 2f / 5f) / (Mathf.PI * 2f / 5f);
            float radius = Mathf.Lerp(24f, 54f, Mathf.Abs(sector - 0.5f) * 2f); // tips at sector edges, valleys at centres
            return d.magnitude < radius ? new Color32(0xFF, 0xD2, 0x3F, 0xFF) : (Color32?)null;
        });

        public static Sprite Arrange() => Draw(p =>
        {
            Color32[] bars = { new Color32(0xEE, 0x52, 0x53, 0xFF), new Color32(0xFE, 0xD3, 0x30, 0xFF), new Color32(0x2B, 0xD9, 0x7C, 0xFF) };
            for (int i = 0; i < 3; i++)
            {
                float cy = 96f - i * 32f, half = 48f - i * 8f;
                if (Mathf.Abs(p.y - cy) < 11f && Mathf.Abs(p.x - 64f) < half) return bars[i];
            }
            return null;
        });

        /// <summary>Distance from p to the segment a-b, for stroke-drawn icons.</summary>
        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        public static Sprite Cross(Color32 c) => Draw(p =>
            Seg(p, new Vector2(30f, 30f), new Vector2(98f, 98f)) < 11f || Seg(p, new Vector2(30f, 98f), new Vector2(98f, 30f)) < 11f ? c : (Color32?)null);

        public static Sprite Check(Color32 c) => Draw(p =>
            Seg(p, new Vector2(26f, 66f), new Vector2(52f, 40f)) < 11f || Seg(p, new Vector2(52f, 40f), new Vector2(102f, 92f)) < 11f ? c : (Color32?)null);

        /// <summary>Two beamed eighth notes.</summary>
        public static Sprite Music(Color32 c) => Draw(p =>
        {
            bool head(Vector2 o) => ((p.x - o.x) / 17f) * ((p.x - o.x) / 17f) + ((p.y - o.y) / 13f) * ((p.y - o.y) / 13f) < 1f;
            bool notes = head(new Vector2(40f, 32f)) || head(new Vector2(92f, 44f));
            bool stems = Seg(p, new Vector2(53f, 34f), new Vector2(53f, 100f)) < 5.5f || Seg(p, new Vector2(105f, 46f), new Vector2(105f, 112f)) < 5.5f;
            bool beam = Seg(p, new Vector2(53f, 98f), new Vector2(105f, 110f)) < 9f;
            return notes || stems || beam ? c : (Color32?)null;
        });

        /// <summary>Speaker cone with two sound waves.</summary>
        public static Sprite Speaker(Color32 c) => Draw(p =>
        {
            bool box = p.x > 18f && p.x < 42f && p.y > 48f && p.y < 80f;
            float spread = Mathf.InverseLerp(42f, 70f, p.x);
            bool cone = p.x >= 40f && p.x < 70f && Mathf.Abs(p.y - 64f) < Mathf.Lerp(16f, 38f, spread);
            Vector2 d = p - new Vector2(70f, 64f);
            float ang = Mathf.Abs(Mathf.Atan2(d.y, d.x)) * Mathf.Rad2Deg, r = d.magnitude;
            bool waves = ang < 50f && (Mathf.Abs(r - 22f) < 5.5f || Mathf.Abs(r - 42f) < 5.5f);
            return box || cone || waves ? c : (Color32?)null;
        });

        /// <summary>Phone outline with buzz marks either side.</summary>
        public static Sprite Vibrate(Color32 c) => Draw(p =>
        {
            float dx = Mathf.Abs(p.x - 64f) - 18f, dy = Mathf.Abs(p.y - 64f) - 36f;
            float outside = new Vector2(Mathf.Max(dx + 8f, 0f), Mathf.Max(dy + 8f, 0f)).magnitude + Mathf.Min(Mathf.Max(dx + 8f, dy + 8f), 0f) - 8f; // rounded rect, r 8
            bool phone = Mathf.Abs(outside) < 5f;
            bool buzz = false;
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = 64f + side * 32f, x1 = 64f + side * 42f;
                buzz |= Seg(p, new Vector2(x0, 82f), new Vector2(x1, 70f)) < 4.5f || Seg(p, new Vector2(x1, 70f), new Vector2(x0, 58f)) < 4.5f
                     || Seg(p, new Vector2(x0, 58f), new Vector2(x1, 46f)) < 4.5f;
            }
            return phone || buzz ? c : (Color32?)null;
        });

        /// <summary>Gold coin with a darker rim and a shine.</summary>
        public static Sprite Coin() => Draw(p =>
        {
            float r = Vector2.Distance(p, new Vector2(64f, 64f));
            if (r > 56f) return null;
            if (r > 46f) return new Color32(0xE0, 0x93, 0x10, 0xFF);
            bool shine = Seg(p, new Vector2(44f, 62f), new Vector2(56f, 88f)) < 6f;
            return shine ? new Color32(0xFF, 0xF1, 0xB8, 0xFF) : new Color32(0xFF, 0xC7, 0x2C, 0xFF);
        });

        /// <summary>Cog wheel with a hole.</summary>
        public static Sprite Gear(Color32 c) => Draw(p =>
        {
            Vector2 d = p - new Vector2(64f, 64f);
            float r = d.magnitude, a = Mathf.Atan2(d.y, d.x);
            float teeth = Mathf.Repeat(a * 8f / (Mathf.PI * 2f), 1f) < 0.5f ? 54f : 42f;
            return r < teeth && r > 18f ? c : (Color32?)null;
        });

        /// <summary>House: walls, roof and a door.</summary>
        public static Sprite House(Color32 c) => Draw(p =>
        {
            bool walls = p.x > 30f && p.x < 98f && p.y > 18f && p.y < 66f;
            bool door = p.x > 54f && p.x < 74f && p.y > 18f && p.y < 46f;
            bool roof = p.y >= 62f && p.y < 112f && Mathf.Abs(p.x - 64f) < (112f - p.y) * 0.98f;
            return (walls && !door) || roof ? c : (Color32?)null;
        });

        /// <summary>Market stall: striped awning over a counter.</summary>
        public static Sprite Shop() => Draw(p =>
        {
            if (p.y > 70f && p.y < 104f && p.x > 16f && p.x < 112f)
                return ((int)((p.x - 16f) / 16f) % 2 == 0) ? new Color32(0xEE, 0x52, 0x53, 0xFF) : new Color32(255, 255, 255, 255);
            if (p.y > 24f && p.y <= 70f && p.x > 26f && p.x < 102f) return p.y < 44f ? new Color32(0xF4, 0xA2, 0x3C, 0xFF) : new Color32(0xFF, 0xE0, 0x9E, 0xFF);
            return null;
        });

        /// <summary>Winners' podium: three steps, the middle one tallest and gold.</summary>
        public static Sprite Podium() => Draw(p =>
        {
            if (p.y < 20f) return null;
            if (p.x > 46f && p.x < 82f && p.y < 96f) return new Color32(0xFF, 0xC7, 0x2C, 0xFF);
            if (p.x > 14f && p.x <= 46f && p.y < 70f) return new Color32(0xC9, 0xD3, 0xE3, 0xFF);
            if (p.x >= 82f && p.x < 114f && p.y < 58f) return new Color32(0xE0, 0x9A, 0x62, 0xFF);
            return null;
        });

        public static Sprite Ball() => Draw(p =>
        {
            Vector2 d = p - new Vector2(64f, 64f);
            if (d.magnitude > 54f) return null;
            Color32[] slices = { new Color32(0xEE, 0x52, 0x53, 0xFF), new Color32(0xFE, 0xD3, 0x30, 0xFF), new Color32(0x2B, 0xD9, 0x7C, 0xFF), new Color32(0x1E, 0x90, 0xFF, 0xFF), new Color32(0x88, 0x54, 0xD0, 0xFF), new Color32(0xE8, 0x43, 0x93, 0xFF) };
            float ang = Mathf.Repeat(Mathf.Atan2(d.y, d.x) + Mathf.PI, Mathf.PI * 2f);
            Color32 c = slices[Mathf.Min(5, (int)(ang / (Mathf.PI * 2f) * 6f))];
            float shine = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(48f, 84f)) / 22f) * 0.7f;
            return Color32.Lerp(c, White, shine);
        });
    }
}
