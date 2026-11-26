using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>Darstellung einer Karte (Rahmen, Bild, Name, Werte, Regeltext).</summary>
    public class CardView : MonoBehaviour
    {
        public const float W = 220f;
        public const float H = 320f;

        public CardData card;
        public RectTransform rt;
        public Button button;
        public CanvasGroup group;
        Image glowImg;

        public static CardView Create(Transform parent, CardData card)
        {
            var rt = UIKit.Rect("Karte_" + card.name, parent);
            rt.sizeDelta = new Vector2(W, H);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var cv = rt.gameObject.AddComponent<CardView>();
            cv.rt = rt;
            cv.card = card;
            cv.group = rt.gameObject.AddComponent<CanvasGroup>();

            // Leuchten (für "spielbar")
            cv.glowImg = UIKit.Img(rt, "Leuchten", UIKit.RoundedSoft, new Color(1f, 0.85f, 0.3f, 0f));
            UIKit.Stretch(cv.glowImg.rectTransform, -22, -22, -22, -22);

            // Schatten
            var shadow = UIKit.Img(rt, "Schatten", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(shadow.rectTransform, -10, -18, -10, -4);

            // Rahmen (klickbar)
            Color frameColor = Color.Lerp(card.color, new Color(0.12f, 0.09f, 0.07f), 0.45f);
            var frame = UIKit.Img(rt, "Rahmen", UIKit.Rounded, frameColor);
            UIKit.Stretch(frame.rectTransform);
            frame.raycastTarget = true;
            cv.button = frame.gameObject.AddComponent<Button>();
            cv.button.transition = Selectable.Transition.None;

            var gold = UIKit.Img(rt, "Goldrand", UIKit.Rounded, new Color(0.93f, 0.78f, 0.42f));
            UIKit.Stretch(gold.rectTransform, 6, 6, 6, 6);
            var inner = UIKit.Img(rt, "Innen", UIKit.Rounded, Color.Lerp(card.color, new Color(0.1f, 0.08f, 0.1f), 0.78f));
            UIKit.Stretch(inner.rectTransform, 9, 9, 9, 9);

            // Bild
            var artFrame = UIKit.Img(rt, "Bildrahmen", UIKit.Rounded, new Color(0.93f, 0.78f, 0.42f));
            artFrame.rectTransform.anchorMin = new Vector2(0f, 1f);
            artFrame.rectTransform.anchorMax = new Vector2(1f, 1f);
            artFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
            artFrame.rectTransform.offsetMin = new Vector2(16f, -186f);
            artFrame.rectTransform.offsetMax = new Vector2(-16f, -44f);

            var mask = UIKit.Img(artFrame.transform, "Maske", UIKit.Rounded, Color.white);
            UIKit.Stretch(mask.rectTransform, 3, 3, 3, 3);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var artRt = UIKit.Rect("Bild", mask.transform);
            UIKit.Stretch(artRt);
            var raw = artRt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var portrait = MonsterModel.Portrait(card);
            if (portrait != null)
            {
                raw.texture = portrait;
                // Bildausschnitt an Seitenverhältnis anpassen (Porträt 256x320, Rahmen ~182x136)
                raw.uvRect = new Rect(0f, 0.18f, 1f, 0.6f);
            }
            else raw.color = card.color;

            var shade = UIKit.Img(mask.transform, "Verlauf", UIKit.Gradient, new Color(0f, 0f, 0f, 0.45f));
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(1f, 0.35f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            // Namensleiste
            var plate = UIKit.Img(rt, "Namensleiste", UIKit.Rounded, Color.Lerp(card.color, new Color(0.15f, 0.1f, 0.05f), 0.35f));
            plate.rectTransform.anchorMin = new Vector2(0f, 1f);
            plate.rectTransform.anchorMax = new Vector2(1f, 1f);
            plate.rectTransform.offsetMin = new Vector2(40f, -46f);
            plate.rectTransform.offsetMax = new Vector2(-12f, -12f);
            var name = UIKit.Text(plate.transform, "Name", card.name, 24f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(name.rectTransform, 8, 2, 8, 2);
            name.enableAutoSizing = true;
            name.fontSizeMin = 12f;
            name.fontSizeMax = 24f;
            name.outlineWidth = 0.2f;
            name.outlineColor = new Color32(0, 0, 0, 180);

            // Textfeld
            var box = UIKit.Img(rt, "Textfeld", UIKit.Rounded, new Color(0.96f, 0.92f, 0.82f, 0.95f));
            box.rectTransform.anchorMin = new Vector2(0f, 0f);
            box.rectTransform.anchorMax = new Vector2(1f, 0f);
            box.rectTransform.offsetMin = new Vector2(18f, 30f);
            box.rectTransform.offsetMax = new Vector2(-18f, 128f);

            string rules = card.RulesText;
            string body = string.IsNullOrEmpty(rules)
                ? $"<i><color=#5b4a36>{card.flavor}</color></i>"
                : $"{rules}\n<i><color=#5b4a36>{card.flavor}</color></i>";
            var text = UIKit.Text(box.transform, "Regeln", body, 15f, new Color(0.15f, 0.1f, 0.08f), TextAlignmentOptions.Center);
            UIKit.Stretch(text.rectTransform, 8, 6, 8, 6);
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 16f;

            // Werte
            var cost = UIKit.Badge(rt, "Kosten", new Color(0.2f, 0.45f, 0.95f), 52f, card.cost.ToString());
            UIKit.Place((RectTransform)cost.transform.parent, new Vector2(0f, 1f), new Vector2(22f, -24f), new Vector2(52f, 52f));
            var atk = UIKit.Badge(rt, "Angriff", new Color(0.85f, 0.2f, 0.15f), 54f, card.attack.ToString());
            UIKit.Place((RectTransform)atk.transform.parent, new Vector2(0f, 0f), new Vector2(22f, 22f), new Vector2(54f, 54f));
            var hp = UIKit.Badge(rt, "Leben", new Color(0.2f, 0.65f, 0.25f), 54f, card.health.ToString());
            UIKit.Place((RectTransform)hp.transform.parent, new Vector2(1f, 0f), new Vector2(-22f, 22f), new Vector2(54f, 54f));

            return cv;
        }

        public void SetPlayable(bool playable, bool dimIfNot)
        {
            group.alpha = (!playable && dimIfNot) ? 0.6f : 1f;
            glowImg.color = new Color(1f, 0.85f, 0.3f, playable ? 0.85f : 0f);
        }
    }
}
