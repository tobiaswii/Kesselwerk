using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kesselwerk
{
    /// <summary>
    /// Baut Licht, Himmel, Nebel, Post-Processing und die Arena (schwebende Insel) im Code.
    /// </summary>
    public static class ArenaBuilder
    {
        public const int SlotCount = 4;
        const float SlotSpacing = 2.25f;
        const float RowZ = 1.65f;

        public static Vector3 SlotPos(bool isPlayer, int i) =>
            new Vector3((i - (SlotCount - 1) * 0.5f) * SlotSpacing, 0f, isPlayer ? -RowZ : RowZ);

        public static Vector3 FacePos(bool isPlayer) =>
            new Vector3(0f, 0.6f, isPlayer ? -4.1f : 4.1f);

        public static Camera SetupCameraAndLight()
        {
            // Himmel
            var sky = Shader.Find("Skybox/Procedural");
            if (sky != null)
            {
                var skyMat = new Material(sky);
                skyMat.SetFloat("_SunSize", 0.03f);
                skyMat.SetFloat("_AtmosphereThickness", 0.75f);
                skyMat.SetFloat("_Exposure", 1.25f);
                skyMat.SetColor("_SkyTint", new Color(0.45f, 0.6f, 0.95f));
                skyMat.SetColor("_GroundColor", new Color(0.25f, 0.3f, 0.35f));
                RenderSettings.skybox = skyMat;
            }

            // Umgebungslicht (drei Farben: Himmel, Horizont, Boden)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.22f, 0.18f);

            // Nebel für Tiefe
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.72f, 0.88f);
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 45f;

            // Sonne
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sonne").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.transform.rotation = Quaternion.Euler(48f, -25f, 0f);

            // Hintergrund-Kamera (füllt die Flächen hinter der Oberfläche)
            var bg = new GameObject("BG Kamera").AddComponent<Camera>();
            bg.depth = -100;
            bg.clearFlags = CameraClearFlags.SolidColor;
            bg.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
            bg.cullingMask = 0;

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.depth = 0;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 42f;
            cam.farClipPlane = 120f;

            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
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
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.8f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);

            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.15f);
            color.contrast.Override(12f);
            color.saturation.Override(18f);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
        }

        public static void BuildArena()
        {
            var root = new GameObject("Arena").transform;

            // Schwebende Insel: Felssockel + Grasdecke
            var rock = Shape(PrimitiveType.Cylinder, root, new Vector3(0f, -0.75f, 0f), new Vector3(11.5f, 0.7f, 12.5f), new Color(0.42f, 0.36f, 0.32f), 0.05f);
            Shape(PrimitiveType.Cylinder, root, new Vector3(0f, -1.6f, 0f), new Vector3(9.5f, 0.6f, 10.5f), new Color(0.35f, 0.3f, 0.27f), 0.05f);
            Shape(PrimitiveType.Cylinder, root, new Vector3(0f, -2.3f, 0f), new Vector3(6.5f, 0.6f, 7f), new Color(0.3f, 0.26f, 0.24f), 0.05f);
            Shape(PrimitiveType.Cylinder, root, new Vector3(0f, -0.08f, 0f), new Vector3(11.2f, 0.08f, 12.2f), new Color(0.38f, 0.62f, 0.3f), 0.15f);
            rock.name = "Fels";

            // Steinpfad in der Mitte
            for (int i = -4; i <= 4; i++)
            {
                var stone = Shape(PrimitiveType.Cylinder, root, new Vector3(i * 1.15f + Random.Range(-0.1f, 0.1f), 0.01f, Random.Range(-0.12f, 0.12f)),
                                  new Vector3(0.75f, 0.03f, 0.55f), new Color(0.62f, 0.6f, 0.56f), 0.2f);
                stone.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            }

            // Felsen am Rand
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                var p = new Vector3(Mathf.Cos(a) * 5.4f, 0.05f, Mathf.Sin(a) * 5.9f);
                if (Mathf.Abs(p.z) > 4.6f && Mathf.Abs(p.x) < 1.8f) continue; // Platz für die Kessel
                float s = Random.Range(0.35f, 0.8f);
                var r = Shape(PrimitiveType.Cube, root, p, new Vector3(s, s * Random.Range(0.6f, 1.2f), s), new Color(0.5f, 0.47f, 0.45f) * Random.Range(0.85f, 1.1f), 0.1f);
                r.transform.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            }

            // Ein paar Bäume am Rand (Stamm + Krone)
            Vector3[] trees = { new Vector3(-5.2f, 0f, 3.2f), new Vector3(5.0f, 0f, 3.6f), new Vector3(-5.4f, 0f, -2.0f), new Vector3(5.3f, 0f, -2.6f), new Vector3(-3.8f, 0f, 5.0f), new Vector3(3.9f, 0f, 5.1f) };
            foreach (var t in trees) Tree(root, t, Random.Range(0.8f, 1.2f));

            // Plätze für Monster: leuchtende Ringe
            for (int i = 0; i < SlotCount; i++)
            {
                SlotPad(root, SlotPos(true, i), new Color(0.25f, 0.6f, 1f));
                SlotPad(root, SlotPos(false, i), new Color(1f, 0.35f, 0.3f));
            }

            // Kessel als "Lebenspunkte" der Spieler
            Kessel(root, FacePos(true), new Color(0.3f, 0.65f, 1f));
            Kessel(root, FacePos(false), new Color(1f, 0.35f, 0.25f));
        }

        static void Tree(Transform root, Vector3 pos, float scale)
        {
            var trunk = Shape(PrimitiveType.Cylinder, root, pos + new Vector3(0f, 0.6f * scale, 0f), new Vector3(0.22f, 0.6f, 0.22f) * scale, new Color(0.4f, 0.28f, 0.18f), 0.05f);
            trunk.name = "Baum";
            Color leaf = new Color(0.25f, 0.55f, 0.25f) * Random.Range(0.9f, 1.15f);
            Shape(PrimitiveType.Sphere, root, pos + new Vector3(0f, 1.55f * scale, 0f), new Vector3(1.2f, 1.0f, 1.2f) * scale, leaf, 0.1f);
            Shape(PrimitiveType.Sphere, root, pos + new Vector3(0.3f, 2.1f * scale, 0.1f), new Vector3(0.8f, 0.7f, 0.8f) * scale, leaf * 1.1f, 0.1f);
        }

        static void SlotPad(Transform root, Vector3 pos, Color glow)
        {
            Shape(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.02f, pos.z), new Vector3(1.75f, 0.02f, 1.75f), new Color(0.24f, 0.4f, 0.22f), 0.2f);
            var ring = Shape(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.035f, pos.z), new Vector3(1.85f, 0.01f, 1.85f), glow * 0.6f, 0.5f);
            Emissive(ring, glow * 0.9f);
            Shape(PrimitiveType.Cylinder, root, new Vector3(pos.x, 0.045f, pos.z), new Vector3(1.6f, 0.012f, 1.6f), new Color(0.26f, 0.42f, 0.24f), 0.2f);
        }

        static void Kessel(Transform root, Vector3 pos, Color glow)
        {
            var pot = Shape(PrimitiveType.Sphere, root, new Vector3(pos.x, 0.55f, pos.z), new Vector3(1.6f, 1.1f, 1.6f), new Color(0.16f, 0.16f, 0.18f), 0.6f);
            pot.name = "Kessel";
            Shape(PrimitiveType.Cylinder, root, new Vector3(pos.x, 1.0f, pos.z), new Vector3(1.45f, 0.08f, 1.45f), new Color(0.2f, 0.2f, 0.22f), 0.7f);
            var brew = Shape(PrimitiveType.Cylinder, root, new Vector3(pos.x, 1.06f, pos.z), new Vector3(1.25f, 0.02f, 1.25f), glow, 0.8f);
            Emissive(brew, glow * 2.2f);
            // Feuer darunter
            Fx.Bubbles(new Vector3(pos.x, 1.08f, pos.z), glow * 2.5f, 0.5f);
            Fx.Bubbles(new Vector3(pos.x, 0.05f, pos.z), new Color(2.5f, 1.1f, 0.3f), 0.6f);
        }

        static void Emissive(GameObject go, Color c)
        {
            var m = go.GetComponent<Renderer>().material;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
        }

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, float smoothness)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var m = go.GetComponent<Renderer>().material;
            m.color = color;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return go;
        }
    }
}
