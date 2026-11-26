using UnityEditor;
using UnityEngine;

namespace Kesselwerk.EditorTools
{
    /// <summary>
    /// Projekteinstellungen, die das Spiel braucht: Beim Start des Spielmodus wird der Code neu geladen,
    /// damit zur Laufzeit erzeugte Materialien und Texturen nicht aus einer alten Sitzung "hängen bleiben"
    /// (sonst erscheinen Pflanzen pink).
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        static ProjectSetup()
        {
            if (EditorSettings.enterPlayModeOptionsEnabled)
            {
                EditorSettings.enterPlayModeOptionsEnabled = false;
                Debug.Log("Kesselwerk: Domain-Reload beim Spielstart wieder eingeschaltet.");
            }
        }
    }
}
