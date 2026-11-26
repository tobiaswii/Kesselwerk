using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kesselwerk
{
    /// <summary>
    /// Wechselt zwischen Menü und Kampf, ohne Szenen laden zu müssen:
    /// alles Erzeugte wird entfernt und der neue Bildschirm aufgebaut.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        static GameFlow instance;

        public static void Go(bool battle)
        {
            if (instance == null)
                instance = new GameObject("GameFlow").AddComponent<GameFlow>();
            instance.StartCoroutine(instance.Switch(battle));
        }

        public static void ToMenu() => Go(false);
        public static void ToBattle() => Go(true);

        IEnumerator Switch(bool battle)
        {
            // Alles außer Kamera, Licht, Szenen-Volume und uns selbst entfernen
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go == gameObject) continue;
                if (go.GetComponent<Camera>() != null && go.CompareTag("MainCamera")) continue;
                var light = go.GetComponent<Light>();
                if (light != null && light.type == LightType.Directional) continue;
                if (go.name == "Global Volume") continue;
                Destroy(go);
            }
            yield return null; // Zerstörung abwarten

            var cam = Camera.main;
            if (cam != null) cam.ResetProjectionMatrix();

            if (battle) new GameObject("BattleManager").AddComponent<BattleManager>();
            else new GameObject("MenuManager").AddComponent<MenuManager>();
        }
    }
}
