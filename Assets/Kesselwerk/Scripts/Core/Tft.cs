using System;
using System.Collections.Generic;

namespace Kesselwerk.Core
{
    // =====================================================================
    // Kesselschlacht: Runden-Auto-Battler mit Laden, Bank, Sternen und Lobby
    // (Prinzip wie Teamfight Tactics – eigene Werte und Regeln)
    // =====================================================================

    [Serializable]
    public class TftUnit
    {
        public int uid;
        public string id;
        public int star = 1;
        public CreatureDef Def => CreatureDb.Get(id);
    }

    [Serializable]
    public class TftPlayer
    {
        public string name;
        public bool bot;
        public int hp = 100;
        public int gold;
        public int level = 1;
        public int xp;
        public int streak;           // >0 Siegesserie, <0 Niederlagenserie
        public int place;            // Endplatz (0 = noch im Spiel)
        public TftUnit[] bench = new TftUnit[Tft.BenchSize];
        public TftUnit[] board = new TftUnit[Hex.SideCells];
        public string[] shop = new string[Tft.ShopSize];
        public bool locked;
        public int lastOpponent = -1;
        public bool Alive => hp > 0;

        public int BoardCount { get { int n = 0; foreach (var u in board) if (u != null) n++; return n; } }

        public IEnumerable<TftUnit> AllUnits()
        {
            foreach (var u in bench) if (u != null) yield return u;
            foreach (var u in board) if (u != null) yield return u;
        }

        /// <summary>Aufstellung als Kampf-Team (Stern → Stufe, gewählte Attacken je Kreatur).</summary>
        public TeamSpec ToTeam(Func<string, int[]> moves = null)
        {
            var t = new TeamSpec();
            for (int i = 0; i < board.Length; i++)
            {
                var u = board[i];
                if (u == null) continue;
                var s = new UnitSpec(u.id, Tft.LevelForStar(u.star), 0, u.star) { hex = i };
                var m = moves?.Invoke(u.id);
                if (m != null && m.Length > 0)
                {
                    var d = u.Def;
                    if (MoveDb.IsUnlocked(d, m[0], s.level)) s.move1 = m[0];
                    if (m.Length > 1 && MoveDb.IsUnlocked(d, m[1], s.level)) s.move2 = m[1];
                }
                t.Add(s);
            }
            return t;
        }
    }

    public static class Tft
    {
        public const int Players = 8;
        public const int BenchSize = 9;
        public const int ShopSize = 5;
        public const int MaxLevel = 9;
        public const int RerollCost = 2;
        public const int XpCost = 4;
        public const int StartHp = 100;

        public static readonly int[] PoolSize = { 0, 24, 20, 16, 12, 9 };
        // Erfahrung bis zur nächsten Stufe (Index = aktuelle Stufe)
        public static readonly int[] XpToNext = { 0, 2, 2, 6, 10, 20, 36, 48, 72, 0 };
        // Ladenchancen in Prozent je Stufe für Kosten 1–5
        public static readonly int[,] Odds =
        {
            { 0, 0, 0, 0, 0 },
            { 100, 0, 0, 0, 0 },   // 1
            { 100, 0, 0, 0, 0 },   // 2
            { 75, 25, 0, 0, 0 },   // 3
            { 55, 30, 15, 0, 0 },  // 4
            { 45, 33, 20, 2, 0 },  // 5
            { 30, 40, 25, 5, 0 },  // 6
            { 19, 30, 35, 15, 1 }, // 7
            { 15, 20, 35, 25, 5 }, // 8
            { 10, 15, 30, 30, 15 },// 9
        };

        public static int Cost(CreatureDef d) => d == null ? 1 : d.rarity switch
        {
            Rarity.Gewoehnlich => 1,
            Rarity.Ungewoehnlich => 2,
            Rarity.Selten => 3,
            Rarity.Episch => 4,
            _ => 5,
        };

        public static int LevelForStar(int star) => star >= 3 ? 30 : star == 2 ? 13 : 1;

