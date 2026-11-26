using UnityEngine;
using UnityEngine.Rendering;

namespace Kesselwerk
{
    /// <summary>Partikel-Effekte, komplett im Code erzeugt (leuchtende, additive Partikel).</summary>
    public static class Fx
    {
        static Material additive;
        static Texture2D softDot;

        public static Material AdditiveMaterial => Additive;

        static Material Additive
        {
            get
            {
                if (additive != null) return additive;
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                additive = new Material(shader);
                additive.SetTexture("_BaseMap", SoftDot);
                additive.SetTexture("_MainTex", SoftDot);
                // Transparent + additiv
                additive.SetFloat("_Surface", 1f);
                additive.SetFloat("_Blend", 2f);
                additive.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                additive.SetInt("_DstBlend", (int)BlendMode.One);
                additive.SetInt("_ZWrite", 0);
                additive.SetOverrideTag("RenderType", "Transparent");
                additive.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                additive.EnableKeyword("_BLENDMODE_ADD");
                additive.renderQueue = (int)RenderQueue.Transparent;
                return additive;
            }
        }

        static Texture2D SoftDot
        {
            get
            {
                if (softDot != null) return softDot;
                const int s = 64;
                softDot = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = (x + 0.5f) / s * 2f - 1f;
                        float dy = (y + 0.5f) / s * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a;
                        softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                softDot.Apply();
                return softDot;
            }
        }

        static ParticleSystem Create(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Additive;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>Kurzer Funkenregen (Treffer, Beschwörung, Tod).</summary>
        public static void Burst(Vector3 pos, Color color, int count = 24, float speed = 4f, float size = 0.18f, float life = 0.5f, float gravity = 1f)
        {
            var ps = Create("FX_Burst", pos);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = count + 10;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ps.Play();
        }

        /// <summary>Leuchtender Ring am Boden (Beschwörung).</summary>
        public static void Ring(Vector3 pos, Color color)
        {
            var ps = Create("FX_Ring", pos);
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 3.2f;
            main.startSize = 0.25f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 50) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.radiusThickness = 0f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ps.Play();
        }

        /// <summary>Dauerhaft aufsteigende Blasen/Funken (z. B. über dem Kessel).</summary>
        public static ParticleSystem Bubbles(Vector3 pos, Color color, float radius)
        {
            var ps = Create("FX_Blasen", pos);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ps.Play();
            return ps;
        }

        static void Stretched(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale;
            r.lengthScale = lengthScale;
        }

        static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        /// <summary>Lichtsäule (Beschwörung).</summary>
        public static void Pillar(Vector3 pos, Color color, float height, int count = 60)
        {
            var ps = Create("FX_Saeule", pos);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(height * 2f, height * 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Stretched(ps, 0.12f, 2f);
            FadeOut(ps);
            ps.Play();
        }

        /// <summary>Dauerhafte Funken-Aura um seltene Monster (wandert mit).</summary>
        public static ParticleSystem Aura(Transform parent, Color color, float radius, float height)
        {
            var ps = Create("FX_Aura", parent.position);
            ps.transform.SetParent(parent, true);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.rateOverTime = 10f + radius * 6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.y = new ParticleSystem.MinMaxCurve(height * 0.4f, height * 0.9f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            ps.Play();
            return ps;
        }

        /// <summary>Gerichtete Funkenstreifen (Treffer).</summary>
        public static void Sparks(Vector3 pos, Color color, Vector3 direction, int count = 30)
        {
            var ps = Create("FX_Funken", pos);
            ps.transform.rotation = Quaternion.LookRotation(direction.sqrMagnitude > 0.001f ? direction : Vector3.up);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = color;
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.1f;
            Stretched(ps, 0.06f, 1.5f);
            ps.Play();

            // Heller Blitz am Einschlag
            Burst(pos, color * 1.5f, 6, 0.5f, 0.9f, 0.18f, 0f);
        }

        /// <summary>Feuer-/Energiewelle über eine Reihe (Schaden an allen).</summary>
        public static void Wave(Vector3 center, float width, Color color)
        {
            var ps = Create("FX_Welle", center);
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startColor = color;
            main.gravityModifier = -0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120), new ParticleSystem.Burst(0.15f, 80) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 0.2f, 1.2f);
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            FadeOut(ps);
            ps.Play();
        }

        /// <summary>Zuckender Blitz zwischen zwei Punkten.</summary>
        public static void Lightning(Vector3 from, Vector3 to, Color color)
        {
            var go = new GameObject("FX_Blitz");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Additive;
            lr.positionCount = 14;
            lr.widthMultiplier = 0.16f;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = 2;
            go.AddComponent<FxLightning>().Init(lr, from, to, color);
            Burst(to, color, 30, 4f, 0.2f, 0.4f, 0.5f);
        }
    }
}

namespace Kesselwerk
{
    /// <summary>Lässt einen Blitz ein paar Mal zucken und dann verblassen.</summary>
    public class FxLightning : UnityEngine.MonoBehaviour
    {
        UnityEngine.LineRenderer lr;
        UnityEngine.Vector3 a, b;
        UnityEngine.Color color;
        float t;

        public void Init(UnityEngine.LineRenderer line, UnityEngine.Vector3 from, UnityEngine.Vector3 to, UnityEngine.Color c)
        {
            lr = line; a = from; b = to; color = c;
            Jitter();
        }

        void Jitter()
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)(n - 1);
                var p = UnityEngine.Vector3.Lerp(a, b, k);
                if (i > 0 && i < n - 1) p += UnityEngine.Random.insideUnitSphere * 0.35f;
                lr.SetPosition(i, p);
            }
        }

        void Update()
        {
            t += UnityEngine.Time.deltaTime;
            if (UnityEngine.Time.frameCount % 3 == 0) Jitter();
            float alpha = 1f - UnityEngine.Mathf.Clamp01((t - 0.25f) / 0.25f);
            var c = new UnityEngine.Color(color.r, color.g, color.b, alpha);
            lr.startColor = c;
            lr.endColor = c;
            if (t > 0.5f) Destroy(gameObject);
        }
    }
}
