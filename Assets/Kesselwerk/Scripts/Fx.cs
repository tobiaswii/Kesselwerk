using UnityEngine;
using UnityEngine.Rendering;

namespace Kesselwerk
{
    /// <summary>Partikel-Effekte, komplett im Code erzeugt (leuchtende, additive Partikel).</summary>
    public static class Fx
    {
        static Material additive;
        static Texture2D softDot;

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
    }
}
