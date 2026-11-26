using System;
using System.Collections.Generic;

namespace Kesselwerk.Core
{
    // =====================================================================
    // Aufstellung
    // =====================================================================

    /// <summary>Eine Kreatur in einer Aufstellung. Feld 0–2 = vordere Reihe, 3–5 = hintere Reihe.</summary>
    [Serializable]
    public class UnitSpec
    {
        public string id;
        public int level = 1;
        public int stars = 1;
        public int stage = 0;
        public int slot;

        public UnitSpec() { }
        public UnitSpec(string id, int level, int slot, int stars = 1, int stage = 0)
        {
            this.id = id; this.level = level; this.slot = slot; this.stars = stars; this.stage = stage;
        }
    }

    [Serializable]
    public class TeamSpec
    {
        public List<UnitSpec> units = new List<UnitSpec>();
        public const int Slots = 6;

        public TeamSpec Add(UnitSpec u) { units.Add(u); return this; }

        public bool IsValid(out string error)
        {
            error = null;
            if (units.Count == 0) { error = "Das Team ist leer."; return false; }
            if (units.Count > Slots) { error = "Höchstens sechs Kreaturen."; return false; }
            var used = new HashSet<int>();
            foreach (var u in units)
            {
                if (CreatureDb.Get(u.id) == null) { error = "Unbekannte Kreatur: " + u.id; return false; }
                if (u.slot < 0 || u.slot >= Slots || !used.Add(u.slot)) { error = "Ungültiges Feld."; return false; }
                if (u.level < 1 || u.level > Stats.MaxLevelCap || u.stars < 1 || u.stars > Stats.MaxStars) { error = "Ungültige Stufe."; return false; }
            }
            return true;
        }
    }

    // =====================================================================
    // Werte
    // =====================================================================

    public static class Stats
    {
        public const int MaxStars = 5;
        public const int MaxLevelCap = 60;

        public static int MaxLevel(int stars) => 10 + stars * 10;
        public static float LevelMul(int level) => 1f + 0.07f * (level - 1);
        public static float StarMul(int stars) => 1f + 0.15f * (stars - 1);
        public static float StageMul(int stage) => 1f + 0.2f * stage;

        static float Mul(UnitSpec u) => LevelMul(u.level) * StarMul(u.stars) * StageMul(u.stage);

        public static int Hp(CreatureDef d, UnitSpec u) => MathK.RoundToInt(d.hp * Mul(u));
        public static int Atk(CreatureDef d, UnitSpec u) => MathK.RoundToInt(d.atk * Mul(u));
        public static int Def(CreatureDef d, UnitSpec u) => MathK.RoundToInt(d.def * (1f + 0.04f * (u.level - 1)) * StageMul(u.stage));
        public static int Spd(CreatureDef d, UnitSpec u) => d.spd + u.stage * 6 + (u.stars - 1) * 3;

        /// <summary>Grobe Kampfkraft zur Anzeige und für Gegnerwahl.</summary>
        public static int Power(CreatureDef d, UnitSpec u) =>
            MathK.RoundToInt((Hp(d, u) * 0.25f + Atk(d, u) * 2.2f + Def(d, u) * 2f) * (1f + d.crit) * (Spd(d, u) / 100f));
    }

    // =====================================================================
    // Synergien
    // =====================================================================

    public sealed class Synergy
    {
        public string name;
        public string text;
        public int tier; // 1 oder 2
    }

    public static class Synergies
    {
        /// <summary>Elementbonus ab 2 bzw. 3 Kreaturen desselben Elements.</summary>
        public static string ElementText(Element e, int tier)
        {
            int t = tier;
            switch (e)
            {
                case Element.Feuer: return t == 1 ? "+10 % Angriff" : "+25 % Angriff";
                case Element.Wasser: return t == 1 ? "+20 % Heilung erhalten" : "+40 % Heilung erhalten";
                case Element.Natur: return t == 1 ? "Regeneration 3 % pro Aktion" : "Regeneration 6 % pro Aktion";
                case Element.Erde: return t == 1 ? "+15 % Verteidigung" : "+35 % Verteidigung";
                case Element.Luft: return t == 1 ? "+10 % Tempo" : "+20 % Tempo";
                case Element.Blitz: return t == 1 ? "+10 % Kritchance" : "+20 % Kritchance";
                case Element.Eis: return t == 1 ? "Gegner −5 % Tempo" : "Gegner −12 % Tempo";
                case Element.Licht: return t == 1 ? "Startschild 10 %" : "Startschild 20 %";
                case Element.Schatten: return t == 1 ? "Lebensraub 8 %" : "Lebensraub 16 %";
                default: return t == 1 ? "+20 % Energie" : "+40 % Energie";
            }
        }

