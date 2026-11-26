using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    public enum BodyShape { Capsule, Sphere, Cube }

    /// <summary>Dauerhafte Eigenschaft eines Monsters.</summary>
    public enum Keyword
    {
        None,
        Guardian,    // Wächter: muss zuerst angegriffen werden
        FirstStrike, // Erstschlag: bekommt beim Angreifen keinen Gegenschaden
        Thorns,      // Stacheln: Angreifer erleiden 1 zusätzlichen Schaden
    }

    /// <summary>Effekt, der beim Ausspielen einmal ausgelöst wird.</summary>
    public enum OnPlayEffect
    {
        None,
        HealOwner,          // Du heilst X Leben
        DamageAllEnemies,   // X Schaden an allen gegnerischen Monstern
        DamageRandomEnemy,  // X Schaden an einem zufälligen gegnerischen Monster
        DrawCard,           // Ziehe X Karten
    }

    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>Daten einer Monster-Karte.</summary>
    [System.Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public int cost;
        public int attack;
        public int health;
        public Color color;
        public BodyShape shape;
        public string model;
        public Keyword keyword;
        public OnPlayEffect onPlay;
        public int effectValue;
        public string flavor;
        public Rarity rarity;

        public CardData(string id, string name, int cost, int attack, int health, Color color, string model,
                        Keyword keyword, OnPlayEffect onPlay, int effectValue, string flavor, Rarity rarity = Rarity.Common, BodyShape shape = BodyShape.Capsule)
        {
            this.id = id;
            this.name = name;
            this.cost = cost;
            this.attack = attack;
            this.health = health;
            this.color = color;
            this.model = model;
            this.keyword = keyword;
            this.onPlay = onPlay;
            this.effectValue = effectValue;
            this.flavor = flavor;
            this.rarity = rarity;
            this.shape = shape;
        }

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "Selten";
                case Rarity.Epic: return "Episch";
                case Rarity.Legendary: return "Legendär";
                default: return "Gewöhnlich";
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.3f, 0.6f, 1f);
                case Rarity.Epic: return new Color(0.75f, 0.35f, 1f);
                case Rarity.Legendary: return new Color(1f, 0.62f, 0.15f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        /// <summary>Wie oft darf die Karte im Deck sein?</summary>
        public int MaxCopies => rarity == Rarity.Legendary ? 1 : 3;

        /// <summary>Staub-Kosten zum Herstellen.</summary>
        public int CraftCost => rarity == Rarity.Legendary ? 1600 : rarity == Rarity.Epic ? 400 : rarity == Rarity.Rare ? 100 : 40;

        /// <summary>Staub für überzählige Kopien.</summary>
        public int DustValue => rarity == Rarity.Legendary ? 400 : rarity == Rarity.Epic ? 100 : rarity == Rarity.Rare ? 20 : 5;

        public static string KeywordName(Keyword k)
        {
            switch (k)
            {
                case Keyword.Guardian: return "Wächter";
                case Keyword.FirstStrike: return "Erstschlag";
                case Keyword.Thorns: return "Stacheln";
                default: return "";
            }
        }

        /// <summary>Regeltext für die Karte (ohne Spruch).</summary>
        public string RulesText
        {
            get
            {
                var parts = new List<string>();
                switch (keyword)
                {
                    case Keyword.Guardian: parts.Add("<b>Wächter:</b> Gegner müssen zuerst dieses Monster angreifen."); break;
                    case Keyword.FirstStrike: parts.Add("<b>Erstschlag:</b> Erleidet beim Angreifen keinen Gegenschaden."); break;
                    case Keyword.Thorns: parts.Add("<b>Stacheln:</b> Wer es angreift, erleidet 1 zusätzlichen Schaden."); break;
                }
                switch (onPlay)
                {
                    case OnPlayEffect.HealOwner: parts.Add($"<b>Beim Ausspielen:</b> Du heilst {effectValue} Leben."); break;
                    case OnPlayEffect.DamageAllEnemies: parts.Add($"<b>Beim Ausspielen:</b> {effectValue} Schaden an allen gegnerischen Monstern."); break;
                    case OnPlayEffect.DamageRandomEnemy: parts.Add($"<b>Beim Ausspielen:</b> {effectValue} Schaden an einem zufälligen gegnerischen Monster."); break;
                    case OnPlayEffect.DrawCard: parts.Add(effectValue > 1 ? $"<b>Beim Ausspielen:</b> Ziehe {effectValue} Karten." : "<b>Beim Ausspielen:</b> Ziehe eine Karte."); break;
                }
                return parts.Count > 0 ? string.Join("\n", parts) : "";
            }
        }
    }

    /// <summary>Alle Karten des Prototyps.</summary>
    public static class CardDatabase
    {
        public static readonly List<CardData> All = new List<CardData>
        {
            // Gewöhnlich
            new CardData("kiesel", "Kneifkrebs",      1, 1, 1, new Color(0.90f, 0.40f, 0.30f), "Crab",        Keyword.None,        OnPlayEffect.DrawCard,          1, "Klein, rot, nachtragend."),
            new CardData("biene",  "Summbiene",       1, 2, 1, new Color(1.00f, 0.80f, 0.20f), "Bee",         Keyword.FirstStrike, OnPlayEffect.None,              0, "Sticht zuerst, fragt nie."),
            new CardData("moos",   "Moosmampf",       2, 1, 4, new Color(0.35f, 0.70f, 0.30f), "Tree",        Keyword.Guardian,    OnPlayEffect.None,              0, "Frisst alles. Auch Ärger."),
            new CardData("frost",  "Frostgeist",      2, 3, 1, new Color(0.55f, 0.80f, 1.00f), "Ghost",       Keyword.FirstStrike, OnPlayEffect.None,              0, "Kalt. Durchsichtig. Gemein."),
            new CardData("knochen","Knochenkopf",     2, 2, 3, new Color(0.85f, 0.82f, 0.75f), "Skull",       Keyword.None,        OnPlayEffect.None,              0, "Hat keine Haut, also kein Problem."),
            new CardData("hirsch", "Waldhirsch",      3, 2, 3, new Color(0.75f, 0.55f, 0.35f), "Deer",        Keyword.None,        OnPlayEffect.HealOwner,         3, "Bringt Ruhe auf jede Lichtung."),
            new CardData("glotz",  "Glotzauge",       3, 3, 4, new Color(0.55f, 0.45f, 0.85f), "Cyclops",     Keyword.None,        OnPlayEffect.None,              0, "Sieht alles. Versteht wenig."),
            // Selten
            new CardData("glut",   "Glutteufel",      3, 3, 2, new Color(1.00f, 0.45f, 0.15f), "Demon",       Keyword.None,        OnPlayEffect.DamageAllEnemies,  1, "Heißer als Omas Bügeleisen.",          Rarity.Rare),
            new CardData("kaktus", "Pieksi",          3, 2, 5, new Color(0.40f, 0.75f, 0.35f), "Cactus",      Keyword.Thorns,      OnPlayEffect.None,              0, "Umarmungen nicht empfohlen.",          Rarity.Rare),
            new CardData("zapf",   "Zapfzahn",        4, 4, 3, new Color(0.55f, 0.85f, 0.35f), "Alien",       Keyword.None,        OnPlayEffect.DamageRandomEnemy, 2, "Beißt in alles, was Strom hat.",       Rarity.Rare),
            new CardData("gift",   "Giftteufel",      4, 3, 4, new Color(0.40f, 0.80f, 0.40f), "GreenDemon",  Keyword.Thorns,      OnPlayEffect.DamageAllEnemies,  1, "Riecht nach Sumpf und schlechter Laune.", Rarity.Rare),
            // Episch
            new CardData("yeti",   "Schneestampfer",  4, 3, 6, new Color(0.80f, 0.88f, 1.00f), "Yeti",        Keyword.None,        OnPlayEffect.HealOwner,         4, "Kalte Füße, warmes Herz.",             Rarity.Epic),
            new CardData("panda",  "Bambusbrecher",   5, 4, 7, new Color(0.90f, 0.90f, 0.90f), "Panda",       Keyword.Guardian,    OnPlayEffect.None,              0, "Isst 12 Stunden. Kämpft die anderen 12.", Rarity.Epic),
            new CardData("stern",  "Sternenwandler",  5, 5, 4, new Color(0.50f, 0.65f, 1.00f), "Alien_Tall",  Keyword.None,        OnPlayEffect.DamageRandomEnemy, 3, "Kam zu Besuch. Blieb zum Kämpfen.",    Rarity.Epic),
            // Legendär
            new CardData("donner", "Donnerdrache",    6, 6, 6, new Color(0.95f, 0.72f, 0.20f), "Dragon",      Keyword.None,        OnPlayEffect.DamageAllEnemies,  2, "Brüllt nicht. Blitzt.",                Rarity.Legendary),
            new CardData("sonne",  "Sonnendrache",    7, 7, 7, new Color(1.00f, 0.85f, 0.30f), "YellowDragon",Keyword.FirstStrike, OnPlayEffect.HealOwner,         5, "Wenn er gähnt, wird es Sommer.",       Rarity.Legendary),
        };

        public static CardData Get(string id)
        {
            foreach (var c in All) if (c.id == id) return c;
            return null;
        }

        /// <summary>Deck aus gespeicherten Karten-IDs bauen und mischen.</summary>
        public static List<CardData> BuildDeck(List<string> ids)
        {
            var deck = new List<CardData>();
            foreach (var id in ids)
            {
                var c = Get(id);
                if (c != null) deck.Add(c);
            }
            Shuffle(deck);
            return deck;
        }

        static void Shuffle(List<CardData> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        /// <summary>Zufälliges Deck mit 20 Karten für den KI-Gegner.</summary>
        public static List<CardData> BuildDeck()
        {
            var deck = new List<CardData>();
            int guard = 0;
            while (deck.Count < 20 && guard++ < 500)
            {
                var c = All[Random.Range(0, All.Count)];
                int have = 0;
                foreach (var d in deck) if (d == c) have++;
                if (have >= c.MaxCopies) continue;
                // Seltene Karten seltener
                float chance = c.rarity == Rarity.Legendary ? 0.25f : c.rarity == Rarity.Epic ? 0.5f : c.rarity == Rarity.Rare ? 0.8f : 1f;
                if (Random.value > chance) continue;
                deck.Add(c);
            }
            Shuffle(deck);
            return deck;
        }
    }
}
