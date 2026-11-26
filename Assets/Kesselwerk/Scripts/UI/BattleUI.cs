using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kesselwerk
{
    /// <summary>
    /// Komplette Spieloberfläche (uGUI + TextMeshPro), im Code aufgebaut.
    /// Passt sich hochkant und quer an (Layout über Anteile des Bildschirms).
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        public const float TopFrac = 0.085f;
        public static float BottomFrac(bool portrait) => portrait ? 0.35f : 0.38f;

        BattleManager bm;
        Camera cam;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, topBar, bottomArea, infoRow, handArea, hudLayer, fxLayer, overlay;

        TextMeshProUGUI enemyName, enemyHp, enemyMeta, playerHp, playerMeta, hint;
        RectTransform energyRow;
        Button mainButton;
        TextMeshProUGUI[] phaseLabels;
        Image[] phasePills;

        RectTransform bannerRt;
        TextMeshProUGUI bannerText;
        CanvasGroup bannerGroup;
        Coroutine bannerRoutine;

        Button faceButton;

        readonly List<CardView> handViews = new List<CardView>();
        readonly Dictionary<Creature, CreatureHud> huds = new Dictionary<Creature, CreatureHud>();
        bool lastPortrait;
        int lastW, lastH;

        class CreatureHud
        {
            public RectTransform stats, hit, ring;
            public Image ringImg, barFill;
            public TextMeshProUGUI atk, hp, tag;
        }

        static readonly Color Gold = new Color(0.95f, 0.8f, 0.45f);
        static readonly Color PanelColor = new Color(0.09f, 0.08f, 0.12f, 0.96f);

        // ------------------------------------------------------------------
        // Aufbau
        // ------------------------------------------------------------------

        public void Init(BattleManager manager, Camera camera)
        {
            bm = manager;
            cam = camera;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            var cgo = new GameObject("Kesselwerk UI");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            hudLayer = UIKit.Rect("Monster-Anzeigen", root);
            UIKit.Stretch(hudLayer);

            BuildTopBar();
            BuildBottomArea();

            faceButton = UIKit.Button(root, "DirektAngriff", "Direkt angreifen!", new Color(0.8f, 0.2f, 0.15f), () => bm.OnFaceTapped(), 34f);
            ((RectTransform)faceButton.transform).sizeDelta = new Vector2(330f, 86f);
            faceButton.gameObject.SetActive(false);

            fxLayer = UIKit.Rect("Effekte", root);
            UIKit.Stretch(fxLayer);

            BuildBanner();

            overlay = UIKit.Rect("Overlay", root);
            UIKit.Stretch(overlay);
            overlay.gameObject.SetActive(false);

            ApplyOrientation(true);
        }

        void BuildTopBar()
        {
            topBar = UIKit.Rect("ObereLeiste", root);
            var bg = UIKit.Img(topBar, "Hintergrund", UIKit.Gradient, new Color(0.05f, 0.04f, 0.08f, 0.85f));
            UIKit.Stretch(bg.rectTransform, 0, -60, 0, 0);
            bg.rectTransform.localScale = new Vector3(1f, -1f, 1f);
            var line = UIKit.Img(topBar, "Goldlinie", null, new Color(Gold.r, Gold.g, Gold.b, 0.0f));
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.offsetMin = new Vector2(0f, 0f);
            line.rectTransform.offsetMax = new Vector2(0f, 4f);

            var avatar = UIKit.Img(topBar, "Avatar", UIKit.Circle, new Color(0.75f, 0.2f, 0.15f));
            avatar.rectTransform.anchorMin = avatar.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            avatar.rectTransform.sizeDelta = new Vector2(96f, 96f);
            avatar.rectTransform.anchoredPosition = new Vector2(70f, 0f);
            var aText = UIKit.Text(avatar.transform, "Buchstabe", "G", 52f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(aText.rectTransform);

            enemyName = UIKit.Text(topBar, "Name", "Gegner", 40f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            enemyName.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            enemyName.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            enemyName.rectTransform.offsetMin = new Vector2(135f, 0f);
            enemyName.rectTransform.offsetMax = new Vector2(0f, -6f);

            enemyMeta = UIKit.Text(topBar, "Meta", "", 30f, new Color(0.8f, 0.8f, 0.85f), TextAlignmentOptions.MidlineLeft);
            enemyMeta.rectTransform.anchorMin = new Vector2(0f, 0f);
            enemyMeta.rectTransform.anchorMax = new Vector2(0.7f, 0.5f);
            enemyMeta.rectTransform.offsetMin = new Vector2(135f, 6f);
            enemyMeta.rectTransform.offsetMax = Vector2.zero;

            var hpBadge = UIKit.Badge(topBar, "Leben", new Color(0.8f, 0.15f, 0.2f), 100f, "25");
            var hpRt = (RectTransform)hpBadge.transform.parent;
            hpRt.anchorMin = hpRt.anchorMax = new Vector2(1f, 0.5f);
            hpRt.anchoredPosition = new Vector2(-80f, 0f);
            enemyHp = hpBadge;

            var menu = UIKit.Button(topBar, "Menue", "Menü", new Color(0.35f, 0.33f, 0.4f), () => bm.OnMenuButton(), 30f);
            var mrt = (RectTransform)menu.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(1f, 0.5f);
            mrt.pivot = new Vector2(1f, 0.5f);
            mrt.anchoredPosition = new Vector2(-150f, 0f);
            mrt.sizeDelta = new Vector2(170f, 76f);
        }

        void BuildBottomArea()
        {
            bottomArea = UIKit.Rect("UntererBereich", root);
            var bg = UIKit.Img(bottomArea, "Hintergrund", UIKit.Gradient, new Color(0.05f, 0.04f, 0.08f, 0.92f));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, -80);
            var line = UIKit.Img(bottomArea, "Goldlinie", null, new Color(Gold.r, Gold.g, Gold.b, 0.0f));
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.offsetMin = new Vector2(0f, -4f);
            line.rectTransform.offsetMax = Vector2.zero;

            // Infozeile: Leben, Energie, Phasen, Knopf
            infoRow = UIKit.Rect("Infozeile", bottomArea);
            infoRow.anchorMin = new Vector2(0f, 1f);
            infoRow.anchorMax = new Vector2(1f, 1f);
            infoRow.pivot = new Vector2(0.5f, 1f);
            infoRow.offsetMin = new Vector2(0f, -170f);
            infoRow.offsetMax = new Vector2(0f, -8f);

            var hpBadge = UIKit.Badge(infoRow, "Leben", new Color(0.2f, 0.6f, 0.25f), 96f, "25");
            var hpRt = (RectTransform)hpBadge.transform.parent;
            hpRt.anchorMin = hpRt.anchorMax = new Vector2(0f, 1f);
            hpRt.anchoredPosition = new Vector2(66f, -54f);
            playerHp = hpBadge;

            energyRow = UIKit.Rect("Energie", infoRow);
            energyRow.anchorMin = energyRow.anchorMax = new Vector2(0f, 1f);
            energyRow.pivot = new Vector2(0f, 0.5f);
            energyRow.anchoredPosition = new Vector2(130f, -34f);
            energyRow.sizeDelta = new Vector2(500f, 44f);

            playerMeta = UIKit.Text(infoRow, "Meta", "", 26f, new Color(0.8f, 0.8f, 0.85f), TextAlignmentOptions.MidlineLeft);
            playerMeta.rectTransform.anchorMin = playerMeta.rectTransform.anchorMax = new Vector2(0f, 1f);
            playerMeta.rectTransform.pivot = new Vector2(0f, 0.5f);
            playerMeta.rectTransform.anchoredPosition = new Vector2(130f, -80f);
            playerMeta.rectTransform.sizeDelta = new Vector2(520f, 36f);

            // Phasen-Anzeige
            var phases = UIKit.Rect("Phasen", infoRow);
            phases.anchorMin = phases.anchorMax = new Vector2(0f, 1f);
            phases.pivot = new Vector2(0f, 0.5f);
            phases.anchoredPosition = new Vector2(20f, -132f);
            phases.sizeDelta = new Vector2(640f, 44f);
            string[] names = { "Ziehen", "Haupt", "Kampf", "Ende" };
            phaseLabels = new TextMeshProUGUI[names.Length];
            phasePills = new Image[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var pill = UIKit.Img(phases, "Phase_" + names[i], UIKit.Rounded, new Color(1f, 1f, 1f, 0.08f));
                pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                pill.rectTransform.pivot = new Vector2(0f, 0.5f);
                pill.rectTransform.anchoredPosition = new Vector2(i * 156f, 0f);
                pill.rectTransform.sizeDelta = new Vector2(148f, 42f);
                var t = UIKit.Text(pill.transform, "Text", names[i], 24f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.Center, true);
                UIKit.Stretch(t.rectTransform);
                phaseLabels[i] = t;
                phasePills[i] = pill;
            }

            mainButton = UIKit.Button(infoRow, "Hauptknopf", "Weiter", new Color(0.85f, 0.55f, 0.1f), () => bm.OnMainButton(), 36f);
            var mbRt = (RectTransform)mainButton.transform;
            mbRt.anchorMin = mbRt.anchorMax = new Vector2(1f, 1f);
            mbRt.pivot = new Vector2(1f, 1f);
            mbRt.anchoredPosition = new Vector2(-20f, -12f);
            mbRt.sizeDelta = new Vector2(300f, 92f);

            hint = UIKit.Text(infoRow, "Hinweis", "", 24f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.MidlineRight);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(1f, 0.5f);
            hint.rectTransform.anchoredPosition = new Vector2(-20f, -132f);
            hint.rectTransform.sizeDelta = new Vector2(420f, 44f);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 14f;
            hint.fontSizeMax = 24f;

            handArea = UIKit.Rect("Hand", bottomArea);
            handArea.anchorMin = new Vector2(0f, 0f);
            handArea.anchorMax = new Vector2(1f, 1f);
            handArea.offsetMin = new Vector2(10f, 10f);
            handArea.offsetMax = new Vector2(-10f, -175f);
        }

        void BuildBanner()
        {
            bannerRt = UIKit.Rect("Banner", root);
            UIKit.Place(bannerRt, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(820f, 130f));
            var bg = UIKit.Img(bannerRt, "Hintergrund", UIKit.RoundedSoft, new Color(0.05f, 0.04f, 0.07f, 0.85f));
            UIKit.Stretch(bg.rectTransform, -20, -20, -20, -20);
            var gold = UIKit.Img(bannerRt, "Rand", UIKit.Rounded, new Color(Gold.r, Gold.g, Gold.b, 0.9f));
            UIKit.Stretch(gold.rectTransform);
            var inner = UIKit.Img(bannerRt, "Innen", UIKit.Rounded, new Color(0.12f, 0.1f, 0.16f));
            UIKit.Stretch(inner.rectTransform, 4, 4, 4, 4);
            bannerText = UIKit.Text(bannerRt, "Text", "", 54f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(bannerText.rectTransform, 20, 6, 20, 6);
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 24f;
            bannerText.fontSizeMax = 54f;
            bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
        }

        // ------------------------------------------------------------------
        // Ausrichtung
        // ------------------------------------------------------------------

        bool IsPortrait => Screen.height >= Screen.width;

        void ApplyOrientation(bool force)
        {
            bool portrait = IsPortrait;
            if (!force && portrait == lastPortrait && Screen.width == lastW && Screen.height == lastH) return;
            lastPortrait = portrait;
            lastW = Screen.width;
            lastH = Screen.height;

            scaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = portrait ? 0f : 1f;

            UIKit.Anchor(topBar, 0f, 1f - TopFrac, 1f, 1f);
            UIKit.Anchor(bottomArea, 0f, 0f, 1f, BottomFrac(portrait));
            if (portrait)
            {
                // Hochkant: Infozeile oben, Hand darunter über die ganze Breite
                handArea.anchorMin = new Vector2(0f, 0f);
                handArea.anchorMax = new Vector2(1f, 1f);
                handArea.offsetMin = new Vector2(10f, 10f);
                handArea.offsetMax = new Vector2(-10f, -175f);
            }
            else
            {
                // Quer: Infos links und rechts, Hand in der Mitte in voller Höhe
                handArea.anchorMin = new Vector2(0.35f, 0f);
                handArea.anchorMax = new Vector2(0.77f, 1f);
                handArea.offsetMin = new Vector2(0f, 8f);
                handArea.offsetMax = new Vector2(0f, -4f);
            }
            RefreshHand();
        }

        // ------------------------------------------------------------------
        // Aktualisierung
        // ------------------------------------------------------------------

        public void RefreshAll()
        {
            RefreshInfo();
            RefreshHand();
        }

        public void RefreshInfo()
        {
            var p = bm.Player;
            var e = bm.Enemy;
            enemyHp.text = Mathf.Max(0, e.hp).ToString();
            enemyMeta.text = $"Hand {e.hand.Count}   ·   Deck {e.deck.Count}";
            playerHp.text = Mathf.Max(0, p.hp).ToString();
            playerMeta.text = $"Energie {p.energy}/{p.maxEnergy}   ·   Deck {p.deck.Count}";

            // Energie-Kristalle
            for (int i = energyRow.childCount - 1; i >= 0; i--) Destroy(energyRow.GetChild(i).gameObject);
            for (int i = 0; i < p.maxEnergy; i++)
            {
                bool full = i < p.energy;
                var gem = UIKit.Img(energyRow, "Kristall", UIKit.Rounded, full ? new Color(0.35f, 0.7f, 1f) : new Color(0.25f, 0.28f, 0.35f));
                gem.rectTransform.anchorMin = gem.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                gem.rectTransform.sizeDelta = new Vector2(30f, 30f);
                gem.rectTransform.anchoredPosition = new Vector2(20f + i * 44f, 0f);
                gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // Phasen
            int active = bm.PhaseIndex;
            for (int i = 0; i < phasePills.Length; i++)
            {
                bool on = i == active && bm.IsPlayerTurn;
                phasePills[i].color = on ? new Color(0.85f, 0.55f, 0.1f) : new Color(1f, 1f, 1f, 0.08f);
                phaseLabels[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            UIKit.SetLabel(mainButton, bm.MainButtonLabel);
            mainButton.interactable = bm.MainButtonEnabled;
            hint.text = bm.HintText;
        }

        public void RefreshHand()
        {
            if (handArea == null || bm == null || bm.Player == null) return;
            foreach (var v in handViews) if (v != null) Destroy(v.gameObject);
            handViews.Clear();

            Canvas.ForceUpdateCanvases();
            var hand = bm.Player.hand;
            int n = hand.Count;
            if (n == 0) return;

            Rect area = handArea.rect;
            float scale = Mathf.Min(area.height * 0.97f / CardView.H, 1.9f);
            float cardW = CardView.W * scale;
            float maxSpan = area.width - cardW;
            float step = n > 1 ? Mathf.Min(cardW * 1.04f, maxSpan / (n - 1)) : 0f;
            float total = step * (n - 1);

            for (int i = 0; i < n; i++)
            {
                var card = hand[i];
                var v = CardView.Create(handArea, card);
                v.rt.anchorMin = v.rt.anchorMax = new Vector2(0.5f, 0.5f);
                float t = n > 1 ? (i / (float)(n - 1)) * 2f - 1f : 0f;
                v.rt.anchoredPosition = new Vector2(-total * 0.5f + i * step, -Mathf.Abs(t) * 10f * scale);
                v.rt.localRotation = Quaternion.Euler(0f, 0f, -t * 4f);
                v.rt.localScale = Vector3.one * scale;
                bool playable = bm.CanPlayFromHand(card);
                v.SetPlayable(playable, bm.IsPlayerTurn && bm.CurrentPhase == BattleManager.Phase.Main);
                int index = i;
                v.button.onClick.AddListener(() => bm.OnHandCardTapped(index));
                handViews.Add(v);
            }
        }

        void LateUpdate()
        {
            if (bm == null) return;
            ApplyOrientation(false);
            UpdateHuds();
            UpdateFaceButton();
        }

        // ------------------------------------------------------------------
        // Anzeigen über den Monstern
        // ------------------------------------------------------------------

        public void AddCreature(Creature c)
        {
            var hud = new CreatureHud();

            // Leuchtring (Auswahl / Ziel)
            var ringImg = UIKit.Img(hudLayer, "Ring_" + c.card.name, UIKit.Glow, new Color(1f, 1f, 1f, 0f));
            hud.ring = ringImg.rectTransform;
            hud.ringImg = ringImg;

            // Klickfläche
            var hit = UIKit.Img(hudLayer, "Klick_" + c.card.name, null, new Color(0f, 0f, 0f, 0f));
            hit.raycastTarget = true;
            var btn = hit.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => bm.OnCreatureTapped(c));
            hud.hit = hit.rectTransform;

            // Werte
            hud.stats = UIKit.Rect("Werte_" + c.card.name, hudLayer);
            hud.stats.sizeDelta = new Vector2(190f, 70f);
            var barBg = UIKit.Img(hud.stats, "Balken", UIKit.Rounded, new Color(0f, 0f, 0f, 0.7f));
            UIKit.Place(barBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(100f, 18f));
            hud.barFill = UIKit.Img(barBg.transform, "Fuellung", UIKit.Rounded, new Color(0.3f, 0.85f, 0.35f));
            hud.barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            hud.barFill.rectTransform.anchorMax = new Vector2(1f, 1f);
            hud.barFill.rectTransform.offsetMin = new Vector2(3f, 3f);
            hud.barFill.rectTransform.offsetMax = new Vector2(-3f, -3f);
            hud.atk = UIKit.Badge(hud.stats, "Angriff", new Color(0.85f, 0.2f, 0.15f), 56f, c.attack.ToString());
            UIKit.Place((RectTransform)hud.atk.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(-72f, 0f), new Vector2(56f, 56f));
            hud.hp = UIKit.Badge(hud.stats, "Leben", new Color(0.2f, 0.65f, 0.25f), 56f, c.health.ToString());
            UIKit.Place((RectTransform)hud.hp.transform.parent, new Vector2(0.5f, 0.5f), new Vector2(72f, 0f), new Vector2(56f, 56f));
            hud.tag = UIKit.Text(hud.stats, "Status", "", 22f, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center, true);
            UIKit.Place(hud.tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(220f, 30f));
            hud.tag.outlineWidth = 0.25f;
            hud.tag.outlineColor = new Color32(0, 0, 0, 220);

            huds[c] = hud;
        }

        public void RemoveCreature(Creature c)
        {
            if (!huds.TryGetValue(c, out var hud)) return;
            if (hud.stats != null) Destroy(hud.stats.gameObject);
            if (hud.hit != null) Destroy(hud.hit.gameObject);
            if (hud.ring != null) Destroy(hud.ring.gameObject);
            huds.Remove(c);
        }

        public void ClearCreatures()
        {
            foreach (var c in new List<Creature>(huds.Keys)) RemoveCreature(c);
        }

        void UpdateHuds()
        {
            if (cam == null) return;
            float sf = canvas.scaleFactor;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);

            foreach (var kv in huds)
            {
                var c = kv.Key;
                var hud = kv.Value;
                if (c == null) continue;

                Vector3 basePos = c.transform.position;
                Vector3 feet = cam.WorldToScreenPoint(basePos);
                Vector3 head = cam.WorldToScreenPoint(basePos + Vector3.up * (c.Height + 0.25f));
                Vector3 mid = cam.WorldToScreenPoint(basePos + Vector3.up * (c.Height * 0.5f));
                bool visible = feet.z > 0f;
                hud.stats.gameObject.SetActive(visible);
                hud.hit.gameObject.SetActive(visible);
                hud.ring.gameObject.SetActive(visible);
                if (!visible) continue;

                float hPx = Mathf.Abs(head.y - feet.y);
                hud.stats.position = head;
                hud.hit.position = mid;
                hud.hit.sizeDelta = new Vector2(hPx * 0.95f, hPx * 1.05f) / sf;
                hud.ring.position = feet;
                hud.ring.sizeDelta = new Vector2(hPx * 1.4f, hPx * 0.55f) / sf;

                hud.atk.text = c.attack.ToString();
                hud.hp.text = Mathf.Max(0, c.health).ToString();
                float frac = Mathf.Clamp01(c.health / (float)Mathf.Max(1, c.maxHealth));
                hud.barFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, frac), 1f);
                hud.barFill.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.3f, 0.85f, 0.35f), frac);

                // Zustand
                Color ring = new Color(1f, 1f, 1f, 0f);
                string tag = c.Keyword == Keyword.Guardian ? "Wächter" : "";
                var state = bm.GetHighlight(c);
                switch (state)
                {
                    case BattleManager.Highlight.CanAttack:
                        ring = new Color(1f, 0.85f, 0.25f, 0.55f * pulse + 0.25f);
                        break;
                    case BattleManager.Highlight.Selected:
                        ring = new Color(1f, 1f, 1f, 0.95f);
                        tag = "Wähle ein Ziel";
                        break;
                    case BattleManager.Highlight.Target:
                        ring = new Color(1f, 0.25f, 0.2f, 0.6f * pulse + 0.3f);
                        break;
                    case BattleManager.Highlight.Exhausted:
                        tag = string.IsNullOrEmpty(tag) ? "hat angegriffen" : tag;
                        break;
                }
                hud.ringImg.color = ring;
                hud.tag.text = tag;
            }
        }

        void UpdateFaceButton()
        {
            bool show = bm.CanAttackFaceNow;
            if (faceButton.gameObject.activeSelf != show) faceButton.gameObject.SetActive(show);
            if (!show || cam == null) return;
            Vector3 p = cam.WorldToScreenPoint(ArenaBuilder.FacePos(false) + Vector3.up * 1.6f);
            if (p.z > 0f) faceButton.transform.position = p;
        }

        // ------------------------------------------------------------------
        // Overlays: Karten-Detail, Ziehphase, gegnerische Karte, Spielende
        // ------------------------------------------------------------------

        public bool OverlayOpen => overlay.gameObject.activeSelf;

        void ClearOverlay()
        {
            for (int i = overlay.childCount - 1; i >= 0; i--) Destroy(overlay.GetChild(i).gameObject);
        }

        public void CloseOverlay()
        {
            ClearOverlay();
            overlay.gameObject.SetActive(false);
        }

        RectTransform OpenOverlay(string title, bool tapToClose)
        {
            ClearOverlay();
            overlay.gameObject.SetActive(true);
            var dim = UIKit.Img(overlay, "Abdunkeln", null, new Color(0.02f, 0.02f, 0.05f, 0.82f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            if (tapToClose)
            {
                var b = dim.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(CloseOverlay);
            }

            var content = UIKit.Rect("Inhalt", overlay);
            UIKit.Stretch(content);

            if (!string.IsNullOrEmpty(title))
            {
                var t = UIKit.Text(content, "Titel", title, 54f, Gold, TextAlignmentOptions.Center, true);
                t.rectTransform.anchorMin = new Vector2(0f, 0.86f);
                t.rectTransform.anchorMax = new Vector2(1f, 0.95f);
                t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.outlineWidth = 0.2f;
                t.outlineColor = new Color32(0, 0, 0, 200);
            }
            return content;
        }

        CardView BigCard(RectTransform content, CardData card)
        {
            Canvas.ForceUpdateCanvases();
            Rect r = root.rect;
            float scale = Mathf.Min(r.height * 0.56f / CardView.H, r.width * 0.78f / CardView.W);
            var v = CardView.Create(content, card);
            v.rt.anchorMin = v.rt.anchorMax = new Vector2(0.5f, 0.52f);
            v.rt.anchoredPosition = Vector2.zero;
            v.button.interactable = false;
            StartCoroutine(PopIn(v.rt, scale));
            return v;
        }

        IEnumerator PopIn(RectTransform rt, float targetScale)
        {
            float t = 0f;
            while (t < 1f && rt != null)
            {
                t += Time.deltaTime / 0.3f;
                float k = Mathf.Clamp01(t);
                float s = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);
                rt.localScale = Vector3.one * targetScale * s;
                rt.localRotation = Quaternion.Euler(0f, (1f - k) * 70f, 0f);
                yield return null;
            }
            if (rt != null)
            {
                rt.localScale = Vector3.one * targetScale;
                rt.localRotation = Quaternion.identity;
            }
        }

        Button OverlayButton(RectTransform content, string label, Color color, float xFrac, Action onClick)
        {
            var b = UIKit.Button(content, label, label, color, () => onClick?.Invoke(), 40f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(xFrac, 0.13f);
            rt.sizeDelta = new Vector2(400f, 110f);
            rt.anchoredPosition = Vector2.zero;
            return b;
        }

        /// <summary>Karte groß anzeigen, mit optionaler Aktion (z. B. Ausspielen).</summary>
        public void ShowCardDetail(CardData card, string title, string actionLabel, bool actionEnabled, Action onAction)
        {
            var content = OpenOverlay(title, true);
            BigCard(content, card);
            if (!string.IsNullOrEmpty(actionLabel))
            {
                var a = OverlayButton(content, actionLabel, new Color(0.2f, 0.6f, 0.25f), 0.3f, () => { CloseOverlay(); onAction?.Invoke(); });
                a.interactable = actionEnabled;
                OverlayButton(content, "Zurück", new Color(0.35f, 0.33f, 0.4f), 0.7f, CloseOverlay);
            }
            else
            {
                OverlayButton(content, "Schließen", new Color(0.35f, 0.33f, 0.4f), 0.5f, CloseOverlay);
            }
        }

        /// <summary>Ziehphase: gezogene Karte zeigen, weiter mit Tipp.</summary>
        public void ShowDrawReveal(CardData card, Action onContinue)
        {
            var content = OpenOverlay("Ziehphase: Du ziehst", false);
            BigCard(content, card);
            OverlayButton(content, "Weiter", new Color(0.85f, 0.55f, 0.1f), 0.5f, () => { CloseOverlay(); onContinue?.Invoke(); });
        }

        /// <summary>Zeigt kurz die Karte, die der Gegner ausspielt.</summary>
        public IEnumerator ShowEnemyPlay(CardData card)
        {
            var content = OpenOverlay("Gegner spielt", true);
            BigCard(content, card);
            float t = 0f;
            while (t < 1.8f && OverlayOpen)
            {
                t += Time.deltaTime;
                yield return null;
            }
            CloseOverlay();
        }

        public void ShowGameOver(string text, bool won, string reward, Action onRestart, Action onMenu)
        {
            var content = OpenOverlay(null, false);
            var t = UIKit.Text(content, "Ergebnis", text, 120f, won ? Gold : new Color(1f, 0.4f, 0.35f), TextAlignmentOptions.Center, true);
            t.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            t.rectTransform.anchorMax = new Vector2(1f, 0.72f);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(0, 0, 0, 220);

            var r = UIKit.Text(content, "Belohnung", reward, 64f, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center, true);
            r.rectTransform.anchorMin = new Vector2(0f, 0.44f);
            r.rectTransform.anchorMax = new Vector2(1f, 0.54f);
            r.rectTransform.offsetMin = r.rectTransform.offsetMax = Vector2.zero;
            r.outlineWidth = 0.25f;
            r.outlineColor = new Color32(0, 0, 0, 220);

            OverlayButton(content, "Nochmal", new Color(0.85f, 0.55f, 0.1f), 0.3f, () => { CloseOverlay(); onRestart?.Invoke(); });
            OverlayButton(content, "Zum Menü", new Color(0.35f, 0.33f, 0.4f), 0.7f, () => { CloseOverlay(); onMenu?.Invoke(); });
        }

        public void ShowConfirm(string title, string text, string confirmLabel, Action onConfirm)
        {
            var content = OpenOverlay(title, true);
            var t = UIKit.Text(content, "Text", text, 48f, Color.white, TextAlignmentOptions.Center);
            t.rectTransform.anchorMin = new Vector2(0f, 0.45f);
            t.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            OverlayButton(content, confirmLabel, new Color(0.8f, 0.2f, 0.15f), 0.3f, () => { CloseOverlay(); onConfirm?.Invoke(); });
            OverlayButton(content, "Weiterspielen", new Color(0.35f, 0.33f, 0.4f), 0.7f, CloseOverlay);
        }

        // ------------------------------------------------------------------
        // Banner & schwebende Zahlen
        // ------------------------------------------------------------------

        public void Banner(string text, float seconds = 1.3f)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text, seconds));
        }

        IEnumerator BannerRoutine(string text, float seconds)
        {
            bannerText.text = text;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.2f;
                bannerGroup.alpha = Mathf.Clamp01(t);
                bannerRt.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(t));
                yield return null;
            }
            yield return new WaitForSeconds(seconds);
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                bannerGroup.alpha = 1f - Mathf.Clamp01(t);
                yield return null;
            }
            bannerGroup.alpha = 0f;
        }

        public void FloatText(Vector3 worldPos, string text, Color color, float size = 72f)
        {
            if (cam == null) return;
            Vector3 p = cam.WorldToScreenPoint(worldPos);
            if (p.z < 0f) return;
            var t = UIKit.Text(fxLayer, "Zahl", text, size, color, TextAlignmentOptions.Center, true);
            t.rectTransform.sizeDelta = new Vector2(300f, 120f);
            t.rectTransform.position = p;
            t.outlineWidth = 0.3f;
            t.outlineColor = new Color32(0, 0, 0, 230);
            StartCoroutine(FloatRoutine(t));
        }

        IEnumerator FloatRoutine(TextMeshProUGUI t)
        {
            float time = 0f;
            Vector3 start = t.rectTransform.position;
            Color c = t.color;
            while (time < 1.1f && t != null)
            {
                time += Time.deltaTime;
                float k = time / 1.1f;
                t.rectTransform.position = start + Vector3.up * (k * 120f * canvas.scaleFactor);
                t.rectTransform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, (k - 0.15f) / 0.85f));
                t.color = new Color(c.r, c.g, c.b, 1f - Mathf.Clamp01((k - 0.6f) / 0.4f));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }
    }
}
