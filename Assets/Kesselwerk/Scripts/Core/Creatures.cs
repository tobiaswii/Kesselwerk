using System.Collections.Generic;

namespace Kesselwerk.Core
{
    /// <summary>Art der Spezialfähigkeit (wird ausgelöst, sobald die Energie voll ist).</summary>
    public enum SkillKind
    {
        Strike,        // starker Einzelschlag
        Cleave,        // Ziel und benachbarte Gegner
        AllEnemies,    // trifft alle Gegner
        BackLine,      // trifft die hintere Reihe (oder alle, falls leer)
        Chain,         // springt auf bis zu 3 Gegner, wird schwächer
        Burn,          // Schlag + Brennen
        Stun,          // Schlag + Betäubung
        Execute,       // doppelter Schaden gegen angeschlagene Gegner
        Drain,         // Schlag + heilt sich selbst
        HealLowest,    // heilt den schwächsten Verbündeten
        HealAll,       // heilt alle Verbündeten
        ShieldAll,     // Schild für alle Verbündeten
        Taunt,         // Schild für sich + zieht Angriffe auf sich
        BuffAttack,    // Angriff aller Verbündeten erhöht
        DebuffDefense, // Verteidigung aller Gegner gesenkt
    }

    /// <summary>Passive Eigenschaft, immer aktiv.</summary>
    public enum Passive
    {
        Keine,
        Dornen,          // wirft 20 % des Nahkampfschadens zurück
        Lebensraub,      // heilt sich um 20 % des verursachten Schadens
        Regeneration,    // heilt 5 % Max-Leben bei jeder eigenen Aktion
        Wut,             // +8 % Angriff pro erlittenem Treffer (max. 5)
        Startschild,     // beginnt mit Schild (25 % Max-Leben)
        Todesexplosion,  // trifft beim Tod alle Gegner
        Kritmeister,     // +20 % Kritchance, Kritschaden x2
        Ausweichen,      // weicht 15 % der Angriffe aus
        Elementkraft,    // +15 % Schaden mit Elementvorteil
    }

    public sealed class SkillDef
    {
        public string name;
        public SkillKind kind;
        public float power;      // Schadens-/Heilfaktor bezogen auf Angriff
        public int duration;     // Dauer in Aktionen für Zustände
        public string text;      // Beschreibung für Karte

        public SkillDef(string name, SkillKind kind, float power, int duration, string text)
        {
            this.name = name; this.kind = kind; this.power = power; this.duration = duration; this.text = text;
        }

        public bool TargetsAllies =>
            kind == SkillKind.HealLowest || kind == SkillKind.HealAll || kind == SkillKind.ShieldAll ||
            kind == SkillKind.Taunt || kind == SkillKind.BuffAttack;
    }

    /// <summary>Unveränderliche Beschreibung einer Kreatur (datengetrieben, leicht erweiterbar).</summary>
    public sealed class CreatureDef
    {
        public string id;
        public string[] stageNames;   // Name je Entwicklungsstufe
        public string description;
        public string model;          // Name des 3D-Modells (Resources/Monsters)
        public Element element;
        public Rarity rarity;
        public Role role;
        public int hp, atk, def, spd; // Grundwerte auf Stufe 1
        public float crit;            // Kritchance 0..1
        public bool ranged;           // Fernkampf: kann jede Reihe angreifen
        public SkillDef skill;
        public Passive passive;
        public bool isBoss;

        public string Name => stageNames[0];
        public string StageName(int stage) => stageNames[MathK.Clamp(stage, 0, stageNames.Length - 1)];
        public int MaxStage => stageNames.Length - 1;

        public static string PassiveText(Passive p)
        {
            switch (p)
            {
                case Passive.Dornen: return "Dornen: wirft 20 % des Nahkampfschadens zurück.";
                case Passive.Lebensraub: return "Lebensraub: heilt sich um 20 % des Schadens.";
                case Passive.Regeneration: return "Regeneration: heilt 5 % Leben pro Aktion.";
                case Passive.Wut: return "Wut: +8 % Angriff pro erlittenem Treffer (max. 5).";
                case Passive.Startschild: return "Startschild: beginnt mit einem Schild.";
                case Passive.Todesexplosion: return "Todesexplosion: trifft beim Tod alle Gegner.";
                case Passive.Kritmeister: return "Kritmeister: +20 % Kritchance, doppelter Kritschaden.";
                case Passive.Ausweichen: return "Ausweichen: weicht 15 % der Angriffe aus.";
                case Passive.Elementkraft: return "Elementkraft: +15 % Schaden mit Elementvorteil.";
                default: return "";
            }
        }
    }

