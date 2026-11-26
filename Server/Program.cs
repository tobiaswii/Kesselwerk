using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kesselwerk.Core;

// =====================================================================
// Kesselwerk-Server
// - Konten (Gerät bekommt Kennung + geheimen Schlüssel, nur der Hash wird gespeichert)
// - Cloud-Spielstand
// - Arena (asynchrones PvP): Verteidigungsteams, Gegnersuche, Kämpfe werden
//   NUR auf dem Server berechnet (gleicher deterministischer Kampfkern wie im Spiel)
// - Rangliste mit Elo-Wertung und Rängen Bronze … Legende
// - Prüfung von Kampagnen-Ergebnissen
// Speicherung als JSON-Dateien unter DATA_DIR (Standard /data).
// =====================================================================

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.IncludeFields = true;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
});
var app = builder.Build();

string dataDir = Environment.GetEnvironmentVariable("DATA_DIR") ?? "/data";
var store = new Store(dataDir);
var limiter = new RateLimiter(120);

// Ratenbegrenzung je IP (hinter Cloudflare: CF-Connecting-IP)
app.Use(async (ctx, next) =>
{
    string ip = ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
    if (!limiter.Allow(ip)) { ctx.Response.StatusCode = 429; await ctx.Response.WriteAsJsonAsync(new { error = "Zu viele Anfragen" }); return; }
    await next();
});

Player? Auth(HttpRequest req)
{
    string id = req.Headers["X-Kw-Id"].FirstOrDefault() ?? "";
    string token = req.Headers["X-Kw-Token"].FirstOrDefault() ?? "";
    var p = store.Get(id);
    if (p == null || string.IsNullOrEmpty(token)) return null;
    return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(p.tokenHash), SHA256.HashData(Encoding.UTF8.GetBytes(token))) ? p : null;
}

IResult Unauthorized() => Results.Json(new { error = "Nicht angemeldet" }, statusCode: 401);

// Öffentliche Bestenliste als Webseite
app.MapGet("/", () => Results.Content(LeaderboardPage.Html, "text/html; charset=utf-8"));

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", version = "1.0.0", kreaturen = CreatureDb.All.Count, spieler = store.Count }));

app.MapPost("/api/v1/register", (RegisterRequest req) =>
{
    string name = Store.CleanName(req?.name);
    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var p = new Player
    {
        id = Guid.NewGuid().ToString("N"),
        name = name,
        tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
        rating = 1000,
        created = DateTime.UtcNow,
    };
    store.Put(p);
    return Results.Ok(new { id = p.id, token, name = p.name });
});

app.MapGet("/api/v1/me", (HttpRequest r) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    return Results.Ok(Profile(p, store));
});

app.MapPut("/api/v1/name", (HttpRequest r, RegisterRequest req) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    p.name = Store.CleanName(req?.name);
    store.Put(p);
    return Results.Ok(Profile(p, store));
});

// ---------------- Cloud-Spielstand ----------------
app.MapPut("/api/v1/save", async (HttpRequest r) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    using var reader = new StreamReader(r.Body);
    string json = await reader.ReadToEndAsync();
    if (json.Length > 300_000) return Results.BadRequest(new { error = "Spielstand zu groß" });
    try { JsonDocument.Parse(json); } catch { return Results.BadRequest(new { error = "Kein gültiges JSON" }); }
    store.WriteSave(p.id, json);
    p.lastSave = DateTime.UtcNow;
    store.Put(p);
    return Results.Ok(new { gespeichert = p.lastSave });
});

app.MapGet("/api/v1/save", (HttpRequest r) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    var json = store.ReadSave(p.id);
    return json == null ? Results.NotFound(new { error = "Kein Cloud-Spielstand" }) : Results.Text(json, "application/json");
});

// ---------------- Arena ----------------
app.MapPut("/api/v1/arena/defense", (HttpRequest r, TeamRequest req) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    var team = ToTeam(req?.units);
    if (!team.IsValid(out var err)) return Results.BadRequest(new { error = err });
    p.defense = req!.units;
    p.defensePower = Power(team);
    store.Put(p);
    return Results.Ok(new { ok = true, kraft = p.defensePower });
});

var matches = new ConcurrentDictionary<string, Match>();

