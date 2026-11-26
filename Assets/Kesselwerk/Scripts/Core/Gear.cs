using System;
using System.Collections.Generic;

namespace Kesselwerk.Core
{
    /// <summary>Drei Ausrüstungsplätze je Kreatur.</summary>
    public enum GearSlot { Waffe, Ruestung, Amulett }

    /// <summary>Ausrüstungsgegenstand (datengetrieben). Werte gelten auf Stufe 1, jede weitere Stufe +25 %.</summary>
    public sealed class GearDef
    {
        public string id, name, text;
        public GearSlot slot;
        public Rarity rarity;
        public float atkPct, hpPct, defPct, crit;
        public int spd, energy;

        public static string SlotName(GearSlot s) => s == GearSlot.Waffe ? "Waffe" : s == GearSlot.Ruestung ? "Rüstung" : "Amulett";
    }

    /// <summary>Gesammelte Werte aller angelegten Gegenstände einer Kreatur.</summary>
    public struct GearBonus
    {
        public float atkPct, hpPct, defPct, crit;
        public float spd, energy;

        public string Describe()
        {
            var parts = new List<string>();
            if (atkPct > 0) parts.Add($"Angriff +{atkPct * 100f:0} %");
            if (hpPct > 0) parts.Add($"Leben +{hpPct * 100f:0} %");
            if (defPct > 0) parts.Add($"Verteidigung +{defPct * 100f:0} %");
            if (crit > 0) parts.Add($"Krit +{crit * 100f:0} %");
            if (spd > 0) parts.Add($"Tempo +{spd:0}");
            if (energy > 0) parts.Add($"Startenergie +{energy:0}");
            return string.Join(", ", parts);
        }
    }

    public static class GearDb
    {
        public const int MaxLevel = 5;
        static List<GearDef> all;
        static Dictionary<string, GearDef> byId;

        public static IReadOnlyList<GearDef> All { get { Ensure(); return all; } }

        public static GearDef Get(string id)
        {
            Ensure();
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var g) ? g : null;
        }

        public static float LevelMul(int level) => 1f + 0.25f * (MathK.Clamp(level, 1, MaxLevel) - 1);

        /// <summary>Werte eines Gegenstands auf einer Stufe.</summary>
        public static GearBonus Bonus(string id, int level)
        {
            var g = Get(id);
            var b = new GearBonus();
            if (g == null) return b;
            float m = LevelMul(level);
            b.atkPct = g.atkPct * m; b.hpPct = g.hpPct * m; b.defPct = g.defPct * m;
            b.crit = g.crit * m; b.spd = g.spd * m; b.energy = g.energy * m;
            return b;
        }

        public static GearBonus Sum(UnitSpec u)
        {
            var a = Bonus(u.weapon, u.weaponLv);
            var b = Bonus(u.armor, u.armorLv);
            var c = Bonus(u.amulet, u.amuletLv);
            return new GearBonus
            {
                atkPct = a.atkPct + b.atkPct + c.atkPct,
                hpPct = a.hpPct + b.hpPct + c.hpPct,
                defPct = a.defPct + b.defPct + c.defPct,
                crit = a.crit + b.crit + c.crit,
                spd = a.spd + b.spd + c.spd,
                energy = Math.Min(80f, a.energy + b.energy + c.energy),
            };
        }

        public static List<GearDef> OfRarity(Rarity r)
        {
            Ensure();
            var l = new List<GearDef>();
            foreach (var g in all) if (g.rarity == r) l.Add(g);
            return l;
        }

