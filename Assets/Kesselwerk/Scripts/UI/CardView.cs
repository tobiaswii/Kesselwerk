using Kesselwerk.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>
    /// Darstellung einer Karte: Rahmen nach Seltenheit (Silber, Blau, Lila, Gold),
    /// Eckverzierungen, Edelstein, großes Monsterbild, Namensband, Regeltext
    /// und animierter Folienglanz für seltene Karten.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public const float W = 220f;
        public const float H = 320f;

        public CreatureDef def;
        public OwnedCreature owned;
        public RectTransform rt;
        public Button button;
        public CanvasGroup group;
        Image glowImg;
        RectTransform sheen;
        Image gemGlow;
        float sheenOffset;
        float sheenPeriod;

        static Color Metal(Rarity r) => Look.RarityColor(r);

        static Image Diamond(Transform parent, string name, Color color, Vector2 anchor, Vector2 pos, float size)
        {
            var img = UIKit.Img(parent, name, UIKit.Rounded, color);
            UIKit.Place(img.rectTransform, anchor, pos, new Vector2(size, size));
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            return img;
        }

        static Image StretchImg(Transform parent, string name, Sprite sprite, Color color, float l, float b, float r, float t)
        {
            var img = UIKit.Img(parent, name, sprite, color);
            UIKit.Stretch(img.rectTransform, l, b, r, t);
            return img;
        }

        /// <summary>Karte einer Kreatur. owned = null zeigt Stufe 1; unknown = noch nicht entdeckt (Silhouette).</summary>
        public static CardView Create(Transform parent, CreatureDef def, OwnedCreature owned = null, bool unknown = false)
        {
            int stage = owned != null ? owned.stage : 0;
            var spec = owned != null ? owned.ToSpec(0) : new UnitSpec(def.id, 1, 0);
            Color elemColor = Look.ElementColor(def.element);
            string cardName = unknown ? "???" : def.StageName(stage);
            var rt = UIKit.Rect("Karte_" + def.id, parent);
            rt.sizeDelta = new Vector2(W, H);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var cv = rt.gameObject.AddComponent<CardView>();
            cv.rt = rt;
            cv.def = def;
            cv.owned = owned;
            cv.group = rt.gameObject.AddComponent<CanvasGroup>();

            Color metal = Metal(def.rarity);
            Color metalDark = Color.Lerp(metal, Color.black, 0.55f);
            Color metalLight = Color.Lerp(metal, Color.white, 0.45f);
            bool fancy = !unknown && def.rarity >= Rarity.Selten;

            // Leuchten (spielbar) und Schatten
            cv.glowImg = StretchImg(rt, "Leuchten", UIKit.RoundedSoft, new Color(1f, 0.85f, 0.3f, 0f), -24, -24, -24, -24);
            StretchImg(rt, "Schatten", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.65f), -10, -20, -10, -4);
            if (fancy)
                StretchImg(rt, "Seltenheitsschein", UIKit.RoundedSoft, new Color(metal.r, metal.g, metal.b, def.rarity >= Rarity.Legendaer ? 0.55f : 0.35f), -14, -14, -14, -14);

            // Metallrahmen in drei Lagen (dunkel, hell, Grundfarbe) wirkt geprägt
            var frame = StretchImg(rt, "Rahmen", UIKit.Rounded, metalDark, 0, 0, 0, 0);
            frame.raycastTarget = true;
            cv.button = frame.gameObject.AddComponent<Button>();
            cv.button.transition = Selectable.Transition.None;
            StretchImg(rt, "RahmenHell", UIKit.Rounded, metalLight, 3, 3, 3, 3);
            StretchImg(rt, "RahmenMetall", UIKit.Rounded, metal, 5, 5, 5, 5);
            StretchImg(rt, "RahmenSchattierung", UIKit.Gradient, new Color(0f, 0f, 0f, 0.35f), 5, 5, 5, 5);

            // Innenfläche mit Farbverlauf in Monsterfarbe
            var inner = StretchImg(rt, "Innen", UIKit.Rounded, Color.Lerp(elemColor, new Color(0.08f, 0.06f, 0.1f), 0.72f), 11, 11, 11, 11);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var innerGlow = UIKit.Img(inner.transform, "InnenLicht", UIKit.Glow, new Color(elemColor.r, elemColor.g, elemColor.b, 0.45f));
            UIKit.Place(innerGlow.rectTransform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(300f, 260f));

            // Großes Monsterbild
            var artFrame = UIKit.Img(inner.transform, "Bildrahmen", UIKit.Rounded, metalDark);
            artFrame.rectTransform.anchorMin = new Vector2(0f, 1f);
            artFrame.rectTransform.anchorMax = new Vector2(1f, 1f);
            artFrame.rectTransform.pivot = new Vector2(0.5f, 1f);
            artFrame.rectTransform.offsetMin = new Vector2(4f, -196f);
            artFrame.rectTransform.offsetMax = new Vector2(-4f, -4f);

            var mask = UIKit.Img(artFrame.transform, "Maske", UIKit.Rounded, Color.white);
            UIKit.Stretch(mask.rectTransform, 2, 2, 2, 2);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var artRt = UIKit.Rect("Bild", mask.transform);
            UIKit.Stretch(artRt);
            var raw = artRt.gameObject.AddComponent<RawImage>();
            raw.raycastTarget = false;
            var portrait = MonsterModel.Portrait(def);
            if (unknown) raw.color = new Color(0f, 0f, 0f, 0.92f);
            if (portrait != null)
            {
                raw.texture = portrait;
                // Porträt 256x320, Rahmen ~194x188: oberen Teil fast quadratisch zeigen
                raw.uvRect = new Rect(0.02f, 0.1f, 0.96f, 0.8f);
            }
            else raw.color = elemColor;

            var shadeTop = UIKit.Img(mask.transform, "VerlaufOben", UIKit.Gradient, new Color(0f, 0f, 0f, 0.35f));
            shadeTop.rectTransform.anchorMin = new Vector2(0f, 0.78f);
            shadeTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            shadeTop.rectTransform.offsetMin = shadeTop.rectTransform.offsetMax = Vector2.zero;
            shadeTop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            var shade = UIKit.Img(mask.transform, "Verlauf", UIKit.Gradient, new Color(0f, 0f, 0f, 0.55f));
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

            // Folienglanz (läuft schräg über das Bild)
            if (fancy)
            {
                var sh = UIKit.Img(mask.transform, "Glanz", UIKit.Glow, new Color(1f, 1f, 1f, def.rarity >= Rarity.Legendaer ? 0.55f : 0.38f));
                sh.rectTransform.anchorMin = sh.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                sh.rectTransform.sizeDelta = new Vector2(70f, 420f);
                sh.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -28f);
                cv.sheen = sh.rectTransform;
                cv.sheenOffset = Random.value * 3f;
                cv.sheenPeriod = def.rarity >= Rarity.Legendaer ? 2.2f : 3.2f;
            }

            // Namensband (ragt seitlich über den Rahmen hinaus)
            var ribbon = UIKit.Rect("Namensband", rt);
            ribbon.anchorMin = new Vector2(0f, 1f);
            ribbon.anchorMax = new Vector2(1f, 1f);
            ribbon.offsetMin = new Vector2(-8f, -226f);
            ribbon.offsetMax = new Vector2(8f, -190f);
            StretchImg(ribbon, "BandSchatten", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.5f), -4, -8, -4, 0);
            StretchImg(ribbon, "BandRand", UIKit.Rounded, metalDark, 0, 0, 0, 0);
            StretchImg(ribbon, "Band", UIKit.Rounded, Color.Lerp(elemColor, new Color(0.2f, 0.12f, 0.06f), 0.4f), 2, 2, 2, 2);
            var bandShine = StretchImg(ribbon, "BandGlanz", UIKit.Gradient, new Color(1f, 1f, 1f, 0.22f), 2, 2, 2, 2);
            bandShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Diamond(ribbon, "BandEndeL", metal, new Vector2(0f, 0.5f), new Vector2(2f, 0f), 18f);
            Diamond(ribbon, "BandEndeR", metal, new Vector2(1f, 0.5f), new Vector2(-2f, 0f), 18f);

            var name = UIKit.Text(ribbon, "Name", cardName, 26f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(name.rectTransform, 22, 1, 22, 1);
            name.enableAutoSizing = true;
            name.fontSizeMin = 12f;
            name.fontSizeMax = 26f;
            UX.Outline(name, 0.25f);

            // Regeltext auf Pergament
            var box = UIKit.Img(rt, "Textfeld", UIKit.Rounded, new Color(0.96f, 0.92f, 0.82f, 0.96f));
            box.rectTransform.anchorMin = new Vector2(0f, 0f);
            box.rectTransform.anchorMax = new Vector2(1f, 0f);
            box.rectTransform.offsetMin = new Vector2(16f, 18f);
            box.rectTransform.offsetMax = new Vector2(-16f, 94f);
            StretchImg(box.transform, "Pergamentrand", UIKit.Gradient, new Color(0.55f, 0.4f, 0.2f, 0.25f), 0, 0, 0, 0);

            string body = unknown
                ? $"<i><color=#5b4a36>Noch nicht entdeckt.\n{Look.RarityName(def.rarity)} · {def.element}</color></i>"
                : $"<b>{def.skill.name}:</b> {def.skill.text}\n<color=#6b5a46><size=85%>{Look.RoleName(def.role)} · {Look.RarityName(def.rarity)}</size></color>";
            var text = UIKit.Text(box.transform, "Regeln", body, 15f, new Color(0.15f, 0.1f, 0.08f), TextAlignmentOptions.Center);
            UIKit.Stretch(text.rectTransform, 30, 4, 30, 4);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 15f;

            // Eckverzierungen
            Diamond(rt, "EckeLO", metalLight, new Vector2(0f, 1f), new Vector2(8f, -8f), 14f);
            Diamond(rt, "EckeRO", metalLight, new Vector2(1f, 1f), new Vector2(-8f, -8f), 14f);
            Diamond(rt, "EckeLU", metalLight, new Vector2(0f, 0f), new Vector2(8f, 8f), 14f);
            Diamond(rt, "EckeRU", metalLight, new Vector2(1f, 0f), new Vector2(-8f, 8f), 14f);

            // Seltenheits-Edelstein oben rechts
            var gemHolder = UIKit.Rect("Edelstein", rt);
            UIKit.Place(gemHolder, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(40f, 40f));
            if (fancy)
            {
                cv.gemGlow = UIKit.Img(gemHolder, "SteinLeuchten", UIKit.Glow, new Color(metal.r, metal.g, metal.b, 0.8f));
                UIKit.Place(cv.gemGlow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            }
            Diamond(gemHolder, "SteinFassung", metalDark, new Vector2(0.5f, 0.5f), Vector2.zero, 30f);
            Diamond(gemHolder, "Stein", fancy ? metal : new Color(0.6f, 0.62f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, 23f);
            Diamond(gemHolder, "SteinGlanz", new Color(1f, 1f, 1f, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(-3f, 4f), 8f);

            // Werte (große Abzeichen)
            var elem = UIKit.Badge(rt, "Element", Color.Lerp(elemColor, Color.black, 0.15f), 64f, Look.ElementShort(def.element));
            elem.fontSize = 20f;
            UIKit.Place((RectTransform)elem.transform.parent, new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(64f, 64f));
            if (!unknown)
            {
                var atk = UIKit.Badge(rt, "Angriff", new Color(0.88f, 0.22f, 0.15f), 62f, Stats.Atk(def, spec).ToString());
                atk.fontSize = 22f;
                UIKit.Place((RectTransform)atk.transform.parent, new Vector2(0f, 0f), new Vector2(18f, 20f), new Vector2(62f, 62f));
                var hp = UIKit.Badge(rt, "Leben", new Color(0.22f, 0.68f, 0.28f), 62f, Stats.Hp(def, spec).ToString());
                hp.fontSize = 20f;
                UIKit.Place((RectTransform)hp.transform.parent, new Vector2(1f, 0f), new Vector2(-18f, 20f), new Vector2(62f, 62f));

                // Stufe und Sterne unter dem Bild
                string lvl = owned != null ? $"Stufe {owned.level}  <color=#ffd23f>{Look.Stars(owned.stars)}</color>" : "Stufe 1";
                var lv = UIKit.Text(rt, "Stufe", lvl, 17f, Color.white, TextAlignmentOptions.Center, true);
                lv.rectTransform.anchorMin = lv.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                lv.rectTransform.anchoredPosition = new Vector2(0f, -178f);
                lv.rectTransform.sizeDelta = new Vector2(180f, 24f);
                UX.Outline(lv, 0.25f);
            }

            return cv;
        }

        void Update()
        {
            if (sheen != null)
            {
                float t = ((Time.time + sheenOffset) % sheenPeriod) / sheenPeriod;
                // nur im ersten Drittel laufen, danach Pause
                float k = Mathf.Clamp01(t * 3f);
                sheen.anchoredPosition = new Vector2(Mathf.Lerp(-200f, 200f, k), 0f);
            }
            if (gemGlow != null)
            {
                float p = 0.55f + 0.35f * Mathf.Sin((Time.time + sheenOffset) * 3f);
                var c = gemGlow.color;
                c.a = p;
                gemGlow.color = c;
            }
        }

        public void SetPlayable(bool playable, bool dimIfNot)
        {
            group.alpha = (!playable && dimIfNot) ? 0.6f : 1f;
            glowImg.color = new Color(1f, 0.85f, 0.3f, playable ? 0.85f : 0f);
        }
    }
}
