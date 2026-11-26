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
                case "gear":
                {
                    // gear <id> : alle Ausrüstungsteile geben (zum Testen)
                    foreach (var g in GearDb.All) D.AddGear(g.id);
                    Game.Save();
                    return "ok " + D.gear.Count;
                }
                case "gearpick":
                    MenuUI.Instance?.Show(MenuPage.Collection);
                    MenuUI.Instance?.DebugGearPicker(CreatureDb.Get(p[1]), (GearSlot)int.Parse(p[2]));
                    return "ok";
                case "tft":
                {
                    // tft new | auto | fight | state – Kesselschlacht steuern
                    string sub = p.Length > 1 ? p[1] : "state";
                    if (sub == "new") { TftSession.NewGame(); GameFlow.ToTft(); return "ok neue Partie"; }
                    var tm = Object.FindFirstObjectByType<TftManager>();
                    if (sub == "auto") return tm != null ? tm.DebugAuto() : "nicht in der Planung";
                    if (sub == "fight") { if (tm == null) return "nicht in der Planung"; tm.DebugFight(); return "ok Kampf"; }
                    var g = TftSession.Current;
                    if (g == null) return "keine Partie";
                    var me = g.players[0];
                    return $"Runde {g.RoundName}, Leben {me.hp}, Gold {me.gold}, Stufe {me.level}, Feld {me.BoardCount}, Laden {string.Join(",", me.shop)}, Ende {g.Over}";
                }
                case "movepick":
                    MenuUI.Instance?.Show(MenuPage.Collection);
                    MenuUI.Instance?.DebugMovePicker(CreatureDb.Get(p[1]));
                    return "ok";
                case "moves":
                {
                    // moves <id> <i> [j] – Attacken setzen (Index im Pool)
                    var oc = D.Get(p[1]);
                    if (oc == null) return "nicht im Besitz";
                    oc.move1 = int.Parse(p[2]);
                    oc.move2 = p.Length > 3 ? int.Parse(p[3]) : -1;
                    Game.Save();
                    var pool = MoveDb.PoolFor(oc.Def);
                    var eq = Progress.EquippedMoves(oc);
                    var n = new System.Collections.Generic.List<string>();
                    foreach (int i in eq) n.Add(pool[i].move.name + "/" + pool[i].move.fx);
                    return "ok " + string.Join(", ", n);
                }
                case "pool":
                {
                    var d = CreatureDb.Get(p[1]);
                    var n = new System.Collections.Generic.List<string>();
                    foreach (var pm in MoveDb.PoolFor(d)) n.Add($"{pm.move.name}({pm.move.fx},ab {pm.unlock})");
                    return string.Join("; ", n);
                }
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
                case "caption":
                    Caption.Show(cmd.Length > 8 ? cmd.Substring(8) : "");
                    return "ok";
                case "arena":
                {
                    // arena register|defense|find|attack
                    var m = MenuUI.Instance;
                    if (m == null) return "kein Menü";
                    m.Show(MenuPage.Arena);
                    switch (p.Length > 1 ? p[1] : "")
                    {
                        case "register": m.ArenaRegister(); break;
                        case "defense": m.ArenaDefense(); break;
                        case "find": m.ArenaFind(); break;
                        case "attack": m.ArenaAttack(); break;
                    }
                    return "ok arena " + (Online.HasAccount ? "Konto vorhanden" : "kein Konto");
                }
                case "cloud":
                    if (p.Length > 1 && p[1] == "up") Online.UploadSave(() => Debug.Log("[Cloud] hochgeladen"), e => Debug.Log("[Cloud] Fehler " + e));
                    else Online.DownloadSave(j => Debug.Log("[Cloud] geladen " + j.Length + " Zeichen"), e => Debug.Log("[Cloud] Fehler " + e));
                    return "ok";
                case "rec":
                {
                    // rec start NAME FPS | rec stop – Video im Ordner ClaudeBridge des Projekts
                    if (p.Length > 1 && p[1] == "stop") return FrameRecorder.Stop();
                    string name = p.Length > 2 ? p[2] : "demo";
                    float fps = p.Length > 3 ? float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture) : 15f;
                    string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "ClaudeBridge");
                    return FrameRecorder.Start(System.IO.Path.Combine(dir, name + ".mjpeg"), fps, 1280, 720);
                }
                case "give":
                {
                    // give <id> [stufe] – Kreatur schenken (zum Testen neuer Modelle)
                    var oc = D.Get(p[1]);
                    if (oc == null) { oc = new OwnedCreature { id = p[1] }; D.creatures.Add(oc); }
                    if (p.Length > 2) oc.stage = int.Parse(p[2]);
                    Game.Save();
                    return "ok " + p[1];
                }
                case "team":
                {
                    // team id0 id1 … id5 – Team setzen (fehlende Kreaturen werden geschenkt), "-" = leer
                    for (int i = 0; i < TeamSpec.Slots; i++)
                    {
                        string id = i + 1 < p.Length ? p[i + 1] : "-";
                        if (id == "-") { D.team[i] = null; continue; }
                        if (D.Get(id) == null) D.creatures.Add(new OwnedCreature { id = id, level = 25, stars = 3 });
                        D.team[i] = id;
                    }
                    Game.Save();
                    return "ok team " + D.TeamCount();
                }
                case "timescale":
                    Time.timeScale = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture);
                    return "ok";
            }
            return "unbekannt: " + cmd;
        }
    }
}