app.MapPost("/api/v1/arena/match", (HttpRequest r) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    foreach (var kv in matches) if (kv.Value.expires < DateTime.UtcNow) matches.TryRemove(kv.Key, out _);

    var rnd = Random.Shared;
    var candidates = store.All().Where(o => o.id != p.id && o.defense != null && o.defense.Count > 0)
        .OrderBy(o => Math.Abs(o.rating - p.rating) + rnd.Next(0, 120)).Take(5).ToList();
    Match m;
    if (candidates.Count > 0)
    {
        var o = candidates[rnd.Next(candidates.Count)];
        m = new Match { opponentId = o.id, opponentName = o.name, opponentRating = o.rating, units = o.defense! };
    }
    else
    {
        // Noch keine echten Gegner: Trainingsgegner passend zur Wertung
        m = Bot(p.rating, rnd);
    }
    m.id = Guid.NewGuid().ToString("N");
    m.playerId = p.id;
    m.seed = RandomNumberGenerator.GetInt32(1, int.MaxValue);
    m.expires = DateTime.UtcNow.AddMinutes(10);
    matches[m.id] = m;
    return Results.Ok(new { matchId = m.id, gegner = m.opponentName, wertung = m.opponentRating, rang = Rank(m.opponentRating), units = m.units, bot = m.opponentId == null });
});

app.MapPost("/api/v1/arena/fight", (HttpRequest r, FightRequest req) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    if (req?.matchId == null || !matches.TryRemove(req.matchId, out var m) || m.playerId != p.id || m.expires < DateTime.UtcNow)
        return Results.BadRequest(new { error = "Kampf abgelaufen – bitte neuen Gegner suchen" });
    var attack = ToTeam(req.units);
    if (!attack.IsValid(out var err)) return Results.BadRequest(new { error = err });

    // Der Server rechnet den Kampf selbst – das Ergebnis des Spiels zählt nicht
    var result = BattleSim.Run(attack, ToTeam(m.units), m.seed);
    bool won = result.winner == 0;
    var opp = m.opponentId != null ? store.Get(m.opponentId) : null;
    int oppRating = opp?.rating ?? m.opponentRating;
    int delta = Elo(p.rating, oppRating, won);
    if (m.opponentId == null) delta = won ? Math.Min(delta, 15) : Math.Max(delta, -10); // Trainingsgegner zählen weniger
    p.rating = Math.Max(0, p.rating + delta);
    if (won) p.wins++; else p.losses++;
    store.Put(p);
    if (opp != null)
    {
        opp.rating = Math.Max(0, opp.rating - delta);
        if (won) opp.defLosses++; else opp.defWins++;
        store.Put(opp);
    }
    return Results.Ok(new
    {
        seed = m.seed,
        sieger = result.winner,
        ereignisse = result.events.Count,
        aenderung = delta,
        wertung = p.rating,
        rang = Rank(p.rating),
        gegnerUnits = m.units,
    });
});

app.MapGet("/api/v1/leaderboard", () =>
{
    var top = store.All().Where(p => p.wins + p.losses > 0 || p.defense != null)
        .OrderByDescending(p => p.rating).Take(50)
        .Select((p, i) => new { platz = i + 1, name = p.name, wertung = p.rating, rang = Rank(p.rating), siege = p.wins, niederlagen = p.losses });
    return Results.Ok(top);
});

// ---------------- Kesselschlacht (asynchron online) ----------------
// Jede Runde lädt das Spiel die eigene Aufstellung hoch; andere Spieler bekommen diese als Gegner („Geister“).
var ghosts = new GhostStore(dataDir);

app.MapPut("/api/v1/tft/board", (HttpRequest r, BoardRequest req) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    if (req == null || req.units == null || req.units.Count == 0) return Results.BadRequest(new { error = "Leeres Feld" });
    if (req.stage < 1 || req.stage > 12 || req.round < 1 || req.round > Tft.RoundsPerStage) return Results.BadRequest(new { error = "Ungültige Runde" });
    var team = ToTeam(req.units);
    if (!team.IsValid(out var err)) return Results.BadRequest(new { error = err });
    foreach (var u in req.units)
        if (u.hex < 0 || u.stars < 1 || u.stars > 3 || u.level != Tft.LevelForStar(u.stars)) return Results.BadRequest(new { error = "Ungültige Einheit" });
    if (req.units.Count > Tft.MaxLevel) return Results.BadRequest(new { error = "Zu viele Einheiten" });
    ghosts.Add($"{req.stage}-{req.round}", new Ghost { playerId = p.id, name = p.name, units = req.units, time = DateTime.UtcNow });
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/v1/tft/ghosts", (HttpRequest r, int stage, int round) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    var list = ghosts.Pick($"{stage}-{round}", p.id, 7);
    return Results.Ok(list.Select(g => new { name = g.name, units = g.units }));
});