        /// <summary>Verkaufswert: Kosten × Anzahl verschmolzener Exemplare (3★ etwas weniger).</summary>
        public static int SellValue(TftUnit u)
        {
            int c = Cost(u.Def);
            int copies = u.star == 1 ? 1 : u.star == 2 ? 3 : 9;
            return u.star == 1 ? c : c * copies - 1;
        }

        public static int Interest(int gold) => Math.Min(5, gold / 10);
        public static int StreakBonus(int streak) { int s = Math.Abs(streak); return s >= 5 ? 3 : s == 4 ? 2 : s >= 2 ? 1 : 0; }
        public static int StageDamage(int stage) => stage <= 1 ? 0 : stage == 2 ? 2 : stage == 3 ? 5 : stage == 4 ? 8 : stage == 5 ? 10 : 12 + (stage - 6) * 3;

        /// <summary>Gegnerische Monster für Computer-Runden (Runde 1-1 bis 1-3 und jede 7. Runde).</summary>
        public static bool IsCreepRound(int stage, int round) => stage == 1 || round == 7;
        public const int RoundsPerStage = 7;
    }

    /// <summary>Eine Partie mit 8 Spielern. Spieler 0 ist der Mensch, 1–7 sind Computer-Gegner.</summary>
    [Serializable]
    public class TftGame
    {
        public TftPlayer[] players = new TftPlayer[Tft.Players];
        public Dictionary<string, int> pool = new Dictionary<string, int>();
        public List<string> allowed = new List<string>();
        public int stage = 1, round = 1;
        public int seed;
        public int nextUid = 1;
        public int finishedCount;
        [NonSerialized] Rng rng;

        static readonly string[] BotNames = { "Glutbaron", "Moosmutter", "Frostfuchs", "Kesselkönig", "Donnerdora", "Nebelnico", "Sternsepp", "Krabbenkurt", "Wurzelwilma" };

        Rng R => rng ??= new Rng(seed * 7919 + stage * 131 + round);

        public static TftGame New(string playerName, IEnumerable<string> unlocked, int seed)
        {
            var g = new TftGame { seed = seed };
            g.rng = new Rng(seed);
            foreach (var id in unlocked)
            {
                var d = CreatureDb.Get(id);
                if (d == null || d.isBoss || g.allowed.Contains(id)) continue;
                g.allowed.Add(id);
                g.pool[id] = Tft.PoolSize[Tft.Cost(d)];
            }
            var names = new List<string>(BotNames);
            for (int i = 0; i < Tft.Players; i++)
            {
                string n = i == 0 ? playerName : names[g.rng.Range(0, names.Count)];
                names.Remove(n);
                g.players[i] = new TftPlayer { name = n, bot = i > 0, hp = Tft.StartHp };
            }
            foreach (var p in g.players) { p.gold = 0; g.Refresh(p); }
            g.StartPlanning();
            return g;
        }

        public string RoundName => $"{stage}-{round}";
        public int AliveCount { get { int n = 0; foreach (var p in players) if (p.Alive) n++; return n; } }
        public bool Over => !players[0].Alive || AliveCount <= 1;

        // -----------------------------------------------------------------
        // Planungsphase
        // -----------------------------------------------------------------

        /// <summary>Beginn einer Runde: Einkommen, Erfahrung, neuer Laden; Computer-Gegner spielen ihren Zug.</summary>
        public void StartPlanning()
        {
            rng = null;
            foreach (var p in players)
            {
                if (!p.Alive) continue;
                if (!(stage == 1 && round == 1))
                {
                    int income = 5 + Tft.Interest(p.gold) + Tft.StreakBonus(p.streak);
                    if (stage == 1) income = round + 1;
                    p.gold += income;
                    AddXp(p, 2);
                }
                else { p.gold = 3; p.level = 1; }
                if (!p.locked) Refresh(p);
                p.locked = false;
            }
            for (int i = 1; i < players.Length; i++) if (players[i].Alive) BotTurn(players[i]);
        }

