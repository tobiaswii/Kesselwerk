using System.Collections;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Ein Monster in der Arena. Wird zur Laufzeit aus Grundformen gebaut (Platzhalter),
    /// später durch echte animierte Modelle ersetzt.
    /// </summary>
    public class Creature : MonoBehaviour
    {
        public CardData card;
        public int attack;
        public int health;
        public int maxHealth;
        public bool isPlayer;
        public bool justSummoned;

        Vector3 home;
        float bobSeed;
        bool animating;
        Transform body;
        Renderer bodyRenderer;
        Color baseColor;

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
            c.BuildBody();
            return c;
        }

        void BuildBody()
        {
            float size = 0.7f + card.cost * 0.08f;
            PrimitiveType type = card.shape == BodyShape.Sphere ? PrimitiveType.Sphere
                               : card.shape == BodyShape.Cube ? PrimitiveType.Cube
                               : PrimitiveType.Capsule;

            var bodyGo = Prim(type, transform, new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, card.color);
            if (type == PrimitiveType.Capsule) bodyGo.transform.localScale = new Vector3(size, size * 0.5f, size);
            body = bodyGo.transform;
            bodyRenderer = bodyGo.GetComponent<Renderer>();
            baseColor = card.color;

            // Augen (schauen nach vorne = lokale +Z)
            float eyeY = size * 0.65f;
            float eyeZ = size * 0.45f;
            float eyeX = size * 0.2f;
            float eyeSize = size * 0.28f;
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, transform, new Vector3(eyeX * side, eyeY, eyeZ), Vector3.one * eyeSize, Color.white);
                Prim(PrimitiveType.Sphere, transform, new Vector3(eyeX * side, eyeY, eyeZ + eyeSize * 0.35f), Vector3.one * eyeSize * 0.5f, new Color(0.1f, 0.1f, 0.12f));
            }

            // Kleine Hörner ab Kosten 3, damit stärkere Monster bedrohlicher aussehen
            if (card.cost >= 3)
            {
                Color horn = Color.Lerp(card.color, Color.black, 0.45f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var h = Prim(PrimitiveType.Cube, transform, new Vector3(size * 0.25f * side, size * 1.0f, 0f), new Vector3(0.12f, 0.3f, 0.12f) * size, horn);
                    h.transform.localRotation = Quaternion.Euler(0f, 0f, -20f * side);
                }
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

        void Update()
        {
            if (animating) return;
            // Leichtes Wippen im Stand
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
                float s = EaseOutBack(Mathf.Clamp01(t));
                transform.localScale = Vector3.one * s;
                yield return null;
            }
            transform.localScale = Vector3.one;
            animating = false;
        }

        /// <summary>Springt auf das Ziel zu (bis kurz davor).</summary>
        public IEnumerator LungeOut(Vector3 target)
        {
            animating = true;
            Vector3 start = home;
            Vector3 end = Vector3.Lerp(home, target, 0.75f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.22f;
                float k = Mathf.Clamp01(t);
                Vector3 p = Vector3.Lerp(start, end, k * k);
                p.y += Mathf.Sin(k * Mathf.PI) * 0.8f;
                transform.position = p;
                yield return null;
            }
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
            animating = false;
        }

        public IEnumerator HitFlash()
        {
            if (bodyRenderer == null) yield break;
            bool wasAnimating = animating;
            animating = true;
            bodyRenderer.material.color = Color.white;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.25f;
                float shake = (1f - t) * 0.15f;
                transform.position = home + new Vector3(Random.Range(-shake, shake), 0f, Random.Range(-shake, shake));
                bodyRenderer.material.color = Color.Lerp(Color.white, baseColor, t);
                yield return null;
            }
            bodyRenderer.material.color = baseColor;
            transform.position = home;
            animating = wasAnimating;
        }

        public IEnumerator Die()
        {
            animating = true;
            float t = 0f;
            Vector3 startScale = transform.localScale;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.4f;
                transform.localScale = startScale * (1f - Mathf.Clamp01(t));
                transform.Rotate(0f, 720f * Time.deltaTime, 0f);
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
