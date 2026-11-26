using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kesselwerk
{
    /// <summary>
    /// Baut Licht, Himmel, Nebel, Post-Processing und die Arena:
    /// eine Waldlichtung mit Bäumen, Büschen, Blumen und Steinen (Stylized Nature MegaKit).
    /// </summary>
    public static class ArenaBuilder
    {
        public const int SlotCount = 6;

        /// <summary>Tiefenunschärfe – wird von der Kamera passend zum Abstand eingestellt.</summary>
        public static DepthOfField Dof;
        public static ColorAdjustments Grade;
        public static WhiteBalance Balance;

        public static void FocusAt(float distance)
        {
            if (Dof == null) return;
            Dof.gaussianStart.Override(distance + 8f);
            Dof.gaussianEnd.Override(distance + 35f);
        }
        const float ColSpacing = 2.5f;
        const float FrontZ = 1.7f;
        const float RowGap = 2.2f;

        /// <summary>Formation: Feld 0–2 vordere Reihe (zur Mitte), 3–5 hintere Reihe.</summary>
        public static Vector3 SlotPos(bool isPlayer, int slot)
        {
            int col = slot % 3, row = slot / 3;
            float z = FrontZ + row * RowGap;
            return new Vector3((col - 1) * ColSpacing, 0f, isPlayer ? -z : z);
        }

        /// <summary>Mitte der Lichtung (Menü-Kessel).</summary>
        public static Vector3 FacePos(bool isPlayer) =>
            new Vector3(0f, 0.6f, isPlayer ? -4.1f : 4.1f);

        // ------------------------------------------------------------------
        // Licht, Himmel, Kamera, Post-Processing
        // ------------------------------------------------------------------

        /// <summary>Stimmung je Region (Biom): Licht, Nebel und Farbfilter.</summary>
        public static void ApplyRegionMood(int region)
        {
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            Color filter = Color.white, fog = RenderSettings.fogColor, sunCol = sun != null ? sun.color : Color.white;
            float temp = 12f, sunI = 1.5f, fogStart = 20f, fogEnd = 65f;
            switch (region)
            {
                case 1: // Vulkan
                    filter = new Color(1f, 0.93f, 0.87f); fog = new Color(0.5f, 0.3f, 0.24f); sunCol = new Color(1f, 0.82f, 0.68f);
                    temp = 18f; sunI = 1.3f; fogStart = 12f; fogEnd = 45f;
                    RenderSettings.ambientSkyColor = new Color(0.7f, 0.42f, 0.35f);
                    RenderSettings.ambientEquatorColor = new Color(0.65f, 0.4f, 0.3f);
                    break;
                case 2: // Eis
                    filter = new Color(0.85f, 0.95f, 1.05f); fog = new Color(0.85f, 0.92f, 1f); sunCol = new Color(0.9f, 0.95f, 1f);
                    temp = -25f; sunI = 1.6f; fogStart = 10f; fogEnd = 40f;
                    RenderSettings.ambientSkyColor = new Color(0.75f, 0.85f, 1f);
                    RenderSettings.ambientEquatorColor = new Color(0.8f, 0.85f, 0.95f);
                    break;
                case 3: // Schatten
                    filter = new Color(0.88f, 0.84f, 0.98f); fog = new Color(0.18f, 0.12f, 0.28f); sunCol = new Color(0.8f, 0.75f, 1f);
                    temp = -10f; sunI = 0.9f; fogStart = 8f; fogEnd = 38f;
                    RenderSettings.ambientSkyColor = new Color(0.35f, 0.3f, 0.55f);
                    RenderSettings.ambientEquatorColor = new Color(0.3f, 0.25f, 0.4f);
                    RenderSettings.ambientGroundColor = new Color(0.15f, 0.12f, 0.2f);
                    break;
            }
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            if (sun != null) { sun.color = sunCol; sun.intensity = sunI; }
            if (Grade != null) Grade.colorFilter.Override(filter);
            if (Balance != null) Balance.temperature.Override(temp);
            if (RenderSettings.skybox != null && region > 0)
                RenderSettings.skybox.SetColor("_SkyTint", Color.Lerp(new Color(0.5f, 0.65f, 1f), fog, 0.6f));
        }

        public static Camera SetupCameraAndLight()
        {
            var sky = Shader.Find("Skybox/Procedural");
            if (sky != null)
            {
                var skyMat = new Material(sky);
                skyMat.SetFloat("_SunSize", 0.04f);
                skyMat.SetFloat("_AtmosphereThickness", 0.65f);
                skyMat.SetFloat("_Exposure", 1.3f);
                skyMat.SetColor("_SkyTint", new Color(0.5f, 0.65f, 1f));
                skyMat.SetColor("_GroundColor", new Color(0.55f, 0.62f, 0.5f));
                RenderSettings.skybox = skyMat;
            }

            // Warmes, weiches Umgebungslicht mit leicht bläulichen Schatten
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.7f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.36f, 0.26f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.92f);
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 65f;

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sonne").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.68f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;
            cam.rect = new Rect(0f, 0f, 1f, 1f);

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                data.renderShadows = true;
            }

            SetupPostProcessing();
            return cam;
        }

        static void SetupPostProcessing()
        {
            var go = new GameObject("Kesselwerk Post-Processing");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.7f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);

            var color = profile.Add<ColorAdjustments>(true);
            Grade = color;
            color.postExposure.Override(0.2f);
            color.contrast.Override(8f);
            color.saturation.Override(22f);

            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(12f);
            Balance = wb;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            // Hintergrund weich verschwimmen lassen (gemalter Look)
            var dof = profile.Add<DepthOfField>(true);
            Dof = dof;
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(17f);
            dof.gaussianEnd.Override(34f);
            dof.gaussianMaxRadius.Override(0.8f);
            dof.highQualitySampling.Override(true);
        }

        // ------------------------------------------------------------------
        // Arena
        // ------------------------------------------------------------------

        static readonly List<Vector3> blocked = new List<Vector3>();

        static bool Free(Vector3 p, float radius)
        {
            foreach (var b in blocked)
                if ((new Vector2(p.x - b.x, p.z - b.z)).magnitude < radius + b.y) return false;
            return true;
        }

        static void Block(Vector3 p, float radius) => blocked.Add(new Vector3(p.x, radius, p.z));

        public static void BuildArena(bool battle = true)
        {
            var root = new GameObject("Arena").transform;
            blocked.Clear();

            // Boden
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.name = "Boden";
            ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            var gm = ground.GetComponent<Renderer>().material;
            var gtex = NatureKit.GroundTexture();
            if (gm.HasProperty("_BaseMap")) { gm.SetTexture("_BaseMap", gtex); gm.SetTextureScale("_BaseMap", new Vector2(14f, 14f)); }
            gm.mainTexture = gtex;
            gm.mainTextureScale = new Vector2(14f, 14f);
            if (gm.HasProperty("_Smoothness")) gm.SetFloat("_Smoothness", 0f);


            if (battle)
            {
                // Formation freihalten und mit leuchtenden Rune-Ringen markieren
                for (int i = 0; i < SlotCount; i++) { Block(SlotPos(true, i), 1.1f); Block(SlotPos(false, i), 1.1f); }
                for (float x = -4f; x <= 4.01f; x += 0.5f) Block(new Vector3(x, 0f, 0f), 0.8f);
                for (int i = 0; i < SlotCount; i++)
                {
                    SlotPad(root, SlotPos(true, i), new Color(0.35f, 0.75f, 1f));
                    SlotPad(root, SlotPos(false, i), new Color(1f, 0.55f, 0.35f));
                }
                // Mittellinie aus Steinen
                for (float x = -4.5f; x <= 4.51f; x += 0.9f)
                {
                    var p = new Vector3(x, 0f, Random.Range(-0.1f, 0.1f));
                    NatureKit.Spawn(Pick("RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3"), root, p, 0.6f);
                }
            }
            else
            {
                // Menü: ein Kessel in der Mitte, Pfad dorthin
                Block(Vector3.zero, 1.8f);
                for (int i = 0; i < 4; i++) Block(new Vector3(-1.8f + i * 1.2f, 0f, 1.4f), 1f);
                Kessel(root, new Vector3(0f, 0.6f, 2.6f), new Color(1f, 0.6f, 0.25f));
                Block(new Vector3(0f, 0f, 2.6f), 1.6f);
                for (float z = -4.0f; z <= 1.0f; z += 0.85f)
                {
                    var p = new Vector3(Random.Range(-0.15f, 0.15f), 0f, z);
                    NatureKit.Spawn(Pick("RockPath_Round_Small_1", "RockPath_Round_Small_2", "RockPath_Round_Small_3"), root, p, 0.75f);
                }
            }

            // Baumring um die Lichtung (vorne zur Kamera hin frei lassen)
            string[] trees = { "CommonTree_1", "CommonTree_2", "CommonTree_3", "CommonTree_4", "CommonTree_5", "Pine_1", "Pine_2", "Pine_3", "TwistedTree_1", "TwistedTree_2" };
            for (int i = 0; i < 46; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(8.5f, 17f);
                var p = new Vector3(Mathf.Cos(a) * r * 1.1f, 0f, Mathf.Sin(a) * r);
                if (p.z < -5f && Mathf.Abs(p.x) < 9f) continue; // nicht vor die Kamera
                if (!Free(p, 1.6f)) continue;
                Block(p, 1.6f);
                NatureKit.Spawn(trees[Random.Range(0, trees.Length)], root, p, Random.Range(4.5f, 7.5f));
            }

            // Büsche und große Pflanzen am Rand der Lichtung
            string[] bushes = { "Bush_Common", "Bush_Common_Flowers", "Plant_7_Big", "Fern_1", "Plant_1" };
            for (int i = 0; i < 60; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(6.0f, 9.5f);
                var p = new Vector3(Mathf.Cos(a) * r * 1.1f, 0f, Mathf.Sin(a) * r);
                if (p.z < -6.5f && Mathf.Abs(p.x) < 6f) continue;
                if (!Free(p, 0.7f)) continue;
                Block(p, 0.7f);
                NatureKit.Spawn(bushes[Random.Range(0, bushes.Length)], root, p, Random.Range(1.0f, 1.8f));
            }

            // Felsen
            string[] rocks = { "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3" };
            for (int i = 0; i < 14; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(5.8f, 8f);
                var p = new Vector3(Mathf.Cos(a) * r * 1.1f, 0f, Mathf.Sin(a) * r);
                if (p.z < -6f && Mathf.Abs(p.x) < 5f) continue;
                if (!Free(p, 0.8f)) continue;
                Block(p, 0.8f);
                NatureKit.Spawn(rocks[Random.Range(0, rocks.Length)], root, p, Random.Range(0.8f, 1.6f));
            }

            // Gras, Blumen, Klee, Pilze und Kiesel auf der ganzen Fläche (nicht auf den Plätzen)
            string[] small = { "Grass_Common_Short", "Grass_Common_Short", "Grass_Common_Short", "Grass_Common_Tall", "Grass_Wispy_Tall", "Grass_Wispy_Tall",
                               "Clover_1", "Flower_3_Single", "Flower_3_Single", "Flower_3_Group", "Flower_4_Group", "Mushroom_Common", "Pebble_Round_1", "Pebble_Round_2" };
            for (int i = 0; i < 260; i++)
            {
                var p = new Vector3(Random.Range(-11f, 11f), 0f, Random.Range(-7f, 12f));
                if (!Free(p, 0.15f)) continue;
                string n = small[Random.Range(0, small.Length)];
                float h = n.StartsWith("Pebble") ? 0.18f : n.StartsWith("Mushroom") ? Random.Range(0.25f, 0.4f)
                        : n.Contains("Group") || n.StartsWith("Clover") ? Random.Range(0.5f, 0.8f) : Random.Range(0.3f, 0.6f);
                NatureKit.Spawn(n, root, p, h);
            }

            // Ein paar Pilz-Gruppen als Farbtupfer
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-4.5f, 7f));
                if (!Free(p, 0.4f)) continue;
                NatureKit.Spawn("Mushroom_Laetiporus", root, p, Random.Range(0.35f, 0.5f));
            }

            // Hügel im Hintergrund (verschwinden im Nebel)
            for (int i = 0; i < 6; i++)
            {
                var hill = Prim(PrimitiveType.Sphere, root, new Vector3(-30f + i * 12f + Random.Range(-3f, 3f), -4f, 32f + Random.Range(-4f, 6f)),
                                new Vector3(Random.Range(20f, 30f), Random.Range(10f, 16f), 16f), new Color(0.38f, 0.55f, 0.32f));
                hill.name = "Huegel";
            }
        }

        static string Pick(params string[] names) => names[Random.Range(0, names.Length)];

        static void SlotPad(Transform root, Vector3 pos, Color glow)
        {
            var ring = Prim(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.02f, pos.z), new Vector3(1.9f, 0.01f, 1.9f), glow * 0.5f);
            Emissive(ring, glow * 0.8f);
            Prim(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.025f, pos.z), new Vector3(1.72f, 0.012f, 1.72f), new Color(0.5f, 0.55f, 0.36f));
            var inner = Prim(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.03f, pos.z), new Vector3(1.2f, 0.012f, 1.2f), glow * 0.4f);
            Emissive(inner, glow * 0.35f);
            Prim(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.035f, pos.z), new Vector3(1.08f, 0.012f, 1.08f), new Color(0.55f, 0.6f, 0.4f));
        }

        static void Kessel(Transform root, Vector3 pos, Color glow)
        {
            // Steinkreis + Holzscheite + Kessel
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                NatureKit.Spawn("Pebble_Round_" + (1 + i % 3), root, new Vector3(pos.x + Mathf.Cos(a) * 1.05f, 0f, pos.z + Mathf.Sin(a) * 1.05f), 0.35f);
            }
            var pot = Prim(PrimitiveType.Sphere, root, new Vector3(pos.x, 0.6f, pos.z), new Vector3(1.4f, 1.0f, 1.4f), new Color(0.14f, 0.13f, 0.15f));
            pot.name = "Kessel";
            var mat = pot.GetComponent<Renderer>().material;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
            Prim(PrimitiveType.Cylinder, root, new Vector3(pos.x, 1.02f, pos.z), new Vector3(1.25f, 0.07f, 1.25f), new Color(0.2f, 0.19f, 0.21f));
            var brew = Prim(PrimitiveType.Cylinder, root, new Vector3(pos.x, 1.08f, pos.z), new Vector3(1.08f, 0.02f, 1.08f), glow);
            Emissive(brew, glow * 2.2f);
            Fx.Bubbles(new Vector3(pos.x, 1.1f, pos.z), glow * 2.5f, 0.45f);
            Fx.Bubbles(new Vector3(pos.x, 0.05f, pos.z), new Color(2.5f, 1.1f, 0.3f), 0.55f);
        }

        static void Emissive(GameObject go, Color c)
        {
            var m = go.GetComponent<Renderer>().material;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var m = go.GetComponent<Renderer>().material;
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            return go;
        }
    }
}
