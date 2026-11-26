using System.Collections.Generic;
using Kesselwerk.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>
    /// Kesselschlacht – Planungsphase: Laden, Bank, Wabenfeld (Ziehen und Ablegen), Spielerliste.
    /// </summary>
    public class TftManager : MonoBehaviour
    {
        Camera cam;
        Material[] tiles;
        readonly Dictionary<int, Creature> views = new Dictionary<int, Creature>();
        readonly Dictionary<int, RectTransform> starTags = new Dictionary<int, RectTransform>();
        RectTransform root, tagLayer, shopRow, playerList, synergyCol;
        TextMeshProUGUI goldText, levelText, roundText, fieldText, toastText;
        Image xpFill;
        Button lockBtn;
        CanvasGroup toastGroup;
        float toastUntil;

        TftGame G => TftSession.Current;
        TftPlayer Me => TftSession.Me;

        // Ziehen
        TftUnit dragUnit;
        Creature dragView;
        Vector2 dragStart;
        bool dragging;

        void Start()
        {
            Application.targetFrameRate = 60;
            if (TftSession.Current == null && !TftSession.TryLoad()) TftSession.NewGame();
            cam = ArenaBuilder.SetupCameraAndLight();
            ArenaBuilder.BuildArena(true, true);
            ArenaBuilder.ApplyRegionMood(TftSession.Region);
            WeatherFx.DressRegion(TftSession.Region);
            tiles = ArenaBuilder.BuildHexBoard(true);
            GrassField.Create(TftSession.Region, true, true);
            BuildUi();
            Sync();
            Refresh();
            Sound.Music(true);
            if (!string.IsNullOrEmpty(TftSession.Report)) Toast(TftSession.Report);
            if (G.Over) ShowGameOver();
        }

        // ------------------------------------------------------------------
        // 3D-Ansicht
        // ------------------------------------------------------------------

        Vector3 PosOf(TftUnit u, out bool onBench)
        {
            int b = System.Array.IndexOf(Me.bench, u);
            onBench = b >= 0;
            if (onBench) return ArenaBuilder.BenchPos(b);
            int c = System.Array.IndexOf(Me.board, u);
            return ArenaBuilder.HexWorld(Hex.ToBoard(c, 0));
        }

        /// <summary>3D-Monster an die Daten anpassen (neue erzeugen, alte entfernen, alle an ihren Platz).</summary>
        void Sync()
        {
            var alive = new HashSet<int>();
            foreach (var u in Me.AllUnits())
            {
                alive.Add(u.uid);
                var pos = PosOf(u, out bool bench);
                if (!views.TryGetValue(u.uid, out var v) || v == null)
                {
                    v = Creature.Spawn(u.Def, 0, pos, Vector3.forward, true, 0, 0.8f + 0.12f * (u.star - 1));
                    views[u.uid] = v;
                    StartCoroutine(v.SpawnAnim());
                    if (u.star > 1) Fx.Burst(pos + Vector3.up, new Color(3f, 2.4f, 0.6f), 50, 5f, 0.25f, 0.8f, 0.5f);
                    MakeTag(u);
                }
                v.transform.position = pos;
                v.home = pos;
                v.SetHomeFacing(Vector3.forward);
            }
            var dead = new List<int>();
            foreach (var kv in views) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (int id in dead)
            {
                if (views[id] != null) Destroy(views[id].gameObject);
                views.Remove(id);
                if (starTags.TryGetValue(id, out var t) && t != null) Destroy(t.gameObject);
                starTags.Remove(id);
            }
        }

        void MakeTag(TftUnit u)
        {
            var tag = UIKit.Rect("Sterne", tagLayer);
            tag.sizeDelta = new Vector2(80f, 20f);
            for (int i = 0; i < u.star; i++)
            {
                var d = UIKit.Img(tag, "Stern", null, u.star == 3 ? new Color(1f, 0.8f, 0.2f) : u.star == 2 ? new Color(0.8f, 0.85f, 0.95f) : new Color(0.75f, 0.5f, 0.3f));
                d.rectTransform.sizeDelta = new Vector2(14f, 14f);
                d.rectTransform.anchoredPosition = new Vector2((i - (u.star - 1) * 0.5f) * 18f, 0f);
                d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            starTags[u.uid] = tag;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            bool portrait = UX.Portrait;
            cam.fieldOfView = portrait ? 60f : 40f;
            float pitch = portrait ? 60f : 52f;
            Vector3 look = portrait ? new Vector3(0f, 0f, -3.4f) : new Vector3(0f, 0f, -3.3f);
            float dist = portrait ? 25f : 19f;
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(look - rot * Vector3.forward * dist, rot);
            ArenaBuilder.FocusAt(dist);

            // Sterne über den Monstern
            var canvasRt = (RectTransform)tagLayer;
            foreach (var kv in starTags)
            {
                if (!views.TryGetValue(kv.Key, out var v) || v == null || kv.Value == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(v.Top);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out var lp);
                kv.Value.anchoredPosition = lp;
                kv.Value.gameObject.SetActive(sp.z > 0f);
            }

            if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.unscaledTime < toastUntil ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }

        // ------------------------------------------------------------------
        // Ziehen und Ablegen (über eine unsichtbare Fläche hinter der Oberfläche)
        // ------------------------------------------------------------------

        void PointerDown(Vector2 sp)
        {
            dragUnit = null; dragging = false;
            float best = 90f * (Screen.height / 1080f);
            foreach (var u in Me.AllUnits())
            {
                if (!views.TryGetValue(u.uid, out var v) || v == null) continue;
                Vector3 p = cam.WorldToScreenPoint(v.Center);
                float d = Vector2.Distance(sp, p);
                if (p.z > 0 && d < best) { best = d; dragUnit = u; dragView = v; }
            }
            dragStart = sp;
        }

        void PointerDrag(Vector2 sp)
        {
            if (dragUnit == null) return;
            if (!dragging && Vector2.Distance(sp, dragStart) > 8f) { dragging = true; HighlightTiles(true); }
            if (!dragging) return;
            if (Ground(sp, out var w)) dragView.transform.position = w + Vector3.up * 0.4f;
        }

        void PointerUp(Vector2 sp)
        {
            if (dragUnit == null) return;
            HighlightTiles(false);
            if (!dragging)
            {
                // Antippen: zeigt Infos
                var d = dragUnit.Def;
                Toast($"{d.Name}  <size=80%>{Tft.Cost(d)} Gold · {d.element} · {Look.RoleName(d.role)} · {dragUnit.star} Stern(e)</size>");
                dragUnit = null;
                return;
            }
            dragging = false;
            // Über dem Laden losgelassen → verkaufen
            if (shopRow != null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)shopRow.parent, sp, null))
            {
                int val = Tft.SellValue(dragUnit);
                G.Sell(Me, dragUnit);
                Sound.Play("klick");
                Toast($"Verkauft für {val} Gold");
            }
            else if (Ground(sp, out var w))
            {
                int bestCell = -1; float bd = 1.1f; bool bench = false;
                for (int i = 0; i < Hex.SideCells; i++)
                {
                    float d = Vector3.Distance(w, ArenaBuilder.HexWorld(Hex.ToBoard(i, 0)));
                    if (d < bd) { bd = d; bestCell = i; bench = false; }
                }
                for (int i = 0; i < Tft.BenchSize; i++)
                {
                    float d = Vector3.Distance(w, ArenaBuilder.BenchPos(i));
                    if (d < bd) { bd = d; bestCell = i; bench = true; }
                }
                if (bestCell >= 0 && !G.Move(Me, dragUnit, bench, bestCell, out var err)) Toast(err);
                else Sound.Play("klick", 0.6f, 1.3f);
            }
            dragUnit = null;
            Sync();
            Refresh();
        }

        bool Ground(Vector2 sp, out Vector3 w)
        {
            var ray = cam.ScreenPointToRay(sp);
            w = Vector3.zero;
            if (Mathf.Abs(ray.direction.y) < 0.001f) return false;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) return false;
            w = ray.origin + ray.direction * t;
            return true;
        }

        void HighlightTiles(bool on)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                Color c = on ? (Me.board[i] == null ? ArenaBuilder.OwnTileHi : ArenaBuilder.OwnTileUsed) : ArenaBuilder.OwnTile;
                tiles[i].SetColor("_BaseColor", c); tiles[i].color = c;
            }
        }

        // ------------------------------------------------------------------
        // Oberfläche
        // ------------------------------------------------------------------

        void BuildUi()
        {
            root = UX.CreateCanvas("KesselschlachtUI", 10, out _, out _);

            // Unsichtbare Fläche für Ziehen/Antippen im 3D-Bereich
            var field = UIKit.Img(root, "Spielflaeche", null, new Color(0f, 0f, 0f, 0f));
            UIKit.Stretch(field.rectTransform);
            field.raycastTarget = true;
            var fp = field.gameObject.AddComponent<FieldPointer>();
            fp.down = PointerDown; fp.drag = PointerDrag; fp.up = PointerUp;

            tagLayer = UIKit.Rect("Sterne", root);
            UIKit.Stretch(tagLayer);

            // Kopf: Titel und Runde
            var head = UIKit.Rect("Kopf", root);
            head.anchorMin = new Vector2(0f, 1f); head.anchorMax = new Vector2(0.6f, 1f); head.pivot = new Vector2(0f, 1f);
            head.sizeDelta = new Vector2(0f, 110f); head.anchoredPosition = Vector2.zero;
            var t = UIKit.Text(head, "Titel", "KESSELSCHLACHT", 34f, Color.white, TextAlignmentOptions.TopLeft, true);
            t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = new Vector2(1f, 1f); t.rectTransform.pivot = new Vector2(0f, 1f);
            t.rectTransform.anchoredPosition = new Vector2(30f, -18f); t.rectTransform.sizeDelta = new Vector2(-30f, 44f);
            t.characterSpacing = 6f; UX.Outline(t, 0.15f);
            var line = UIKit.Img(head, "Linie", UIKit.HGradient, new Color(1f, 0.75f, 0.3f));
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = new Vector2(0f, 1f); line.rectTransform.pivot = new Vector2(0f, 1f);
            line.rectTransform.anchoredPosition = new Vector2(30f, -64f); line.rectTransform.sizeDelta = new Vector2(380f, 3f);
            roundText = UIKit.Text(head, "Runde", "", 22f, UIKit.TextDim, TextAlignmentOptions.TopLeft, true);
            roundText.rectTransform.anchorMin = roundText.rectTransform.anchorMax = new Vector2(0f, 1f); roundText.rectTransform.pivot = new Vector2(0f, 1f);
            roundText.rectTransform.anchoredPosition = new Vector2(30f, -72f); roundText.rectTransform.sizeDelta = new Vector2(700f, 30f);
            roundText.characterSpacing = 2f;

            var back = UIKit.Button(root, "Menue", "MENÜ", new Color(0.65f, 0.7f, 0.78f), () => { Sound.Play("klick"); GameFlow.ToMenu(); }, 24f);
            UIKit.Place((RectTransform)back.transform, new Vector2(1f, 1f), new Vector2(-90f, -50f), new Vector2(140f, 60f));

            // Synergien links
            synergyCol = UIKit.Rect("Synergien", root);
            synergyCol.anchorMin = synergyCol.anchorMax = new Vector2(0f, 1f); synergyCol.pivot = new Vector2(0f, 1f);
            synergyCol.anchoredPosition = new Vector2(22f, -130f); synergyCol.sizeDelta = new Vector2(250f, 520f);

            // Spielerliste rechts
            playerList = UIKit.Rect("Spieler", root);
            playerList.anchorMin = playerList.anchorMax = new Vector2(1f, 1f); playerList.pivot = new Vector2(1f, 1f);
            playerList.anchoredPosition = new Vector2(-22f, -100f); playerList.sizeDelta = new Vector2(250f, 8 * 50f);

            // Untere Leiste: Stufe | Laden | Gold
            var bar = UIKit.GlassPanel(root, "Leiste", null, 0.85f);
            bar.raycastTarget = true;
            bar.rectTransform.anchorMin = new Vector2(0f, 0f); bar.rectTransform.anchorMax = new Vector2(1f, 0f); bar.rectTransform.pivot = new Vector2(0.5f, 0f);
            bar.rectTransform.anchoredPosition = new Vector2(0f, 10f); bar.rectTransform.sizeDelta = new Vector2(-20f, 190f);

            // links: Stufe und Erfahrung
            var lv = UIKit.Rect("Stufe", bar.transform);
            lv.anchorMin = new Vector2(0f, 0f); lv.anchorMax = new Vector2(0.18f, 1f); lv.offsetMin = new Vector2(14f, 12f); lv.offsetMax = new Vector2(-6f, -12f);
            levelText = UIKit.Text(lv, "Text", "", 26f, Color.white, TextAlignmentOptions.TopLeft, true);
            levelText.rectTransform.anchorMin = new Vector2(0f, 0.62f); levelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            levelText.rectTransform.offsetMin = levelText.rectTransform.offsetMax = Vector2.zero;
            xpFill = UX.Bar(lv, "EP", new Color(0.45f, 0.85f, 1f), new Color(1f, 1f, 1f, 0.1f));
            var xr = (RectTransform)xpFill.transform.parent;
            xr.anchorMin = new Vector2(0f, 0.5f); xr.anchorMax = new Vector2(1f, 0.58f); xr.offsetMin = xr.offsetMax = Vector2.zero;
            var xpb = UIKit.Button(lv, "EPKaufen", $"EP +4  ·  {Tft.XpCost} GOLD", new Color(0.45f, 0.85f, 1f), () =>
            {
                if (G.BuyXp(Me, out var e)) { Sound.Play("selten", 0.5f); Refresh(); } else Toast(e);
            }, 22f);
            var xbt = (RectTransform)xpb.transform;
            xbt.anchorMin = new Vector2(0f, 0f); xbt.anchorMax = new Vector2(1f, 0.44f); xbt.offsetMin = xbt.offsetMax = Vector2.zero;

            // Mitte: Laden
            var shopArea = UIKit.Rect("LadenBereich", bar.transform);
            shopArea.anchorMin = new Vector2(0.18f, 0f); shopArea.anchorMax = new Vector2(0.82f, 1f); shopArea.offsetMin = new Vector2(6f, 12f); shopArea.offsetMax = new Vector2(-6f, -12f);
            shopRow = UIKit.Rect("Laden", shopArea);
            UIKit.Stretch(shopRow);

            // rechts: Gold, neu würfeln, sperren
            var gr = UIKit.Rect("Gold", bar.transform);
            gr.anchorMin = new Vector2(0.82f, 0f); gr.anchorMax = new Vector2(1f, 1f); gr.offsetMin = new Vector2(6f, 12f); gr.offsetMax = new Vector2(-14f, -12f);
            goldText = UIKit.Text(gr, "Wert", "", 34f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.TopLeft, true);
            goldText.rectTransform.anchorMin = new Vector2(0f, 0.62f); goldText.rectTransform.anchorMax = new Vector2(0.62f, 1f);
            goldText.rectTransform.offsetMin = goldText.rectTransform.offsetMax = Vector2.zero;
            lockBtn = UIKit.Button(gr, "Sperren", "", new Color(0.65f, 0.7f, 0.78f), () => { Me.locked = !Me.locked; Sound.Play("klick"); Refresh(); }, 18f);
            var lbt = (RectTransform)lockBtn.transform;
            lbt.anchorMin = new Vector2(0.64f, 0.6f); lbt.anchorMax = new Vector2(1f, 1f); lbt.offsetMin = lbt.offsetMax = Vector2.zero;
            var rr = UIKit.Button(gr, "Neu", $"NEU  ·  {Tft.RerollCost} GOLD", new Color(1f, 0.75f, 0.3f), () =>
            {
                if (G.Reroll(Me, out var e)) { Sound.Play("klick", 0.8f, 1.2f); Refresh(); } else Toast(e);
            }, 22f);
            var rbt = (RectTransform)rr.transform;
            rbt.anchorMin = new Vector2(0f, 0f); rbt.anchorMax = new Vector2(1f, 0.52f); rbt.offsetMin = rbt.offsetMax = Vector2.zero;

            // Feldanzeige und Kampf-Knopf
            fieldText = UIKit.Text(root, "Feld", "", 24f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Place(fieldText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(400f, 36f));
            UX.Outline(fieldText, 0.2f);
            var fight = UIKit.Button(root, "Kampf", "KAMPF  »", new Color(1f, 0.4f, 0.35f), StartFight, 34f);
            UIKit.Place((RectTransform)fight.transform, new Vector2(1f, 0f), new Vector2(-150f, 250f), new Vector2(260f, 84f));

            // Hinweis
            var tg = UIKit.GlassPanel(root, "Hinweis", new Color(1f, 0.75f, 0.3f), 0.9f);
            UIKit.Place(tg.rectTransform, new Vector2(0.5f, 0.82f), Vector2.zero, new Vector2(760f, 70f));
            toastText = UIKit.Text(tg.transform, "T", "", 26f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(toastText.rectTransform, 20, 4, 20, 4);
            toastText.enableAutoSizing = true; toastText.fontSizeMin = 14; toastText.fontSizeMax = 26;
            toastGroup = tg.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f; toastGroup.blocksRaycasts = false;
        }

        void Toast(string msg) { toastText.text = msg; toastUntil = Time.unscaledTime + 2.2f; }

        /// <summary>Alle Anzeigen aktualisieren.</summary>
        void Refresh()
        {
            TftSession.Save();
            var me = Me;
            bool creep = Tft.IsCreepRound(G.stage, G.round);
            roundText.text = $"RUNDE {G.RoundName}  ·  {(creep ? "WILDE MONSTER" : "GEGEN SPIELER")}  ·  PLANUNG";
            goldText.text = $"{me.gold} <size=55%>GOLD</size>";
            int need = me.level < Tft.MaxLevel ? Tft.XpToNext[me.level] : 0;
            levelText.text = $"STUFE {me.level}  <size=70%><color=#c7d0da>{(need > 0 ? $"{me.xp}/{need} EP" : "MAX")}</color></size>";
            xpFill.fillAmount = need > 0 ? me.xp / (float)need : 1f;
            UIKit.SetLabel(lockBtn, me.locked ? "GESPERRT" : "SPERREN");
            int onBoard = me.BoardCount;
            fieldText.text = $"FELD  {onBoard} / {me.level}" + (onBoard < me.level && HasBench() ? "  <color=#ffd23f>· Platz frei!</color>" : "");

            // Laden
            UX.Clear(shopRow);
            for (int i = 0; i < Tft.ShopSize; i++)
            {
                int idx = i;
                var id = me.shop[i];
                var cell = UIKit.Rect("Platz", shopRow);
                cell.anchorMin = new Vector2(i / (float)Tft.ShopSize, 0f); cell.anchorMax = new Vector2((i + 1) / (float)Tft.ShopSize, 1f);
                cell.offsetMin = new Vector2(5f, 0f); cell.offsetMax = new Vector2(-5f, 0f);
                if (id == null) { var e = UIKit.GlassPanel(cell, "Leer", null, 0.3f); UIKit.Stretch(e.rectTransform); continue; }
                var d = CreatureDb.Get(id);
                int cost = Tft.Cost(d);
                int owned = 0; foreach (var u in me.AllUnits()) if (u.id == id && u.star == 1) owned++;
                Color cc = CostColor(cost);
                var b = UIKit.Button(cell, "Karte", "", cc, () =>
                {
                    if (G.Buy(Me, idx, out var err)) { Sound.Play("klick", 0.8f, 1.1f); Sync(); Refresh(); } else Toast(err);
                }, 20f);
                UIKit.Stretch((RectTransform)b.transform);
                var tx = b.GetComponentInChildren<TextMeshProUGUI>();
                tx.alignment = TextAlignmentOptions.TopLeft;
                tx.rectTransform.offsetMin = new Vector2(20f, 8f); tx.rectTransform.offsetMax = new Vector2(-8f, -8f);
                tx.enableAutoSizing = true; tx.fontSizeMin = 11; tx.fontSizeMax = 22;
                tx.characterSpacing = 0.5f;
                tx.text = $"{d.Name.ToUpperInvariant()}\n<size=72%><color={Look.Hex(Color.Lerp(Look.ElementColor(d.element), Color.white, 0.4f))}>{d.element}</color>  ·  <color=#c7d0da>{Look.RoleName(d.role)}</color></size>";
                var price = UIKit.Text(b.transform.GetChild(0), "Preis", $"{cost}", 26f, cc, TextAlignmentOptions.BottomRight, true);
                UIKit.Stretch(price.rectTransform, 0, 6, 12, 0);
                if (owned >= 2)
                {
                    var up = UIKit.Text(b.transform.GetChild(0), "Stern", "STERN +1", 16f, new Color(1f, 0.85f, 0.35f), TextAlignmentOptions.BottomLeft, true);
                    UIKit.Stretch(up.rectTransform, 20, 8, 0, 0);
                }
            }

            // Spielerliste
            UX.Clear(playerList);
            var order = new List<TftPlayer>(G.players);
            order.Sort((a, c) => c.hp != a.hp ? c.hp.CompareTo(a.hp) : string.Compare(a.name, c.name, System.StringComparison.Ordinal));
            for (int i = 0; i < order.Count; i++)
            {
                var p = order[i];
                bool isMe = p == me;
                var row = UIKit.GlassPanel(playerList, "Spieler", isMe ? new Color(0.45f, 0.85f, 1f) : (Color?)null, p.Alive ? 0.75f : 0.35f);
                row.rectTransform.anchorMin = row.rectTransform.anchorMax = new Vector2(1f, 1f); row.rectTransform.pivot = new Vector2(1f, 1f);
                row.rectTransform.anchoredPosition = new Vector2(0f, -i * 48f); row.rectTransform.sizeDelta = new Vector2(250f, 44f);
                var nm = UIKit.Text(row.transform, "Name", p.Alive ? p.name : $"{p.name}  <size=80%>#{p.place}</size>", 17f, p.Alive ? Color.white : new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.MidlineLeft, true);
                UIKit.Stretch(nm.rectTransform, 16, 14, 60, 0);
                var hpT = UIKit.Text(row.transform, "LP", p.hp.ToString(), 20f, p.hp > 50 ? new Color(0.5f, 1f, 0.6f) : p.hp > 20 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.45f, 0.4f), TextAlignmentOptions.MidlineRight, true);
                UIKit.Stretch(hpT.rectTransform, 0, 10, 10, 0);
                var hb = UX.Bar(row.transform, "Leben", new Color(0.4f, 0.9f, 0.5f), new Color(1f, 1f, 1f, 0.08f));
                var hbr = (RectTransform)hb.transform.parent;
                hbr.anchorMin = new Vector2(0f, 0f); hbr.anchorMax = new Vector2(1f, 0f); hbr.pivot = new Vector2(0.5f, 0f);
                hbr.anchoredPosition = new Vector2(0f, 5f); hbr.sizeDelta = new Vector2(-24f, 6f);
                hb.fillAmount = Mathf.Clamp01(p.hp / (float)Tft.StartHp);
            }

            // Synergien des eigenen Feldes
            UX.Clear(synergyCol);
            var team = me.ToTeam();
            var syn = team.units.Count > 0 ? Synergies.For(team) : new List<Synergy>();
            var hd = UIKit.Text(synergyCol, "Kopf", "SYNERGIEN", 16f, new Color(0.45f, 0.85f, 1f), TextAlignmentOptions.TopLeft, true);
            hd.rectTransform.anchorMin = new Vector2(0f, 1f); hd.rectTransform.anchorMax = new Vector2(1f, 1f); hd.rectTransform.pivot = new Vector2(0.5f, 1f);
            hd.rectTransform.sizeDelta = new Vector2(0f, 22f); hd.characterSpacing = 5f;
            if (syn.Count == 0)
            {
                var none = UIKit.Text(synergyCol, "Leer", "Zwei Monster gleichen Elements aufs Feld stellen", 15f, UIKit.TextDim, TextAlignmentOptions.TopLeft);
                none.rectTransform.anchorMin = new Vector2(0f, 1f); none.rectTransform.anchorMax = new Vector2(1f, 1f); none.rectTransform.pivot = new Vector2(0.5f, 1f);
                none.rectTransform.anchoredPosition = new Vector2(0f, -26f); none.rectTransform.sizeDelta = new Vector2(0f, 44f);
            }
            for (int i = 0; i < syn.Count; i++)
            {
                var chip = UIKit.GlassPanel(synergyCol, "Synergie", new Color(0.45f, 0.85f, 1f), 0.65f);
                chip.rectTransform.anchorMin = chip.rectTransform.anchorMax = new Vector2(0f, 1f); chip.rectTransform.pivot = new Vector2(0f, 1f);
                chip.rectTransform.anchoredPosition = new Vector2(0f, -26f - i * 50f); chip.rectTransform.sizeDelta = new Vector2(250f, 44f);
                var st = UIKit.Text(chip.transform, "T", $"{syn[i].name.ToUpperInvariant()}\n<size=72%><color=#c7d0da>{syn[i].text}</color></size>", 17f, Color.white, TextAlignmentOptions.MidlineLeft, true);
                UIKit.Stretch(st.rectTransform, 16, 2, 8, 2);
                st.enableAutoSizing = true; st.fontSizeMin = 10; st.fontSizeMax = 17;
            }
        }

        bool HasBench() { foreach (var u in Me.bench) if (u != null) return true; return false; }

        static Color CostColor(int cost) => cost switch
        {
            1 => new Color(0.75f, 0.78f, 0.82f),
            2 => new Color(0.4f, 0.9f, 0.5f),
            3 => new Color(0.4f, 0.7f, 1f),
            4 => new Color(0.8f, 0.5f, 1f),
            _ => new Color(1f, 0.8f, 0.3f),
        };

        void StartFight()
        {
            Sound.Play("klick");
            if (Me.BoardCount == 0 && HasBench()) { Toast("Zieh zuerst Monster von der Bank aufs Feld!"); return; }
            if (TftSession.Resolve()) GameFlow.ToBattle();
            else
            {
                Sync();
                Refresh();
                Toast(TftSession.Report);
                if (G.Over) ShowGameOver();
            }
        }

        void ShowGameOver()
        {
            var dim = UIKit.Img(root, "Abdunkeln", null, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var panel = UIKit.GlassPanel(root, "Ende", new Color(1f, 0.75f, 0.3f), 0.92f);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 480f));
            int place = TftSession.Place;
            var t = UIKit.Text(panel.transform, "Platz", place == 1 ? "<color=#ffd23f>SIEG!</color>" : $"PLATZ {place}", 72f, Color.white, TextAlignmentOptions.Top, true);
            UIKit.Stretch(t.rectTransform, 20, 0, 20, 40);
            t.characterSpacing = 8f;
            var r = UIKit.Text(panel.transform, "Belohnung", $"Belohnung: <color=#c38cff>{TftSession.LastReward} Kristalle</color>  ·  <color=#ffd23f>{Mathf.Max(0, 9 - place) * 40} Gold</color>\n<size=75%><color=#c7d0da>Mit Kristallen beschwörst du neue Monster – sie erscheinen dann auch im Laden.</color></size>", 28f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(r.rectTransform, 30, 140, 30, 150);
            var again = UIKit.Button(panel.transform, "Neu", "NEUE PARTIE", new Color(1f, 0.4f, 0.35f), () => { TftSession.NewGame(); GameFlow.ToTft(); }, 28f);
            UIKit.Place((RectTransform)again.transform, new Vector2(0.3f, 0f), new Vector2(0f, 70f), new Vector2(300f, 80f));
            var menu = UIKit.Button(panel.transform, "Menue", "MENÜ", new Color(0.65f, 0.7f, 0.78f), () => { TftSession.Current = null; TftSession.Save(); GameFlow.ToMenu(); }, 28f);
            UIKit.Place((RectTransform)menu.transform, new Vector2(0.7f, 0f), new Vector2(0f, 70f), new Vector2(300f, 80f));
        }

        // Debug-Hilfen
        public string DebugAuto()
        {
            G.AutoTurn(Me);
            Sync(); Refresh();
            return $"ok Stufe {Me.level}, Feld {Me.BoardCount}, Gold {Me.gold}";
        }
        public void DebugFight() => StartFight();
    }

    /// <summary>Reicht Zeiger-Ereignisse der unsichtbaren Spielfläche weiter.</summary>
    public class FieldPointer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public System.Action<Vector2> down, drag, up;
        public void OnPointerDown(PointerEventData e) => down?.Invoke(e.position);
        public void OnDrag(PointerEventData e) => drag?.Invoke(e.position);
        public void OnPointerUp(PointerEventData e) => up?.Invoke(e.position);
    }
}
