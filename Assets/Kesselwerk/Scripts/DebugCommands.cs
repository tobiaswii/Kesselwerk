using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Testbefehle (z. B. vom Editor-Testwerkzeug): steuern das Spiel ohne Klicks.
    /// Nur für Entwicklung und QA – im fertigen Spiel nicht erreichbar.
    /// </summary>
    public static class DebugCommands
    {
        public static string Run(string cmd)
        {
            var p = cmd.Trim().Split(' ');
            if (p.Length == 0) return "leer";
            var D = Game.Data;
            switch (p[0])
            {
                case "menu":
                {
                    var page = p.Length > 1 ? (MenuPage)System.Enum.Parse(typeof(MenuPage), p[1], true) : MenuPage.Home;
                    if (MenuUI.Instance != null && MenuUI.Instance.isActiveAndEnabled) MenuUI.Instance.Show(page);
                    else GameFlow.ToMenu(page);
                    return "ok menu " + page;
                }
                case "stage":
                    MenuUI.Instance?.SelectStage(int.Parse(p[1]), int.Parse(p[2]));
                    return "ok";
                case "battle":
                {
                    int r = p.Length > 1 ? int.Parse(p[1]) : 0, i = p.Length > 2 ? int.Parse(p[2]) : 0;
                    Game.PendingStage = Campaign.Get(r, i);
                    GameFlow.ToBattle();
                    return "ok battle " + Game.PendingStage.name;
                }
                case "speed":
                    AutoBattleManager.Speed = int.Parse(p[1]);
                    Time.timeScale = AutoBattleManager.Speed;
                    return "ok";
                case "skip":
                    AutoBattleManager.Instance?.SkipToEnd();
                    return "ok";
                case "crystals":
                    D.crystals += int.Parse(p[1]);
                    Game.Save();
                    return "ok " + D.crystals;
                case "gold":
                    D.gold += int.Parse(p[1]);
                    Game.Save();
                    return "ok " + D.gold;
                case "summon":
                    MenuUI.Instance?.Show(MenuPage.Summon);
                    MenuUI.Instance?.DoSummon(p.Length > 1 ? int.Parse(p[1]) : 1);
                    return "ok";
                case "detail":
                    MenuUI.Instance?.Show(MenuPage.Collection);
                    MenuUI.Instance?.ShowDetail(CreatureDb.Get(p[1]));
                    return "ok";
                case "tasks":
                    MenuUI.Instance?.ShowTasks();
                    return "ok";
                case "rates":
                    MenuUI.Instance?.ShowRates();
                    return "ok";
                case "reset":
                    Game.Reset();
                    GameFlow.ToMenu();
                    return "ok reset";
                case "state":
                    return $"Gold {D.gold}, Kristalle {D.crystals}, Kreaturen {D.creatures.Count}, Team {D.TeamCount()}, Fortschritt {D.regionProgress[0]}/{D.regionProgress[1]}, Siege {D.wins}/{D.battles}" +
                           (AutoBattleManager.Instance != null ? $", Kampf läuft (fertig: {AutoBattleManager.Instance.Finished})" : "");
                case "timescale":
                    Time.timeScale = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                    return "ok";
            }
            return "unbekannt: " + cmd;
        }
    }
}
