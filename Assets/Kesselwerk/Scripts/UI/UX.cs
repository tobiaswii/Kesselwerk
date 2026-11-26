using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>Weitere UI-Bausteine: Leinwand mit sicherem Bereich, Balken, Panels, Hinweise.</summary>
    public static class UX
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.38f);
        public static readonly Color PanelDark = new Color(0.07f, 0.06f, 0.1f, 0.9f);
        public static readonly Color Accent = new Color(0.95f, 0.6f, 0.15f);
        public static readonly Color Blue = new Color(0.25f, 0.5f, 0.95f);
        public static readonly Color Green = new Color(0.25f, 0.7f, 0.35f);
        public static readonly Color Red = new Color(0.85f, 0.25f, 0.2f);
        public static readonly Color Grey = new Color(0.35f, 0.36f, 0.42f);

        /// <summary>Leinwand im Bildschirm, passt sich an Hoch-/Querformat an. Gibt den sicheren Bereich zurück.</summary>
        public static RectTransform CreateCanvas(string name, int order, out Canvas canvas, out CanvasScaler scaler)
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var go = new GameObject(name);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<OrientationScaler>().scaler = scaler;

            var safe = UIKit.Rect("SichererBereich", go.transform);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            return safe;
        }

        public static bool Portrait => Screen.height >= Screen.width;

        public static Image Panel(Transform parent, string name, Color color)
        {
            var img = UIKit.Img(parent, name, UIKit.Rounded, color);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>Gibt einem Panel einen farbigen Rand (Rand und Füllung liegen hinter dem Inhalt).</summary>
        public static void Frame(Image panel, Color frame, float width = 4f)
        {
            var fill = UIKit.Img(panel.transform, "Fuellung", UIKit.Rounded, panel.color);
            UIKit.Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();
            var edge = UIKit.Img(panel.transform, "Rand", UIKit.Rounded, frame);
            UIKit.Stretch(edge.rectTransform, -width, -width, -width, -width);
            edge.transform.SetAsFirstSibling();
            panel.color = new Color(0f, 0f, 0f, 0.004f);
        }

        /// <summary>Fortschrittsbalken mit Hintergrund; gibt die Füllung zurück (fillAmount setzen).</summary>
        public static Image Bar(Transform parent, string name, Color fill, Color back)
        {
            var bg = UIKit.Img(parent, name, UIKit.Rounded, back);
            var f = UIKit.Img(bg.transform, "Fuellung", UIKit.Rounded, fill);
            UIKit.Stretch(f.rectTransform, 2, 2, 2, 2);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 1f;
            return f;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
        {
            var t = UIKit.Text(parent, "Text", text, size, color, align, bold);
            return t;
        }

        public static void Outline(TextMeshProUGUI t, float width = 0.2f)
        {
            // Bei noch inaktiven Objekten ist das Material evtl. noch nicht gesetzt
            if (t.fontSharedMaterial == null && t.font != null) t.fontSharedMaterial = t.font.material;
            if (t.fontSharedMaterial == null) return;
            t.outlineWidth = width;
            t.outlineColor = new Color32(0, 0, 0, 200);
        }

        /// <summary>Kleines Element-Abzeichen (farbiger Kreis mit Kürzel).</summary>
        public static RectTransform ElementChip(Transform parent, Core.Element e, float size)
        {
            var img = UIKit.Img(parent, "Element", UIKit.Circle, Look.ElementColor(e));
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var t = UIKit.Text(img.transform, "K", Look.ElementShort(e), size * 0.36f, new Color(0.1f, 0.08f, 0.06f), TextAlignmentOptions.Center, true);
            UIKit.Stretch(t.rectTransform);
            return img.rectTransform;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    /// <summary>Hält Inhalte im sicheren Bereich (Notch, abgerundete Ecken).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        RectTransform rt;

        void Awake() => rt = (RectTransform)transform;

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0 || Screen.height == 0) return;
            last = sa;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Stellt die Referenzauflösung je nach Ausrichtung um.</summary>
    public class OrientationScaler : MonoBehaviour
    {
        public CanvasScaler scaler;
        bool? lastPortrait;
        public event System.Action Changed;

        void Update()
        {
            bool p = UX.Portrait;
            if (lastPortrait == p) return;
            lastPortrait = p;
            scaler.referenceResolution = p ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = p ? 0f : 1f;
            Changed?.Invoke();
        }
    }
}
