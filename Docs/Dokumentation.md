# Kesselwerk – Projektdokumentation

Stand: 26.11.2026 · Unity 6000.6.11f1 (URP) · Zielgeräte iPhone/iPad (hoch- und querkant), Android möglich

---

## 1. Funktionsübersicht

| Bereich | Umfang |
|---|---|
| **Kreaturen** | 59 sammelbare Kreaturen + 4 Bosse, 10 Elemente, 6 Seltenheiten, 4 Rollen (Tank, Angreifer, Support, Heiler), je 2 Entwicklungsstufen, eigene Spezialfähigkeit und Passive |
| **Modelle** | Alle Kreaturen selbst erzeugt (Blender-Generator unter `Tools/Creatures`), Anime-Shader mit Umriss, Randlicht und Leuchtstellen |
| **Beschwören** | 1er/10er-Beschwörung, Garantien (spätestens Selten nach 10, Legendär nach 60), Kartenenthüllung |
| **Sammlung** | Detailansicht mit 3D-Vorschau, Tränke (EP), Fusion (Sterne), Entwicklung, 3 Ausrüstungsplätze |
| **Ausrüstung** | 15 Gegenstände (Waffe, Rüstung, Amulett), Stufen 1–5, Beute aus Elite- und Bosskämpfen |
| **Team** | 6 Plätze (vorne/hinten), Element-Synergien und Rollen-Synergien (Bollwerk, Rudeljagd, Ausgewogen) |
| **Kampf** | Vollautomatisch, deterministisch, 3D mit Nah-/Fernkampf, Spezialangriffen (Zoom, Zeitlupe, Effekte), Tempo 1×–3×, Überspringen |
| **Zustände** | Brand, Frost (verlangsamt), Gift (stapelbar, schwächt Heilung), Betäubung, Schild, Provokation, Buffs/Debuffs; Grundangriffe von Feuer/Eis/Natur/Blitz lösen Zustände aus |
| **Wetter** | Klar, Regen, Hitze, Schnee, Nebel, Gewitter – feste Zuordnung je Stufe, verändert Werte und Effekte, sichtbar mit Partikeln und Blitzen |
| **Kampagne** | 4 Regionen (Grüne Ebenen, Vulkanregion, Eisgebirge, Schattenreich) × 8 Stufen, Elite-Stufen, je ein Boss mit 3 Phasen (Schildwall, Raserei, Verzweiflung), eigene Arena-Gestaltung je Region |
| **Arena (Online)** | Konto ohne E-Mail, Verteidigungsteam, Gegnersuche nach Wertung (sonst Trainingsgegner), Kampf wird **auf dem Server** berechnet, Elo-Wertung, Ränge Bronze bis Legende, Rangliste |
| **Cloud** | Spielstand hoch- und herunterladen |
| **Fortschritt** | Gold, Kristalle, Tränke, tägliche Aufgaben, Anmeldebonus, Statistik je Kampf mit MVP |
| **Audio** | Klänge und Musik werden im Spiel erzeugt (Platzhalter) |

## 2. Architektur

```
Assets/Kesselwerk/
  Scripts/Core/        reines C# ohne Unity (asmdef Kesselwerk.Core, noEngineReferences)
     Types.cs          Elemente, Seltenheit, Rollen, Wetter, Zufall (xorshift), Mathe
     Creatures.cs      Kreaturen-Datenbank, Fähigkeiten, Passive
     Battle.cs         TeamSpec, Werte, Synergien, BattleSim (deterministisch, Ereignisliste)
     Gear.cs           Ausrüstung
     Progression.cs    Spielstand, Fusion/Entwicklung, Beschwörung, Kampagne, Belohnungen
  Scripts/             Unity-Seite: Darstellung, UI, Audio
     Battle/           AutoBattleManager (spielt Ereignisse ab), BattleHud, WeatherFx
     UI/               MenuUI (alle Seiten im Code aufgebaut), UX-Bausteine
     Online/Online.cs  REST-Client für den Server
     MonsterModel.cs   lädt Modelle, setzt Anime-Shader, rendert Kartenbilder
  Resources/Monsters/  Kreaturen-Modelle (FBX, „Gen_…“)
  Resources/Shaders/   Kreatur.shader (Cel-Shading, Umriss, Leuchten über Eckpunkt-Alpha)
  Editor/              ClaudeBridge (Test-Steuerung), ProjectSetup
  Tests/               EditMode-Tests (Kesselwerk.Tests)
Server/                ASP.NET 8 Minimal API, kompiliert Scripts/Core mit
Tools/Creatures/       Blender-Generator (Python/bpy) für alle Kreaturen
```

