using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Zusätzliche Attacken-Effekte (komplett im Code): Klingenbögen, Bisse, Strahlen, Meteore,
    /// Druckwellen, Schutzkuppeln, Dornen, Wirbelstürme, Wolken, Energiesammeln.
    /// </summary>
    public static class FxPlus
    {
        static Mesh crescent, ring, sphere, cone;

        // ------------------------------------------------------------------
        // Grundlagen
        // ------------------------------------------------------------------

        static Material Mat(Color c)
        {
            var m = new Material(Fx.AdditiveMaterial);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            return m;
        }

        static void SetAlpha(Material m, Color c, float a)
        {
            var k = new Color(c.r * a, c.g * a, c.b * a, a);
            m.SetColor("_BaseColor", k);
            m.SetColor("_Color", k);
        }

        /// <summary>Weiße Eckpunktfarben setzen (der Partikel-Shader multipliziert damit).</summary>
        static Mesh White(Mesh m)
        {
            var cols = new Color[m.vertexCount];
            for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
            m.colors = cols;
            return m;
        }

        static GameObject MeshObj(string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>Lässt ein Objekt über die Zeit laufen (step bekommt 0..1) und löscht es danach.</summary>
        public static void Animate(GameObject go, float duration, Action<float> step, Material owned = null)
        {
            var a = go.AddComponent<FxTimeline>();
            a.duration = duration; a.step = step; a.owned = owned;
        }

        static Mesh Crescent
        {
            get
            {
                if (crescent != null) return crescent;
                // Halbmondförmiger Bogen in der XY-Ebene, Öffnung nach -X, Breite läuft spitz aus
                var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                int n = 24;
                for (int i = 0; i <= n; i++)
                {
                    float k = i / (float)n;
                    float ang = Mathf.Lerp(-80f, 80f, k) * Mathf.Deg2Rad;
                    float w = Mathf.Sin(k * Mathf.PI) * 0.28f;
                    v.Add(new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f));
                    v.Add(new Vector3(Mathf.Cos(ang) * (1f - w), Mathf.Sin(ang) * (1f - w), 0f));
                    uv.Add(new Vector2(k, 1f)); uv.Add(new Vector2(k, 0f));
                }
                for (int i = 0; i < n; i++)
                {
                    int a = i * 2;
                    t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                    t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); // beidseitig
                }
                crescent = new Mesh { name = "Bogen" };
                crescent.SetVertices(v); crescent.SetUVs(0, uv); crescent.SetTriangles(t, 0);
                crescent.RecalculateBounds();
                return White(crescent);
            }
        }

        static Mesh Ring
        {
            get
            {
                if (ring != null) return ring;
                var v = new List<Vector3>(); var t = new List<int>();
                int n = 48;
                for (int i = 0; i <= n; i++)
                {
                    float ang = i / (float)n * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    v.Add(d); v.Add(d * 0.82f);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = i * 2;
                    t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                    t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
                ring = new Mesh { name = "Ring" };
                ring.SetVertices(v); ring.SetTriangles(t, 0); ring.RecalculateBounds();
                return White(ring);
            }
        }

        static Mesh Sphere
        {
            get
            {
                if (sphere != null) return sphere;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere = UnityEngine.Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
                UnityEngine.Object.Destroy(tmp);
                return White(sphere);
            }
        }

        static Mesh Cone
        {
            get
            {
                if (cone != null) return cone;
                var v = new List<Vector3>(); var t = new List<int>();
                int n = 8;
                v.Add(new Vector3(0f, 1f, 0f));
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * 0.18f, 0f, Mathf.Sin(a) * 0.18f));
                }
                for (int i = 0; i < n; i++) t.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });
                cone = new Mesh { name = "Dorn" };
                cone.SetVertices(v); cone.SetTriangles(t, 0); cone.RecalculateNormals(); cone.RecalculateBounds();
                return White(cone);
            }
        }

        static ParticleSystem Particles(string name, Vector3 pos, Color c)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Fx.AdditiveMaterial;
            var main = ps.main;
            main.loop = false;
            main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
            return ps;
        }

        // ------------------------------------------------------------------
        // Effekte
        // ------------------------------------------------------------------

        /// <summary>Energie zieht sich am Körper zusammen (Kraft sammeln).</summary>
        public static void Gather(Vector3 center, Color c, float height, float time)
        {
            var ps = Particles("FX_Sammeln", center, c * 2.5f);
            var main = ps.main;
            main.duration = Mathf.Max(0.1f, time * 0.8f);
            main.startLifetime = 0.35f;
            main.startSpeed = -3.2f - height;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.maxParticles = 200;
            var em = ps.emission; em.rateOverTime = 110f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.1f + height * 0.4f;
            ps.Play();
            Fx.Ring(center - Vector3.up * (height * 0.5f) + Vector3.up * 0.05f, c * 2.5f);
        }

        /// <summary>Staubwolke am Boden (Ansturm, Landung).</summary>
        public static void Dust(Vector3 pos, float amount)
        {
            var ps = Particles("FX_Staub", pos + Vector3.up * 0.05f, new Color(0.55f, 0.5f, 0.42f, 0.5f));
            var main = ps.main;
            main.duration = 0.1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f * amount);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f * (0.5f + amount));
            main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(4 + amount * 10)) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = 0.2f;
            ps.Play();
        }

        /// <summary>Klingenbögen über dem Ziel (Hiebe, Klauen, Windklingen).</summary>
        public static void Slash(Vector3 center, Vector3 fromDir, Color c, int count = 3, float size = 1f)
        {
            fromDir.y = 0f;
            if (fromDir.sqrMagnitude < 0.01f) fromDir = Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                var m = Mat(c * 3f);
                var go = MeshObj("FX_Hieb", Crescent, m, center);
                float roll = -50f + i * (100f / Mathf.Max(1, count - 1)) + UnityEngine.Random.Range(-12f, 12f);
                var baseRot = Quaternion.LookRotation(-fromDir.normalized) * Quaternion.Euler(0f, 90f, roll);
                float delay = i * 0.06f;
                float s = size * (0.75f + 0.15f * i);
                Animate(go, 0.3f + delay, k =>
                {
                    float u = Mathf.Clamp01((k * (0.3f + delay) - delay) / 0.3f);
                    go.transform.rotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Lerp(-70f, 70f, u));
                    go.transform.localScale = Vector3.one * s * Mathf.Lerp(0.6f, 1.25f, u) * (u > 0f ? 1f : 0f);
                    SetAlpha(m, c * 3f, u < 0.5f ? 1f : (1f - u) * 2f);
                }, m);
            }
            Fx.Sparks(center, c * 2.5f, -fromDir + Vector3.up * 0.3f, 22);
        }

        /// <summary>Zwei Kieferbögen schnappen zu.</summary>
        public static void Bite(Vector3 center, Color c, float size = 1f)
        {
            for (int j = 0; j < 2; j++)
            {
                float sg = j == 0 ? 1f : -1f;
                var m = Mat(c * 3f);
                var go = MeshObj("FX_Biss", Crescent, m, center);
                var rot = Camera.main != null ? Quaternion.LookRotation(Camera.main.transform.forward) : Quaternion.identity;
                Animate(go, 0.28f, k =>
                {
                    float close = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.2f));
                    go.transform.rotation = rot * Quaternion.Euler(0f, 0f, sg > 0 ? 90f : -90f);
                    go.transform.position = center + Vector3.up * sg * Mathf.Lerp(0.55f, 0.08f, close) * size;
                    go.transform.localScale = new Vector3(0.6f, 0.9f, 1f) * size;
                    SetAlpha(m, c * 3f, k < 0.6f ? 1f : (1f - k) / 0.4f);
                }, m);
            }
        }

        /// <summary>Lebensenergie fliegt vom Ziel zum Angreifer zurück.</summary>
        public static void Drain(Vector3 from, Vector3 to, Color c)
        {
            var ps = Particles("FX_Entzug", from, c * 2.5f);
            var main = ps.main;
            float dist = Vector3.Distance(from, to);
            main.duration = 0.3f;
            main.startLifetime = 0.45f;
            main.startSpeed = dist / 0.45f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            var em = ps.emission; em.rateOverTime = 60f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.15f;
            ps.transform.rotation = Quaternion.LookRotation(to - from);
            ps.Play();
        }

        /// <summary>Strahl vom Angreifer zum Ziel, schwillt an und ab.</summary>
        public static IEnumerator Beam(Vector3 from, Vector3 to, Color c, float duration, float width = 0.5f)
        {
            var go = new GameObject("FX_Strahl");
            var outer = go.AddComponent<LineRenderer>();
            outer.sharedMaterial = Fx.AdditiveMaterial;
            outer.positionCount = 2; outer.numCapVertices = 6;
            outer.SetPosition(0, from); outer.SetPosition(1, to);
            var coreGo = new GameObject("Kern"); coreGo.transform.SetParent(go.transform);
            var core = coreGo.AddComponent<LineRenderer>();
            core.sharedMaterial = Fx.AdditiveMaterial;
            core.positionCount = 2; core.numCapVertices = 4;
            core.SetPosition(0, from); core.SetPosition(1, to);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float k = Mathf.Clamp01(t);
                float w = Mathf.Sin(k * Mathf.PI) * width * (0.85f + 0.15f * Mathf.Sin(Time.time * 60f));
                outer.widthMultiplier = w; core.widthMultiplier = w * 0.35f;
                outer.startColor = outer.endColor = c * 2.5f;
                core.startColor = core.endColor = Color.white * 2f;
                if (UnityEngine.Random.value < 0.4f) Fx.Burst(to, c * 2.5f, 4, 3f, 0.15f, 0.3f, 0f);
                yield return null;
            }
            UnityEngine.Object.Destroy(go);
        }

        /// <summary>Großes Geschoss mit Glutspur (Feuerball, Eisspeer, Felsbrocken …).</summary>
        public static IEnumerator Shot(Vector3 from, Vector3 to, Color c, float time, float size, bool arc = true)
        {
            var m = Mat(c * 2.5f);
            var go = MeshObj("FX_Geschoss", Sphere, m, from);
            go.transform.localScale = Vector3.one * size;
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Fx.AdditiveMaterial;
            tr.time = 0.25f; tr.widthMultiplier = size * 0.9f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c * 3f, 0f), new GradientColorKey(c * 1.5f, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            var embers = Particles("Glut", from, c * 2f);
            embers.transform.SetParent(go.transform, true);
            var em = embers.emission; em.rateOverTime = 70f;
            var main = embers.main; main.loop = true; main.startLifetime = 0.35f; main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, size * 0.4f); main.stopAction = ParticleSystemStopAction.None;
            embers.Play();
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                float k = Mathf.Clamp01(t);
                go.transform.position = Vector3.Lerp(from, to, k) + (arc ? Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.0f : Vector3.zero);
                go.transform.localScale = Vector3.one * size * (1f + 0.15f * Mathf.Sin(Time.time * 40f));
                yield return null;
            }
            embers.transform.SetParent(null, true);
            embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            UnityEngine.Object.Destroy(embers.gameObject, 0.5f);
            UnityEngine.Object.Destroy(go);
            UnityEngine.Object.Destroy(m, 0.5f);
            Fx.Burst(to, c * 3f, 40, 5f, 0.25f, 0.5f, 0.3f);
        }

        /// <summary>Wachsende Energiekugel an einer Stelle (Aufladen).</summary>
        public static void Orb(Vector3 pos, Color c, float size, float time)
        {
            var m = Mat(c * 2.5f);
            var go = MeshObj("FX_Kugel", Sphere, m, pos);
            Animate(go, time, k =>
            {
                go.transform.localScale = Vector3.one * size * Mathf.SmoothStep(0.1f, 1f, k) * (1f + 0.12f * Mathf.Sin(Time.time * 35f));
                SetAlpha(m, c * 2.5f, 0.4f + 0.6f * k);
            }, m);
        }

        /// <summary>Meteor fällt schräg vom Himmel auf eine Stelle.</summary>
        public static IEnumerator Meteor(Vector3 target, Color c, float size)
        {
            Vector3 from = target + new Vector3(UnityEngine.Random.Range(-2.5f, 2.5f), 9f, UnityEngine.Random.Range(1.5f, 3f));
            yield return Shot(from, target, c, 0.32f, size, false);
            Shockwave(new Vector3(target.x, 0.06f, target.z), c, 1.4f * size / 0.35f, 0.45f);
            Dust(new Vector3(target.x, 0f, target.z), 1f);
        }

        /// <summary>Flacher, sich ausbreitender Ring am Boden.</summary>
        public static void Shockwave(Vector3 pos, Color c, float radius, float time)
        {
            var m = Mat(c * 2.5f);
            var go = MeshObj("FX_Druckwelle", Ring, m, new Vector3(pos.x, Mathf.Max(0.05f, pos.y), pos.z));
            Animate(go, time, k =>
            {
                float e = 1f - (1f - k) * (1f - k);
                go.transform.localScale = new Vector3(radius * e, 1f, radius * e);
                SetAlpha(m, c * 2.5f, 1f - k);
            }, m);
        }

        /// <summary>Schutzkuppel um eine Kreatur.</summary>
        public static void Dome(Vector3 pos, Color c, float radius)
        {
            var m = Mat(c * 1.2f);
            var go = MeshObj("FX_Kuppel", Sphere, m, pos + Vector3.up * radius * 0.35f);
            Animate(go, 0.9f, k =>
            {
                float grow = k < 0.25f ? Mathf.SmoothStep(0f, 1.12f, k / 0.25f) : Mathf.Lerp(1.12f, 1f, (k - 0.25f) / 0.75f);
                go.transform.localScale = new Vector3(2f, 1.6f, 2f) * radius * grow;
                SetAlpha(m, c * 1.2f, k < 0.6f ? 0.55f : 0.55f * (1f - k) / 0.4f);
            }, m);
            Fx.Ring(pos + Vector3.up * 0.06f, c * 2.5f);
        }

        /// <summary>Dornen/Stacheln brechen aus dem Boden.</summary>
        public static void Spikes(Vector3 pos, Color c, int count = 7, float height = 1.1f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                float r = i == 0 ? 0f : UnityEngine.Random.Range(0.35f, 0.75f);
                Vector3 p = new Vector3(pos.x + Mathf.Cos(a) * r, 0f, pos.z + Mathf.Sin(a) * r);
                var m = Mat(c * 1.8f);
                var go = MeshObj("FX_Dorn", Cone, m, p);
                float h = height * (i == 0 ? 1.3f : UnityEngine.Random.Range(0.6f, 1f));
                var tilt = Quaternion.Euler(UnityEngine.Random.Range(-20f, 20f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(-20f, 20f));
                float delay = i * 0.025f;
                Animate(go, 0.7f, k =>
                {
                    float u = Mathf.Clamp01((k * 0.7f - delay) / 0.12f);
                    float down = Mathf.Clamp01((k - 0.65f) / 0.35f);
                    go.transform.rotation = tilt;
                    go.transform.localScale = new Vector3(1.2f, h * u * (1f - down), 1.2f);
                    SetAlpha(m, c * 1.8f, 1f - down);
                }, m);
            }
            Dust(pos, 1.2f);
        }

        /// <summary>Wirbelsturm, der über eine Strecke zieht.</summary>
        public static IEnumerator Tornado(Vector3 from, Vector3 to, Color c, float time)
        {
            var ps = Particles("FX_Wirbel", from, c * 1.8f);
            var main = ps.main;
            main.loop = true; main.stopAction = ParticleSystemStopAction.None;
            main.startLifetime = 0.6f; main.startSpeed = 0f; main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 260f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.35f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(9f); vel.radial = new ParticleSystem.MinMaxCurve(0.9f);
            vel.y = new ParticleSystem.MinMaxCurve(3.2f); vel.x = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            ps.Play();
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                ps.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                if (UnityEngine.Random.value < 0.3f) Dust(ps.transform.position, 0.6f);
                yield return null;
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            UnityEngine.Object.Destroy(ps.gameObject, 0.8f);
        }

        /// <summary>Wabernde Wolke (Gift, Albtraum).</summary>
        public static void Cloud(Vector3 pos, Color c, float radius)
        {
            var ps = Particles("FX_Wolke", pos + Vector3.up * 0.4f, c * 0.9f);
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.04f;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 26), new ParticleSystem.Burst(0.2f, 14) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = radius;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            ps.Play();
        }

        /// <summary>Eissplitter und Schneegestöber über einer Stelle.</summary>
        public static void Blizzard(Vector3 pos, Color c)
        {
            var ps = Particles("FX_Eissturm", pos + Vector3.up * 3f, c * 2f);
            var main = ps.main;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            var em = ps.emission; em.rateOverTime = 160f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 1.2f;
            ps.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(4f);
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            ps.Play();
        }

        /// <summary>Aufsteigende Heil-Lichter mit Ring.</summary>
        public static void Heal(Vector3 pos, Color c, float height)
        {
            Fx.Ring(pos + Vector3.up * 0.06f, c * 2.5f);
            var ps = Particles("FX_Heilung", pos, c * 2.5f);
            var main = ps.main;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, height * 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.gravityModifier = -0.1f;
            var em = ps.emission; em.rateOverTime = 70f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.55f; sh.rotation = new Vector3(-90f, 0f, 0f);
            ps.Play();
            Shockwave(pos, c, 1.6f, 0.6f);
        }
    }

    /// <summary>Kleiner Zeitgeber für Code-Effekte.</summary>
    public class FxTimeline : MonoBehaviour
    {
        public float duration = 0.5f;
        public Action<float> step;
        public Material owned;
        float t;

        void Start() => step?.Invoke(0f);

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duration));
            step?.Invoke(k);
            if (k >= 1f)
            {
                if (owned != null) Destroy(owned);
                Destroy(gameObject);
            }
        }
    }
}
