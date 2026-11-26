using System;
using Kesselwerk.Core;
using UnityEngine;

namespace Kesselwerk
{
    /// <summary>
    /// Zentraler Spielzustand: lädt und speichert den Spielstand lokal (JSON in PlayerPrefs mit Sicherungskopie).
    /// Später ersetzt bzw. ergänzt ein Server-Backend diese lokale Speicherung (siehe Online/IBackend).
    /// </summary>
    public class ArenaFight
    {
        public TeamSpec enemy;
        public int seed;
        public string opponent;
        public int serverWinner, delta, rating;
        public string rank;
    }

    public static class Game
    {
        const string Key = "kesselwerk_spielstand_v2";
        const string BackupKey = "kesselwerk_spielstand_v2_backup";

        static PlayerData data;

        public static PlayerData Data
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        /// <summary>Stufe, die als Nächstes gekämpft wird.</summary>
        public static StageDef PendingStage;

        /// <summary>Arena-Kampf, den der Server bereits berechnet hat (null = Kampagne).</summary>
        public static ArenaFight PendingArena;

        public static int Today => (int)(DateTime.UtcNow.Date - new DateTime(2026, 1, 1)).TotalDays;

        public static void Load()
        {
            data = TryParse(PlayerPrefs.GetString(Key, "")) ?? TryParse(PlayerPrefs.GetString(BackupKey, ""));
            if (data == null)
            {
                data = PlayerData.NewGame();
                Debug.Log("Kesselwerk: neuer Spielstand angelegt.");
            }
            data.Normalize();
            Progress.RollDay(data, Today);
            Sound.MusicVolume = data.musicVolume;
            Sound.SfxVolume = data.sfxVolume;
        }

        static PlayerData TryParse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var p = JsonUtility.FromJson<PlayerData>(json);
                return p != null && p.creatures != null && p.creatures.Count > 0 ? p : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Kesselwerk: Spielstand beschädigt – " + e.Message);
                return null;
            }
        }

        public static void Save()
        {
            if (data == null) return;
            data.musicVolume = Sound.MusicVolume;
            data.sfxVolume = Sound.SfxVolume;
            string json = JsonUtility.ToJson(data);
            string old = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(old)) PlayerPrefs.SetString(BackupKey, old);
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
            data = PlayerData.NewGame();
            data.Normalize();
            Save();
        }

        public static string Export() => JsonUtility.ToJson(Data);

        /// <summary>Übernimmt einen Spielstand (z. B. aus der Cloud). false = ungültig.</summary>
        public static bool Import(string json)
        {
            var p = TryParse(json);
            if (p == null) return false;
            p.Normalize();
            data = p;
            Save();
            return true;
        }
    }
}
