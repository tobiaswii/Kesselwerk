using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Kesselwerk.EditorTools
{
    /// <summary>
    /// Test-Brücke für die automatisierte Qualitätssicherung:
    /// liest Befehle aus "ClaudeBridge/cmd.txt" im Projektordner und schreibt Ergebnisse nach "ClaudeBridge/out.txt".
    /// Konsolenmeldungen landen in "ClaudeBridge/log.txt", Screenshots als PNG im selben Ordner.
    /// Befehle: refresh | play | stop | shot NAME | tests | game BEFEHL | clearlog | status
    /// Nur im Editor aktiv – nicht Teil des Spiels.
    /// </summary>
    [InitializeOnLoad]
    public static class ClaudeBridge
    {
        static readonly string Dir = Path.Combine(Directory.GetCurrentDirectory(), "ClaudeBridge");
        static readonly string CmdFile = Path.Combine(Dir, "cmd.txt");
        static readonly string OutFile = Path.Combine(Dir, "out.txt");
        static readonly string LogFile = Path.Combine(Dir, "log.txt");
        static double nextPoll;
        static readonly List<string> compileErrors = new List<string>();

        static ClaudeBridge()
        {
            Directory.CreateDirectory(Dir);
            EditorApplication.update += Poll;
            Application.logMessageReceivedThreaded += OnLog;
            CompilationPipeline.assemblyCompilationFinished += (asm, msgs) =>
            {
                foreach (var m in msgs)
                    if (m.type == CompilerMessageType.Error) compileErrors.Add($"{m.file}({m.line}): {m.message}");
            };
            CompilationPipeline.compilationStarted += _ => compileErrors.Clear();
            Write(OutFile, $"bereit {DateTime.Now:HH:mm:ss} – Kompilierfehler: {(EditorUtility.scriptCompilationFailed ? "JA" : "nein")}");
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            try
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] {type}: {msg}";
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    line += "\n" + stack;
                File.AppendAllText(LogFile, line + "\n");
            }
            catch { }
        }

        static void Write(string path, string text)
        {
            try { File.WriteAllText(path, text); } catch { }
        }

        static string recDir;
        static int recFrame;
        static double recInterval, recNext;

        static void Poll()
        {
            if (recDir != null && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= recNext)
            {
                recNext = EditorApplication.timeSinceStartup + recInterval;
                ScreenCapture.CaptureScreenshot(Path.Combine(recDir, $"f{recFrame++:00000}.png"));
            }
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.4;
            if (!File.Exists(CmdFile)) return;
            string cmd;
            try { cmd = File.ReadAllText(CmdFile).Trim(); File.Delete(CmdFile); }
            catch { return; }
            if (string.IsNullOrEmpty(cmd)) return;
            try { Write(OutFile, Execute(cmd)); }
            catch (Exception e) { Write(OutFile, "Fehler: " + e); }
        }

        static string Execute(string cmd)
        {
            var parts = cmd.Split(new[] { ' ' }, 2);
            string arg = parts.Length > 1 ? parts[1] : "";
            switch (parts[0])
            {
                case "refresh":
                    AssetDatabase.Refresh();
                    return "refresh angestoßen";
                case "status":
                    return $"Kompiliert: {!EditorApplication.isCompiling} · Fehler: {EditorUtility.scriptCompilationFailed} · Spielmodus: {EditorApplication.isPlaying}\n" +
                           string.Join("\n", compileErrors);
                case "play":
                    if (EditorUtility.scriptCompilationFailed) return "Kompilierfehler:\n" + string.Join("\n", compileErrors);
                    EditorApplication.isPlaying = true;
                    return "play";
                case "stop":
                    EditorApplication.isPlaying = false;
                    return "stop";
                case "clearlog":
                    Write(LogFile, "");
                    return "log geleert";
                case "shot":
                {
                    string name = string.IsNullOrEmpty(arg) ? "shot" : arg;
                    string path = Path.Combine(Dir, name + ".png");
                    if (File.Exists(path)) File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    EditorApplication.QueuePlayerLoopUpdate();
                    return "screenshot " + path;
                }
                case "game":
                    if (!EditorApplication.isPlaying) return "nicht im Spielmodus";
                {
                    // Spielcode liegt in Assembly-CSharp – per Reflexion aufrufen
                    var t = Type.GetType("Kesselwerk.DebugCommands, Assembly-CSharp");
                    if (t == null) return "DebugCommands nicht gefunden";
                    return (string)t.GetMethod("Run").Invoke(null, new object[] { arg });
                }
                case "size":
                {
                    var wh = arg.Split(' ');
                    return SetGameViewSize(int.Parse(wh[0]), int.Parse(wh[1]));
                }
                case "record":
                {
                    // record start NAME FPS | record stop
                    var a = arg.Split(' ');
                    if (a[0] == "stop") { string d = recDir; recDir = null; return $"Aufnahme beendet: {recFrame} Bilder in {d}"; }
                    recDir = Path.Combine(Dir, "rec_" + (a.Length > 1 ? a[1] : "demo"));
                    Directory.CreateDirectory(recDir);
                    recFrame = 0;
                    recInterval = 1.0 / (a.Length > 2 ? double.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture) : 8.0);
                    recNext = 0;
                    return "Aufnahme läuft: " + recDir;
                }
                case "tests":
                    RunTests();
                    return "tests gestartet";
            }
            return "unbekannt: " + cmd;
        }

        /// <summary>Stellt die Auflösung der Game-Ansicht ein (Hoch-/Querformat testen).</summary>
        static string SetGameViewSize(int w, int h)
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var inst = single.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(inst, new object[] { (int)GameViewSizeGroupType.Standalone });
            var gType = group.GetType();
            int total = (int)gType.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var size = gType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                var st = size.GetType();
                if ((int)st.GetProperty("width").GetValue(size) == w && (int)st.GetProperty("height").GetValue(size) == h) { index = i; break; }
            }
            if (index < 0)
            {
                var gvs = asm.GetType("UnityEditor.GameViewSize");
                var gvst = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = gvs.GetConstructor(new[] { gvst, typeof(int), typeof(int), typeof(string) });
                var ns = ctor.Invoke(new object[] { Enum.ToObject(gvst, 1), w, h, $"Kesselwerk {w}x{h}" });
                gType.GetMethod("AddCustomSize").Invoke(group, new[] { ns });
                index = (int)gType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var win = EditorWindow.GetWindow(gvType);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var prop = gvType.GetProperty("selectedSizeIndex", flags);
            if (prop != null) prop.SetValue(win, index);
            else gvType.GetMethod("SizeSelectionCallback", flags)?.Invoke(win, new object[] { index, null });
            win.Repaint();
            return $"Game-Ansicht {w}x{h} (Index {index})";
        }

        static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }

        class Callbacks : ICallbacks
        {
            readonly StringBuilder sb = new StringBuilder();
            int passed, failed;

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                sb.Insert(0, $"Tests: {passed} bestanden, {failed} fehlgeschlagen\n");
                Write(Path.Combine(Dir, "tests.txt"), sb.ToString());
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.Test.IsSuite) return;
                if (result.TestStatus == TestStatus.Passed) { passed++; sb.AppendLine("OK   " + result.Test.FullName); }
                else if (result.TestStatus == TestStatus.Failed) { failed++; sb.AppendLine("FAIL " + result.Test.FullName + ": " + result.Message); }
            }
        }
    }
}