        public static List<Synergy> For(TeamSpec team)
        {
            var list = new List<Synergy>();
            var counts = new int[10];
            var roles = new int[4];
            var seen = new HashSet<string>();
            foreach (var u in team.units)
            {
                var d = CreatureDb.Get(u.id);
                if (d == null) continue;
                roles[(int)d.role]++;
                if (seen.Add(d.id)) counts[(int)d.element]++; // gleiche Kreatur zählt nur einmal
            }
            for (int i = 0; i < 10; i++)
            {
                int tier = counts[i] >= 3 ? 2 : counts[i] >= 2 ? 1 : 0;
                if (tier > 0)
                    list.Add(new Synergy { name = ((Element)i).ToString() + (tier == 2 ? " III" : " II"), text = ElementText((Element)i, tier), tier = tier });
            }
            if (roles[(int)Role.Tank] > 0 && roles[(int)Role.Heiler] > 0)
                list.Add(new Synergy { name = "Bollwerk", text = "Tank + Heiler: +10 % Verteidigung", tier = 1 });
            if (roles[(int)Role.Angreifer] >= 2 && roles[(int)Role.Support] > 0)
                list.Add(new Synergy { name = "Rudeljagd", text = "2 Angreifer + Support: +8 % Angriff", tier = 1 });
            if (roles[0] > 0 && roles[1] > 0 && roles[2] > 0 && roles[3] > 0)
                list.Add(new Synergy { name = "Ausgewogen", text = "Alle vier Rollen: +6 % auf alle Werte", tier = 1 });
            return list;
        }

        internal static int ElementTier(TeamSpec team, Element e)
        {
            var seen = new HashSet<string>();
            int n = 0;
            foreach (var u in team.units)
            {
                var d = CreatureDb.Get(u.id);
                if (d != null && d.element == e && seen.Add(d.id)) n++;
            }
            return n >= 3 ? 2 : n >= 2 ? 1 : 0;
        }
    }

    // =====================================================================
    // Kampfeinheit
    // =====================================================================

    public sealed class BattleUnit
    {
        public int uid, team, slot;
        public CreatureDef def;
        public UnitSpec spec;

        public float maxHp, hp, atk, defense, spd, crit, critDmg = 1.5f, dodge, lifesteal, regen, energyMul = 1f, healTakenMul = 1f, thorns;
        public float shield, gauge, energy;
        public float startShield;

        public int burnTurns; public float burnDmg; public int burnSource = -1;
        public int stunTurns;
        public int atkUpTurns; public float atkUpPct;
        public int defDownTurns; public float defDownPct;
        public int tauntTurns;
        public int rage;

        public int damageDealt, healingDone, damageTaken;
        public bool dead;

        public bool Alive => hp > 0f;
        public bool Front => slot < 3;
        public int Column => slot % 3;
        public float HpPct => maxHp > 0 ? hp / maxHp : 0f;
        public string Name => def.StageName(spec.stage);

        public float CurrentAtk => atk * (1f + (atkUpTurns > 0 ? atkUpPct : 0f) + rage * 0.08f);
        public float CurrentDef => defense * (1f - (defDownTurns > 0 ? defDownPct : 0f));
    }

    // =====================================================================
    // Ereignisse (werden in 3D abgespielt)
    // =====================================================================

    public enum EvType
    {
        Attack,     // Grundangriff beginnt (actor -> target)
        Skill,      // Spezialfähigkeit beginnt (actor, text = Name)
        Damage,     // Schaden (actor -> target, value)
        Heal,       // Heilung (actor -> target, value)
        Shield,     // Schild erhalten (target, value)
        Buff,       // Angriff erhöht (target)
        Debuff,     // Verteidigung gesenkt (target)
        Burn,       // Brennt (target)
        BurnTick,   // Brandschaden (target, value)
        Stun,       // betäubt (target)
        Stunned,    // setzt aus (actor)
        Dodge,      // ausgewichen (target)
        Taunt,      // provoziert (actor)
        Death,      // stirbt (target)
        End,        // Kampfende (value = Sieger)
    }

