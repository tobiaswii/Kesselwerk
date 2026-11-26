using System.Collections;
using System.Collections.Generic;
using Kesselwerk.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>
    /// Menüs: Start, Kampagne, Beschwörung, Sammlung (mit 3D-Vorschau), Team, Einstellungen.
    /// Alles im Code aufgebaut, passt sich an Hoch- und Querformat an.
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        MenuManager mm;
        RectTransform root, page, overlay, topBar, toastRt;
        TextMeshProUGUI goldText, crystalText, potionText, stoneText, toastText;
        CanvasGroup toastGroup;
        Button backButton;
        MenuPage current;
        Canvas canvas;
        CanvasScaler scaler;

        int campaignRegion;
        int selectedStage = -1;
        int selectedSlot = -1;
        Element? filter;
        CreatureDef detailDef;

        static PlayerData D => Game.Data;
        static bool P => UX.Portrait;

        public static MenuUI Instance { get; private set; }

        public void Init(MenuManager manager)
        {
            Instance = this;
            mm = manager;
            root = UX.CreateCanvas("Kesselwerk Menü", 10, out canvas, out scaler);
            canvas.GetComponent<OrientationScaler>().Changed += () => StartCoroutine(RebuildNextFrame());

            page = UIKit.Rect("Seite", root);
            UIKit.Stretch(page, 0, 0, 0, 150);
            BuildTopBar();
            overlay = UIKit.Rect("Overlay", root);
            UIKit.Stretch(overlay);
            overlay.gameObject.SetActive(false);
            BuildToast();

            for (int r = Campaign.RegionCount - 1; r >= 0; r--)
                if (Campaign.IsUnlocked(D, r, 0)) { campaignRegion = r; break; }
            Show(GameFlow.StartPage);
        }

        IEnumerator RebuildNextFrame()
        {
            yield return null;
            Show(current);
            if (detailDef != null && overlay.gameObject.activeSelf) ShowDetail(detailDef);
        }

        // ------------------------------------------------------------------
        // Rahmen: Kopfzeile, Hinweise
        // ------------------------------------------------------------------

        void BuildTopBar()
        {
            topBar = UIKit.Rect("Kopfzeile", root);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 140f);
            var bg = UIKit.Img(topBar, "Hintergrund", UIKit.Gradient, new Color(0.02f, 0.02f, 0.05f, 0.7f));
            UIKit.Stretch(bg.rectTransform, 0, -30, 0, 0);
            bg.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            backButton = UIKit.Button(topBar, "Zurueck", "‹ Zurück", UX.Grey, () => { Sound.Play("klick"); mm.HidePreview(); Show(MenuPage.Home); }, 34f);
            UIKit.Place((RectTransform)backButton.transform, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(210f, 90f));

            var row = UIKit.Rect("Waehrungen", topBar);
            row.anchorMin = new Vector2(1f, 0.5f);
            row.anchorMax = new Vector2(1f, 0.5f);
            row.pivot = new Vector2(1f, 0.5f);
            row.anchoredPosition = new Vector2(-14f, 0f);
            row.sizeDelta = new Vector2(760f, 80f);
            goldText = Currency(row, 0, "Gold", new Color(1f, 0.8f, 0.25f));
            crystalText = Currency(row, 1, "Kristalle", new Color(0.75f, 0.5f, 1f));
            potionText = Currency(row, 2, "Tränke", new Color(0.4f, 0.95f, 0.6f));
            stoneText = Currency(row, 3, "Steine", new Color(1f, 0.6f, 0.35f));
            RefreshTop();
        }

        TextMeshProUGUI Currency(RectTransform row, int i, string name, Color c)
        {
            var bg = UIKit.Img(row, name, UIKit.Rounded, new Color(0f, 0f, 0f, 0.55f));
            bg.rectTransform.anchorMin = bg.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            bg.rectTransform.pivot = new Vector2(0f, 0.5f);
            bg.rectTransform.anchoredPosition = new Vector2(i * 190f, 0f);
            bg.rectTransform.sizeDelta = new Vector2(180f, 66f);
            var gem = UIKit.Img(bg.transform, "Symbol", UIKit.Rounded, c);
            gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            gem.rectTransform.anchoredPosition = new Vector2(30f, 0f);
            gem.rectTransform.sizeDelta = new Vector2(30f, 30f);
            gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var t = UIKit.Text(bg.transform, "Wert", "0", 30f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(56f, 0f);
            t.rectTransform.offsetMax = new Vector2(-8f, 0f);
            t.enableAutoSizing = true;
            t.fontSizeMin = 16f;
            t.fontSizeMax = 30f;
            var lbl = UIKit.Text(bg.transform, "Name", name, 14f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.BottomRight);
            UIKit.Stretch(lbl.rectTransform, 0, 2, 10, 0);
            return t;
        }

        void RefreshTop()
        {
            goldText.text = D.gold.ToString("N0");
            crystalText.text = D.crystals.ToString("N0");
            potionText.text = D.potions.ToString();
            stoneText.text = D.evoStones.ToString();
        }

        void BuildToast()
        {
            toastRt = UIKit.Rect("Hinweis", root);
            UIKit.Place(toastRt, new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(960f, 100f));
            var bg = UIKit.Img(toastRt, "Hintergrund", UIKit.RoundedSoft, new Color(0.05f, 0.04f, 0.08f, 0.92f));
            UIKit.Stretch(bg.rectTransform, -16, -10, -16, -10);
            toastText = UIKit.Text(toastRt, "Text", "", 32f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(toastText.rectTransform, 12, 0, 12, 0);
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 16f;
            toastText.fontSizeMax = 32f;
            toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
        }

        Coroutine toastCo;

        public void Toast(string text)
        {
            if (toastCo != null) StopCoroutine(toastCo);
            toastCo = StartCoroutine(ToastRoutine(text));
        }

        IEnumerator ToastRoutine(string text)
        {
            toastRt.SetAsLastSibling();
            toastText.text = text;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.2f) { toastGroup.alpha = t; yield return null; }
            toastGroup.alpha = 1f;
            yield return new WaitForSeconds(2.4f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f) { toastGroup.alpha = 1f - t; yield return null; }
            toastGroup.alpha = 0f;
        }

        // ------------------------------------------------------------------
        // Seiten
        // ------------------------------------------------------------------

        public void Show(MenuPage p)
        {
            current = p;
            page.gameObject.SetActive(true);
            UX.Clear(page);
            if (p != MenuPage.Collection) { overlay.gameObject.SetActive(false); detailDef = null; mm.HidePreview(); }
            backButton.gameObject.SetActive(p != MenuPage.Home);
            RefreshTop();
            switch (p)
            {
                case MenuPage.Home: BuildHome(); break;
                case MenuPage.Campaign: BuildCampaign(); break;
                case MenuPage.Summon: BuildSummon(); break;
                case MenuPage.Collection: BuildCollection(); break;
                case MenuPage.Team: BuildTeam(); break;
                case MenuPage.Settings: BuildSettings(); break;
                case MenuPage.Arena: BuildArena(); break;
            }
        }

        RectTransform Area(float xMin, float yMin, float xMax, float yMax)
        {
            var a = UIKit.Rect("Bereich", page);
            UIKit.Anchor(a, xMin, yMin, xMax, yMax);
            a.offsetMin = a.offsetMax = Vector2.zero;
            return a;
        }

        TextMeshProUGUI Heading(Transform parent, string text, float size = 64f)
        {
            var t = UIKit.Text(parent, "Ueberschrift", text, size, Color.white, TextAlignmentOptions.Top, true);
            t.rectTransform.anchorMin = new Vector2(0f, 1f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.sizeDelta = new Vector2(0f, size * 1.3f);
            UX.Outline(t, 0.25f);
            return t;
        }

        Button Btn(Transform parent, string label, Color c, UnityEngine.Events.UnityAction a, float font = 40f)
        {
            return UIKit.Button(parent, label, label, c, () => { Sound.Play("klick"); a(); }, font);
        }

        // ---------------- Start ----------------

        void BuildHome()
        {
            var title = UIKit.Rect("Titel", page);
            if (P) UIKit.Anchor(title, 0f, 0.74f, 1f, 0.95f); else UIKit.Anchor(title, 0f, 0.62f, 0.55f, 0.95f);
            title.offsetMin = title.offsetMax = Vector2.zero;
            var t = UIKit.Text(title, "Logo", "KESSELWERK", 120f, UX.Gold, TextAlignmentOptions.Center, true);
            UIKit.Stretch(t.rectTransform, 0, 60, 0, 0);
            t.enableAutoSizing = true; t.fontSizeMin = 40; t.fontSizeMax = 130;
            UX.Outline(t, 0.3f);
            var sub = UIKit.Text(title, "Untertitel", "Monster sammeln · Team aufstellen · Automatisch kämpfen", 34f, Color.white, TextAlignmentOptions.Bottom);
            UIKit.Stretch(sub.rectTransform, 0, 0, 0, 0);
            UX.Outline(sub);

            var col = UIKit.Rect("Knoepfe", page);
            if (P) UIKit.Anchor(col, 0.06f, 0.02f, 0.94f, 0.42f); else UIKit.Anchor(col, 0.62f, 0.06f, 0.97f, 0.92f);
            col.offsetMin = col.offsetMax = Vector2.zero;

            var entries = new (string, Color, MenuPage?, System.Action)[]
            {
                ("Kampagne", UX.Accent, MenuPage.Campaign, null),
                ("Beschwören", new Color(0.55f, 0.3f, 0.85f), MenuPage.Summon, null),
                ("Sammlung", UX.Blue, MenuPage.Collection, null),
                ("Team", UX.Green, MenuPage.Team, null),
                ("Aufgaben", new Color(0.75f, 0.45f, 0.2f), null, ShowTasks),
                ("Arena (Online)", new Color(0.75f, 0.25f, 0.3f), MenuPage.Arena, null),
                ("Einstellungen", new Color(0.3f, 0.32f, 0.4f), MenuPage.Settings, null),
            };
            int cols = P ? 2 : 1;
            int rows = Mathf.CeilToInt(entries.Length / (float)cols);
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                int cx = i % cols, cy = i / cols;
                var b = Btn(col, e.Item1, e.Item2, () => { if (e.Item3.HasValue) Show(e.Item3.Value); else e.Item4(); }, P ? 46f : 42f);
                var rt = (RectTransform)b.transform;
                float w = 1f / cols, h = 1f / rows;
                rt.anchorMin = new Vector2(cx * w, 1f - (cy + 1) * h);
                rt.anchorMax = new Vector2((cx + 1) * w, 1f - cy * h);
                rt.offsetMin = new Vector2(10f, 8f);
                rt.offsetMax = new Vector2(-10f, -8f);
            }

            int open = 0;
            for (int i = 0; i < 3; i++) if (!D.taskClaimed[i] && Progress.TaskProgress(D, i) >= Progress.TaskGoal[i]) open++;
            if (open > 0) Toast($"{open} Aufgabe(n) abholbereit!");
        }

        public void ShowTasks()
        {
            var panel = OpenOverlay("Tägliche Aufgaben", new Vector2(900f, 900f));
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                int prog = Progress.TaskProgress(D, i), goal = Progress.TaskGoal[i];
                bool done = prog >= goal, claimed = D.taskClaimed[i];
                var row = UIKit.Img(panel, "Aufgabe", UIKit.Rounded, new Color(1f, 1f, 1f, 0.06f));
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -160f - i * 170f);
                row.rectTransform.sizeDelta = new Vector2(-60f, 150f);
                var tx = UIKit.Text(row.transform, "T", $"<b>{Progress.TaskNames[i]}</b>\n<size=75%>{Mathf.Min(prog, goal)} / {goal}  ·  Belohnung {Progress.TaskCrystals[i]} Kristalle</size>", 34f, Color.white, TextAlignmentOptions.MidlineLeft);
                UIKit.Stretch(tx.rectTransform, 30, 0, 300, 0);
                var b = Btn(row.transform, claimed ? "Erledigt" : done ? "Abholen" : "Offen", claimed ? UX.Grey : done ? UX.Green : UX.Grey,
                    () => { if (Progress.ClaimTask(D, idx)) { Game.Save(); Sound.Play("selten"); Toast($"+{Progress.TaskCrystals[idx]} Kristalle"); ShowTasks(); RefreshTop(); } }, 32f);
                b.interactable = done && !claimed;
                UIKit.Place((RectTransform)b.transform, new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(250f, 96f));
            }
            var streak = UIKit.Text(panel, "Serie", $"Login-Serie: {D.loginStreak} Tag(e) – jeden Tag mehr Kristalle (bis Tag 7).", 28f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center);
            streak.rectTransform.anchorMin = new Vector2(0f, 0f);
            streak.rectTransform.anchorMax = new Vector2(1f, 0f);
            streak.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            streak.rectTransform.sizeDelta = new Vector2(-60f, 80f);
        }

        // ---------------- Kampagne ----------------

        void BuildCampaign()
        {
            var head = Area(0f, 0.88f, 1f, 1f);
            Heading(head, "Kampagne", 56f);

            // Regionen
            var tabs = Area(0.04f, 0.8f, 0.96f, 0.88f);
            for (int r = 0; r < Campaign.RegionCount; r++)
            {
                int rr = r;
                bool unlocked = Campaign.IsUnlocked(D, r, 0);
                var b = Btn(tabs, unlocked ? Campaign.RegionNames[r] : Campaign.RegionNames[r] + "\n<size=65%>gesperrt</size>", r == campaignRegion ? UX.Accent : UX.Grey,
                    () => { campaignRegion = rr; selectedStage = -1; Show(MenuPage.Campaign); }, P ? 26f : 30f);
                b.interactable = unlocked;
                var rt = (RectTransform)b.transform;
                UIKit.Anchor(rt, r / (float)Campaign.RegionCount, 0f, (r + 1) / (float)Campaign.RegionCount, 1f);
                rt.offsetMin = new Vector2(8f, 6f);
                rt.offsetMax = new Vector2(-8f, -6f);
            }

            // Stufen als Pfad
            var path = P ? Area(0.04f, 0.47f, 0.96f, 0.79f) : Area(0.03f, 0.03f, 0.5f, 0.79f);
            var pbg = UX.Panel(path, "Karte", new Color(0.05f, 0.08f, 0.05f, 0.6f));
            UIKit.Stretch(pbg.rectTransform);
            var info = UIKit.Text(path, "Beschreibung", Campaign.RegionText[campaignRegion], 26f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Top);
            info.rectTransform.anchorMin = new Vector2(0f, 1f);
            info.rectTransform.anchorMax = new Vector2(1f, 1f);
            info.rectTransform.pivot = new Vector2(0.5f, 1f);
            info.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            info.rectTransform.sizeDelta = new Vector2(-40f, 70f);

            int n = Campaign.StagesPerRegion;
            int perRow = 4;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var s = Campaign.Get(campaignRegion, i);
                bool unlocked = Campaign.IsUnlocked(D, campaignRegion, i);
                bool cleared = D.regionProgress[campaignRegion] > i;
                int row = i / perRow, colI = i % perRow;
                if (row % 2 == 1) colI = perRow - 1 - colI; // Schlangenlinie
                float x = (colI + 0.5f) / perRow, y = 0.62f - row * 0.36f;
                Color c = !unlocked ? new Color(0.25f, 0.25f, 0.28f) : cleared ? new Color(0.25f, 0.6f, 0.3f) : (s.boss ? new Color(0.8f, 0.25f, 0.2f) : UX.Accent);
                var node = UIKit.Button(path, "Stufe" + i, s.boss ? "BOSS" : (i + 1).ToString(), c, () => { if (unlocked) { selectedStage = idx; Sound.Play("klick"); Show(MenuPage.Campaign); } }, s.boss ? 30f : 44f);
                var rt = (RectTransform)node.transform;
                float size = s.boss ? 150f : s.elite ? 130f : 115f;
                UIKit.Place(rt, new Vector2(x, y), Vector2.zero, new Vector2(size, size));
                node.GetComponent<Image>().sprite = UIKit.Circle;
                if (i == selectedStage)
                {
                    var ring = UIKit.Img(path, "Auswahl", UIKit.Glow, new Color(1f, 0.9f, 0.4f, 0.9f));
                    UIKit.Place(ring.rectTransform, new Vector2(x, y), Vector2.zero, new Vector2(size + 80f, size + 80f));
                    ring.transform.SetSiblingIndex(rt.GetSiblingIndex());
                }
                if (s.elite)
                {
                    var el = UIKit.Text(rt, "Elite", "Elite", 20f, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Bottom, true);
                    UIKit.Stretch(el.rectTransform, 0, -26, 0, 0);
                    UX.Outline(el);
                }
            }
            if (selectedStage < 0)
            {
                int next = Mathf.Min(D.regionProgress[campaignRegion], n - 1);
                selectedStage = next;
                Show(MenuPage.Campaign);
                return;
            }

            // Details zur gewählten Stufe
            var det = P ? Area(0.04f, 0.02f, 0.96f, 0.46f) : Area(0.52f, 0.03f, 0.97f, 0.79f);
            var dbg = UX.Panel(det, "Details", UX.PanelDark);
            UIKit.Stretch(dbg.rectTransform);
            var stage = Campaign.Get(campaignRegion, selectedStage);
            var dh = Heading(det, $"Stufe {stage.name}", 44f);
            dh.rectTransform.anchoredPosition = new Vector2(0f, -16f);

            var team = D.BuildTeam();
            int myPower = TeamPower(team), enemyPower = TeamPower(stage.enemy);
            string pc = myPower >= enemyPower ? "#7dffb0" : myPower >= enemyPower * 0.8f ? "#ffd23f" : "#ff7a6b";
            var pw = UIKit.Text(det, "Kraft", $"Deine Kraft <color={pc}><b>{myPower:N0}</b></color>   ·   Gegner <b>{enemyPower:N0}</b>", 28f, Color.white, TextAlignmentOptions.Top);
            pw.rectTransform.anchorMin = new Vector2(0f, 1f);
            pw.rectTransform.anchorMax = new Vector2(1f, 1f);
            pw.rectTransform.pivot = new Vector2(0.5f, 1f);
            pw.rectTransform.anchoredPosition = new Vector2(0f, -78f);
            pw.rectTransform.sizeDelta = new Vector2(-40f, 40f);

            // Gegnerliste
            var list = UIKit.Rect("Gegner", det);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 1f);
            list.anchoredPosition = new Vector2(0f, -130f);
            list.sizeDelta = new Vector2(-40f, 260f);
            var units = stage.enemy.units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var d = CreatureDb.Get(u.id);
                int cx = i % 3, cy = i / 3;
                var cell = UIKit.Img(list, "Gegner", UIKit.Rounded, d.isBoss ? new Color(0.5f, 0.12f, 0.1f, 0.8f) : new Color(1f, 1f, 1f, 0.06f));
                UIKit.Anchor(cell.rectTransform, cx / 3f, 1f - (cy + 1) * 0.5f, (cx + 1) / 3f, 1f - cy * 0.5f);
                cell.rectTransform.offsetMin = new Vector2(6f, 6f);
                cell.rectTransform.offsetMax = new Vector2(-6f, -6f);
                var chip = UX.ElementChip(cell.transform, d.element, 52f);
                chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                chip.anchoredPosition = new Vector2(38f, 0f);
                var nt = UIKit.Text(cell.transform, "N", $"<b>{d.StageName(u.stage)}</b>\n<size=75%>Stufe {u.level} · {(u.slot < 3 ? "vorne" : "hinten")}</size>", 24f, Color.white, TextAlignmentOptions.MidlineLeft);
                UIKit.Stretch(nt.rectTransform, 72, 0, 6, 0);
                nt.enableAutoSizing = true; nt.fontSizeMin = 12; nt.fontSizeMax = 24;
            }

            bool first = D.regionProgress[campaignRegion] <= selectedStage;
            string rw = $"Belohnung: <color=#ffd23f>{stage.gold} Gold</color>, <color=#9fd3ff>{stage.xp} EP</color>" +
                        (first ? $", <color=#d79bff>{stage.crystalsFirst} Kristalle</color>, {stage.potions} Tränke" + (stage.stones > 0 ? $", {stage.stones} Steine" : "") + " (erstmalig)" : "");
            var rwt = UIKit.Text(det, "Belohnung", rw, 26f, Color.white, TextAlignmentOptions.Center);
            rwt.rectTransform.anchorMin = new Vector2(0f, 0f);
            rwt.rectTransform.anchorMax = new Vector2(1f, 0f);
            rwt.rectTransform.anchoredPosition = new Vector2(0f, 175f);
            rwt.rectTransform.sizeDelta = new Vector2(-40f, 70f);

            var fight = Btn(det, "Kämpfen!", new Color(0.85f, 0.3f, 0.15f), () =>
            {
                if (D.TeamCount() == 0) { Toast("Stelle zuerst ein Team auf!"); return; }
                Game.PendingStage = stage;
                GameFlow.ToBattle();
            }, 50f);
            UIKit.Place((RectTransform)fight.transform, new Vector2(0.5f, 0f), new Vector2(150f, 75f), new Vector2(380f, 110f));
            var teamB = Btn(det, "Team", UX.Green, () => Show(MenuPage.Team), 40f);
            UIKit.Place((RectTransform)teamB.transform, new Vector2(0.5f, 0f), new Vector2(-180f, 75f), new Vector2(240f, 110f));
        }

        public void SelectStage(int region, int index)
        {
            campaignRegion = region;
            selectedStage = index;
            Show(MenuPage.Campaign);
        }

        static int TeamPower(TeamSpec t)
        {
            int p = 0;
            foreach (var u in t.units) p += Stats.Power(CreatureDb.Get(u.id), u);
            return p;
        }

        // ---------------- Beschwörung ----------------

        void BuildSummon()
        {
            var area = P ? Area(0.05f, 0.03f, 0.95f, 0.97f) : Area(0.2f, 0.04f, 0.8f, 0.96f);
            var bg = UX.Panel(area, "Portal", new Color(0.12f, 0.06f, 0.2f, 0.85f));
            UIKit.Stretch(bg.rectTransform);
            var glow = UIKit.Img(area, "Leuchten", UIKit.Glow, new Color(0.7f, 0.4f, 1f, 0.5f));
            UIKit.Place(glow.rectTransform, new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(900f, 900f));
            StartCoroutine(Pulse(glow.rectTransform));
            Heading(area, "Kesselbeschwörung", 60f).rectTransform.anchoredPosition = new Vector2(0f, -30f);

            var txt = UIKit.Text(area, "Text",
                "Wirf Kristalle in den Kessel und beschwöre neue Kreaturen.\nDoppelte Kreaturen werden zu Kopien für die <b>Fusion</b> (mehr Sterne).", 30f, Color.white, TextAlignmentOptions.Center);
            UIKit.Place(txt.rectTransform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(860f, 200f));

            int toRare = Mathf.Max(1, Summon.PityRare - D.pityRare);
            int toLeg = Mathf.Max(1, Summon.PityLegend - D.pityLegend);
            var pity = UIKit.Text(area, "Garantie",
                $"Garantiert <color=#5aa0ff>Selten+</color> in spätestens <b>{toRare}</b> Beschwörung(en)\nGarantiert <color=#ffb84a>Legendär+</color> in spätestens <b>{toLeg}</b>", 28f, new Color(1f, 0.95f, 0.8f), TextAlignmentOptions.Center);
            UIKit.Place(pity.rectTransform, new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(860f, 120f));

            var one = Btn(area, $"1x  ({Summon.CostSingle})", new Color(0.45f, 0.3f, 0.85f), () => DoSummon(1), 40f);
            UIKit.Place((RectTransform)one.transform, new Vector2(0.5f, 0.22f), new Vector2(-220f, 0f), new Vector2(380f, 130f));
            var ten = Btn(area, $"10x  ({Summon.CostTen})", new Color(0.85f, 0.5f, 0.15f), () => DoSummon(10), 40f);
            UIKit.Place((RectTransform)ten.transform, new Vector2(0.5f, 0.22f), new Vector2(220f, 0f), new Vector2(380f, 130f));
            one.interactable = D.crystals >= Summon.CostSingle;
            ten.interactable = D.crystals >= Summon.CostTen;

            var rates = Btn(area, "Wahrscheinlichkeiten", UX.Grey, ShowRates, 30f);
            UIKit.Place((RectTransform)rates.transform, new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(460f, 90f));
        }

        IEnumerator Pulse(RectTransform rt)
        {
            while (rt != null)
            {
                rt.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 2f) * 0.06f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Time.time * 10f);
                yield return null;
            }
        }

        public void ShowRates()
        {
            var panel = OpenOverlay("Wahrscheinlichkeiten", new Vector2(860f, 1100f));
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Summon.Rates.Length; i++)
            {
                var r = (Rarity)i;
                var names = new List<string>();
                foreach (var d in CreatureDb.OfRarity(r)) names.Add(d.Name);
                sb.Append($"<color={Look.Hex(Look.RarityColor(r))}><b>{Look.RarityName(r)}  {Summon.Rates[i]:0.0} %</b></color>\n<size=70%>{string.Join(", ", names)}</size>\n\n");
            }
            sb.Append($"<size=75%>Spätestens jede {Summon.PityRare}. Beschwörung ist mindestens Selten, spätestens jede {Summon.PityLegend}. mindestens Legendär. Innerhalb einer Seltenheit sind alle Kreaturen gleich wahrscheinlich.</size>");
            var t = UIKit.Text(panel, "Tabelle", sb.ToString(), 32f, Color.white, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(t.rectTransform, 50, 150, 50, 140);
            t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 32;
        }

        Coroutine summonCo;

        public void DoSummon(int count)
        {
            if (!Summon.Pull(D, count, new Kesselwerk.Core.Rng(System.Environment.TickCount ^ D.summons * 7919), out var res, out var err))
            {
                Toast(err);
                return;
            }
            Game.Save();
            RefreshTop();
            if (summonCo != null) StopCoroutine(summonCo);
            summonCo = StartCoroutine(SummonSequence(res));
        }

        IEnumerator SummonSequence(List<SummonResult> res)
        {
            overlay.gameObject.SetActive(true);
            UX.Clear(overlay);
            var dim = UIKit.Img(overlay, "Abdunkeln", null, new Color(0.02f, 0.01f, 0.05f, 0.9f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var halo = UIKit.Img(overlay, "Leuchten", UIKit.Glow, new Color(0.6f, 0.35f, 1f, 0.35f));
            UIKit.Place(halo.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1600f, 1600f));
            StartCoroutine(Pulse(halo.rectTransform));
            Sound.Play("beschwoerung");

            // Karten austeilen (Rückseiten)
            int n = res.Count;
            int cols = n == 1 ? 1 : (P ? 3 : 5);
            int rows = Mathf.CeilToInt(n / (float)cols);
            float scale = n == 1 ? 1.7f : (P ? 1.0f : 1.05f);
            float cw = CardView.W * scale * 1.1f, ch = CardView.H * scale * 1.2f;
            var backs = new List<RectTransform>();
            var targets = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                int cx = i % cols, cy = i / cols;
                int inRow = Mathf.Min(cols, n - cy * cols);
                var pos = new Vector2((cx - (inRow - 1) * 0.5f) * cw, ((rows - 1) * 0.5f - cy) * ch + 70f);
                targets.Add(pos);
                var back = UIKit.Rect("Rueckseite", overlay);
                back.sizeDelta = new Vector2(CardView.W, CardView.H);
                back.localScale = Vector3.one * scale;
                var bimg = UIKit.Img(back, "Rahmen", UIKit.Rounded, Color.Lerp(Look.RarityColor(res[i].def.rarity), new Color(0.2f, 0.15f, 0.3f), 0.75f));
                UIKit.Stretch(bimg.rectTransform);
                var inner = UIKit.Img(back, "Innen", UIKit.Rounded, new Color(0.15f, 0.1f, 0.25f));
                UIKit.Stretch(inner.rectTransform, 10, 10, 10, 10);
                var sym = UIKit.Text(back, "K", "K", 120f, new Color(1f, 0.82f, 0.38f, 0.8f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(sym.rectTransform);
                back.anchoredPosition = new Vector2(0f, -900f);
                backs.Add(back);
            }
            for (int i = 0; i < n; i++)
            {
                StartCoroutine(MoveTo(backs[i], targets[i], 0.3f));
                yield return new WaitForSeconds(0.06f);
            }
            yield return new WaitForSeconds(0.4f);

            // Umdrehen
            for (int i = 0; i < n; i++)
            {
                var r = res[i];
                yield return Flip(backs[i], 0.12f, true);
                var v = CardView.Create(overlay, r.def, D.Get(r.def.id));
                v.rt.anchoredPosition = targets[i];
                v.rt.localScale = new Vector3(0f, scale, 1f);
                v.button.interactable = false;
                Destroy(backs[i].gameObject);
                if (r.def.rarity >= Rarity.Selten)
                {
                    var g = UIKit.Img(v.rt, "Seltenheit", UIKit.Glow, Look.RarityColor(r.def.rarity));
                    UIKit.Stretch(g.rectTransform, -60, -60, -60, -60);
                    g.transform.SetAsFirstSibling();
                    Sound.Play("selten", 0.8f, r.def.rarity >= Rarity.Legendaer ? 0.8f : 1f);
                }
                else Sound.Play("klick", 0.8f, 1.3f);
                var tag = UIKit.Text(v.rt, "Marke", r.isNew ? "NEU!" : "+1 Kopie", 30f, r.isNew ? UX.Gold : new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Center, true);
                tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                tag.rectTransform.anchoredPosition = new Vector2(0f, 22f);
                tag.rectTransform.sizeDelta = new Vector2(220f, 44f);
                UX.Outline(tag, 0.3f);
                yield return ScaleX(v.rt, scale, 0.12f);
                yield return new WaitForSeconds(n == 1 ? 0.1f : 0.04f);
            }

            // Beste Kreatur in 3D zeigen
            SummonResult best = res[0];
            foreach (var r in res) if (r.def.rarity > best.def.rarity || (r.def.rarity == best.def.rarity && r.isNew && !best.isNew)) best = r;

            var showBtn = Btn(overlay, "3D ansehen", UX.Blue, () => StartCoroutine(Reveal3D(best)), 36f);
            UIKit.Place((RectTransform)showBtn.transform, new Vector2(0.5f, 0f), new Vector2(-220f, 110f), new Vector2(360f, 110f));
            var ok = Btn(overlay, "Weiter", UX.Accent, () => { overlay.gameObject.SetActive(false); Show(MenuPage.Summon); }, 40f);
            UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(220f, 110f), new Vector2(360f, 110f));

            if (best.def.rarity >= Rarity.Episch) yield return Reveal3D(best);
        }

        IEnumerator Reveal3D(SummonResult r)
        {
            overlay.gameObject.SetActive(false);
            UX.Clear(page);
            mm.ShowPreview(r.def, D.Get(r.def.id)?.stage ?? 0, true);
            var cap = UIKit.Text(page, "Name", $"<color={Look.Hex(Look.RarityColor(r.def.rarity))}>{Look.RarityName(r.def.rarity).ToUpper()}</color>\n<size=130%>{r.def.Name}</size>", 56f, Color.white, TextAlignmentOptions.Bottom, true);
            UIKit.Anchor(cap.rectTransform, 0f, 0.06f, 1f, 0.3f);
            cap.rectTransform.offsetMin = cap.rectTransform.offsetMax = Vector2.zero;
            UX.Outline(cap, 0.3f);
            yield return new WaitForSeconds(0.4f);
            var ok = Btn(page, "Weiter", UX.Accent, () => { mm.HidePreview(); Show(MenuPage.Summon); }, 40f);
            UIKit.Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(360f, 110f));
        }

        IEnumerator MoveTo(RectTransform rt, Vector2 to, float time)
        {
            Vector2 from = rt.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                if (rt == null) yield break;
                float k = 1f - (1f - t) * (1f - t);
                rt.anchoredPosition = Vector2.Lerp(from, to, k);
                rt.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 25f);
                yield return null;
            }
            if (rt != null) { rt.anchoredPosition = to; rt.localRotation = Quaternion.identity; }
        }

        IEnumerator Flip(RectTransform rt, float time, bool close)
        {
            Vector3 s = rt.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                rt.localScale = new Vector3(s.x * (close ? 1f - t : t), s.y, 1f);
                yield return null;
            }
        }

        IEnumerator ScaleX(RectTransform rt, float target, float time)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                rt.localScale = new Vector3(target * t, target, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one * target;
        }

        // ---------------- Sammlung ----------------

        void BuildCollection()
        {
            var head = Area(0f, 0.9f, 1f, 1f);
            int owned = 0, total = 0;
            foreach (var d in CreatureDb.All) if (!d.isBoss) { total++; if (D.Owns(d.id)) owned++; }
            Heading(head, $"Sammlung  <size=60%>{owned} / {total} entdeckt</size>", 52f);

            // Elementfilter
            var bar = Area(0.02f, 0.84f, 0.98f, 0.9f);
            int chips = 11;
            for (int i = 0; i < chips; i++)
            {
                Element? e = i == 0 ? (Element?)null : (Element)(i - 1);
                bool on = filter == e;
                var c = i == 0 ? UX.Grey : Look.ElementColor((Element)(i - 1));
                var b = UIKit.Button(bar, "Filter", i == 0 ? "Alle" : Look.ElementShort((Element)(i - 1)), on ? c : Color.Lerp(c, Color.black, 0.6f),
                    () => { filter = e; Sound.Play("klick"); Show(MenuPage.Collection); }, 24f);
                var rt = (RectTransform)b.transform;
                UIKit.Anchor(rt, i / (float)chips, 0f, (i + 1) / (float)chips, 1f);
                rt.offsetMin = new Vector2(3f, 4f);
                rt.offsetMax = new Vector2(-3f, -4f);
            }

            // Raster mit Karten
            var area = Area(0.02f, 0.01f, 0.98f, 0.83f);
            var scroll = MakeScroll(area, out var content);
            var list = new List<CreatureDef>();
            foreach (var d in CreatureDb.All) if (!d.isBoss && (filter == null || d.element == filter)) list.Add(d);
            list.Sort((a, b) => D.Owns(a.id) != D.Owns(b.id) ? (D.Owns(a.id) ? -1 : 1) : b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : string.Compare(a.Name, b.Name));

            Canvas.ForceUpdateCanvases();
            float width = area.rect.width;
            int cols = P ? 3 : 6;
            float cellW = width / cols;
            float scale = cellW * 0.92f / CardView.W;
            float cellH = CardView.H * scale + 20f;
            content.sizeDelta = new Vector2(0f, Mathf.CeilToInt(list.Count / (float)cols) * cellH + 20f);
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                var oc = D.Get(d.id);
                var v = CardView.Create(content, d, oc, oc == null);
                v.rt.anchorMin = v.rt.anchorMax = new Vector2(0f, 1f);
                v.rt.anchoredPosition = new Vector2((i % cols + 0.5f) * cellW, -(i / cols + 0.5f) * cellH - 10f);
                v.rt.localScale = Vector3.one * scale;
                v.button.onClick.AddListener(() => { Sound.Play("klick"); ShowDetail(d); });
                if (oc != null && (Progress.CanFuse(D, oc) || (oc.level >= Progress.EvolveLevel && oc.stage < d.MaxStage)))
                {
                    var dot = UIKit.Img(v.rt, "Hinweis", UIKit.Circle, new Color(1f, 0.3f, 0.25f));
                    UIKit.Place(dot.rectTransform, new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(34f, 34f));
                }
                if (oc != null && System.Array.IndexOf(D.team, d.id) >= 0)
                {
                    var tt = UIKit.Text(v.rt, "ImTeam", "IM TEAM", 20f, new Color(0.5f, 1f, 0.6f), TextAlignmentOptions.Center, true);
                    tt.rectTransform.anchorMin = tt.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                    tt.rectTransform.anchoredPosition = new Vector2(0f, -4f);
                    tt.rectTransform.sizeDelta = new Vector2(200f, 30f);
                    UX.Outline(tt, 0.3f);
                }
            }
        }

        ScrollRect MakeScroll(RectTransform area, out RectTransform content)
        {
            var view = UIKit.Img(area, "Ansicht", UIKit.Rounded, new Color(0f, 0f, 0f, 0.35f));
            UIKit.Stretch(view.rectTransform);
            view.raycastTarget = true;
            view.gameObject.AddComponent<RectMask2D>();
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f;
            content = UIKit.Rect("Inhalt", view.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            sr.content = content;
            sr.viewport = view.rectTransform;
            return sr;
        }

        public void ShowDetail(CreatureDef d)
        {
            detailDef = d;
            var oc = D.Get(d.id);
            UX.Clear(overlay);
            overlay.gameObject.SetActive(true);
            page.gameObject.SetActive(false); // freie Sicht auf die 3D-Vorschau
            if (oc != null) { if (mm.PreviewDef != d || mm.PreviewStage != oc.stage) mm.ShowPreview(d, oc.stage); }
            else mm.HidePreview();

            // Ziehfläche zum Drehen der 3D-Vorschau
            var drag = UIKit.Img(overlay, "Drehen", null, new Color(0f, 0f, 0f, oc != null ? 0.01f : 0.6f));
            UIKit.Stretch(drag.rectTransform);
            drag.raycastTarget = true;
            var rot = drag.gameObject.AddComponent<DragRotate>();
            rot.onDrag = dx => mm.RotatePreview(-dx * 0.4f);

            var panel = UX.Panel(overlay, "Info", new Color(0.06f, 0.05f, 0.09f, 0.94f));
            if (P) UIKit.Anchor(panel.rectTransform, 0.02f, 0.01f, 0.98f, 0.5f); else UIKit.Anchor(panel.rectTransform, 0.02f, 0.02f, 0.46f, 0.86f);
            panel.rectTransform.offsetMin = panel.rectTransform.offsetMax = Vector2.zero;
            UX.Frame(panel, Look.RarityColor(d.rarity), 3f);

            string title = oc != null ? oc.Name : (d.Name + "  <size=60%>(nicht entdeckt)</size>");
            var spec = oc != null ? D.SpecFor(oc, 0) : new UnitSpec(d.id, 1, 0);
            var sb = new System.Text.StringBuilder();
            sb.Append($"<size=130%><b>{title}</b></size>\n");
            sb.Append($"<color={Look.Hex(Look.ElementColor(d.element))}>{d.element}</color>  ·  <color={Look.Hex(Look.RarityColor(d.rarity))}>{Look.RarityName(d.rarity)}</color>  ·  {Look.RoleName(d.role)}  ·  {(d.ranged ? "Fernkampf" : "Nahkampf")}\n");
            if (oc != null) sb.Append($"Stufe <b>{oc.level}</b>/{Stats.MaxLevel(oc.stars)}  <color=#ffd23f>{Look.Stars(oc.stars)}</color>  ·  Kopien {oc.copies}  ·  Entwicklung {oc.stage + 1}/{d.MaxStage + 1}\n");
            sb.Append($"<size=90%>Leben <b>{Stats.Hp(d, spec)}</b>   Angriff <b>{Stats.Atk(d, spec)}</b>   Verteidigung <b>{Stats.Def(d, spec)}</b>   Tempo <b>{Stats.Spd(d, spec)}</b>   Krit <b>{d.crit * 100f:0}%</b></size>\n\n");
            sb.Append($"<color=#ffd23f><b>Spezial: {d.skill.name}</b></color>\n<size=90%>{d.skill.text}</size>\n");
            if (d.passive != Passive.Keine) sb.Append($"<color=#9fd3ff><b>Passiv</b></color>\n<size=90%>{CreatureDef.PassiveText(d.passive)}</size>\n");
            var strong = ElementChart.StrongAgainst(d.element);
            var sn = new List<string>();
            foreach (var e in strong) sn.Add(e.ToString());
            sb.Append($"<size=85%>Stark gegen: {string.Join(", ", sn)}</size>\n");
            sb.Append($"<i><size=85%><color=#c9b99a>{d.description}</color></size></i>");
            var info = UIKit.Text(panel.transform, "Text", sb.ToString(), 30f, Color.white, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(info.rectTransform, 36, oc != null ? 340 : 140, 36, 30);
            info.enableAutoSizing = true; info.fontSizeMin = 14; info.fontSizeMax = 32;

            if (oc != null)
            {
                // EP-Balken
                var xp = UX.Bar(panel.transform, "EP", new Color(0.4f, 0.75f, 1f), new Color(1f, 1f, 1f, 0.08f));
                var xr = (RectTransform)xp.transform.parent;
                xr.anchorMin = new Vector2(0f, 0f);
                xr.anchorMax = new Vector2(1f, 0f);
                xr.anchoredPosition = new Vector2(0f, 230f);
                xr.sizeDelta = new Vector2(-72f, 26f);
                bool maxed = oc.level >= Stats.MaxLevel(oc.stars);
                xp.fillAmount = maxed ? 1f : oc.xp / (float)Progress.XpToNext(oc.level);
                var xt = UIKit.Text(xr, "W", maxed ? "Höchststufe – Fusion erhöht die Grenze" : $"{oc.xp} / {Progress.XpToNext(oc.level)} EP", 18f, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Stretch(xt.rectTransform);

                // Ausrüstung: drei Plätze
                var gearRow = UIKit.Rect("Ausruestung", panel.transform);
                gearRow.anchorMin = new Vector2(0f, 0f);
                gearRow.anchorMax = new Vector2(1f, 0f);
                gearRow.pivot = new Vector2(0.5f, 0f);
                gearRow.anchoredPosition = new Vector2(0f, 258f);
                gearRow.sizeDelta = new Vector2(-40f, 78f);
                for (int gi = 0; gi < 3; gi++)
                {
                    var slot = (GearSlot)gi;
                    int uid = slot == GearSlot.Waffe ? oc.weaponUid : slot == GearSlot.Ruestung ? oc.armorUid : oc.amuletUid;
                    var g = D.GetGear(uid);
                    string label = g != null
                        ? $"<size=70%>{GearDef.SlotName(slot)}</size>\n<color={Look.Hex(Look.RarityColor(g.Def.rarity))}>{g.Def.name}</color> {Roman(g.level)}"
                        : $"<size=70%>{GearDef.SlotName(slot)}</size>\n<color=#9aa>– leer –</color>";
                    var gb = Btn(gearRow, label, new Color(0.18f, 0.2f, 0.28f), () => ShowGearPicker(d, slot), 26f);
                    var grt = (RectTransform)gb.transform;
                    UIKit.Anchor(grt, gi / 3f, 0f, (gi + 1) / 3f, 1f);
                    grt.offsetMin = new Vector2(6f, 0f);
                    grt.offsetMax = new Vector2(-6f, 0f);
                }

                var row = UIKit.Rect("Aktionen", panel.transform);
                row.anchorMin = new Vector2(0f, 0f);
                row.anchorMax = new Vector2(1f, 0f);
                row.pivot = new Vector2(0.5f, 0f);
                row.anchoredPosition = new Vector2(0f, 110f);
                row.sizeDelta = new Vector2(-40f, 100f);
                AddAction(row, 0, $"Trank ({D.potions})", new Color(0.25f, 0.6f, 0.4f), () =>
                {
                    if (Progress.UsePotion(D, oc, out var err)) { Game.Save(); Sound.Play("heilung"); } else Toast(err);
                    ShowDetail(d); RefreshTop();
                });
                bool canStar = oc.stars < Stats.MaxStars;
                AddAction(row, 1, canStar ? $"Fusion {oc.copies}/{Progress.CopiesForStar(oc.stars)}" : "Max. Sterne", new Color(0.8f, 0.6f, 0.15f), () =>
                {
                    if (Progress.Fuse(D, oc, out var err)) { Game.Save(); Sound.Play("selten"); Toast($"{oc.Name} hat jetzt {oc.stars} Sterne!"); mm.ShowPreview(d, oc.stage, true); }
                    else Toast(err + $" (Gold: {Progress.GoldForStar(oc.stars)})");
                    ShowDetail(d); RefreshTop();
                });
                AddAction(row, 2, oc.stage < d.MaxStage ? "Entwickeln" : "Entwickelt", new Color(0.6f, 0.3f, 0.85f), () =>
                {
                    if (Progress.Evolve(D, oc, out var err)) { Game.Save(); Sound.Play("selten"); Toast($"Entwickelt zu {oc.Name}!"); mm.ShowPreview(d, oc.stage, true); }
                    else Toast(err);
                    ShowDetail(d); RefreshTop();
                });
            }

            var close = Btn(panel.transform, "Schließen", UX.Grey, () => { overlay.gameObject.SetActive(false); detailDef = null; mm.HidePreview(); Show(MenuPage.Collection); }, 34f);
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(300f, 80f));
        }

        static string Roman(int n) => n <= 1 ? "" : n == 2 ? "II" : n == 3 ? "III" : n == 4 ? "IV" : "V";

        public void DebugGearPicker(CreatureDef d, GearSlot slot) { ShowDetail(d); ShowGearPicker(d, slot); }

        /// <summary>Auswahl eines Gegenstands für einen Ausrüstungsplatz, inkl. Aufwerten.</summary>
        void ShowGearPicker(CreatureDef d, GearSlot slot)
        {
            var oc = D.Get(d.id);
            if (oc == null) return;
            UX.Clear(overlay);
            overlay.gameObject.SetActive(true);
            var dim = UIKit.Img(overlay, "Abdunkeln", null, new Color(0f, 0f, 0f, 0.7f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UX.Panel(overlay, "Ausruestung", new Color(0.07f, 0.06f, 0.1f, 0.97f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, P ? new Vector2(1000f, 1500f) : new Vector2(1300f, 900f));
            UX.Frame(panel, new Color(0.5f, 0.85f, 1f), 3f);
            Heading(panel.transform, $"{GearDef.SlotName(slot)} für {oc.Name}", 44f).rectTransform.anchoredPosition = new Vector2(0f, -20f);
            var info = UIKit.Text(panel.transform, "Gold", $"Gold: <color=#ffd23f>{D.gold:N0}</color>   ·   Ausrüstung findest du in der Kampagne (Elite und Boss garantiert)", 24f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Top);
            info.rectTransform.anchorMin = new Vector2(0f, 1f);
            info.rectTransform.anchorMax = new Vector2(1f, 1f);
            info.rectTransform.pivot = new Vector2(0.5f, 1f);
            info.rectTransform.anchoredPosition = new Vector2(0f, -80f);
            info.rectTransform.sizeDelta = new Vector2(-40f, 40f);

            var area = UIKit.Rect("Liste", panel.transform);
            UIKit.Stretch(area, 30, 140, 30, 130);
            MakeScroll(area, out var content);
            var list = D.gear.FindAll(g => g.Def.slot == slot);
            list.Sort((a, b) => b.Def.rarity != a.Def.rarity ? b.Def.rarity.CompareTo(a.Def.rarity) : b.level.CompareTo(a.level));
            int current = slot == GearSlot.Waffe ? oc.weaponUid : slot == GearSlot.Ruestung ? oc.armorUid : oc.amuletUid;
            float rowH = 120f;
            content.sizeDelta = new Vector2(0f, Mathf.Max(1, list.Count) * (rowH + 10f) + 10f);
            if (list.Count == 0)
            {
                var none = UIKit.Text(content, "Leer", "Noch keine Gegenstände für diesen Platz.", 30f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
                none.rectTransform.anchorMin = new Vector2(0f, 1f);
                none.rectTransform.anchorMax = new Vector2(1f, 1f);
                none.rectTransform.pivot = new Vector2(0.5f, 1f);
                none.rectTransform.sizeDelta = new Vector2(0f, 120f);
            }
            for (int i = 0; i < list.Count; i++)
            {
                var g = list[i];
                var gd = g.Def;
                var row = UIKit.Img(content, "Gegenstand", UIKit.Rounded, g.uid == current ? new Color(0.2f, 0.35f, 0.25f, 0.9f) : new Color(1f, 1f, 1f, 0.06f));
                row.rectTransform.anchorMin = new Vector2(0f, 1f);
                row.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.rectTransform.pivot = new Vector2(0.5f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -10f - i * (rowH + 10f));
                row.rectTransform.sizeDelta = new Vector2(-20f, rowH);
                var gem = UIKit.Img(row.transform, "Stein", UIKit.Rounded, Look.RarityColor(gd.rarity));
                UIKit.Place(gem.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(40f, 40f));
                gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var wearer = D.WearerOf(g.uid);
                string w = wearer == null ? "" : wearer == oc ? "  <color=#7dffb0>(angelegt)</color>" : $"  <color=#bbbbcc>(trägt {wearer.Name})</color>";
                var t = UIKit.Text(row.transform, "Text",
                    $"<b><color={Look.Hex(Look.RarityColor(gd.rarity))}>{gd.name}</color></b> <size=80%>Stufe {g.level}/{GearDb.MaxLevel}</size>{w}\n<size=78%>{GearDb.Bonus(gd.id, g.level).Describe()}</size>\n<size=70%><i><color=#c9b99a>{gd.text}</color></i></size>",
                    26f, Color.white, TextAlignmentOptions.MidlineLeft);
                UIKit.Stretch(t.rectTransform, 84, 4, 440, 4);
                t.enableAutoSizing = true; t.fontSizeMin = 12; t.fontSizeMax = 26;
                var eq = Btn(row.transform, g.uid == current ? "Ablegen" : "Anlegen", g.uid == current ? UX.Grey : UX.Green, () =>
                {
                    if (g.uid == current) Progress.Unequip(oc, slot);
                    else if (!Progress.Equip(D, oc, g.uid, out var err)) Toast(err);
                    Game.Save();
                    ShowGearPicker(d, slot);
                }, 26f);
                UIKit.Place((RectTransform)eq.transform, new Vector2(1f, 0.5f), new Vector2(-330f, 0f), new Vector2(190f, 84f));
                bool max = g.level >= GearDb.MaxLevel;
                var up = Btn(row.transform, max ? "Max." : $"Aufwerten\n<size=75%>{Progress.GearUpgradeCost(g)} Gold</size>", new Color(0.8f, 0.6f, 0.15f), () =>
                {
                    if (Progress.UpgradeGear(D, g, out var err)) { Game.Save(); Sound.Play("selten"); }
                    else Toast(err);
                    RefreshTop();
                    ShowGearPicker(d, slot);
                }, 24f);
                up.interactable = !max;
                UIKit.Place((RectTransform)up.transform, new Vector2(1f, 0.5f), new Vector2(-115f, 0f), new Vector2(210f, 84f));
            }

            var back = Btn(panel.transform, "Zurück", UX.Grey, () => ShowDetail(d), 32f);
            UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 65f), new Vector2(300f, 84f));
        }

        void AddAction(RectTransform row, int i, string label, Color c, UnityEngine.Events.UnityAction a)
        {
            var b = Btn(row, label, c, a, 28f);
            var rt = (RectTransform)b.transform;
            UIKit.Anchor(rt, i / 3f, 0f, (i + 1) / 3f, 1f);
            rt.offsetMin = new Vector2(6f, 0f);
            rt.offsetMax = new Vector2(-6f, 0f);
        }

        // ---------------- Team ----------------

        void BuildTeam()
        {
            var head = Area(0f, 0.9f, 1f, 1f);
            Heading(head, "Teamaufstellung", 52f);

            var grid = P ? Area(0.04f, 0.56f, 0.96f, 0.9f) : Area(0.02f, 0.3f, 0.5f, 0.9f);
            var gbg = UX.Panel(grid, "Formation", new Color(0.05f, 0.1f, 0.05f, 0.55f));
            UIKit.Stretch(gbg.rectTransform);
            // Gegner wären oben: vordere Reihe oben anzeigen
            for (int s = 0; s < 6; s++)
            {
                int slot = s;
                int col = s % 3, row = s / 3;
                var cell = UIKit.Rect("Feld" + s, grid);
                UIKit.Anchor(cell, col / 3f, row == 0 ? 0.5f : 0f, (col + 1) / 3f, row == 0 ? 1f : 0.5f);
                cell.offsetMin = new Vector2(10f, 10f);
                cell.offsetMax = new Vector2(-10f, -34f);
                var bg = UIKit.Img(cell, "Grund", UIKit.Rounded, s == selectedSlot ? new Color(1f, 0.8f, 0.3f, 0.5f) : new Color(1f, 1f, 1f, 0.08f));
                UIKit.Stretch(bg.rectTransform);
                bg.raycastTarget = true;
                var btn = bg.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    Sound.Play("klick");
                    if (selectedSlot == slot && D.team[slot] != null) { D.SetTeamSlot(slot, null); Game.Save(); mm.SpawnPals(); }
                    selectedSlot = selectedSlot == slot ? -1 : slot;
                    Show(MenuPage.Team);
                });
                var oc = D.Get(D.team[s]);
                if (oc != null)
                {
                    var portrait = MonsterModel.Portrait(oc.Def);
                    var raw = UIKit.Rect("Bild", cell).gameObject.AddComponent<RawImage>();
                    UIKit.Stretch(raw.rectTransform, 6, 44, 6, 6);
                    raw.texture = portrait;
                    raw.uvRect = new Rect(0.1f, 0.15f, 0.8f, 0.75f);
                    raw.raycastTarget = false;
                    var chip = UX.ElementChip(cell, oc.Def.element, 40f);
                    chip.anchorMin = chip.anchorMax = new Vector2(0f, 1f);
                    chip.anchoredPosition = new Vector2(26f, -26f);
                    var nm = UIKit.Text(cell, "Name", $"{oc.Name} <size=80%>Lv{oc.level}</size>", 24f, Color.white, TextAlignmentOptions.Bottom, true);
                    UIKit.Stretch(nm.rectTransform, 4, 4, 4, 0);
                    nm.enableAutoSizing = true; nm.fontSizeMin = 12; nm.fontSizeMax = 24;
                    UX.Outline(nm);
                    if (slot == selectedSlot)
                    {
                        var rm = UIKit.Text(cell, "Entfernen", "nochmal tippen = entfernen", 16f, new Color(1f, 0.8f, 0.6f), TextAlignmentOptions.Top);
                        UIKit.Stretch(rm.rectTransform, 0, 0, 0, -22);
                    }
                }
                else
                {
                    var plus = UIKit.Text(cell, "Leer", "+", 80f, new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Center, true);
                    UIKit.Stretch(plus.rectTransform);
                }
                if (col == 0)
                {
                    var rl = UIKit.Text(grid, "Reihe", row == 0 ? "VORNE (Nahkampf, Tanks)" : "HINTEN (Fernkampf, Heiler)", 22f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.TopLeft, true);
                    UIKit.Anchor(rl.rectTransform, 0f, row == 0 ? 0.5f : 0f, 1f, row == 0 ? 1f : 0.5f);
                    rl.rectTransform.offsetMin = new Vector2(16f, 0f);
                    rl.rectTransform.offsetMax = new Vector2(0f, -4f);
                }
            }

            // Synergien und Kraft
            var syn = P ? Area(0.04f, 0.42f, 0.96f, 0.555f) : Area(0.02f, 0.02f, 0.5f, 0.29f);
            var sbg = UX.Panel(syn, "Synergien", UX.PanelDark);
            UIKit.Stretch(sbg.rectTransform);
            var team = D.BuildTeam();
            var list = Synergies.For(team);
            var sb = new System.Text.StringBuilder($"<b>Kraft {TeamPower(team):N0}</b>   ·   {D.TeamCount()}/6 Kreaturen\n");
            if (list.Count == 0) sb.Append("<size=80%><color=#bbbbcc>Keine Synergien. Tipp: 2–3 Kreaturen gleichen Elements oder Tank + Heiler.</color></size>");
            foreach (var s in list) sb.Append($"<size=80%><color=#ffd23f>{s.name}</color>: {s.text}</size>   ");
            var st = UIKit.Text(syn, "T", sb.ToString(), 28f, Color.white, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(st.rectTransform, 24, 10, 24, 10);
            st.enableAutoSizing = true; st.fontSizeMin = 14; st.fontSizeMax = 28;

            // Auswahl
            var pick = P ? Area(0.02f, 0.11f, 0.98f, 0.41f) : Area(0.52f, 0.12f, 0.98f, 0.9f);
            MakeScroll(pick, out var content);
            Canvas.ForceUpdateCanvases();
            var owned = new List<OwnedCreature>(D.creatures);
            owned.Sort((a, b) => Stats.Power(b.Def, b.ToSpec(0)).CompareTo(Stats.Power(a.Def, a.ToSpec(0))));
            int cols = P ? 4 : 4;
            float cellW = pick.rect.width / cols;
            float scale = cellW * 0.9f / CardView.W;
            float cellH = CardView.H * scale + 14f;
            content.sizeDelta = new Vector2(0f, Mathf.CeilToInt(owned.Count / (float)cols) * cellH + 14f);
            for (int i = 0; i < owned.Count; i++)
            {
                var oc = owned[i];
                var v = CardView.Create(content, oc.Def, oc);
                v.rt.anchorMin = v.rt.anchorMax = new Vector2(0f, 1f);
                v.rt.anchoredPosition = new Vector2((i % cols + 0.5f) * cellW, -(i / cols + 0.5f) * cellH - 7f);
                v.rt.localScale = Vector3.one * scale;
                bool inTeam = System.Array.IndexOf(D.team, oc.id) >= 0;
                if (inTeam) v.group.alpha = 0.45f;
                v.button.onClick.AddListener(() =>
                {
                    Sound.Play("klick");
                    int slot = selectedSlot;
                    if (slot < 0)
                    {
                        // freies Feld passend zur Rolle suchen
                        bool front = oc.Def.role == Role.Tank || (!oc.Def.ranged && oc.Def.role == Role.Angreifer);
                        int[] order = front ? new[] { 1, 0, 2, 4, 3, 5 } : new[] { 4, 3, 5, 1, 0, 2 };
                        foreach (var o in order) if (D.Get(D.team[o]) == null) { slot = o; break; }
                    }
                    if (slot < 0) { Toast("Team ist voll – tippe zuerst auf ein Feld."); return; }
                    D.SetTeamSlot(slot, oc.id);
                    selectedSlot = -1;
                    Game.Save();
                    mm.SpawnPals();
                    Show(MenuPage.Team);
                });
            }

            var auto = Btn(page, "Auto-Aufstellung", UX.Blue, AutoTeam, 32f);
            var ar = (RectTransform)auto.transform;
            if (P) UIKit.Place(ar, new Vector2(0.5f, 0f), new Vector2(-240f, 70f), new Vector2(420f, 100f));
            else UIKit.Place(ar, new Vector2(0.75f, 0f), new Vector2(-240f, 60f), new Vector2(420f, 90f));
            var fight = Btn(page, "Zur Kampagne", UX.Accent, () => Show(MenuPage.Campaign), 32f);
            var fr = (RectTransform)fight.transform;
            if (P) UIKit.Place(fr, new Vector2(0.5f, 0f), new Vector2(240f, 70f), new Vector2(420f, 100f));
            else UIKit.Place(fr, new Vector2(0.75f, 0f), new Vector2(240f, 60f), new Vector2(420f, 90f));
        }

        void AutoTeam()
        {
            var list = new List<OwnedCreature>(D.creatures);
            list.Sort((a, b) => Stats.Power(b.Def, b.ToSpec(0)).CompareTo(Stats.Power(a.Def, a.ToSpec(0))));
            for (int i = 0; i < 6; i++) D.team[i] = null;
            int f = 0, bk = 0;
            int[] front = { 1, 0, 2 }, back = { 4, 3, 5 };
            foreach (var c in list)
            {
                if (f + bk >= 6) break;
                bool wantFront = c.Def.role == Role.Tank || (!c.Def.ranged && c.Def.role == Role.Angreifer);
                if (wantFront && f < 3) D.team[front[f++]] = c.id;
                else if (!wantFront && bk < 3) D.team[back[bk++]] = c.id;
                else if (f < 3) D.team[front[f++]] = c.id;
                else if (bk < 3) D.team[back[bk++]] = c.id;
            }
            Game.Save();
            mm.SpawnPals();
            Show(MenuPage.Team);
            Toast("Stärkste Kreaturen aufgestellt.");
        }

        // ---------------- Arena (online) ----------------

        OnlineMatch currentMatch;
        List<OnlineBoardEntry> board;

        static Color RankColor(string rank)
        {
            switch (rank)
            {
                case "Silber": return new Color(0.8f, 0.82f, 0.88f);
                case "Gold": return new Color(1f, 0.8f, 0.3f);
                case "Platin": return new Color(0.5f, 0.95f, 0.9f);
                case "Diamant": return new Color(0.5f, 0.75f, 1f);
                case "Meister": return new Color(0.8f, 0.45f, 1f);
                case "Legende": return new Color(1f, 0.4f, 0.35f);
                default: return new Color(0.85f, 0.55f, 0.3f);
            }
        }

        void BuildArena()
        {
            var head = Area(0f, 0.9f, 1f, 1f);
            Heading(head, "Arena  <size=55%>Online gegen echte Spieler</size>", 52f);

            var left = P ? Area(0.03f, 0.46f, 0.97f, 0.89f) : Area(0.02f, 0.03f, 0.56f, 0.89f);
            var lbg = UX.Panel(left, "Profil", UX.PanelDark);
            UIKit.Stretch(lbg.rectTransform);

            if (!Online.HasAccount)
            {
                var t = UIKit.Text(left, "Info",
                    "<b>So funktioniert die Arena</b>\n\n• Du stellst ein <b>Verteidigungsteam</b> auf – andere Spieler kämpfen dagegen, auch wenn du offline bist.\n" +
                    "• Du greifst Teams anderer Spieler an. Den Kampf berechnet der <b>Server</b>, so ist Schummeln ausgeschlossen.\n" +
                    "• Siege bringen Wertung: Bronze, Silber, Gold, Platin, Diamant, Meister, Legende.\n\nDafür brauchst du ein Online-Konto (ohne E-Mail, nur ein Name).",
                    30f, Color.white, TextAlignmentOptions.TopLeft);
                UIKit.Stretch(t.rectTransform, 40, 160, 40, 40);
                t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 30;
                var reg = Btn(left, "Online-Konto anlegen", UX.Accent, ArenaRegister, 38f);
                UIKit.Place((RectTransform)reg.transform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(520f, 110f));
                BuildBoard();
                return;
            }

            var status = UIKit.Text(left, "Status", "Verbinde mit dem Server …", 30f, Color.white, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(status.rectTransform, 40, 380, 40, 30);
            status.enableAutoSizing = true; status.fontSizeMin = 16; status.fontSizeMax = 32;
            Online.Profile(me =>
            {
                if (status == null) return;
                string c = Look.Hex(RankColor(me.rang));
                status.text = $"<size=120%><b>{me.name}</b></size>\n" +
                              $"<color={c}><b>{me.rang}</b></color>  ·  Wertung <b>{me.wertung}</b>  ·  Platz <b>{me.platz}</b>\n" +
                              $"Angriffe: {me.siege} Siege / {me.niederlagen} Niederlagen\n" +
                              $"Verteidigung: {me.verteidigungSiege} gehalten / {me.verteidigungNiederlagen} verloren" +
                              (me.verteidigung == 0 ? "\n<color=#ffb36b>Noch kein Verteidigungsteam gesetzt!</color>" : "");
            }, e => { if (status != null) status.text = $"<color=#ff8a6b>{e}</color>\n<size=80%>Läuft der Server? {Online.BaseUrl}</size>"; });

            // Gegnerbereich
            var opp = UIKit.Rect("Gegner", left);
            opp.anchorMin = new Vector2(0f, 0f);
            opp.anchorMax = new Vector2(1f, 0f);
            opp.pivot = new Vector2(0.5f, 0f);
            opp.anchoredPosition = new Vector2(0f, 150f);
            opp.sizeDelta = new Vector2(-40f, 220f);
            if (currentMatch != null)
            {
                var m = currentMatch;
                var ot = UIKit.Text(opp, "Name", $"Gegner: <b>{m.gegner}</b>  <color={Look.Hex(RankColor(m.rang))}>{m.rang}</color>  ({m.wertung})", 28f, Color.white, TextAlignmentOptions.TopLeft);
                ot.rectTransform.anchorMin = new Vector2(0f, 1f);
                ot.rectTransform.anchorMax = new Vector2(1f, 1f);
                ot.rectTransform.pivot = new Vector2(0.5f, 1f);
                ot.rectTransform.sizeDelta = new Vector2(0f, 44f);
                for (int i = 0; i < m.units.Count; i++)
                {
                    var u = m.units[i];
                    var d = CreatureDb.Get(u.id);
                    if (d == null) continue;
                    var cell = UIKit.Img(opp, "Einheit", UIKit.Rounded, new Color(1f, 1f, 1f, 0.07f));
                    UIKit.Anchor(cell.rectTransform, (i % 3) / 3f, i < 3 ? 0.4f : 0f, (i % 3 + 1) / 3f, i < 3 ? 0.78f : 0.38f);
                    cell.rectTransform.offsetMin = new Vector2(4f, 2f);
                    cell.rectTransform.offsetMax = new Vector2(-4f, -2f);
                    var chip = UX.ElementChip(cell.transform, d.element, 40f);
                    chip.anchorMin = chip.anchorMax = new Vector2(0f, 0.5f);
                    chip.anchoredPosition = new Vector2(28f, 0f);
                    var nt = UIKit.Text(cell.transform, "N", $"{d.StageName(u.stage)} <size=80%>Lv{u.level}</size>", 22f, Color.white, TextAlignmentOptions.MidlineLeft, true);
                    UIKit.Stretch(nt.rectTransform, 54, 0, 4, 0);
                    nt.enableAutoSizing = true; nt.fontSizeMin = 11; nt.fontSizeMax = 22;
                }
            }

            var row = UIKit.Rect("Knoepfe", left);
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 30f);
            row.sizeDelta = new Vector2(-40f, 100f);
            AddAction(row, 0, "Verteidigung\n<size=70%>= aktuelles Team</size>", UX.Green, ArenaDefense);
            AddAction(row, 1, currentMatch == null ? "Gegner suchen" : "Anderer Gegner", UX.Blue, ArenaFind);
            AddAction(row, 2, "Angreifen!", new Color(0.85f, 0.3f, 0.15f), ArenaAttack);

            BuildBoard();
        }

        // --- Arena-Aktionen (auch für Testbefehle erreichbar) ---
        public void ArenaRegister()
        {
            string name = D.playerName == "Kesselmeister" ? "Kesselmeister" + Random.Range(100, 999) : D.playerName;
            Online.Register(name, r =>
            {
                D.playerName = r.name;
                Game.Save();
                Online.SetDefense(D.BuildTeam(), null, null);
                Toast($"Willkommen in der Arena, {r.name}!");
                Show(MenuPage.Arena);
            }, e => Toast(e));
        }

        public void ArenaDefense()
        {
            if (D.TeamCount() == 0) { Toast("Stelle zuerst ein Team auf."); return; }
            Online.SetDefense(D.BuildTeam(), () => { Toast("Verteidigungsteam gespeichert."); Show(MenuPage.Arena); }, e => Toast(e));
        }

        public void ArenaFind() =>
            Online.FindMatch(m => { currentMatch = m; Show(MenuPage.Arena); }, e => Toast(e));

        public void ArenaAttack()
        {
            if (currentMatch == null) { Toast("Suche zuerst einen Gegner."); return; }
            if (D.TeamCount() == 0) { Toast("Stelle zuerst ein Team auf."); return; }
            var m = currentMatch;
            currentMatch = null;
            Online.Fight(m.matchId, D.BuildTeam(), f =>
            {
                Game.PendingArena = new ArenaFight
                {
                    enemy = Online.ToTeam(f.gegnerUnits), seed = f.seed, opponent = m.gegner,
                    serverWinner = f.sieger, delta = f.aenderung, rating = f.wertung, rank = f.rang
                };
                GameFlow.ToBattle();
            }, e => { Toast(e); Show(MenuPage.Arena); });
        }

        void BuildBoard()
        {
            var right = P ? Area(0.03f, 0.02f, 0.97f, 0.45f) : Area(0.58f, 0.03f, 0.98f, 0.89f);
            var rbg = UX.Panel(right, "Rangliste", UX.PanelDark);
            UIKit.Stretch(rbg.rectTransform);
            var h = UIKit.Text(right, "Titel", "Rangliste", 38f, UX.Gold, TextAlignmentOptions.Top, true);
            h.rectTransform.anchorMin = new Vector2(0f, 1f);
            h.rectTransform.anchorMax = new Vector2(1f, 1f);
            h.rectTransform.pivot = new Vector2(0.5f, 1f);
            h.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            h.rectTransform.sizeDelta = new Vector2(0f, 50f);
            var list = UIKit.Text(right, "Liste", "Lade …", 26f, Color.white, TextAlignmentOptions.TopLeft);
            UIKit.Stretch(list.rectTransform, 30, 20, 30, 74);
            list.enableAutoSizing = true; list.fontSizeMin = 12; list.fontSizeMax = 26;
            Online.Leaderboard(entries =>
            {
                if (list == null) return;
                board = entries;
                if (entries.Count == 0) { list.text = "Noch niemand in der Rangliste – sei der Erste!"; return; }
                var sb = new System.Text.StringBuilder();
                foreach (var e in entries)
                {
                    if (e.platz > 15) break;
                    sb.Append($"<b>{e.platz}.</b> {e.name}  <color={Look.Hex(RankColor(e.rang))}>{e.rang}</color>  <color=#aab>{e.wertung}</color>\n");
                }
                list.text = sb.ToString();
            }, e => { if (list != null) list.text = $"<color=#ff8a6b>{e}</color>"; });
        }

        // ---------------- Einstellungen ----------------

        void BuildSettings()
        {
            var area = P ? Area(0.05f, 0.03f, 0.95f, 0.97f) : Area(0.15f, 0.03f, 0.85f, 0.97f);
            var bg = UX.Panel(area, "Einstellungen", UX.PanelDark);
            UIKit.Stretch(bg.rectTransform);
            Heading(area, "Einstellungen", 56f).rectTransform.anchoredPosition = new Vector2(0f, -20f);

            Slider(area, 0, "Musik", Sound.MusicVolume, v => { Sound.MusicVolume = v; Game.Save(); });
            Slider(area, 1, "Effekte", Sound.SfxVolume, v => { Sound.SfxVolume = v; Game.Save(); });

            var spd = Btn(area, $"Kampftempo: {D.battleSpeed}x", UX.Blue, () => { D.battleSpeed = D.battleSpeed >= 3 ? 1 : D.battleSpeed + 1; Game.Save(); Show(MenuPage.Settings); }, 32f);
            UIKit.Place((RectTransform)spd.transform, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(520f, 84f));

            int owned = D.creatures.Count;
            var prof = UIKit.Text(area, "Profil",
                $"<b>Profil: {D.playerName}</b>\nKämpfe {D.battles}  ·  Siege {D.wins}  ·  Beschwörungen {D.summons}  ·  Kreaturen {owned}\n" +
                $"Kampagne: {Campaign.RegionNames[0]} {D.regionProgress[0]}/{Campaign.StagesPerRegion}, {Campaign.RegionNames[1]} {D.regionProgress[1]}/{Campaign.StagesPerRegion}",
                28f, Color.white, TextAlignmentOptions.Center);
            UIKit.Place(prof.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -600f), new Vector2(900f, 130f));
            prof.enableAutoSizing = true; prof.fontSizeMin = 14; prof.fontSizeMax = 28;

            var credits = UIKit.Text(area, "Credits",
                "<size=80%>3D-Modelle: Quaternius (CC0) · Klänge und Musik: im Spiel erzeugt (Platzhalter)\nServer: " + Online.BaseUrl + "</size>",
                26f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
            UIKit.Place(credits.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 168f), new Vector2(900f, 76f));

            var up = Btn(area, "In Cloud sichern", UX.Blue, () =>
            {
                if (!Online.HasAccount) { Toast("Zuerst in der Arena ein Online-Konto anlegen."); return; }
                Online.UploadSave(() => Toast("Spielstand in der Cloud gesichert."), e => Toast(e));
            }, 28f);
            UIKit.Place((RectTransform)up.transform, new Vector2(0.5f, 1f), new Vector2(-230f, -490f), new Vector2(420f, 84f));
            var down = Btn(area, "Aus Cloud laden", new Color(0.3f, 0.4f, 0.6f), () =>
            {
                if (!Online.HasAccount) { Toast("Zuerst in der Arena ein Online-Konto anlegen."); return; }
                Online.DownloadSave(json =>
                {
                    if (Game.Import(json)) { Toast("Cloud-Spielstand geladen."); mm.SpawnPals(); Show(MenuPage.Settings); }
                    else Toast("Cloud-Spielstand ist ungültig.");
                }, e => Toast(e));
            }, 28f);
            UIKit.Place((RectTransform)down.transform, new Vector2(0.5f, 1f), new Vector2(230f, -490f), new Vector2(420f, 84f));

            var reset = Btn(area, "Spielstand zurücksetzen", UX.Red, () =>
            {
                var panel = OpenOverlay("Wirklich zurücksetzen?", new Vector2(820f, 560f));
                var t = UIKit.Text(panel, "T", "Alle Kreaturen, Fortschritte und Währungen gehen verloren.", 32f, Color.white, TextAlignmentOptions.Center);
                UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(700f, 160f));
                var yes = Btn(panel, "Ja, zurücksetzen", UX.Red, () => { Game.Reset(); overlay.gameObject.SetActive(false); mm.SpawnPals(); Show(MenuPage.Home); Toast("Neuer Spielstand angelegt."); }, 32f);
                UIKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(460f, 100f));
            }, 30f);
            UIKit.Place((RectTransform)reset.transform, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(520f, 84f));
        }

        void Slider(RectTransform parent, int i, string label, float value, System.Action<float> onChange)
        {
            var row = UIKit.Rect(label, parent);
            row.anchorMin = new Vector2(0.1f, 1f);
            row.anchorMax = new Vector2(0.9f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -150f - i * 130f);
            row.sizeDelta = new Vector2(0f, 110f);
            var t = UIKit.Text(row, "Name", label, 34f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            UIKit.Anchor(t.rectTransform, 0f, 0f, 0.3f, 1f);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;

            var track = UIKit.Img(row, "Spur", UIKit.Rounded, new Color(1f, 1f, 1f, 0.12f));
            UIKit.Anchor(track.rectTransform, 0.32f, 0.35f, 1f, 0.65f);
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;
            var fillArea = UIKit.Rect("Fuellbereich", track.transform);
            UIKit.Stretch(fillArea);
            var fill = UIKit.Img(fillArea, "Fuellung", UIKit.Rounded, UX.Accent);
            UIKit.Stretch(fill.rectTransform);
            var handleArea = UIKit.Rect("Griffbereich", track.transform);
            UIKit.Stretch(handleArea, 20, 0, 20, 0);
            var handle = UIKit.Img(handleArea, "Griff", UIKit.Circle, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(60f, 60f);
            var s = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange(v));
        }

        // ------------------------------------------------------------------
        // Overlay-Fenster
        // ------------------------------------------------------------------

        RectTransform OpenOverlay(string title, Vector2 size)
        {
            overlay.gameObject.SetActive(true);
            UX.Clear(overlay);
            var dim = UIKit.Img(overlay, "Abdunkeln", null, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UX.Panel(overlay, "Fenster", new Color(0.08f, 0.07f, 0.12f, 0.97f));
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UX.Frame(panel, UX.Gold, 3f);
            Heading(panel.transform, title, 48f).rectTransform.anchoredPosition = new Vector2(0f, -24f);
            var close = Btn(panel.transform, "Schließen", UX.Grey, () => { overlay.gameObject.SetActive(false); Show(current); }, 32f);
            UIKit.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 84f));
            return panel.rectTransform;
        }
    }

    /// <summary>Wandelt Ziehen in eine Drehung der 3D-Vorschau um.</summary>
    public class DragRotate : MonoBehaviour, IDragHandler
    {
        public System.Action<float> onDrag;
        public void OnDrag(PointerEventData e) => onDrag?.Invoke(e.delta.x);
    }
}
