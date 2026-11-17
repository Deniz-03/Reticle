# Requirements – Crosshair-Overlay für Windows

> Arbeitstitel: **Reticle** (eigener Name, keine Anlehnung an Marke, Logo oder UI des Vorbilds).
> Ziel: gleiche *Funktionalität* wie das kommerzielle Produkt „Crosshair X“ (CenterPoint Gaming), **keine 1:1-Kopie** – eigenes UI, eigenes Datenformat, eigene Assets, eigene Texte.

## 1. Analyse des Vorbilds (Stand 17.11.2026)

Quellen: centerpointgaming.com (Startseite, Hilfeartikel zu Per-Weapon-Crosshairs, Zentrierung, Custom Image, Keybinds, Anti-Cheat) und die Steam-Store-Seite.

| Bereich | Was das Vorbild kann |
|---|---|
| Overlay | Transparentes, immer-oben Fenster über dem Spiel (Borderless/Windowed). Für Exclusive Fullscreen ein Xbox-Game-Bar-Widget. Keine DLL-Injection, kein Speicherzugriff, keine Treiber. |
| Designer | Linien (Länge, Dicke, Abstand/Gap), Punkt, Outline, Farbe, Deckkraft, Rotation, Skalierung, Position, Blur – pro Layer, mit Live-Vorschau. |
| Formen | Formbibliothek: Dreiecke, Chevrons, Bögen, T-Form, Rechtecke, in beliebigem Winkel kombinierbar. Radiale Crosshairs mit 3/5/6/7/8 Armen. |
| Layer | Crosshair-Layer (Vektor, animierbar), Bild-Layer (PNG, animiertes GIF), Freihand-Layer (Pixel), Text-Layer (Text, Emoji). |
| Animationen | Reaktive Animationen, z. B. beim Schießen (Linksklick) – mehrstufig, pro Taste auslösbar, individuelles Easing. Nur Vektor-Layer animierbar. |
| Bibliothek | Unbegrenzt viele Crosshairs speichern und schnell wechseln. |
| Keybinds | Crosshair ein/aus, Designs wechseln (durchschalten), „Ausblenden solange gehalten“, Größe/Form global ändern, je gespeichertem Crosshair eine eigene Taste (Per-Weapon). Aktion „Keine Aktion“ zum Deaktivieren. |
| Smart Toggle | Automatisch ausblenden bei Rechtsklick (Zielen/ADS). |
| Position | Globaler Offset (x, y) + Button „Zentrieren“; globale Größe als Master-Multiplikator. |
| Multi-Monitor | Auswahl des Monitors; Position pro Spiel speicherbar. |
| Teilen | Crosshair als Code teilen/importieren; Export als Bilddatei. |
| Community | Galerie mit >100 000 Community-Crosshairs, Steam Workshop, Web-Demo. |
| Sonstiges | Tray-Icon, 15–17 Sprachen, Betrieb mit Admin-Rechten möglich (nötig, wenn das Spiel als Admin läuft). |
| Preis | ca. 10 € einmalig (Steam/MS Store/Epic). |

## 1a. Entscheidungen (17.11.2026)

- **Stack:** C# / WPF auf **.NET 10 (LTS)** – .NET 8 läuft im Nov. 2026 aus dem Support, daher 10. Testframework xUnit.
- **Zielbild:** Eine App, die man **direkt mit Valorant** nutzen kann. Daraus folgt:
  - Valorant läuft mit Vanguard → nur reines Overlay (OV-8), Eingaben nur passiv lesen (AN-4).
  - Valorant im Modus **„Vollbild im Fenster“ (Windowed Fullscreen)** wird unterstützt; Exclusive Fullscreen nicht (Game-Bar-Widget bleibt „Kann“).
  - **Vorhandene Profile übernehmen:** Valorant-Crosshair-Codes (`0;P;c;5;...`, wie sie auf Pro-Settings-Seiten geteilt werden) lassen sich importieren (IM-1..3).
  - Codes des Vorbilds (Crosshair X) haben ein proprietäres, undokumentiertes Format und werden **nicht** unterstützt.
- **Umfang erster Build:** alle M- und S-Anforderungen + Valorant-Import. Kann-Anforderungen folgen später.

## 2. Abgrenzung

**Übernommen wird:** die Funktionalität aus Abschnitt 1.
**Nicht übernommen wird:** Name, Logo, Farbschema, Layout, Texte, Grafiken, Community-Inhalte, das Share-Code-Format des Vorbilds.
**Bewusst später / optional:** Online-Community-Galerie (braucht Server), Steam Workshop, Xbox-Game-Bar-Widget (eigene UWP-App), Mehrsprachigkeit über DE/EN hinaus.

## 3. Funktionale Anforderungen

Priorität: **M** = Muss (MVP), **S** = Soll (v1.0), **K** = Kann (später).

