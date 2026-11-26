using System;
using System.Collections.Generic;

namespace Kesselwerk.Core
{
    /// <summary>Wie eine Attacke im Kampf aussieht (rein optisch, beeinflusst die Rechnung nicht).</summary>
    public enum MoveFx
    {
        Slash,      // Klauenhieb / Schnitt
        Bite,       // Biss
        Charge,     // Ansturm mit Wucht
        Projectile, // Geschoss
        Beam,       // Strahl
        Nova,       // Explosion um das Ziel
        Rain,       // Regen aus Feuer/Sternen vom Himmel
        Lightning,  // Blitz
        Quake,      // Erdbeben / Stampfer
        Vortex,     // Wirbelsturm
        Wave,       // Flutwelle
        Blizzard,   // Eissturm
        Cloud,      // Giftwolke / Albtraum
        HealAura,   // Heilung
        ShieldDome, // Schutzkuppel
        Roar,       // Kampfschrei
        Spikes,     // Dornen / Stacheln aus dem Boden
    }

    /// <summary>Eine Attacke aus der Bibliothek.</summary>
    public sealed class MoveDef
    {
        public string id, name, text;
        public SkillKind kind;
        public float power;
        public int duration;
        public MoveFx fx;

        public MoveDef(string id, string name, SkillKind kind, float power, int duration, MoveFx fx, string text)
        {
            this.id = id; this.name = name; this.kind = kind; this.power = power; this.duration = duration; this.fx = fx; this.text = text;
        }
    }

    /// <summary>Eine Attacke im Pool einer Kreatur (mit Freischalt-Stufe).</summary>
    public sealed class PoolMove
    {
        public MoveDef move;
        public int unlock;      // ab dieser Stufe nutzbar
        public bool signature;  // Markenattacke der Kreatur
    }

    /// <summary>
    /// Attacken: Jede Kreatur hat vier Attacken (Markenattacke + drei aus Element und Rolle).
    /// Zwei davon nimmt sie in den Kampf mit. Attacken steigen mit der Stufe im Rang (I–V) und werden stärker.
    /// </summary>
    public static class MoveDb
    {
        public static readonly int[] Unlock = { 1, 6, 14, 25 };
        public const int MaxRank = 5;
        public const int LevelsPerRank = 7;
        public const int Equip = 2;

        static readonly Dictionary<string, MoveDef> all = new Dictionary<string, MoveDef>();
        static readonly Dictionary<Element, MoveDef[]> byElement = new Dictionary<Element, MoveDef[]>();
        static readonly Dictionary<Role, MoveDef[]> byRole = new Dictionary<Role, MoveDef[]>();
        static readonly Dictionary<string, PoolMove[]> pools = new Dictionary<string, PoolMove[]>();

        static MoveDef M(string id, string name, SkillKind k, float p, int d, MoveFx fx, string text)
        {
            var m = new MoveDef(id, name, k, p, d, fx, text);
            all[id] = m;
            return m;
        }

