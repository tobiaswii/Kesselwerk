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

        public CardData(string id, string name, int cost, int attack, int health, Color color, string model,
                        Keyword keyword, OnPlayEffect onPlay, int effectValue, string flavor, BodyShape shape = BodyShape.Capsule)
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
            this.shape = shape;
        }

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
            new CardData("kiesel", "Kneifkrebs",     1, 1, 1, new Color(0.90f, 0.40f, 0.30f), "Crab",   Keyword.None,        OnPlayEffect.DrawCard,          1, "Klein, rot, nachtragend."),
            new CardData("moos",   "Moosmampf",      2, 1, 4, new Color(0.35f, 0.70f, 0.30f), "Tree",   Keyword.Guardian,    OnPlayEffect.None,              0, "Frisst alles. Auch Ärger."),
            new CardData("frost",  "Frostgeist",     2, 3, 1, new Color(0.55f, 0.80f, 1.00f), "Ghost",  Keyword.FirstStrike, OnPlayEffect.None,              0, "Kalt. Durchsichtig. Gemein."),
            new CardData("glut",   "Glutteufel",     3, 3, 2, new Color(1.00f, 0.45f, 0.15f), "Demon",  Keyword.None,        OnPlayEffect.DamageAllEnemies,  1, "Heißer als Omas Bügeleisen."),
            new CardData("kaktus", "Pieksi",         3, 2, 5, new Color(0.40f, 0.75f, 0.35f), "Cactus", Keyword.Thorns,      OnPlayEffect.None,              0, "Umarmungen nicht empfohlen."),
            new CardData("zapf",   "Zapfzahn",       4, 4, 3, new Color(0.55f, 0.85f, 0.35f), "Alien",  Keyword.None,        OnPlayEffect.DamageRandomEnemy, 2, "Beißt in alles, was Strom hat."),
            new CardData("yeti",   "Schneestampfer", 4, 3, 6, new Color(0.80f, 0.88f, 1.00f), "Yeti",   Keyword.None,        OnPlayEffect.HealOwner,         4, "Kalte Füße, warmes Herz."),
            new CardData("donner", "Donnerdrache",   6, 6, 6, new Color(0.95f, 0.72f, 0.20f), "Dragon", Keyword.None,        OnPlayEffect.DamageAllEnemies,  2, "Brüllt nicht. Blitzt."),
        };

        /// <summary>Baut ein gemischtes Deck mit 20 Karten.</summary>
        public static List<CardData> BuildDeck()
        {
            var deck = new List<CardData>();
            int[] copies = { 3, 3, 3, 3, 2, 2, 2, 2 };
            for (int i = 0; i < All.Count; i++)
                for (int c = 0; c < copies[i]; c++)
                    deck.Add(All[i]);

            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck;
        }
    }
}
