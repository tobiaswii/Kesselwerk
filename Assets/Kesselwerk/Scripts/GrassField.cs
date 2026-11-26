using System.Collections.Generic;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Dichtes, im Wind wogendes Gras (GPU-Instancing, ein Zeichenaufruf je 1023 Halme).
    /// Farbe und Dichte je Region; die Kampfplätze bleiben etwas freier.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        Mesh blade;
        Material mat;
        readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();

        public static GrassField Create(int region, bool battle)
        {
            var go = new GameObject("Gras");
            var g = go.AddComponent<GrassField>();
            g.Build(region, battle);
            return g;
        }

        void Build(int region, bool battle)
        {
            var shader = Resources.Load<Shader>("Shaders/Gras");
            if (shader == null) shader = Shader.Find("Kesselwerk/Gras");
            if (shader == null) { enabled = false; return; }
            mat = new Material(shader) { enableInstancing = true };
            Color bottom, top; int count; float height;
            switch (region)
            {
                case 1: bottom = new Color(0.16f, 0.09f, 0.06f); top = new Color(0.55f, 0.36f, 0.2f); count = 2500; height = 0.32f; break;
                case 2: bottom = new Color(0.22f, 0.32f, 0.38f); top = new Color(0.62f, 0.74f, 0.82f); count = 1600; height = 0.2f; break;
                case 3: bottom = new Color(0.1f, 0.06f, 0.16f); top = new Color(0.45f, 0.32f, 0.7f); count = 5000; height = 0.38f; break;
                default: bottom = new Color(0.1f, 0.26f, 0.08f); top = new Color(0.58f, 0.82f, 0.3f); count = 9000; height = 0.42f; break;
            }
            mat.SetColor("_Bottom", bottom);
            mat.SetColor("_Top", top);
            mat.SetFloat("_Wind", region == 2 ? 0.2f : 0.38f);
            blade = BladeMesh();

            var rnd = new System.Random(1234 + region);
            var list = new List<Matrix4x4>(count);
            for (int i = 0; i < count * 3 && list.Count < count; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1) * 15f;
                float z = (float)(rnd.NextDouble() * 2 - 1) * 12f + 2f;
                if (z < -6.5f && Mathf.Abs(x) < 6f) continue; // direkt vor der Kamera frei
                float hMul = 1f;
                if (battle && Mathf.Abs(x) < 4.8f && Mathf.Abs(z) < 4.6f) { if (rnd.NextDouble() < 0.55) continue; hMul = 0.55f; }
                if (!battle && Mathf.Abs(x) < 1.6f && z < 3.5f && z > -4.5f) continue; // Weg zum Kessel
                float h = height * hMul * (0.65f + (float)rnd.NextDouble() * 0.7f);
                float w = 0.05f + (float)rnd.NextDouble() * 0.04f;
                var rot = Quaternion.Euler((float)(rnd.NextDouble() * 16 - 8), (float)(rnd.NextDouble() * 360), (float)(rnd.NextDouble() * 16 - 8));
                list.Add(Matrix4x4.TRS(new Vector3(x, 0f, z), rot, new Vector3(w, h, w)));
            }
            for (int i = 0; i < list.Count; i += 1023)
                batches.Add(list.GetRange(i, Mathf.Min(1023, list.Count - i)).ToArray());
        }

        static Mesh BladeMesh()
        {
            // Spitz zulaufender, leicht gebogener Halm (Höhe 1, Breite 1), UV.y = Höhe
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int seg = 4;
            for (int i = 0; i <= seg; i++)
            {
                float h = i / (float)seg;
                float w = (1 - h) * 0.5f;
                float bend = h * h * 0.25f;
                v.Add(new Vector3(-w, h, bend)); v.Add(new Vector3(w, h, bend));
                uv.Add(new Vector2(0, h)); uv.Add(new Vector2(1, h));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            var m = new Mesh { name = "Halm" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 60f);
            return m;
        }

        void Update()
        {
            if (mat == null) return;
            var rp = new RenderParams(mat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = true };
            foreach (var b in batches) Graphics.RenderMeshInstanced(rp, blade, 0, b);
        }
    }
}