app.MapPost("/api/v1/tft/result", (HttpRequest r, ResultRequest req) =>
{
    var p = Auth(r); if (p == null) return Unauthorized();
    int place = Math.Clamp(req?.place ?? 8, 1, 8);
    p.tftGames++;
    if (place == 1) p.tftWins++;
    if (place <= 4) p.tftTop4++;
    p.tftPoints += 9 - place;
    store.Put(p);
    return Results.Ok(new { ok = true, punkte = p.tftPoints });
});

app.MapGet("/api/v1/tft/leaderboard", () =>
{
    var top = store.All().Where(p => p.tftGames > 0)
        .OrderByDescending(p => p.tftPoints).ThenByDescending(p => p.tftWins).Take(50)
        .Select((p, i) => new { platz = i + 1, name = p.name, punkte = p.tftPoints, siege = p.tftWins, top4 = p.tftTop4, partien = p.tftGames });
    return Results.Ok(top);
});

// ---------------- Kampagne prüfen ----------------
app.MapPost("/api/battle/verify", (VerifyRequest req) =>
{
    if (req?.team == null) return Results.BadRequest(new { error = "Team fehlt" });
    if (req.region < 0 || req.region >= Campaign.RegionCount || req.stage < 0 || req.stage >= Campaign.StagesPerRegion)
        return Results.BadRequest(new { error = "Unbekannte Stufe" });
    var team = ToTeam(req.team);
    if (!team.IsValid(out var err)) return Results.BadRequest(new { error = err });
    var stage = Campaign.Get(req.region, req.stage);
    var result = BattleSim.Run(team, stage.enemy, req.seed);
    bool ok = result.winner == req.claimedWinner && result.events.Count == req.claimedEvents;
    return Results.Ok(new { gueltig = ok, sieger = result.winner, ereignisse = result.events.Count, aktionen = result.actions,
        hinweis = ok ? "Ergebnis bestätigt" : "Ergebnis stimmt nicht mit der Nachrechnung überein" });
});

app.Run();

// =====================================================================
// Hilfen
// =====================================================================

static TeamSpec ToTeam(List<UnitSpec>? units)
{
    var t = new TeamSpec();
    if (units != null) foreach (var u in units) t.Add(u);
    return t;
}

static int Power(TeamSpec t) => t.units.Sum(u => CreatureDb.Get(u.id) is { } d ? Stats.Power(d, u) : 0);

static int Elo(int a, int b, bool won)
{
    double expected = 1.0 / (1.0 + Math.Pow(10, (b - a) / 400.0));
    return (int)Math.Round(32 * ((won ? 1 : 0) - expected));
}

static string Rank(int rating) =>
    rating >= 1850 ? "Legende" : rating >= 1700 ? "Meister" : rating >= 1550 ? "Diamant" :
    rating >= 1400 ? "Platin" : rating >= 1250 ? "Gold" : rating >= 1100 ? "Silber" : "Bronze";

static object Profile(Player p, Store s) => new
{
    id = p.id, name = p.name, wertung = p.rating, rang = Rank(p.rating),
    siege = p.wins, niederlagen = p.losses, verteidigungSiege = p.defWins, verteidigungNiederlagen = p.defLosses,
    verteidigung = p.defense?.Count ?? 0, cloudSpeicher = p.lastSave,
    platz = s.All().Count(o => o.rating > p.rating) + 1,
};

static Match Bot(int rating, Random rnd)
{
    // Wertung → Kampagnenstufe als Trainingsgegner
    int idx = Math.Clamp((rating - 800) / 60, 0, Campaign.RegionCount * Campaign.StagesPerRegion - 1);
    var stage = Campaign.Get(idx / Campaign.StagesPerRegion, idx % Campaign.StagesPerRegion);
    var units = stage.enemy.units.Where(u => !CreatureDb.Get(u.id)!.isBoss).ToList();
    if (units.Count == 0) units = stage.enemy.units;
    string[] names = { "Kesselgeist", "Sumpfbaron", "Funkenfee", "Moosmeister", "Frostfritz", "Glutgretel" };
    return new Match { opponentName = names[rnd.Next(names.Length)] + " (Training)", opponentRating = rating, units = units };
}

