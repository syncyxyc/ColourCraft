# Umsetzungsplan und Status

| Meilenstein | Inhalt | Status |
|---|---|---|
| M0 | Bestandsaufnahme | ✅ abgeschlossen (18.10.2026) |
| M1 | Grundgerüst | ✅ abgeschlossen (18.10.2026) |
| M2 | Dokumentmodell, Import, Darstellung | offen |
| M3 | Ebenen und Komposition | offen |
| M4 | Werkzeuge, Auswahl, Historie | offen |
| M5 | Strichmuster-Werkzeug | offen |
| M6 | Effekte | offen |
| M7 | Projektformat `.orbit` | offen |
| M8 | Professionelle Oberfläche | offen |
| M9 | Anpassbarkeit | offen |
| M10 | Plugin-API | offen |
| M11 | Stabilisierung | offen |
| M12 | Release | offen |

## M0 – Bestandsaufnahme (18.10.2026)

- Repository `syncyxyc/ColourCraft` (privat) war leer, es gab keinen Bestandscode.
- Rechner: Windows 11 Pro (10.0.26200), x64. Git 2.55 vorhanden.
- **Vorher kein .NET SDK installiert.** Installiert wurde .NET SDK 10.0.402 (winget,
  `Microsoft.DotNet.SDK.10`). .NET 10 ist die aktuelle LTS-Version.
- Kein Inno Setup / WiX geprüft – relevant erst für M12.
- Risiken:
  - Umfang: Die Spezifikation ist sehr groß; Umsetzung strikt in Meilensteinen.
  - Single-File-EXE ist groß (142 MB unkomprimiert, siehe build-and-release.de.md).
  - AvalonDock: Lizenz (Ms-PL) und .NET-10-Kompatibilität sind vor M8 zu prüfen.
  - UI-Tests: vollautomatisiert nur über den eingebauten Smoke-Test; komplexe Interaktionen
    (Ziehen von Kontrollpunkten) brauchen in M5 zusätzlich ein manuelles Protokoll.

## M1 – Grundgerüst (18.10.2026)

Erfüllt:
- Solution `OrbitStudio.sln` mit 6 Quell- und 4 Testprojekten, zentrale Paketverwaltung.
- Startfenster mit vollständiger Menüstruktur (10 Menüs) aus der zentralen Command-Registry.
- Dokument-Tabs: mehrere Dokumente, Tab schließen, Tabwechsel, Titel mit Änderungsmarkierung.
- Arbeitsfläche mit Schachbrett (Transparenz) und Dokumenthintergrund, Scrollleisten.
- Statusleiste: Werkzeugstatus, Bildgröße, Cursorposition (Dokumentpixel), Zoom.
- Dialog „Neues Bild“ mit Validierung (Größe, Name), Hintergrund Weiß/Transparent.
- Dokumentinformationen, Über-Dialog, Vollbild (F11), Schließen/Alle schließen/Beenden.
- Basis-Theme über Design-Tokens, deutsche Texte zentral in Ressourcen.
- Settings-Dienst (global + Arbeitsbereich getrennt, versioniert, Fallback, atomares Speichern);
  Fensterposition/-größe/-zustand wird gespeichert und wiederhergestellt.
- Datei-Logging ohne Telemetrie, globale Fehlerbehandlung mit deutscher Meldung.
- Pro-Monitor-DPI (PerMonitorV2) per Manifest.
- `build.ps1`, `test.ps1`, `publish.ps1`, `clean.ps1`.
- Tests: 64 Unit-/Architekturtests + 18 UI-Prüfungen, alle bestanden (siehe testing.de.md).
- Single-File-EXE veröffentlicht und erfolgreich gestartet (Smoke-Test bestanden).

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- Zoom, Schwenken, Koordinatentransformationen, DPI-genaue 100 %-Darstellung → M2.
- Symbolleisten, andockbare Panels, Farbfelder → M8.
- App-Icon → M8.

## Nächster Schritt: M2

Pixelpuffer (BGRA, premultiplied) in Domain/Imaging, SkiaSharp-Codecs für PNG/JPEG/BMP,
Darstellung des Puffers, zentrale Koordinatentransformation (Dokument ↔ Viewport ↔ Bildschirm)
inkl. DPI, Zoom (Mausrad um den Mauszeiger, Anpassen, 100 %), Schwenken, Exportieren.
