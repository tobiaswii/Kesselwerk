using System.Collections.Generic;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>Laufende Kesselschlacht (zwischen Planungsansicht und Kampf).</summary>
    public static class TftSession
    {
        public static TftGame Current;
        public static TftGame.Pairing Fight;
        public static TeamSpec TeamA, TeamB;
        public static string Opponent = "";
        public static string Report = "";
        public static int LastReward;
        public static bool RewardGiven;

        public static TftPlayer Me => Current?.players[0];

        // ---------------- Online: echte Aufstellungen anderer Spieler als Gegner ----------------
        static readonly Dictionary<string, List<TftGhost>> ghosts = new Dictionary<string, List<TftGhost>>();
        public static int GhostCount => Current != null && ghosts.TryGetValue(Current.RoundName, out var l) ? l.Count : 0;

        /// <summary>Für die aktuelle Runde Gegner-Aufstellungen vom Server holen (still, ohne Fehlermeldung).</summary>
        public static void FetchGhosts(System.Action done = null)
        {
            if (Current == null || !Online.HasAccount) return;
            string key = Current.RoundName;
            if (ghosts.ContainsKey(key)) { done?.Invoke(); return; }
            Online.TftGhosts(Current.stage, Current.round, list => { ghosts[key] = list ?? new List<TftGhost>(); done?.Invoke(); }, _ => { });
        }

        static TftGhost TakeGhost()
        {
            if (Current == null || !ghosts.TryGetValue(Current.RoundName, out var l) || l.Count == 0) return null;
            var g = l[Random.Range(0, l.Count)];
            l.Remove(g);
            var t = Online.ToTeam(g.units);
            return t.units.Count > 0 && t.IsValid(out _) ? g : null;
        }

        // ---------------- Speichern (übersteht das Schließen der App) ----------------
        const string Key = "kesselwerk_tft";

        [System.Serializable] class PSave
        {
            public string name; public bool bot; public int hp, gold, level, xp, streak, place, lastOpponent; public bool locked;
            public string[] bench, board, shop;
        }
        [System.Serializable] class GSave
        {
            public int stage, round, seed, nextUid; public bool rewardGiven;
            public List<string> allowed = new List<string>(), poolIds = new List<string>();
            public List<int> poolCounts = new List<int>();
            public List<PSave> players = new List<PSave>();
        }

        static string Enc(TftUnit u) => u == null ? "" : $"{u.id}:{u.star}:{u.uid}";
        static TftUnit Dec(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            var p = s.Split(':');
            return new TftUnit { id = p[0], star = int.Parse(p[1]), uid = int.Parse(p[2]) };
        }

        public static void Save()
        {
            if (Current == null) { PlayerPrefs.DeleteKey(Key); return; }
            var g = Current;
            var gs = new GSave { stage = g.stage, round = g.round, seed = g.seed, nextUid = g.nextUid, rewardGiven = RewardGiven, allowed = new List<string>(g.allowed) };
            foreach (var kv in g.pool) { gs.poolIds.Add(kv.Key); gs.poolCounts.Add(kv.Value); }
            foreach (var p in g.players)
            {
                var ps = new PSave { name = p.name, bot = p.bot, hp = p.hp, gold = p.gold, level = p.level, xp = p.xp, streak = p.streak, place = p.place, lastOpponent = p.lastOpponent, locked = p.locked,
                    bench = new string[p.bench.Length], board = new string[p.board.Length], shop = new string[p.shop.Length] };
                for (int i = 0; i < p.bench.Length; i++) ps.bench[i] = Enc(p.bench[i]);
                for (int i = 0; i < p.board.Length; i++) ps.board[i] = Enc(p.board[i]);
                for (int i = 0; i < p.shop.Length; i++) ps.shop[i] = p.shop[i] ?? "";
                gs.players.Add(ps);
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(gs));
            PlayerPrefs.Save();
        }

        /// <summary>Gespeicherte Partie laden (falls vorhanden und nicht beendet).</summary>
        public static bool TryLoad()
        {
            if (Current != null) return true;
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                var gs = JsonUtility.FromJson<GSave>(json);
                var g = new TftGame { stage = gs.stage, round = gs.round, seed = gs.seed, nextUid = gs.nextUid, allowed = gs.allowed };
                for (int i = 0; i < gs.poolIds.Count; i++) g.pool[gs.poolIds[i]] = gs.poolCounts[i];
                for (int i = 0; i < gs.players.Count && i < g.players.Length; i++)
                {
                    var ps = gs.players[i];
                    var p = new TftPlayer { name = ps.name, bot = ps.bot, hp = ps.hp, gold = ps.gold, level = ps.level, xp = ps.xp, streak = ps.streak, place = ps.place, lastOpponent = ps.lastOpponent, locked = ps.locked };
                    for (int k = 0; k < p.bench.Length && k < ps.bench.Length; k++) p.bench[k] = Dec(ps.bench[k]);
                    for (int k = 0; k < p.board.Length && k < ps.board.Length; k++) p.board[k] = Dec(ps.board[k]);
                    for (int k = 0; k < p.shop.Length && k < ps.shop.Length; k++) p.shop[k] = string.IsNullOrEmpty(ps.shop[k]) ? null : ps.shop[k];
                    g.players[i] = p;
                }
                Current = g; RewardGiven = gs.rewardGiven; Fight = null; Report = "Partie fortgesetzt";
                if (g.Over) { Current = null; PlayerPrefs.DeleteKey(Key); return false; }
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("Kesselschlacht: Spielstand unlesbar – " + e.Message); PlayerPrefs.DeleteKey(Key); return false; }
        }
        public static bool Running => (Current != null || TryLoad()) && !Current.Over;
        public static int Region => Current == null ? 0 : (Current.stage - 1) % Campaign.RegionNames.Length;

        /// <summary>Gewählte Attacken aus der Sammlung (falls die Kreatur dort vorhanden ist).</summary>
        public static int[] Moves(string id)
        {
            var oc = Game.Data.Get(id);
            return oc == null ? null : new[] { oc.move1, oc.move2 };
        }

        /// <summary>Welche Monster im Laden auftauchen: alle mit Kosten 1–2, dazu alles aus der Sammlung.</summary>
        public static List<string> Unlocked()
        {
            var list = new List<string>();
            foreach (var d in CreatureDb.All)
            {
                if (d.isBoss) continue;
                if (Tft.Cost(d) <= 2 || Game.Data.Get(d.id) != null) list.Add(d.id);
            }
            return list;
        }

        public static int UnlockedCount => Unlocked().Count;

        public static void NewGame()
        {
            string name = string.IsNullOrEmpty(Game.Data.playerName) ? "Du" : Game.Data.playerName;
            Current = TftGame.New(name, Unlocked(), Random.Range(1, int.MaxValue));
            Fight = null; Report = ""; RewardGiven = false; LastReward = 0;
            Save();
            // Für Online-Gegner und Bestenliste ein Konto anlegen (nur Name, keine Anmeldung nötig)
            if (!Online.HasAccount) Online.Register(name, _ => FetchGhosts(), _ => { });
        }

        /// <summary>Kampfphase: alle anderen Kämpfe sofort rechnen, den eigenen vorbereiten. false = kein Kampf zum Zeigen.</summary>
        public static bool Resolve()
        {
            var g = Current;
            var before = new int[g.players.Length];
            for (int i = 0; i < before.Length; i++) before[i] = g.players[i].hp;
            hpBefore = before;

            TftGame.Pairing mine = null;
            foreach (var pr in g.MakePairings())
            {
                if (mine == null && (pr.a == 0 || (pr.b == 0 && !pr.ghost))) { mine = pr; continue; }
                var ta = g.TeamOf(pr.a, null); var tb = g.TeamOf(pr.b, null);
                g.Apply(pr, Run(ta, tb, pr.seed));
            }
            if (mine == null) { EndRound(); return false; }
            if (mine.b == 0) mine = new TftGame.Pairing { a = 0, b = mine.a, creep = mine.creep, ghost = false, seed = mine.seed };
            Fight = mine;
            TeamA = g.TeamOf(0, Moves);
            TeamB = g.TeamOf(mine.b, null);
            Opponent = mine.creep ? "Wilde Monster" : g.players[mine.b].name + (mine.ghost ? " (Spiegelbild)" : "");
            // Online: eigene Aufstellung hochladen, Computer-Gegner durch echte Aufstellung ersetzen
            if (Online.HasAccount && TeamA.units.Count > 0)
                Online.TftBoard(g.stage, g.round, TeamA, null, _ => { });
            if (!mine.creep && mine.b > 0)
            {
                var gh = TakeGhost();
                if (gh != null) { TeamB = Online.ToTeam(gh.units); Opponent = $"{gh.name}  ·  online"; }
            }
            if (TeamA.units.Count == 0 || TeamB.units.Count == 0)
            {
                FinishFight(Run(TeamA, TeamB, mine.seed));
                return false;
            }
            return true;
        }

        static int[] hpBefore;

        static BattleResult Run(TeamSpec a, TeamSpec b, int seed)
        {
            if (a.units.Count == 0) return TftGame.Loss(b);
            if (b.units.Count == 0) return new BattleResult { winner = 0, units = new List<BattleUnit>(), events = new List<BattleEvent>() };
            return BattleSim.Run(a, b, seed);
        }

        public static void FinishFight(BattleResult res)
        {
            if (Fight == null) return;
            Current.Apply(Fight, res);
            Fight = null;
            EndRound();
        }

        static void EndRound()
        {
            var g = Current;
            var me = g.players[0];
            int lost = hpBefore != null ? hpBefore[0] - me.hp : 0;
            string round = g.RoundName;
            g.EndRound();
            Report = lost > 0 ? $"Runde {round}: −{lost} Leben" : $"Runde {round}: gewonnen!";
            Save();
            if (g.Over && !RewardGiven)
            {
                RewardGiven = true;
                int place = Place;
                LastReward = place == 1 ? 300 : place == 2 ? 200 : place == 3 ? 150 : place == 4 ? 100 : 50;
                Game.Data.crystals += LastReward;
                Game.Data.gold += Mathf.Max(0, 9 - place) * 40;
                Game.Save();
                if (Online.HasAccount) Online.TftResult(place, null, _ => { });
            }
        }

        /// <summary>Endplatz des Spielers (1 = Sieg).</summary>
        public static int Place
        {
            get
            {
                var me = Me;
                if (me == null) return 0;
                if (me.Alive) return 1;
                return Mathf.Max(1, me.place);
            }
        }
    }
}