        int[] OddsFor(int level)
        {
            int l = Math.Max(1, Math.Min(Tft.MaxLevel, level));
            return new[] { Tft.Odds[l, 0], Tft.Odds[l, 1], Tft.Odds[l, 2], Tft.Odds[l, 3], Tft.Odds[l, 4] };
        }

        string Draw(int level)
        {
            var odds = OddsFor(level);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                int roll = R.Range(0, 100), cost = 1, acc = 0;
                for (int c = 0; c < 5; c++) { acc += odds[c]; if (roll < acc) { cost = c + 1; break; } }
                int total = 0;
                foreach (var id in allowed) if (Tft.Cost(CreatureDb.Get(id)) == cost) total += pool[id];
                if (total <= 0) continue;
                int pick = R.Range(0, total);
                foreach (var id in allowed)
                {
                    if (Tft.Cost(CreatureDb.Get(id)) != cost) continue;
                    pick -= pool[id];
                    if (pick < 0) return id;
                }
            }
            return null;
        }

        /// <summary>Neue Auswahl im Laden (nicht gekaufte gehen zurück in den Vorrat).</summary>
        public void Refresh(TftPlayer p)
        {
            for (int i = 0; i < Tft.ShopSize; i++)
            {
                if (p.shop[i] != null) pool[p.shop[i]]++;
                p.shop[i] = null;
            }
            for (int i = 0; i < Tft.ShopSize; i++)
            {
                var id = Draw(p.level);
                if (id != null) { pool[id]--; p.shop[i] = id; }
            }
        }

        public bool Reroll(TftPlayer p, out string error)
        {
            error = null;
            if (p.gold < Tft.RerollCost) { error = "Nicht genug Gold."; return false; }
            p.gold -= Tft.RerollCost;
            Refresh(p);
            return true;
        }

        public bool BuyXp(TftPlayer p, out string error)
        {
            error = null;
            if (p.level >= Tft.MaxLevel) { error = "Höchste Stufe erreicht."; return false; }
            if (p.gold < Tft.XpCost) { error = "Nicht genug Gold."; return false; }
            p.gold -= Tft.XpCost;
            AddXp(p, 4);
            return true;
        }

        static void AddXp(TftPlayer p, int amount)
        {
            if (p.level >= Tft.MaxLevel) return;
            p.xp += amount;
            while (p.level < Tft.MaxLevel && p.xp >= Tft.XpToNext[p.level])
            {
                p.xp -= Tft.XpToNext[p.level];
                p.level++;
            }
            if (p.level >= Tft.MaxLevel) p.xp = 0;
        }

        int FreeBench(TftPlayer p) { for (int i = 0; i < p.bench.Length; i++) if (p.bench[i] == null) return i; return -1; }

        /// <summary>Kaufen: auf die Bank (oder direkt verschmelzen, wenn die Bank voll ist und es zum Stern reicht).</summary>
        public bool Buy(TftPlayer p, int shopIndex, out string error)
        {
            error = null;
            var id = p.shop[shopIndex];
            if (id == null) { error = "Leer."; return false; }
            int cost = Tft.Cost(CreatureDb.Get(id));
            if (p.gold < cost) { error = "Nicht genug Gold."; return false; }
            int same = 0;
            foreach (var u in p.AllUnits()) if (u.id == id && u.star == 1) same++;
            int slot = FreeBench(p);
            if (slot < 0 && same < 2) { error = "Die Bank ist voll."; return false; }
            p.gold -= cost;
            p.shop[shopIndex] = null;
            var nu = new TftUnit { uid = nextUid++, id = id, star = 1 };
            if (slot >= 0) { p.bench[slot] = nu; Combine(p, id, 1, null); }
            else Combine(p, id, 1, nu); // Bank voll: wird sofort verschmolzen
            return true;
        }

