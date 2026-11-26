using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Hauptmenü: dieselbe Waldlichtung, deine Monster laufen herum, die Kamera kreist langsam.
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        Camera cam;
        MenuUI ui;
        float angle = -90f;
        readonly List<Creature> pals = new List<Creature>();

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.AutoRotation;

            cam = ArenaBuilder.SetupCameraAndLight();
            ArenaBuilder.BuildArena();
            SpawnPals();

            ui = gameObject.AddComponent<MenuUI>();
            ui.Init(this, cam);
        }

        /// <summary>Zeigt bis zu 4 Monster aus deinem Deck auf der Lichtung.</summary>
        public void SpawnPals()
        {
            foreach (var p in pals) if (p != null) Destroy(p.gameObject);
            pals.Clear();

            var cards = new List<CardData>();
            foreach (var id in PlayerProfile.Current.deck)
            {
                var c = CardDatabase.Get(id);
                if (c != null && !cards.Contains(c)) cards.Add(c);
            }
            cards.Sort((a, b) => b.rarity != a.rarity ? b.rarity.CompareTo(a.rarity) : b.cost.CompareTo(a.cost));

            Vector3[] spots = { new Vector3(-0.9f, 0f, 0.2f), new Vector3(1.0f, 0f, 0.6f), new Vector3(-2.6f, 0f, 1.4f), new Vector3(2.7f, 0f, 1.6f) };
            for (int i = 0; i < Mathf.Min(4, cards.Count); i++)
            {
                var c = Creature.Spawn(cards[i], spots[i], true, i);
                pals.Add(c);
                StartCoroutine(c.SpawnAnim());
            }
        }

        void LateUpdate()
        {
            if (cam == null) return;
            angle += Time.deltaTime * 3f;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 target = new Vector3(0f, 0.8f, 0.6f);
            float radius = 9.5f;
            cam.transform.position = target + new Vector3(Mathf.Cos(rad) * radius, 4.2f, Mathf.Sin(rad) * radius);
            cam.transform.LookAt(target);

            cam.ResetProjectionMatrix();
            var proj = cam.projectionMatrix;
            proj.m12 = -0.12f; // Monster etwas nach oben, Menü unten
            cam.projectionMatrix = proj;

            // Monster schauen grob zur Kamera
            foreach (var p in pals)
            {
                if (p == null) continue;
                Vector3 d = cam.transform.position - p.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    p.transform.rotation = Quaternion.Slerp(p.transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 2f);
            }
        }
    }
}
