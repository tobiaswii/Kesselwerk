using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Spielstand: Gold, Staub, Kartensammlung und Deck. Wird lokal gespeichert (PlayerPrefs, JSON).
    /// </summary>
    [System.Serializable]
    public class PlayerProfile
    {
        public const int DeckSize = 20;
        public const int PackPrice = 100;
        public const int PackSize = 4;
        public const int WinGold = 100;
        public const int LossGold = 30;
        const string SaveKey = "kesselwerk_profil_v1";

        [System.Serializable]
        public class Entry
        {
            public string id;
            public int count;
        }

        public int gold = 300;
        public int dust = 0;
        public int wins = 0;
        public int losses = 0;
        public List<Entry> collection = new List<Entry>();
        public List<string> deck = new List<string>();

        static PlayerProfile current;

        public static PlayerProfile Current
        {
            get
            {
                if (current == null) current = Load();
                return current;
            }
        }

        static PlayerProfile Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var p = JsonUtility.FromJson<PlayerProfile>(json);
                    if (p != null) { p.Cleanup(); return p; }
                }
                catch { /* defekter Spielstand -> neu anfangen */ }
            }
            return CreateStarter();
        }

        /// <summary>Startprofil: 3x jede gewöhnliche Karte, Deck mit 20 davon.</summary>
        static PlayerProfile CreateStarter()
        {
            var p = new PlayerProfile();
            foreach (var c in CardDatabase.All)
                if (c.rarity == Rarity.Common) p.SetCount(c.id, 3);
            p.AutoFillDeck();
            p.Save();
            return p;
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        /// <summary>Alles zurücksetzen (zum Testen).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            current = CreateStarter();
        }

        void Cleanup()
        {
            collection.RemoveAll(e => CardDatabase.Get(e.id) == null || e.count <= 0);
            deck.RemoveAll(id => CardDatabase.Get(id) == null);
            // Deck darf nicht mehr enthalten, als man besitzt
            var tmp = new List<string>(deck);
            deck.Clear();
            foreach (var id in tmp) if (CanAddToDeck(id)) deck.Add(id);
        }

        // ------------------------------------------------------------------
        // Sammlung
        // ------------------------------------------------------------------

        public int Count(string id)
        {
            foreach (var e in collection) if (e.id == id) return e.count;
            return 0;
        }

        void SetCount(string id, int count)
        {
            foreach (var e in collection)
                if (e.id == id) { e.count = count; return; }
            collection.Add(new Entry { id = id, count = count });
        }

        /// <summary>Fügt eine Karte hinzu. Kopien über dem Maximum werden zu Staub. Gibt den erhaltenen Staub zurück.</summary>
        public int AddCard(CardData card)
        {
            int have = Count(card.id);
            if (have >= card.MaxCopies)
            {
                dust += card.DustValue;
                return card.DustValue;
            }
            SetCount(card.id, have + 1);
            return 0;
        }

        public bool Craft(CardData card)
        {
            if (dust < card.CraftCost || Count(card.id) >= card.MaxCopies) return false;
            dust -= card.CraftCost;
            SetCount(card.id, Count(card.id) + 1);
            Save();
            return true;
        }

        // ------------------------------------------------------------------
        // Packs
        // ------------------------------------------------------------------

        public static Rarity RollRarity()
        {
            float r = Random.value;
            if (r < 0.02f) return Rarity.Legendary;
            if (r < 0.10f) return Rarity.Epic;
            if (r < 0.34f) return Rarity.Rare;
            return Rarity.Common;
        }

        static CardData RandomCard(Rarity rarity)
        {
            var list = CardDatabase.All.FindAll(c => c.rarity == rarity);
            if (list.Count == 0) list = CardDatabase.All;
            return list[Random.Range(0, list.Count)];
        }

        /// <summary>Kauft und öffnet ein Pack. Gibt die Karten zurück (null, wenn zu wenig Gold).</summary>
        public List<CardData> OpenPack()
        {
            if (gold < PackPrice) return null;
            gold -= PackPrice;
            var cards = new List<CardData>();
            bool hasRare = false;
            for (int i = 0; i < PackSize; i++)
            {
                var r = RollRarity();
                if (i == PackSize - 1 && !hasRare && r == Rarity.Common) r = Rarity.Rare; // mindestens eine seltene
                if (r != Rarity.Common) hasRare = true;
                cards.Add(RandomCard(r));
            }
            return cards;
        }

        // ------------------------------------------------------------------
        // Deck
        // ------------------------------------------------------------------

        public int InDeck(string id)
        {
            int n = 0;
            foreach (var d in deck) if (d == id) n++;
            return n;
        }

        public bool CanAddToDeck(string id)
        {
            var c = CardDatabase.Get(id);
            if (c == null || deck.Count >= DeckSize) return false;
            int inDeck = InDeck(id);
            return inDeck < Count(id) && inDeck < c.MaxCopies;
        }

        public bool AddToDeck(string id)
        {
            if (!CanAddToDeck(id)) return false;
            deck.Add(id);
            Save();
            return true;
        }

        public bool RemoveFromDeck(string id)
        {
            bool ok = deck.Remove(id);
            if (ok) Save();
            return ok;
        }

        public bool DeckComplete => deck.Count == DeckSize;

        /// <summary>Füllt das Deck mit den stärksten verfügbaren Karten auf.</summary>
        public void AutoFillDeck()
        {
            var candidates = new List<CardData>(CardDatabase.All);
            candidates.Sort((a, b) => (b.rarity.CompareTo(a.rarity) * 10 + (b.attack + b.health).CompareTo(a.attack + a.health)));
            bool added = true;
            while (deck.Count < DeckSize && added)
            {
                added = false;
                foreach (var c in candidates)
                {
                    if (deck.Count >= DeckSize) break;
                    if (CanAddToDeck(c.id)) { deck.Add(c.id); added = true; }
                }
            }
            Save();
        }
    }
}
