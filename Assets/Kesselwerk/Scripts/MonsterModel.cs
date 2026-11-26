using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kesselwerk
{
    /// <summary>
    /// Lädt Monster-Modelle aus Resources/Monsters und rendert Kartenbilder (Porträts).
    /// Die Modelle schauen in ihrer Grundstellung in Richtung +Z.
    /// </summary>
    public static class MonsterModel
    {
        static readonly Dictionary<string, Texture> portraitCache = new Dictionary<string, Texture>();

        public static GameObject Load(CardData card, Transform parent, float targetHeight,
                                      out Animator animator, out AnimationClip[] clips)
        {
            animator = null;
            clips = null;
            if (card == null || string.IsNullOrEmpty(card.model)) return null;

            var prefab = Resources.Load<GameObject>("Monsters/" + card.model);
            if (prefab == null) return null;

            var inst = Object.Instantiate(prefab, parent);
            inst.name = "Modell";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            // Textur sicher zuweisen und Material etwas "weicher" machen
            var tex = Resources.Load<Texture2D>("Monsters/Textures/" + card.model + "_Texture");
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                foreach (var m in r.materials)
                {
                    if (tex != null)
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    }
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
                }
            }

            // Einheitliche Höhe
            Bounds b = CalcBounds(inst);
            if (b.size.y > 0.001f) inst.transform.localScale *= targetHeight / b.size.y;

            // Auf den Boden stellen
            b = CalcBounds(inst);
            inst.transform.position += new Vector3(0f, parent.position.y - b.min.y, 0f);

            animator = inst.GetComponentInChildren<Animator>();
            if (animator == null) animator = inst.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            clips = Resources.LoadAll<AnimationClip>("Monsters/" + card.model);
            return inst;
        }

        public static Bounds CalcBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static AnimationClip FindClip(AnimationClip[] clips, string[] keys)
        {
            if (clips == null) return null;
            foreach (var key in keys)
                foreach (var c in clips)
                {
                    if (c == null || c.name.StartsWith("__preview__")) continue;
                    string n = c.name;
                    int bar = n.LastIndexOf('|');
                    string s = bar >= 0 ? n.Substring(bar + 1) : n;
                    if (s.EndsWith(key, System.StringComparison.OrdinalIgnoreCase)) return c;
                }
            return null;
        }

        /// <summary>Rendert ein Kartenbild des Monsters (einmal pro Karte, danach aus dem Cache).</summary>
        public static Texture Portrait(CardData card)
        {
            if (card == null) return null;
            if (portraitCache.TryGetValue(card.id, out var cached) && cached != null) return cached;

            var stage = new GameObject("PortraitStage");
            stage.transform.position = new Vector3(500f, 500f, 500f);
            var holder = new GameObject("Holder");
            holder.transform.SetParent(stage.transform, false);
            holder.transform.localRotation = Quaternion.Euler(0f, 200f, 0f); // leicht schräg zur Kamera

            var model = Load(card, holder.transform, 1.5f, out _, out var clips);
            Texture result = null;

            if (model != null)
            {
                var idle = FindClip(clips, MonsterAnimator.IdleKeys);
                if (idle != null) idle.SampleAnimation(model, idle.length * 0.3f);

                Bounds b = CalcBounds(model);
                var camGo = new GameObject("PortraitCam");
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.Lerp(card.color, new Color(0.16f, 0.13f, 0.18f), 0.4f);
                cam.fieldOfView = 30f;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 50f;

                var rt = new RenderTexture(256, 320, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                rt.Create();
                cam.targetTexture = rt;
                cam.aspect = 256f / 320f;

                float h = Mathf.Max(0.5f, b.size.y);
                float dist = (h * 0.62f) / Mathf.Tan(15f * Mathf.Deg2Rad);
                Vector3 look = b.center + new Vector3(0f, h * 0.05f, 0f);
                camGo.transform.position = look + new Vector3(0.15f * h, 0.25f * h, -dist);
                camGo.transform.LookAt(look);

                // Eigene Porträt-Lichter (Führungslicht vorne, Randlicht hinten)
                var keyGo = new GameObject("PortraitKey");
                keyGo.transform.SetParent(stage.transform, false);
                keyGo.transform.position = look + new Vector3(-0.6f * h, 0.9f * h, -dist * 0.7f);
                var key = keyGo.AddComponent<Light>();
                key.type = LightType.Point;
                key.color = new Color(1f, 0.95f, 0.88f);
                key.intensity = 6f * h * h;
                key.range = dist * 3f;
                var rimGo = new GameObject("PortraitRim");
                rimGo.transform.SetParent(stage.transform, false);
                rimGo.transform.position = look + new Vector3(0.5f * h, 0.8f * h, dist * 0.6f);
                var rim = rimGo.AddComponent<Light>();
                rim.type = LightType.Point;
                rim.color = Color.Lerp(card.color, Color.white, 0.4f);
                rim.intensity = 5f * h * h;
                rim.range = dist * 3f;

                // In URP über eine Render-Anfrage rendern, sonst klassisch
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                    RenderPipeline.SubmitRenderRequest(cam, request);
                else
                    cam.Render();
                cam.targetTexture = null;
                Object.Destroy(camGo);
                result = rt;
            }

            Object.Destroy(stage);
            portraitCache[card.id] = result;
            return result;
        }
    }
}