### 3.1 Overlay
| ID | Anforderung | Prio |
|---|---|---|
| OV-1 | Transparentes, rahmenloses Fenster, immer im Vordergrund, über dem gewählten Monitor. | M |
| OV-2 | Click-through: Maus- und Tastatureingaben gehen vollständig ans Spiel. | M |
| OV-3 | Erscheint nicht in Taskleiste und Alt-Tab. | M |
| OV-4 | Crosshair standardmäßig exakt in der Monitormitte, pixelgenau (auch bei ungerader Auflösung und DPI-Skalierung ≠ 100 %). | M |
| OV-5 | Globaler Offset (x, y in px) und Button „Zentrieren“ (setzt auf 0,0). | M |
| OV-6 | Globale Größe als Multiplikator auf das aktive Crosshair. | M |
| OV-7 | Monitorauswahl bei mehreren Bildschirmen. | M |
| OV-8 | Kein Eingriff ins Spiel: keine Injection, kein Speicherzugriff, keine Treiber, keine synthetischen Eingaben, kein Screen-Capture. | M |
| OV-9 | Funktioniert in Borderless/Windowed-Spielen. Exclusive Fullscreen wird als Einschränkung dokumentiert. | M |
| OV-10 | Overlay bleibt über dem Spiel, auch nachdem das Spiel den Fokus bekommt (periodisches Re-Topmost). | S |
| OV-11 | Xbox-Game-Bar-Widget für Exclusive Fullscreen. | K |

### 3.2 Designer (Editor)
| ID | Anforderung | Prio |
|---|---|---|
| DE-1 | Live-Vorschau im Editor (Zoom, Hintergrund wählbar: hell/dunkel/Spielszene-Beispielbild). | M |
| DE-2 | Layer-Liste: hinzufügen, löschen, duplizieren, umsortieren, ein-/ausblenden, umbenennen. | M |
| DE-3 | **Klassisch-Layer:** 4 Linien mit Länge, Dicke, Gap, je Linie an/aus (z. B. T-Form), Mittelpunkt (Größe, Form rund/eckig). | M |
| DE-4 | **Radial-Layer:** 2–8 Arme, Startwinkel, Länge, Dicke, Gap. | S |
| DE-5 | **Form-Layer:** Kreis/Ring, Rechteck, Dreieck, Chevron, Bogen (Start-/Endwinkel), Linie. | S |
| DE-6 | **Bild-Layer:** PNG/JPG/WebP und animiertes GIF importieren; wird in der Datei eingebettet. | S |
| DE-7 | **Text-Layer:** Text/Emoji, Schriftart, Größe. | K |
| DE-8 | **Freihand-Layer:** Pixel zeichnen auf kleinem Raster. | K |
| DE-9 | Pro Layer: Farbe, Deckkraft, Rotation, Skalierung, Position (Offset), Blur. | M |
| DE-10 | Outline pro Layer: an/aus, Dicke, Farbe, Deckkraft. | M |
| DE-11 | Undo/Redo im Editor. | S |
| DE-12 | Pixel-Snapping: Linien werden auf ganze Pixel gerundet, damit sie scharf sind. | M |

### 3.3 Animationen
| ID | Anforderung | Prio |
|---|---|---|
| AN-1 | Pro Vektor-Layer eine Animation mit Auslöser (Linke Maustaste, Rechte Maustaste, beliebige Taste). | S |
| AN-2 | Animierbare Eigenschaften: Gap/Spreizung, Skalierung, Rotation, Deckkraft, Farbe. | S |
| AN-3 | Mehrstufig (Keyframes), Dauer und Easing pro Stufe, Modus „einmal“ oder „solange gehalten“. | S |
| AN-4 | Eingaben werden nur gelesen (passiver Low-Level-Hook), nie blockiert oder verändert. | M |

### 3.4 Bibliothek & Profile
| ID | Anforderung | Prio |
|---|---|---|
| LB-1 | Unbegrenzt viele Crosshairs speichern, umbenennen, duplizieren, löschen. | M |
| LB-2 | Vorschaubilder in der Bibliothek. | M |
| LB-3 | Mitgelieferte, selbst gestaltete Starter-Presets (≥ 15). | M |
| LB-4 | Aktives Crosshair per Klick setzen. | M |
| LB-5 | Profile pro Spiel: Monitor, Offset, Größe, aktives Crosshair; automatische Aktivierung, wenn das Spiel-Executable im Vordergrund ist. | K |

### 3.5 Hotkeys
| ID | Anforderung | Prio |
|---|---|---|
| HK-1 | Frei belegbare globale Hotkeys (Taste, Maustaste, mit Modifiern). | M |
| HK-2 | Aktionen: Overlay umschalten, nächstes/vorheriges Crosshair, Ausblenden solange gehalten, Zentrieren, Größe +/–. | M |
| HK-3 | Pro gespeichertem Crosshair eine eigene Taste (Per-Weapon). | M |
| HK-4 | Smart Toggle: Ausblenden während rechte Maustaste gehalten (an/aus). | M |
| HK-5 | Hotkeys funktionieren, während das Spiel im Vordergrund ist; Option „als Administrator starten“ mit Hinweis im UI. | M |
| HK-6 | Konflikterkennung bei doppelt belegten Tasten. | S |

