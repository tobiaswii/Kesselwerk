using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Ein Monster in der Arena. Lädt das animierte 3D-Modell aus Resources/Monsters.
    /// Fehlt das Modell, wird ein Platzhalter aus Grundformen gebaut.
    /// </summary>
    public class Creature : MonoBehaviour
    {
        /// <summary>Drehung der Modelle, falls sie mit dem Rücken zur Kamera stehen (in Grad).</summary>
        public static float ModelYawOffset = 0f;

        public CardData card;
        public int attack;
        public int health;
        public int maxHealth;
        public bool isPlayer;
        public bool justSummoned;

        Vector3 home;
        float bobSeed;
        bool animating;
        bool hasModel;
        MonsterAnimator anim;

        readonly List<Material> materials = new List<Material>();
        readonly List<Color> baseColors = new List<Color>();

        public static Creature Spawn(CardData card, Vector3 position, bool isPlayer)
        {
            var root = new GameObject("Kreatur_" + card.name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, isPlayer ? 0f : 180f, 0f);

            var c = root.AddComponent<Creature>();
            c.card = card;
            c.attack = card.attack;
            c.health = card.health;
            c.maxHealth = card.health;
            c.isPlayer = isPlayer;
            c.home = position;
            c.bobSeed = Random.Range(0f, 10f);

            if (!c.TryBuildModel()) c.BuildPrimitiveBody();
            c.CollectMaterials();
            return c;
        }

        // ------------------------------------------------------------------
        // Aussehen
        // ------------------------------------------------------------------

        bool TryBuildModel()
        {
            if (string.IsNullOrEmpty(card.model)) return false;
            var prefab = Resources.Load<GameObject>("Monsters/" + card.model);
            if (prefab == null) return false;

            var inst = Instantiate(prefab, transform);
            inst.name = "Modell";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, ModelYawOffset, 0f);

            // Textur sicherheitshalber selbst zuweisen (falls der Import sie nicht verknüpft hat)
            var tex = Resources.Load<Texture2D>("Monsters/Textures/" + card.model + "_Texture");
            if (tex != null)
            {
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.materials)
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    }
            }

            // Auf einheitliche Größe bringen (stärkere Monster etwas größer)
            float targetHeight = 1.0f + card.cost * 0.12f;
            Bounds b = CalcBounds(inst);
            if (b.size.y > 0.001f)
                inst.transform.localScale *= targetHeight / b.size.y;

            // Auf den Boden stellen
            b = CalcBounds(inst);
            inst.transform.position += new Vector3(0f, transform.position.y - b.min.y, 0f);

            var animator = inst.GetComponentInChildren<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            anim = gameObject.AddComponent<MonsterAnimator>();
            anim.Init(animator, Resources.LoadAll<AnimationClip>("Monsters/" + card.model));
            anim.PlayIdle();

            hasModel = true;
            return true;
        }

        static Bounds CalcBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        void BuildPrimitiveBody()
        {
            float size = 0.7f + card.cost * 0.08f;
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

        void SetTint(float flash)
        {
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null)
                    materials[i].color = Color.Lerp(baseColors[i], new Color(1f, 0.35f, 0.35f), flash);
        }

        // ------------------------------------------------------------------
        // Bewegung und Animationen
        // ------------------------------------------------------------------

        void Update()
        {
            if (animating || hasModel) return;
            float y = Mathf.Sin(Time.time * 2.2f + bobSeed) * 0.06f;
            transform.position = home + new Vector3(0f, y, 0f);
        }

        public IEnumerator SpawnAnim()
        {
            animating = true;
            transform.position = home;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                transform.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t));
                yield return null;
            }
            transform.localScale = Vector3.one;
            animating = false;
        }

        /// <summary>Springt auf das Ziel zu und spielt die Angriffsanimation.</summary>
        public IEnumerator LungeOut(Vector3 target)
        {
            animating = true;
            float attackLen = anim != null ? anim.Play(MonsterAnimator.AttackKeys, false) : 0f;

            Vector3 start = home;
            Vector3 end = Vector3.Lerp(home, target, hasModel ? 0.6f : 0.75f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.25f;
                float k = Mathf.Clamp01(t);
                Vector3 p = Vector3.Lerp(start, end, k * k);
                p.y += Mathf.Sin(k * Mathf.PI) * (hasModel ? 0.4f : 0.8f);
                transform.position = p;
                yield return null;
            }

            // Kurz warten, bis der Biss/Schlag "trifft"
            if (attackLen > 0f) yield return new WaitForSeconds(Mathf.Min(attackLen * 0.4f, 0.35f));
        }

        /// <summary>Kehrt nach dem Angriff an den Platz zurück.</summary>
        public IEnumerator LungeBack()
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                transform.position = Vector3.Lerp(start, home, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
                yield return null;
            }
            transform.position = home;
            if (anim != null && health > 0) anim.PlayIdle();
            animating = false;
        }

        public IEnumerator HitFlash()
        {
            bool wasAnimating = animating;
            animating = true;
            float hitLen = anim != null ? anim.Play(MonsterAnimator.HitKeys, false) : 0f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                float shake = (1f - t) * 0.12f;
                transform.position = home + new Vector3(Random.Range(-shake, shake), 0f, Random.Range(-shake, shake));
                SetTint(1f - Mathf.Clamp01(t));
                yield return null;
            }
            SetTint(0f);
            transform.position = home;

            if (hitLen > 0.3f) yield return new WaitForSeconds(hitLen - 0.3f);
            if (anim != null && health > 0) anim.PlayIdle();
            animating = wasAnimating;
        }

        public IEnumerator Die()
        {
            animating = true;
            float deathLen = anim != null ? anim.Play(MonsterAnimator.DeathKeys, false) : 0f;
            if (deathLen > 0f) yield return new WaitForSeconds(Mathf.Min(deathLen, 1.4f));

            float t = 0f;
            Vector3 startScale = transform.localScale;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.35f;
                transform.localScale = startScale * (1f - Mathf.Clamp01(t));
                if (!hasModel) transform.Rotate(0f, 720f * Time.deltaTime, 0f);
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