**Grundprinzip:** Der Kampf wird vollständig in `BattleSim` berechnet (gleiche Teams + gleicher Startwert + gleiches Wetter = gleiches Ergebnis). Unity spielt nur die Ereignisliste ab. Der Server nutzt denselben Code und berechnet Arena-Kämpfe selbst – das Spiel kann das Ergebnis nicht fälschen.

**Kreaturen-Generator:** `species.py` enthält die Baupläne (Vierbeiner/Drache, Zweibeiner, Vogel, Schwebewesen), `roster.py` die Liste aller Kreaturen mit Aussehen und Spieldaten. `python3 batch.py` (neue) bzw. `batch2.py` (Neumodelle der ursprünglichen Kreaturen) erzeugen FBX-Dateien mit Skelett, Eckpunktfarben und 7 Animationen (Idle, Walk, Attack, Punch, HitRecieve, Death, Dance). Benötigt `pip install bpy`.

## 3. Projekt starten

1. Repository klonen (GitHub Desktop) – `https://github.com/tobiaswii/Kesselwerk` (privat).
2. Unity Hub → Projekt hinzufügen → Ordner wählen → mit **Unity 6000.6.11f1** öffnen (gleiche Version auf allen PCs).
3. Szene ist leer gestartet: `GameBootstrap` baut alles beim Start auf. Einfach **Play** drücken.
4. Server-Adresse steht in `Scripts/Online/Online.cs` (`https://kesselwerk.wittmer.id`).

## 4. Build für iOS (iPhone/iPad)

Voraussetzungen: Mac mit Xcode, kostenloses Apple-Konto reicht zum Testen auf eigenen Geräten (7 Tage gültig), für den App Store das Apple Developer Program (99 €/Jahr).

1. Unity: *File → Build Profiles → iOS* hinzufügen (iOS Build Support im Hub installieren).
2. *Player Settings*: Company/Product Name, Bundle Identifier (z. B. `id.wittmer.kesselwerk`), Ausrichtung *Auto Rotation*, Ziel-iOS 15+.
3. *Build* → Xcode-Projekt entsteht → in Xcode öffnen → Team (Apple-ID) wählen → iPhone per Kabel → *Run*.
4. Hinweis: Ohne Mac ist ein iOS-Build nur über einen Cloud-Dienst möglich (z. B. Unity Build Automation, kostenpflichtig) – derzeit offen.

## 5. Build für Android

1. Unity Hub: *Android Build Support* (mit SDK/NDK/OpenJDK) installieren.
2. *Build Profiles → Android*, *Player Settings*: Package Name, Minimum API 26, IL2CPP + ARM64.
3. *Build* → `.apk` auf ein Android-Gerät kopieren und installieren (Unbekannte Quellen erlauben).

## 6. Server

Siehe Projekt-Dokument „kesselwerk-server.md“ (Homeserver). Kurz: Docker-Stack `/opt/stacks/kesselwerk` auf VM 100, `https://kesselwerk.wittmer.id`, Update durch Austausch von `src/` und `docker compose up -d --build`.

## 7. Tests

