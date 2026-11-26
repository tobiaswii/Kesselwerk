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
3. [Git LFS](https://git-lfs.com) installieren (für 3D-Modelle, Texturen, Sounds)
4. Repository klonen und den Ordner im Unity Hub über „Add project from disk“ öffnen

## Lizenzen

Alle verwendeten fremden Assets sind in [ASSETS.md](ASSETS.md) mit Quelle und Lizenz aufgeführt.

© Kesselwerk. Alle Rechte vorbehalten.
