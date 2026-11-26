using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kesselwerk
{
    /// <summary>
    /// Lädt Monster-Modelle aus Resources/Monsters und rendert Kartenbilder (Porträts).
    /// Die Modelle schauen in ihrer Grundstellung in Richtung +Z.
    /// </summary>
    public static class MonsterModel
    {
        static readonly Dictionary<string, Texture> portraitCache = new Dictionary<string, Texture>();
        static int portraitCounter;

        public static GameObject Load(string model, Transform parent, float targetHeight,
                                      out Animator animator, out AnimationClip[] clips)
        {
            animator = null;
            clips = null;
            if (string.IsNullOrEmpty(model)) return null;

            var prefab = Resources.Load<GameObject>("Monsters/" + model);
            if (prefab == null) return null;

            var inst = Object.Instantiate(prefab, parent);
            inst.name = "Modell";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            // Anime-Shader für alle Monster: Textur (falls vorhanden) × Eckpunktfarbe
            var tex = Resources.Load<Texture2D>("Monsters/Textures/" + model + "_Texture");
            bool generated = model.StartsWith("Gen_");
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var mats = r.sharedMaterials;
                var fresh = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    Texture t = tex;
                    if (t == null && mats[i] != null && !generated)
                        t = mats[i].HasProperty("_BaseMap") ? mats[i].GetTexture("_BaseMap") : mats[i].mainTexture;
                    fresh[i] = ToonMaterial(t, mats[i] != null && !generated && t == null ? mats[i].color : Color.white);
                }
                r.materials = fresh;
            }

            // Einheitliche Höhe
            Bounds b = CalcBounds(inst);
            if (b.size.y > 0.001f) inst.transform.localScale *= targetHeight / b.size.y;

            // Auf den Boden stellen
            b = CalcBounds(inst);
            inst.transform.position += new Vector3(0f, parent.position.y - b.min.y, 0f);

            animator = inst.GetComponentInChildren<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            clips = Resources.LoadAll<AnimationClip>("Monsters/" + model);
            return inst;
        }

        static Shader toon;

        /// <summary>Neues Material mit dem Kreatur-Shader (je Instanz, weil Treffer die Farbe tönen).</summary>
        public static Material ToonMaterial(Texture tex, Color color)
        {
            if (toon == null) toon = Resources.Load<Shader>("Shaders/Kreatur");
            if (toon == null) toon = Shader.Find("Kesselwerk/Kreatur");
            var m = new Material(toon);
            m.SetTexture("_FurTex", FurNoise());
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            return m;
        }

        static Texture2D furNoise;

        /// <summary>Kachelbares, länglich gestrecktes Rauschen – wirkt auf der Oberfläche wie feine Fellsträhnen.</summary>
        public static Texture2D FurNoise()
        {
            if (furNoise != null) return furNoise;
            const int N = 128;
            var rnd = new System.Random(42);
            float[,] Lattice(int gx, int gy)
            {
                var g = new float[gx, gy];
                for (int x = 0; x < gx; x++) for (int y = 0; y < gy; y++) g[x, y] = (float)rnd.NextDouble();
                return g;
            }
            float Sample(float[,] g, float u, float v)
            {
                int gx = g.GetLength(0), gy = g.GetLength(1);
                float fx = u * gx, fy = v * gy;
                int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                float a = g[x0 % gx, y0 % gy], b = g[(x0 + 1) % gx, y0 % gy], c = g[x0 % gx, (y0 + 1) % gy], d = g[(x0 + 1) % gx, (y0 + 1) % gy];
                return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
            }
            var fine = Lattice(48, 8); var mid = Lattice(16, 4); var coarse = Lattice(6, 6);
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)N, v = y / (float)N;
                    float val = Sample(fine, u, v) * 0.55f + Sample(mid, u, v) * 0.3f + Sample(coarse, u, v) * 0.15f;
                    px[y * N + x] = new Color(val, val, val, 1f);
                }
            furNoise = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "Fellrauschen" };
            furNoise.SetPixels(px); furNoise.Apply(true);
            return furNoise;
        }

        public static Bounds CalcBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static AnimationClip FindClip(AnimationClip[] clips, string[] keys)
        {
            if (clips == null) return null;
            foreach (var key in keys)
                foreach (var c in clips)
                {
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    string n = c.name;
                    int bar = n.LastIndexOf('|');
                    string s = bar >= 0 ? n.Substring(bar + 1) : n;
                    if (s.EndsWith(key, System.StringComparison.OrdinalIgnoreCase)) return c;
                }
            return null;
        }

        /// <summary>Rendert ein Kartenbild des Monsters (einmal pro Karte, danach aus dem Cache).</summary>
        public static Texture Portrait(Kesselwerk.Core.CreatureDef def)
        {
            if (def == null) return null;
            if (portraitCache.TryGetValue(def.id, out var cached) && cached != null) return cached;
            Color elemColor = Look.ElementColor(def.element);

            // Jedes Porträt an einem eigenen Ort, damit sich Modelle im selben Bild nie überlagern
            portraitCounter++;
            var stage = new GameObject("PortraitStage");
            stage.transform.position = new Vector3(500f + (portraitCounter % 64) * 40f, 500f, 500f);
            var holder = new GameObject("Holder");
            holder.transform.SetParent(stage.transform, false);
            holder.transform.localRotation = Quaternion.Euler(0f, 200f, 0f); // leicht schräg zur Kamera

            var model = Load(def.model, holder.transform, 1.5f, out _, out var clips);
            Texture result = null;

            if (model != null)
            {
                var idle = FindClip(clips, MonsterAnimator.IdleKeys);
                if (idle != null) idle.SampleAnimation(model, idle.length * 0.3f);

                Bounds b = CalcBounds(model);
                var camGo = new GameObject("PortraitCam");
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.Lerp(elemColor, new Color(0.16f, 0.13f, 0.18f), 0.4f);
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;

                var rt = new RenderTexture(256, 320, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                rt.Create();
                cam.targetTexture = rt;
                cam.aspect = 256f / 320f;

                float h = Mathf.Max(0.5f, b.size.y);
                float dist = (h * 0.62f) / Mathf.Tan(15f * Mathf.Deg2Rad);
                Vector3 look = b.center + new Vector3(0f, h * 0.05f, 0f);
                camGo.transform.position = look + new Vector3(0.15f * h, 0.25f * h, -dist);
                camGo.transform.LookAt(look);

                // Eigene Porträt-Lichter (Führungslicht vorne, Randlicht hinten)
                var keyGo = new GameObject("PortraitKey");
                keyGo.transform.SetParent(stage.transform, false);
                keyGo.transform.position = look + new Vector3(-0.6f * h, 0.9f * h, -dist * 0.7f);
                var key = keyGo.AddComponent<Light>();
                key.type = LightType.Point;
                key.color = new Color(1f, 0.95f, 0.88f);
                key.intensity = 6f * h * h;
                key.range = dist * 3f;
                var rimGo = new GameObject("PortraitRim");
                rimGo.transform.SetParent(stage.transform, false);
                rimGo.transform.position = look + new Vector3(0.5f * h, 0.8f * h, dist * 0.6f);
                var rim = rimGo.AddComponent<Light>();
                rim.type = LightType.Point;
                rim.color = Color.Lerp(elemColor, Color.white, 0.4f);
                rim.intensity = 5f * h * h;
                rim.range = dist * 3f;

                // In URP über eine Render-Anfrage rendern, sonst klassisch
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                    RenderPipeline.SubmitRenderRequest(cam, request);
                else
                    cam.Render();
                cam.targetTexture = null;
                Object.Destroy(camGo);
                result = rt;
            }

            Object.DestroyImmediate(stage);
            portraitCache[def.id] = result;
            return result;
        }
    }
}
