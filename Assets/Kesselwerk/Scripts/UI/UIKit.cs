using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>Hilfsfunktionen und im Code erzeugte Grafiken (abgerundete Ecken, Kreise, Verläufe).</summary>
    public static class UIKit
    {
        static Sprite rounded, roundedSoft, circle, glow, gradient;

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(96, 22f, 1.2f));
        public static Sprite RoundedSoft => roundedSoft != null ? roundedSoft : (roundedSoft = MakeRounded(96, 26f, 18f));
        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128, 1.5f));
        public static Sprite Glow => glow != null ? glow : (glow = MakeCircle(128, 60f));
        public static Sprite Gradient => gradient != null ? gradient : (gradient = MakeGradient());

        static Sprite MakeRounded(int size, float radius, float feather)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = size * 0.5f;
            float h = half - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - (h - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (h - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                    float d = outside + inside - radius;
                    float a = feather > 1.5f ? Mathf.Clamp01(-d / feather) : Mathf.Clamp01(0.5f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            float b = radius + feather + 2f;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeCircle(int size, float feather)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float half = size * 0.5f;
            float r = half - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - r;
                    float a = feather > 1.5f ? Mathf.Clamp01(-d / feather) : Mathf.Clamp01(0.5f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeGradient()
        {
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - y / 63f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.richText = true;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color color, UnityAction onClick, float fontSize = 38f)
        {
            var img = Img(parent, name, Rounded, color);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.7f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);

            var shine = Img(img.transform, "Glanz", Gradient, new Color(1f, 1f, 1f, 0.18f));
            shine.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            shine.rectTransform.anchorMax = new Vector2(1f, 1f);
            shine.rectTransform.offsetMin = new Vector2(6f, 0f);
            shine.rectTransform.offsetMax = new Vector2(-6f, -4f);
            shine.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            var t = Text(img.transform, "Text", label, fontSize, Color.white, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform, 10, 4, 10, 4);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = fontSize;
            return btn;
        }

        public static void SetLabel(Button b, string text)
        {
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = text;
        }

        /// <summary>Runder Wert-Anzeiger (Kosten, Angriff, Leben).</summary>
        public static TextMeshProUGUI Badge(Transform parent, string name, Color color, float size, string value)
        {
            var outer = Img(parent, name, Circle, new Color(0.08f, 0.06f, 0.05f, 0.95f));
            outer.rectTransform.sizeDelta = new Vector2(size, size);
            var ring = Img(outer.transform, "Ring", Circle, new Color(0.95f, 0.8f, 0.45f));
            Stretch(ring.rectTransform, 2, 2, 2, 2);
            var fill = Img(outer.transform, "Fuellung", Circle, color);
            Stretch(fill.rectTransform, size * 0.09f, size * 0.09f, size * 0.09f, size * 0.09f);
            var hi = Img(fill.transform, "Glanz", Gradient, new Color(1f, 1f, 1f, 0.35f));
            hi.rectTransform.anchorMin = new Vector2(0.15f, 0.5f);
            hi.rectTransform.anchorMax = new Vector2(0.85f, 0.95f);
            hi.rectTransform.offsetMin = hi.rectTransform.offsetMax = Vector2.zero;
            hi.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            var t = Text(outer.transform, "Wert", value, size * 0.55f, Color.white, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform);
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 200);
            return t;
        }
    }
}
