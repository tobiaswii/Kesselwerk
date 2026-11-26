using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Startet das Spiel automatisch, sobald in Unity auf Play gedrückt wird –
    /// egal welche Szene offen ist.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (!TextMeshProReady())
            {
                Debug.LogError("Kesselwerk: Die TextMeshPro-Grundressourcen fehlen. Bitte in Unity oben im Menü " +
                               "'Window > TextMeshPro > Import TMP Essential Resources' klicken und danach erneut auf Play drücken.");
                return;
            }

            if (Object.FindFirstObjectByType<BattleManager>() == null)
                new GameObject("BattleManager").AddComponent<BattleManager>();
        }

        static bool TextMeshProReady()
        {
            try
            {
                return TMPro.TMP_Settings.instance != null && TMPro.TMP_Settings.defaultFontAsset != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
