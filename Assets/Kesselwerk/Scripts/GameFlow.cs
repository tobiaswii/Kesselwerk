using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kesselwerk
{
    public enum MenuPage { Home, Campaign, Summon, Collection, Team, Settings, Arena }

    /// <summary>
    /// Wechselt zwischen Menü und Kampf, ohne Szenen laden zu müssen:
    /// alles Erzeugte wird entfernt und der neue Bildschirm aufgebaut.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        static GameFlow instance;
        public static MenuPage StartPage = MenuPage.Home;

        public static void Go(bool battle)
        {
            if (instance == null)
            {
                instance = new GameObject("GameFlow").AddComponent<GameFlow>();
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.StartCoroutine(instance.Switch(battle));
        }

        public static void ToMenu(MenuPage page = MenuPage.Home)
        {
            StartPage = page;
            Go(false);
        }

        public static void ToBattle() => Go(true);

        /// <summary>Planungsansicht der Kesselschlacht.</summary>
        public static void ToTft()
        {
            if (instance == null)
            {
                instance = new GameObject("GameFlow").AddComponent<GameFlow>();
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.StartCoroutine(instance.Switch(false, true));
        }

        IEnumerator Switch(bool battle, bool tft = false)
        {
            Time.timeScale = 1f;
            // Alles außer Kamera, Licht und Szenen-Volume entfernen
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.GetComponent<Camera>() != null && go.CompareTag("MainCamera")) continue;
                var light = go.GetComponent<Light>();
                if (light != null && light.type == LightType.Directional) continue;
                if (go.name == "Global Volume") continue;
                Destroy(go);
            }
            yield return null; // Zerstörung abwarten

            var cam = Camera.main;
            if (cam != null) cam.ResetProjectionMatrix();

            if (tft) new GameObject("TftManager").AddComponent<TftManager>();
            else if (battle) new GameObject("AutoBattleManager").AddComponent<AutoBattleManager>();
            else new GameObject("MenuManager").AddComponent<MenuManager>();
        }
    }
}
