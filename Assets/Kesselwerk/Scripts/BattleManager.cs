using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Prototyp: komplettes Duell gegen eine einfache KI.
    /// Baut Arena, Kamera und Licht selbst auf – es reicht, in einer beliebigen Szene auf Play zu drücken.
    /// Oberfläche bewusst simpel (IMGUI), passt sich hochkant und quer an.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        const int SlotCount = 3;
        const int StartHp = 20;
        const int MaxEnergyCap = 10;
        const int StartHand = 4;
        const int MaxHand = 7;

        class Side
        {
            public string name;
            public bool isPlayer;
            public int hp = StartHp;
            public int energy;
            public int maxEnergy;
            public List<CardData> deck = new List<CardData>();
            public List<CardData> hand = new List<CardData>();
            public Creature[] slots = new Creature[SlotCount];
        }

        class Popup
        {
            public Vector3 worldPos;
            public string text;
            public Color color;
            public float born;
        }

        Side player;
        Side enemy;
        bool playerTurn;
        bool busy;
        bool gameOver;
        string resultText = "";
        string banner = "";
        float bannerUntil;
        readonly List<Popup> popups = new List<Popup>();

        Camera cam;
        Vector3 camShake;
        float shakeUntil;

        // UI-Maße (virtuelle Einheiten, werden skaliert)
        float uiScale = 1f;
        const float TopBar = 44f;
        const float HandPortrait = 210f;
        const float HandLandscape = 160f;

        GUIStyle cardStyle, titleStyle, smallStyle, statStyle, costStyle, bannerStyle, infoStyle, buttonStyle, worldStatStyle;

        // ------------------------------------------------------------------
        // Aufbau
        // ------------------------------------------------------------------

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            SetupScene();
            NewGame();
        }

        void SetupScene()
        {
            // Hintergrund-Kamera füllt die Bereiche hinter der Oberfläche
            var bg = new GameObject("BG Kamera").AddComponent<Camera>();
            bg.depth = -100;
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = new Color(0.10f, 0.09f, 0.13f);
            bg.cullingMask = 0;

            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.depth = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.55f, 0.75f);
            cam.fieldOfView = 45f;

            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Sonne").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Boden
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Arena";
            Destroy(ground.GetComponent<Collider>());
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(9.5f, 0.5f, 11f);
            ground.GetComponent<Renderer>().material.color = new Color(0.42f, 0.62f, 0.32f);

            // Mittellinie
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(line.GetComponent<Collider>());
            line.transform.position = new Vector3(0f, 0.01f, 0f);
            line.transform.localScale = new Vector3(9.5f, 0.02f, 0.08f);
            line.GetComponent<Renderer>().material.color = new Color(0.9f, 0.9f, 0.8f);

            // Slot-Markierungen
            for (int i = 0; i < SlotCount; i++)
            {
                MakeSlotMarker(SlotPos(true, i));
                MakeSlotMarker(SlotPos(false, i));
            }

            // Die "Kessel" der Spieler = Ziel für direkten Schaden
            MakeKessel(FacePos(true), new Color(0.3f, 0.6f, 1f));
            MakeKessel(FacePos(false), new Color(1f, 0.35f, 0.3f));
        }

        void MakeSlotMarker(Vector3 pos)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(m.GetComponent<Collider>());
            m.transform.position = new Vector3(pos.x, 0.01f, pos.z);
            m.transform.localScale = new Vector3(1.8f, 0.01f, 1.8f);
            m.GetComponent<Renderer>().material.color = new Color(0.33f, 0.5f, 0.26f);
        }

        void MakeKessel(Vector3 pos, Color glow)
        {
            var pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pot.GetComponent<Collider>());
            pot.name = "Kessel";
            pot.transform.position = new Vector3(pos.x, 0.45f, pos.z);
            pot.transform.localScale = new Vector3(1.4f, 0.45f, 1.4f);
            pot.GetComponent<Renderer>().material.color = new Color(0.18f, 0.18f, 0.2f);

            var brew = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(brew.GetComponent<Collider>());
            brew.transform.position = new Vector3(pos.x, 0.92f, pos.z);
            brew.transform.localScale = new Vector3(1.2f, 0.03f, 1.2f);
            brew.GetComponent<Renderer>().material.color = glow;
        }

        static Vector3 SlotPos(bool isPlayer, int i) =>
            new Vector3((i - 1) * 2.8f, 0f, isPlayer ? -1.8f : 1.8f);

        static Vector3 FacePos(bool isPlayer) =>
            new Vector3(0f, 0.5f, isPlayer ? -4.3f : 4.3f);

        // ------------------------------------------------------------------
        // Spielablauf
        // ------------------------------------------------------------------

        void NewGame()
        {
            StopAllCoroutines();
            foreach (var c in FindObjectsByType<Creature>(FindObjectsSortMode.None))
                Destroy(c.gameObject);
            popups.Clear();

            player = new Side { name = "Du", isPlayer = true, deck = CardDatabase.BuildDeck() };
            enemy = new Side { name = "Gegner", isPlayer = false, deck = CardDatabase.BuildDeck() };
            for (int i = 0; i < StartHand; i++) { Draw(player); Draw(enemy); }

            gameOver = false;
            busy = false;
            resultText = "";
            StartTurn(player);
        }

        Side Opp(Side s) => s == player ? enemy : player;

        void Draw(Side s)
        {
            if (s.deck.Count == 0 || s.hand.Count >= MaxHand) return;
            s.hand.Add(s.deck[0]);
            s.deck.RemoveAt(0);
        }

        void StartTurn(Side s)
        {
            s.maxEnergy = Mathf.Min(MaxEnergyCap, s.maxEnergy + 1);
            s.energy = s.maxEnergy;
            Draw(s);
            foreach (var c in s.slots) if (c != null) c.justSummoned = false;
            playerTurn = s.isPlayer;
            ShowBanner(s.isPlayer ? "Dein Zug" : "Gegner ist dran");
        }

        void ShowBanner(string text, float seconds = 1.2f)
        {
            banner = text;
            bannerUntil = Time.time + seconds;
        }

        void TryPlayCard(int handIndex)
        {
            if (busy || !playerTurn || gameOver) return;
            var card = player.hand[handIndex];
            if (card.cost > player.energy) { ShowBanner("Nicht genug Energie"); return; }
            int slot = FirstFreeSlot(player);
            if (slot < 0) { ShowBanner("Kein freier Platz"); return; }

            player.energy -= card.cost;
            player.hand.RemoveAt(handIndex);
            Summon(player, card, slot);
        }

        static int FirstFreeSlot(Side s)
        {
            for (int i = 0; i < SlotCount; i++) if (s.slots[i] == null) return i;
            return -1;
        }

        void Summon(Side s, CardData card, int slot)
        {
            var c = Creature.Spawn(card, SlotPos(s.isPlayer, slot), s.isPlayer);
            c.justSummoned = true;
            s.slots[slot] = c;
            StartCoroutine(c.SpawnAnim());
        }

        void OnEndTurnPressed()
        {
            if (busy || !playerTurn || gameOver) return;
            StartCoroutine(EndTurn(player));
        }

        IEnumerator EndTurn(Side side)
        {
            busy = true;
            yield return AttackPhase(side);
            if (CheckGameOver()) { busy = false; yield break; }

            var next = Opp(side);
            StartTurn(next);

            if (!next.isPlayer)
            {
                yield return new WaitForSeconds(0.8f);
                yield return AiPlay(next);
                yield return new WaitForSeconds(0.4f);
                yield return EndTurn(next);
                yield break;
            }
            busy = false;
        }

        /// <summary>Alle Monster, die nicht gerade erst beschworen wurden, greifen gegenüber an.</summary>
        IEnumerator AttackPhase(Side side)
        {
            var opp = Opp(side);
            for (int i = 0; i < SlotCount; i++)
            {
                var attacker = side.slots[i];
                if (attacker == null || attacker.justSummoned) continue;

                var target = opp.slots[i];
                Vector3 targetPos = target != null ? target.transform.position : FacePos(opp.isPlayer);

                yield return attacker.LungeOut(targetPos);

                if (target != null)
                {
                    target.health -= attacker.attack;
                    attacker.health -= target.attack;
                    AddPopup(target.transform.position, "-" + attacker.attack, new Color(1f, 0.3f, 0.3f));
                    if (target.attack > 0)
                        AddPopup(attacker.transform.position, "-" + target.attack, new Color(1f, 0.6f, 0.2f));
                    StartCoroutine(target.HitFlash());
                }
                else
                {
                    opp.hp -= attacker.attack;
                    AddPopup(FacePos(opp.isPlayer), "-" + attacker.attack, new Color(1f, 0.2f, 0.2f));
                    Shake(0.25f);
                }

                yield return attacker.LungeBack();
                if (target != null && attacker.health > 0) StartCoroutine(attacker.HitFlash());

                // Besiegte entfernen
                if (target != null && target.health <= 0) { opp.slots[i] = null; StartCoroutine(target.Die()); }
                if (attacker.health <= 0) { side.slots[i] = null; StartCoroutine(attacker.Die()); }

                yield return new WaitForSeconds(0.25f);
                if (opp.hp <= 0) yield break;
            }
        }

        /// <summary>Sehr einfache KI: spielt die teuerste bezahlbare Karte, bevorzugt Plätze gegenüber deinen Monstern.</summary>
        IEnumerator AiPlay(Side ai)
        {
            while (true)
            {
                int best = -1;
                for (int i = 0; i < ai.hand.Count; i++)
                {
                    if (ai.hand[i].cost > ai.energy) continue;
                    if (best < 0 || ai.hand[i].cost > ai.hand[best].cost) best = i;
                }
                if (best < 0) yield break;

                int slot = -1;
                var opp = Opp(ai);
                for (int i = 0; i < SlotCount; i++)
                    if (ai.slots[i] == null && opp.slots[i] != null) { slot = i; break; }
                if (slot < 0) slot = FirstFreeSlot(ai);
                if (slot < 0) yield break;

                var card = ai.hand[best];
                ai.energy -= card.cost;
                ai.hand.RemoveAt(best);
                Summon(ai, card, slot);
                yield return new WaitForSeconds(0.6f);
            }
        }

        bool CheckGameOver()
        {
            if (player.hp <= 0 || enemy.hp <= 0)
            {
                gameOver = true;
                resultText = player.hp <= 0 && enemy.hp <= 0 ? "Unentschieden!"
                           : player.hp <= 0 ? "Verloren!" : "Gewonnen!";
                return true;
            }
            return false;
        }

        void AddPopup(Vector3 pos, string text, Color color) =>
            popups.Add(new Popup { worldPos = pos + Vector3.up * 1.6f, text = text, color = color, born = Time.time });

        void Shake(float seconds) => shakeUntil = Time.time + seconds;

        // ------------------------------------------------------------------
        // Kamera (passt sich Hoch- und Querformat an)
        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (cam == null) return;
            bool portrait = Screen.height >= Screen.width;
            uiScale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 420f);
            float vh = Screen.height / uiScale;

            float bottom = (portrait ? HandPortrait : HandLandscape) / vh;
            float top = TopBar / vh;
            cam.rect = new Rect(0f, bottom, 1f, Mathf.Max(0.1f, 1f - bottom - top));

            // Abstand so wählen, dass die ganze Arena-Breite ins Bild passt
            float aspect = cam.pixelHeight > 0 ? cam.pixelWidth / (float)cam.pixelHeight : 1f;
            float halfV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            float needHalfWidth = 4.6f;
            float dist = Mathf.Max(10.5f, needHalfWidth / Mathf.Tan(halfH));

            Vector3 target = new Vector3(0f, 0f, -0.3f);
            Vector3 dir = Quaternion.Euler(52f, 0f, 0f) * Vector3.back;
            camShake = Time.time < shakeUntil ? Random.insideUnitSphere * 0.15f : Vector3.zero;
            cam.transform.position = target + dir * dist + camShake;
            cam.transform.LookAt(target);
        }

        // ------------------------------------------------------------------
        // Oberfläche (IMGUI – reicht für den Prototyp, wird später ersetzt)
        // ------------------------------------------------------------------

        void InitStyles()
        {
            if (cardStyle != null) return;
            cardStyle = new GUIStyle(GUI.skin.button);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = false, clipping = TextClipping.Clip };
            titleStyle.normal.textColor = Color.white;
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic, alignment = TextAnchor.UpperCenter, wordWrap = true };
            smallStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f);
            statStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
            statStyle.normal.textColor = Color.white;
            costStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            costStyle.normal.textColor = new Color(0.6f, 0.9f, 1f);
            bannerStyle = new GUIStyle(GUI.skin.box) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bannerStyle.normal.textColor = Color.white;
            infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            infoStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            worldStatStyle = new GUIStyle(GUI.skin.box) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            worldStatStyle.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (player == null) return;
            InitStyles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            float vw = Screen.width / uiScale;
            float vh = Screen.height / uiScale;
            bool portrait = Screen.height >= Screen.width;

            DrawWorldLabels();

            // Obere Leiste: Gegner
            GUI.Box(new Rect(0, 0, vw, TopBar), GUIContent.none);
            GUI.Label(new Rect(12, 0, vw - 24, TopBar),
                $"Gegner   Leben {Mathf.Max(0, enemy.hp)}   Karten auf der Hand {enemy.hand.Count}", infoStyle);

            // Untere Leiste: du
            float handH = portrait ? HandPortrait : HandLandscape;
            float barY = vh - handH;
            GUI.Box(new Rect(0, barY, vw, handH), GUIContent.none);
            GUI.Label(new Rect(12, barY + 4, vw - 160, 34),
                $"Du   Leben {Mathf.Max(0, player.hp)}   Energie {player.energy}/{player.maxEnergy}   Deck {player.deck.Count}", infoStyle);

            GUI.enabled = playerTurn && !busy && !gameOver;
            if (GUI.Button(new Rect(vw - 140, barY + 4, 128, 34), "Zug beenden", buttonStyle))
                OnEndTurnPressed();
            GUI.enabled = true;

            DrawHand(new Rect(6, barY + 42, vw - 12, handH - 48));

            // Hinweis-Banner
            if (Time.time < bannerUntil && !gameOver)
                GUI.Box(new Rect(vw * 0.5f - 130, TopBar + 12, 260, 46), banner, bannerStyle);

            // Spielende
            if (gameOver)
            {
                var r = new Rect(vw * 0.5f - 150, vh * 0.35f, 300, 130);
                GUI.Box(r, resultText, bannerStyle);
                if (GUI.Button(new Rect(r.x + 60, r.y + 80, 180, 40), "Neues Spiel", buttonStyle))
                    NewGame();
            }
        }

        void DrawHand(Rect area)
        {
            int n = player.hand.Count;
            if (n == 0) return;
            float gap = 6f;
            float cardW = Mathf.Min(112f, (area.width - gap * (n - 1)) / n);
            float cardH = Mathf.Min(area.height, cardW * 1.45f);
            float total = n * cardW + (n - 1) * gap;
            float x = area.x + (area.width - total) * 0.5f;
            float y = area.y + (area.height - cardH) * 0.5f;

            for (int i = 0; i < n; i++)
            {
                var card = player.hand[i];
                var r = new Rect(x + i * (cardW + gap), y, cardW, cardH);
                bool affordable = card.cost <= player.energy && playerTurn && !busy && !gameOver;

                GUI.backgroundColor = Color.Lerp(card.color, Color.black, 0.35f);
                GUI.color = affordable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                if (GUI.Button(r, GUIContent.none, cardStyle))
                {
                    TryPlayCard(i);
                    GUI.color = Color.white;
                    GUI.backgroundColor = Color.white;
                    return; // Hand hat sich geändert
                }
                GUI.backgroundColor = Color.white;

                // Kosten (blau, oben links)
                DrawStatBadge(new Rect(r.x + 3, r.y + 3, 24, 24), card.cost.ToString(), new Color(0.2f, 0.45f, 0.95f));

                // Name (Schrift wird bei langen Namen / schmalen Karten kleiner)
                titleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(cardW / Mathf.Max(6, card.name.Length) * 1.7f), 9, 14);
                GUI.Label(new Rect(r.x + 2, r.y + 28, r.width - 4, 22), card.name, titleStyle);

                if (cardH > 120f)
                    GUI.Label(new Rect(r.x + 4, r.y + 52, r.width - 8, r.height - 82), card.text, smallStyle);

                // Angriff (rot, unten links) und Leben (grün, unten rechts)
                DrawStatBadge(new Rect(r.x + 3, r.yMax - 27, 24, 24), card.attack.ToString(), new Color(0.85f, 0.2f, 0.2f));
                DrawStatBadge(new Rect(r.xMax - 27, r.yMax - 27, 24, 24), card.health.ToString(), new Color(0.2f, 0.7f, 0.25f));
                GUI.color = Color.white;
            }
        }

        static Texture2D whiteTex;

        static void DrawRect(Rect r, Color color)
        {
            if (whiteTex == null)
            {
                whiteTex = new Texture2D(1, 1);
                whiteTex.SetPixel(0, 0, Color.white);
                whiteTex.Apply();
            }
            var old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            GUI.DrawTexture(r, whiteTex);
            GUI.color = old;
        }

        void DrawStatBadge(Rect r, string text, Color color)
        {
            DrawRect(r, new Color(0f, 0f, 0f, 0.7f));
            DrawRect(new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f), color);
            GUI.Label(r, text, statStyle);
        }

        void DrawWorldLabels()
        {
            if (cam == null) return;

            foreach (var side in new[] { player, enemy })
            {
                foreach (var c in side.slots)
                {
                    if (c == null) continue;
                    Vector3 sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * (1.3f + c.card.cost * 0.12f));
                    if (sp.z < 0) continue;
                    float gx = sp.x / uiScale;
                    float gy = (Screen.height - sp.y) / uiScale;

                    // Lebensbalken
                    float frac = Mathf.Clamp01(c.health / (float)Mathf.Max(1, c.maxHealth));
                    var bar = new Rect(gx - 30, gy - 4, 60, 8);
                    DrawRect(bar, new Color(0f, 0f, 0f, 0.6f));
                    DrawRect(new Rect(bar.x + 1, bar.y + 1, (bar.width - 2) * frac, bar.height - 2),
                             Color.Lerp(new Color(0.9f, 0.2f, 0.2f), new Color(0.25f, 0.85f, 0.3f), frac));

                    // Angriff links, Leben rechts
                    DrawStatBadge(new Rect(gx - 54, gy - 11, 22, 22), c.attack.ToString(), new Color(0.85f, 0.2f, 0.2f));
                    DrawStatBadge(new Rect(gx + 32, gy - 11, 22, 22), Mathf.Max(0, c.health).ToString(), new Color(0.2f, 0.7f, 0.25f));

                    if (c.justSummoned && side.isPlayer)
                        GUI.Label(new Rect(gx - 40, gy + 6, 80, 18), "schläft", smallStyle);
                }
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.time - p.born;
                if (age > 1.1f) { popups.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(p.worldPos + Vector3.up * age * 1.2f);
                if (sp.z < 0) continue;
                float gx = sp.x / uiScale;
                float gy = (Screen.height - sp.y) / uiScale;
                var style = new GUIStyle(bannerStyle) { fontSize = 24 };
                style.normal.background = null;
                style.normal.textColor = new Color(p.color.r, p.color.g, p.color.b, 1f - age / 1.1f);
                GUI.Label(new Rect(gx - 40, gy - 20, 80, 40), p.text, style);
            }
        }
    }
}
