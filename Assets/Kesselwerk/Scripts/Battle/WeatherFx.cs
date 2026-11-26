using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Sichtbares Wetter im Kampf: Schnee, Regen, Glut, Nebelschwaden, Gewitter mit Blitzen.
    /// Dazu eine eigene Gestaltung je Region (Lava, Eis, Schattenkristalle).
    /// </summary>
    public class WeatherFx : MonoBehaviour
    {
        Weather weather;
        float nextFlash;
        System.Action<Color> flash;

        public static WeatherFx Create(Weather w, System.Action<Color> flashScreen)
        {
            var go = new GameObject("Wetter");
            var fx = go.AddComponent<WeatherFx>();
            fx.weather = w;
            fx.flash = flashScreen;
            fx.nextFlash = Time.time + Random.Range(3f, 6f);
            switch (w)
            {
                case Weather.Schnee:
                    fx.Particles("Schnee", new Color(1f, 1f, 1f, 0.9f), 160, 0.09f, new Vector3(0f, -1.2f, 0f), 7f, 0.8f, false);
                    break;
                case Weather.Regen:
                    fx.Particles("Regen", new Color(0.7f, 0.85f, 1f, 0.55f), 420, 0.04f, new Vector3(0.6f, -14f, 0f), 1.1f, 0.1f, true);
                    break;
                case Weather.Sturm:
                    fx.Particles("Regen", new Color(0.75f, 0.85f, 1f, 0.6f), 600, 0.045f, new Vector3(3f, -17f, 0f), 1f, 0.1f, true);
                    break;
                case Weather.Hitze:
                    fx.Particles("Glut", new Color(3f, 1.2f, 0.35f, 1f), 90, 0.07f, new Vector3(0.2f, 1.1f, 0f), 5f, 0.6f, false, fromGround: true);
                    break;
                case Weather.Nebel:
                    fx.Particles("Nebel", new Color(0.75f, 0.7f, 0.95f, 0.12f), 40, 2.6f, new Vector3(0.25f, 0.05f, 0f), 12f, 0.15f, false, fromGround: true, low: true);
                    break;
            }
            return fx;
        }

        void Particles(string name, Color color, int rate, float size, Vector3 velocity, float life, float noise, bool stretch,
                       bool fromGround = false, bool low = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, fromGround ? 0.1f : 9f, 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f);
            main.startColor = color;
            main.maxParticles = rate * Mathf.CeilToInt(life + 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(22f, low ? 0.6f : 0.2f, 14f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = velocity.x; vel.y = velocity.y; vel.z = velocity.z;
            if (noise > 0f)
            {
                var n = ps.noise; n.enabled = true; n.strength = noise; n.frequency = 0.4f;
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Fx.AdditiveMaterial;
            if (stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1f;
            }
            ps.Play();
        }

        void Update()
        {
            if (weather != Weather.Sturm || Time.time < nextFlash) return;
            nextFlash = Time.time + Random.Range(4f, 8f);
            var from = new Vector3(Random.Range(-9f, 9f), 14f, Random.Range(6f, 14f));
            Fx.Lightning(from, new Vector3(from.x + Random.Range(-1.5f, 1.5f), 0f, from.z), new Color(2.5f, 2.6f, 3f));
            flash?.Invoke(new Color(0.85f, 0.9f, 1f, 0.35f));
            Sound.Play("krit", 0.35f, 0.5f);
        }

        // ------------------------------------------------------------------
        // Gestaltung je Region
        // ------------------------------------------------------------------

        public static void DressRegion(int region)
        {
            var arena = GameObject.Find("Arena");
            if (arena == null || region == 0) return;
            // Boden und Pflanzen einfärben
            Color ground = region == 1 ? new Color(0.55f, 0.4f, 0.36f) : region == 2 ? new Color(1.45f, 1.55f, 1.75f) : new Color(0.42f, 0.36f, 0.55f);
            Color plants = region == 1 ? new Color(0.7f, 0.58f, 0.5f) : region == 2 ? new Color(1.25f, 1.35f, 1.5f) : new Color(0.5f, 0.42f, 0.7f);
            foreach (var r in arena.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                bool isGround = r.gameObject.name == "Boden";
                if (!isGround && r.gameObject.name.StartsWith("Slot")) continue;
                foreach (var m in r.materials)
                {
                    if (isGround && region == 2)
                    {
                        // Schneedecke statt eingefärbtem Gras
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", NatureKit.GroundTexture() is Texture2D gt ? SnowTex(gt) : null);
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.92f, 0.95f, 1f));
                        continue;
                    }
                    if (m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION")) continue;
                    var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    var t = isGround ? ground : plants;
                    var n = new Color(c.r * t.r, c.g * t.g, c.b * t.b, c.a);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", n); else m.color = n;
                }
            }
            var root = new GameObject("Regionsdeko").transform;
            root.SetParent(arena.transform, false);
            var rnd = new System.Random(region * 977);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float d = 6.2f + (float)rnd.NextDouble() * 6f;
                var p = new Vector3(Mathf.Cos(a) * d * 1.15f, 0f, Mathf.Sin(a) * d + 1f);
                if (p.z < -3.5f && Mathf.Abs(p.x) < 7f) continue; // Kamerablick freihalten
                float s = 0.6f + (float)rnd.NextDouble() * 0.9f;
                switch (region)
                {
                    case 1: // Lavabecken und schwarze Felsnadeln
                        if (i % 3 == 0) Lava(root, p, s);
                        else Spire(root, p, s, new Color(0.16f, 0.12f, 0.12f), Color.black);
                        break;
                    case 2: // Eiskristalle
                        Crystal(root, p, s, new Color(0.75f, 0.9f, 1f), new Color(0.25f, 0.55f, 0.9f));
                        break;
                    case 3: // Schattenkristalle
                        Crystal(root, p, s, new Color(0.45f, 0.3f, 0.75f), new Color(0.6f, 0.25f, 1.2f));
                        break;
                }
            }
        }

        /// <summary>Graustufen-Version der Bodentextur (wirkt als Schnee mit leichter Struktur).</summary>
        static Texture2D SnowTex(Texture2D src)
        {
            try
            {
                var px = src.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    float l = px[i].grayscale;
                    px[i] = new Color(0.82f + l * 0.25f, 0.86f + l * 0.22f, 0.95f + l * 0.08f, 1f);
                }
                var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
                t.SetPixels(px); t.Apply();
                return t;
            }
            catch { return null; }
        }

        static Material Lit(Color c, Color emission)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.6f);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        static void Lava(Transform root, Vector3 p, float s)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Lava";
            go.transform.SetParent(root, false);
            go.transform.position = p + Vector3.up * 0.01f;
            go.transform.localScale = new Vector3(1.6f * s, 0.02f, 1.2f * s);
            go.GetComponent<Renderer>().material = Lit(new Color(1f, 0.35f, 0.05f), new Color(4f, 1.2f, 0.2f));
            var l = new GameObject("Glut").AddComponent<Light>();
            l.transform.SetParent(go.transform, false);
            l.transform.position = p + Vector3.up * 0.6f;
            l.color = new Color(1f, 0.45f, 0.15f); l.range = 3.5f * s; l.intensity = 2.5f;
        }

        static void Spire(Transform root, Vector3 p, float s, Color c, Color e)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.position = p + Vector3.up * 0.9f * s;
            go.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
            go.transform.localScale = new Vector3(0.5f * s, 1.8f * s, 0.5f * s);
            go.GetComponent<Renderer>().material = Lit(c, e);
        }

        static void Crystal(Transform root, Vector3 p, float s, Color c, Color e)
        {
            var cluster = new GameObject("Kristall").transform;
            cluster.SetParent(root, false);
            cluster.position = p;
            var mat = Lit(c, e);
            for (int k = 0; k < 3; k++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(cluster, false);
                float h = (k == 0 ? 1.4f : 0.8f) * s;
                go.transform.localPosition = new Vector3((k - 1) * 0.25f * s, h * 0.45f, (k % 2) * 0.15f * s);
                go.transform.localRotation = Quaternion.Euler((k - 1) * 18f, 45f + k * 20f, (k - 1) * -14f);
                go.transform.localScale = new Vector3(0.28f * s, h, 0.28f * s);
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }
}