    public struct BattleEvent
    {
        public EvType type;
        public int actor, target, value;
        public bool crit;
        public float elem;      // Elementfaktor (1 = neutral)
        public SkillKind skill;
        public string text;

        public override string ToString() => $"{type} {actor}->{target} {value}{(crit ? " KRIT" : "")} {text}";
    }

    public sealed class BattleResult
    {
        public int winner;  // 0 = Spieler, 1 = Gegner
        public int actions;
        public bool timeout;
        public List<BattleUnit> units;
        public List<BattleEvent> events;
        public int seed;

        public int Alive(int team)
        {
            int n = 0;
            foreach (var u in units) if (u.team == team && u.Alive) n++;
            return n;
        }
    }

    // =====================================================================
    // Simulation
    // =====================================================================

    /// <summary>
    /// Vollautomatischer, deterministischer Kampf. Gleiche Teams + gleicher Startwert = gleiches Ergebnis.
    /// Dadurch kann ein Server jeden Kampf nachrechnen und Manipulation erkennen.
    /// </summary>
    public sealed class BattleSim
    {
        public const int MaxActions = 400;
        public const float EnergyPerAttack = 25f;
        public const float EnergyPerHit = 10f;
        public const float GaugeFull = 1000f;
        public const float DamageScale = 1.9f; // Tempo der Kämpfe (höher = kürzer)

        readonly List<BattleUnit> units = new List<BattleUnit>();
        readonly List<BattleEvent> events = new List<BattleEvent>();
        readonly Rng rng;
        int actions;

        public static BattleResult Run(TeamSpec player, TeamSpec enemy, int seed)
        {
            var sim = new BattleSim(player, enemy, seed);
            return sim.Execute(seed);
        }

        BattleSim(TeamSpec player, TeamSpec enemy, int seed)
        {
            rng = new Rng(seed);
            AddTeam(player, 0, enemy);
            AddTeam(enemy, 1, player);
        }

        void AddTeam(TeamSpec team, int side, TeamSpec other)
        {
            float atkB = 0f, defB = 0f, spdB = 0f, critB = 0f, regenB = 0f, lsB = 0f, energyB = 0f, healB = 0f, shieldB = 0f, allB = 0f;
            switch (Synergies.ElementTier(team, Element.Feuer)) { case 1: atkB += 0.10f; break; case 2: atkB += 0.25f; break; }
            switch (Synergies.ElementTier(team, Element.Wasser)) { case 1: healB += 0.2f; break; case 2: healB += 0.4f; break; }
            switch (Synergies.ElementTier(team, Element.Natur)) { case 1: regenB += 0.03f; break; case 2: regenB += 0.06f; break; }
            switch (Synergies.ElementTier(team, Element.Erde)) { case 1: defB += 0.15f; break; case 2: defB += 0.35f; break; }
            switch (Synergies.ElementTier(team, Element.Luft)) { case 1: spdB += 0.10f; break; case 2: spdB += 0.20f; break; }
            switch (Synergies.ElementTier(team, Element.Blitz)) { case 1: critB += 0.10f; break; case 2: critB += 0.20f; break; }
            switch (Synergies.ElementTier(other, Element.Eis)) { case 1: spdB -= 0.05f; break; case 2: spdB -= 0.12f; break; }
            switch (Synergies.ElementTier(team, Element.Licht)) { case 1: shieldB += 0.10f; break; case 2: shieldB += 0.20f; break; }
            switch (Synergies.ElementTier(team, Element.Schatten)) { case 1: lsB += 0.08f; break; case 2: lsB += 0.16f; break; }
            switch (Synergies.ElementTier(team, Element.Arkan)) { case 1: energyB += 0.2f; break; case 2: energyB += 0.4f; break; }

            foreach (var s in Synergies.For(team))
            {
                if (s.name == "Bollwerk") defB += 0.10f;
                if (s.name == "Rudeljagd") atkB += 0.08f;
                if (s.name == "Ausgewogen") allB += 0.06f;
            }

            var sorted = new List<UnitSpec>(team.units);
            sorted.Sort((a, b) => a.slot.CompareTo(b.slot));
            foreach (var spec in sorted)
            {
                var d = CreatureDb.Get(spec.id);
                if (d == null) continue;
                var u = new BattleUnit
                {
                    uid = units.Count, team = side, slot = spec.slot, def = d, spec = spec,
                };
                u.maxHp = Stats.Hp(d, spec) * (1f + allB);
                u.hp = u.maxHp;
                u.atk = Stats.Atk(d, spec) * (1f + atkB + allB);
                u.defense = Stats.Def(d, spec) * (1f + defB + allB);
                u.spd = Stats.Spd(d, spec) * (1f + spdB + allB);
                u.crit = d.crit + critB;
                u.regen = regenB;
                u.lifesteal = lsB;
                u.energyMul = 1f + energyB;
                u.healTakenMul = 1f + healB;

                switch (d.passive)
                {
                    case Passive.Kritmeister: u.crit += 0.2f; u.critDmg = 2f; break;
                    case Passive.Ausweichen: u.dodge = 0.15f; break;
                    case Passive.Lebensraub: u.lifesteal += 0.2f; break;
                    case Passive.Regeneration: u.regen += 0.05f; break;
                    case Passive.Dornen: u.thorns = 0.2f; break;
                    case Passive.Startschild: u.shield += u.maxHp * 0.25f; break;
                }
                u.shield += u.maxHp * shieldB;
                u.startShield = u.shield;
                // leicht versetzter Start, damit nicht alle gleichzeitig handeln
                u.gauge = rng.Range(0f, 200f);
                units.Add(u);
            }
        }