// =====================================================================
// Datenmodell und Speicher
// =====================================================================

class RegisterRequest { public string? name { get; set; } }
class BoardRequest { public int stage { get; set; } public int round { get; set; } public List<UnitSpec>? units { get; set; } }
class ResultRequest { public int place { get; set; } }
class Ghost { public string playerId { get; set; } = ""; public string name { get; set; } = ""; public List<UnitSpec> units { get; set; } = new(); public DateTime time { get; set; } }

/// <summary>Aufstellungen je Runde (je Spieler nur die neueste, höchstens 300 je Runde), als JSON-Datei gesichert.</summary>
class GhostStore
{
    readonly string file;
    readonly object gate = new();
    readonly Dictionary<string, List<Ghost>> byRound;
    static readonly JsonSerializerOptions opts = new() { IncludeFields = true };
    DateTime lastWrite = DateTime.MinValue;

    public GhostStore(string dir)
    {
        file = Path.Combine(dir, "tft_ghosts.json");
        try { byRound = File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, List<Ghost>>>(File.ReadAllText(file), opts) ?? new() : new(); }
        catch { byRound = new(); }
    }

    public void Add(string key, Ghost g)
    {
        lock (gate)
        {
            if (!byRound.TryGetValue(key, out var list)) byRound[key] = list = new List<Ghost>();
            list.RemoveAll(x => x.playerId == g.playerId);
            list.Add(g);
            if (list.Count > 300) list.RemoveAt(0);
            if ((DateTime.UtcNow - lastWrite).TotalSeconds > 20)
            {
                lastWrite = DateTime.UtcNow;
                string tmp = file + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(byRound, opts));
                File.Move(tmp, file, true);
            }
        }
    }

    public List<Ghost> Pick(string key, string exceptPlayer, int n)
    {
        lock (gate)
        {
            if (!byRound.TryGetValue(key, out var list)) return new List<Ghost>();
            return list.Where(g => g.playerId != exceptPlayer).OrderBy(_ => Random.Shared.Next()).Take(n).ToList();
        }
    }
}
class TeamRequest { public List<UnitSpec>? units { get; set; } }
class FightRequest { public string? matchId { get; set; } public List<UnitSpec>? units { get; set; } }
class VerifyRequest
{
    public int region { get; set; }
    public int stage { get; set; }
    public int seed { get; set; }
    public int claimedWinner { get; set; }
    public int claimedEvents { get; set; }
    public List<UnitSpec>? team { get; set; }
}

class Player
{
    public string id { get; set; } = "";
    public string name { get; set; } = "";
    public string tokenHash { get; set; } = "";
    public int rating { get; set; }
    public int wins { get; set; }
    public int losses { get; set; }
    public int defWins { get; set; }
    public int defLosses { get; set; }
    public int defensePower { get; set; }
    public List<UnitSpec>? defense { get; set; }
    public int tftGames { get; set; }
    public int tftWins { get; set; }
    public int tftTop4 { get; set; }
    public int tftPoints { get; set; }
    public DateTime created { get; set; }
    public DateTime? lastSave { get; set; }
}

class Match
{
    public string id = "", playerId = "";
    public string? opponentId;
    public string opponentName = "";
    public int opponentRating, seed;
    public List<UnitSpec> units = new();
    public DateTime expires;
}

class Store
{
    readonly string dir, file;
    readonly object gate = new();
    readonly Dictionary<string, Player> players;
    static readonly JsonSerializerOptions opts = new() { IncludeFields = true, WriteIndented = false };

    public Store(string dir)
    {
        this.dir = dir;
        Directory.CreateDirectory(Path.Combine(dir, "saves"));
        file = Path.Combine(dir, "players.json");
        players = File.Exists(file)
            ? JsonSerializer.Deserialize<Dictionary<string, Player>>(File.ReadAllText(file), opts) ?? new()
            : new();
    }

    public int Count { get { lock (gate) return players.Count; } }
    public Player? Get(string id) { lock (gate) return id != null && players.TryGetValue(id, out var p) ? p : null; }
    public List<Player> All() { lock (gate) return players.Values.ToList(); }