        /// <summary>Drei gleiche Exemplare mit gleichem Stern verschmelzen (auch mehrstufig, Feld bevorzugt).</summary>
        public void Combine(TftPlayer p, string id, int star, TftUnit extra)
        {
            while (star < 3)
            {
                var list = new List<(bool board, int idx)>();
                for (int i = 0; i < p.board.Length; i++) if (p.board[i] != null && p.board[i].id == id && p.board[i].star == star) list.Add((true, i));
                for (int i = 0; i < p.bench.Length; i++) if (p.bench[i] != null && p.bench[i].id == id && p.bench[i].star == star) list.Add((false, i));
                int need = extra != null ? 2 : 3;
                if (list.Count < need)
                {
                    if (extra != null) { int f = FreeBench(p); if (f >= 0) p.bench[f] = extra; }
                    return;
                }
                var keep = list[0];
                for (int k = 0; k < need; k++)
                {
                    if (list[k].board) p.board[list[k].idx] = null; else p.bench[list[k].idx] = null;
                }
                var up = new TftUnit { uid = nextUid++, id = id, star = star + 1 };
                if (keep.board) p.board[keep.idx] = up; else p.bench[keep.idx] = up;
                extra = null;
                star++;
            }
        }

        public void Sell(TftPlayer p, TftUnit u)
        {
            for (int i = 0; i < p.bench.Length; i++) if (p.bench[i] == u) p.bench[i] = null;
            for (int i = 0; i < p.board.Length; i++) if (p.board[i] == u) p.board[i] = null;
            p.gold += Tft.SellValue(u);
            int copies = u.star == 1 ? 1 : u.star == 2 ? 3 : 9;
            if (pool.ContainsKey(u.id)) pool[u.id] += copies;
        }

        /// <summary>Kreatur verschieben: Feld 0–27 oder Bank (bench = true). Belegte Ziele werden getauscht.</summary>
        public bool Move(TftPlayer p, TftUnit u, bool toBench, int index, out string error)
        {
            error = null;
            bool fromBench = Array.IndexOf(p.bench, u) >= 0;
            int from = fromBench ? Array.IndexOf(p.bench, u) : Array.IndexOf(p.board, u);
            if (from < 0) { error = "Unbekannt."; return false; }
            var dest = toBench ? p.bench : p.board;
            if (index < 0 || index >= dest.Length) { error = "Ungültiges Feld."; return false; }
            var other = dest[index];
            if (!toBench && fromBench && other == null && p.BoardCount >= p.level) { error = $"Stufe {p.level}: höchstens {p.level} Monster auf dem Feld."; return false; }
            var src = fromBench ? p.bench : p.board;
            src[from] = other;
            dest[index] = u;
            return true;
        }

        // -----------------------------------------------------------------
        // Kampfphase
        // -----------------------------------------------------------------

        public class Pairing { public int a, b; public bool creep; public bool ghost; public int seed; }

        /// <summary>Gegner für diese Runde: Computer-Monster oder zufällige Paare (bei ungerader Zahl ein Spiegelgegner).</summary>
        public List<Pairing> MakePairings()
        {
            var res = new List<Pairing>();
            var alive = new List<int>();
            for (int i = 0; i < players.Length; i++) if (players[i].Alive) alive.Add(i);
            if (Tft.IsCreepRound(stage, round))
            {
                foreach (int i in alive) res.Add(new Pairing { a = i, b = -1, creep = true, seed = R.Range(1, int.MaxValue) });
                return res;
            }
            // Mischen, wiederholte Gegner vermeiden
            for (int i = alive.Count - 1; i > 0; i--) { int j = R.Range(0, i + 1); (alive[i], alive[j]) = (alive[j], alive[i]); }
            for (int i = 0; i + 1 < alive.Count; i += 2)
            {
                if (players[alive[i]].lastOpponent == alive[i + 1] && i + 2 < alive.Count) (alive[i + 1], alive[i + 2]) = (alive[i + 2], alive[i + 1]);
                res.Add(new Pairing { a = alive[i], b = alive[i + 1], seed = R.Range(1, int.MaxValue) });
            }
            if (alive.Count % 2 == 1)
            {
                int last = alive[alive.Count - 1];
                int ghost = alive[R.Range(0, alive.Count - 1)];
                res.Add(new Pairing { a = last, b = ghost, ghost = true, seed = R.Range(1, int.MaxValue) });
            }
            return res;
        }

