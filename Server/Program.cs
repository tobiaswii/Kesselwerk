using Kesselwerk.Core;

// Kesselwerk-Prüfserver (Prototyp)
// Nutzt exakt denselben Kampfkern wie das Spiel. Da der Kampf deterministisch ist
// (gleiche Teams + gleicher Startwert = gleiches Ergebnis), kann der Server jedes
// gemeldete Ergebnis nachrechnen und Manipulation erkennen.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", kreaturen = CreatureDb.All.Count }));

app.MapPost("/api/battle/verify", (VerifyRequest req) =>
{
    if (req?.team == null) return Results.BadRequest(new { error = "Team fehlt" });
    if (req.region < 0 || req.region >= Campaign.RegionCount || req.stage < 0 || req.stage >= Campaign.StagesPerRegion)
        return Results.BadRequest(new { error = "Unbekannte Stufe" });

    var team = new TeamSpec();
    foreach (var u in req.team) team.Add(new UnitSpec(u.id, u.level, u.slot, u.stars, u.stage));
    if (!team.IsValid(out var err)) return Results.BadRequest(new { error = err });

    var stage = Campaign.Get(req.region, req.stage);
    var result = BattleSim.Run(team, stage.enemy, req.seed);
    bool ok = result.winner == req.claimedWinner && result.events.Count == req.claimedEvents;
    return Results.Ok(new
    {
        gueltig = ok,
        sieger = result.winner,
        ereignisse = result.events.Count,
        aktionen = result.actions,
        hinweis = ok ? "Ergebnis bestätigt" : "Ergebnis stimmt nicht mit der Nachrechnung überein"
    });
});

app.Run();

public class VerifyUnit { public string id { get; set; } public int level { get; set; } = 1; public int stars { get; set; } = 1; public int stage { get; set; } public int slot { get; set; } }
public class VerifyRequest
{
    public int region { get; set; }
    public int stage { get; set; }
    public int seed { get; set; }
    public int claimedWinner { get; set; }
    public int claimedEvents { get; set; }
    public List<VerifyUnit> team { get; set; }
}
