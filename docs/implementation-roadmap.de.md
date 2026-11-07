# Umsetzungsplan und Status

| Meilenstein | Inhalt | Status |
|---|---|---|
| M0 | Bestandsaufnahme | ✅ abgeschlossen (18.10.2026) |
| M1 | Grundgerüst | ✅ abgeschlossen (18.10.2026) |
| M2 | Dokumentmodell, Import, Darstellung | ✅ abgeschlossen (18.10.2026) |
| M3 | Ebenen und Komposition | ✅ abgeschlossen (18.10.2026) |
| M4 | Werkzeuge, Auswahl, Historie | ✅ abgeschlossen (18.10.2026) |
| M5 | Strichmuster-Werkzeug | ✅ abgeschlossen (18.10.2026) |
| M6 | Effekte | ✅ abgeschlossen (18.10.2026) |
| M7 | Projektformat `.orbit` | ✅ abgeschlossen (18.10.2026) |
| M8 | Professionelle Oberfläche | in Arbeit (Modelle fertig, WPF-Oberfläche ausstehend unter Windows) |
| M9 | Anpassbarkeit | in Arbeit (Modelle fertig, Oberfläche ausstehend unter Windows) |
| M10 | Plugin-API | in Arbeit (Laufzeit und Beispielplugin fertig, Verwaltungsdialog ausstehend unter Windows) |
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

## M4 – Werkzeuge, Auswahl, Historie (18.10.2026)

Erfüllt:
- Neues Projekt `OrbitStudio.History`: Historie je Dokument mit Pixel-/Masken-Deltas, Auswahl-, Ebenenbaum- und
  Inhaltsschnappschüssen, Zusammenfassen von Reglerschritten, Grenzen für Anzahl und Speicher (Einstellungen).
  Rückgängig (Strg+Z) / Wiederholen (Strg+Y) für alle Bearbeitungen inkl. aller Ebenenoperationen und Panel-Änderungen.
- Neues Projekt `OrbitStudio.Tools`: Werkzeug-API `ITool` mit Optionen, Überlagerungen, Abbruch/Übernahme;
  22 Werkzeuge in 5 Gruppen (Auswahl 6, Transformation 2, Zeichnen 9, Füllung/Farbe 3, Ansicht 2).
- Auswahlmodell mit Grenzregel, Verknüpfungsmodi, Zauberstab/Farbbereich mit Toleranz, Erweitern/Verkleinern
  (euklidisch), Umkehren, Alles auswählen, Aufheben, Auswahl aus Ebenentransparenz; Auswahlkontur auf der Leinwand.
  Alle Malwerkzeuge sind auf Auswahl und Bildgrenzen begrenzt.
- Bearbeiten: Ausschneiden, Kopieren, Einfügen (als neue Ebene, an Originalposition, mit Auswahl und
  Verschieben-Werkzeug), Löschen; Zwischenablage mit PNG (Transparenz) und Standard-Bitmap.
- Bild: Bildgröße, Leinwandgröße (Ausrichtung), Drehen 90°/180°/270°, Spiegeln, Auf Auswahl zuschneiden.
  Ebenen: spiegeln, frei drehen/skalieren (Dialog), Maske bemalen („Maske bearbeiten“).
- Oberfläche: Werkzeugleiste mit Vorder-/Hintergrundfarbe (Farbdialog mit RGBA und Hex, Tauschen X, Zurücksetzen D),
  Optionsleiste (Größe, Härte, Deckkraft, Kantenglättung, Linienbreite, Toleranz, Modi), Werkzeug-Tastenkürzel,
  Statusleiste mit Werkzeug, Auswahlgröße und Meldungen („Rückgängig: Pinsel“, gesperrte Ebene …).
- Die Rückfragen vor Löschen/Zusammenführen/Reduzieren/Maske anwenden aus M3 sind entfallen (rückgängig machbar).
- Tests: 333 Unit-/Integrations-/Architekturtests (je Werkzeuggruppe ein Integrationsablauf), 117 Smoke-Prüfungen.
  Single-File-EXE geprüft.

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- Text, Klonen, Retusche (Werkzeuggruppe „Erweitert“) und Farbfelder.
- Freies Drehen/Skalieren von Ebenen nur per Dialog, ohne Anfasser und Live-Vorschau.
- Eingabetest mit echter Maus für das Malen: Skript erweitert, in M4 nicht ausgeführt.

