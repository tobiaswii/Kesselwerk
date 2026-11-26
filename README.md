# Kesselwerk – Monster-Kartenspiel (Arbeitstitel)

Ein Sammelkartenspiel für iPhone und iPad, in dem eigene Kreaturen in animierten Kämpfen gegeneinander antreten.

**Studio:** Kesselwerk
**Status:** Prototyp-Phase

## Spielidee

- Rundenbasiertes Kartenspiel (Inspiration: Yu-Gi-Oh), Stimmung inspiriert von Palworld – **alle Kreaturen sind Eigenentwicklungen**.
- Ausgespielte Karten erscheinen als Kreaturen in einer kleinen 3D-Arena und kämpfen animiert.
- Eigene Decks bauen, gegeneinander antreten (zuerst gegen KI und Freunde, später Rangliste).
- Spielbar **hochkant und quer** (Handy und Tablet).

## Technik

| Bereich | Entscheidung |
|---|---|
| Engine | Unity 6 LTS (Personal, kostenlos) |
| Zielplattform | iOS (iPhone/iPad), später optional Android |
| Testen | Unity Device Simulator + Web-Build auf eigenem Server (Safari) |
| iOS-Build | Cloud-Build ohne eigenen Mac (Codemagic / Unity Build Automation) |
| Grafik | Low-Poly 3D, freie CC0-Assets als Basis |

## Projekt einrichten

1. [Unity Hub](https://unity.com/download) installieren
2. Unity 6 LTS mit den Modulen **Web Build Support** und **iOS Build Support** installieren
3. Repository klonen (z. B. mit [GitHub Desktop](https://desktop.github.com)) und den Ordner im Unity Hub über „Add project from disk“ öffnen

## Lizenzen

Alle verwendeten fremden Assets sind in [ASSETS.md](ASSETS.md) mit Quelle und Lizenz aufgeführt.

© Kesselwerk. Alle Rechte vorbehalten.

## Stand 26.11.2026 – Umbau zum 3D-Monster-Autokampf

- **Spiel:** Sammeln → Team (6 Felder, vorne/hinten) → vollautomatischer 3D-Kampf → Belohnungen.
- **Inhalt:** 24 Kreaturen (Quaternius, CC0), 10 Elemente, 6 Seltenheiten, Synergien, Fusion (Sterne), Entwicklung, Beschwörung mit Garantie, Kampagne mit 2 Regionen (je 8 Stufen inkl. Elite und Boss), tägliche Aufgaben, Login-Belohnung, Einstellungen (Musik/Effekte getrennt).
- **Kampfkern:** `Assets/Kesselwerk/Scripts/Core` – reines C#, deterministisch (gleiche Teams + Startwert = gleiches Ergebnis). 34 automatische Tests in `Assets/Kesselwerk/Tests` (Unity Test Runner, EditMode) – alle bestanden.
- **Server-Prototyp:** `Server/` (ASP.NET, nutzt denselben Kampfkern) prüft gemeldete Kampfergebnisse nach. Lokal getestet, inkl. Nachrechnen eines echten Unity-Kampfes. Noch nicht auf dem Homeserver installiert.
- **Test-Brücke (nur Editor):** `Assets/Kesselwerk/Editor/ClaudeBridge.cs` – Befehle über `ClaudeBridge/cmd.txt` (play, stop, shot, tests, size, record, game …).

### Zuhause weitermachen
1. Repo klonen bzw. `git pull`.
2. Im Unity Hub das Projekt öffnen (Unity 6000.6.10f1, beim ersten Öffnen dauert der Import etwas).
3. Auf Play drücken – das Spiel startet von selbst im Menü (egal welche Szene offen ist).
4. Tests: Window › General › Test Runner › EditMode › Run All.

### Offen
- Demo-Video fertig schneiden (Aufnahmen liegen lokal auf dem Arbeits-PC in `ClaudeBridge/rec_demo2`, nicht im Repo)
- Online-Teil auf dem Homeserver (Konten, Speicherstand, Ranglisten, PvP)
- Weitere Regionen, Ausrüstung, Basisbau
- Echte Sounds statt synthetischer Platzhalter