        BattleResult Execute(int seed)
        {
            int winner = -1;
            bool timeout = false;
            var ready = new List<BattleUnit>();

            while (winner < 0)
            {
                // Uhr weiterdrehen, bis mindestens eine Kreatur dran ist
                ready.Clear();
                float minTime = float.MaxValue;
                foreach (var u in units)
                {
                    if (!u.Alive) continue;
                    float t = (GaugeFull - u.gauge) / Math.Max(1f, u.spd);
                    if (t < minTime) minTime = t;
                }
                if (minTime < 0f) minTime = 0f;
                foreach (var u in units)
                {
                    if (!u.Alive) continue;
                    u.gauge += u.spd * minTime;
                    if (u.gauge >= GaugeFull - 0.001f) ready.Add(u);
                }
                ready.Sort((a, b) =>
                {
                    int c = b.spd.CompareTo(a.spd);
                    if (c != 0) return c;
                    c = a.team.CompareTo(b.team);
                    return c != 0 ? c : a.slot.CompareTo(b.slot);
                });

                foreach (var actor in ready)
                {
                    if (!actor.Alive) continue;
                    actor.gauge -= GaugeFull;
                    Act(actor);
                    actions++;
                    winner = CheckWinner();
                    if (winner >= 0) break;
                    if (actions >= MaxActions)
                    {
                        timeout = true;
                        winner = TeamHpPct(0) > TeamHpPct(1) ? 0 : 1; // bei Gleichstand gewinnt der Verteidiger
                        break;
                    }
                }
            }

            events.Add(new BattleEvent { type = EvType.End, value = winner });
            return new BattleResult { winner = winner, actions = actions, timeout = timeout, units = units, events = events, seed = seed };
        }

        int CheckWinner()
        {
            bool a = false, b = false;
            foreach (var u in units)
            {
                if (!u.Alive) continue;
                if (u.team == 0) a = true; else b = true;
            }
            if (!b) return 0;
            if (!a) return 1;
            return -1;
        }

        float TeamHpPct(int team)
        {
            float hp = 0f, max = 0f;
            foreach (var u in units) if (u.team == team) { hp += Math.Max(0f, u.hp); max += u.maxHp; }
            return max > 0 ? hp / max : 0f;
        }

        // -----------------------------------------------------------------
        // Aktionen
        // -----------------------------------------------------------------