        static MoveDb()
        {
            byElement[Element.Feuer] = new[] {
                M("glutball", "Glutball", SkillKind.Burn, 1.3f, 2, MoveFx.Projectile, "Ein Feuerball, das Ziel brennt."),
                M("feuersturm", "Feuersturm", SkillKind.AllEnemies, 0.75f, 0, MoveFx.Rain, "Feuer regnet auf alle Gegner.") };
            byElement[Element.Wasser] = new[] {
                M("wasserstrahl", "Wasserstrahl", SkillKind.Strike, 1.5f, 0, MoveFx.Beam, "Ein harter Wasserstrahl auf ein Ziel."),
                M("flutwelle_m", "Flutwelle", SkillKind.BackLine, 0.95f, 0, MoveFx.Wave, "Eine Welle überrollt die hintere Reihe.") };
            byElement[Element.Natur] = new[] {
                M("rankenpeitsche", "Rankenpeitsche", SkillKind.Cleave, 1.15f, 0, MoveFx.Spikes, "Ranken schlagen das Ziel und seine Nachbarn."),
                M("sporenwolke", "Sporenwolke", SkillKind.Poison, 0.45f, 2, MoveFx.Cloud, "Giftsporen auf alle Gegner.") };
            byElement[Element.Erde] = new[] {
                M("felsbrocken", "Felsbrocken", SkillKind.Stun, 1.25f, 1, MoveFx.Projectile, "Ein Felsbrocken betäubt das Ziel."),
                M("erdbeben", "Erdbeben", SkillKind.AllEnemies, 0.7f, 0, MoveFx.Quake, "Der Boden bebt unter allen Gegnern.") };
            byElement[Element.Luft] = new[] {
                M("windklinge", "Windklinge", SkillKind.Chain, 1.15f, 0, MoveFx.Slash, "Eine Windklinge springt über drei Gegner."),
                M("wirbelsturm", "Wirbelsturm", SkillKind.BackLine, 0.9f, 0, MoveFx.Vortex, "Ein Wirbelsturm fegt die hintere Reihe.") };
            byElement[Element.Blitz] = new[] {
                M("donnerkeil", "Donnerkeil", SkillKind.Stun, 1.2f, 1, MoveFx.Lightning, "Ein Blitz aus dem Himmel betäubt das Ziel."),
                M("kettenblitz", "Kettenblitz", SkillKind.Chain, 1.2f, 0, MoveFx.Lightning, "Ein Blitz springt über drei Gegner.") };
            byElement[Element.Eis] = new[] {
                M("eisspeer", "Eisspeer", SkillKind.Freeze, 1.35f, 2, MoveFx.Projectile, "Ein Eisspeer verlangsamt das Ziel."),
                M("blizzard", "Blizzard", SkillKind.AllEnemies, 0.7f, 0, MoveFx.Blizzard, "Ein Eissturm über allen Gegnern.") };
            byElement[Element.Licht] = new[] {
                M("lichtstrahl", "Lichtstrahl", SkillKind.Strike, 1.55f, 0, MoveFx.Beam, "Ein gleißender Lichtstrahl."),
                M("segenslicht", "Segenslicht", SkillKind.HealAll, 0.9f, 0, MoveFx.HealAura, "Warmes Licht heilt alle Verbündeten.") };
            byElement[Element.Schatten] = new[] {
                M("schattenbiss", "Schattenbiss", SkillKind.Drain, 1.35f, 0, MoveFx.Bite, "Ein Biss, der Leben stiehlt."),
                M("albtraum", "Albtraum", SkillKind.DebuffDefense, 0.25f, 2, MoveFx.Cloud, "Senkt die Verteidigung aller Gegner.") };
            byElement[Element.Arkan] = new[] {
                M("arkankugel", "Arkankugel", SkillKind.Execute, 1.3f, 0, MoveFx.Nova, "Trifft angeschlagene Gegner doppelt."),
                M("sternregen", "Sternregen", SkillKind.AllEnemies, 0.8f, 0, MoveFx.Rain, "Sterne regnen auf alle Gegner.") };

            byRole[Role.Angreifer] = new[] {
                M("ansturm", "Wilder Ansturm", SkillKind.Strike, 1.8f, 0, MoveFx.Charge, "Ein wuchtiger Ansturm auf ein Ziel."),
                M("raubbiss", "Raubtierbiss", SkillKind.Execute, 1.4f, 0, MoveFx.Bite, "Ein Biss, doppelt gegen Angeschlagene.") };
            byRole[Role.Tank] = new[] {
                M("panzerhaltung", "Panzerhaltung", SkillKind.Taunt, 1.0f, 2, MoveFx.ShieldDome, "Schild für sich, zieht Angriffe auf sich."),
                M("stampfer", "Stampfer", SkillKind.Cleave, 1.1f, 1, MoveFx.Quake, "Stampft Ziel und Nachbarn, betäubt das Ziel.") };
            byRole[Role.Support] = new[] {
                M("kampfschrei", "Kampfschrei", SkillKind.BuffAttack, 0.25f, 2, MoveFx.Roar, "Alle Verbündeten erhalten mehr Angriff."),
                M("schutzwall", "Schutzwall", SkillKind.ShieldAll, 0.5f, 0, MoveFx.ShieldDome, "Ein Schild für alle Verbündeten.") };
            byRole[Role.Heiler] = new[] {
                M("heilhaende", "Heilende Hände", SkillKind.HealLowest, 2.0f, 0, MoveFx.HealAura, "Heilt den schwächsten Verbündeten stark."),
                M("lebensquelle", "Lebensquelle", SkillKind.HealAll, 1.0f, 0, MoveFx.HealAura, "Heilt alle Verbündeten.") };
        }

        public static MoveDef Get(string id) { lock (pools) return id != null && all.TryGetValue(id, out var m) ? m : null; }
        public static IEnumerable<MoveDef> All => all.Values;

        /// <summary>Stabiler Hash (nicht string.GetHashCode – der ist in .NET pro Prozess zufällig).</summary>
        static uint Hash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }

        /// <summary>Optik der Markenattacke aus Art, Element und Nah-/Fernkampf ableiten.</summary>
        public static MoveFx FxFor(SkillKind k, Element e, bool ranged)
        {
            switch (k)
            {
                case SkillKind.HealLowest: case SkillKind.HealAll: return MoveFx.HealAura;
                case SkillKind.ShieldAll: case SkillKind.Taunt: return MoveFx.ShieldDome;
                case SkillKind.BuffAttack: return MoveFx.Roar;
                case SkillKind.DebuffDefense: case SkillKind.Poison: return MoveFx.Cloud;
                case SkillKind.Chain: return MoveFx.Lightning;
                case SkillKind.Drain: return MoveFx.Bite;
                case SkillKind.Freeze: return ranged ? MoveFx.Projectile : MoveFx.Slash;
                case SkillKind.Burn: return ranged ? MoveFx.Projectile : MoveFx.Charge;
                case SkillKind.Stun: return e == Element.Blitz ? MoveFx.Lightning : ranged ? MoveFx.Projectile : MoveFx.Charge;
                case SkillKind.Cleave: return MoveFx.Slash;
                case SkillKind.Execute: return ranged ? MoveFx.Nova : MoveFx.Bite;
                case SkillKind.Strike: return ranged ? MoveFx.Beam : MoveFx.Charge;
                case SkillKind.BackLine: return e == Element.Wasser ? MoveFx.Wave : e == Element.Luft ? MoveFx.Vortex : MoveFx.Rain;
                case SkillKind.AllEnemies:
                    switch (e)
                    {
                        case Element.Wasser: return MoveFx.Wave;
                        case Element.Erde: return MoveFx.Quake;
                        case Element.Eis: return MoveFx.Blizzard;
                        case Element.Blitz: return MoveFx.Lightning;
                        case Element.Luft: return MoveFx.Vortex;
                        default: return MoveFx.Rain;
                    }
            }
            return MoveFx.Nova;
        }

