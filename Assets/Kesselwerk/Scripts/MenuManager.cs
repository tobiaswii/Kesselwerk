using System.Collections;
using System.Collections.Generic;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// 3D-Hintergrund der Menüs: Waldlichtung mit Kessel, deine Teamkreaturen stehen herum.
    /// Kamera kreist langsam oder zeigt eine Kreatur groß (Vorschau, Beschwörung).
    /// </summary>
    public class MenuManager : MonoBehaviour
    {
        Camera cam;
        MenuUI ui;
        float angle = -90f;
        readonly List<Creature> pals = new List<Creature>();

        Creature preview;
        float previewYaw;
        bool previewMode;
        float previewBlend;
        Vector3 previewSpot = new Vector3(0f, 0f, -3.2f);
        float shakeUntil;

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.AutoRotation;

            cam = ArenaBuilder.SetupCameraAndLight();
            ArenaBuilder.BuildArena(false);
            SpawnPals();

            int crystals = Progress.DailyLogin(Game.Data, Game.Today);
            Game.Save();

            ui = gameObject.AddComponent<MenuUI>();
            ui.Init(this);
            if (crystals > 0) ui.Toast($"Tägliche Belohnung: +{crystals} Kristalle, +200 Gold (Serie {Game.Data.loginStreak})");
            Sound.Music(false);
        }

        /// <summary>Zeigt die Kreaturen deines Teams auf der Lichtung.</summary>
        public void SpawnPals()
        {
            foreach (var p in pals) if (p != null) Destroy(p.gameObject);
            pals.Clear();
            var data = Game.Data;
            Vector3[] spots =
            {
                new Vector3(-1.3f, 0f, 0.4f), new Vector3(1.3f, 0f, 0.6f), new Vector3(-3.2f, 0f, 1.4f),
                new Vector3(3.2f, 0f, 1.6f), new Vector3(-2.2f, 0f, 3.6f), new Vector3(2.4f, 0f, 3.8f)
            };
            int i = 0;
            foreach (var id in data.team)
            {
                var c = data.Get(id);
                if (c == null || i >= spots.Length) continue;
                var v = Creature.Spawn(c.Def, c.stage, spots[i], Vector3.back, true, i);
                pals.Add(v);
                StartCoroutine(v.SpawnAnim(i * 0.1f));
                i++;
            }
        }

        // ------------------------------------------------------------------
        // Vorschau einer einzelnen Kreatur
        // ------------------------------------------------------------------

        public Creature ShowPreview(CreatureDef def, int stage, bool dramatic = false)
        {
            HidePreview();
            preview = Creature.Spawn(def, stage, previewSpot, Vector3.back, true, 0, 1.15f);
            previewYaw = 0f;
            previewMode = true;
            foreach (var p in pals) if (p != null) p.gameObject.SetActive(false);
            StartCoroutine(preview.SpawnAnim(0f));
            if (dramatic) StartCoroutine(SummonReveal(def));
            Sound.Voice(def, 0.8f);
            return preview;
        }

        IEnumerator SummonReveal(CreatureDef def)
        {
            Color rc = Look.RarityColor(def.rarity);
            Color ec = Look.ElementColor(def.element);
            int r = (int)def.rarity;
            Fx.Pillar(previewSpot, rc * 3f, 4f + r, 60 + r * 40);
            Fx.Ring(previewSpot + Vector3.up * 0.1f, rc * 3f);
            if (r >= (int)Rarity.Episch)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    Fx.Lightning(previewSpot + new Vector3(Mathf.Cos(a) * 2.5f, 6f, Mathf.Sin(a) * 2.5f), previewSpot + Vector3.up * 0.8f, rc * 3f);
                }
            }
            if (r >= (int)Rarity.Legendaer)
            {
                shakeUntil = Time.time + 0.6f;
                yield return new WaitForSeconds(0.25f);
                Fx.Burst(previewSpot + Vector3.up * 1.2f, rc * 3f, 140, 7f, 0.35f, 1.2f, 0.3f);
                Fx.Ring(previewSpot + Vector3.up * 0.2f, ec * 3f);
            }
            if (r >= (int)Rarity.Mythisch)
            {
                yield return new WaitForSeconds(0.3f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    Fx.Pillar(previewSpot + new Vector3(Mathf.Cos(a) * 1.8f, 0f, Mathf.Sin(a) * 1.8f), (i % 2 == 0 ? rc : ec) * 3f, 5f, 40);
                }
            }
        }

        public CreatureDef PreviewDef => preview != null ? preview.def : null;
        public int PreviewStage => preview != null ? preview.stage : -1;

        public void RotatePreview(float delta) => previewYaw += delta;

        public void HidePreview()
        {
            if (preview != null) Destroy(preview.gameObject);
            preview = null;
            previewMode = false;
            foreach (var p in pals) if (p != null) p.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (cam == null) return;
            bool portrait = UX.Portrait;
            cam.fieldOfView = portrait ? 52f : 36f;

            previewBlend = Mathf.MoveTowards(previewBlend, previewMode ? 1f : 0f, Time.deltaTime * 2.5f);

            // Kreisende Übersicht
            angle += Time.deltaTime * 3f;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 target = new Vector3(0f, 0.9f, 1.2f);
            float radius = portrait ? 12.5f : 9.5f;
            Vector3 orbitPos = target + new Vector3(Mathf.Cos(rad) * radius, 4.2f, Mathf.Sin(rad) * radius);

            // Vorschau: nah vor der Kreatur, leicht von oben (im Querformat rechts versetzt für das Infofeld)
            float h = preview != null ? preview.Height : 1.5f;
            Vector3 pLook = previewSpot + Vector3.up * h * 0.55f;
            float pDist = (portrait ? 5.2f : 4.4f) + h * 0.9f;
            Vector3 side = portrait ? Vector3.zero : Vector3.left * (h * 1.1f + 0.6f); // Kreatur rechts neben dem Infofeld
            Vector3 previewPos = pLook + side + new Vector3(0f, h * 0.35f + 0.6f, -pDist);
            Vector3 previewLookAt = pLook + side + (portrait ? Vector3.down * h * 0.25f : Vector3.zero);

            float k = Mathf.SmoothStep(0f, 1f, previewBlend);
            Vector3 pos = Vector3.Lerp(orbitPos, previewPos, k);
            Vector3 look = Vector3.Lerp(target, previewLookAt, k);
            if (Time.time < shakeUntil) pos += Random.insideUnitSphere * 0.15f;
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            ArenaBuilder.FocusAt(Vector3.Distance(pos, look));

            cam.ResetProjectionMatrix();
            var proj = cam.projectionMatrix;
            proj.m12 = Mathf.Lerp(-0.12f, portrait ? -0.35f : 0f, k);
            cam.projectionMatrix = proj;

            if (preview != null)
                preview.transform.rotation = Quaternion.Euler(0f, 180f + previewYaw, 0f);

            foreach (var p in pals)
            {
                if (p == null || !p.gameObject.activeSelf) continue;
                Vector3 d = cam.transform.position - p.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f)
                    p.transform.rotation = Quaternion.Slerp(p.transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 2f);
            }
        }
    }
}