        void Act(BattleUnit a)
        {
            // Zustände zu Beginn der eigenen Aktion
            if (a.burnTurns > 0)
            {
                a.burnTurns--;
                int dmg = MathK.RoundToInt(a.burnDmg);
                ApplyRaw(a, dmg);
                if (a.burnSource >= 0) units[a.burnSource].damageDealt += dmg;
                Emit(EvType.BurnTick, a.uid, a.uid, dmg);
                if (!a.Alive) { Die(a); return; }
            }
            if (a.regen > 0f && a.hp < a.maxHp)
                Heal(a, a, a.maxHp * a.regen, false);

            if (a.atkUpTurns > 0) a.atkUpTurns--;
            if (a.defDownTurns > 0) a.defDownTurns--;
            if (a.tauntTurns > 0) a.tauntTurns--;

            if (a.stunTurns > 0)
            {
                a.stunTurns--;
                Emit(EvType.Stunned, a.uid, a.uid, 0);
                return;
            }

            if (a.energy >= 100f) { a.energy = 0f; CastSkill(a); }
            else BasicAttack(a);
        }

        void BasicAttack(BattleUnit a)
        {
            var t = ChooseTarget(a);
            if (t == null) return;
            Emit(EvType.Attack, a.uid, t.uid, 0);
            a.energy += EnergyPerAttack * a.energyMul;
            Hit(a, t, 1f, true, !a.def.ranged);
        }

        void CastSkill(BattleUnit a)
        {
            var s = a.def.skill;
            var ev = new BattleEvent { type = EvType.Skill, actor = a.uid, target = -1, skill = s.kind, text = s.name };

            switch (s.kind)
            {
                case SkillKind.Strike:
                {
                    var t = ChooseTarget(a); if (t == null) return;
                    ev.target = t.uid; events.Add(ev);
                    Hit(a, t, s.power, true, !a.def.ranged);
                    break;
                }
                case SkillKind.Burn:
                {
                    var t = ChooseTarget(a); if (t == null) return;
                    ev.target = t.uid; events.Add(ev);
                    if (Hit(a, t, s.power, true, false) && t.Alive)
                    {
                        t.burnTurns = s.duration;
                        t.burnDmg = a.CurrentAtk * 0.3f;
                        t.burnSource = a.uid;
                        Emit(EvType.Burn, a.uid, t.uid, s.duration);
                    }
                    break;
                }
                case SkillKind.Stun:
                {
                    var t = ChooseTarget(a); if (t == null) return;
                    ev.target = t.uid; events.Add(ev);
                    if (Hit(a, t, s.power, true, false) && t.Alive)
                    {
                        t.stunTurns = Math.Max(t.stunTurns, s.duration);
                        Emit(EvType.Stun, a.uid, t.uid, s.duration);
                    }
                    break;
                }
                case SkillKind.Execute:
                {
                    var t = LowestEnemy(a); if (t == null) return;
                    ev.target = t.uid; events.Add(ev);
                    float p = t.HpPct < 0.5f ? s.power * 2f : s.power;
                    Hit(a, t, p, true, false);
                    break;
                }
                case SkillKind.Drain:
                {
                    var t = ChooseTarget(a); if (t == null) return;
                    ev.target = t.uid; events.Add(ev);
                    int before = a.damageDealt;
                    Hit(a, t, s.power, true, false);
                    Heal(a, a, (a.damageDealt - before) * 0.5f, true);
                    break;
                }
                case SkillKind.Cleave:
                {
                    var t = ChooseTarget(a); if (t == null) return;
                    ev.target = t.uid; events.Add(ev);
                    var targets = new List<BattleUnit> { t };
                    foreach (var e in Enemies(a))
                        if (e != t && e.Front == t.Front && Math.Abs(e.Column - t.Column) == 1) targets.Add(e);
                    foreach (var e in targets)
                        if (Hit(a, e, s.power, false, false) && e.Alive && s.duration > 0 && e == t)
                        {
                            e.stunTurns = Math.Max(e.stunTurns, s.duration);
                            Emit(EvType.Stun, a.uid, e.uid, s.duration);
                        }
                    break;
                }
                case SkillKind.AllEnemies:
                {
                    events.Add(ev);
                    foreach (var e in Enemies(a)) Hit(a, e, s.power, false, false);
                    break;
                }
                case SkillKind.BackLine:
                {
                    events.Add(ev);
                    var list = new List<BattleUnit>();
                    foreach (var e in Enemies(a)) if (!e.Front) list.Add(e);
                    if (list.Count == 0) list = Enemies(a);
                    foreach (var e in list) Hit(a, e, s.power, false, false);
                    break;
                }
                case SkillKind.Chain:
                {
                    events.Add(ev);
                    var pool = Enemies(a);
                    float p = s.power;
                    for (int i = 0; i < 3 && pool.Count > 0; i++)
                    {
                        var e = pool[rng.Range(0, pool.Count)];
                        pool.Remove(e);
                        Hit(a, e, p, false, false);
                        p *= 0.8f;
                    }
                    break;
                }
                case SkillKind.HealLowest:
                {
                    var t = LowestAlly(a);
                    ev.target = t.uid; events.Add(ev);
                    Heal(a, t, a.CurrentAtk * s.power, true);
                    break;
                }
                case SkillKind.HealAll:
                {
                    events.Add(ev);
                    foreach (var t in Allies(a)) Heal(a, t, a.CurrentAtk * s.power, true);
                    break;
                }
                case SkillKind.ShieldAll:
                {
                    events.Add(ev);
                    foreach (var t in Allies(a))
                    {
                        float v = t.maxHp * 0.12f + a.CurrentAtk * s.power;
                        t.shield += v;
                        Emit(EvType.Shield, a.uid, t.uid, MathK.RoundToInt(v));
                    }
                    break;
                }
                case SkillKind.Taunt:
                {
                    ev.target = a.uid; events.Add(ev);
                    float v = a.maxHp * 0.25f * s.power;
                    a.shield += v;
                    a.tauntTurns = s.duration;
                    Emit(EvType.Shield, a.uid, a.uid, MathK.RoundToInt(v));
                    Emit(EvType.Taunt, a.uid, a.uid, s.duration);
                    break;
                }
                case SkillKind.BuffAttack:
                {
                    events.Add(ev);
                    foreach (var t in Allies(a))
                    {
                        t.atkUpTurns = s.duration + 1;
                        t.atkUpPct = Math.Max(t.atkUpPct, s.power);
                        Emit(EvType.Buff, a.uid, t.uid, MathK.RoundToInt(s.power * 100f));
                    }
                    break;
                }
                case SkillKind.DebuffDefense:
                {
                    events.Add(ev);
                    foreach (var t in Enemies(a))
                    {
                        t.defDownTurns = s.duration + 1;
                        t.defDownPct = Math.Max(t.defDownPct, s.power);
                        Emit(EvType.Debuff, a.uid, t.uid, MathK.RoundToInt(s.power * 100f));
                    }
                    // leichter Zusatzschaden auf ein Ziel, damit die Fähigkeit nicht "leer" wirkt
                    var t2 = ChooseTarget(a);
                    if (t2 != null) Hit(a, t2, 0.8f, false, false);
                    break;
                }
            }
        }