## M5 – Strichmuster-Werkzeug (18.10.2026)

Erfüllt (Spezifikation 1.1):
- Werkzeug „Strichmuster“ (Kategorie Zeichnen, Umschalt+L). Linie aufziehen; Start- und Endpunkt danach als
  Kontrollpunkte ziehbar, die Linie verschiebt beide; Umschalt rastet 15° ein; Pfeiltasten verschieben.
- Kontrollpunkte und Hilfslinie sind reine Überlagerungen; die Vorschau ist eine temporäre Raster-Überlagerung
  (`OverlayRaster`), die Ebene bleibt bis „Anwenden“ unverändert. Vorschau = Ergebnis (byteweise getestet).
- Trefferprüfung in Bildschirmpixeln → korrekt bei Zoom, Verschiebung und DPI-Skalierung; Koordinaten als `PointD`.
- Länge, Winkel, Richtung, Mittelpunkt und Strichanzahl werden laufend angezeigt; Felder X1/Y1/X2/Y2 synchron mit der
  Leinwand (beide Richtungen, Enter übernimmt).
- Parameter Linienstärke, Strichlänge, Strichabstand (Feld + Schieberegler), Farbe mit Alpha (Farbfeld → Farbdialog,
  Hex-Feld), Deckkraft, Kantenglättung; Standardwerte und Minima laut Spezifikation, ungültige Eingaben begrenzt bzw.
  abgewiesen. Periode = Strichlänge + Strichabstand (exakte Pixeltests).
- Segmentberechnung `DashLine` ohne UI mit allen geforderten Randfällen (identische Punkte, kürzer als ein Strich,
  Abstand 0, sehr große Werte, alle Winkel); Rasterung nur im Band um die Linie.
- Anwenden (Schaltfläche/Enter) auf die aktive Ebene bzw. Maske, begrenzt auf Auswahl und Bildgrenzen, als ein
  Rückgängig-Schritt; Abbrechen (Schaltfläche/Esc) verwirft vollständig; Zurücksetzen stellt die Standardwerte her.
- Werkzeug-API erweitert: Optionstypen Farbe, Schaltfläche, Anzeige, Freigabezustand; `OverlayRaster`;
  `PointerLost`, `ViewChanged`. Optionsleiste bricht um.
- **Abnahmekriterium ausgeführt** (siehe testing.de.md): im echten Fenster (Debug und veröffentlichte EXE) per
  Smoke-Test sowie als Integrationstest.
- Tests: 390 Unit-/Integrations-/Architekturtests, 135 Smoke-Prüfungen. Single-File-EXE geprüft.

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- Eingabetest mit echter Windows-Maus für das Strichmuster: Skript erweitert, **nicht ausgeführt** (Sitzung aktiv).
- Vorschau ohne Ebenen-Deckkraft/-Mischmodus; Optionsleiste mehrzeilig bis zur Options-Palette (M8);
  Parameter nicht gespeichert (M9).

## M6 – Effekte (18.10.2026)

Erfüllt:
- Neues Projekt `OrbitStudio.Effects` (nur Domain und History): Effektvertrag mit ID, Name, Kategorie, Beschreibung,
  Parametern (Zahl, Ja/Nein, Farbe) mit Standardwerten, Wertebereichen und Validierung (Einzelwerte und Regeln),
  Registry, Engine mit Auswahl-Mischung, Abbruch, Vorschau in reduzierter Auflösung und Übernahme als Rückgängig-Schritt.