        /// <summary>Computer-Monster je Runde (stärker mit der Stufe).</summary>
        public TeamSpec CreepTeam()
        {
            var t = new TeamSpec();
            int n = stage == 1 ? round : 2 + stage;
            int star = stage >= 5 ? 2 : 1;
            var ids = new List<string>();
            foreach (var id in allowed) if (Tft.Cost(CreatureDb.Get(id)) <= Math.Min(5, stage)) ids.Add(id);
            var r = new Rng(seed + stage * 1000 + round);
            int[] cells = { 3, 2, 4, 1, 5, 10, 9, 11, 16, 17 };
            for (int i = 0; i < n && i < cells.Length && ids.Count > 0; i++)
                t.Add(new UnitSpec(ids[r.Range(0, ids.Count)], Tft.LevelForStar(star) + (stage >= 3 ? 4 : 0), 0, Math.Max(star, 1)) { hex = cells[i] });
            return t;
        }

        public TeamSpec TeamOf(int i, Func<string, int[]> moves) => i < 0 ? CreepTeam() : players[i].ToTeam(i == 0 ? moves : null);

        /// <summary>Kampfergebnis anwenden: Schaden = Rundenschaden + überlebende Monster (nach Sternen).</summary>
        public void Apply(Pairing pr, BattleResult res)
        {
            var pa = players[pr.a];
            bool aWon = res.winner == 0;
            int survivors = 0;
            foreach (var u in res.units) if (u.Alive) survivors += u.spec.stars;
            int dmg = Tft.StageDamage(stage) + survivors;
            if (pr.creep)
            {
                if (aWon) { pa.gold += stage == 1 ? 1 : 2 + stage / 2; }
                else pa.hp -= Math.Max(1, dmg / 2);
                return;
            }
            var pb = players[pr.b];
            pa.lastOpponent = pr.b;
            if (aWon) { Win(pa); if (!pr.ghost) { Lose(pb); pb.hp -= dmg; } }
            else { Lose(pa); pa.hp -= dmg; if (!pr.ghost) Win(pb); }
            if (!pr.ghost) pb.lastOpponent = pr.a;
        }

        static void Win(TftPlayer p) { p.streak = p.streak > 0 ? p.streak + 1 : 1; p.gold += 1; }
        static void Lose(TftPlayer p) { p.streak = p.streak < 0 ? p.streak - 1 : -1; }

        /// <summary>Nach allen Kämpfen: Ausgeschiedene einordnen, nächste Runde.</summary>
        public void EndRound()
        {
            int alive = AliveCount;
            foreach (var p in players)
            {
                if (p.hp <= 0 && p.place == 0)
                {
                    p.hp = 0;
                    p.place = alive + 0; // Platzierung = Zahl der noch Lebenden + 1 (siehe unten)
                    foreach (var u in p.AllUnits()) if (pool.ContainsKey(u.id)) pool[u.id] += u.star == 1 ? 1 : u.star == 2 ? 3 : 9;
                }
            }
            // gleichzeitig Ausgeschiedene bekommen denselben Platz; korrekt: Platz = Anzahl Lebender + 1
            int living = AliveCount;
            foreach (var p in players) if (p.hp <= 0 && p.place == alive) p.place = living + 1;
            if (living == 1) foreach (var p in players) if (p.Alive) p.place = 1;
            round++;
            if (round > Tft.RoundsPerStage || (stage == 1 && round > 3)) { stage++; round = 1; }
            if (!Over) StartPlanning();
        }