        // -----------------------------------------------------------------
        // Schaden und Heilung
        // -----------------------------------------------------------------

        /// <summary>Fügt Schaden zu. Gibt false zurück, wenn ausgewichen wurde.</summary>
        bool Hit(BattleUnit a, BattleUnit t, float power, bool canDodge, bool melee)
        {
            if (!t.Alive) return false;
            if (canDodge && t.dodge > 0f && rng.Value() < t.dodge)
            {
                Emit(EvType.Dodge, a.uid, t.uid, 0);
                return false;
            }

            float elem = ElementChart.Multiplier(a.def.element, t.def.element);
            float dmg = a.CurrentAtk * power * DamageScale * (100f / (100f + Math.Max(0f, t.CurrentDef)));
            float elemMul = elem;
            if (elem > 1f && a.def.passive == Passive.Elementkraft) elemMul += 0.15f;
            dmg *= elemMul;
            bool crit = rng.Value() < a.crit;
            if (crit) dmg *= a.critDmg;
            dmg *= rng.Range(0.92f, 1.08f);
            int value = Math.Max(1, MathK.RoundToInt(dmg));

            ApplyRaw(t, value);
            a.damageDealt += value;
            events.Add(new BattleEvent { type = EvType.Damage, actor = a.uid, target = t.uid, value = value, crit = crit, elem = elem });

            t.energy = Math.Min(100f, t.energy + EnergyPerHit * t.energyMul);
            if (t.def.passive == Passive.Wut && t.rage < 5) t.rage++;

            if (a.lifesteal > 0f && a.Alive) Heal(a, a, value * a.lifesteal, false);

            if (melee && t.thorns > 0f && a.Alive)
            {
                int back = Math.Max(1, MathK.RoundToInt(value * t.thorns));
                ApplyRaw(a, back);
                t.damageDealt += back;
                events.Add(new BattleEvent { type = EvType.Damage, actor = t.uid, target = a.uid, value = back, elem = 1f, text = "Dornen" });
                if (!a.Alive) Die(a);
            }

            if (!t.Alive) Die(t);
            return true;
        }