- Alle 14 Pflicht-Effekte aus Abschnitt 9 (Gaußscher und einfacher Weichzeichner, Schärfen, Helligkeit, Kontrast,
  Farbton, Sättigung, Invertieren, Schwarzweiß, Sepia, Kantenerkennung, Rauschen, Rauschreduktion,
  Farbkanalanpassung), dazu Tonwertkorrektur, Schlagschatten, Verpixeln und die kombinierten Dialoge
  Helligkeit/Kontrast und Farbton/Sättigung – 19 registrierte Effekte.
- **Referenztests mit exakten, von Hand berechneten Pixelwerten** je Effekt (Gauß gegen eine naive 2D-Referenz ±1,
  einfacher Weichzeichner exakt gegen eine naive Referenz).
- Menüs „Farben“ und „Effekte“ aktiv (außer Farbkurven → M8, Erweiterungen → M10), Tastenkürzel Strg+U, Strg+L,
  Strg+Umschalt+B, Strg+Umschalt+I.
- Effektdialog mit Schiebereglern, Zahlenfeldern, Farbfeld, Live-Vorschau (Hintergrund, abbrechbar), „Original
  zeigen“, „Zurücksetzen“, Regelprüfung; Ausführung als abbrechbarer Hintergrundjob (Esc), Befehle und Werkzeuge
  währenddessen gesperrt.
- Tests: 436 Unit-/Integrations-/Architekturtests, 153 Smoke-Prüfungen (Debug und veröffentlichte EXE).

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- Farbkurven (Kurveneditor), Effektbrowser, Fortschrittsanzeige → M8; Plugin-Effekte → M10; Messungen → M11.
- Effekte sind destruktiv mit Rückgängig (E-039).

## M7 – Projektformat (18.10.2026)

Erfüllt:
- Neues Projekt `OrbitStudio.Storage` (nur Domain): versioniertes, dokumentiertes Format `.orbit` (ZIP, `manifest.json`,
  rohe vormultiplizierte Pixel je Ebene, Masken, SHA-256 je Datenblock). Gespeichert werden Abmessungen, Auflösung,
  Hintergrund, Ebenen und Gruppen (verschachtelt), Pixel, Namen, Sichtbarkeit, Deckkraft, Mischmodi, Sperre, Masken
  (inkl. deaktiviert), aktive Ebene, Metadaten, Formatversion.
- Prüfung vor dem vollständigen Laden; beschädigte, manipulierte, fremde, zu große und neuere Dateien werden
  kontrolliert mit deutscher Meldung abgelehnt. Migrationskette für künftige Versionen (mit Test-Migrationen geprüft).
- Sichere Dateiersetzung (temporäre Datei, auf Datenträger schreiben, erneut prüfen, ersetzen); Abbruch und Fehler lassen
  die vorhandene Datei unverändert (getestet: Abbruch, schreibgeschützte Datei, fehlender Ordner).
- Datei › Speichern (Strg+S), Speichern unter (Strg+Umschalt+S), Kopie speichern, Öffnen (auch Drag-and-drop,
  Kommandozeile, Zuletzt verwendet). Speicherpunkt in der Historie: Rückgängig bis zum gespeicherten Stand entfernt das
  Sternchen. Rückfrage „Speichern / Nicht speichern / Abbrechen“ beim Schließen, bei „Alle schließen“ und beim Beenden.
- **Pflicht-Workflow aus Abschnitt 13 besteht als automatisierter Test** (alle 14 Schritte).
- `build/test.ps1` baut die App vor dem Smoke-Test neu (vorher konnte eine veraltete EXE geprüft werden).
- Tests: 488 Unit-/Integrations-/Architekturtests, 166 Smoke-Prüfungen (Debug und veröffentlichte EXE).

Offen / bewusst verschoben (siehe bekannte-einschraenkungen.md):
- Auswahl, Ansicht und Historie werden nicht im Projekt gespeichert; kein Vorschaubild, keine automatische Sicherung.
- Laufzeit von Speichern/Öffnen großer Projekte nicht gemessen (M11).

## M8 – Professionelle Oberfläche (begonnen 22.10.2026, macOS ohne WPF-Laufzeit)

