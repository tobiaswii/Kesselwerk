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
    /// Hauptmenü-Oberfläche: Start, Shop (Kartenpacks), Sammlung (Herstellen, Deck bearbeiten).
    /// </summary>
    public class MenuUI : MonoBehaviour
    {
        enum Page { Home, Shop, Collection }

        static readonly Color Gold = new Color(0.95f, 0.8f, 0.45f);
        static readonly Color Orange = new Color(0.88f, 0.55f, 0.12f);
        static readonly Color Blue = new Color(0.22f, 0.5f, 0.9f);
        static readonly Color Green = new Color(0.22f, 0.62f, 0.3f);
        static readonly Color Grey = new Color(0.35f, 0.33f, 0.4f);

        MenuManager mm;
        Camera cam;
        Canvas canvas;
        CanvasScaler scaler;
        RectTransform root, topBar, pageRoot, overlay, toastRt;
        TextMeshProUGUI goldText, dustText, toastText;
        CanvasGroup toastGroup;
        Coroutine toastRoutine;
        Button backButton;
        Page page = Page.Home;
        bool lastPortrait;
        int lastW, lastH;

        PlayerProfile P => PlayerProfile.Current;
        bool IsPortrait => Screen.height >= Screen.width;

        // ------------------------------------------------------------------
        // Aufbau
        // ------------------------------------------------------------------

        public void Init(MenuManager manager, Camera camera)
        {
            mm = manager;
            cam = camera;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            var cgo = new GameObject("Kesselwerk Menü");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            pageRoot = UIKit.Rect("Seite", root);
            UIKit.Stretch(pageRoot);

            BuildTopBar();
            BuildToast();

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

            var coin = UIKit.Badge(topBar, "Gold", new Color(0.95f, 0.72f, 0.15f), 70f, "G");
            var coinRt = (RectTransform)coin.transform.parent;
            coinRt.anchorMin = coinRt.anchorMax = new Vector2(0f, 0.5f);
            coinRt.anchoredPosition = new Vector2(60f, 0f);
            goldText = UIKit.Text(topBar, "GoldWert", "0", 42f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            goldText.rectTransform.anchorMin = goldText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            goldText.rectTransform.pivot = new Vector2(0f, 0.5f);
            goldText.rectTransform.anchoredPosition = new Vector2(105f, 0f);
            goldText.rectTransform.sizeDelta = new Vector2(200f, 60f);

            var dust = UIKit.Badge(topBar, "Staub", new Color(0.65f, 0.35f, 0.95f), 70f, "S");
            var dustRt = (RectTransform)dust.transform.parent;
            dustRt.anchorMin = dustRt.anchorMax = new Vector2(0f, 0.5f);
            dustRt.anchoredPosition = new Vector2(330f, 0f);
            dustText = UIKit.Text(topBar, "StaubWert", "0", 42f, Color.white, TextAlignmentOptions.MidlineLeft, true);
            dustText.rectTransform.anchorMin = dustText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            dustText.rectTransform.pivot = new Vector2(0f, 0.5f);
            dustText.rectTransform.anchoredPosition = new Vector2(375f, 0f);
            dustText.rectTransform.sizeDelta = new Vector2(200f, 60f);

            backButton = UIKit.Button(topBar, "Zurueck", "Zurück", Grey, () => ShowPage(Page.Home), 34f);
            var bRt = (RectTransform)backButton.transform;
            bRt.anchorMin = bRt.anchorMax = new Vector2(1f, 0.5f);
            bRt.pivot = new Vector2(1f, 0.5f);
            bRt.anchoredPosition = new Vector2(-24f, 0f);
            bRt.sizeDelta = new Vector2(220f, 84f);
        }

        void BuildToast()
        {
            toastRt = UIKit.Rect("Hinweis", root);
            UIKit.Place(toastRt, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(860f, 120f));
            var bg = UIKit.Img(toastRt, "Hintergrund", UIKit.Rounded, new Color(Gold.r, Gold.g, Gold.b, 0.95f));
            UIKit.Stretch(bg.rectTransform);
            var inner = UIKit.Img(toastRt, "Innen", UIKit.Rounded, new Color(0.12f, 0.1f, 0.16f));
            UIKit.Stretch(inner.rectTransform, 4, 4, 4, 4);
            toastText = UIKit.Text(toastRt, "Text", "", 42f, Color.white, TextAlignmentOptions.Center, true);
            UIKit.Stretch(toastText.rectTransform, 20, 6, 20, 6);
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 20f;
            toastText.fontSizeMax = 42f;
            toastGroup = toastRt.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
        }

        void Toast(string text)
        {
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(text));
        }

        IEnumerator ToastRoutine(string text)
        {
            toastRt.SetAsLastSibling();
            toastText.text = text;
            toastGroup.alpha = 1f;
            yield return new WaitForSeconds(1.6f);
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.3f; toastGroup.alpha = 1f - t; yield return null; }
            toastGroup.alpha = 0f;
        }

        void ApplyOrientation(bool force)
        {
            bool portrait = IsPortrait;
            if (!force && portrait == lastPortrait && Screen.width == lastW && Screen.height == lastH) return;
            lastPortrait = portrait;
            lastW = Screen.width;
            lastH = Screen.height;
            scaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = portrait ? 0f : 1f;
            UIKit.Anchor(topBar, 0f, 1f - BattleUI.TopFrac, 1f, 1f);
            CloseOverlay();
            ShowPage(page);
        }

        void LateUpdate()
        {
            ApplyOrientation(false);
            goldText.text = P.gold.ToString();
            dustText.text = P.dust.ToString();
        }

        void ShowPage(Page p)
        {
            page = p;
            for (int i = pageRoot.childCount - 1; i >= 0; i--) Destroy(pageRoot.GetChild(i).gameObject);
            backButton.gameObject.SetActive(p != Page.Home);
            Canvas.ForceUpdateCanvases();
            switch (p)
            {
                case Page.Home: BuildHome(); break;
                case Page.Shop: BuildShop(); break;
                case Page.Collection: BuildCollection(); break;
            }
        }

        RectTransform PageArea()
        {
            var area = UIKit.Rect("Bereich", pageRoot);
            UIKit.Anchor(area, 0f, 0f, 1f, 1f - BattleUI.TopFrac);
            return area;
        }

        static Image Panel(RectTransform parent, string name)
        {
            var shadow = UIKit.Img(parent, name + "_Schatten", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.5f));
            var frame = UIKit.Img(parent, name, UIKit.Rounded, new Color(Gold.r, Gold.g, Gold.b, 0.9f));
            var inner = UIKit.Img(frame.transform, "Innen", UIKit.Rounded, new Color(0.08f, 0.07f, 0.11f, 0.9f));
            UIKit.Stretch(inner.rectTransform, 4, 4, 4, 4);
            shadow.rectTransform.SetParent(frame.transform, false);
            shadow.rectTransform.SetAsFirstSibling();
            UIKit.Stretch(shadow.rectTransform, -16, -22, -16, -10);
            return frame;
        }

        // ------------------------------------------------------------------
        // Startseite
        // ------------------------------------------------------------------

        void BuildHome()
        {
            var area = PageArea();
            bool portrait = IsPortrait;

            var title = UIKit.Text(area, "Titel", "KESSELWERK", portrait ? 120f : 110f, Gold, TextAlignmentOptions.Center, true);
            title.rectTransform.anchorMin = new Vector2(0f, portrait ? 0.82f : 0.78f);
            title.rectTransform.anchorMax = new Vector2(1f, 0.97f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.outlineWidth = 0.22f;
            title.outlineColor = new Color32(60, 30, 0, 255);
            title.characterSpacing = 6f;
            var sub = UIKit.Text(area, "Untertitel", "Monster · Karten · Abenteuer", 40f, Color.white, TextAlignmentOptions.Center);
            sub.rectTransform.anchorMin = new Vector2(0f, portrait ? 0.78f : 0.72f);
            sub.rectTransform.anchorMax = new Vector2(1f, portrait ? 0.83f : 0.79f);
            sub.rectTransform.offsetMin = sub.rectTransform.offsetMax = Vector2.zero;
            sub.outlineWidth = 0.2f;
            sub.outlineColor = new Color32(0, 0, 0, 200);

            var panel = Panel(area, "Menuefeld");
            var prt = panel.rectTransform;
            if (portrait) { prt.anchorMin = new Vector2(0.08f, 0.04f); prt.anchorMax = new Vector2(0.92f, 0.34f); }
            else { prt.anchorMin = new Vector2(0.25f, 0.05f); prt.anchorMax = new Vector2(0.75f, 0.36f); }
            prt.offsetMin = prt.offsetMax = Vector2.zero;

            string deckInfo = P.DeckComplete ? $"Deck bereit ({P.deck.Count}/{PlayerProfile.DeckSize})" : $"Deck unvollständig ({P.deck.Count}/{PlayerProfile.DeckSize})";
            var info = UIKit.Text(panel.transform, "Info", $"{deckInfo}   ·   Siege {P.wins}   ·   Niederlagen {P.losses}", 30f, new Color(0.85f, 0.85f, 0.9f), TextAlignmentOptions.Center);
            info.rectTransform.anchorMin = new Vector2(0f, 0.82f);
            info.rectTransform.anchorMax = new Vector2(1f, 0.97f);
            info.rectTransform.offsetMin = new Vector2(20f, 0f);
            info.rectTransform.offsetMax = new Vector2(-20f, 0f);
            info.enableAutoSizing = true;
            info.fontSizeMin = 16f;
            info.fontSizeMax = 30f;

            var fight = UIKit.Button(panel.transform, "Kaempfen", "Kämpfen!", Orange, OnFight, 60f);
            var frt = (RectTransform)fight.transform;
            frt.anchorMin = new Vector2(0.06f, 0.45f);
            frt.anchorMax = new Vector2(0.94f, 0.8f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;

            var shop = UIKit.Button(panel.transform, "Shop", "Shop", Blue, () => ShowPage(Page.Shop), 44f);
            var srt = (RectTransform)shop.transform;
            srt.anchorMin = new Vector2(0.06f, 0.07f);
            srt.anchorMax = new Vector2(0.48f, 0.38f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;

            var coll = UIKit.Button(panel.transform, "Sammlung", "Sammlung & Deck", Green, () => ShowPage(Page.Collection), 44f);
            var crt = (RectTransform)coll.transform;
            crt.anchorMin = new Vector2(0.52f, 0.07f);
            crt.anchorMax = new Vector2(0.94f, 0.38f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
        }

        void OnFight()
        {
            if (!P.DeckComplete)
            {
                Toast($"Dein Deck braucht {PlayerProfile.DeckSize} Karten");
                ShowPage(Page.Collection);
                return;
            }
            GameFlow.ToBattle();
        }

        // ------------------------------------------------------------------
        // Shop
        // ------------------------------------------------------------------

        void BuildShop()
        {
            var area = PageArea();
            bool portrait = IsPortrait;

            var panel = Panel(area, "Shopfeld");
            var prt = panel.rectTransform;
            if (portrait) { prt.anchorMin = new Vector2(0.08f, 0.05f); prt.anchorMax = new Vector2(0.92f, 0.62f); }
            else { prt.anchorMin = new Vector2(0.2f, 0.06f); prt.anchorMax = new Vector2(0.8f, 0.9f); }
            prt.offsetMin = prt.offsetMax = Vector2.zero;

            var title = UIKit.Text(panel.transform, "Titel", "Monster-Pack", 56f, Gold, TextAlignmentOptions.Center, true);
            title.rectTransform.anchorMin = new Vector2(0f, 0.86f);
            title.rectTransform.anchorMax = new Vector2(1f, 0.97f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;

            // Pack-Grafik
            var packHolder = UIKit.Rect("Pack", panel.transform);
            packHolder.anchorMin = new Vector2(0.5f, 0.55f);
            packHolder.anchorMax = new Vector2(0.5f, 0.55f);
            packHolder.sizeDelta = new Vector2(CardView.W, CardView.H);
            Canvas.ForceUpdateCanvases();
            float h = panel.rectTransform.rect.height * 0.5f;
            packHolder.localScale = Vector3.one * Mathf.Clamp(h / CardView.H, 0.5f, 1.6f);
            CreatePack(packHolder);
            StartCoroutine(Bob(packHolder));

            var odds = UIKit.Text(panel.transform, "Chancen",
                $"{PlayerProfile.PackSize} Karten · mindestens 1 seltene\n<size=80%><color=#bbbbbb>Gewöhnlich 66% · <color=#5aa0ff>Selten 24%</color> · <color=#c070ff>Episch 8%</color> · <color=#ffa030>Legendär 2%</color></color></size>",
                30f, Color.white, TextAlignmentOptions.Center);
            odds.rectTransform.anchorMin = new Vector2(0f, 0.2f);
            odds.rectTransform.anchorMax = new Vector2(1f, 0.31f);
            odds.rectTransform.offsetMin = new Vector2(20f, 0f);
            odds.rectTransform.offsetMax = new Vector2(-20f, 0f);
            odds.enableAutoSizing = true;
            odds.fontSizeMin = 14f;
            odds.fontSizeMax = 30f;

            var buy = UIKit.Button(panel.transform, "Kaufen", $"Kaufen  ·  {PlayerProfile.PackPrice} Gold", Orange, BuyPack, 44f);
            var brt = (RectTransform)buy.transform;
            brt.anchorMin = new Vector2(0.12f, 0.05f);
            brt.anchorMax = new Vector2(0.88f, 0.17f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            buy.interactable = P.gold >= PlayerProfile.PackPrice;

            var hint = UIKit.Text(area, "Hinweis", "Siege bringen Gold. Doppelte Karten werden zu Staub –\ndamit stellst du in der Sammlung gezielt Karten her.",
                30f, Color.white, TextAlignmentOptions.Center);
            hint.rectTransform.anchorMin = new Vector2(0.05f, portrait ? 0.64f : 0.9f);
            hint.rectTransform.anchorMax = new Vector2(0.95f, portrait ? 0.74f : 0.99f);
            hint.rectTransform.offsetMin = hint.rectTransform.offsetMax = Vector2.zero;
            hint.outlineWidth = 0.2f;
            hint.outlineColor = new Color32(0, 0, 0, 220);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 14f;
            hint.fontSizeMax = 30f;
        }

        IEnumerator Bob(RectTransform rt)
        {
            Vector2 start = rt.anchoredPosition;
            while (rt != null)
            {
                rt.anchoredPosition = start + new Vector2(0f, Mathf.Sin(Time.time * 2f) * 10f);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.3f) * 2f);
                yield return null;
            }
        }

        /// <summary>Gestaltetes Pack (gleiche Größe wie eine Karte).</summary>
        static void CreatePack(RectTransform parent)
        {
            var glow = UIKit.Img(parent, "Leuchten", UIKit.Glow, new Color(1f, 0.7f, 0.25f, 0.6f));
            UIKit.Stretch(glow.rectTransform, -90, -90, -90, -90);
            var shadow = UIKit.Img(parent, "Schatten", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(shadow.rectTransform, -12, -20, -12, -4);
            var frame = UIKit.Img(parent, "Rahmen", UIKit.Rounded, Gold);
            UIKit.Stretch(frame.rectTransform);
            var inner = UIKit.Img(parent, "Innen", UIKit.Rounded, new Color(0.55f, 0.18f, 0.55f));
            UIKit.Stretch(inner.rectTransform, 7, 7, 7, 7);
            var grad = UIKit.Img(inner.transform, "Verlauf", UIKit.Gradient, new Color(1f, 0.6f, 0.2f, 0.7f));
            UIKit.Stretch(grad.rectTransform);
            Emblem(parent, 120f, new Vector2(0f, 20f));
            var label = UIKit.Text(parent, "Name", "MONSTER\nPACK", 30f, Color.white, TextAlignmentOptions.Center, true);
            label.rectTransform.anchorMin = new Vector2(0f, 0.04f);
            label.rectTransform.anchorMax = new Vector2(1f, 0.3f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(60, 0, 40, 255);
            // Zackenrand oben und unten wie bei einer Folienpackung
            for (int i = 0; i < 11; i++)
            {
                foreach (float y in new[] { 1f, 0f })
                {
                    var t = UIKit.Img(parent, "Zacke", null, Gold);
                    t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(i / 10f, y);
                    t.rectTransform.sizeDelta = new Vector2(14f, 14f);
                    t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
            }
        }

        static void Emblem(RectTransform parent, float size, Vector2 pos)
        {
            var ring = UIKit.Img(parent, "Emblem", UIKit.Circle, Gold);
            UIKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var innerC = UIKit.Img(ring.transform, "Innen", UIKit.Circle, new Color(0.18f, 0.08f, 0.2f));
            UIKit.Stretch(innerC.rectTransform, size * 0.07f, size * 0.07f, size * 0.07f, size * 0.07f);
            var k = UIKit.Text(ring.transform, "K", "K", size * 0.6f, Gold, TextAlignmentOptions.Center, true);
            UIKit.Stretch(k.rectTransform);
        }

        /// <summary>Kartenrückseite.</summary>
        static RectTransform CreateCardBack(Transform parent)
        {
            var rt = UIKit.Rect("Rueckseite", parent);
            rt.sizeDelta = new Vector2(CardView.W, CardView.H);
            var shadow = UIKit.Img(rt, "Schatten", UIKit.RoundedSoft, new Color(0f, 0f, 0f, 0.6f));
            UIKit.Stretch(shadow.rectTransform, -10, -18, -10, -4);
            var frame = UIKit.Img(rt, "Rahmen", UIKit.Rounded, Gold);
            UIKit.Stretch(frame.rectTransform);
            frame.raycastTarget = true;
            var inner = UIKit.Img(rt, "Innen", UIKit.Rounded, new Color(0.2f, 0.1f, 0.3f));
            UIKit.Stretch(inner.rectTransform, 8, 8, 8, 8);
            var grad = UIKit.Img(inner.transform, "Verlauf", UIKit.Gradient, new Color(0.5f, 0.25f, 0.8f, 0.6f));
            UIKit.Stretch(grad.rectTransform);
            var line = UIKit.Img(rt, "Linie", UIKit.Rounded, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            UIKit.Stretch(line.rectTransform, 18, 18, 18, 18);
            var inner2 = UIKit.Img(rt, "Innen2", UIKit.Rounded, new Color(0.22f, 0.11f, 0.32f));
            UIKit.Stretch(inner2.rectTransform, 21, 21, 21, 21);
            Emblem(rt, 110f, Vector2.zero);
            return rt;
        }

        void BuyPack()
        {
            var cards = P.OpenPack();
            if (cards == null) { Toast("Nicht genug Gold"); return; }

            var results = new List<(CardData card, bool isNew, int dust)>();
            foreach (var c in cards)
            {
                bool isNew = P.Count(c.id) == 0;
                int dust = P.AddCard(c);
                results.Add((c, isNew, dust));
            }
            P.Save();
            StartCoroutine(OpenPackRoutine(results));
        }

        IEnumerator OpenPackRoutine(List<(CardData card, bool isNew, int dust)> results)
        {
            var content = OpenOverlay("Pack geöffnet!");
            bool portrait = IsPortrait;
            Canvas.ForceUpdateCanvases();
            Rect r = root.rect;

            int n = results.Count;
            int cols = portrait ? 2 : n;
            int rows = Mathf.CeilToInt(n / (float)cols);
            float scale = Mathf.Min(r.width * 0.86f / (cols * CardView.W * 1.1f), r.height * 0.62f / (rows * CardView.H * 1.1f));
            float cw = CardView.W * scale * 1.1f;
            float ch = CardView.H * scale * 1.1f;

            int flipped = 0;
            Button next = null;
            Button revealAll = null;
            var flips = new List<Action>();

            for (int i = 0; i < n; i++)
            {
                var res = results[i];
                int col = i % cols;
                int row = i / cols;
                var slot = UIKit.Rect("Platz" + i, content);
                slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.52f);
                slot.anchoredPosition = new Vector2((col - (cols - 1) * 0.5f) * cw, ((rows - 1) * 0.5f - row) * ch);
                slot.sizeDelta = new Vector2(CardView.W, CardView.H);
                slot.localScale = Vector3.one * scale;

                var glow = UIKit.Img(slot, "Seltenheit", UIKit.Glow, new Color(1f, 1f, 1f, 0f));
                UIKit.Stretch(glow.rectTransform, -80, -80, -80, -80);

                var back = CreateCardBack(slot);
                StartCoroutine(DealIn(slot, i * 0.12f, scale));

                bool done = false;
                Action flip = () =>
                {
                    if (done) return;
                    done = true;
                    StartCoroutine(Flip(slot, back, glow, res.card, res.isNew, res.dust, scale, () =>
                    {
                        flipped++;
                        if (flipped >= n && next != null)
                        {
                            next.gameObject.SetActive(true);
                            if (revealAll != null) revealAll.gameObject.SetActive(false);
                        }
                    }));
                };
                flips.Add(flip);
                foreach (var b in back.GetComponentsInChildren<Image>())
                {
                    if (!b.raycastTarget) continue;
                    var bb = b.GetComponent<Button>();
                    if (bb == null) bb = b.gameObject.AddComponent<Button>();
                    bb.transition = Selectable.Transition.None;
                    bb.onClick.AddListener(() => flip());
                }
            }

            revealAll = OverlayButton(content, "Alle aufdecken", Blue, 0.5f, () => { foreach (var f in flips) f(); });
            next = OverlayButton(content, "Weiter", Orange, 0.5f, () => { CloseOverlay(); ShowPage(Page.Shop); mm.SpawnPals(); });
            next.gameObject.SetActive(false);
            yield break;
        }

        IEnumerator DealIn(RectTransform slot, float delay, float scale)
        {
            Vector2 target = slot.anchoredPosition;
            slot.anchoredPosition = target + new Vector2(0f, -900f);
            slot.localRotation = Quaternion.Euler(0f, 0f, 25f);
            yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < 1f && slot != null)
            {
                t += Time.deltaTime / 0.35f;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                slot.anchoredPosition = Vector2.Lerp(target + new Vector2(0f, -900f), target, k);
                slot.localRotation = Quaternion.Euler(0f, 0f, 25f * (1f - k));
                yield return null;
            }
            if (slot != null) { slot.anchoredPosition = target; slot.localRotation = Quaternion.identity; }
        }

        IEnumerator Flip(RectTransform slot, RectTransform back, Image glow, CardData card, bool isNew, int dust, float scale, Action onDone)
        {
            float t = 0f;
            while (t < 1f && slot != null)
            {
                t += Time.deltaTime / 0.15f;
                slot.localScale = new Vector3(scale * (1f - Mathf.Clamp01(t)), scale * (1f + 0.08f * t), 1f);
                yield return null;
            }
            if (slot == null) yield break;
            Destroy(back.gameObject);
            var view = CardView.Create(slot, card);
            view.rt.anchorMin = view.rt.anchorMax = new Vector2(0.5f, 0.5f);
            view.rt.anchoredPosition = Vector2.zero;
            view.button.interactable = false;

            Color rc = CardData.RarityColor(card.rarity);
            glow.color = new Color(rc.r, rc.g, rc.b, card.rarity == Rarity.Common ? 0.25f : 0.85f);

            var rarity = UIKit.Text(slot, "Seltenheit", CardData.RarityName(card.rarity), 28f, rc, TextAlignmentOptions.Center, true);
            UIKit.Place(rarity.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(300f, 44f));
            rarity.outlineWidth = 0.25f;
            rarity.outlineColor = new Color32(0, 0, 0, 230);

            if (isNew || dust > 0)
            {
                var tag = UIKit.Img(slot, "Marke", UIKit.Rounded, isNew ? new Color(0.9f, 0.2f, 0.25f) : new Color(0.55f, 0.3f, 0.85f));
                UIKit.Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -4f), new Vector2(isNew ? 100f : 150f, 44f));
                tag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
                var tt = UIKit.Text(tag.transform, "Text", isNew ? "NEU!" : $"+{dust} Staub", 26f, Color.white, TextAlignmentOptions.Center, true);
                UIKit.Stretch(tt.rectTransform);
            }

            t = 0f;
            while (t < 1f && slot != null)
            {
                t += Time.deltaTime / 0.2f;
                float k = Mathf.Clamp01(t);
                slot.localScale = new Vector3(scale * k, scale * (1.08f - 0.08f * k), 1f);
                yield return null;
            }
            if (slot != null) slot.localScale = Vector3.one * scale;
            onDone?.Invoke();

            // Pulsierendes Leuchten
            while (glow != null && card.rarity != Rarity.Common)
            {
                float p = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
                glow.color = new Color(rc.r, rc.g, rc.b, p);
                yield return null;
            }
        }

        // ------------------------------------------------------------------
        // Sammlung & Deck
        // ------------------------------------------------------------------

        void BuildCollection()
        {
            var area = PageArea();
            bool portrait = IsPortrait;

            // Kopfzeile mit Deck-Zähler und Knöpfen
            var header = UIKit.Rect("Kopf", area);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(20f, -130f);
            header.offsetMax = new Vector2(-20f, -10f);

            Color deckColor = P.DeckComplete ? new Color(0.5f, 1f, 0.55f) : new Color(1f, 0.6f, 0.4f);
            var deckText = UIKit.Text(header, "Deck", $"Deck  {P.deck.Count}/{PlayerProfile.DeckSize}", 48f, deckColor, TextAlignmentOptions.MidlineLeft, true);
            deckText.rectTransform.anchorMin = new Vector2(0f, 0f);
            deckText.rectTransform.anchorMax = new Vector2(0.4f, 1f);
            deckText.rectTransform.offsetMin = deckText.rectTransform.offsetMax = Vector2.zero;
            deckText.outlineWidth = 0.2f;
            deckText.outlineColor = new Color32(0, 0, 0, 220);

            var auto = UIKit.Button(header, "Auto", "Auto-Füllen", Blue, () => { P.AutoFillDeck(); ShowPage(Page.Collection); mm.SpawnPals(); }, 32f);
            var art = (RectTransform)auto.transform;
            art.anchorMin = new Vector2(0.42f, 0.12f);
            art.anchorMax = new Vector2(0.7f, 0.88f);
            art.offsetMin = art.offsetMax = Vector2.zero;

            var clear = UIKit.Button(header, "Leeren", "Deck leeren", Grey, () => { P.deck.Clear(); P.Save(); ShowPage(Page.Collection); }, 32f);
            var crt = (RectTransform)clear.transform;
            crt.anchorMin = new Vector2(0.72f, 0.12f);
            crt.anchorMax = new Vector2(1f, 0.88f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;

            // Kartenraster zum Scrollen
            var panel = Panel(area, "Sammlungsfeld");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.offsetMin = new Vector2(16f, 16f);
            prt.offsetMax = new Vector2(-16f, -140f);

            var scrollRt = UIKit.Rect("Scroll", panel.transform);
            UIKit.Stretch(scrollRt, 10, 10, 10, 10);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var viewport = UIKit.Img(scrollRt, "Sichtbereich", null, new Color(0f, 0f, 0f, 0.001f));
            viewport.raycastTarget = true;
            UIKit.Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = UIKit.Rect("Inhalt", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            Canvas.ForceUpdateCanvases();
            int cols = portrait ? 3 : 6;
            float spacing = 22f;
            float width = viewport.rectTransform.rect.width - 30f;
            float cellW = (width - spacing * (cols - 1)) / cols;
            float scale = cellW / CardView.W;
            float cellH = CardView.H * scale + 46f;

            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(15, 15, 15, 30);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.rectTransform;
            scroll.content = content;

            var cards = new List<CardData>(CardDatabase.All);
            cards.Sort((a, b) => a.rarity != b.rarity ? a.rarity.CompareTo(b.rarity) : a.cost.CompareTo(b.cost));

            foreach (var card in cards)
            {
                var cell = UIKit.Rect("Zelle_" + card.name, content);
                int owned = P.Count(card.id);
                int inDeck = P.InDeck(card.id);

                var view = CardView.Create(cell, card);
                view.rt.anchorMin = view.rt.anchorMax = new Vector2(0.5f, 1f);
                view.rt.pivot = new Vector2(0.5f, 1f);
                view.rt.anchoredPosition = Vector2.zero;
                view.rt.localScale = Vector3.one * scale;
                view.SetPlayable(false, owned == 0);
                if (owned == 0) view.group.alpha = 0.35f;
                var c = card;
                view.button.onClick.AddListener(() => ShowCardInfo(c));

                // Unter der Karte: Besitz / im Deck
                string line = owned == 0
                    ? $"<color=#ff9a7a>Fehlt</color> · {card.CraftCost} Staub"
                    : $"Besitz {owned}/{card.MaxCopies}   ·   <color=#7dff8a>im Deck {inDeck}</color>";
                var lt = UIKit.Text(cell, "Status", line, 24f, Color.white, TextAlignmentOptions.Center, true);
                lt.rectTransform.anchorMin = new Vector2(0f, 0f);
                lt.rectTransform.anchorMax = new Vector2(1f, 0f);
                lt.rectTransform.pivot = new Vector2(0.5f, 0f);
                lt.rectTransform.offsetMin = new Vector2(0f, 0f);
                lt.rectTransform.offsetMax = new Vector2(0f, 40f);
                lt.enableAutoSizing = true;
                lt.fontSizeMin = 12f;
                lt.fontSizeMax = 24f;

                if (inDeck > 0)
                {
                    var badge = UIKit.Badge(cell, "ImDeck", new Color(0.2f, 0.65f, 0.25f), 56f, inDeck.ToString());
                    UIKit.Place((RectTransform)badge.transform.parent, new Vector2(1f, 1f), new Vector2(-14f, -14f), new Vector2(56f, 56f));
                }
            }
        }

        void ShowCardInfo(CardData card)
        {
            var content = OpenOverlay(card.name);
            Canvas.ForceUpdateCanvases();
            Rect r = root.rect;
            float scale = Mathf.Min(r.height * 0.5f / CardView.H, r.width * 0.7f / CardView.W);

            var holder = UIKit.Rect("Karte", content);
            holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.56f);
            holder.sizeDelta = new Vector2(CardView.W, CardView.H);
            holder.localScale = Vector3.one * scale;
            var glow = UIKit.Img(holder, "Leuchten", UIKit.Glow, CardData.RarityColor(card.rarity) * new Color(1f, 1f, 1f, 0.6f));
            UIKit.Stretch(glow.rectTransform, -70, -70, -70, -70);
            var view = CardView.Create(holder, card);
            view.rt.anchorMin = view.rt.anchorMax = new Vector2(0.5f, 0.5f);
            view.rt.anchoredPosition = Vector2.zero;
            view.button.interactable = false;

            int owned = P.Count(card.id);
            int inDeck = P.InDeck(card.id);
            var info = UIKit.Text(content, "Info",
                $"<color=#{ColorUtility.ToHtmlStringRGB(CardData.RarityColor(card.rarity))}>{CardData.RarityName(card.rarity)}</color>   ·   Besitz {owned}/{card.MaxCopies}   ·   im Deck {inDeck}   ·   Deck {P.deck.Count}/{PlayerProfile.DeckSize}",
                32f, Color.white, TextAlignmentOptions.Center, true);
            info.rectTransform.anchorMin = new Vector2(0f, 0.22f);
            info.rectTransform.anchorMax = new Vector2(1f, 0.27f);
            info.rectTransform.offsetMin = new Vector2(20f, 0f);
            info.rectTransform.offsetMax = new Vector2(-20f, 0f);
            info.enableAutoSizing = true;
            info.fontSizeMin = 14f;
            info.fontSizeMax = 32f;

            var add = SmallButton(content, "+ Ins Deck", Green, new Vector2(0.18f, 0.15f), () =>
            {
                if (P.AddToDeck(card.id)) { Refresh(card); }
                else Toast(P.deck.Count >= PlayerProfile.DeckSize ? "Deck ist voll" : "Keine weitere Kopie verfügbar");
            });
            add.interactable = P.CanAddToDeck(card.id);

            var remove = SmallButton(content, "– Aus Deck", Grey, new Vector2(0.5f, 0.15f), () =>
            {
                if (P.RemoveFromDeck(card.id)) Refresh(card);
            });
            remove.interactable = inDeck > 0;

            bool canCraft = owned < card.MaxCopies;
            var craft = SmallButton(content, canCraft ? $"Herstellen\n<size=70%>{card.CraftCost} Staub</size>" : "Maximum", new Color(0.55f, 0.3f, 0.85f), new Vector2(0.82f, 0.15f), () =>
            {
                if (P.Craft(card)) { Toast($"{card.name} hergestellt!"); Refresh(card); }
                else Toast("Nicht genug Staub");
            });
            craft.interactable = canCraft && P.dust >= card.CraftCost;

            var close = SmallButton(content, "Schließen", Grey, new Vector2(0.5f, 0.05f), () => { CloseOverlay(); ShowPage(Page.Collection); mm.SpawnPals(); });
            ((RectTransform)close.transform).sizeDelta = new Vector2(420f, 90f);
        }

        void Refresh(CardData card)
        {
            ShowCardInfo(card);
        }

        Button SmallButton(RectTransform parent, string label, Color color, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
        {
            var b = UIKit.Button(parent, label, label, color, onClick, 34f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(IsPortrait ? 300f : 340f, 110f);
            rt.anchoredPosition = Vector2.zero;
            return b;
        }

        // ------------------------------------------------------------------
        // Overlay
        // ------------------------------------------------------------------

        RectTransform OpenOverlay(string title)
        {
            for (int i = overlay.childCount - 1; i >= 0; i--) Destroy(overlay.GetChild(i).gameObject);
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            toastRt.SetAsLastSibling();
            var dim = UIKit.Img(overlay, "Abdunkeln", null, new Color(0.02f, 0.02f, 0.05f, 0.85f));
            UIKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var content = UIKit.Rect("Inhalt", overlay);
            UIKit.Stretch(content);
            if (!string.IsNullOrEmpty(title))
            {
                var t = UIKit.Text(content, "Titel", title, 60f, Gold, TextAlignmentOptions.Center, true);
                t.rectTransform.anchorMin = new Vector2(0f, 0.88f);
                t.rectTransform.anchorMax = new Vector2(1f, 0.96f);
                t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.outlineWidth = 0.2f;
                t.outlineColor = new Color32(0, 0, 0, 200);
            }
            return content;
        }

        void CloseOverlay()
        {
            if (overlay == null) return;
            for (int i = overlay.childCount - 1; i >= 0; i--) Destroy(overlay.GetChild(i).gameObject);
            overlay.gameObject.SetActive(false);
        }

        Button OverlayButton(RectTransform content, string label, Color color, float xFrac, Action onClick)
        {
            var b = UIKit.Button(content, label, label, color, () => onClick?.Invoke(), 40f);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(xFrac, 0.1f);
            rt.sizeDelta = new Vector2(440f, 110f);
            rt.anchoredPosition = Vector2.zero;
            return b;
        }
    }
}
