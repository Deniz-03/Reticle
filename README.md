# Reticle

Reticle ist ein lokales Windows-Overlay für eigene Fadenkreuze. Es kommuniziert nicht mit Spielen und sammelt keine Telemetrie.

## Installation und Start

1. Lade die portable `Reticle.exe` aus dem `dist`-Ordner bzw. veröffentliche sie mit `build/publish.ps1`.
2. Starte die Anwendung. Das aktive Fadenkreuz erscheint mittig auf dem ausgewählten Monitor und lässt Mausklicks durch.
3. Öffne **Bibliothek**, um ein Preset zu aktivieren, zu duplizieren oder einen Share-Code zu importieren. Im **Designer** werden Änderungen sofort ins Overlay übernommen.

Die Einstellungen und Bibliothek liegen im lokalen Anwendungsdatenordner und bleiben bei Updates erhalten.

## Valorant

Nutze für ein sichtbares Overlay den Anzeigemodus **„Vollbild im Fenster“**. Exklusives Vollbild kann Windows-Overlays verdecken.

Du kannst das Valorant-Fadenkreuz ausblenden (Fadenkreuz → innere/äußere Linien und Mittelpunkt deaktivieren) oder es bewusst mit Reticle kombinieren. Einen Valorant-Code fügst du über **Bibliothek → Importieren…** ein.

Falls Riot Client oder das Spiel als Administrator läuft, starte auch Reticle als Administrator; Windows lässt globale Hooks sonst nicht zuverlässig in den Prozess mit höherer Integrität gelangen.

Die Nutzung geschieht auf eigenes Risiko. Reticle zeichnet lediglich ein Windows-Overlay und greift nicht auf Spielprozesse zu; es gibt keine offizielle Freigabe durch Riot Games.

## Problemlösung

- **Overlay nicht sichtbar:** Prüfe den Fenstermodus und wechsle von exklusivem Vollbild zu „Vollbild im Fenster“.
- **Hotkeys funktionieren nicht:** Prüfe die Bindung und starte Reticle als Administrator, wenn das Zielspiel erhöht läuft.
- **Fadenkreuz wirkt unscharf:** Verwende eine ganzzahlige Designer-Vergrößerung und überprüfe den globalen Größenfaktor sowie die Windows-Skalierung.

## Entwicklung

```powershell
dotnet test Reticle.sln -c Release
.\build\publish.ps1
```