        void ApplyRaw(BattleUnit t, int value)
        {
            float v = value;
            if (t.shield > 0f)
            {
                float absorbed = Math.Min(t.shield, v);
                t.shield -= absorbed;
                v -= absorbed;
            }
            t.hp -= v;
            t.damageTaken += value;
        }

        void Heal(BattleUnit a, BattleUnit t, float amount, bool emitAlways)
        {
            if (!t.Alive) return;
            float v = amount * t.healTakenMul;
            float real = Math.Min(v, t.maxHp - t.hp);
            if (real <= 0f && !emitAlways) return;
            t.hp += Math.Max(0f, real);
            int value = MathK.RoundToInt(Math.Max(0f, real));
            a.healingDone += value;
            Emit(EvType.Heal, a.uid, t.uid, value);
        }

        void Die(BattleUnit t)
        {
            if (t.hp > 0f || t.dead) return;
            t.dead = true;
            t.hp = 0f;
            Emit(EvType.Death, t.uid, t.uid, 0);
            if (t.def.passive == Passive.Todesexplosion)
                foreach (var e in Enemies(t)) Hit(t, e, 0.6f, false, false);
        }

        void Emit(EvType type, int actor, int target, int value) =>
            events.Add(new BattleEvent { type = type, actor = actor, target = target, value = value, elem = 1f });

        // -----------------------------------------------------------------
        // Zielwahl (KI)
        // -----------------------------------------------------------------

        List<BattleUnit> Enemies(BattleUnit a)
        {
            var l = new List<BattleUnit>();
            foreach (var u in units) if (u.Alive && u.team != a.team) l.Add(u);
            return l;
        }

        List<BattleUnit> Allies(BattleUnit a)
        {
            var l = new List<BattleUnit>();
            foreach (var u in units) if (u.Alive && u.team == a.team) l.Add(u);
            return l;
        }

        /// <summary>
        /// Provozierende Gegner zuerst. Nahkämpfer greifen die vordere Reihe an (gleiche Spalte bevorzugt),
        /// erst wenn sie leer ist die hintere. Fernkämpfer konzentrieren sich auf den schwächsten Gegner.
        /// </summary>
        BattleUnit ChooseTarget(BattleUnit a)
        {
            var enemies = Enemies(a);
            if (enemies.Count == 0) return null;

            BattleUnit best = null;
            foreach (var e in enemies)
                if (e.tauntTurns > 0 && (best == null || e.hp < best.hp)) best = e;
            if (best != null) return best;

            if (a.def.ranged)
            {
                foreach (var e in enemies)
                    if (best == null || e.HpPct < best.HpPct || (e.HpPct == best.HpPct && e.slot < best.slot)) best = e;
                return best;
            }

            bool frontAlive = false;
            foreach (var e in enemies) if (e.Front) { frontAlive = true; break; }
            int bestDist = int.MaxValue;
            foreach (var e in enemies)
            {
                if (frontAlive && !e.Front) continue;
                int dist = Math.Abs(e.Column - a.Column);
                if (dist < bestDist || (dist == bestDist && e.slot < best.slot)) { best = e; bestDist = dist; }
            }
            return best;
        }

        BattleUnit LowestEnemy(BattleUnit a)
        {
            BattleUnit best = null;
            foreach (var e in Enemies(a)) if (best == null || e.HpPct < best.HpPct) best = e;
            return best;
        }

        BattleUnit LowestAlly(BattleUnit a)
        {
            BattleUnit best = a;
            foreach (var e in Allies(a)) if (e.HpPct < best.HpPct) best = e;
            return best;
        }
    }
}