- **46 automatische Tests** (EditMode in Unity bzw. lokal mit .NET): Kampf-Determinismus, Elementvorteile, Synergien, Fortschritt, Fusion/Entwicklung, Beschwörungs-Garantien, Kampagne, Belohnungen, Ausrüstung, Bossphasen, Brand/Frost/Gift, Wetter, Vollständigkeit aller Kreaturen.
- In Unity: *Window → General → Test Runner → EditMode → Run All* (oder über die Brücke: Befehl `tests`).
- Server: lokal mit allen Endpunkten getestet; live geprüft am 26.11.: Konto, Verteidigung, Gegnersuche, Server-Kampf, Rangliste, Cloud-Spielstand.

## 8. Bekannte Fehler und Grenzen

- Klänge und Musik sind einfache, im Spiel erzeugte Platzhalter.
- Einige Vierbeiner ähneln sich noch in den Proportionen; Feinschliff einzelner Modelle (Golem, Wichtel) offen.
- Mausklicks auf Spiel-Knöpfe kommen im Editor über die Fernsteuerung nicht an (nur Testumgebung, im Spiel unkritisch).
- Arena: solange es kaum Spieler gibt, kämpft man gegen Trainingsgegner (max. +15/−10 Wertung).
- iOS-Build noch nicht durchgeführt (kein Mac vorhanden).

## 9. Offen / nächste Schritte

- Feinschliff der Modelle, Entwicklungsstufe 2 mit eigenem, größerem Aussehen
- Musik und Soundeffekte (lizenzfreie Quellen oder selbst erzeugt)
- iOS-Build über Mac oder Cloud-Build
- Weitere Spielmodi (Dungeons, Tagesevents, Weltboss) nach Bedarf

## Kesselschlacht (Hauptmodus seit 26.11.2026, Prinzip wie Teamfight Tactics)

- **Ablauf:** 8 Spieler (du + 7 Computer-Gegner, online durch echte Aufstellungen ersetzt), je 100 Leben. Runden 1-1 bis 1-3 und jede 7. Runde: wilde Monster. Sonst Kampf gegen einen anderen Spieler; Verlierer verliert Rundenschaden + Sterne der überlebenden Gegner.
- **Wirtschaft:** Einkommen 5 + Zinsen (1 je 10 Gold, max. 5) + Serienbonus (2/3/4/5+ Siege oder Niederlagen in Folge: +1/+1/+2/+3). Neu würfeln 2 Gold, Erfahrung 4 Gold für 4 EP, Stufe 1–9 = Anzahl Monster auf dem Feld.
- **Laden:** 5 Plätze, Kosten 1–5 nach Seltenheit, Chancen je Stufe (Tabelle `Tft.Odds`). Im Laden erscheinen alle Monster mit Kosten 1–2 und alles aus deiner Sammlung → Beschwören schaltet neue Monster für den Laden frei.
- **Sterne:** 3 gleiche = 2 Sterne, 3 × 2 Sterne = 3 Sterne. Sterne → Kampfstufe 1/13/30; höhere Sterne schalten die in der Sammlung gewählten Attacken frei und erhöhen deren Rang.
- **Wabenfeld:** 7 × 4 Waben je Seite (`Core/Hex.cs`), Nahkampf Reichweite 1, Fernkampf 3, Monster laufen bis zu 2 Waben je Zug. Erschöpfung ab Aktion 40 (Schaden steigt, Heilung/Schilde sinken) verhindert Patt-Kämpfe.
- **Belohnung:** Kristalle nach Platz (300/200/150/100/50) und Gold; laufende Partien werden gespeichert (PlayerPrefs `kesselwerk_tft`).
- **Online:** `PUT /api/v1/tft/board`, `GET /api/v1/tft/ghosts`, `POST /api/v1/tft/result`, `GET /api/v1/tft/leaderboard`; öffentliche Bestenliste unter https://kesselwerk.wittmer.id/
- **Debug:** `tft new | auto | fight | state`, `moves <id> <i> [j]`, `movepick <id>`, `pool <id>`.
- **Code:** `Core/Tft.cs` (Regeln, Computer-Gegner), `Core/Hex.cs`, `Tft/TftSession.cs` (Ablauf, Speichern, Online), `Tft/TftManager.cs` (Planungsansicht).
