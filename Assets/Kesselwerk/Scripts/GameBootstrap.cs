using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Startet den Prototyp automatisch, sobald in Unity auf Play gedrückt wird –
    /// egal welche Szene offen ist. Keine Szene muss eingerichtet werden.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<BattleManager>() == null)
                new GameObject("BattleManager").AddComponent<BattleManager>();
        }
    }
}