        static void Ensure()
        {
            if (all != null) return;
            all = new List<GearDef>();
            byId = new Dictionary<string, GearDef>();
            // Waffen
            Add("w_knueppel", "Holzknüppel", GearSlot.Waffe, Rarity.Gewoehnlich, atk: 0.08f, text: "Schwer, aber ehrlich.");
            Add("w_klaue", "Eisenklaue", GearSlot.Waffe, Rarity.Ungewoehnlich, atk: 0.12f, text: "Geschärft am Kesselrand.");
            Add("w_glut", "Glutklinge", GearSlot.Waffe, Rarity.Selten, atk: 0.17f, crit: 0.03f, text: "Glüht nach jedem Treffer nach.");
            Add("w_sturm", "Sturmzahn", GearSlot.Waffe, Rarity.Episch, atk: 0.22f, crit: 0.05f, text: "Knistert vor Energie.");
            Add("w_drache", "Drachenzahn", GearSlot.Waffe, Rarity.Legendaer, atk: 0.3f, crit: 0.08f, text: "Aus dem Zahn eines Donnerdrachen.");
            // Rüstungen
            Add("r_leder", "Lederwams", GearSlot.Ruestung, Rarity.Gewoehnlich, hp: 0.1f, text: "Besser als nichts.");
            Add("r_rinde", "Rindenpanzer", GearSlot.Ruestung, Rarity.Ungewoehnlich, hp: 0.13f, def: 0.08f, text: "Aus der Rinde eines Hainriesen.");
            Add("r_kristall", "Kristallschild", GearSlot.Ruestung, Rarity.Selten, hp: 0.17f, def: 0.12f, text: "Bricht das Licht – und Schläge.");
            Add("r_frost", "Frostpanzer", GearSlot.Ruestung, Rarity.Episch, hp: 0.24f, def: 0.15f, text: "Ewiges Eis vom Gletscher.");
            Add("r_titan", "Titanenhaut", GearSlot.Ruestung, Rarity.Legendaer, hp: 0.34f, def: 0.2f, text: "Unzerstörbar, sagt man.");
            // Amulette
            Add("a_feder", "Glücksfeder", GearSlot.Amulett, Rarity.Gewoehnlich, spd: 6, text: "Macht leichtfüßig.");
            Add("a_energie", "Energiestein", GearSlot.Amulett, Rarity.Ungewoehnlich, energy: 20, text: "Startet den Kampf mit Energie.");
            Add("a_auge", "Kritauge", GearSlot.Amulett, Rarity.Selten, crit: 0.1f, text: "Findet jede Schwachstelle.");
            Add("a_phoenix", "Phönixfeder", GearSlot.Amulett, Rarity.Episch, spd: 8, energy: 25, text: "Warm und voller Leben.");
            Add("a_stern", "Sternenherz", GearSlot.Amulett, Rarity.Legendaer, atk: 0.08f, hp: 0.08f, spd: 8, energy: 30, text: "Pulsiert im Takt der Sterne.");
        }

        static void Add(string id, string name, GearSlot slot, Rarity r, float atk = 0f, float hp = 0f, float def = 0f, float crit = 0f, int spd = 0, int energy = 0, string text = "")
        {
            var g = new GearDef { id = id, name = name, slot = slot, rarity = r, atkPct = atk, hpPct = hp, defPct = def, crit = crit, spd = spd, energy = energy, text = text };
            all.Add(g);
            byId[id] = g;
        }

        /// <summary>Zufällige Beute passend zur Region (spätere Regionen = seltenere Gegenstände).</summary>
        public static GearDef RollDrop(Rng rng, int region, bool boss)
        {
            float r = rng.Value() * 100f;
            // Gewichte je Region: Gewöhnlich, Ungewöhnlich, Selten, Episch, Legendär
            float[][] w =
            {
                new[] { 60f, 30f, 9f, 1f, 0f },
                new[] { 35f, 38f, 20f, 6f, 1f },
                new[] { 15f, 35f, 32f, 15f, 3f },
                new[] { 5f, 25f, 38f, 25f, 7f },
            };
            var weights = w[MathK.Clamp(region, 0, w.Length - 1)];
            int tier = 0;
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++) { acc += weights[i]; if (r < acc) { tier = i; break; } tier = i; }
            if (boss) tier = Math.Min(4, tier + 1);
            var pool = OfRarity((Rarity)tier);
            return pool[rng.Range(0, pool.Count)];
        }
    }
}
