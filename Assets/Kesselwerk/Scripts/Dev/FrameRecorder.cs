using System.Collections;
using System.IO;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Nimmt das Spielbild für Vorführvideos auf (nur über Testbefehle erreichbar).
    /// Alle Bilder landen als JPEG hintereinander in EINER Datei (MJPEG-Strom),
    /// dazu eine Zeitdatei. ffmpeg kann den Strom direkt lesen.
    /// </summary>
    public class FrameRecorder : MonoBehaviour
    {
        static FrameRecorder instance;
        FileStream stream;
        StreamWriter times;
        float interval, next, start;
        int frames, width, height;
        RenderTexture rt;
        Texture2D tex;

        public static string Start(string path, float fps, int w, int h)
        {
            Stop();
            var go = new GameObject("Rekorder");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FrameRecorder>();
            instance.Begin(path, fps, w, h);
            return "Aufnahme läuft: " + path;
        }

        public static string Stop()
        {
            if (instance == null) return "keine Aufnahme";
            string r = instance.End();
            Destroy(instance.gameObject);
            instance = null;
            return r;
        }

        void Begin(string path, float fps, int w, int h)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            times = new StreamWriter(path + ".times.txt");
            interval = 1f / Mathf.Max(1f, fps);
            width = w; height = h;
            rt = new RenderTexture(w, h, 0);
            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            start = Time.realtimeSinceStartup;
            next = start;
            StartCoroutine(Loop());
        }

        string End()
        {
            float dur = Time.realtimeSinceStartup - start;
            stream?.Flush(); stream?.Dispose(); stream = null;
            times?.Flush(); times?.Dispose(); times = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            if (tex != null) Destroy(tex);
            return $"Aufnahme beendet: {frames} Bilder in {dur:0.0} s ({frames / Mathf.Max(0.01f, dur):0.0} Bilder/s)";
        }

        IEnumerator Loop()
        {
            var eof = new WaitForEndOfFrame();
            while (stream != null)
            {
                yield return eof;
                if (stream == null) yield break;
                float now = Time.realtimeSinceStartup;
                if (now < next) continue;
                next += interval;
                if (next < now) next = now + interval; // Rückstand nicht aufholen
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(shot, rt);
                Destroy(shot);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prev;
                var jpg = tex.EncodeToJPG(85);
                stream.Write(jpg, 0, jpg.Length);
                times.WriteLine((now - start).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                frames++;
            }
        }

        void OnDestroy()
        {
            if (stream != null) End();
        }
    }
}
