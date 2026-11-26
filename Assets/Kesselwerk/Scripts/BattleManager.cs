using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Spielregeln und Ablauf.
    /// Zug: Ziehphase → Hauptphase (Karten ausspielen) → Kampfphase (Angreifer und Ziel wählen) → Ende.
    /// Jedes Monster darf einmal pro Zug angreifen (auch im Zug des Ausspielens).
    /// Den Gegner selbst darf man erst angreifen, wenn er keine Monster mehr hat.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public enum Phase { Setup, Draw, Main, Battle, End, EnemyTurn, GameOver }
        public enum Highlight { None, CanAttack, Selected, Target, Exhausted }

        const int StartHp = 25;
        const int MaxEnergyCap = 10;
        const int StartHand = 4;
        const int MaxHand = 7;

        public class Side
        {
            public string name;
            public bool isPlayer;
            public int hp = StartHp;
            public int energy;
            public int maxEnergy;
            public List<CardData> deck = new List<CardData>();
            public List<CardData> hand = new List<CardData>();
            public Creature[] slots = new Creature[ArenaBuilder.SlotCount];

            public IEnumerable<Creature> Creatures
            {
                get { foreach (var c in slots) if (c != null && !c.IsDead) yield return c; }
            }

            public int CreatureCount
            {
                get { int n = 0; foreach (var c in slots) if (c != null && !c.IsDead) n++; return n; }
            }
        }

        public Side Player { get; private set; }
        public Side Enemy { get; private set; }
        public Phase CurrentPhase { get; private set; } = Phase.Setup;
        public bool IsPlayerTurn => CurrentPhase == Phase.Draw || CurrentPhase == Phase.Main || CurrentPhase == Phase.Battle || CurrentPhase == Phase.End;

        Camera cam;
        BattleUI ui;
        Creature selected;
        bool busy;
        float shakeUntil;

        // ------------------------------------------------------------------
        // Start
        // ------------------------------------------------------------------

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = true;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            cam = ArenaBuilder.SetupCameraAndLight();
            ArenaBuilder.BuildArena();

            ui = gameObject.AddComponent<BattleUI>();
            ui.Init(this, cam);

            NewGame();
        }

        void NewGame()
        {
            StopAllCoroutines();
            ui.ClearCreatures();
            ui.CloseOverlay();
            foreach (var c in FindObjectsByType<Creature>(FindObjectsSortMode.None)) Destroy(c.gameObject);

            Player = new Side { name = "Du", isPlayer = true, deck = CardDatabase.BuildDeck() };
            Enemy = new Side { name = "Gegner", isPlayer = false, deck = CardDatabase.BuildDeck() };
            for (int i = 0; i < StartHand; i++) { Draw(Player); Draw(Enemy); }

            selected = null;
            busy = false;
            StartCoroutine(PlayerTurn());
        }

        Side Opp(Side s) => s == Player ? Enemy : Player;

        CardData Draw(Side s)
        {
            if (s.deck.Count == 0 || s.hand.Count >= MaxHand) return null;
            var c = s.deck[0];
            s.deck.RemoveAt(0);
            s.hand.Add(c);
            return c;
        }

        void BeginTurn(Side s)
        {
            s.maxEnergy = Mathf.Min(MaxEnergyCap, s.maxEnergy + 1);
            s.energy = s.maxEnergy;
            foreach (var c in s.Creatures) c.hasAttacked = false;
        }

        void SetPhase(Phase p)
        {
            CurrentPhase = p;
            if (p != Phase.Battle) selected = null;
            ui.RefreshAll();
        }

        // ------------------------------------------------------------------
        // Zug des Spielers
        // ------------------------------------------------------------------

        IEnumerator PlayerTurn()
        {
            BeginTurn(Player);
            SetPhase(Phase.Draw);
            ui.Banner("Dein Zug");
            yield return new WaitForSeconds(0.9f);

            var drawn = Draw(Player);
            ui.RefreshAll();
            if (drawn != null)
            {
                bool done = false;
                ui.ShowDrawReveal(drawn, () => done = true);
                while (!done) yield return null;
            }
            else
            {
                ui.Banner(Player.hand.Count >= MaxHand ? "Hand ist voll" : "Deck ist leer");
                yield return new WaitForSeconds(1f);
            }

            SetPhase(Phase.Main);
            ui.Banner("Hauptphase");
        }

        public void OnMainButton()
        {
            if (busy || ui.OverlayOpen) return;
            switch (CurrentPhase)
            {
                case Phase.Main:
                    SetPhase(Phase.Battle);
                    ui.Banner("Kampfphase");
                    break;
                case Phase.Battle:
                    StartCoroutine(EndPlayerTurn());
                    break;
            }
        }

        IEnumerator EndPlayerTurn()
        {
            SetPhase(Phase.End);
            ui.Banner("Zugende");
            yield return new WaitForSeconds(0.8f);
            yield return EnemyTurn();
        }

        public bool CanPlayFromHand(CardData card) =>
            CurrentPhase == Phase.Main && !busy && card.cost <= Player.energy && FirstFreeSlot(Player) >= 0;

        public void OnHandCardTapped(int index)
        {
            if (busy || index < 0 || index >= Player.hand.Count) return;
            var card = Player.hand[index];

            if (CurrentPhase == Phase.Main)
            {
                string why = card.cost > Player.energy ? "Nicht genug Energie"
                           : FirstFreeSlot(Player) < 0 ? "Kein freier Platz"
                           : null;
                string label = why ?? $"Ausspielen ({card.cost} Energie)";
                ui.ShowCardDetail(card, "Deine Karte", label, why == null, () => StartCoroutine(PlayCard(Player, card)));
            }
            else
            {
                ui.ShowCardDetail(card, "Deine Karte", null, false, null);
            }
        }

        public void OnCreatureTapped(Creature c)
        {
            if (busy || c == null || c.IsDead) return;

            if (CurrentPhase == Phase.Battle)
            {
                if (c.isPlayer)
                {
                    if (c.hasAttacked) { ui.Banner("Hat schon angegriffen", 0.8f); return; }
                    selected = selected == c ? null : c;
                    ui.RefreshInfo();
                    return;
                }
                if (selected != null)
                {
                    if (IsValidTarget(Enemy, c)) StartCoroutine(Attack(Player, selected, c));
                    else ui.Banner(HasGuardian(Enemy) ? "Erst den Wächter angreifen!" : "Ungültiges Ziel", 1f);
                    return;
                }
            }

            // Sonst: Karte anschauen
            ui.ShowCardDetail(c.card, c.isPlayer ? "Dein Monster" : "Gegnerisches Monster", null, false, null);
        }

        public void OnFaceTapped()
        {
            if (busy || !CanAttackFaceNow) return;
            StartCoroutine(Attack(Player, selected, null));
        }

        public bool CanAttackFaceNow =>
            CurrentPhase == Phase.Battle && !busy && selected != null && !selected.hasAttacked && Enemy.CreatureCount == 0;

        // ------------------------------------------------------------------
        // Anzeige-Hilfen für die Oberfläche
        // ------------------------------------------------------------------

        public int PhaseIndex
        {
            get
            {
                switch (CurrentPhase)
                {
                    case Phase.Draw: return 0;
                    case Phase.Main: return 1;
                    case Phase.Battle: return 2;
                    case Phase.End: return 3;
                    default: return -1;
                }
            }
        }

        public string MainButtonLabel
        {
            get
            {
                switch (CurrentPhase)
                {
                    case Phase.Main: return "Zum Kampf";
                    case Phase.Battle: return "Zug beenden";
                    case Phase.EnemyTurn: return "Gegner ist dran";
                    case Phase.GameOver: return "Spielende";
                    default: return "...";
                }
            }
        }

        public bool MainButtonEnabled => !busy && (CurrentPhase == Phase.Main || CurrentPhase == Phase.Battle);

        public string HintText
        {
            get
            {
                switch (CurrentPhase)
                {
                    case Phase.Main: return "Tippe eine Karte an, um sie anzusehen und auszuspielen";
                    case Phase.Battle:
                        if (selected == null) return "Wähle ein eigenes Monster zum Angreifen";
                        return Enemy.CreatureCount == 0 ? "Greife den Gegner direkt an!" : "Wähle ein gegnerisches Monster";
                    case Phase.EnemyTurn: return "Der Gegner ist am Zug";
                    default: return "";
                }
            }
        }

        public Highlight GetHighlight(Creature c)
        {
            if (CurrentPhase != Phase.Battle || busy || c == null || c.IsDead) return Highlight.None;
            if (c.isPlayer)
            {
                if (c == selected) return Highlight.Selected;
                return c.hasAttacked ? Highlight.Exhausted : Highlight.CanAttack;
            }
            if (selected != null && IsValidTarget(Enemy, c)) return Highlight.Target;
            return Highlight.None;
        }

        // ------------------------------------------------------------------
        // Regeln
        // ------------------------------------------------------------------

        static int FirstFreeSlot(Side s)
        {
            // Von der Mitte nach außen füllen
            int[] order = { 1, 2, 0, 3 };
            foreach (int i in order)
                if (i < s.slots.Length && (s.slots[i] == null || s.slots[i].IsDead)) return i;
            return -1;
        }

        static bool HasGuardian(Side s)
        {
            foreach (var c in s.Creatures) if (c.Keyword == Keyword.Guardian) return true;
            return false;
        }

        static bool IsValidTarget(Side defender, Creature target)
        {
            if (target == null || target.IsDead) return false;
            if (HasGuardian(defender) && target.Keyword != Keyword.Guardian) return false;
            return true;
        }

        IEnumerator PlayCard(Side side, CardData card)
        {
            if (card.cost > side.energy) yield break;
            int slot = FirstFreeSlot(side);
            if (slot < 0) yield break;

            busy = true;
            side.energy -= card.cost;
            side.hand.Remove(card);
            ui.RefreshAll();

            var c = Creature.Spawn(card, ArenaBuilder.SlotPos(side.isPlayer, slot), side.isPlayer, slot);
            side.slots[slot] = c;
            ui.AddCreature(c);
            yield return c.SpawnAnim();

            yield return ApplyOnPlay(side, c);
            yield return RemoveDead();

            busy = false;
            ui.RefreshAll();
            CheckGameOver();
        }

        IEnumerator ApplyOnPlay(Side side, Creature c)
        {
            var card = c.card;
            var opp = Opp(side);
            switch (card.onPlay)
            {
                case OnPlayEffect.HealOwner:
                    side.hp = Mathf.Min(StartHp, side.hp + card.effectValue);
                    var face = ArenaBuilder.FacePos(side.isPlayer);
                    Fx.Burst(face + Vector3.up, new Color(0.4f, 2.2f, 0.7f), 30, 2.5f, 0.2f, 0.9f, -0.6f);
                    ui.FloatText(face + Vector3.up * 1.8f, "+" + card.effectValue, new Color(0.4f, 1f, 0.5f));
                    ui.Banner($"{card.name}: +{card.effectValue} Leben", 0.9f);
                    yield return new WaitForSeconds(0.6f);
                    break;

                case OnPlayEffect.DamageAllEnemies:
                    ui.Banner($"{card.name}: {card.effectValue} Schaden an allen!", 0.9f);
                    yield return new WaitForSeconds(0.3f);
                    foreach (var t in new List<Creature>(opp.Creatures)) Damage(t, card.effectValue, new Color(2.5f, 0.9f, 0.2f));
                    yield return new WaitForSeconds(0.6f);
                    break;

                case OnPlayEffect.DamageRandomEnemy:
                    var list = new List<Creature>(opp.Creatures);
                    if (list.Count > 0)
                    {
                        var t = list[Random.Range(0, list.Count)];
                        ui.Banner($"{card.name} beißt {t.card.name}!", 0.9f);
                        yield return new WaitForSeconds(0.3f);
                        Damage(t, card.effectValue, new Color(0.6f, 2.5f, 0.4f));
                        yield return new WaitForSeconds(0.6f);
                    }
                    break;

                case OnPlayEffect.DrawCard:
                    for (int i = 0; i < Mathf.Max(1, card.effectValue); i++) Draw(side);
                    if (side.isPlayer) ui.Banner("Du ziehst eine Karte", 0.8f);
                    ui.RefreshAll();
                    yield return new WaitForSeconds(0.4f);
                    break;
            }
        }

        void Damage(Creature target, int amount, Color fxColor)
        {
            if (target == null || amount <= 0) return;
            target.health -= amount;
            Vector3 p = target.transform.position + Vector3.up * target.Height * 0.6f;
            Fx.Burst(p, fxColor, 26, 4f, 0.2f, 0.45f, 1.2f);
            ui.FloatText(p + Vector3.up * 0.8f, "-" + amount, new Color(1f, 0.35f, 0.3f));
            StartCoroutine(target.HitFlash());
        }

        IEnumerator Attack(Side side, Creature attacker, Creature target)
        {
            if (attacker == null || attacker.IsDead || attacker.hasAttacked) yield break;
            busy = true;
            selected = null;
            attacker.hasAttacked = true;
            ui.RefreshInfo();

            var opp = Opp(side);
            Vector3 targetPos = target != null ? target.transform.position : ArenaBuilder.FacePos(opp.isPlayer);
            yield return attacker.LungeOut(targetPos);

            if (target != null)
            {
                Damage(target, attacker.attack, new Color(2.5f, 1.6f, 0.4f));
                int counter = attacker.Keyword == Keyword.FirstStrike ? 0 : target.attack;
                if (target.Keyword == Keyword.Thorns) counter += 1;
                if (counter > 0) Damage(attacker, counter, new Color(2.5f, 0.6f, 0.3f));
            }
            else
            {
                opp.hp -= attacker.attack;
                Vector3 face = ArenaBuilder.FacePos(opp.isPlayer) + Vector3.up;
                Fx.Burst(face, new Color(2.8f, 0.8f, 0.3f), 50, 5f, 0.28f, 0.6f, 1f);
                ui.FloatText(face + Vector3.up, "-" + attacker.attack, new Color(1f, 0.3f, 0.25f), 96f);
                shakeUntil = Time.time + 0.3f;
            }
            ui.RefreshInfo();

            yield return attacker.LungeBack();
            yield return RemoveDead();
            yield return new WaitForSeconds(0.15f);

            busy = false;
            ui.RefreshAll();
            CheckGameOver();
        }

        IEnumerator RemoveDead()
        {
            bool any = false;
            foreach (var s in new[] { Player, Enemy })
                for (int i = 0; i < s.slots.Length; i++)
                {
                    var c = s.slots[i];
                    if (c != null && c.IsDead)
                    {
                        s.slots[i] = null;
                        ui.RemoveCreature(c);
                        StartCoroutine(c.Die());
                        any = true;
                    }
                }
            if (any) yield return new WaitForSeconds(0.7f);
        }

        bool CheckGameOver()
        {
            if (CurrentPhase == Phase.GameOver) return true;
            if (Player.hp > 0 && Enemy.hp > 0) return false;

            StopAllCoroutines();
            busy = false;
            CurrentPhase = Phase.GameOver;
            ui.RefreshAll();
            bool won = Enemy.hp <= 0 && Player.hp > 0;
            string text = Player.hp <= 0 && Enemy.hp <= 0 ? "Unentschieden!" : won ? "Gewonnen!" : "Verloren!";
            ui.ShowGameOver(text, won, NewGame);
            return true;
        }

        // ------------------------------------------------------------------
        // Zug des Gegners (KI)
        // ------------------------------------------------------------------

        IEnumerator EnemyTurn()
        {
            SetPhase(Phase.EnemyTurn);
            BeginTurn(Enemy);
            ui.Banner("Gegner ist am Zug");
            yield return new WaitForSeconds(1.0f);

            Draw(Enemy);
            ui.RefreshInfo();
            yield return new WaitForSeconds(0.4f);

            // Hauptphase: teuerste bezahlbare Karten zuerst
            while (true)
            {
                CardData best = null;
                foreach (var c in Enemy.hand)
                    if (c.cost <= Enemy.energy && (best == null || c.cost > best.cost)) best = c;
                if (best == null || FirstFreeSlot(Enemy) < 0) break;

                yield return ui.ShowEnemyPlay(best);
                yield return PlayCard(Enemy, best);
                if (CurrentPhase == Phase.GameOver) yield break;
                yield return new WaitForSeconds(0.3f);
            }

            // Kampfphase
            bool announced = false;
            foreach (var attacker in new List<Creature>(Enemy.Creatures))
            {
                if (attacker == null || attacker.IsDead || attacker.hasAttacked) continue;
                if (!announced) { ui.Banner("Gegner greift an!", 0.9f); announced = true; yield return new WaitForSeconds(0.8f); }

                var target = ChooseTarget(attacker, Player, out bool face);
                if (target == null && !face) continue;
                yield return Attack(Enemy, attacker, target);
                if (CurrentPhase == Phase.GameOver) yield break;
                yield return new WaitForSeconds(0.35f);
            }

            yield return new WaitForSeconds(0.5f);
            yield return PlayerTurn();
        }

        Creature ChooseTarget(Creature attacker, Side defender, out bool face)
        {
            face = false;
            if (defender.CreatureCount == 0) { face = true; return null; }

            Creature best = null;
            float bestScore = float.MinValue;
            foreach (var t in defender.Creatures)
            {
                if (!IsValidTarget(defender, t)) continue;
                bool kills = attacker.attack >= t.health;
                int counter = (attacker.Keyword == Keyword.FirstStrike ? 0 : t.attack) + (t.Keyword == Keyword.Thorns ? 1 : 0);
                bool survives = attacker.health > counter;
                float score = (kills ? 10f + t.card.cost * 2f : 0f) + (survives ? 5f : -3f) - t.health * 0.5f + t.attack;
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // Kamera
        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (cam == null) return;
            bool portrait = Screen.height >= Screen.width;
            float bottom = BattleUI.BottomFrac(portrait);
            float top = BattleUI.TopFrac;
            cam.rect = new Rect(0f, bottom, 1f, Mathf.Max(0.1f, 1f - bottom - top));

            float aspect = cam.pixelHeight > 0 ? cam.pixelWidth / (float)cam.pixelHeight : 1f;
            float halfV = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            float needHalfWidth = 5.0f;
            float dist = Mathf.Max(11.5f, needHalfWidth / Mathf.Tan(halfH));

            Vector3 target = new Vector3(0f, 0.3f, -0.2f);
            Vector3 dir = Quaternion.Euler(50f, 0f, 0f) * Vector3.back;
            Vector3 shake = Time.time < shakeUntil ? Random.insideUnitSphere * 0.12f : Vector3.zero;
            cam.transform.position = target + dir * dist + shake;
            cam.transform.LookAt(target);
        }
    }
}
