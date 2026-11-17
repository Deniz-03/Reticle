# Reticle

[![CI](https://github.com/Deniz-03/Reticle/actions/workflows/ci.yml/badge.svg)](https://github.com/Deniz-03/Reticle/actions/workflows/ci.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

Reticle ist ein kostenloses, quelloffenes Crosshair-Overlay für Windows 10/11. Es zeichnet ein frei gestaltbares Fadenkreuz pixelgenau in die Bildschirmmitte, lässt alle Klicks durch und greift nicht in Spiele ein.

## Funktionen

- **Click-through-Overlay**: transparent, immer im Vordergrund, nicht in Taskleiste oder Alt-Tab, pixelgenau zentriert (auch bei ungerader Auflösung und DPI-Skalierung), Monitorauswahl, Versatz und globale Größe.
- **Designer mit Ebenen**: klassisches Kreuz (Linien, Abstand, Punkt, T-Form), radiale Kreuze mit 2–8 Armen, Formen (Kreis, Ring, Rechteck, Dreieck, Chevron, Bogen, Linie) und Bilder (PNG, JPG, WebP, animiertes GIF). Pro Ebene: Farbe, Deckkraft, Rotation, Skalierung, Versatz, Blur, Umrandung. Live-Vorschau mit Undo/Redo.
- **Animationen**: Ebenen reagieren auf Tasten oder Maustasten (z. B. Spreizung beim Schießen), mehrstufig mit Easing.
- **Bibliothek**: beliebig viele Crosshairs, 17 mitgelieferte Presets, Vorschaubilder.
- **Hotkeys**: Overlay ein/aus, nächstes/vorheriges Crosshair, Ausblenden solange gedrückt, Zentrieren, Größe ±, eigene Taste pro Crosshair (z. B. pro Waffe), automatisches Ausblenden bei gedrückter rechter Maustaste.
- **Teilen & Import**: eigene Share-Codes (`RTCL1.…`), Datei-Export/-Import (`*.reticle.json`), PNG-Export und **Import von Valorant-Crosshair-Codes** (`0;P;c;5;…`).
- **System**: Tray-Icon, Autostart, Einzelinstanz, Hinweis bei Spielen mit Administratorrechten, Deutsch und Englisch.
- **Datenschutz**: keine Netzwerkverbindungen, keine Telemetrie. Daten liegen lokal unter `%APPDATA%\Reticle`.

## Installation

1. Lade die aktuelle `Reticle.exe` unter [Releases](https://github.com/Deniz-03/Reticle/releases) herunter (portable, keine Installation nötig).
2. Starte sie. Das aktive Fadenkreuz erscheint mittig auf dem gewählten Monitor.
3. Unter **Bibliothek** aktivierst du Presets oder importierst Codes, im **Designer** passt du sie an – Änderungen erscheinen sofort im Overlay.

Standard-Hotkeys: `F8` Overlay ein/aus, `F9` nächstes Crosshair, `Shift+F9` vorheriges, `Strg+F8` zentrieren.

## Nutzung mit Valorant

- Anzeigemodus **„Vollbild im Fenster“** verwenden. Exklusives Vollbild verdeckt Windows-Overlays.
- Das Valorant-eigene Fadenkreuz ausblenden (innere/äußere Linien und Mittelpunkt aus) oder bewusst kombinieren.
- Valorant-Codes über **Bibliothek → Importieren…** einfügen.
- Läuft der Riot Client als Administrator, Reticle ebenfalls als Administrator starten – sonst kommen Hotkeys nicht an.

> **Hinweis:** Reticle zeichnet nur ein Windows-Overlay. Es liest oder verändert keinen Spielspeicher, injiziert nichts und sendet keine Eingaben. Eine offizielle Freigabe durch Spielehersteller gibt es trotzdem nicht – die Nutzung erfolgt auf eigenes Risiko.

## Problemlösung

| Problem | Lösung |
|---|---|
| Overlay nicht sichtbar | Vom exklusiven Vollbild auf „Vollbild im Fenster“ wechseln. |
| Hotkeys reagieren nicht im Spiel | Reticle als Administrator starten (Einstellungen → „Als Administrator neu starten“). |
| Fadenkreuz unscharf | Globale Größe auf ganzzahlige Werte setzen, Windows-Skalierung prüfen. |

## Entwicklung

Voraussetzung: Windows 10/11 und das [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build Reticle.sln
dotnet test Reticle.sln
dotnet run --project src/Reticle.App
.\build\publish.ps1        # portable Reticle.exe in dist/
```

Projektstruktur:

| Pfad | Inhalt |
|---|---|
| `src/Reticle.Core` | Plattformunabhängige Logik: Datenmodell, Geometrie, Animationen, Hotkey-Matching, Codecs, Speicherung, Presets |
| `src/Reticle.App` | WPF-App: Overlay-Fenster, globale Input-Hooks, Editor-UI, Tray |
| `tests/` | xUnit-Tests für Core und App |

Ein Release entsteht automatisch, wenn ein Tag `v*` gepusht wird (z. B. `git tag v1.0.0 && git push origin v1.0.0`).

## Lizenz

[GNU General Public License v3.0](LICENSE)
