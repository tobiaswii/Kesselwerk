using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    public enum BodyShape { Capsule, Sphere, Cube }

    /// <summary>Daten einer Monster-Karte.</summary>
    [System.Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public string text;
        public int cost;
        public int attack;
        public int health;
        public Color color;
        public BodyShape shape;
        /// <summary>Name des 3D-Modells unter Resources/Monsters (leer = Platzhalter aus Grundformen).</summary>
        public string model;

        public CardData(string id, string name, int cost, int attack, int health, Color color, BodyShape shape, string text, string model = null)
        {
            this.id = id;
            this.name = name;
            this.cost = cost;
            this.attack = attack;
            this.health = health;
            this.color = color;
            this.shape = shape;
            this.text = text;
            this.model = model;
        }
    }

    /// <summary>Alle Karten des Prototyps. Platzhalter-Monster, eigene Namen.</summary>
    public static class CardDatabase
    {
        public static readonly List<CardData> All = new List<CardData>
        {
            new CardData("kiesel",  "Kneifkrebs",     1, 1, 2, new Color(0.90f, 0.40f, 0.30f), BodyShape.Sphere,  "Klein, rot, nachtragend.",          "Crab"),
            new CardData("moos",    "Moosmampf",      2, 1, 4, new Color(0.35f, 0.70f, 0.30f), BodyShape.Capsule, "Frisst alles. Auch Ärger.",         "Tree"),
            new CardData("frost",   "Frostgeist",     2, 3, 1, new Color(0.55f, 0.85f, 1.00f), BodyShape.Sphere,  "Kalt. Durchsichtig. Gemein.",       "Ghost"),
            new CardData("glut",    "Glutteufel",     3, 3, 3, new Color(1.00f, 0.50f, 0.15f), BodyShape.Cube,    "Heißer als Omas Bügeleisen.",       "Demon"),
            new CardData("kaktus",  "Pieksi",         3, 2, 5, new Color(0.40f, 0.75f, 0.35f), BodyShape.Capsule, "Umarmungen nicht empfohlen.",       "Cactus"),
            new CardData("zapf",    "Zapfzahn",       4, 4, 3, new Color(0.60f, 0.90f, 0.40f), BodyShape.Cube,    "Beißt in alles, was Strom hat.",    "Alien"),
            new CardData("yeti",    "Schneestampfer", 4, 3, 5, new Color(0.85f, 0.90f, 1.00f), BodyShape.Capsule, "Kalte Füße, warmes Herz.",          "Yeti"),
            new CardData("donner",  "Donnerdrache",   6, 6, 6, new Color(0.95f, 0.75f, 0.25f), BodyShape.Capsule, "Brüllt nicht. Blitzt.",             "Dragon"),
        };

        /// <summary>Baut ein gemischtes Deck mit 20 Karten.</summary>
        public static List<CardData> BuildDeck()
        {
            var deck = new List<CardData>();
            int[] copies = { 3, 3, 3, 3, 2, 2, 2, 2 }; // = 20 Karten
            for (int i = 0; i < All.Count; i++)
                for (int c = 0; c < copies[i]; c++)
                    deck.Add(All[i]);

            // Mischen (Fisher-Yates)
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck;
        }
    }
}
