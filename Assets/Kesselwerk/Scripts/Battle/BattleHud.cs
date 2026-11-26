using System.Collections;
using System.Collections.Generic;
using Kesselwerk.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>Oberfläche im Kampf: Lebens- und Energieleisten, Schadenszahlen, Fähigkeitsbanner, Tempo, Ergebnis.</summary>
    public class BattleHud : MonoBehaviour
    {
        class UnitBar
        {
            public RectTransform rt;
            public Image hp, shield, energy;
            public TextMeshProUGUI label;
            public Creature view;
        }

        AutoBattleManager bm;
        Camera cam;
        RectTransform root, barLayer, fxLayer, overlay, bannerRt;
        TextMeshProUGUI bannerText, speedLabel, titleText;
        CanvasGroup bannerGroup;
        Image bossHp;
        TextMeshProUGUI bossLabel;
        RectTransform bossBar;
        readonly Dictionary<int, UnitBar> bars = new Dictionary<int, UnitBar>();
        Canvas canvas;
        CanvasScaler scaler;

        public void Init(AutoBattleManager manager, Camera camera, StageDef stage)
        {
            bm = manager;
            cam = camera;
            root = UX.CreateCanvas("Kampf-HUD", 20, out canvas, out scaler);

            barLayer = UIKit.Rect("Leisten", root);
            UIKit.Stretch(barLayer);
            fxLayer = UIKit.Rect("Zahlen", root);
            UIKit.Stretch(fxLayer);

            // Kopfzeile
            var top = UIKit.Rect("Kopfzeile", root);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 120f);
            var topBg = UIKit.Img(top, "Hintergrund", UIKit.Gradient, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Stretch(topBg.rectTransform, 0, -40, 0, 0);
            topBg.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            titleText = UIKit.Text(top, "Titel", $"{Campaign.RegionNames[stage.region]}  ·  {stage.name}", 40f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(0.6f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(30f, 0f);
            titleText.rectTransform.offsetMax = Vector2.zero;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 18f;
            titleText.fontSizeMax = 40f;
            UX.Outline(titleText);

            var speed = UIKit.Button(top, "Tempo", "", new Color(0.2f, 0.45f, 0.85f), () => { bm.CycleSpeed(); RefreshSpeed(); Sound.Play("klick"); }, 34f);
            UIKit.Place((RectTransform)speed.transform, new Vector2(1f, 0.5f), new Vector2(-300f, 0f), new Vector2(150f, 84f));
            speedLabel = speed.GetComponentInChildren<TextMeshProUGUI>();
            var skip = UIKit.Button(top, "Ueberspringen", "Ende ▶▶", new Color(0.35f, 0.35f, 0.42f), () => { bm.SkipToEnd(); Sound.Play("klick"); }, 30f);
            UIKit.Place((RectTransform)skip.transform, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(190f, 84f));
            UIKit.SetLabel(skip, "Ende");
            RefreshSpeed();

            // Bosslebensleiste
            bossBar = UIKit.Rect("Bossleiste", root);
            bossBar.anchorMin = new Vector2(0.15f, 1f);
            bossBar.anchorMax = new Vector2(0.85f, 1f);
            bossBar.pivot = new Vector2(0.5f, 1f);
            bossBar.anchoredPosition = new Vector2(0f, -135f);
            bossBar.sizeDelta = new Vector2(0f, 40f);
            bossHp = UX.Bar(bossBar, "Leben", new Color(0.9f, 0.2f, 0.15f), new Color(0f, 0f, 0f, 0.7f));
            UIKit.Stretch(bossHp.transform.parent as RectTransform);
            bossLabel = UIKit.Text(bossBar, "Name", "", 26f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(bossLabel.rectTransform);
            UX.Outline(bossLabel);
            bossBar.gameObject.SetActive(false);

            // Banner für Fähigkeiten
            bannerRt = UIKit.Rect("Banner", root);
            UIKit.Place(bannerRt, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(900f, 110f));
            var bbg = UIKit.Img(bannerRt, "Hintergrund", UIKit.RoundedSoft, new Color(0.05f, 0.04f, 0.08f, 0.8f));
            UIKit.Stretch(bbg.rectTransform, -20, -10, -20, -10);
            bannerText = UIKit.Text(bannerRt, "Text", "", 46f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(bannerText.rectTransform);
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 20f;
            bannerText.fontSizeMax = 46f;
            UX.Outline(bannerText, 0.25f);
            bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;

            overlay = UIKit.Rect("Overlay", root);
            UIKit.Stretch(overlay);
            overlay.gameObject.SetActive(false);
        }

        void RefreshSpeed() => speedLabel.text = $"{AutoBattleManager.Speed}x";

        public void ShowSynergies(List<Synergy> player, List<Synergy> enemy)
        {
            Chips(player, true);
            Chips(enemy, false);
        }

        void Chips(List<Synergy> list, bool left)
        {
            if (list.Count == 0) return;
            var col = UIKit.Rect(left ? "SynergienSpieler" : "SynergienGegner", root);
            col.anchorMin = col.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            col.pivot = new Vector2(left ? 0f : 1f, 1f);
            col.anchoredPosition = new Vector2(left ? 16f : -16f, -190f);
            col.sizeDelta = new Vector2(330f, 400f);
            for (int i = 0; i < list.Count; i++)
            {
                var chip = UIKit.Img(col, "Synergie", UIKit.Rounded, left ? new Color(0.1f, 0.25f, 0.5f, 0.85f) : new Color(0.5f, 0.15f, 0.1f, 0.85f));
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(left ? 0f : 1f, 1f);
                chip.rectTransform.pivot = new Vector2(left ? 0f : 1f, 1f);
                chip.rectTransform.anchoredPosition = new Vector2(0f, -i * 66f);
                chip.rectTransform.sizeDelta = new Vector2(330f, 60f);
                var t = UIKit.Text(chip.transform, "T", $"<b>{list[i].name}</b>\n<size=70%>{list[i].text}</size>", 22f, Color.white, left ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight);
                UIKit.Stretch(t.rectTransform, 12, 2, 12, 2);
            }
        }

        // ------------------------------------------------------------------
        // Leisten über den Kreaturen
        // ------------------------------------------------------------------

        public void AddUnit(BattleUnit u, Creature view)
        {
            var b = new UnitBar { view = view };
            b.rt = UIKit.Rect("Leiste_" + u.uid, barLayer);
            b.rt.sizeDelta = new Vector2(150f, 46f);
            var bg = UIKit.Img(b.rt, "Rahmen", UIKit.Rounded, new Color(0f, 0f, 0f, 0.6f));
            bg.rectTransform.anchorMin = new Vector2(0f, 0f);
            bg.rectTransform.anchorMax = new Vector2(1f, 0f);
            bg.rectTransform.pivot = new Vector2(0.5f, 0f);
            bg.rectTransform.sizeDelta = new Vector2(0f, 30f);

            var hpBack = UIKit.Img(bg.transform, "HPGrund", UIKit.Rounded, new Color(0.15f, 0.1f, 0.1f));
            hpBack.rectTransform.anchorMin = new Vector2(0f, 0.38f);
            hpBack.rectTransform.anchorMax = new Vector2(1f, 1f);
            hpBack.rectTransform.offsetMin = new Vector2(3f, 0f);
            hpBack.rectTransform.offsetMax = new Vector2(-3f, -3f);
            b.hp = UIKit.Img(hpBack.transform, "HP", UIKit.Rounded, u.team == 0 ? new Color(0.3f, 0.85f, 0.35f) : new Color(0.95f, 0.3f, 0.25f));
            UIKit.Stretch(b.hp.rectTransform);
            b.hp.type = Image.Type.Filled;
            b.hp.fillMethod = Image.FillMethod.Horizontal;
            b.shield = UIKit.Img(hpBack.transform, "Schild", UIKit.Rounded, new Color(0.75f, 0.9f, 1f, 0.85f));
            UIKit.Stretch(b.shield.rectTransform);
            b.shield.type = Image.Type.Filled;
            b.shield.fillMethod = Image.FillMethod.Horizontal;
            b.shield.fillOrigin = 1;
            b.shield.fillAmount = 0f;

            var enBack = UIKit.Img(bg.transform, "EnergieGrund", UIKit.Rounded, new Color(0.12f, 0.12f, 0.2f));
            enBack.rectTransform.anchorMin = new Vector2(0f, 0f);
            enBack.rectTransform.anchorMax = new Vector2(1f, 0.38f);
            enBack.rectTransform.offsetMin = new Vector2(3f, 3f);
            enBack.rectTransform.offsetMax = new Vector2(-3f, -1f);
            b.energy = UIKit.Img(enBack.transform, "Energie", UIKit.Rounded, new Color(1f, 0.8f, 0.2f));
            UIKit.Stretch(b.energy.rectTransform);
            b.energy.type = Image.Type.Filled;
            b.energy.fillMethod = Image.FillMethod.Horizontal;
            b.energy.fillAmount = 0f;

            var chip = UX.ElementChip(b.rt, u.def.element, 34f);
            chip.anchorMin = chip.anchorMax = new Vector2(0f, 0f);
            chip.anchoredPosition = new Vector2(-6f, 15f);
            b.label = UIKit.Text(b.rt, "Stufe", $"Lv {u.spec.level}", 20f, Color.white, TextAlignmentOptions.BottomLeft, true);
            b.label.rectTransform.anchorMin = new Vector2(0f, 1f);
            b.label.rectTransform.anchorMax = new Vector2(1f, 1f);
            b.label.rectTransform.pivot = new Vector2(0f, 1f);
            b.label.rectTransform.anchoredPosition = new Vector2(16f, 2f);
            b.label.rectTransform.sizeDelta = new Vector2(0f, 22f);
            UX.Outline(b.label);
            bars[u.uid] = b;

            if (u.def.isBoss)
            {
                bossBar.gameObject.SetActive(true);
                bossLabel.text = $"BOSS · {u.Name}";
                b.rt.gameObject.SetActive(false);
            }
        }

        public void SetBar(int uid, float hp, float max, float shield, float energy, bool isBoss)
        {
            if (!bars.TryGetValue(uid, out var b)) return;
            float h = max > 0 ? Mathf.Clamp01(hp / max) : 0f;
            b.hp.fillAmount = h;
            b.shield.fillAmount = max > 0 ? Mathf.Clamp01(shield / max) : 0f;
            b.energy.fillAmount = Mathf.Clamp01(energy / 100f);
            b.energy.color = energy >= 100f ? new Color(1f, 1f, 0.6f) : new Color(1f, 0.8f, 0.2f);
            if (isBoss) bossHp.fillAmount = h;
            if (hp <= 0f) b.rt.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            foreach (var kv in bars)
            {
                var b = kv.Value;
                if (b.view == null || !b.view.gameObject.activeInHierarchy || b.view.IsDead)
                {
                    if (b.rt.gameObject.activeSelf && (b.view == null || b.view.IsDead)) b.rt.gameObject.SetActive(false);
                    continue;
                }
                Vector3 sp = cam.WorldToScreenPoint(b.view.Top);
                if (sp.z < 0f) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(barLayer, sp, null, out var local);
                b.rt.anchoredPosition = local + new Vector2(0f, 10f);
            }
        }

        // ------------------------------------------------------------------
        // Schwebende Zahlen und Banner
        // ------------------------------------------------------------------

        readonly Dictionary<Vector2Int, float> floatSlots = new Dictionary<Vector2Int, float>();

        public void Float(Vector3 world, string text, Color color, float size = 44f)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fxLayer, sp, null, out var local);
            // Mehrere Texte am selben Ort übereinander stapeln statt überlappen
            var key = new Vector2Int(Mathf.RoundToInt(local.x / 80f), Mathf.RoundToInt(local.y / 80f));
            int stack = 0;
            while (floatSlots.TryGetValue(key + new Vector2Int(0, stack), out var until) && until > Time.time) stack++;
            floatSlots[key + new Vector2Int(0, stack)] = Time.time + 0.45f;
            var t = UIKit.Text(fxLayer, "Zahl", text, size, color, TextAlignmentOptions.Center, true);
            t.rectTransform.anchoredPosition = local + new Vector2(Random.Range(-20f, 20f), stack * 52f);
            t.rectTransform.sizeDelta = new Vector2(400f, 80f);
            UX.Outline(t, 0.28f);
            StartCoroutine(FloatRoutine(t));
        }

        IEnumerator FloatRoutine(TextMeshProUGUI t)
        {
            var rt = t.rectTransform;
            Vector2 start = rt.anchoredPosition;
            for (float a = 0f; a < 1f; a += Time.deltaTime / 0.9f)
            {
                float k = Mathf.Clamp01(a);
                rt.anchoredPosition = start + new Vector2(0f, 90f * k);
                rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.3f, k / 0.15f) : Mathf.Lerp(1.3f, 1f, (k - 0.15f) / 0.3f));
                t.alpha = k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        Coroutine bannerCo;

        public void Banner(string text, float time = 1.1f)
        {
            if (bannerCo != null) StopCoroutine(bannerCo);
            bannerCo = StartCoroutine(BannerRoutine(text, time));
        }

        IEnumerator BannerRoutine(string text, float time)
        {
            bannerText.text = text;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.15f)
            {
                bannerGroup.alpha = t;
                bannerRt.localScale = Vector3.one * Mathf.Lerp(1.3f, 1f, t);
                yield return null;
            }
            bannerGroup.alpha = 1f;
            bannerRt.localScale = Vector3.one;
            yield return new WaitForSeconds(time);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
            {
                bannerGroup.alpha = 1f - t;
                yield return null;
            }
            bannerGroup.alpha = 0f;
        }

        // ------------------------------------------------------------------
        // Ergebnis
        // ------------------------------------------------------------------

        public void ShowResult(bool won, Rewards rw, BattleResult result, System.Action onContinue, System.Action onRetry)
        {
            overlay.gameObject.SetActive(true);
            UX.Clear(overlay);
            var dim = UIKit.Img(overlay, "Abdunkeln", null, new Color(0f, 0f, 0f, 0.55f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            bool portrait = UX.Portrait;
            var panel = UX.Panel(overlay, "Ergebnis", new Color(0.08f, 0.07f, 0.12f, 0.96f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, portrait ? new Vector2(980f, 1500f) : new Vector2(1500f, 960f));
            UX.Frame(panel, won ? UX.Gold : new Color(0.6f, 0.6f, 0.7f));

            var title = UIKit.Text(panel.transform, "Titel", won ? "SIEG!" : "NIEDERLAGE", 96f, won ? UX.Gold : new Color(0.85f, 0.85f, 0.9f), TextAlignmentOptions.Center, true);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            title.rectTransform.sizeDelta = new Vector2(0f, 120f);
            UX.Outline(title, 0.25f);

            // Belohnungen
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#ffd23f>+{rw.gold} Gold</color>    <color=#9fd3ff>+{rw.xp} EP</color>");
            if (rw.crystals > 0) sb.Append($"    <color=#d79bff>+{rw.crystals} Kristalle</color>");
            if (rw.potions > 0) sb.Append($"    <color=#7dffb0>+{rw.potions} Tränke</color>");
            if (rw.stones > 0) sb.Append($"    <color=#ffb36b>+{rw.stones} Steine</color>");
            if (rw.firstClear) sb.Append("\n<size=80%><color=#ffd23f>Erstmals geschafft!</color></size>");
            if (rw.levelUps.Count > 0) sb.Append("\n<size=75%>Aufgestiegen: " + string.Join(", ", rw.levelUps) + "</size>");
            if (!won) sb.Append("\n<size=75%><color=#bbbbcc>Tipp: Stärke dein Team mit Tränken, Fusion oder Elementvorteilen.</color></size>");
            var rewards = UIKit.Text(panel.transform, "Belohnung", sb.ToString(), 36f, Color.white, TextAlignmentOptions.Top);
            rewards.rectTransform.anchorMin = new Vector2(0f, 1f);
            rewards.rectTransform.anchorMax = new Vector2(1f, 1f);
            rewards.rectTransform.pivot = new Vector2(0.5f, 1f);
            rewards.rectTransform.anchoredPosition = new Vector2(0f, -160f);
            rewards.rectTransform.sizeDelta = new Vector2(-60f, 200f);

            // Statistik: Schaden und Heilung je Kreatur
            var stats = UIKit.Rect("Statistik", panel.transform);
            stats.anchorMin = new Vector2(0f, 0f);
            stats.anchorMax = new Vector2(1f, 1f);
            stats.offsetMin = new Vector2(50f, 170f);
            stats.offsetMax = new Vector2(-50f, -380f);
            var head = UIKit.Text(stats, "Kopf", "Statistik  <size=70%><color=#ff8a6b>■ Schaden</color>  <color=#7dffb0>■ Heilung</color></size>", 34f, Color.white, TextAlignmentOptions.TopLeft, true);
            UIKit.Stretch(head.rectTransform);

            var mine = result.units.FindAll(u => u.team == 0);
            float maxVal = 1f;
            foreach (var u in mine) maxVal = Mathf.Max(maxVal, u.damageDealt, u.healingDone);
            BattleUnit mvp = null;
            foreach (var u in mine) if (mvp == null || u.damageDealt + u.healingDone > mvp.damageDealt + mvp.healingDone) mvp = u;
            float rowH = 70f;
            for (int i = 0; i < mine.Count; i++)
            {
                var u = mine[i];
                var row = UIKit.Rect("Zeile", stats);
                row.anchorMin = new Vector2(0f, 1f);
                row.anchorMax = new Vector2(1f, 1f);
                row.pivot = new Vector2(0.5f, 1f);
                row.anchoredPosition = new Vector2(0f, -60f - i * (rowH + 8f));
                row.sizeDelta = new Vector2(0f, rowH);
                var chip = UX.ElementChip(row, u.def.element, 44f);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(24f, 0f);
                string nm = u.Name + (u == mvp ? "  <color=#ffd23f>MVP</color>" : "") + (u.Alive ? "" : "  <color=#888>(besiegt)</color>");
                var name = UIKit.Text(row, "Name", nm, 26f, Color.white, TextAlignmentOptions.MidlineLeft, true);
                name.rectTransform.anchorMin = new Vector2(0f, 0f);
                name.rectTransform.anchorMax = new Vector2(0.36f, 1f);
                name.rectTransform.offsetMin = new Vector2(56f, 0f);
                name.rectTransform.offsetMax = Vector2.zero;
                name.enableAutoSizing = true;
                name.fontSizeMin = 14f;
                name.fontSizeMax = 26f;

                var dmg = UX.Bar(row, "Schaden", new Color(1f, 0.45f, 0.3f), new Color(1f, 1f, 1f, 0.06f));
                var dRt = (RectTransform)dmg.transform.parent;
                dRt.anchorMin = new Vector2(0.38f, 0.52f);
                dRt.anchorMax = new Vector2(1f, 0.98f);
                dRt.offsetMin = dRt.offsetMax = Vector2.zero;
                dmg.fillAmount = u.damageDealt / maxVal;
                var dT = UIKit.Text(dRt, "W", u.damageDealt.ToString("N0"), 20f, Color.white, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Stretch(dT.rectTransform, 10, 0, 0, 0);

                var heal = UX.Bar(row, "Heilung", new Color(0.4f, 1f, 0.6f), new Color(1f, 1f, 1f, 0.06f));
                var hRt = (RectTransform)heal.transform.parent;
                hRt.anchorMin = new Vector2(0.38f, 0.02f);
                hRt.anchorMax = new Vector2(1f, 0.48f);
                hRt.offsetMin = hRt.offsetMax = Vector2.zero;
                heal.fillAmount = u.healingDone / maxVal;
                var hT = UIKit.Text(hRt, "W", u.healingDone.ToString("N0"), 20f, Color.white, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Stretch(hT.rectTransform, 10, 0, 0, 0);
            }

            var cont = UIKit.Button(panel.transform, "Weiter", won ? "Weiter" : "Zurück", UX.Accent, () => { Sound.Play("klick"); onContinue(); }, 44f);
            UIKit.Place((RectTransform)cont.transform, new Vector2(0.5f, 0f), new Vector2(portrait ? 200f : 260f, 90f), new Vector2(360f, 110f));
            var retry = UIKit.Button(panel.transform, "Nochmal", "Nochmal", UX.Blue, () => { Sound.Play("klick"); onRetry(); }, 44f);
            UIKit.Place((RectTransform)retry.transform, new Vector2(0.5f, 0f), new Vector2(portrait ? -200f : -260f, 90f), new Vector2(360f, 110f));

            panel.rectTransform.localScale = Vector3.one * 0.7f;
            StartCoroutine(Pop(panel.rectTransform));
        }

        IEnumerator Pop(RectTransform rt)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                rt.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, 1f - (1f - t) * (1f - t));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }
    }
}
