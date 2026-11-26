using System.Collections;
using System.Collections.Generic;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// 3D-Darstellung einer Kreatur: Modell, Animationen (Idle, Laufen, Angriff, Spezial,
    /// Treffer, Zurückstoßen, Tod, Sieg) und Effekte nach Element und Seltenheit.
    /// </summary>
    public class Creature : MonoBehaviour
    {
        public CreatureDef def;
        public int stage;
        public bool isPlayer;
        public int slot;
        public float Height { get; private set; } = 1.2f;
        public bool IsDead { get; private set; }

        public Vector3 home;
        Quaternion homeRot;
        MonsterAnimator anim;
        bool hasModel;
        bool busy;
        float bobSeed;
        Transform body;

        readonly List<Material> materials = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();

        public Color ElementColor => Look.ElementColor(def.element);
        public Vector3 Center => transform.position + Vector3.up * Height * 0.55f;
        public Vector3 Top => transform.position + Vector3.up * (Height + 0.35f);

        /// <summary>Erzeugt eine Kreatur. facing = Blickrichtung in Ruhe.</summary>
        public static Creature Spawn(CreatureDef def, int stage, Vector3 position, Vector3 facing, bool isPlayer, int slot, float sizeMul = 1f)
        {
            var root = new GameObject("Kreatur_" + def.StageName(stage));
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing : Vector3.forward);

            var c = root.AddComponent<Creature>();
            c.def = def;
            c.stage = stage;
            c.isPlayer = isPlayer;
            c.slot = slot;
            c.home = position;
            c.homeRot = root.transform.rotation;
            c.bobSeed = Random.Range(0f, 10f);

            var bodyGo = new GameObject("Koerper");
            bodyGo.transform.SetParent(root.transform, false);
            c.body = bodyGo.transform;

            // Größe nach Rolle, Seltenheit und Entwicklungsstufe – Bosse sind riesig
            float h = 1.35f + (int)def.rarity * 0.1f + (def.role == Role.Tank ? 0.2f : 0f) + stage * 0.3f;
            if (def.isBoss) h = 3.1f;
            h *= sizeMul;

            var model = MonsterModel.Load(def.model, c.body, h, out var animator, out var clips);
            if (model != null)
            {
                c.hasModel = true;
                c.Height = MonsterModel.CalcBounds(model).size.y;
                c.anim = root.AddComponent<MonsterAnimator>();
                c.anim.Init(animator, clips);
                c.anim.PlayIdle();
            }
            else
            {
                c.BuildPlaceholder(h); // klar erkennbarer Platzhalter, falls ein Modell fehlt
            }
            c.CollectMaterials();

            // Entwickelte Kreaturen bekommen einen farbigen Schimmer
            if (stage > 0) c.TintBase(Color.Lerp(Color.white, c.ElementColor, 0.25f));

            // Seltene Kreaturen und Bosse funkeln
            if (def.rarity >= Rarity.Selten || def.isBoss)
            {
                Color rc = Look.RarityColor(def.rarity) * (def.rarity >= Rarity.Legendaer ? 3f : 2.2f);
                if (def.isBoss) rc = new Color(3f, 0.6f, 0.3f);
                Fx.Aura(root.transform, rc, 0.55f + (int)def.rarity * 0.1f, c.Height);
            }
            return c;
        }

        void BuildPlaceholder(float size)
        {
            Height = size;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(go.GetComponent<Collider>());
            go.name = "PLATZHALTER";
            go.transform.SetParent(body, false);
            go.transform.localPosition = new Vector3(0f, size * 0.5f, 0f);
            go.transform.localScale = new Vector3(size * 0.6f, size * 0.5f, size * 0.6f);
            go.GetComponent<Renderer>().material.color = ElementColor;
        }

        void CollectMaterials()
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (!m.HasProperty("_BaseColor") && !m.HasProperty("_Color")) continue;
                    materials.Add(m);
                    baseColors.Add(GetCol(m));
                }
            }
        }

        static Color GetCol(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
        static void SetCol(Material m, Color c) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c; }

        void TintBase(Color tint)
        {
            for (int i = 0; i < materials.Count; i++)
            {
                baseColors[i] = baseColors[i] * tint;
                SetCol(materials[i], baseColors[i]);
            }
        }

        void SetFlash(Color tint, float amount)
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null)
                    SetCol(materials[i], Color.Lerp(baseColors[i], tint, amount));
        }

        void Update()
        {
            if (busy || hasModel || IsDead) return;
            float y = Mathf.Sin(Time.time * 2.2f + bobSeed) * 0.06f;
            body.localPosition = new Vector3(0f, y, 0f);
        }

        public void FaceHome() => transform.rotation = homeRot;

        IEnumerator TurnTo(Vector3 target, float time)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) yield break;
            Quaternion from = transform.rotation, to = Quaternion.LookRotation(d);
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.rotation = to;
        }

        // ------------------------------------------------------------------
        // Auftritt
        // ------------------------------------------------------------------

        public IEnumerator SpawnAnim(float delay = 0f)
        {
            busy = true;
            transform.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Color rc = Look.RarityColor(def.rarity);
            Fx.Ring(home + Vector3.up * 0.05f, ElementColor * 2.5f);
            Fx.Pillar(home, (def.rarity <= Rarity.Ungewoehnlich ? ElementColor : rc) * 2.5f, Height, 40 + (int)def.rarity * 30);
            Fx.Burst(home + Vector3.up * 0.3f, ElementColor * 2f, 30, 3f, 0.2f, 0.6f, -0.3f);
            if (def.rarity >= Rarity.Legendaer || def.isBoss)
            {
                Fx.Ring(home + Vector3.up * 0.1f, rc * 3f);
                Fx.Burst(home + Vector3.up * Height * 0.6f, rc * 3f, 90, 6f, 0.3f, 1f, 0.2f);
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
            {
                transform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t));
                yield return null;
            }
            transform.localScale = Vector3.one;
            busy = false;
        }

        // ------------------------------------------------------------------
        // Kampf
        // ------------------------------------------------------------------

        /// <summary>Nahkampf: läuft zum Ziel, schlägt zu. onImpact wird beim Treffer aufgerufen.</summary>
        public IEnumerator MeleeAttack(Creature target, System.Action onImpact, bool special = false)
        {
            busy = true;
            yield return TurnTo(target.transform.position, 0.12f);
            anim?.Play(MonsterAnimator.WalkKeys, true);
            StartTrail();

            Vector3 dir = (target.transform.position - transform.position);
            dir.y = 0f;
            float reach = 0.55f + target.Height * 0.25f + Height * 0.2f;
            Vector3 end = target.transform.position - dir.normalized * reach;
            end.y = home.y;
            Vector3 start = transform.position;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.22f)
            {
                float k = Mathf.Clamp01(t);
                Vector3 p = Vector3.Lerp(start, end, k * k * (3f - 2f * k));
                p.y += Mathf.Sin(k * Mathf.PI) * 0.35f;
                transform.position = p;
                yield return null;
            }
            transform.position = end;

            float len = anim != null ? anim.Play(special ? MonsterAnimator.SpecialKeys : MonsterAnimator.AttackKeys, false) : 0f;
            yield return new WaitForSeconds(Mathf.Clamp(len * 0.35f, 0.1f, 0.22f));
            onImpact?.Invoke();
            yield return new WaitForSeconds(0.1f);
            yield return ReturnHome();
        }

        /// <summary>Fernkampf: Angriff im Stand, Geschoss fliegt zum Ziel.</summary>
        public IEnumerator RangedAttack(Creature target, System.Action onImpact)
        {
            busy = true;
            yield return TurnTo(target.transform.position, 0.12f);
            float len = anim != null ? anim.Play(MonsterAnimator.AttackKeys, false) : 0f;
            // kleiner Hüpfer nach vorn
            Vector3 fwd = transform.forward * 0.25f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                transform.position = home + fwd * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                yield return null;
            }
            transform.position = home;
            yield return Projectile(Center, target.Center, ElementColor, 0.22f);
            onImpact?.Invoke();
            if (len > 0.5f) yield return new WaitForSeconds(0.1f);
            yield return TurnTo(transform.position + homeRot * Vector3.forward, 0.15f);
            anim?.PlayIdle();
            busy = false;
        }

        /// <summary>Spezialfähigkeit vorbereiten: Sprung, Leuchten, Elementwirbel.</summary>
        public IEnumerator CastStart()
        {
            busy = true;
            Color c = ElementColor;
            Fx.Ring(transform.position + Vector3.up * 0.05f, c * 3f);
            Fx.Pillar(transform.position, c * 2.5f, Height * 1.3f, 50);
            anim?.Play(MonsterAnimator.SpecialKeys, false);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                float k = Mathf.Clamp01(t);
                body.localPosition = new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f);
                body.localRotation = Quaternion.Euler(0f, k * 360f, 0f);
                SetFlash(c, Mathf.Sin(k * Mathf.PI) * 0.6f);
                yield return null;
            }
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            SetFlash(c, 0f);
        }

        public void CastEnd()
        {
            anim?.PlayIdle();
            transform.rotation = homeRot;
            busy = false;
        }

        IEnumerator ReturnHome()
        {
            anim?.Play(MonsterAnimator.WalkKeys, true);
            Vector3 start = transform.position;
            Quaternion r0 = transform.rotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.24f)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(start, home, k);
                transform.rotation = Quaternion.Slerp(r0, homeRot, k);
                yield return null;
            }
            transform.position = home;
            transform.rotation = homeRot;
            StopTrail();
            if (!IsDead) anim?.PlayIdle();
            busy = false;
        }


        // ------------------------------------------------------------------
        // Bausteine für Attacken-Animationen
        // ------------------------------------------------------------------

        public float PlayClip(string[] keys) => anim != null ? anim.Play(keys, false) : 0f;
        public IEnumerator Face(Vector3 p, float time = 0.12f) { busy = true; yield return TurnTo(p, time); }
        public IEnumerator GoHome() => ReturnHome();

        /// <summary>Kraft sammeln: Clip abspielen, leicht anheben, pulsierend in der Elementfarbe leuchten, Funken ziehen sich zusammen.</summary>
        public IEnumerator Channel(string[] keys, float time, Color c, float lift = 0.25f)
        {
            busy = true;
            PlayClip(keys);
            FxPlus.Gather(Center, c, Height, time);
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                float k = Mathf.Clamp01(t);
                body.localPosition = new Vector3(0f, Mathf.Sin(k * Mathf.PI * 0.5f) * lift, 0f);
                SetFlash(c, (0.25f + 0.35f * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f))) * k);
                yield return null;
            }
        }

        /// <summary>Nach dem Kraftsammeln wieder normal.</summary>
        public IEnumerator Release(float time = 0.2f)
        {
            Vector3 p0 = body.localPosition;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                float k = Mathf.Clamp01(t);
                body.localPosition = Vector3.Lerp(p0, Vector3.zero, k);
                SetFlash(ElementColor, (1f - k) * 0.4f);
                yield return null;
            }
            body.localPosition = Vector3.zero;
            SetFlash(Color.white, 0f);
        }

        /// <summary>Ansturm oder Sprung aufs Ziel mit gewähltem Clip. onImpact beim Treffer.</summary>
        public IEnumerator Lunge(Creature target, string[] keys, System.Action onImpact, bool leap, float speed = 0.2f)
        {
            busy = true;
            yield return TurnTo(target.transform.position, 0.1f);
            StartTrail();
            Vector3 dir = target.transform.position - transform.position; dir.y = 0f;
            float reach = 0.5f + target.Height * 0.25f + Height * 0.2f;
            Vector3 end = target.transform.position - dir.normalized * reach; end.y = home.y;
            Vector3 start = transform.position;
            if (leap)
            {
                float len = PlayClip(keys);
                // Absprung-Ausholen
                yield return new WaitForSeconds(Mathf.Clamp(len * 0.12f, 0.05f, 0.12f));
                for (float t = 0f; t < 1f; t += Time.deltaTime / (speed * 1.6f))
                {
                    float k = Mathf.Clamp01(t);
                    Vector3 p = Vector3.Lerp(start, end, k);
                    p.y += Mathf.Sin(k * Mathf.PI) * (1.2f + Height * 0.4f);
                    transform.position = p;
                    yield return null;
                }
                transform.position = end;
                onImpact?.Invoke();
                yield return new WaitForSeconds(0.18f);
            }
            else
            {
                anim?.Play(MonsterAnimator.WalkKeys, true);
                for (float t = 0f; t < 1f; t += Time.deltaTime / speed)
                {
                    float k = Mathf.Clamp01(t);
                    transform.position = Vector3.Lerp(start, end, k * k);
                    if (Random.value < 0.5f) FxPlus.Dust(transform.position, 0.5f);
                    yield return null;
                }
                transform.position = end;
                float len = PlayClip(keys);
                yield return new WaitForSeconds(Mathf.Clamp(len * 0.35f, 0.08f, 0.22f));
                onImpact?.Invoke();
                yield return new WaitForSeconds(0.12f);
            }
            yield return ReturnHome();
        }

        /// <summary>Starker Rückstoß nach großen Treffern.</summary>
        public void Knock(Vector3 from, float power)
        {
            if (IsDead) return;
            StartCoroutine(KnockRoutine(from, power));
        }

        IEnumerator KnockRoutine(Vector3 from, float power)
        {
            Vector3 push = transform.position - from; push.y = 0f;
            push = push.sqrMagnitude > 0.001f ? push.normalized * power : Vector3.zero;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                float k = Mathf.Clamp01(t);
                body.localPosition = transform.InverseTransformVector(push) * Mathf.Sin(k * Mathf.PI) + Vector3.up * Mathf.Sin(k * Mathf.PI) * power * 0.6f;
                yield return null;
            }
            body.localPosition = Vector3.zero;
        }

        public static IEnumerator Projectile(Vector3 from, Vector3 to, Color color, float time)
        {
            var go = new GameObject("Geschoss");
            go.transform.position = from;
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Fx.AdditiveMaterial;
            tr.time = 0.18f;
            tr.widthMultiplier = 0.35f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color * 3f, 0f), new GradientColorKey(color * 2f, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                float k = Mathf.Clamp01(t);
                go.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                yield return null;
            }
            go.transform.position = to;
            Fx.Burst(to, color * 2.5f, 18, 3f, 0.15f, 0.35f, 0f);
            Destroy(go, 0.3f);
        }

        /// <summary>Treffer: rot aufleuchten, zurückgestoßen werden.</summary>
        public void Hit(Vector3 from, bool heavy)
        {
            if (IsDead) return;
            StartCoroutine(HitRoutine(from, heavy));
        }

        IEnumerator HitRoutine(Vector3 from, bool heavy)
        {
            anim?.Play(MonsterAnimator.HitKeys, false);
            Vector3 push = transform.position - from;
            push.y = 0f;
            push = push.sqrMagnitude > 0.001f ? push.normalized * (heavy ? 0.45f : 0.2f) : Vector3.zero;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                float k = Mathf.Clamp01(t);
                body.localPosition = transform.InverseTransformVector(push) * Mathf.Sin(k * Mathf.PI) +
                                     new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (1f - k) * 0.05f;
                SetFlash(new Color(1f, 0.25f, 0.2f), 1f - k);
                yield return null;
            }
            body.localPosition = Vector3.zero;
            SetFlash(Color.white, 0f);
            if (!IsDead && !busy) anim?.PlayIdle();
        }

        public void Glow(Color c)
        {
            if (IsDead) return;
            StartCoroutine(GlowRoutine(c));
        }

        IEnumerator GlowRoutine(Color c)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                SetFlash(c, (1f - Mathf.Clamp01(t)) * 0.7f);
                yield return null;
            }
            SetFlash(Color.white, 0f);
        }

        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            StartCoroutine(DieRoutine());
        }

        IEnumerator DieRoutine()
        {
            StopTrail();
            float len = anim != null ? anim.Play(MonsterAnimator.DeathKeys, false) : 0f;
            yield return new WaitForSeconds(Mathf.Clamp(len, 0.3f, 1.0f));
            Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.8f, 0.8f, 0.9f), 30, 2.5f, 0.3f, 0.9f, -0.2f);
            Vector3 s0 = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                transform.localScale = s0 * (1f - Mathf.Clamp01(t));
                yield return null;
            }
            gameObject.SetActive(false);
        }

        /// <summary>Siegesfreude: hüpfen und jubeln.</summary>
        public IEnumerator Victory()
        {
            if (IsDead) yield break;
            busy = true;
            anim?.Play(MonsterAnimator.VictoryKeys, true);
            float seed = bobSeed;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin((t + seed) * 6f)) * 0.35f, 0f);
                yield return null;
            }
            body.localPosition = Vector3.zero;
        }

        /// <summary>Niederlage: sinkt traurig zusammen.</summary>
        public IEnumerator Defeat()
        {
            if (IsDead) yield break;
            anim?.Play(MonsterAnimator.HitKeys, false);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
            {
                body.localScale = new Vector3(1f, 1f - Mathf.Clamp01(t) * 0.25f, 1f);
                yield return null;
            }
        }

        // ------------------------------------------------------------------
        // Spur beim Laufen
        // ------------------------------------------------------------------

        GameObject trailGo;

        void StartTrail()
        {
            if (trailGo != null) return;
            trailGo = new GameObject("Spur");
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
            var tr = trailGo.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Fx.AdditiveMaterial;
            tr.time = 0.2f;
            tr.widthMultiplier = Height * 0.5f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            Color c = (def.rarity <= Rarity.Ungewoehnlich ? ElementColor : Look.RarityColor(def.rarity)) * 2f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.minVertexDistance = 0.05f;
        }

        void StopTrail()
        {
            if (trailGo == null) return;
            var tr = trailGo.GetComponent<TrailRenderer>();
            if (tr != null) tr.emitting = false;
            Destroy(trailGo, 0.4f);
            trailGo = null;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
