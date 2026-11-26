using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Ein Monster in der Arena. Schaut in Ruhe zur Kamera, dreht sich zum Angreifen zum Ziel.
    /// Fehlt das 3D-Modell, wird ein Platzhalter aus Grundformen gebaut.
    /// </summary>
    public class Creature : MonoBehaviour
    {
        public CardData card;
        public int attack;
        public int health;
        public int maxHealth;
        public bool isPlayer;
        public int slot;
        public bool hasAttacked;

        public Keyword Keyword => card.keyword;
        public bool IsDead => health <= 0;
        /// <summary>Höhe des Modells in Metern (für Anzeigen darüber).</summary>
        public float Height { get; private set; } = 1.2f;

        Vector3 home;
        Quaternion homeRot;
        float bobSeed;
        bool animating;
        bool hasModel;
        MonsterAnimator anim;

        readonly List<Material> materials = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();

        public static Creature Spawn(CardData card, Vector3 position, bool isPlayer, int slot)
        {
            var root = new GameObject("Kreatur_" + card.name);
            root.transform.position = position;
            // Beide Seiten schauen zur Kamera (die steht auf der Spielerseite bei -Z)
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var c = root.AddComponent<Creature>();
            c.card = card;
            c.attack = card.attack;
            c.health = card.health;
            c.maxHealth = card.health;
            c.isPlayer = isPlayer;
            c.slot = slot;
            c.home = position;
            c.homeRot = root.transform.rotation;
            c.bobSeed = Random.Range(0f, 10f);

            float targetHeight = Mathf.Min(1.35f + card.cost * 0.1f, 2.1f);
            var model = MonsterModel.Load(card, root.transform, targetHeight, out var animator, out var clips);
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
                c.BuildPrimitiveBody();
            }
            c.CollectMaterials();

            // Seltene Monster bekommen eine funkelnde Aura
            if (card.rarity != Rarity.Common)
            {
                Color rc = CardData.RarityColor(card.rarity) * (card.rarity == Rarity.Legendary ? 3f : 2.2f);
                Fx.Aura(root.transform, rc, 0.55f + (int)card.rarity * 0.12f, c.Height);
            }
            return c;
        }

        void BuildPrimitiveBody()
        {
            float size = 0.7f + card.cost * 0.08f;
            Height = size * 1.1f;
            PrimitiveType type = card.shape == BodyShape.Sphere ? PrimitiveType.Sphere
                               : card.shape == BodyShape.Cube ? PrimitiveType.Cube
                               : PrimitiveType.Capsule;

            var bodyGo = Prim(type, transform, new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, card.color);
            if (type == PrimitiveType.Capsule) bodyGo.transform.localScale = new Vector3(size, size * 0.5f, size);

            float eyeY = size * 0.65f, eyeZ = size * 0.45f, eyeX = size * 0.2f, eyeSize = size * 0.28f;
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, transform, new Vector3(eyeX * side, eyeY, eyeZ), Vector3.one * eyeSize, Color.white);
                Prim(PrimitiveType.Sphere, transform, new Vector3(eyeX * side, eyeY, eyeZ + eyeSize * 0.35f), Vector3.one * eyeSize * 0.5f, new Color(0.1f, 0.1f, 0.12f));
            }
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        void CollectMaterials()
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
                foreach (var m in r.materials)
                {
                    if (!m.HasProperty("_BaseColor") && !m.HasProperty("_Color")) continue;
                    materials.Add(m);
                    baseColors.Add(m.color);
                }
        }

        void SetTint(Color tint, float amount)
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null)
                    materials[i].color = Color.Lerp(baseColors[i], tint, amount);
        }

        void Update()
        {
            if (animating || hasModel) return;
            float y = Mathf.Sin(Time.time * 2.2f + bobSeed) * 0.06f;
            transform.position = home + new Vector3(0f, y, 0f);
        }

        // ------------------------------------------------------------------
        // Animationen
        // ------------------------------------------------------------------

        public IEnumerator SpawnAnim()
        {
            animating = true;
            transform.position = home;
            Color rc = CardData.RarityColor(card.rarity);
            Fx.Burst(home + Vector3.up * 0.2f, card.color * 2f, 40, 3.5f, 0.22f, 0.7f, -0.3f);
            Fx.Ring(home + Vector3.up * 0.05f, card.color * 2.5f);
            Fx.Pillar(home, (card.rarity == Rarity.Common ? card.color : rc) * 2.5f, Height, 40 + (int)card.rarity * 40);
            if (card.rarity == Rarity.Legendary)
            {
                Fx.Ring(home + Vector3.up * 0.1f, rc * 3f);
                Fx.Burst(home + Vector3.up * Height * 0.6f, rc * 3f, 90, 6f, 0.3f, 1f, 0.2f);
            }
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.4f;
                transform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t));
                yield return null;
            }
            transform.localScale = Vector3.one;
            animating = false;
        }

        /// <summary>Dreht sich zum Ziel, springt hin und spielt die Angriffsanimation.</summary>
        public IEnumerator LungeOut(Vector3 target)
        {
            animating = true;
            Vector3 flat = target - home;
            flat.y = 0f;
            Quaternion look = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : homeRot;

            // Zum Ziel drehen
            float t = 0f;
            Quaternion startRot = transform.rotation;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.15f;
                transform.rotation = Quaternion.Slerp(startRot, look, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            float attackLen = anim != null ? anim.Play(MonsterAnimator.AttackKeys, false) : 0f;
            StartTrail();

            Vector3 end = Vector3.Lerp(home, target, hasModel ? 0.62f : 0.75f);
            end.y = home.y;
            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.28f;
                float k = Mathf.Clamp01(t);
                Vector3 p = Vector3.Lerp(home, end, k * k);
                p.y += Mathf.Sin(k * Mathf.PI) * (hasModel ? 0.45f : 0.8f);
                transform.position = p;
                yield return null;
            }

            if (attackLen > 0f) yield return new WaitForSeconds(Mathf.Min(attackLen * 0.35f, 0.3f));
        }

        /// <summary>Zurück an den Platz und wieder zur Kamera drehen.</summary>
        public IEnumerator LungeBack()
        {
            Vector3 start = transform.position;
            Quaternion startRot = transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                transform.position = Vector3.Lerp(start, home, k);
                transform.rotation = Quaternion.Slerp(startRot, homeRot, k);
                yield return null;
            }
            transform.position = home;
            transform.rotation = homeRot;
            StopTrail();
            if (anim != null && !IsDead) anim.PlayIdle();
            animating = false;
        }

        GameObject trailGo;

        void StartTrail()
        {
            if (trailGo != null) return;
            trailGo = new GameObject("Spur");
            trailGo.transform.SetParent(transform, false);
            trailGo.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
            var tr = trailGo.AddComponent<TrailRenderer>();
            tr.sharedMaterial = Fx.AdditiveMaterial;
            tr.time = 0.22f;
            tr.widthMultiplier = Height * 0.55f;
            tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            Color c = (card.rarity == Rarity.Common ? card.color : CardData.RarityColor(card.rarity)) * 2f;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
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

        public IEnumerator HitFlash()
        {
            bool wasAnimating = animating;
            animating = true;
            float hitLen = anim != null ? anim.Play(MonsterAnimator.HitKeys, false) : 0f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                float shake = (1f - t) * 0.12f;
                transform.position = home + new Vector3(Random.Range(-shake, shake), 0f, Random.Range(-shake, shake));
                SetTint(new Color(1f, 0.3f, 0.3f), 1f - Mathf.Clamp01(t));
                yield return null;
            }
            SetTint(Color.white, 0f);
            transform.position = home;

            if (hitLen > 0.35f) yield return new WaitForSeconds(hitLen - 0.35f);
            if (anim != null && !IsDead) anim.PlayIdle();
            animating = wasAnimating;
        }

        public IEnumerator HealFlash()
        {
            Fx.Burst(transform.position + Vector3.up * Height * 0.5f, new Color(0.3f, 2f, 0.6f), 20, 2f, 0.15f, 0.8f, -0.5f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.5f;
                SetTint(new Color(0.5f, 1f, 0.6f), 1f - Mathf.Clamp01(t));
                yield return null;
            }
            SetTint(Color.white, 0f);
        }

        public IEnumerator Die()
        {
            animating = true;
            float deathLen = anim != null ? anim.Play(MonsterAnimator.DeathKeys, false) : 0f;
            if (deathLen > 0f) yield return new WaitForSeconds(Mathf.Min(deathLen, 1.3f));

            Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.8f, 0.8f, 0.9f), 30, 2.5f, 0.3f, 0.9f, -0.2f);
            float t = 0f;
            Vector3 startScale = transform.localScale;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                transform.localScale = startScale * (1f - Mathf.Clamp01(t));
                yield return null;
            }
            Destroy(gameObject);
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
