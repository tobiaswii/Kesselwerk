using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kesselwerk
{
    /// <summary>
    /// Lädt Naturmodelle (Quaternius Stylized Nature MegaKit, CC0) aus Resources/Nature
    /// und richtet die Materialien ein (Texturen, Alpha-Ausschnitt für Blätter/Gras, beidseitig).
    /// </summary>
    public static class NatureKit
    {
        static readonly Dictionary<string, Material> fixedMaterials = new Dictionary<string, Material>();
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        static GameObject Prefab(string name)
        {
            if (prefabs.TryGetValue(name, out var p)) return p;
            p = Resources.Load<GameObject>("Nature/" + name);
            prefabs[name] = p;
            return p;
        }

        /// <summary>Setzt ein Naturmodell. Größe = längste Seite in Metern (0 = Originalgröße).</summary>
        public static GameObject Spawn(string name, Transform parent, Vector3 pos, float height, float yaw = -1f, Color? tint = null)
        {
            var prefab = Prefab(name);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw < 0f ? Random.Range(0f, 360f) : yaw, 0f);

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Fix(mats[i], tint);
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            if (height > 0f)
            {
                var b = MonsterModel.CalcBounds(go);
                float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (size > 0.001f) go.transform.localScale *= height / size;
                b = MonsterModel.CalcBounds(go);
                go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
            }
            return go;
        }

        static string BaseName(Material m)
        {
            string n = m.name.Replace(" (Instance)", "");
            int dot = n.IndexOf('.');
            return dot > 0 ? n.Substring(0, dot) : n;
        }

        static bool IsFoliage(string n)
        {
            string l = n.ToLowerInvariant();
            return l.Contains("leaf") || l.Contains("leaves") || l.Contains("grass") || l.Contains("flower");
        }

        static Texture2D FindTexture(string n)
        {
            string[] candidates =
            {
                n, n + "_C", n + "_Diffuse",
                n == "Leaves_Pine" ? "Leaf_Pine_C" : null,
                n == "PathRocks" ? "PathRocks_Diffuse" : null,
                n == "Rocks" ? "Rocks_Diffuse" : null,
            };
            foreach (var c in candidates)
            {
                if (string.IsNullOrEmpty(c)) continue;
                var t = Resources.Load<Texture2D>("Nature/Textures/" + c);
                if (t != null) return t;
            }
            return null;
        }

        static Material Fix(Material src, Color? tint)
        {
            if (src == null) return null;
            string n = BaseName(src);
            string key = n + (tint.HasValue ? tint.Value.ToString() : "");
            if (fixedMaterials.TryGetValue(key, out var cached)) return cached;

            var m = new Material(src) { name = n + "_KW" };
            var tex = FindTexture(n);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint ?? Color.white);

            if (IsFoliage(n))
            {
                // Blätter/Gras: Alpha ausschneiden und beidseitig rendern
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);
                m.doubleSidedGI = true;
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }

            fixedMaterials[key] = m;
            return m;
        }

        // ------------------------------------------------------------------
        // Boden-Textur (gemalt wirkendes Gras, im Code erzeugt)
        // ------------------------------------------------------------------

        public static Texture2D GroundTexture()
        {
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            Color a = new Color(0.42f, 0.62f, 0.28f);
            Color b = new Color(0.55f, 0.72f, 0.32f);
            Color c = new Color(0.36f, 0.52f, 0.26f);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // Kachelbares Rauschen (auf einem Torus abgetastet)
                    float u = x / (float)s * Mathf.PI * 2f;
                    float v = y / (float)s * Mathf.PI * 2f;
                    float n1 = Mathf.PerlinNoise(3f + Mathf.Cos(u) * 1.5f, 7f + Mathf.Sin(u) * 1.5f + Mathf.Cos(v) * 1.5f);
                    float n2 = Mathf.PerlinNoise(11f + Mathf.Cos(v) * 4f, 2f + Mathf.Sin(v) * 4f + Mathf.Sin(u) * 4f);
                    Color col = Color.Lerp(a, b, Mathf.SmoothStep(0.3f, 0.7f, n1));
                    col = Color.Lerp(col, c, Mathf.SmoothStep(0.55f, 0.8f, n2) * 0.6f);
                    tex.SetPixel(x, y, col);
                }
            tex.Apply(true);
            return tex;
        }
    }
}
