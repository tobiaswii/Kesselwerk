using System;

namespace Kesselwerk.Core
{
    /// <summary>Die zehn Elemente. Reihenfolge nicht ändern (wird gespeichert).</summary>
    public enum Element { Feuer, Wasser, Natur, Erde, Luft, Blitz, Eis, Licht, Schatten, Arkan }

    /// <summary>Seltenheit beeinflusst Beschwörungschance, Aussehen und Entwicklung – nicht allein die Stärke.</summary>
    public enum Rarity { Gewoehnlich, Ungewoehnlich, Selten, Episch, Legendaer, Mythisch }

    /// <summary>Rolle im Team.</summary>
    public enum Role { Tank, Angreifer, Support, Heiler }

    /// <summary>
    /// Elementtabelle. Ein Element ist gegen zwei andere stark (x1,5).
    /// Greift man ein Element an, das gegen das eigene stark ist, gibt es x0,75.
    /// Licht und Schatten sind gegenseitig stark (beide x1,5).
    /// Arkan ist stark gegen Licht und Schatten, ansonsten neutral.
    /// </summary>
    public static class ElementChart
    {
        public const float Strong = 1.5f;
        public const float Weak = 0.75f;

        static readonly Element[][] beats =
        {
            /* Feuer    */ new[] { Element.Natur, Element.Eis },
            /* Wasser   */ new[] { Element.Feuer, Element.Erde },
            /* Natur    */ new[] { Element.Wasser, Element.Erde },
            /* Erde     */ new[] { Element.Blitz, Element.Feuer },
            /* Luft     */ new[] { Element.Natur, Element.Arkan },
            /* Blitz    */ new[] { Element.Wasser, Element.Luft },
            /* Eis      */ new[] { Element.Luft, Element.Wasser },
            /* Licht    */ new[] { Element.Schatten },
            /* Schatten */ new[] { Element.Licht },
            /* Arkan    */ new[] { Element.Licht, Element.Schatten },
        };

        public static bool Beats(Element attacker, Element defender)
        {
            foreach (var e in beats[(int)attacker]) if (e == defender) return true;
            return false;
        }

        public static float Multiplier(Element attacker, Element defender)
        {
            if (Beats(attacker, defender)) return Strong;
            if (Beats(defender, attacker)) return Weak;
            return 1f;
        }

        public static Element[] StrongAgainst(Element e) => beats[(int)e];

        public static string Name(Element e) => e.ToString();

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Gewoehnlich: return "Gewöhnlich";
                case Rarity.Ungewoehnlich: return "Ungewöhnlich";
                case Rarity.Selten: return "Selten";
                case Rarity.Episch: return "Episch";
                case Rarity.Legendaer: return "Legendär";
                default: return "Mythisch";
            }
        }
    }

    /// <summary>Deterministischer Zufallsgenerator (xorshift32). Gleicher Startwert = gleicher Kampf, auch auf dem Server.</summary>
    public sealed class Rng
    {
        uint state;

        public Rng(int seed)
        {
            state = (uint)seed;
            if (state == 0) state = 0x9E3779B9u;
            for (int i = 0; i < 4; i++) NextUInt();
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Wert in [0,1).</summary>
        public float Value() => (NextUInt() & 0xFFFFFF) / 16777216f;

        /// <summary>Ganzzahl in [min,max).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextUInt() % (uint)(max - min));
        }

        public float Range(float min, float max) => min + (max - min) * Value();
    }

    public static class MathK
    {
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