    /// <summary>Alle Kreaturen. Neue Kreatur = neuer Eintrag hier + Modell in Resources/Monsters.</summary>
    public static class CreatureDb
    {
        static List<CreatureDef> all;
        static Dictionary<string, CreatureDef> byId;

        public static IReadOnlyList<CreatureDef> All { get { Ensure(); return all; } }

        public static CreatureDef Get(string id)
        {
            Ensure();
            return id != null && byId.TryGetValue(id, out var d) ? d : null;
        }

        public static List<CreatureDef> OfRarity(Rarity r, bool includeBosses = false)
        {
            Ensure();
            var list = new List<CreatureDef>();
            foreach (var d in all) if (d.rarity == r && (includeBosses || !d.isBoss)) list.Add(d);
            return list;
        }

        static void Ensure()
        {
            if (all != null) return;
            all = new List<CreatureDef>();
            byId = new Dictionary<string, CreatureDef>();

            // ---------- Gewöhnlich ----------
            Add("krebs", new[] { "Kneifkrebs", "Panzerkrebs" }, "Crab", Element.Wasser, Rarity.Gewoehnlich, Role.Tank,
                1150, 62, 64, 80, 0.05f, false,
                new SkillDef("Muschelwall", SkillKind.Taunt, 0.9f, 3, "Erhält einen Schild und zieht Angriffe auf sich."),
                Passive.Dornen, "Zwickt alles, was zu nah kommt. Lebt in Bachläufen.");
            Add("biene", new[] { "Summbiene", "Stachelkönigin" }, "Bee", Element.Luft, Rarity.Gewoehnlich, Role.Angreifer,
                700, 118, 26, 125, 0.15f, true,
                new SkillDef("Stachelhagel", SkillKind.BackLine, 1.3f, 0, "Trifft die hintere Reihe der Gegner."),
                Passive.Ausweichen, "Schneller als sie aussieht – und viel lauter.");
            Add("moos", new[] { "Moosmampf", "Hainriese" }, "Tree", Element.Natur, Rarity.Gewoehnlich, Role.Tank,
                1250, 58, 52, 72, 0.05f, false,
                new SkillDef("Rankengriff", SkillKind.Stun, 1.0f, 1, "Ranken packen ein Ziel und betäuben es."),
                Passive.Regeneration, "Schläft am liebsten. Wächst dabei weiter.");
            Add("knochen", new[] { "Knochenkopf", "Schädelfürst" }, "Skull", Element.Schatten, Rarity.Gewoehnlich, Role.Angreifer,
                760, 112, 30, 104, 0.12f, false,
                new SkillDef("Dunkler Biss", SkillKind.Execute, 1.6f, 0, "Doppelter Schaden gegen angeschlagene Gegner."),
                Passive.Todesexplosion, "Klappert nachts durch alte Ruinen.");

            // ---------- Ungewöhnlich ----------
            Add("frostgeist", new[] { "Frostgeist", "Gletscherseele" }, "Ghost", Element.Eis, Rarity.Ungewoehnlich, Role.Support,
                820, 88, 36, 112, 0.08f, true,
                new SkillDef("Eisnebel", SkillKind.DebuffDefense, 0.3f, 3, "Senkt die Verteidigung aller Gegner um 30 %."),
                Passive.Ausweichen, "Wo er schwebt, gefriert der Atem.");
            Add("hirsch", new[] { "Waldhirsch", "Hainwächter" }, "Deer", Element.Natur, Rarity.Ungewoehnlich, Role.Heiler,
                780, 80, 38, 102, 0.05f, true,
                new SkillDef("Lichtung", SkillKind.HealAll, 1.1f, 0, "Heilt alle Verbündeten."),
                Passive.Regeneration, "Sein Geweih blüht im Frühling.");
            Add("glotz", new[] { "Glotzauge", "Allseher" }, "Cyclops", Element.Arkan, Rarity.Ungewoehnlich, Role.Support,
                860, 90, 40, 98, 0.1f, true,
                new SkillDef("Arkanblick", SkillKind.Chain, 1.2f, 0, "Ein Strahl springt auf bis zu drei Gegner."),
                Passive.Elementkraft, "Sieht alles. Versteht wenig.");
            Add("pieksi", new[] { "Pieksi", "Dornfels" }, "Cactus", Element.Erde, Rarity.Ungewoehnlich, Role.Tank,
                1180, 66, 60, 78, 0.05f, false,
                new SkillDef("Stachelpanzer", SkillKind.ShieldAll, 0.6f, 3, "Gibt allen Verbündeten einen Schild."),
                Passive.Dornen, "Umarmungen nur auf eigene Gefahr.");

            // ---------- Selten ----------
            Add("glut", new[] { "Glutteufel", "Flammenherzog" }, "Demon", Element.Feuer, Rarity.Selten, Role.Angreifer,
                800, 128, 32, 108, 0.14f, false,
                new SkillDef("Feuersturm", SkillKind.Burn, 1.4f, 3, "Starker Feuerschlag, Ziel brennt 3 Aktionen lang."),
                Passive.Wut, "Kocht schneller über als jeder Kessel.");
            Add("zapf", new[] { "Zapfzahn", "Sturmrufer" }, "Alien", Element.Blitz, Rarity.Selten, Role.Angreifer,
                740, 122, 30, 120, 0.18f, true,
                new SkillDef("Kettenblitz", SkillKind.Chain, 1.4f, 0, "Ein Blitz springt auf bis zu drei Gegner."),
                Passive.Kritmeister, "Funkt bei jeder Berührung.");
            Add("gift", new[] { "Giftteufel", "Seuchenbaron" }, "GreenDemon", Element.Schatten, Rarity.Selten, Role.Support,
                840, 96, 38, 106, 0.1f, true,
                new SkillDef("Dunkle Explosion", SkillKind.AllEnemies, 0.8f, 0, "Eine Schattenexplosion trifft alle Gegner."),
                Passive.Lebensraub, "Riecht nach Sumpf und Ärger.");

            // ---------- Episch ----------
            Add("yeti", new[] { "Schneestampfer", "Lawinenkönig" }, "Yeti", Element.Eis, Rarity.Episch, Role.Tank,
                1400, 80, 66, 76, 0.08f, false,
                new SkillDef("Lawine", SkillKind.Cleave, 1.3f, 1, "Trifft das Ziel und seine Nachbarn."),
                Passive.Startschild, "Ein Schritt, und der Boden bebt.");
            Add("panda", new[] { "Bambusbrecher", "Bergmeister" }, "Panda", Element.Erde, Rarity.Episch, Role.Angreifer,
                980, 132, 44, 96, 0.15f, false,
                new SkillDef("Erdspalter", SkillKind.Stun, 1.7f, 1, "Gewaltiger Hieb, betäubt das Ziel."),
                Passive.Wut, "Gemütlich, bis jemand sein Essen anrührt.");
            Add("stern", new[] { "Sternenwandler", "Himmelsweber" }, "Alien_Tall", Element.Licht, Rarity.Episch, Role.Heiler,
                880, 92, 40, 110, 0.08f, true,
                new SkillDef("Sternensegen", SkillKind.HealLowest, 2.4f, 0, "Heilt den schwächsten Verbündeten stark."),
                Passive.Startschild, "Kam mit einer Sternschnuppe.");

            // ---------- Legendär ----------
            Add("donner", new[] { "Donnerdrache", "Sturmtyrann" }, "Dragon", Element.Blitz, Rarity.Legendaer, Role.Angreifer,
                1100, 140, 46, 112, 0.18f, true,
                new SkillDef("Himmelszorn", SkillKind.AllEnemies, 1.1f, 0, "Blitze schlagen in alle Gegner ein."),
                Passive.Elementkraft, "Sein Brüllen ist der Donner selbst.");

            // ---------- Mythisch ----------
            Add("sonne", new[] { "Sonnendrache", "Sonnengott" }, "YellowDragon", Element.Licht, Rarity.Mythisch, Role.Support,
                1200, 130, 50, 108, 0.12f, true,
                new SkillDef("Morgenröte", SkillKind.BuffAttack, 0.35f, 3, "Alle Verbündeten erhalten +35 % Angriff."),
                Passive.Regeneration, "Wenn er erwacht, geht die Sonne auf.");

            // ---------- Weitere Kreaturen ----------
            Add("kueken", new[] { "Glutküken", "Phönixhahn" }, "Chicken", Element.Feuer, Rarity.Gewoehnlich, Role.Angreifer,
                680, 116, 26, 112, 0.12f, false,
                new SkillDef("Funkenpicker", SkillKind.Burn, 1.2f, 2, "Pickt mit glühendem Schnabel, das Ziel brennt."),
                Passive.Wut, "Klein, laut und immer kurz vorm Explodieren.");
            Add("pilz", new[] { "Pilzling", "Sporenkönig" }, "Mushroom", Element.Natur, Rarity.Gewoehnlich, Role.Heiler,
                720, 78, 34, 98, 0.05f, true,
                new SkillDef("Heilsporen", SkillKind.HealLowest, 2.0f, 0, "Heilsporen umhüllen den schwächsten Verbündeten."),
                Passive.Regeneration, "Wächst über Nacht – und heilt dabei andere.");
            Add("schwein", new[] { "Schlammschwein", "Erdkoloss" }, "Pig", Element.Erde, Rarity.Gewoehnlich, Role.Tank,
                1200, 60, 58, 74, 0.05f, false,
                new SkillDef("Schlammbad", SkillKind.Taunt, 1.0f, 3, "Suhlt sich im Schlamm: Schild und provozieren."),
                Passive.Startschild, "Je dreckiger, desto zäher.");
            Add("pinguin", new[] { "Frostpinguin", "Eiskaiser" }, "Penguin", Element.Eis, Rarity.Ungewoehnlich, Role.Angreifer,
                760, 110, 34, 108, 0.12f, false,
                new SkillDef("Bauchrutscher", SkillKind.Stun, 1.4f, 1, "Rutscht ins Ziel und betäubt es."),
                Passive.Ausweichen, "Rutscht schneller als er läuft.");
            Add("fledermaus", new[] { "Flatterfledermaus", "Nachtschwinge" }, "Bat", Element.Luft, Rarity.Ungewoehnlich, Role.Angreifer,
                700, 112, 28, 122, 0.14f, true,
                new SkillDef("Blutbiss", SkillKind.Drain, 1.5f, 0, "Beißt zu und heilt sich dabei."),
                Passive.Lebensraub, "Hängt tagsüber kopfüber in Höhlen.");
            Add("gerippe", new[] { "Gerippe", "Knochenritter" }, "Skeleton", Element.Schatten, Rarity.Selten, Role.Angreifer,
                820, 126, 36, 102, 0.16f, false,
                new SkillDef("Rostklinge", SkillKind.Cleave, 1.3f, 0, "Weiter Schwung trifft das Ziel und seine Nachbarn."),
                Passive.Kritmeister, "Kämpft seit tausend Jahren. Hat nie verloren, sagt er.");
            Add("schleim", new[] { "Blubberschleim", "Ozeanschleim" }, "Slime", Element.Wasser, Rarity.Selten, Role.Support,
                980, 84, 46, 94, 0.06f, true,
                new SkillDef("Schleimhülle", SkillKind.ShieldAll, 0.7f, 0, "Hüllt alle Verbündeten in einen schützenden Schleim."),
                Passive.Regeneration, "Teilt sich, wenn er sich freut.");
            Add("schlund", new[] { "Tiefenschlund", "Abgrundfürst" }, "Cthulhu", Element.Wasser, Rarity.Legendaer, Role.Support,
                1150, 128, 48, 100, 0.12f, true,
                new SkillDef("Flutwelle", SkillKind.AllEnemies, 1.0f, 0, "Eine riesige Welle überrollt alle Gegner."),
                Passive.Elementkraft, "Schläft am Grund des Ozeans. Meistens.");

            // ---------- Bosse (nur in der Kampagne) ----------
            AddBoss("boss_hain", "Uralter Hainriese", "Tree", Element.Natur, 3600, 105, 60, 80,
                new SkillDef("Wurzelbeben", SkillKind.AllEnemies, 1.0f, 0, "Wurzeln brechen unter allen Gegnern hervor."),
                Passive.Regeneration, "Wächter der grünen Ebenen.");
            AddBoss("boss_glut", "Vulkanherzog", "Demon", Element.Feuer, 3800, 125, 62, 92,
                new SkillDef("Lavasturm", SkillKind.Burn, 1.6f, 3, "Lava verbrennt ein Ziel."),
                Passive.Wut, "Herr der Vulkanregion.");
        }

        static void Add(string id, string[] names, string model, Element el, Rarity r, Role role,
            int hp, int atk, int def, int spd, float crit, bool ranged, SkillDef skill, Passive passive, string desc)
        {
            var d = new CreatureDef
            {
                id = id, stageNames = names, model = model, element = el, rarity = r, role = role,
                hp = hp, atk = atk, def = def, spd = spd, crit = crit, ranged = ranged,
                skill = skill, passive = passive, description = desc
            };
            all.Add(d);
            byId[id] = d;
        }

        static void AddBoss(string id, string name, string model, Element el, int hp, int atk, int def, int spd,
            SkillDef skill, Passive passive, string desc)
        {
            Add(id, new[] { name }, model, el, Rarity.Legendaer, Role.Tank, hp, atk, def, spd, 0.1f, false, skill, passive, desc);
            byId[id].isBoss = true;
        }
    }
}
