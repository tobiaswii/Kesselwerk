using System;
using System.Collections.Generic;

namespace Kesselwerk.Core
{
    // =====================================================================
    // Spielstand
    // =====================================================================

    [Serializable]
    public class OwnedCreature
    {
        public string id;
        public int level = 1;
        public int xp;
        public int stars = 1;
        public int stage;
        public int copies;   // überzählige Exemplare für die Fusion
        public int weaponUid, armorUid, amuletUid; // angelegte Ausrüstung (0 = nichts)

        public CreatureDef Def => CreatureDb.Get(id);
        public UnitSpec ToSpec(int slot) => new UnitSpec(id, level, slot, stars, stage);
        public string Name => Def != null ? Def.StageName(stage) : id;
    }

    [Serializable]
    public class OwnedGear
    {
        public int uid;
        public string id;
        public int level = 1;
        public GearDef Def => GearDb.Get(id);
    }

    [Serializable]
    public class PlayerData
    {
        public int version = 2;
        public string playerName = "Kesselmeister";
        public int gold;
        public int crystals;     // Beschwörungskristalle
        public int potions;      // Erfahrungstränke (je 100 EP)
        public int evoStones;    // Entwicklungssteine
        public List<OwnedCreature> creatures = new List<OwnedCreature>();
        public List<OwnedGear> gear = new List<OwnedGear>();
        public int nextGearUid = 1;
        public string[] team = new string[TeamSpec.Slots];
        public int[] regionProgress = new int[Campaign.RegionCount]; // Anzahl geschaffter Stufen je Region
        public int battles, wins, summons;
        public int pityRare, pityLegend;
        public int lastLoginDay = -1, loginStreak;
        public int taskDay = -1, taskWins, taskSummons, taskLevelUps;
        public bool[] taskClaimed = new bool[3];
        public float musicVolume = 0.6f, sfxVolume = 0.8f;
        public int battleSpeed = 1;

        public OwnedCreature Get(string id)
        {
            foreach (var c in creatures) if (c.id == id) return c;
            return null;
        }

        public bool Owns(string id) => Get(id) != null;

        public TeamSpec BuildTeam()
        {
            var t = new TeamSpec();
            for (int i = 0; i < team.Length; i++)
            {
                var c = Get(team[i]);
                if (c != null) t.Add(SpecFor(c, i));
            }
            return t;
        }

        public OwnedGear GetGear(int uid)
        {
            if (uid <= 0) return null;
            foreach (var g in gear) if (g.uid == uid) return g;
            return null;
        }

        /// <summary>Werte einer Kreatur inklusive ihrer angelegten Ausrüstung.</summary>
        public UnitSpec SpecFor(OwnedCreature c, int slot)
        {
            var s = c.ToSpec(slot);
            var w = GetGear(c.weaponUid); if (w != null) { s.weapon = w.id; s.weaponLv = w.level; }
            var a = GetGear(c.armorUid); if (a != null) { s.armor = a.id; s.armorLv = a.level; }
            var m = GetGear(c.amuletUid); if (m != null) { s.amulet = m.id; s.amuletLv = m.level; }
            return s;
        }

        /// <summary>Wer trägt diesen Gegenstand gerade? (null = niemand)</summary>
        public OwnedCreature WearerOf(int gearUid)
        {
            foreach (var c in creatures)
                if (c.weaponUid == gearUid || c.armorUid == gearUid || c.amuletUid == gearUid) return c;
            return null;
        }

        public OwnedGear AddGear(string id)
        {
            var g = new OwnedGear { uid = nextGearUid++, id = id };
            gear.Add(g);
            return g;
        }

        public int TeamCount()
        {
            int n = 0;
            foreach (var id in team) if (Get(id) != null) n++;
            return n;
        }

        /// <summary>Setzt eine Kreatur auf ein Feld (oder entfernt sie mit null). Eine Kreatur steht nur einmal im Team.</summary>
        public void SetTeamSlot(int slot, string id)
        {
            if (slot < 0 || slot >= team.Length) return;
            if (id != null)
                for (int i = 0; i < team.Length; i++) if (team[i] == id) team[i] = team[slot];
            team[slot] = id;
        }

