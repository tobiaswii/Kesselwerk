using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>Hilfsfunktionen und im Code erzeugte Grafiken (abgerundete Ecken, Kreise, Verläufe).</summary>
    public static class UIKit
    {
        static Sprite rounded, roundedSoft, circle, glow, gradient, hGradient;
        static TMP_FontAsset font;
        static bool fontTried;

        // Moderner Stil (Glas-Optik wie bei aktuellen Survival-/Monsterspielen)
        public static readonly Color Glass = new Color(0.055f, 0.07f, 0.095f, 0.82f);
        public static readonly Color GlassEdge = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color TextDim = new Color(0.78f, 0.82f, 0.88f, 1f);

        /// <summary>Schrift Exo 2 (OFL) aus Resources/Fonts, mit TMP-Standardschrift als Ersatz für fehlende Zeichen.</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (fontTried) return font;
                fontTried = true;
                var f = Resources.Load<Font>("Fonts/Exo2");
                if (f == null) return null;
                font = TMP_FontAsset.CreateFontAsset(f);
                if (font != null && TMP_Settings.defaultFontAsset != null)
                {
                    font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };
                }
                return font;
            }
        }

        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(96, 12f, 1.2f));
        public static Sprite HGradient => hGradient != null ? hGradient : (hGradient = MakeHGradient());
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

        static Sprite MakeHGradient()
        {
            var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - x / 63f, 1.6f)));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 4), new Vector2(0.5f, 0.5f), 100f);
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
            if (Font != null) t.font = Font;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color color, UnityAction onClick, float fontSize = 38f)
        {
            // Glas-Knopf: dunkle Fläche, feine helle Kante, farbiger Akzentstreifen links mit weichem Verlauf
            var edge = Img(parent, name, Rounded, GlassEdge);
            edge.raycastTarget = true;
            var img = Img(edge.transform, "Glas", Rounded, Glass);
            Stretch(img.rectTransform, 2, 2, 2, 2);
            var btn = edge.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.3f, 1.4f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.8f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.55f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            edge.gameObject.AddComponent<PressScale>();

            Color acc = color; acc.a = 1f;
            var glow = Img(img.transform, "Akzentverlauf", HGradient, new Color(acc.r, acc.g, acc.b, 0.45f));
            Stretch(glow.rectTransform, 0, 0, 0, 0);
            glow.rectTransform.anchorMax = new Vector2(0.75f, 1f);
            var bar = Img(img.transform, "Akzent", null, acc);
            bar.rectTransform.anchorMin = new Vector2(0f, 0.12f);
            bar.rectTransform.anchorMax = new Vector2(0f, 0.88f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            bar.rectTransform.sizeDelta = new Vector2(6f, 0f);
            var line = Img(img.transform, "Glanzlinie", null, new Color(1f, 1f, 1f, 0.07f));
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.sizeDelta = new Vector2(-12f, 2f);
            line.rectTransform.anchoredPosition = new Vector2(0f, -3f);

            var t = Text(img.transform, "Text", label, fontSize, Color.white, TextAlignmentOptions.Center, true);
            Stretch(t.rectTransform, 18, 4, 10, 4);
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = fontSize;
            t.characterSpacing = 1.5f;
            return btn;
        }

        /// <summary>Runder Knopf (z. B. Kampagnen-Stufen): Kreis mit Akzentring statt Streifen.</summary>
        public static void Round(Button b, Color accent)
        {
            var edge = b.GetComponent<Image>();
            edge.sprite = Circle; edge.type = Image.Type.Simple;
            Color a = accent; a.a = 1f;
            edge.color = a;
            var glass = b.transform.Find("Glas")?.GetComponent<Image>();
            if (glass != null)
            {
                glass.sprite = Circle; glass.type = Image.Type.Simple;
                Stretch(glass.rectTransform, 5, 5, 5, 5);
                glass.color = new Color(a.r * 0.35f, a.g * 0.35f, a.b * 0.35f, 0.92f);
                foreach (var n in new[] { "Akzent", "Glanzlinie", "Akzentverlauf" })
                {
                    var x = glass.transform.Find(n); if (x != null) x.gameObject.SetActive(false);
                }
                var hi = Img(glass.transform, "Licht", Glow, new Color(a.r, a.g, a.b, 0.45f));
                Stretch(hi.rectTransform, 6, 6, 6, 6);
                hi.transform.SetAsFirstSibling();
            }
        }

        /// <summary>Glas-Fläche mit feiner Kante (für Leisten, Chips, Banner).</summary>
        public static Image GlassPanel(Transform parent, string name, Color? accent = null, float alpha = 0.82f)
        {
            var edge = Img(parent, name, Rounded, GlassEdge);
            var g = Glass; g.a = alpha;
            var fill = Img(edge.transform, "Glas", Rounded, g);
            Stretch(fill.rectTransform, 2, 2, 2, 2);
            if (accent.HasValue)
            {
                Color a = accent.Value; a.a = 0.35f;
                var gr = Img(fill.transform, "Verlauf", HGradient, a);
                Stretch(gr.rectTransform);
                gr.rectTransform.anchorMax = new Vector2(0.7f, 1f);
                a.a = 1f;
                var bar = Img(fill.transform, "Akzent", null, a);
                bar.rectTransform.anchorMin = new Vector2(0f, 0.15f);
                bar.rectTransform.anchorMax = new Vector2(0f, 0.85f);
                bar.rectTransform.pivot = new Vector2(0f, 0.5f);
                bar.rectTransform.anchoredPosition = new Vector2(3f, 0f);
                bar.rectTransform.sizeDelta = new Vector2(5f, 0f);
            }
            return edge;
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
            UX.Outline(t, 0.25f);
            return t;
        }
    }

    /// <summary>Knöpfe federn beim Drücken leicht ein.</summary>
    public class PressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        float target = 1f, cur = 1f;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => target = 0.95f;
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => target = 1f;
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => target = 1f;
        void Update()
        {
            if (Mathf.Abs(cur - target) < 0.001f) return;
            cur = Mathf.MoveTowards(cur, target, Time.unscaledDeltaTime * 1.5f);
            transform.localScale = new Vector3(cur, cur, 1f);
        }
    }
}