        /// <summary>Die vier Attacken einer Kreatur (fest, für alle Spieler gleich).</summary>
        public static PoolMove[] PoolFor(CreatureDef d)
        {
            if (d == null) return new PoolMove[0];
            lock (pools) return BuildPool(d);
        }

        static PoolMove[] BuildPool(CreatureDef d)
        {
            if (pools.TryGetValue(d.id, out var cached)) return cached;
            var list = new List<PoolMove>();
            var sig = new MoveDef("sig_" + d.id, d.skill.name, d.skill.kind, d.skill.power, d.skill.duration,
                                  FxFor(d.skill.kind, d.element, d.ranged), d.skill.text);
            all[sig.id] = sig;
            list.Add(new PoolMove { move = sig, unlock = Unlock[0], signature = true });
            if (!d.isBoss)
            {
                uint h = Hash(d.id);
                var el = byElement.TryGetValue(d.element, out var e) ? e : byElement[Element.Feuer];
                var ro = byRole[d.role];
                var cand = new List<MoveDef>
                {
                    ro[(int)(h % 2)],          // Rollenattacke
                    el[(int)((h >> 3) % 2)],   // Elementattacke
                    el[(int)(((h >> 3) + 1) % 2)],
                    ro[(int)((h + 1) % 2)],
                };
                foreach (var m in cand)
                {
                    if (list.Count >= 4) break;
                    bool dup = false;
                    foreach (var x in list) if (x.move.name == m.name || x.move.kind == m.kind && x.move.fx == m.fx) dup = true;
                    if (dup) continue;
                    list.Add(new PoolMove { move = m, unlock = Unlock[list.Count] });
                }
                // Lücken füllen (nur doppelte Namen vermeiden)
                var extra = new List<MoveDef>(ro); extra.AddRange(el);
                foreach (var r in byRole.Values) extra.AddRange(r);
                foreach (var m in extra)
                {
                    if (list.Count >= 4) break;
                    bool dup = false;
                    foreach (var x in list) if (x.move.name == m.name || x.move.kind == m.kind) dup = true;
                    if (!dup) list.Add(new PoolMove { move = m, unlock = Unlock[list.Count] });
                }
            }
            var arr = list.ToArray();
            pools[d.id] = arr;
            return arr;
        }

        public static bool IsUnlocked(CreatureDef d, int index, int level)
        {
            var p = PoolFor(d);
            return index >= 0 && index < p.Length && level >= p[index].unlock;
        }

        /// <summary>Rang I–V: steigt alle 7 Stufen nach der Freischaltung.</summary>
        public static int Rank(int level, int unlock) =>
            level < unlock ? 0 : MathK.Clamp(1 + (level - unlock) / LevelsPerRank, 1, MaxRank);

        public static float RankMul(int rank) => 1f + 0.1f * (Math.Max(1, rank) - 1);

        /// <summary>Attacke mit Rang-Bonus als Kampf-Fähigkeit.</summary>
        public static SkillDef Scaled(MoveDef m, int rank)
        {
            int dur = m.duration > 0 && rank >= MaxRank ? m.duration + 1 : m.duration;
            return new SkillDef(m.name, m.kind, m.power * RankMul(rank), dur, m.text);
        }

        /// <summary>Welche zwei Attacken die Einheit nutzt. Ungültige/leere Wahl → Markenattacke + stärkste freie.</summary>
        public static int[] Equipped(CreatureDef d, UnitSpec u)
        {
            var pool = PoolFor(d);
            var res = new List<int>();
            foreach (int i in new[] { u.move1, u.move2 })
                if (IsUnlocked(d, i, u.level) && !res.Contains(i)) res.Add(i);
            if (res.Count == 0)
            {
                res.Add(0);
                for (int i = pool.Length - 1; i >= 1; i--)
                    if (IsUnlocked(d, i, u.level)) { res.Add(i); break; }
            }
            return res.ToArray();
        }

        public static string RankName(int rank) => rank <= 0 ? "–" : new[] { "I", "II", "III", "IV", "V" }[MathK.Clamp(rank, 1, 5) - 1];
    }
}