        public void Normalize()
        {
            if (team == null || team.Length != TeamSpec.Slots) team = new string[TeamSpec.Slots];
            if (regionProgress == null || regionProgress.Length != Campaign.RegionCount)
            {
                var r = new int[Campaign.RegionCount];
                if (regionProgress != null) Array.Copy(regionProgress, r, Math.Min(r.Length, regionProgress.Length));
                regionProgress = r;
            }
            if (taskClaimed == null || taskClaimed.Length != 3) taskClaimed = new bool[3];
            creatures.RemoveAll(c => c == null || CreatureDb.Get(c.id) == null);
            if (gear == null) gear = new List<OwnedGear>();
            gear.RemoveAll(g => g == null || GearDb.Get(g.id) == null);
            foreach (var g in gear) if (g.uid >= nextGearUid) nextGearUid = g.uid + 1;
            foreach (var c in creatures)
            {
                if (GetGear(c.weaponUid) == null) c.weaponUid = 0;
                if (GetGear(c.armorUid) == null) c.armorUid = 0;
                if (GetGear(c.amuletUid) == null) c.amuletUid = 0;
            }
            for (int i = 0; i < team.Length; i++) if (team[i] != null && Get(team[i]) == null) team[i] = null;
        }

        public static PlayerData NewGame()
        {
            var p = new PlayerData { gold = 1500, crystals = 1000, potions = 5, evoStones = 2 };
            string[] starters = { "krebs", "moos", "biene", "knochen", "hirsch" };
            foreach (var s in starters) p.creatures.Add(new OwnedCreature { id = s });
            p.team[0] = "krebs"; p.team[1] = "moos"; p.team[3] = "biene"; p.team[4] = "knochen"; p.team[5] = "hirsch";
            p.AddGear("w_knueppel");
            p.AddGear("r_leder");
            return p;
        }
    }

    // =====================================================================
    // Fortschritt: Stufen, Fusion, Entwicklung
    // =====================================================================

    public static class Progress
    {
        public const int PotionXp = 100;

        public static int XpToNext(int level) => 80 + level * 40;

        /// <summary>Gibt EP; steigt automatisch auf, solange die Sterne es zulassen. Liefert die Zahl der Aufstiege.</summary>
        public static int AddXp(OwnedCreature c, int xp)
        {
            int ups = 0;
            int cap = Stats.MaxLevel(c.stars);
            if (c.level >= cap) { c.xp = 0; return 0; }
            c.xp += xp;
            while (c.level < cap && c.xp >= XpToNext(c.level))
            {
                c.xp -= XpToNext(c.level);
                c.level++;
                ups++;
            }
            if (c.level >= cap) c.xp = 0;
            return ups;
        }

        public static bool UsePotion(PlayerData p, OwnedCreature c, out string error)
        {
            error = null;
            if (p.potions <= 0) { error = "Keine Erfahrungstränke mehr."; return false; }
            if (c.level >= Stats.MaxLevel(c.stars)) { error = "Höchststufe erreicht – erst fusionieren."; return false; }
            p.potions--;
            int ups = AddXp(c, PotionXp);
            if (ups > 0) TaskLevelUp(p);
            return true;
        }

        /// <summary>Kopien, die für den nächsten Stern nötig sind.</summary>
        public static int CopiesForStar(int stars) => stars + 1;
        public static int GoldForStar(int stars) => 300 * stars * stars;

        public static bool CanFuse(PlayerData p, OwnedCreature c) =>
            c.stars < Stats.MaxStars && c.copies >= CopiesForStar(c.stars) && p.gold >= GoldForStar(c.stars);

        public static bool Fuse(PlayerData p, OwnedCreature c, out string error)
        {
            error = null;
            if (c.stars >= Stats.MaxStars) { error = "Schon die höchste Sternstufe."; return false; }
            if (c.copies < CopiesForStar(c.stars)) { error = $"Benötigt {CopiesForStar(c.stars)} Kopien (du hast {c.copies})."; return false; }
            if (p.gold < GoldForStar(c.stars)) { error = $"Benötigt {GoldForStar(c.stars)} Gold."; return false; }
            p.gold -= GoldForStar(c.stars);
            c.copies -= CopiesForStar(c.stars);
            c.stars++;
            return true;
        }

        public const int EvolveLevel = 15;
        public static int StonesForEvolve(CreatureDef d) => 2 + (int)d.rarity;
        public static int GoldForEvolve(CreatureDef d) => 800 + 400 * (int)d.rarity;

        public static bool Evolve(PlayerData p, OwnedCreature c, out string error)
        {
            error = null;
            var d = c.Def;
            if (c.stage >= d.MaxStage) { error = "Bereits voll entwickelt."; return false; }
            if (c.level < EvolveLevel) { error = $"Benötigt Stufe {EvolveLevel}."; return false; }
            if (p.evoStones < StonesForEvolve(d)) { error = $"Benötigt {StonesForEvolve(d)} Entwicklungssteine."; return false; }
            if (p.gold < GoldForEvolve(d)) { error = $"Benötigt {GoldForEvolve(d)} Gold."; return false; }
            p.evoStones -= StonesForEvolve(d);
            p.gold -= GoldForEvolve(d);
            c.stage++;
            return true;
        }