    public void Put(Player p)
    {
        lock (gate)
        {
            players[p.id] = p;
            // atomar schreiben: erst Temporärdatei, dann umbenennen
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(players, opts));
            File.Move(tmp, file, true);
        }
    }

    string SavePath(string id) => Path.Combine(dir, "saves", id + ".json");
    public void WriteSave(string id, string json)
    {
        string tmp = SavePath(id) + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, SavePath(id), true);
    }
    public string? ReadSave(string id) => File.Exists(SavePath(id)) ? File.ReadAllText(SavePath(id)) : null;

    public static string CleanName(string? name)
    {
        var s = new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Trim();
        if (s.Length < 3) s = "Kesselmeister" + Random.Shared.Next(100, 999);
        return s.Length > 20 ? s[..20] : s;
    }
}

class RateLimiter
{
    readonly int perMinute;
    readonly ConcurrentDictionary<string, (DateTime start, int count)> hits = new();
    public RateLimiter(int perMinute) { this.perMinute = perMinute; }
    public bool Allow(string key)
    {
        var now = DateTime.UtcNow;
        var v = hits.AddOrUpdate(key, (now, 1), (_, old) => now - old.start > TimeSpan.FromMinutes(1) ? (now, 1) : (old.start, old.count + 1));
        return v.count <= perMinute;
    }
}


static class LeaderboardPage
{
    public const string Html = """
<!DOCTYPE html><html lang="de"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Kesselwerk – Bestenliste</title>
<style>
body{margin:0;font-family:system-ui,-apple-system,Segoe UI,sans-serif;background:radial-gradient(circle at 30% 0%,#1f3a2a,#0b1210 70%);color:#eef3f6;min-height:100vh}
main{max-width:760px;margin:0 auto;padding:40px 18px}
h1{font-size:44px;letter-spacing:8px;font-style:italic;margin:0}
.sub{color:#ffd98c;letter-spacing:4px;font-size:13px;margin:6px 0 28px;border-top:3px solid #ffb84d;display:inline-block;padding-top:8px}
table{width:100%;border-collapse:separate;border-spacing:0 6px}
th{font-size:12px;letter-spacing:3px;color:#9fb3c2;text-align:left;padding:0 14px}
td{background:rgba(14,18,24,.78);padding:12px 14px;border-top:1px solid rgba(255,255,255,.1);border-bottom:1px solid rgba(255,255,255,.1)}
td:first-child{border-left:5px solid #73d9ff;border-radius:8px 0 0 8px;font-weight:700}
td:last-child{border-radius:0 8px 8px 0}
tr.g td:first-child{border-left-color:#ffd23f} tr.s td:first-child{border-left-color:#d7dde6} tr.b td:first-child{border-left-color:#d98a4b}
.n{font-weight:600}.p{font-weight:700;color:#ffd98c}.e{color:#9fb3c2;text-align:center;padding:40px}
</style></head><body><main>
<h1>KESSELWERK</h1><div class="sub">BESTENLISTE · KESSELSCHLACHT</div>
<table><thead><tr><th>#</th><th>NAME</th><th>PUNKTE</th><th>SIEGE</th><th>TOP 4</th><th>PARTIEN</th></tr></thead><tbody id="b"><tr><td colspan="6" class="e">Lade …</td></tr></tbody></table>
<p style="color:#9fb3c2;font-size:13px">Punkte je Partie: Platz 1 = 8 … Platz 8 = 1. Aktualisiert sich jede Minute.</p>
</main><script>
async function load(){try{const r=await fetch('/api/v1/tft/leaderboard');const d=await r.json();const b=document.getElementById('b');
if(!d.length){b.innerHTML='<tr><td colspan="6" class="e">Noch keine Partien gewertet.</td></tr>';return}
b.innerHTML=d.map(e=>`<tr class="${e.platz==1?'g':e.platz==2?'s':e.platz==3?'b':''}"><td>${e.platz}</td><td class="n"></td><td class="p">${e.punkte}</td><td>${e.siege}</td><td>${e.top4}</td><td>${e.partien}</td></tr>`).join('');
[...b.querySelectorAll('td.n')].forEach((td,i)=>td.textContent=d[i].name);}catch(e){}}
load();setInterval(load,60000);
</script></body></html>
""";
}