### 3.6 Teilen & Export
| ID | Anforderung | Prio |
|---|---|---|
| SH-1 | Export als Share-Code (kompakter, versionierter String, eigenes Format) und Import aus Code. | M |
| SH-2 | Export/Import als Datei (`.json`). | M |
| SH-3 | Export als PNG. | S |
| SH-4 | Online-Galerie zum Hochladen/Durchsuchen. | K |

### 3.6a Import vorhandener Crosshairs
| ID | Anforderung | Prio |
|---|---|---|
| IM-1 | Valorant-Crosshair-Code einfügen → wird in ein Crosshair (Klassisch-Layer innen + außen, Punkt, Outline, Farbe inkl. Custom-Hex) umgewandelt. Profil „Primär“ (`P`) wird verwendet. | M |
| IM-2 | Valorant-„Firing Error“ wird als Animation (Spreizung bei Linksklick) übernommen. | S |
| IM-3 | Größen werden wie in Valorant auf 1080p bezogen und mit der Monitorhöhe skaliert (Referenzhöhe im Crosshair gespeichert). | M |

### 3.7 App & System
| ID | Anforderung | Prio |
|---|---|---|
| AP-1 | Tray-Icon: Editor öffnen, Overlay an/aus, Crosshair wählen, Beenden. | M |
| AP-2 | Optional mit Windows starten (minimiert ins Tray). | S |
| AP-3 | Einstellungen und Bibliothek lokal persistent (`%APPDATA%`), überleben Updates. | M |
| AP-4 | UI auf Deutsch und Englisch. | S |
| AP-5 | Installer (NSIS) und portable Version. | S |
| AP-6 | Single-Instance: zweiter Start holt das vorhandene Fenster nach vorn. | M |

## 4. Nicht-funktionale Anforderungen
| ID | Anforderung |
|---|---|
| NF-1 | Overlay im Leerlauf (statisches Crosshair) < 1 % CPU, kein messbarer FPS-Verlust im Spiel. Rendering nur bei Änderungen/Animationen. |
| NF-2 | Reaktionszeit Hotkey → sichtbare Änderung < 16 ms. |
| NF-3 | Plattform: Windows 10/11 x64. |
| NF-4 | Keine Telemetrie, keine Netzwerkverbindung im MVP. |
| NF-5 | Datenformat versioniert mit Migrationen. |
| NF-6 | Testabdeckung für Datenmodell, Share-Code, Geometrie/Rendering-Logik und Hotkey-Zuordnung. |

## 5. Datenmodell (Entwurf)

```ts
interface CrosshairDoc {
  schema: 1;
  id: string; name: string; createdAt: string; updatedAt: string;
  layers: Layer[];            // Reihenfolge = Zeichenreihenfolge
}
type Layer = ClassicLayer | RadialLayer | ShapeLayer | ImageLayer | TextLayer;
interface LayerBase {
  id: string; name: string; visible: boolean;
  color: string; opacity: number; rotation: number; scale: number;
  offset: { x: number; y: number }; blur: number;
  outline: { enabled: boolean; width: number; color: string; opacity: number };
  animation?: Animation;
}
interface Settings {
  activeCrosshairId: string; overlayEnabled: boolean;
  monitorId: string; offset: { x: number; y: number }; globalScale: number;
  hideOnRightClick: boolean; launchOnStartup: boolean; language: 'de' | 'en';
  hotkeys: { action: HotkeyAction; binding: KeyBinding | null }[];
}
```

## 6. Akzeptanzkriterien MVP
1. App starten → Crosshair erscheint mittig auf dem Hauptmonitor; Klicks gehen durch.
2. Im Editor eine Linie ändern → Overlay aktualisiert sich ohne Neustart.
3. Hotkey „Overlay umschalten“ funktioniert, während ein Borderless-Spiel/Fenster den Fokus hat.
4. Rechtsklick halten (Smart Toggle aktiv) → Crosshair weg; loslassen → wieder da.
5. Zwei Crosshairs mit eigener Taste → Tastendruck wechselt sofort.
6. Share-Code exportieren, löschen, wieder importieren → identisches Crosshair.
7. Monitor wechseln → Crosshair mittig auf dem anderen Monitor.
8. App neu starten → alle Einstellungen und Crosshairs sind noch da.
9. Valorant-Code `0;P;c;5;h;0;0l;4;0o;2;0a;1;0f;0;1b;0` importieren → cyanfarbenes Kreuz, Linienlänge 4, Abstand 2, ohne Outline, ohne äußere Linien.
10. Valorant in „Vollbild im Fenster“ → Overlay sichtbar, Hotkeys funktionieren.
