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
        Freeze,        // Schlag + Frost (Ziel wird langsamer)
        Poison,        // Giftwolke: alle Gegner erhalten Giftstapel
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
            Add("krebs", new[] { "Kneifkrebs", "Panzerkrebs" }, "Gen_Kneifkrebs", Element.Wasser, Rarity.Gewoehnlich, Role.Tank,
                1150, 62, 64, 80, 0.05f, false,
                new SkillDef("Muschelwall", SkillKind.Taunt, 0.9f, 3, "Erhält einen Schild und zieht Angriffe auf sich."),
                Passive.Dornen, "Zwickt alles, was zu nah kommt. Lebt in Bachläufen.");
            Add("biene", new[] { "Summbiene", "Stachelkönigin" }, "Gen_Summbiene", Element.Luft, Rarity.Gewoehnlich, Role.Angreifer,
                700, 118, 26, 125, 0.15f, true,
                new SkillDef("Stachelhagel", SkillKind.BackLine, 1.3f, 0, "Trifft die hintere Reihe der Gegner."),
                Passive.Ausweichen, "Schneller als sie aussieht – und viel lauter.");
            Add("moos", new[] { "Moosmampf", "Hainriese" }, "Gen_Moosmampf", Element.Natur, Rarity.Gewoehnlich, Role.Tank,
                1250, 58, 52, 72, 0.05f, false,
                new SkillDef("Rankengriff", SkillKind.Stun, 1.0f, 1, "Ranken packen ein Ziel und betäuben es."),
                Passive.Regeneration, "Schläft am liebsten. Wächst dabei weiter.");
            Add("knochen", new[] { "Knochenkopf", "Schädelfürst" }, "Gen_Knochenkopf", Element.Schatten, Rarity.Gewoehnlich, Role.Angreifer,
                760, 112, 30, 104, 0.12f, false,
                new SkillDef("Dunkler Biss", SkillKind.Execute, 1.6f, 0, "Doppelter Schaden gegen angeschlagene Gegner."),
                Passive.Todesexplosion, "Klappert nachts durch alte Ruinen.");

            // ---------- Ungewöhnlich ----------
            Add("frostgeist", new[] { "Frostgeist", "Gletscherseele" }, "Gen_Frostgeist", Element.Eis, Rarity.Ungewoehnlich, Role.Support,
                820, 88, 36, 112, 0.08f, true,
                new SkillDef("Eisnebel", SkillKind.DebuffDefense, 0.3f, 3, "Senkt die Verteidigung aller Gegner um 30 %."),
                Passive.Ausweichen, "Wo er schwebt, gefriert der Atem.");
            Add("hirsch", new[] { "Waldhirsch", "Hainwächter" }, "Gen_Waldhirsch", Element.Natur, Rarity.Ungewoehnlich, Role.Heiler,
                780, 80, 38, 102, 0.05f, true,
                new SkillDef("Lichtung", SkillKind.HealAll, 1.1f, 0, "Heilt alle Verbündeten."),
                Passive.Regeneration, "Sein Geweih blüht im Frühling.");
            Add("glotz", new[] { "Glotzauge", "Allseher" }, "Gen_Glotzauge", Element.Arkan, Rarity.Ungewoehnlich, Role.Support,
                860, 90, 40, 98, 0.1f, true,
                new SkillDef("Arkanblick", SkillKind.Chain, 1.2f, 0, "Ein Strahl springt auf bis zu drei Gegner."),
                Passive.Elementkraft, "Sieht alles. Versteht wenig.");
            Add("pieksi", new[] { "Pieksi", "Dornfels" }, "Gen_Pieksi", Element.Erde, Rarity.Ungewoehnlich, Role.Tank,
                1180, 66, 60, 78, 0.05f, false,
                new SkillDef("Stachelpanzer", SkillKind.ShieldAll, 0.6f, 3, "Gibt allen Verbündeten einen Schild."),
                Passive.Dornen, "Umarmungen nur auf eigene Gefahr.");

            // ---------- Selten ----------
            Add("glut", new[] { "Glutteufel", "Flammenherzog" }, "Gen_Glutteufel", Element.Feuer, Rarity.Selten, Role.Angreifer,
                800, 128, 32, 108, 0.14f, false,
                new SkillDef("Feuersturm", SkillKind.Burn, 1.4f, 3, "Starker Feuerschlag, Ziel brennt 3 Aktionen lang."),
                Passive.Wut, "Kocht schneller über als jeder Kessel.");
            Add("zapf", new[] { "Zapfzahn", "Sturmrufer" }, "Gen_Zapfzahn", Element.Blitz, Rarity.Selten, Role.Angreifer,
                740, 122, 30, 120, 0.18f, true,
                new SkillDef("Kettenblitz", SkillKind.Chain, 1.4f, 0, "Ein Blitz springt auf bis zu drei Gegner."),
                Passive.Kritmeister, "Funkt bei jeder Berührung.");
            Add("gift", new[] { "Giftteufel", "Seuchenbaron" }, "Gen_Giftteufel", Element.Schatten, Rarity.Selten, Role.Support,
                840, 96, 38, 106, 0.1f, true,
                new SkillDef("Dunkle Explosion", SkillKind.AllEnemies, 0.8f, 0, "Eine Schattenexplosion trifft alle Gegner."),
                Passive.Lebensraub, "Riecht nach Sumpf und Ärger.");

            // ---------- Episch ----------
            Add("yeti", new[] { "Schneestampfer", "Lawinenkönig" }, "Gen_Schneestampfer", Element.Eis, Rarity.Episch, Role.Tank,
                1400, 80, 66, 76, 0.08f, false,
                new SkillDef("Lawine", SkillKind.Cleave, 1.3f, 1, "Trifft das Ziel und seine Nachbarn."),
                Passive.Startschild, "Ein Schritt, und der Boden bebt.");
            Add("panda", new[] { "Bambusbrecher", "Bergmeister" }, "Gen_Bambusbrecher", Element.Erde, Rarity.Episch, Role.Angreifer,
                980, 132, 44, 96, 0.15f, false,
                new SkillDef("Erdspalter", SkillKind.Stun, 1.7f, 1, "Gewaltiger Hieb, betäubt das Ziel."),
                Passive.Wut, "Gemütlich, bis jemand sein Essen anrührt.");
            Add("stern", new[] { "Sternenwandler", "Himmelsweber" }, "Gen_Sternenwandler", Element.Licht, Rarity.Episch, Role.Heiler,
                880, 92, 40, 110, 0.08f, true,
                new SkillDef("Sternensegen", SkillKind.HealLowest, 2.4f, 0, "Heilt den schwächsten Verbündeten stark."),
                Passive.Startschild, "Kam mit einer Sternschnuppe.");

            // ---------- Legendär ----------
            Add("donner", new[] { "Donnerdrache", "Sturmtyrann" }, "Gen_Donnerdrache", Element.Blitz, Rarity.Legendaer, Role.Angreifer,
                1100, 140, 46, 112, 0.18f, true,
                new SkillDef("Himmelszorn", SkillKind.AllEnemies, 1.1f, 0, "Blitze schlagen in alle Gegner ein."),
                Passive.Elementkraft, "Sein Brüllen ist der Donner selbst.");

            // ---------- Mythisch ----------
            Add("sonne", new[] { "Sonnendrache", "Sonnengott" }, "Gen_Sonnendrache", Element.Licht, Rarity.Mythisch, Role.Support,
                1200, 130, 50, 108, 0.12f, true,
                new SkillDef("Morgenröte", SkillKind.BuffAttack, 0.35f, 3, "Alle Verbündeten erhalten +35 % Angriff."),
                Passive.Regeneration, "Wenn er erwacht, geht die Sonne auf.");

            // ---------- Weitere Kreaturen ----------
            Add("kueken", new[] { "Glutküken", "Phönixhahn" }, "Gen_Glutkueken", Element.Feuer, Rarity.Gewoehnlich, Role.Angreifer,
                680, 116, 26, 112, 0.12f, false,
                new SkillDef("Funkenpicker", SkillKind.Burn, 1.2f, 2, "Pickt mit glühendem Schnabel, das Ziel brennt."),
                Passive.Wut, "Klein, laut und immer kurz vorm Explodieren.");
            Add("pilz", new[] { "Pilzling", "Sporenkönig" }, "Gen_Pilzling", Element.Natur, Rarity.Gewoehnlich, Role.Heiler,
                720, 78, 34, 98, 0.05f, true,
                new SkillDef("Heilsporen", SkillKind.HealLowest, 2.0f, 0, "Heilsporen umhüllen den schwächsten Verbündeten."),
                Passive.Regeneration, "Wächst über Nacht – und heilt dabei andere.");
            Add("schwein", new[] { "Schlammschwein", "Erdkoloss" }, "Gen_Schlammschwein", Element.Erde, Rarity.Gewoehnlich, Role.Tank,
                1200, 60, 58, 74, 0.05f, false,
                new SkillDef("Schlammbad", SkillKind.Taunt, 1.0f, 3, "Suhlt sich im Schlamm: Schild und provozieren."),
                Passive.Startschild, "Je dreckiger, desto zäher.");
            Add("pinguin", new[] { "Frostpinguin", "Eiskaiser" }, "Gen_Frostpinguin", Element.Eis, Rarity.Ungewoehnlich, Role.Angreifer,
                760, 110, 34, 108, 0.12f, false,
                new SkillDef("Bauchrutscher", SkillKind.Stun, 1.4f, 1, "Rutscht ins Ziel und betäubt es."),
                Passive.Ausweichen, "Rutscht schneller als er läuft.");
            Add("fledermaus", new[] { "Flatterfledermaus", "Nachtschwinge" }, "Gen_Flatterfledermaus", Element.Luft, Rarity.Ungewoehnlich, Role.Angreifer,
                700, 112, 28, 122, 0.14f, true,
                new SkillDef("Blutbiss", SkillKind.Drain, 1.5f, 0, "Beißt zu und heilt sich dabei."),
                Passive.Lebensraub, "Hängt tagsüber kopfüber in Höhlen.");
            Add("gerippe", new[] { "Gerippe", "Knochenritter" }, "Gen_Gerippe", Element.Schatten, Rarity.Selten, Role.Angreifer,
                820, 126, 36, 102, 0.16f, false,
                new SkillDef("Rostklinge", SkillKind.Cleave, 1.3f, 0, "Weiter Schwung trifft das Ziel und seine Nachbarn."),
                Passive.Kritmeister, "Kämpft seit tausend Jahren. Hat nie verloren, sagt er.");
            Add("schleim", new[] { "Blubberschleim", "Ozeanschleim" }, "Gen_Blubberschleim", Element.Wasser, Rarity.Selten, Role.Support,
                980, 84, 46, 94, 0.06f, true,
                new SkillDef("Schleimhülle", SkillKind.ShieldAll, 0.7f, 0, "Hüllt alle Verbündeten in einen schützenden Schleim."),
                Passive.Regeneration, "Teilt sich, wenn er sich freut.");
            Add("schlund", new[] { "Tiefenschlund", "Abgrundfürst" }, "Gen_Tiefenschlund", Element.Wasser, Rarity.Legendaer, Role.Support,
                1150, 128, 48, 100, 0.12f, true,
                new SkillDef("Flutwelle", SkillKind.AllEnemies, 1.0f, 0, "Eine riesige Welle überrollt alle Gegner."),
                Passive.Elementkraft, "Schläft am Grund des Ozeans. Meistens.");

            // ---------- Neue, selbst gebaute Kreaturen (Modelle "Gen_…", erzeugt mit Tools/Creatures) ----------
            Add("glutfuchs", new[] { "Glutfuchs", "Flammenfuchs" }, "Gen_Glutfuchs", Element.Feuer, Rarity.Selten, Role.Angreifer,
                820, 130, 34, 110, 0.14f, false,
                new SkillDef("Funkenschweif", SkillKind.Burn, 1.35f, 3, "Ein glühender Schweifhieb, das Ziel brennt."),
                Passive.Ausweichen, "Seine Schwanzspitze glimmt wie ein Holzscheit im Kamin.");
            Add("lavaeber", new[] { "Lavaeber", "Magmakeiler" }, "Gen_Lavaeber", Element.Feuer, Rarity.Ungewoehnlich, Role.Tank,
                1250, 67, 61, 76, 0.05f, false,
                new SkillDef("Glutansturm", SkillKind.Taunt, 1.00f, 3, "Stürmt vor, bekommt einen Schild und zieht Angriffe auf sich."),
                Passive.Dornen, "Unter seinen Borsten brodelt flüssiges Gestein.");
            Add("aschekobold", new[] { "Aschekobold", "Feuerwichtel" }, "Gen_Aschekobold", Element.Feuer, Rarity.Gewoehnlich, Role.Angreifer,
                760, 120, 32, 110, 0.14f, false,
                new SkillDef("Glutkralle", SkillKind.Strike, 1.60f, 0, "Ein schneller Hieb mit glühenden Krallen."),
                Passive.Wut, "Klein, frech und immer auf der Suche nach etwas, das brennt.");
            Add("phoenixkueken", new[] { "Phönixküken", "Aschephönix" }, "Gen_Phoenixkueken", Element.Feuer, Rarity.Episch, Role.Heiler,
                890, 93, 40, 108, 0.06f, true,
                new SkillDef("Wiedergeburt", SkillKind.HealAll, 1.20f, 0, "Warme Federn heilen alle Verbündeten."),
                Passive.Regeneration, "Wo es landet, wachsen am nächsten Morgen Blumen.");
            Add("lavadrache", new[] { "Lavadrache", "Vulkanwyrm" }, "Gen_Lavadrache", Element.Feuer, Rarity.Mythisch, Role.Angreifer,
                1010, 159, 37, 114, 0.14f, true,
                new SkillDef("Vulkanatem", SkillKind.AllEnemies, 1.15f, 0, "Feuriger Atem trifft alle Gegner."),
                Passive.Elementkraft, "Schläft im Krater und träumt von Funkenregen.");
            Add("wellenotter", new[] { "Wellenotter", "Gezeitenotter" }, "Gen_Wellenotter", Element.Wasser, Rarity.Gewoehnlich, Role.Angreifer,
                760, 120, 32, 110, 0.14f, false,
                new SkillDef("Wasserpeitsche", SkillKind.Cleave, 1.25f, 0, "Ein Schwanzschlag trifft das Ziel und seine Nachbarn."),
                Passive.Ausweichen, "Schwimmt lieber rückwärts – einfach, weil es geht.");
            Add("nebelqualle", new[] { "Nebelqualle", "Tiefseeschimmer" }, "Gen_Nebelqualle", Element.Wasser, Rarity.Ungewoehnlich, Role.Heiler,
                810, 85, 38, 108, 0.06f, true,
                new SkillDef("Gezeitensegen", SkillKind.HealLowest, 2.20f, 0, "Heilt den schwächsten Verbündeten stark."),
                Passive.Regeneration, "Leuchtet nachts in allen Farben des Meeres.");
            Add("panzerkroete", new[] { "Panzerkröte", "Gezeitenkoloss" }, "Gen_Panzerkroete", Element.Wasser, Rarity.Episch, Role.Tank,
                1440, 73, 68, 76, 0.05f, false,
                new SkillDef("Muschelburg", SkillKind.ShieldAll, 0.75f, 0, "Ein Wasserpanzer schützt alle Verbündeten."),
                Passive.Startschild, "Trägt sein Zuhause auf dem Rücken – und das ganze Team gleich mit.");
            Add("blatthase", new[] { "Blatthase", "Blütenhase" }, "Gen_Blatthase", Element.Natur, Rarity.Gewoehnlich, Role.Heiler,
                780, 82, 37, 108, 0.06f, true,
                new SkillDef("Kleeregen", SkillKind.HealAll, 1.00f, 0, "Kleeblätter regnen auf alle Verbündeten und heilen sie."),
                Passive.Regeneration, "Wo er hoppelt, sprießt sofort frischer Klee.");
            Add("moosbaer", new[] { "Moosbär", "Waldkoloss" }, "Gen_Moosbaer", Element.Natur, Rarity.Selten, Role.Tank,
                1300, 69, 63, 76, 0.05f, false,
                new SkillDef("Wurzelgriff", SkillKind.Stun, 1.10f, 1, "Wurzeln packen ein Ziel und betäuben es."),
                Passive.Regeneration, "Auf seinem Rücken nisten Vögel – er hat nichts dagegen.");
            Add("dornhirsch", new[] { "Dornhirsch", "Waldfürst" }, "Gen_Dornhirsch", Element.Natur, Rarity.Episch, Role.Support,
                1000, 109, 43, 108, 0.09f, true,
                new SkillDef("Dornenkrone", SkillKind.DebuffDefense, 0.35f, 3, "Dornen senken die Verteidigung aller Gegner um 35 %."),
                Passive.Elementkraft, "Sein Geweih trägt jeden Frühling neue Blüten.");
            Add("felsdachs", new[] { "Felsdachs", "Bergbrecher" }, "Gen_Felsdachs", Element.Erde, Rarity.Gewoehnlich, Role.Tank,
                1200, 64, 60, 76, 0.05f, false,
                new SkillDef("Grabwall", SkillKind.Taunt, 0.90f, 3, "Gräbt sich ein: Schild und provozieren."),
                Passive.Startschild, "Gräbt sich durch Fels, als wäre es Pudding.");
            Add("sandgecko", new[] { "Sandgecko", "Wüstenwächter" }, "Gen_Sandgecko", Element.Erde, Rarity.Ungewoehnlich, Role.Angreifer,
                790, 125, 33, 110, 0.14f, false,
                new SkillDef("Sandsturm", SkillKind.BackLine, 1.25f, 0, "Wirbelt Sand auf die hintere Reihe der Gegner."),
                Passive.Ausweichen, "Kann tagelang regungslos in der Sonne liegen.");
            Add("steingolem", new[] { "Steingolem", "Bergtitan" }, "Gen_Steingolem", Element.Erde, Rarity.Legendaer, Role.Tank,
                1580, 73, 74, 76, 0.05f, false,
                new SkillDef("Erdbeben", SkillKind.AllEnemies, 0.95f, 0, "Ein Stampfer erschüttert alle Gegner."),
                Passive.Dornen, "Wurde aus einem ganzen Hügel geformt – und ist noch nicht fertig.");
            Add("windfalke", new[] { "Windfalke", "Sturmschwinge" }, "Gen_Windfalke", Element.Luft, Rarity.Ungewoehnlich, Role.Angreifer,
                790, 125, 33, 114, 0.14f, true,
                new SkillDef("Sturzflug", SkillKind.Execute, 1.60f, 0, "Doppelter Schaden gegen angeschlagene Gegner."),
                Passive.Kritmeister, "Sieht eine Maus aus drei Kilometern Höhe.");
            Add("wolkenschaf", new[] { "Wolkenschaf", "Gewitterwidder" }, "Gen_Wolkenschaf", Element.Luft, Rarity.Gewoehnlich, Role.Support,
                880, 96, 40, 108, 0.09f, true,
                new SkillDef("Schäfchenwolke", SkillKind.ShieldAll, 0.55f, 0, "Weiche Wolken schützen alle Verbündeten."),
                Passive.Startschild, "Hüpft am liebsten auf Wolken. Fällt dabei manchmal runter.");
            Add("sturmgreif", new[] { "Sturmgreif", "Himmelsgreif" }, "Gen_Sturmgreif", Element.Luft, Rarity.Legendaer, Role.Angreifer,
                960, 151, 36, 110, 0.14f, false,
                new SkillDef("Wirbelsturm", SkillKind.AllEnemies, 1.05f, 0, "Ein Wirbelsturm trifft alle Gegner."),
                Passive.Ausweichen, "Halb Adler, halb Löwe, ganz Wind.");
            Add("funkmaus", new[] { "Funkmaus", "Blitzratte" }, "Gen_Funkmaus", Element.Blitz, Rarity.Gewoehnlich, Role.Angreifer,
                760, 120, 32, 114, 0.14f, true,
                new SkillDef("Funkenschlag", SkillKind.Chain, 1.25f, 0, "Ein Funke springt auf bis zu drei Gegner."),
                Passive.Kritmeister, "Lädt sich auf, indem sie über Teppiche rennt.");
            Add("voltwolf", new[] { "Voltwolf", "Donnerheuler" }, "Gen_Voltwolf", Element.Blitz, Rarity.Selten, Role.Angreifer,
                820, 130, 34, 110, 0.14f, false,
                new SkillDef("Donnerbiss", SkillKind.Stun, 1.45f, 1, "Ein elektrischer Biss betäubt das Ziel."),
                Passive.Wut, "Bei Gewitter heult er mit dem Donner um die Wette.");
            Add("volttiger", new[] { "Volttiger", "Gewittertiger" }, "Gen_Volttiger", Element.Blitz, Rarity.Episch, Role.Angreifer,
                870, 137, 35, 110, 0.14f, false,
                new SkillDef("Blitzklaue", SkillKind.Strike, 2.00f, 0, "Ein gewaltiger, elektrisch geladener Hieb."),
                Passive.Kritmeister, "Seine Streifen leuchten, kurz bevor er zuschlägt.");
            Add("frostwolf", new[] { "Frostwolf", "Gletscherwolf" }, "Gen_Frostwolf", Element.Eis, Rarity.Selten, Role.Angreifer,
                820, 130, 34, 110, 0.14f, false,
                new SkillDef("Frostbiss", SkillKind.Stun, 1.40f, 1, "Ein eisiger Biss lässt das Ziel erstarren."),
                Passive.Elementkraft, "Sein Atem lässt Pfützen in Sekunden gefrieren.");
            Add("schneeeule", new[] { "Schneeeule", "Polarkauz" }, "Gen_Schneeeule", Element.Eis, Rarity.Ungewoehnlich, Role.Support,
                920, 100, 41, 108, 0.09f, true,
                new SkillDef("Schneegestöber", SkillKind.DebuffDefense, 0.30f, 3, "Senkt die Verteidigung aller Gegner um 30 %."),
                Passive.Ausweichen, "Ihre Federn knistern leise, wenn es schneit.");
            Add("eisbaer", new[] { "Eisbär", "Gletscherkönig" }, "Gen_Eisbaer", Element.Eis, Rarity.Episch, Role.Tank,
                1440, 73, 68, 76, 0.05f, false,
                new SkillDef("Gletscherwall", SkillKind.Taunt, 1.10f, 3, "Ein Eispanzer und provozierendes Brüllen."),
                Passive.Startschild, "Schläft im Winter – und der Winter ist bei ihm lang.");
            Add("lumifuchs", new[] { "Lumifuchs", "Sonnenfuchs" }, "Gen_Lumifuchs", Element.Licht, Rarity.Episch, Role.Heiler,
                890, 93, 40, 108, 0.06f, true,
                new SkillDef("Morgenglanz", SkillKind.HealAll, 1.25f, 0, "Sanftes Licht heilt alle Verbündeten."),
                Passive.Regeneration, "Leuchtet sanft, wenn jemand in seiner Nähe traurig ist.");
            Add("sternenkitz", new[] { "Sternenkitz", "Himmelshirsch" }, "Gen_Sternenkitz", Element.Licht, Rarity.Selten, Role.Support,
                950, 104, 42, 108, 0.09f, true,
                new SkillDef("Sternenstaub", SkillKind.BuffAttack, 0.30f, 3, "Alle Verbündeten erhalten +30 % Angriff."),
                Passive.Startschild, "Seine Flecken sind Sternbilder, die noch niemand kennt.");
            Add("glanzfalter", new[] { "Glanzfalter", "Lichtschwinge" }, "Gen_Glanzfalter", Element.Licht, Rarity.Ungewoehnlich, Role.Heiler,
                810, 85, 38, 108, 0.06f, true,
                new SkillDef("Lichtstaub", SkillKind.HealLowest, 2.10f, 0, "Leuchtender Staub heilt den schwächsten Verbündeten."),
                Passive.Ausweichen, "Folgt Wanderern, die sich im Wald verlaufen haben.");
            Add("schattenkatze", new[] { "Schattenkatze", "Nachtpanther" }, "Gen_Schattenkatze", Element.Schatten, Rarity.Ungewoehnlich, Role.Angreifer,
                790, 125, 33, 110, 0.14f, false,
                new SkillDef("Schattensprung", SkillKind.Execute, 1.55f, 0, "Springt aus dem Schatten – doppelter Schaden gegen Angeschlagene."),
                Passive.Ausweichen, "Ist immer genau dort, wo du nicht hinschaust.");
            Add("nachtgeist", new[] { "Nachtgeist", "Albtraumschleier" }, "Gen_Nachtgeist", Element.Schatten, Rarity.Gewoehnlich, Role.Support,
                880, 96, 40, 108, 0.09f, true,
                new SkillDef("Albdruck", SkillKind.DebuffDefense, 0.25f, 3, "Senkt die Verteidigung aller Gegner um 25 %."),
                Passive.Lebensraub, "Kichert leise unter dem Bett. Meistens harmlos.");
            Add("schattendrache", new[] { "Schattendrache", "Finsterwyrm" }, "Gen_Schattendrache", Element.Schatten, Rarity.Legendaer, Role.Angreifer,
                960, 151, 36, 114, 0.14f, true,
                new SkillDef("Finsternis", SkillKind.AllEnemies, 1.10f, 0, "Dunkle Flammen treffen alle Gegner."),
                Passive.Lebensraub, "Verschluckt das Mondlicht, wenn er wütend ist.");
            Add("runenfuchs", new[] { "Runenfuchs", "Arkanfuchs" }, "Gen_Runenfuchs", Element.Arkan, Rarity.Selten, Role.Support,
                950, 104, 42, 108, 0.09f, true,
                new SkillDef("Runenkreis", SkillKind.BuffAttack, 0.30f, 3, "Leuchtende Runen geben allen Verbündeten +30 % Angriff."),
                Passive.Elementkraft, "Die Muster in seinem Fell ändern sich mit jedem Vollmond.");
            Add("arkanwichtel", new[] { "Arkanwichtel", "Runenmagier" }, "Gen_Arkanwichtel", Element.Arkan, Rarity.Gewoehnlich, Role.Heiler,
                780, 82, 37, 108, 0.06f, true,
                new SkillDef("Heilrune", SkillKind.HealLowest, 2.00f, 0, "Eine Rune heilt den schwächsten Verbündeten."),
                Passive.Regeneration, "Kritzelt Zaubersprüche in den Sand und vergisst sie sofort.");
            Add("zauberkauz", new[] { "Zauberkauz", "Sternenkauz" }, "Gen_Zauberkauz", Element.Arkan, Rarity.Episch, Role.Support,
                1000, 109, 43, 108, 0.09f, true,
                new SkillDef("Sternenschauer", SkillKind.Chain, 1.35f, 0, "Ein Sternenstrahl springt auf bis zu drei Gegner."),
                Passive.Elementkraft, "Liest nachts in den Sternen – und schläft tagsüber in Büchern.");
            Add("sternendrache", new[] { "Sternendrache", "Kosmoswyrm" }, "Gen_Sternendrache", Element.Arkan, Rarity.Mythisch, Role.Support,
                1160, 121, 49, 108, 0.09f, true,
                new SkillDef("Kosmosflut", SkillKind.AllEnemies, 1.00f, 0, "Ein Sternenregen trifft alle Gegner."),
                Passive.Regeneration, "Trägt einen ganzen Sternenhimmel auf seinen Schuppen.");
            Add("blitzdrache", new[] { "Blitzdrachling", "Sturmdrache" }, "Gen_Blitzdrache", Element.Blitz, Rarity.Legendaer, Role.Angreifer,
                910, 144, 36, 114, 0.14f, true,
                new SkillDef("Gewitterfront", SkillKind.Chain, 1.55f, 0, "Eine Gewitterfront springt auf bis zu drei Gegner."),
                Passive.Kritmeister, "Lädt sich in Gewitterwolken auf, bis seine Schuppen knistern.");
            Add("frostdrache", new[] { "Frostdrachling", "Eiswyrm" }, "Gen_Frostdrache", Element.Eis, Rarity.Legendaer, Role.Support,
                1060, 115, 45, 108, 0.09f, true,
                new SkillDef("Eissturm", SkillKind.DebuffDefense, 0.40f, 3, "Ein Eissturm senkt die Verteidigung aller Gegner um 40 %."),
                Passive.Startschild, "Wo er schläft, wachsen Eisblumen an den Fenstern.");

            // ---------- Bosse (nur in der Kampagne) ----------
            AddBoss("boss_hain", "Uralter Hainriese", "Gen_Moosmampf", Element.Natur, 3600, 105, 60, 80,
                new SkillDef("Wurzelbeben", SkillKind.AllEnemies, 1.0f, 0, "Wurzeln brechen unter allen Gegnern hervor."),
                Passive.Regeneration, "Wächter der grünen Ebenen.");
            AddBoss("boss_glut", "Vulkanherzog", "Gen_Glutteufel", Element.Feuer, 3800, 125, 62, 92,
                new SkillDef("Lavasturm", SkillKind.Burn, 1.6f, 3, "Lava verbrennt ein Ziel."),
                Passive.Wut, "Herr der Vulkanregion.");
            AddBoss("boss_frost", "Frostkönig", "Gen_Schneestampfer", Element.Eis, 4600, 135, 75, 88,
                new SkillDef("Eissturm", SkillKind.AllEnemies, 1.05f, 0, "Ein Eissturm fegt über alle Gegner."),
                Passive.Startschild, "Herrscher über Gletscher und Schnee.");
            AddBoss("boss_schatten", "Schattenfürst", "Gen_Tiefenschlund", Element.Schatten, 4900, 140, 68, 96,
                new SkillDef("Dunkle Flut", SkillKind.Drain, 2.2f, 0, "Verschlingt die Lebenskraft eines Gegners."),
                Passive.Lebensraub, "Was in der Tiefe wohnt, kennt kein Licht.");
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