Umgebung: Dieser Teil entstand auf macOS (.NET SDK 10.0.402, Core-Teile gebaut und getestet; `OrbitStudio.Presentation`
kompiliert mit `-p:EnableWindowsTargeting=true`, **wurde aber nicht gestartet**). Gemäß Spezifikation 15.4 gilt alles
mit WPF als „ausstehend unter Windows“.

Erfüllt (Modell und Logik, getestet):
- **Farbkurven:** `ToneCurve` (2–16 Kontrollpunkte, monotone kubische Interpolation nach Fritsch–Carlson, 256er-Tabelle,
  Editor-Operationen Hinzufügen/Verschieben/Entfernen/Treffertest) und Effekt `CurvesEffect` (Gesamt-, Rot-, Grün-,
  Blaukurve) mit `CurveParameter`. 21 neue Tests mit exakten Werten (u. a. Invertierkurve = `InvertEffect` Byte für Byte).
- **Farbfelder:** `SwatchPalette` (Domain; Hinzufügen, Entfernen, Ersetzen, Verschieben, Hex-Rundreise, Standardpalette,
  Höchstzahl 256, keine Doppelten) und `WorkspaceSettings.Swatches` (Speichern, Normalisieren, Meldung „korrigiert“).
  9 + 3 neue Tests.
- **Panel-Layouts und Arbeitsbereiche:** neues Projekt `OrbitStudio.Docking` (keine Abhängigkeiten, kein WPF/AvalonDock):
  `PanelLayout` (8 Standard-Panels, Sichtbarkeit, Größe, Seite und Reihenfolge, Standardanordnung, Prüfung und Reparatur
  fehlerhafter Layouts) und `WorkspaceCatalog` (benannte Arbeitsbereiche speichern, laden, umbenennen, löschen, „Standard“
  geschützt, „Layout zurücksetzen“, versioniertes JSON mit Fallback bei beschädigten oder neueren Dateien). 33 neue Tests;
  Architekturtest um das Projekt erweitert.
- **Hilfslinien, Raster, Lineale:** `GuideSet` (Hilfslinien mit Treffertest und Einrasten), `GridSettings` (Rasterpunkte) und
  `RulerScale` (1-2-5-Teilung nach Zoom, Teilstriche im sichtbaren Bereich) in `OrbitStudio.Rendering`. 21 neue Tests.
- Testlauf auf macOS: siehe „Testergebnisse 23.10.2026“ unten; die gesamte Solution (inkl. WPF-Projekte) kompiliert mit
  `-p:EnableWindowsTargeting=true`.

Offen (ausstehend unter Windows): Kurveneditor-Steuerelement und Registrierung von „Farbkurven“ im Menü, Farbfelder-Panel,
Anbindung von `PanelLayout`/`WorkspaceCatalog` an andockbare Panels (AvalonDock-Lizenz/.NET-10-Prüfung) und an eine Datei
im Arbeitsbereich-Ordner, Fenster-Menü „Panels“/„Arbeitsbereiche“, Symbolleisten, Historie-Panel, Navigator, Effektbrowser, Lineale,
Raster, Hilfslinien, Icons, Layoutspeicherung.

## M9 – Anpassbarkeit (Modelle, 23.10.2026, macOS)

Erfüllt (ohne Oberfläche, getestet):
- **Themes:** neues Projekt `OrbitStudio.Themes`: 11 Farb-Tokens, Abstand, Eckenradius, UI-Größe; eingebaute Themes „Hell“ und
  „Dunkel“ (beide bestehen die Prüfung); Validierung inkl. WCAG-Kontrast (Text 4,5:1, Akzent/Fokus 3:1); Import/Export als
  versioniertes JSON (wirft nie, liefert deutsche Fehler); `ThemeCatalog` mit Rückfall auf „Hell“ bei unbekanntem oder
  ungültigem Theme. 22 Tests.
- **Tastenkürzel-Profile:** `GestureProfile` (Application): Export der Abweichungen, Import mit Prüfung, Konfliktauflösung und
  Meldung übersprungener Einträge, `AssignExclusive` für den Editor (verdrängte Befehle werden gemeldet). 11 Tests.