        // -------- Ausrüstung --------

        /// <summary>Legt einen Gegenstand an (nimmt ihn vorher anderen Kreaturen ab).</summary>
        public static bool Equip(PlayerData p, OwnedCreature c, int gearUid, out string error)
        {
            error = null;
            var g = p.GetGear(gearUid);
            if (g == null || g.Def == null) { error = "Gegenstand nicht gefunden."; return false; }
            var other = p.WearerOf(gearUid);
            if (other != null) Unequip(other, g.Def.slot);
            switch (g.Def.slot)
            {
                case GearSlot.Waffe: c.weaponUid = gearUid; break;
                case GearSlot.Ruestung: c.armorUid = gearUid; break;
                default: c.amuletUid = gearUid; break;
            }
            return true;
        }

        public static void Unequip(OwnedCreature c, GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Waffe: c.weaponUid = 0; break;
                case GearSlot.Ruestung: c.armorUid = 0; break;
                default: c.amuletUid = 0; break;
            }
        }

        public static int GearUpgradeCost(OwnedGear g) => 250 * g.level * ((int)g.Def.rarity + 1);

        public static bool UpgradeGear(PlayerData p, OwnedGear g, out string error)
        {
            error = null;
            if (g.level >= GearDb.MaxLevel) { error = "Höchste Stufe erreicht."; return false; }
            int cost = GearUpgradeCost(g);
            if (p.gold < cost) { error = $"Benötigt {cost} Gold."; return false; }
            p.gold -= cost;
            g.level++;
            return true;
        }

        // -------- Tägliche Belohnungen und Aufgaben --------

        public static readonly string[] TaskNames = { "Gewinne 3 Kämpfe", "Beschwöre 1 Kreatur", "Steigere 1 Kreatur" };
        public static readonly int[] TaskGoal = { 3, 1, 1 };
        public static readonly int[] TaskCrystals = { 150, 80, 80 };

        public static void RollDay(PlayerData p, int day)
        {
            if (p.taskDay == day) return;
            p.taskDay = day;
            p.taskWins = p.taskSummons = p.taskLevelUps = 0;
            p.taskClaimed = new bool[3];
        }

        public static int TaskProgress(PlayerData p, int i) => i == 0 ? p.taskWins : i == 1 ? p.taskSummons : p.taskLevelUps;

        public static bool ClaimTask(PlayerData p, int i)
        {
            if (i < 0 || i > 2 || p.taskClaimed[i] || TaskProgress(p, i) < TaskGoal[i]) return false;
            p.taskClaimed[i] = true;
            p.crystals += TaskCrystals[i];
            return true;
        }

        static void TaskLevelUp(PlayerData p) => p.taskLevelUps++;

