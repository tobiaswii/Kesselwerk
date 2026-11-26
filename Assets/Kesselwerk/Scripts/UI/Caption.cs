using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>Untertitel für Vorführvideos (nur über Testbefehle erreichbar).</summary>
    public class Caption : MonoBehaviour
    {
        static Caption instance;
        TextMeshProUGUI text;
        Image bg;

        public static void Show(string message)
        {
            if (instance == null)
            {
                var go = new GameObject("Untertitel");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 200;
                var scaler = go.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                instance = go.AddComponent<Caption>();
                instance.bg = UIKit.Img(go.transform, "Hintergrund", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.72f));
                instance.bg.rectTransform.anchorMin = new Vector2(0.08f, 0f);
                instance.bg.rectTransform.anchorMax = new Vector2(0.92f, 0f);
                instance.bg.rectTransform.pivot = new Vector2(0.5f, 0f);
                instance.bg.rectTransform.anchoredPosition = new Vector2(0f, 26f);
                instance.bg.rectTransform.sizeDelta = new Vector2(0f, 120f);
                instance.text = UIKit.Text(instance.bg.transform, "Text", "", 40f, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Stretch(instance.text.rectTransform, 30, 8, 30, 8);
                instance.text.enableAutoSizing = true;
                instance.text.fontSizeMin = 18f;
                instance.text.fontSizeMax = 40f;
            }
            bool on = !string.IsNullOrEmpty(message) && message != "off";
            instance.bg.gameObject.SetActive(on);
            instance.text.text = message;
        }
    }
}
