using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Kesselwerk.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Kesselwerk
{
    // ---------------- Antworten des Servers (Feldnamen wie im JSON) ----------------
    [Serializable] public class OnlineRegister { public string id, token, name; }
    [Serializable] public class OnlineMe { public string id, name, rang, cloudSpeicher; public int wertung, siege, niederlagen, verteidigungSiege, verteidigungNiederlagen, verteidigung, platz; }
    [Serializable] public class OnlineMatch { public string matchId, gegner, rang; public int wertung; public bool bot; public List<UnitSpec> units; }
    [Serializable] public class OnlineFight { public int seed, sieger, ereignisse, aenderung, wertung; public string rang; public List<UnitSpec> gegnerUnits; }
    [Serializable] public class OnlineBoardEntry { public int platz, wertung, siege, niederlagen; public string name, rang; }
    [Serializable] class OnlineBoard { public List<OnlineBoardEntry> items; }
    [Serializable] class TeamBody { public List<UnitSpec> units; }
    [Serializable] class FightBody { public string matchId; public List<UnitSpec> units; }
    [Serializable] class NameBody { public string name; }
    [Serializable] class ErrorBody { public string error; }

    /// <summary>
    /// Verbindung zum Kesselwerk-Server (Homeserver). Konto = Kennung + geheimer Schlüssel auf dem Gerät.
    /// Alle Arena-Kämpfe rechnet der Server; das Spiel zeigt sie nur an.
    /// </summary>
    public class Online : MonoBehaviour
    {
        public const string BaseUrl = "https://kesselwerk.wittmer.id";
        const string KeyId = "kesselwerk_online_id", KeyToken = "kesselwerk_online_token";

        static Online runner;
        static Online R
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("Online");
                    DontDestroyOnLoad(go);
                    runner = go.AddComponent<Online>();
                }
                return runner;
            }
        }

        public static bool HasAccount => !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyId, ""));
        public static OnlineMe Me;

        public static void Register(string name, Action<OnlineRegister> ok, Action<string> fail)
        {
            Send("POST", "/api/v1/register", JsonUtility.ToJson(new NameBody { name = name }), false, json =>
            {
                var r = JsonUtility.FromJson<OnlineRegister>(json);
                PlayerPrefs.SetString(KeyId, r.id);
                PlayerPrefs.SetString(KeyToken, r.token);
                PlayerPrefs.Save();
                ok?.Invoke(r);
            }, fail);
        }

        public static void Profile(Action<OnlineMe> ok, Action<string> fail) =>
            Send("GET", "/api/v1/me", null, true, json => { Me = JsonUtility.FromJson<OnlineMe>(json); ok?.Invoke(Me); }, fail);

        public static void SetDefense(TeamSpec team, Action ok, Action<string> fail) =>
            Send("PUT", "/api/v1/arena/defense", JsonUtility.ToJson(new TeamBody { units = team.units }), true, _ => ok?.Invoke(), fail);

        public static void FindMatch(Action<OnlineMatch> ok, Action<string> fail) =>
            Send("POST", "/api/v1/arena/match", "{}", true, json => ok?.Invoke(JsonUtility.FromJson<OnlineMatch>(json)), fail);

        public static void Fight(string matchId, TeamSpec team, Action<OnlineFight> ok, Action<string> fail) =>
            Send("POST", "/api/v1/arena/fight", JsonUtility.ToJson(new FightBody { matchId = matchId, units = team.units }), true,
                json => ok?.Invoke(JsonUtility.FromJson<OnlineFight>(json)), fail);

        public static void Leaderboard(Action<List<OnlineBoardEntry>> ok, Action<string> fail) =>
            Send("GET", "/api/v1/leaderboard", null, false, json => ok?.Invoke(JsonUtility.FromJson<OnlineBoard>("{\"items\":" + json + "}").items), fail);

        public static void UploadSave(Action ok, Action<string> fail) =>
            Send("PUT", "/api/v1/save", Game.Export(), true, _ => ok?.Invoke(), fail);

        public static void DownloadSave(Action<string> ok, Action<string> fail) =>
            Send("GET", "/api/v1/save", null, true, ok, fail);

        // ------------------------------------------------------------------

        static void Send(string method, string path, string body, bool auth, Action<string> ok, Action<string> fail) =>
            R.StartCoroutine(R.Request(method, path, body, auth, ok, fail));

        IEnumerator Request(string method, string path, string body, bool auth, Action<string> ok, Action<string> fail)
        {
            using var req = new UnityWebRequest(BaseUrl + path, method);
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 15;
            if (auth)
            {
                req.SetRequestHeader("X-Kw-Id", PlayerPrefs.GetString(KeyId, ""));
                req.SetRequestHeader("X-Kw-Token", PlayerPrefs.GetString(KeyToken, ""));
            }
            yield return req.SendWebRequest();

            string text = req.downloadHandler?.text ?? "";
            if (req.result == UnityWebRequest.Result.Success)
            {
                ok?.Invoke(text);
                yield break;
            }
            string msg = "Server nicht erreichbar";
            if (req.responseCode > 0)
            {
                try { var e = JsonUtility.FromJson<ErrorBody>(text); if (!string.IsNullOrEmpty(e?.error)) msg = e.error; }
                catch { msg = $"Fehler {req.responseCode}"; }
            }
            Debug.LogWarning($"Kesselwerk online: {method} {path} → {req.responseCode} {req.error} {msg}");
            fail?.Invoke(msg);
        }

        public static TeamSpec ToTeam(List<UnitSpec> units)
        {
            var t = new TeamSpec();
            if (units != null) foreach (var u in units) t.Add(u);
            return t;
        }
    }
}