        /// <summary>Alle Kämpfe der Runde ohne Anzeige berechnen (für Computer-Paare und Tests).</summary>
        public List<(Pairing pr, BattleResult res)> SimulateAll(Func<string, int[]> moves, bool skipHuman)
        {
            var list = new List<(Pairing, BattleResult)>();
            foreach (var pr in MakePairings())
            {
                if (skipHuman && (pr.a == 0 || pr.b == 0)) { list.Add((pr, null)); continue; }
                var ta = TeamOf(pr.a, moves); var tb = TeamOf(pr.b, moves);
                BattleResult r = ta.units.Count == 0 ? Loss(tb) : tb.units.Count == 0 ? Win0(ta) : BattleSim.Run(ta, tb, pr.seed);
                list.Add((pr, r));
            }
            return list;
        }

        public static BattleResult Loss(TeamSpec winner)
        {
            var r = new BattleResult { winner = 1, units = new List<BattleUnit>(), events = new List<BattleEvent>() };
            foreach (var s in winner.units) r.units.Add(new BattleUnit { team = 1, spec = s, def = CreatureDb.Get(s.id), hp = 1, maxHp = 1 });
            return r;
        }

        static BattleResult Win0(TeamSpec t) => new BattleResult { winner = 0, units = new List<BattleUnit>(), events = new List<BattleEvent>() };

        // -----------------------------------------------------------------
        // Computer-Gegner
        // -----------------------------------------------------------------

        public void AutoTurn(TftPlayer p) => BotTurn(p);

        void BotTurn(TftPlayer p)
        {
            // 1. Paare und Drillinge kaufen, sonst Monster passender Elemente
            var have = new Dictionary<string, int>();
            var elems = new Dictionary<Element, int>();
            foreach (var u in p.AllUnits()) { have[u.id] = (have.TryGetValue(u.id, out var n) ? n : 0) + 1; elems[u.Def.element] = (elems.TryGetValue(u.Def.element, out var e) ? e : 0) + 1; }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < Tft.ShopSize; i++)
                {
                    var id = p.shop[i];
                    if (id == null) continue;
                    var d = CreatureDb.Get(id);
                    int cost = Tft.Cost(d);
                    bool want = have.ContainsKey(id) || (elems.TryGetValue(d.element, out var ec) && ec > 0) || p.BoardCount + Bench(p) < p.level + 2;
                    if (!want || p.gold - cost < (stage >= 3 ? 10 : 0) && !have.ContainsKey(id)) continue;
                    if (Buy(p, i, out _)) { have[id] = (have.TryGetValue(id, out var n2) ? n2 : 0) + 1; elems[d.element] = (elems.TryGetValue(d.element, out var e2) ? e2 : 0) + 1; }
                }
                if (pass == 0 && p.gold >= 14 && stage >= 3) Reroll(p, out _);
            }
            // 2. Stufe kaufen (ab Runde 2 mit Polster)
            while (p.gold >= Tft.XpCost + 10 && p.level < Tft.MaxLevel && stage >= 2) BuyXp(p, out _);
            // 3. Bestes Team aufs Feld: nach Stern und Kosten
            var all = new List<TftUnit>(p.AllUnits());
            all.Sort((x, y) => (y.star * 10 + Tft.Cost(y.Def)).CompareTo(x.star * 10 + Tft.Cost(x.Def)));
            Array.Clear(p.board, 0, p.board.Length);
            Array.Clear(p.bench, 0, p.bench.Length);
            int front = 0, back = 0;
            int[] frontCells = { 3, 2, 4, 1, 5, 0, 6, 10, 9, 11 };
            int[] backCells = { 24, 23, 25, 22, 26, 17, 16, 18, 21, 27 };
            int placed = 0, b = 0;
            foreach (var u in all)
            {
                if (placed < p.level)
                {
                    bool tanky = !u.Def.ranged;
                    int cell = tanky && front < frontCells.Length ? frontCells[front++] : backCells[back++];
                    p.board[cell] = u; placed++;
                }
                else if (b < p.bench.Length) p.bench[b++] = u;
                else Sell(p, u);
            }
        }

        static int Bench(TftPlayer p) { int n = 0; foreach (var u in p.bench) if (u != null) n++; return n; }
    }
}
