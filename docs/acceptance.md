# Abnahmeprotokoll

Stand: 17. November 2026

| Kriterium | Status | Nachweis |
|---|---|---|
| 1. Overlay mittig und klickdurchlässig | automatisiert geprüft | `OverlayPlacementTests`, Overlay-Fensterimplementierung |
| 2. Designer aktualisiert Overlay | automatisiert geprüft | `Designer_FlushSave_UpdatesLibraryAndActiveOverlayDoc` |
| 3. Overlay-Hotkey im Vordergrund | automatisiert geprüft | `HotkeyMatcherTests`; Spiel-Fokus manuell offen |
| 4. Smart Toggle bei Rechtsklick | automatisiert geprüft | `HotkeyMatcherTests` |
| 5. Crosshair-Taste wechselt Profil | automatisiert geprüft | `HotkeyMatcherTests`; echter Spiel-Fokus manuell offen |
| 6. Share-Code Roundtrip | automatisiert geprüft | Core-Codec- und App-Importtests |
| 7. Monitorwechsel | automatisiert geprüft | `OverlayPlacementTests`; zweiter physischer Monitor manuell offen |
| 8. Persistenz nach Neustart | automatisiert geprüft | `StorageTests`, `AppStateTests` |
| 9. Valorant-Beispielcode | automatisiert geprüft | `ImportDialog_DetectsFormat`, `Parse_Requirements_Example` |
| 10. Valorant im Borderless-Modus | offen | benötigt einen echten Valorant-Client und Nutzerprüfung |

Die automatisierte Release-Suite bestand mit 184 Tests (152 Core, 32 App).
