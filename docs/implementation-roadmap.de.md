# Umsetzungsplan und Status

| Meilenstein | Inhalt | Status |
|---|---|---|
| M0 | Bestandsaufnahme | ✅ abgeschlossen (18.10.2026) |
| M1 | Grundgerüst | ✅ abgeschlossen (18.10.2026) |
| M2 | Dokumentmodell, Import, Darstellung | ✅ abgeschlossen (18.10.2026) |
| M3 | Ebenen und Komposition | ✅ abgeschlossen (18.10.2026) |
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

## M2 – Dokumentmodell, Import, Darstellung (18.10.2026)

Erfüllt:
- Pixelmodell in der Domäne: `Bgra32` (vormultipliert), `PixelBuffer` (dokumentiertes Layout,
  Größenprüfung vor Reservierung), `ColorMath`, `IntRect`, `PointD`, verlustfreies Drehen/Spiegeln.
- Dokumente besitzen einen Pixelpuffer; Weiß/Transparent beim Anlegen; Pixeländerungen werden mit
  Bereich gemeldet (Dirty-Rectangle) und nur dort neu übertragen.
- Neues Projekt `OrbitStudio.Imaging`: Codec-Vertrag mit Fähigkeiten und Fehlerklassen, Registry,
  PNG/JPEG/WebP/GIF-Lesen über SkiaSharp (sRGB, EXIF-Ausrichtung), eigener BMP-Encoder.
- Neues Projekt `OrbitStudio.Rendering`: zentrale `ViewTransform` (Dokument ↔ Viewport ↔ DIP, Zoom um
  einen Ankerpunkt, Stufen 1–3.200 %, Anpassen, 100 % = 1 Gerätepixel, Zentrieren, Grenzen, Scrollleisten).
- Leinwand `CanvasView`: scharfe Pixel ab 100 %, hochwertige Verkleinerung darunter, Pixelraster ab 800 %
  (abschaltbar, gespeichert), Mausrad-Zoom am Mauszeiger, Verschieben mit mittlerer Maustaste oder
  Leertaste + Ziehen, eigene Scrollleisten, Cursorposition in Bildpixeln.
- Datei › Öffnen (Mehrfachauswahl), Drag-and-drop, Kommandozeile, „Zuletzt verwendet“ (bereinigt
  fehlende Dateien), bereits geöffnete Dateien werden nur aktiviert.
- Datei › Exportieren nach PNG, JPEG, BMP, WebP; Warnung, wenn Transparenz verloren geht; Lesen und
  Schreiben im Hintergrund; atomares Ersetzen; deutsche Fehlermeldungen je Fehlerart.
- Ansicht › Vergrößern, Verkleinern, 100 %, Bild anpassen, Pixelraster.
- Startparameter `--data-dir <Ordner>` für isolierte Einstellungen/Logs.
- Tests: 181 Unit-/Integrations-/Architekturtests, 42 Smoke-Prüfungen, Eingabetest mit echter
  Maus/Tastatur (siehe testing.de.md). Single-File-EXE gestartet, Smoke-Test bestanden.

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- „Importieren“ (Bild als Ebene) → M3; „Speichern“ als `.orbit` → M7.
- PNG-Export-Warnung „Ebenen werden zusammengeführt“ → M3 (erst mit mehreren Ebenen sinnvoll).
- TIFF wird nicht unterstützt (SkiaSharp kann es nicht).
- DPI-Darstellung bei 150 %/200 % nur per Unit-Test geprüft, nicht manuell am Bildschirm.

## M3 – Ebenen und Komposition (18.10.2026)

Erfüllt:
- Ebenenmodell in der Domäne: Pixelebenen, Gruppen (verschachtelt), Masken (8 Bit, abschaltbar), Sichtbarkeit,
  Deckkraft, Mischmodus, Sperre; Dokument mit Ebenenbaum und aktiver Ebene.
- CPU-Komposition nach W3C-Formeln mit 9 Mischmodi (Pflicht: Normal, Multiplizieren, Negativ, Addieren,
  Bildschirm; zusätzlich Differenz, Abdunkeln, Aufhellen, Überlagern), isolierte Gruppen, Masken,
  Dirty-Rectangle-Neuzusammensetzung. **Referenztests mit exakten Bytewerten** je Mischmodus.
- Neues Projekt `OrbitStudio.Layers`: alle Ebenenoperationen mit Vorbedingungen und Fehlerklassen.
- Menü „Ebenen“ vollständig aktiv (bis auf „Auswahl aus Ebenentransparenz“ und „Ebene transformieren“ → M4),
  Tastenkürzel (Strg+Umschalt+N, Strg+J, F2, Strg+Umschalt+Pfeil hoch/runter, Strg+G, Strg+Umschalt+G, Strg+E, Strg+Umschalt+F).
- Einfaches Ebenen-Panel rechts: Liste mit Einrückung, Sichtbarkeit, Sperre, Maskenanzeige, Deckkraft-Regler,
  Mischmodus-Auswahl, Schaltflächen; Doppelklick öffnet „Ebeneneigenschaften“.
- Datei › Importieren (Strg+Umschalt+O): Bilder als Ebenen, Meldung bei Beschnitt.
- Export setzt sichtbare Ebenen zusammen und warnt vorher bei mehreren Ebenen (Spezifikation 11.1).
- Rückfragen vor nicht umkehrbaren Ebenenänderungen (Löschen, Zusammenführen, Reduzieren, Maske anwenden),
  solange es kein Rückgängig gibt.
- Tests: 260 Unit-/Integrations-/Architekturtests, 79 Smoke-Prüfungen (inkl. Ebenenbefehle über die Registry mit
  geskripteten Dialogen). Single-File-EXE geprüft.

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- Rückgängig/Wiederholen für Ebenenoperationen → M4.
- Auswahl aus Ebenentransparenz, Ebenen transformieren → M4.
- Ebenen-Panel andockbar, Vorschaubilder, Ziehen zum Umsortieren, Verschieben in/aus Gruppen → M8.
- Maske bemalen → M4 (Werkzeuge).

## Nächster Schritt: M4

Historie (Undo/Redo, Command-Pattern mit Deltas), Werkzeug-API `ITool`, Zeichenwerkzeuge (Pinsel, Bleistift,
Radierer, Linie, Rechteck, Ellipse), Auswahlwerkzeuge und Auswahlmasken, Transformation, Farbwahl;
Ebenenoperationen werden rückgängig machbar. Je Werkzeuggruppe mindestens ein Integrationstest.
