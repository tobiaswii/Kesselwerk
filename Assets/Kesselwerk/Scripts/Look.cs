using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>Farben und Namen für Elemente, Seltenheiten und Rollen (einheitliche Designsprache).</summary>
    public static class Look
    {
        public static Color ElementColor(Element e)
        {
            switch (e)
            {
                case Element.Feuer: return new Color(1f, 0.42f, 0.18f);
                case Element.Wasser: return new Color(0.2f, 0.55f, 1f);
                case Element.Natur: return new Color(0.35f, 0.82f, 0.3f);
                case Element.Erde: return new Color(0.72f, 0.52f, 0.3f);
                case Element.Luft: return new Color(0.62f, 0.9f, 0.95f);
                case Element.Blitz: return new Color(1f, 0.88f, 0.2f);
                case Element.Eis: return new Color(0.6f, 0.85f, 1f);
                case Element.Licht: return new Color(1f, 0.95f, 0.65f);
                case Element.Schatten: return new Color(0.55f, 0.3f, 0.75f);
                default: return new Color(0.85f, 0.4f, 0.95f);
            }
        }

        /// <summary>Kurzzeichen für Element-Abzeichen.</summary>
        public static string ElementShort(Element e)
        {
            switch (e)
            {
                case Element.Feuer: return "FEU";
                case Element.Wasser: return "WAS";
                case Element.Natur: return "NAT";
                case Element.Erde: return "ERD";
                case Element.Luft: return "LUF";
                case Element.Blitz: return "BLI";
                case Element.Eis: return "EIS";
                case Element.Licht: return "LIC";
                case Element.Schatten: return "SCH";
                default: return "ARK";
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Ungewoehnlich: return new Color(0.35f, 0.85f, 0.4f);
                case Rarity.Selten: return new Color(0.3f, 0.6f, 1f);
                case Rarity.Episch: return new Color(0.72f, 0.4f, 1f);
                case Rarity.Legendaer: return new Color(1f, 0.72f, 0.2f);
                case Rarity.Mythisch: return new Color(1f, 0.3f, 0.45f);
                default: return new Color(0.78f, 0.8f, 0.84f);
            }
        }

        public static string RarityName(Rarity r) => ElementChart.RarityName(r);

        public static string RoleName(Role r)
        {
            switch (r)
            {
                case Role.Tank: return "Tank";
                case Role.Angreifer: return "Angreifer";
                case Role.Support: return "Unterstützer";
                default: return "Heiler";
            }
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        public static string Stars(int n) => new string('\u2666', n); // ♦ (★ fehlt in der Standardschrift)
    }
}