Offen (ausstehend unter Windows): Theme-Editor und Wechsel zur Laufzeit (WPF-Ressourcen aus den Tokens), Tastenkürzel-Editor-Dialog
mit Suche, Layoutmanager-Dialog, Menü-/Symbolleistenanpassung, Einstellungen-Dialog, Konfigurationsimport/-export als Gesamtpaket.

## M10 – Plugin-API (Laufzeit, 23.10.2026, macOS)

Erfüllt (getestet, mit echten Assembly-Ladevorgängen):
- `OrbitStudio.Plugin.Abstractions` (API Version 1: `IOrbitPlugin`, `IPluginHost`, `PluginManifest`) und
  `OrbitStudio.Plugin.Runtime` (`PluginLoader`): Laden nur aus dem Plugin-Ordner, Manifestprüfung, Pfadgrenze, API-Versionsprüfung,
  Deaktivieren, Fehlerisolierung (ein defektes Plugin stoppt weder den Start noch andere Plugins; halb angemeldete Effekte werden
  nicht übernommen), Präfix- und Duplikatprüfung der Effekt-IDs.
- Beispielplugin `plugins/OrbitStudio.SamplePlugin` mit echtem Effekt „Tonwertstufen“; Weg von Bauen über Installieren bis Nutzen
  wird im Test `SamplePlugin_IsBuiltInstalledAndUsed_EndToEnd` ausgeführt. 17 Tests.
- `docs/plugin-development.de.md` mit Vertrauensmodell (**keine Sandbox**), Schritt-für-Schritt und Fehlertabelle.

Offen (ausstehend unter Windows): Plugin-Verwaltungsdialog und Menü „Erweiterungen“, Laden beim Programmstart aus
`%APPDATA%\OrbitStudio\plugins`, Speichern der Deaktivierungsliste, Plugin-Panels/-Werkzeuge/-Importer (API ≥ 2), Plugin-Konfiguration.

## Testergebnisse 23.10.2026 (macOS, .NET SDK 10.0.402, nur Logik-Tests; keine UI-/Smoke-Tests)

| Testprojekt | bestanden | fehlgeschlagen |
|---|---|---|
| Application | 37 | 0 |
| Architecture | 38 | 0 |
| Configuration | 22 | 0 |
| Docking | 33 | 0 |
| Domain | 83 | 0 |
| Effects | 63 | 0 |
| History | 16 | 0 |
| Imaging | 27 | 0 |
| Integration | 28 | 1 |
| Layers | 23 | 0 |
| Plugin | 17 | 0 |
| Rendering | 89 | 0 |
| Storage | 38 | 0 |
| Themes | 22 | 0 |
| Tools | 106 | 0 |
| **Summe** | **642** | **1** |

Der eine Fehlschlag ist `ProjectFileServiceTests.SaveOverReadOnlyFile_ReportsAccessDenied_AndKeepsFile` (Integration): Unter
macOS überschreibt `File.Move(..., overwrite: true)` eine Datei mit gesetztem Schreibschutz-Attribut ohne Fehler (Ergebnis
`ProjectError.None` statt `AccessDenied`); unter Windows ist das Verhalten ein anderes (dort in M7 bestanden). Er trat schon vor
den Änderungen vom 22./23.10. auf. **Nicht ausgeführt** auf diesem Rechner: alle Smoke-/UI-Tests (brauchen Windows und WPF) und
die Build-Skripte (PowerShell, `build/*.ps1`). Die gesamte Solution wurde nur *kompiliert*
(`dotnet build OrbitStudio.sln -p:EnableWindowsTargeting=true`: 0 Warnungen, 0 Fehler), die App nie gestartet.

## Nächster Schritt: M8 (Rest)

Professionelle Oberfläche: andockbare Panels (Ebenen, Farben, Historie, Navigator, Eigenschaften, Werkzeugoptionen,
Effektbrowser), Symbolleisten, Farbfelder, Farbkurven-Editor, Lineale/Raster/Hilfslinien, eigene Icons, Layoutspeicherung.
Vorher Lizenz und .NET-10-Kompatibilität von AvalonDock prüfen.