        /// <summary>Login-Belohnung, einmal pro Tag. Liefert die Kristalle (0 = heute schon abgeholt).</summary>
        public static int DailyLogin(PlayerData p, int day)
        {
            RollDay(p, day);
            if (p.lastLoginDay == day) return 0;
            p.loginStreak = p.lastLoginDay == day - 1 ? Math.Min(p.loginStreak + 1, 7) : 1;
            p.lastLoginDay = day;
            int crystals = 50 + 25 * p.loginStreak;
            p.crystals += crystals;
            p.gold += 200;
            return crystals;
        }
    }

    // =====================================================================
    // Beschwörung
    // =====================================================================

    public sealed class SummonResult
    {
        public CreatureDef def;
        public bool isNew;
    }

    public static class Summon
    {
        public const int CostSingle = 100;
        public const int CostTen = 900;
        public const int PityRare = 10;     // spätestens jede 10. Beschwörung: mindestens Selten
        public const int PityLegend = 60;   // spätestens jede 60. Beschwörung: mindestens Legendär

        /// <summary>Wahrscheinlichkeiten in Prozent (Summe 100), werden im Spiel angezeigt.</summary>
        public static readonly float[] Rates = { 46f, 28f, 17f, 7f, 1.7f, 0.3f };

        public static Rarity RollRarity(Rng rng)
        {
            float r = rng.Value() * 100f;
            float acc = 0f;
            for (int i = 0; i < Rates.Length; i++)
            {
                acc += Rates[i];
                if (r < acc) return (Rarity)i;
            }
            return Rarity.Gewoehnlich;
        }

        public static bool Pull(PlayerData p, int count, Rng rng, out List<SummonResult> results, out string error)
        {
            results = new List<SummonResult>();
            error = null;
            int cost = count >= 10 ? CostTen : CostSingle * count;
            if (p.crystals < cost) { error = $"Nicht genug Kristalle ({cost} benötigt)."; return false; }
            p.crystals -= cost;

            for (int i = 0; i < count; i++)
            {
                p.pityRare++;
                p.pityLegend++;
                var rarity = RollRarity(rng);
                if (p.pityLegend >= PityLegend && rarity < Rarity.Legendaer) rarity = Rarity.Legendaer;
                if (p.pityRare >= PityRare && rarity < Rarity.Selten) rarity = Rarity.Selten;
                if (rarity >= Rarity.Selten) p.pityRare = 0;
                if (rarity >= Rarity.Legendaer) p.pityLegend = 0;

                var pool = CreatureDb.OfRarity(rarity);
                while (pool.Count == 0 && rarity > Rarity.Gewoehnlich) pool = CreatureDb.OfRarity(--rarity);
                var def = pool[rng.Range(0, pool.Count)];

                var owned = p.Get(def.id);
                bool isNew = owned == null;
                if (isNew) p.creatures.Add(new OwnedCreature { id = def.id });
                else owned.copies++;

                results.Add(new SummonResult { def = def, isNew = isNew });
                p.summons++;
                p.taskSummons++;
            }
            return true;
        }
    }

    // =====================================================================
    // Kampagne
    // =====================================================================

    public sealed class StageDef
    {
        public int region, index;
        public string name;
        public bool elite, boss;
        public TeamSpec enemy;
        public int gold, xp, crystalsFirst, potions, stones;
        public int Seed => 1000 + region * 100 + index;
    }

    public static class Campaign
    {
        public const int RegionCount = 4;
        public const int StagesPerRegion = 8;

        public static readonly string[] RegionNames = { "Grüne Ebenen", "Vulkanregion", "Eisgebirge", "Schattenreich" };

        /// <summary>Festes Wetter je Stufe (jede Region hat ihr typisches Wetter, manche Stufen weichen ab).</summary>
        public static Weather WeatherFor(int region, int index)
        {
            switch (region)
            {
                case 0: return index % 3 == 1 ? Weather.Regen : index % 4 == 3 ? Weather.Sturm : Weather.Klar;
                case 1: return index % 4 == 2 ? Weather.Sturm : Weather.Hitze;
                case 2: return index % 4 == 1 ? Weather.Klar : Weather.Schnee;
                default: return index % 3 == 2 ? Weather.Sturm : Weather.Nebel;
            }
        }
        public static readonly string[] RegionText =
        {
            "Sanfte Hügel, Bäche und alte Bäume. Hier beginnt jede Reise.",
            "Glühende Felsen, Rauch und Lava. Nur starke Teams kommen durch.",
            "Klirrende Kälte und Gletscher. Feuer hilft – Natur leidet.",
            "Ewige Dämmerung. Licht ist hier die stärkste Waffe.",
        };

        static readonly string[][] pools =
        {
            new[] { "krebs", "moos", "biene", "hirsch", "frostgeist", "pieksi", "knochen", "glotz", "pilz", "schwein", "kueken", "pinguin" },
            new[] { "glut", "pieksi", "knochen", "gift", "panda", "zapf", "kueken", "gerippe", "fledermaus", "schleim", "yeti" },
            new[] { "frostgeist", "pinguin", "yeti", "krebs", "schleim", "fledermaus", "glotz", "stern", "zapf", "biene" },
            new[] { "knochen", "gift", "gerippe", "fledermaus", "glotz", "schlund", "panda", "donner", "stern", "glut" },
        };
        static readonly string[] bosses = { "boss_hain", "boss_glut", "boss_frost", "boss_schatten" };

        public static bool IsUnlocked(PlayerData p, int region, int index)
        {
            if (region > 0 && p.regionProgress[region - 1] < StagesPerRegion) return false;
            return index <= p.regionProgress[region];
        }

        public static StageDef Get(int region, int index)
        {
            var s = new StageDef { region = region, index = index };
            s.boss = index == StagesPerRegion - 1;
            s.elite = index == 3 || index == 5;
            s.name = $"{region + 1}-{index + 1}" + (s.boss ? " Boss" : s.elite ? " Elite" : "");

            int level = 1 + region * 9 + index * 2 + (s.elite ? 2 : 0);
            int count = MathK.Clamp(3 + index / 2 + region, 3, 6);
            var rng = new Rng(s.Seed);
            var pool = new List<string>(pools[region]);
            var team = new TeamSpec();
            int[] slotOrder = { 1, 0, 2, 4, 3, 5 };

            if (s.boss)
            {
                team.Add(new UnitSpec(bosses[region], level, 1, StarsFor(level)));
                count = Math.Min(count, 4);
                slotOrder = new[] { 3, 5, 4, 0, 2 };
                for (int i = 0; i < count - 1; i++)
                {
                    var id = pool[rng.Range(0, pool.Count)];
                    pool.Remove(id);
                    team.Add(new UnitSpec(id, level, slotOrder[i], StarsFor(level)));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var id = pool[rng.Range(0, pool.Count)];
                    pool.Remove(id);
                    var d = CreatureDb.Get(id);
                    // Tanks nach vorne, Heiler und Fernkämpfer nach hinten
                    int slot = PickSlot(team, d);
                    team.Add(new UnitSpec(id, level, slot, StarsFor(level)));
                }
            }
            s.enemy = team;

            s.gold = 120 + level * 25 + (s.boss ? 600 : s.elite ? 200 : 0);
            s.xp = 60 + level * 12 + (s.boss ? 150 : 0);
            s.crystalsFirst = s.boss ? 300 : s.elite ? 150 : 60;
            s.potions = s.boss ? 3 : s.elite ? 2 : 1;
            s.stones = s.boss ? 3 : s.elite ? 1 : 0;
            return s;
        }

        /// <summary>Gegner haben gerade so viele Sterne, wie ihre Stufe erfordert.</summary>
        static int StarsFor(int level) => MathK.Clamp((level - 1) / 10, 1, Stats.MaxStars);

        static int PickSlot(TeamSpec team, CreatureDef d)
        {
            bool wantFront = d.role == Role.Tank || (!d.ranged && d.role == Role.Angreifer);
            int[] front = { 1, 0, 2 }, back = { 4, 3, 5 };
            foreach (var s in wantFront ? front : back) if (!Taken(team, s)) return s;
            foreach (var s in wantFront ? back : front) if (!Taken(team, s)) return s;
            return 0;
        }

        static bool Taken(TeamSpec t, int slot)
        {
            foreach (var u in t.units) if (u.slot == slot) return true;
            return false;
        }
    }

    // =====================================================================
    // Belohnung nach dem Kampf
    // =====================================================================

    public sealed class Rewards
    {
        public int gold, xp, crystals, potions, stones;
        public bool firstClear;
        public List<string> gear = new List<string>();
        public List<string> levelUps = new List<string>();
    }

    public static class BattleRewards
    {
        /// <summary>Wendet das Kampfergebnis auf den Spielstand an.</summary>
        public static Rewards Apply(PlayerData p, StageDef stage, BattleResult result)
        {
            var r = new Rewards();
            p.battles++;
            bool won = result.winner == 0;
            if (won)
            {
                p.wins++;
                p.taskWins++;
                r.gold = stage.gold;
                r.xp = stage.xp;
                if (p.regionProgress[stage.region] == stage.index)
                {
                    p.regionProgress[stage.region] = stage.index + 1;
                    r.firstClear = true;
                    r.crystals = stage.crystalsFirst;
                    r.potions = stage.potions;
                    r.stones = stage.stones;
                }
                else
                {
                    r.potions = stage.boss ? 1 : 0;
                }
            }
            else
            {
                r.gold = stage.gold / 5;
                r.xp = stage.xp / 3;
            }

            if (won)
            {
                // Ausrüstung: Boss/Elite beim ersten Sieg sicher, sonst 30 % Chance
                var rng = new Rng(result.seed ^ (p.battles * 7919));
                bool sure = r.firstClear && (stage.boss || stage.elite);
                if (sure || rng.Value() < 0.3f)
                {
                    var g = GearDb.RollDrop(rng, stage.region, stage.boss);
                    p.AddGear(g.id);
                    r.gear.Add(g.name);
                }
            }

            p.gold += r.gold;
            p.crystals += r.crystals;
            p.potions += r.potions;
            p.evoStones += r.stones;

            foreach (var id in p.team)
            {
                var c = p.Get(id);
                if (c == null) continue;
                int ups = Progress.AddXp(c, r.xp);
                if (ups > 0) { r.levelUps.Add($"{c.Name} → Stufe {c.level}"); p.taskLevelUps++; }
            }
            return r;
        }
    }
}
