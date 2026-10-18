# ORBIT Studio – Projektspezifikation für Claude Code

> Diese Datei ersetzt die bisherige CLAUDE.md. Benenne sie beim Projektstart in `CLAUDE.md` um, damit Claude Code sie automatisch liest.
> Lies sie vollständig, bevor du beginnst. Halte dich an die Regeln in Abschnitt 0 und arbeite die Phasen in Abschnitt 12 der Reihe nach ab.

---

## 0. Grundregeln (verbindlich)

- **Keine Erfolgsbehauptungen ohne Nachweis.** Eine Funktion gilt erst dann als fertig, wenn sie implementiert, getestet und tatsächlich ausgeführt wurde. Die bloße Existenz einer Klasse oder eines UI-Elements zählt nicht.
- **Erfinde keine Test- oder Messergebnisse.** Nicht ausgeführte Tests und Messungen werden ausdrücklich als „nicht ausgeführt“ gekennzeichnet.
- **Keine Mock-ups, keine Demo-Oberflächen ohne Funktion.** Jeder sichtbare Befehl muss echte Pixel verändern oder eine echte Konfiguration ändern.
- **Keine Platzhalter im Endstand.** Unvollständige Teile werden in `docs/bekannte-einschraenkungen.md` eindeutig benannt, nicht stillschweigend ausgelassen.
- **Autonom arbeiten.** Entscheidungen, die nicht vorgegeben sind, werden getroffen, begründet in `docs/entscheidungen.md` festgehalten und umgesetzt. Rückfragen nur, wenn eine Entscheidung den Nutzer zwingend betrifft.
- **Recherchiere selbstständig** in offiziellen Dokumentationen (Microsoft Learn, NuGet, Repositories der Bibliotheken). Prüfe vor jeder Abhängigkeit Lizenz, Wartungsstand, .NET-Kompatibilität und Windows-Unterstützung.
- **Nach jedem Meilenstein:** `build`, `test`, Git-Commit, Dokumentation aktualisieren. Bestehende funktionierende Teile nicht blind überschreiben.
- **Keine fremden Inhalte kopieren.** Keine proprietären Quellcodes, Icons, Markenzeichen oder Ressourcen anderer Programme (z. B. Paint.NET). Bedienlogik darf sich an gängigen Bildeditoren orientieren; Code, Icons und Design sind eigenständig.
- **Keine Abhängigkeit von fremden Plugin-Systemen** und keine Laufzeit-Downloads beim Start. Die Kernfunktionen müssen offline laufen.

---

## 1. Produkt

**ORBIT Studio** ist ein eigenständiges Windows-Desktop-Bildbearbeitungsprogramm (x64, Windows 10/11) mit:

- mehreren Dokumenten gleichzeitig (Tabs, unabhängige Historie je Dokument)
- Ebenen, Gruppen, Deckkraft, Mischmodi, Masken
- Zeichen-, Auswahl-, Transformations- und Füllwerkzeugen
- Filtern und Farbeffekten mit interaktiver Vorschau
- zuverlässigem Undo/Redo
- Import/Export gängiger Bildformate und eigenem, versioniertem Projektformat `.orbit`
- konfigurierbaren Menüs, Symbolleisten, Panels, Themes und Tastenkürzeln
- versionierter Plugin-API
- vollständig deutscher Oberfläche und Dokumentation

Der Produktname steht zentral in `src/OrbitStudio.Configuration/AppInfo.cs` und ist dort austauschbar.

### 1.1 Kernfunktion: Interaktives Linien- und Strichmuster-Werkzeug

Dieses Werkzeug ist ein **fester Bestandteil** des Werkzeugsatzes (Kategorie „Zeichnen“) und hat die höchste Priorität nach dem Grundgerüst.

**Kontrollpunkte:**
- Startpunkt (X1, Y1) und Endpunkt (X2, Y2) sind direkt auf der Leinwand mit der Maus ziehbar.
- Eine sichtbare Vorschau der Linie bzw. des Musters aktualisiert sich in Echtzeit.
- Winkel, Länge, Richtung und Position werden automatisch berechnet und angezeigt.
- Numerische Eingabefelder für X1, Y1, X2, Y2 sind mit der Leinwand synchron.
- Kontrollpunkte und Hilfslinien sind **reine Overlays** und werden niemals in Pixel übernommen.
- Die Punkte funktionieren korrekt bei Zoom, Bildverschiebung und DPI-Skalierung.
- Koordinaten werden als `double` in einer Struktur `PointD` gespeichert, unabhängig von der Bildschirmauflösung.

**Parameter:**

| Parameter | Eingabe | Standard | Minimum |
|---|---|---|---|
| Linienstärke (px) | Feld + Schieberegler | 4 | 1 |
| Strichlänge (px) | Feld + Schieberegler | 10 | 1 |
| Strichabstand (px) | Feld + Schieberegler | 5 | 0 |
| Farbe inkl. Alpha | Farbwähler + Hex-Feld | Schwarz | – |
| Deckkraft | Feld + Schieberegler | 100 % | 0 % |

- Ungültige Eingaben werden abgewiesen oder begrenzt.
- **Verbindliche Regel:** Die Periode ist Strichlänge + Strichabstand. Bei 10 px und 5 px entstehen 10 px lange Striche mit jeweils 5 px Lücke.

**Segmentberechnung** (`OrbitStudio.Tools`, ohne UI testbar):
- Gesamtlänge und normierter Richtungsvektor
- gleichmäßige Platzierung der Segmente
- letztes Segment wird am Endpunkt gekürzt
- Randfälle: identische Punkte, Linien kürzer als eine Strichlänge, Abstand 0, sehr große Werte, alle Winkel (horizontal, vertikal, diagonal)

**Anwendung:**
- Die Vorschau liegt in einem temporären Overlay-Layer, das Original bleibt unverändert.
- **Anwenden** übernimmt das Muster auf die aktive Ebene, begrenzt auf eine aktive Auswahl und die Bildgrenzen.
- **Abbrechen** verwirft die Bearbeitung vollständig.
- **Zurücksetzen** stellt die Standardwerte wieder her.
- Die Übernahme ist ein Eintrag im Undo/Redo-System.

**Abnahmekriterium:** Der Nutzer verschiebt zwei Kontrollpunkte auf der Leinwand, die Vorschau passt sich an, alle Parameter wirken, das Ergebnis wird auf die aktive Ebene übernommen, und die Übernahme lässt sich mit Rückgängig zurücknehmen und mit Wiederholen wiederherstellen. Dieses Kriterium muss tatsächlich ausgeführt und in `docs/testing.de.md` dokumentiert werden.

---

## 2. Technologiestack (verbindlich, vor Projektstart prüfen)

| Bereich | Entscheidung |
|---|---|
| Sprache / Framework | C# mit der aktuellen .NET-LTS-Version zum Projektstart (Stand Oktober 2026: .NET 10 LTS, vorher Versionsnummer auf Microsoft Learn bestätigen) |
| UI | WPF, MVVM mit `CommunityToolkit.Mvvm` (MIT) |
| DI | `Microsoft.Extensions.DependencyInjection` |
| Logging | `Microsoft.Extensions.Logging` mit Datei-Sink (ohne Bildinhalte, ohne private Dateiinhalte) |
| Bildpuffer / Rendering | SkiaSharp (MIT) als primäre Grafik- und Codec-Schicht |
| Docking | AvalonDock (Lizenz prüfen, Version passend zur gewählten .NET-Version) |
| Tests | xUnit, Shouldly (BSD) für Assertions |
| Benchmarks | BenchmarkDotNet (MIT) |
| Icons | selbst erstellt als Vektorpfade, oder Icon-Set mit geprüfter Lizenz (MIT, OFL, CC0); Lizenz dokumentieren |

**Achtung Lizenzen:** Vor dem Einsatz jeder Bibliothek prüfen. Insbesondere ImageSharp (Six Labors) und FluentAssertions haben nicht-kostenlose Lizenzbedingungen für kommerzielle Nutzung. Bei Zweifel nicht verwenden und die Entscheidung dokumentieren.

Die Abhängigkeitsübersicht mit Lizenzen steht in `docs/dependencies-and-licenses.de.md`.

---

## 3. Solution und Projektstruktur

```
OrbitStudio.sln
src/
  OrbitStudio.App                 WPF-Startprojekt, Ressourcen, Themes, Icons
  OrbitStudio.Presentation        ViewModels, Views, Menüs, Symbolleisten, Dialoge
  OrbitStudio.Application         Commands, Use Cases, Sitzungs- und Arbeitsbereichsdienste
  OrbitStudio.Domain              Dokument, Ebene, Auswahl, Farbe, Pixel-Basistypen (ohne WPF)
  OrbitStudio.Imaging             Pixelpuffer, Farbkonvertierung, Import/Export-Codecs
  OrbitStudio.Rendering           Ebenenkomposition, Mischmodi, Ansichtstransformation, Caches
  OrbitStudio.Tools               Werkzeug-API und alle Werkzeuge inkl. Strichmuster
  OrbitStudio.Layers              Ebenenoperationen, Gruppen, Masken
  OrbitStudio.Effects             Effekt-API und Effektimplementierungen
  OrbitStudio.History             Undo/Redo, Operationen, Deltas, Snapshots
  OrbitStudio.Storage             Native .orbit-Projekte, Versionierung, Migration
  OrbitStudio.Configuration       Versionierte Einstellungen, Shortcuts, Layouts, Validierung
  OrbitStudio.Themes              Theme-Modell, Design-Tokens, Import/Export, Validierung
  OrbitStudio.Docking             Panel-Layouts und Arbeitsbereiche
  OrbitStudio.Plugin.Abstractions Öffentlicher, versionierter Plugin-Vertrag (minimal)
  OrbitStudio.Plugin.Runtime      Laden, Prüfen, Registrieren, Fehlerisolierung
  OrbitStudio.Diagnostics         Logging, Diagnosebericht (keine externe Telemetrie)
plugins/
  OrbitStudio.SamplePlugin        Beispielplugin mit einem echten Effekt
tests/
  OrbitStudio.Domain.Tests
  OrbitStudio.Imaging.Tests
  OrbitStudio.Rendering.Tests
  OrbitStudio.Tools.Tests
  OrbitStudio.Storage.Tests
  OrbitStudio.Plugin.Tests
  OrbitStudio.Integration.Tests
  OrbitStudio.Performance.Tests   (BenchmarkDotNet, nicht im Standard-Testlauf)
docs/
  architecture.md
  implementation-roadmap.de.md
  user-guide.de.md
  developer-guide.de.md
  plugin-development.de.md
  configuration.de.md
  build-and-release.de.md
  testing.de.md
  dependencies-and-licenses.de.md
  bekannte-einschraenkungen.md
  entscheidungen.md
build/
  build.ps1
  test.ps1
  publish.ps1
  clean.ps1
```

Die Aufteilung darf begründet angepasst werden. Vermeide leere Projekte und zyklische Abhängigkeiten.

### 3.1 Abhängigkeitsregeln (verbindlich)

- `Domain` kennt weder WPF noch Controls, noch Bibliotheken für Codecs.
- `Imaging` kennt keine ViewModels.
- `Rendering` arbeitet nur über definierte Dokument- und Pixelschnittstellen.
- `Tools` und `Effects` arbeiten über kontrollierte Dokumentoperationen, nicht über Controls.
- `History` kennt Operationen, nicht die UI.
- `Storage` kennt das Dokumentmodell, aber keine Fenster.
- `Presentation` bindet an `Application`-Dienste.
- `Plugin.Abstractions` enthält nur den öffentlichen Vertrag.
- Verifiziere die Regeln mit einem Architekturtest (z. B. NetArchTest oder eigener Reflection-Test) und dokumentiere ihn.

---

## 4. Datenmodell

**Dokument:** ID, Name, Breite, Höhe, Auflösung, Farbmodus, Hintergrund und Transparenz, Ebenenbaum, aktive Ebene, aktuelle Auswahl, Metadaten, eigene Historie, Speicherzustand, Speicherpfad, Formatversion.

**Ebene:** ID, Name, Position, Sichtbarkeit, Deckkraft, Mischmodus, Sperrstatus, Pixelinhalt, optional Maske, Transformationszustand.

**Pixel:** Eine einzige interne Repräsentation, festgelegt in `docs/architecture.md`:
- BGRA, 8 Bit je Kanal
- Speicherlayout und Zeilenabstand (Stride) dokumentiert
- Koordinatenkonvention: Ursprung oben links, Ganzzahl-Pixelindizes
- Alpha-Zustand (premultiplied oder straight) festgelegt und überall gleich verwendet

**Zustandstrennung:**
- Pro Dokument: Ebenen, Auswahl, aktive Ebene, Ansicht (Zoom, Verschiebung), Historie.
- Global: Einstellungen, Themes, Shortcuts, Layouts, Plugins.
- Beim Tabwechsel dürfen Ebenen, Auswahl, Farben und Undo-Zustände nicht vermischt werden.

**Validierung:** Ungültige Dimensionen (≤ 0 oder über dem Speicherlimit), übergroße Bilder und unzulässige Zustandskombinationen werden vor der Verarbeitung abgewiesen.

---

## 5. Benutzeroberfläche

### 5.1 Hauptfenster
Titelzeile, Menüleiste, Symbolleisten, Dokument-Tabs, zentrale Arbeitsfläche mit Scrollleisten, Statusleiste (Zoom, Cursorposition, Auswahlgröße, Werkzeugstatus), aktive Vorder- und Hintergrundfarbe, andockbare Panels, Vollbild, Layout zurücksetzen, Fensterzustand speichern und wiederherstellen. Unterstützung für DPI-Skalierung und hohe Auflösungen.

### 5.2 Menüs
Alle Befehle sind über eine **zentrale Command-Registry** definiert. Menüeinträge werden anhand des Dokumentzustands aktiviert oder deaktiviert. Ein Befehl ohne Dokument, Auswahl oder passenden Werkzeugzustand reagiert definiert (deaktiviert oder mit verständlicher Meldung).

Menüs:
- **Datei:** Neu, Öffnen, Zuletzt verwendet, Speichern, Speichern unter, Kopie speichern, Importieren, Exportieren, Dokumentinformationen, Schließen, Alle schließen, Beenden. Druckvorschau und Drucken als späterer Ausbau, falls nicht umgesetzt → in `bekannte-einschraenkungen.md`.
- **Bearbeiten:** Rückgängig, Wiederholen, Ausschneiden, Kopieren, Einfügen, Löschen, Alles auswählen, Auswahl aufheben, Auswahl umkehren, Einstellungen.
- **Ansicht:** Vergrößern, Verkleinern, 100 %, Bild anpassen, Lineale, Raster, Hilfslinien, Pixelraster, Vollbild, Panels, Arbeitsbereiche.
- **Bild:** Bildgröße, Leinwandgröße, Drehen, Spiegeln, Zuschneiden, Bildinformationen.
- **Ebenen:** Neue Ebene, Duplizieren, Löschen, Umbenennen, Nach oben, Nach unten, Gruppe erstellen, Gruppe auflösen, Sichtbarkeit, Deckkraft, Mischmodus, Maske hinzufügen, Zusammenführen.
- **Farben:** Helligkeit/Kontrast, Farbton/Sättigung, Tonwertkorrektur, Farbkurven, Schwarzweiß, Invertieren, Sepia.
- **Effekte:** Weichzeichnen, Schärfen, Rauschen, Rauschreduktion, Kantenerkennung, Schatten, Verzerrungen, Erweiterungen.
- **Werkzeuge:** Werkzeugoptionen, Tastenkürzel, Einstellungen.
- **Fenster:** Werkzeuge, Ebenen, Farben, Historie, Navigator, Eigenschaften, Effektbrowser, Plugins, Diagnose.
- **Hilfe:** Benutzerhandbuch, Tastenkürzel, Über ORBIT Studio, Diagnosebericht, Lizenzinformationen.

### 5.3 Symbolleisten und Tabs
- Konfigurierbar: Einträge verschieben, Gruppen ein- und ausblenden, Beschriftung umschalten, Icon-Größe wählen, Standard wiederherstellen.
- Tabs zeigen Name und ungespeicherte Änderungen, lassen sich einzeln schließen, warnen vor Datenverlust (Speichern, Verwerfen, Abbrechen).

### 5.4 Arbeitsfläche
Schachbrettmuster für Transparenz, Zoom (Mausrad, Zoom um den Mauszeiger), Schwenken, Anpassen an das Fenster, Pixelraster bei hohem Zoom, Lineale, Hilfslinien, Raster, Auswahlkonturen, Transformationsgriffe, Werkzeugvorschauen. Ein Zoomwechsel verändert niemals Pixel.

Koordinatentransformationen zwischen Dokument-, Ebenen-, Viewport- und Bildschirmkoordinaten werden zentral definiert und getestet.

### 5.5 Panels
Mindestens: Werkzeugpalette, Werkzeugoptionen, Ebenen, Farben, Historie, Navigator, Dokumenteigenschaften, Effektbrowser, Plugin-Verwaltung, Diagnose. Ein-/ausblendbar, in der Größe anpassbar, andockbar, in Arbeitsbereichen speicherbar. Eine funktionierende Standardanordnung und ein Reset-Mechanismus sind Pflicht.

### 5.6 Lokalisierung und Bedienung
- Alle sichtbaren Texte liegen in zentralen Ressourcendateien (Deutsch). Keine festen Texte im Code.
- Tastaturbedienung, sichtbare Fokuszustände, sinnvolle Tab-Reihenfolge, Tooltips, barrierefreie Beschriftungen.
- Architektur so anlegen, dass weitere Sprachen später ergänzt werden können.

---

## 6. Bild-Engine und Rendering

- Ebenenkomposition, Alpha-Blending, Auswahlmasken, Dokument- und Ansichtstransformationen.
- Inkrementelle Aktualisierung nur des geänderten Bereichs (Dirty-Rectangles).
- Cache-Invalidierung bei Änderungen.
- Rechenintensive Operationen laufen als abbrechbare Hintergrundjobs (`CancellationToken`), nie im UI-Thread.
- Vorschauen werden auf reduzierten Bildflächen berechnet, solange die Endauflösung nicht gefordert ist.
- Speicher: begrenzte Caches, gezielte Freigabe, Schutz vor übergroßen Bildern, verständliche Meldung bei Speichermangel.
- Ein CPU-Renderpfad ist Pflicht. Ein GPU-Pfad ist optional und erst nach bestandenen Rendering-Referenztests zulässig, mit Rückfall auf CPU.

---

## 7. Werkzeugsystem

Einheitliche Tool-API `ITool` mit: ID, Name, Kategorie, Icon, Tooltip, Tastenkürzel, Optionen, Eingabeverarbeitung (Maus, Tastatur), Vorschau, Abbruch, Bestätigung, Undo-Unterstützung.

Werkzeuge mit echter Funktion (Reihenfolge = Priorität):

1. **Zeichnen:** Pinsel, Bleistift, Radierer, Linie, Rechteck, gefülltes Rechteck, Ellipse, gefüllte Ellipse, Freihand. Optionen: Größe, Härte (sofern umgesetzt), Deckkraft, Vorder- und Hintergrundfarbe, Linienbreite, Füllmodus.
2. **Strichmuster-Werkzeug** (Abschnitt 1.1).
3. **Auswahl:** Rechteck, Ellipse, Freihand, Polygon, Zauberstab, Farbbereich. Aufheben, Umkehren, Erweitern, Verkleinern. Auswahlmasken mit klar definierten Grenzregeln.
4. **Füllung und Farbe:** Pipette, Farbeimer (mit definierter Toleranz, Füllbereich und Transparenzbehandlung), Verlauf, Farbfelder.
5. **Transformation:** Verschieben, Zuschneiden, Skalieren, Drehen, Spiegeln. Vorschau während der Bearbeitung. Abbruch verändert den Originalzustand nicht.
6. **Erweitert:** Text (Basis), Klonen, Retusche, Handwerkzeug, Zoom. Erst robuste Kernfunktion, dann Erweiterung. Teilfunktionen transparent kennzeichnen.

---

## 8. Ebenen

Neue Ebene, Duplizieren, Löschen, Umbenennen, Reihenfolge, Gruppen, Sichtbarkeit, Deckkraft, Mischmodi (mindestens Normal, Multiplizieren, Negativ, Addieren, Bildschirm), Ebenensperren, Masken, Zusammenführen, Auswahl aus Ebenentransparenz, Transformation einzelner Ebenen.

Operationen auf eine Ebene verändern keine Pixel anderer Ebenen. Mischmodi sind durch Referenztests mit exakten Werten abgesichert.

---

## 9. Effekte

Registrierbares Effektmodell. Jeder Effekt definiert: ID, Name, Kategorie, Beschreibung, Parameter, Standardwerte, Wertebereiche, Validierung, Berechnung, Vorschau, Abbruchverhalten, Fehlerbehandlung.

Mindestumfang:
1. Gaußscher Weichzeichner
2. einfacher Weichzeichner
3. Schärfen
4. Helligkeit
5. Kontrast
6. Farbton
7. Sättigung
8. Invertieren
9. Schwarzweiß
10. Sepia
11. Kantenerkennung
12. Rauschen
13. Rauschreduktion
14. Farbkanalanpassung

Effektdialoge mit Schiebereglern, numerischen Eingaben und Live-Vorschau. Nicht destruktive Effekte sind zu bevorzugen, sofern der Aufwand verhältnismäßig ist.

---

## 10. Undo, Redo und Historie

- Dokumentbezogen, mit Operationsnamen und gruppierten Bearbeitungen.
- Kombination aus Command-Pattern, Deltas und gezielten Snapshots. Keine vollständigen Ebenenkopien nach jeder kleinen Aktion.
- Nach Undo entsteht ein korrekter Redo-Zweig, bei neuer Aktion wird er verworfen.
- Historienlimit konfigurierbar, Speicherverbrauch begrenzt.
- Jede bestätigte Bearbeitung ist zuverlässig rückgängig zu machen und wiederherzustellen.

---

## 11. Dateien, Projektformat, Plugins, Konfiguration

### 11.1 Bildformate
- PNG, JPEG, BMP: öffnen und speichern/exportieren (Pflicht).
- TIFF, GIF (statisch): soweit über die gewählte Bibliothek zuverlässig.
- Modularer Importer-/Exporter-Vertrag: Format, Fähigkeiten, Fehlerzustände, Parameter sind definiert.
- PNG-Export ist **nicht** dasselbe wie das Speichern eines mehrlagigen Projekts. Beim PNG-Export wird eine Warnung über das Zusammenführen der Ebenen angezeigt.

### 11.2 Natives Projektformat `.orbit`
- Versioniertes, dokumentiertes Containerformat (z. B. ZIP mit `manifest.json` und Pixeldaten je Ebene, PNG-kodiert oder roh).
- Speichert: Abmessungen, Ebenen und Gruppen, Pixel, Namen, Sichtbarkeit, Deckkraft, Mischmodi, Masken, nicht destruktive Effekte, Metadaten, Formatversion.
- Vor dem vollständigen Laden validieren. Beschädigte, inkompatible oder übergroße Dateien kontrolliert ablehnen.
- Migrationspfade für künftige Formatversionen.
- Sichere Dateiersetzung: erst in temporäre Datei schreiben, dann atomar ersetzen. Unterbrochene Speichervorgänge dürfen keine Datei zerstören.

### 11.3 Speicherverhalten
Speichern, Speichern unter, Kopie speichern, Exportieren, Zuletzt verwendet, Drag-and-drop, Warnung bei ungespeicherten Änderungen.

### 11.4 Plugin-System
- **Vertrag** (`OrbitStudio.Plugin.Abstractions`): Plugin-ID, Anzeigename, Version, Hersteller, API-Version, benötigte Berechtigungen, Einstiegspunkt, registrierte Erweiterungen, Konfigurationsschema.
- **Registrierbar:** Werkzeuge, Effekte, Panels, Importer, Exporter, Menübefehle, Themes, Dokumentaktionen.
- **Laden:** nur aus einem festgelegten Plugin-Ordner im Benutzerprofil und dem Programmordner. Keine stillen Ladevorgänge aus beliebigen Pfaden.
- **Verwaltung:** auflisten, Metadaten, aktivieren, deaktivieren, Fehler anzeigen, Kompatibilität prüfen.
- **Fehlerisolierung:** Ein fehlerhaftes Plugin darf den Start nicht verhindern. Es wird deaktiviert und gemeldet.
- **Sicherheitsgrenze (verbindlich dokumentieren):** Plugins laufen im selben Prozess und sind damit **nicht sandboxed**. Eine Sicherheits-Sandbox darf nicht behauptet werden. Vertrauensmodell in `plugin-development.de.md` festhalten.
- Beispielplugin `OrbitStudio.SamplePlugin` mit einem echten, registrierten Effekt. Weg von Erstellung über Build und Installation bis zur Nutzung dokumentieren und tatsächlich durchführen.

### 11.5 Konfiguration (versioniert, validiert, migrierbar)
Getrennt gespeichert:
- **Global:** Allgemeine Einstellungen, Sprache, zuletzt verwendete Dateien
- **Workspace:** Layouts, Panelgrößen, Symbolleisten, Fensterposition, Zoompräferenzen, benannte Arbeitsbereiche (speichern, laden, umbenennen, löschen, Standardlayout)
- **Theme:** versionierte Theme-Dateien, Import, Export, Wechsel, Reset
- **Plugin:** Plugin-Konfiguration je Plugin
- **Dokumentzustände:** nicht in den Einstellungen, sondern in den Dokumenten

Fehlerhafte Konfiguration fällt auf Standardwerte zurück und meldet dies. Ein fehlerhaftes Theme darf die Anwendung nicht unbenutzbar machen: Validierung vor Aktivierung, Fallback auf Standard-Theme.

### 11.6 Anpassbarkeit
- **Themes:** helles, dunkles und System-Theme, Akzentfarben, Fenster-, Panel- und Textfarben, Rahmen, Hover-, Fokus- und Auswahlfarben, Icon-Varianten, Abstände, UI-Größen. Alles über Design-Tokens.
- **Menüs und Symbolleisten:** Befehle ein- und ausblenden, neu anordnen, Gruppen, Beschriftungen, Icon-Größe. Geschützte Kernaktionen bleiben erreichbar.
- **Tastenkürzel-Editor:** vollständige Befehlsliste, Suche, Neubelegung, Konflikterkennung und -auflösung, Zurücksetzen, Import, Export. Keine Shortcuts direkt in einzelnen Controls.

---

## 12. Umsetzung in Phasen (verbindlich in dieser Reihenfolge)

Das Gesamtziel ist sehr groß. Es wird **in Meilensteinen** umgesetzt, und jeder Meilenstein muss für sich lauffähig und getestet sein. Eine vollständig funktionierende Kernfunktion ist wichtiger als viele halbfertige Funktionen.

**M0 – Bestandsaufnahme**
Repository prüfen, installierte .NET-SDKs ermitteln, Build-Werkzeuge prüfen, Architektur, Risiken und Meilensteine dokumentieren. Vorhandenen Code analysieren, bevor er verändert wird.

**M1 – Grundgerüst**
Solution, Projekte, Startfenster, Menüstruktur, Dokument-Tabs, Arbeitsfläche, Basis-Theme, Settings-Dienst, Logging, erste Tests, `build.ps1` und `test.ps1`. Build und Start werden tatsächlich ausgeführt.

**M2 – Dokumentmodell, Import, Darstellung**
Dokument- und Bildpuffer, Import (PNG, JPEG, BMP), Zoom, Schwenken, Koordinatentransformationen, Speichern und Exportieren. Pixelresultate mit Tests verifiziert.

**M3 – Ebenen und Komposition**
Ebenenmodell, Sichtbarkeit, Deckkraft, Mischmodi mit Referenztests, Reihenfolge, Gruppen, Masken (Basis).

**M4 – Werkzeuge, Auswahl, Historie**
Zeichenwerkzeuge, Auswahlwerkzeuge, Transformation, Undo/Redo. Für jede Werkzeuggruppe mindestens ein Integrationstest.

**M5 – Strichmuster-Werkzeug**
Vollständige Umsetzung nach Abschnitt 1.1 mit Abnahmekriterium und Tests.

**M6 – Effekte**
Effektsystem und die 14 Effekte aus Abschnitt 9, mit Referenzbildern verifiziert. Live-Vorschau.

**M7 – Projektformat**
`.orbit`-Format, Validierung, Versionierung, Migrationsgrundlage, Speichern und Öffnen, sichere Dateiersetzung. Vollständiger Pflicht-Workflow aus Abschnitt 13 besteht.

**M8 – Professionelle Oberfläche**
Vollständige Menüs, Symbolleisten, Panels (Ebenen, Farben, Historie, Navigator, Eigenschaften, Werkzeugoptionen), Dialoge, Statusleiste, Layoutspeicherung.

**M9 – Anpassbarkeit**
Theme-System mit Editor, Layoutmanager, Menü- und Symbolleistenkonfiguration, Tastenkürzel-Editor, zentrale Einstellungen, Import/Export von Konfigurationen.

**M10 – Plugin-API**
Vertrag, Laden, Registrierung, Fehlerisolierung, Verwaltungsdialog, Beispielplugin, Entwicklerdokumentation.

**M11 – Stabilisierung**
Alle Tests, Performancemessungen (Abschnitt 14), Fehlerbehebung, Datenintegrität prüfen.

**M12 – Release**
Release-Build, portable Version, Installer (falls mit verfügbaren Werkzeugen möglich, sonst dokumentiert), Benutzerhandbuch, Entwicklerdoku, Plugin-Doku, Lizenzübersicht, Release-Checkliste. Die fertige EXE wird tatsächlich gestartet und geprüft.

Nach jedem Meilenstein eine kurze Statusmeldung in `docs/implementation-roadmap.de.md` mit erfüllten und offenen Punkten.

---

## 13. Pflicht-Integrationsworkflow

Dieser Ablauf muss als automatisierter Test bestehen:

1. Dokument erstellen
2. Ebene hinzufügen
3. Auf der Ebene zeichnen
4. Auswahl erstellen
5. Effekt anwenden
6. Effekt rückgängig machen
7. Effekt wiederholen
8. Ebene umbenennen
9. Projekt als `.orbit` speichern
10. Projekt schließen
11. Projekt erneut öffnen
12. Pixel, Ebenen und Dokumentzustand prüfen
13. PNG exportieren
14. Exportiertes PNG wieder einlesen und Pixel verifizieren

---

## 14. Tests, Performance und Sicherheit

### 14.1 Teststufen
- **Unit-Tests:** Pixeloperationen, Farbkonvertierung, Alphablending, Mischmodi, Transformationen, Auswahlmasken, Effekte, Dokumentzustände, Undo/Redo, Konfigurationsvalidierung, Plugin-Registrierung, Segmentberechnung des Strichmusters.
- **Rendering-Referenztests:** kleine, exakt definierte Testbilder mit bekannten Pixelwerten. Zulässige Rundungstoleranzen dokumentieren.
- **Integrationstests:** Pflicht-Workflow aus Abschnitt 13, Ebenen, Compositing, Historie.
- **Datei- und Projekttests:** beschädigte, manipulierte und übergroße Dateien, Migration, sichere Speicherung.
- **UI-Tests:** Programmstart, Dokument öffnen, Werkzeugwahl, Dialoge, Speichern, Layout wiederherstellen, Theme-Wechsel, Shortcut-Konfiguration. Automatisiert, soweit zuverlässig möglich, sonst manuell mit Protokoll.
- **Regressionstests:** Jeder behobene Fehler erhält, sofern sinnvoll, einen Test.

### 14.2 Performance
- Erst Ziele definieren, dann messen, dann optimieren.
- Testbilder unterschiedlicher Größe (z. B. 1 MP, 12 MP, 50 MP) und Ebenenzahl (1, 10, 50).
- Gemessen werden: Import, Darstellung, Ebenenkomposition, typische Filter, Undo, Redo, Speichern, Öffnen von `.orbit`, Speicherverbrauch.
- Ergebnisse mit Hardware, Bildgröße, Ebenenzahl und Messmethode dokumentieren.
- **Keine erfundenen Messwerte.** Ohne Messung steht dort „nicht gemessen“.

### 14.3 Sicherheit und Fehlerbehandlung
- Beschädigte Bilder, manipulierte Projektdateien, ungültige Dimensionen, Speichermangel, fehlende Ressourcen, fehlerhafte Plugins, ungültige Konfigurationen, unterbrochene Speichervorgänge, fehlgeschlagene Jobs: alle kontrolliert behandeln.
- Keine stillen Datenverluste.
- Verständliche deutsche Fehlermeldungen.
- Logs enthalten keine Bildinhalte und keine privaten Dateiinhalte.
- Keine Netzwerkzugriffe ohne ausdrückliche Notwendigkeit. Keine Telemetrie.

---

## 15. Build, Auslieferung, EXE

### 15.1 Build-Skripte (PowerShell)
- `build/build.ps1`: Restore, Build (Release und Debug)
- `build/test.ps1`: alle Testprojekte, Ergebnisse als Datei unter `artifacts/testresults/`
- `build/publish.ps1`: Release-Veröffentlichung
- `build/clean.ps1`: Build-Ausgaben entfernen

### 15.2 Publish als Single-File-EXE

Projekteinstellungen im App-Projekt (`OrbitStudio.App.csproj`), Werte an die gewählte .NET-Version anpassen:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <OutputType>WinExe</OutputType>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <PublishReadyToRun>true</PublishReadyToRun>
  <AssemblyName>OrbitStudio</AssemblyName>
  <ApplicationIcon>Resources/Icons/app.ico</ApplicationIcon>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

Befehl:
```
dotnet publish src/OrbitStudio.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish/
```

Ergebnis: `artifacts/publish/OrbitStudio.exe`. Diese Datei muss ohne Installation und ohne separate .NET-Laufzeit auf Windows 10/11 x64 starten. Die Startprüfung wird in `docs/build-and-release.de.md` mit Datum und tatsächlichem Ergebnis dokumentiert.

### 15.3 Portable und installierbare Version
- Portable Version: die Single-File-EXE plus Lizenzdatei und Handbuch in einem ZIP.
- Installer: nur, wenn ein Werkzeug wie Inno Setup oder WiX verfügbar ist. Voraussetzungen dokumentieren. Signierung ist optional und wird dokumentiert, falls kein Zertifikat vorliegt.

### 15.4 Umgebungshinweis
WPF lässt sich nur unter **Windows** vollständig bauen und testen. Arbeitet Claude Code in einer Linux-Umgebung, wird der plattformunabhängige Core (Domain, Imaging, Rendering, Tools, Effects, History, Storage, Tests der Logik) dort entwickelt. Alle WPF-Teile, der Build der EXE und die UI-Tests werden als „ausstehend unter Windows“ markiert, bis sie tatsächlich ausgeführt wurden.

---

## 16. Dokumentation (deutsch, fortlaufend)

- `docs/architecture.md`: Schichten, Module, Schnittstellen, Datenfluss, Pixelrepräsentation, Abhängigkeitsregeln.
- `docs/implementation-roadmap.de.md`: Meilensteine mit Status.
- `docs/user-guide.de.md`: Bedienung, Tastenkürzel, Werkzeuge, Projektformat.
- `docs/developer-guide.de.md`: Aufbau, Build, Tests, Konventionen.
- `docs/plugin-development.de.md`: Plugin-Vertrag, Beispiel-Schritt-für-Schritt, Vertrauensmodell.
- `docs/configuration.de.md`: Konfigurationsdateien, Themes, Shortcuts, Layouts.
- `docs/build-and-release.de.md`: Build, Publish, Release-Checkliste.
- `docs/testing.de.md`: Teststrategie, Ergebnisse mit Datum, Abnahme des Strichmuster-Werkzeugs.
- `docs/dependencies-and-licenses.de.md`: alle Abhängigkeiten, Lizenzen, Zweck.
- `docs/bekannte-einschraenkungen.md`: offene und nicht umgesetzte Teile.
- `docs/entscheidungen.md`: jede wichtige Entscheidung mit Alternativen und Begründung.

Dokumentation wird bei jeder relevanten Codeänderung aktualisiert, nicht erst am Ende.

---

## 17. Definition of Done

Das Projekt gilt erst als fertig, wenn alle Punkte belegt sind:

- [ ] Die Anwendung startet als eigenständige Windows-EXE.
- [ ] Die Hauptoberfläche funktioniert bei verschiedenen Fenstergrößen und DPI-Einstellungen.
- [ ] Bilder lassen sich öffnen und exportieren.
- [ ] Mehrere Dokumente sind unabhängig bearbeitbar.
- [ ] Die Grundwerkzeuge verändern nachweislich Pixeldaten.
- [ ] Ebenen und Mischmodi sind durch Referenztests bestätigt.
- [ ] Effekte liefern geprüfte Pixelresultate.
- [ ] Undo und Redo funktionieren für alle bestätigten Operationen.
- [ ] `.orbit`-Projekte lassen sich speichern, schließen und erneut öffnen (Pflicht-Workflow aus Abschnitt 13 besteht).
- [ ] Strichmuster-Werkzeug erfüllt das Abnahmekriterium aus Abschnitt 1.1.
- [ ] Themes lassen sich wechseln, importieren, exportieren und zurücksetzen.
- [ ] Layouts lassen sich speichern und wiederherstellen.
- [ ] Menüs und Symbolleisten sind anpassbar.
- [ ] Tastenkürzel lassen sich neu belegen, Konflikte werden erkannt.
- [ ] Das Beispielplugin wurde gebaut, installiert und im Programm verwendet.
- [ ] Fehlerhafte Dateien und Konfigurationen werden kontrolliert behandelt.
- [ ] Die automatisierten Tests bestehen, Ergebnisse sind dokumentiert.
- [ ] Build- und Publish-Prozess sind reproduzierbar dokumentiert.
- [ ] Die deutsche Benutzer- und Entwicklerdokumentation ist vorhanden.
- [ ] Der Release-Build wurde tatsächlich gestartet und geprüft.
- [ ] Alle Einschränkungen stehen in `bekannte-einschraenkungen.md`.

---

## 18. Verbote

- Keine Abhängigkeit von Paint.NET, seinem Plugin-System oder anderen Bildeditoren.
- Keine Icons, Schriften, Bibliotheken oder Codeteile ohne geklärte Lizenz.
- Keine Behauptung einer Sandbox für Plugins.
- Keine erfundenen Testergebnisse, Messwerte oder Erfolgsmeldungen.
- Keine versteckten Platzhalter oder stillen Auslassungen.
- Keine Dateien ohne vorheriges Lesen überschreiben.
- Keine destruktiven Operationen ohne nachvollziehbares Verhalten und Datensicherung.

---

## 19. Erster Arbeitsauftrag

1. Repository und Umgebung prüfen (M0).
2. Dokumente `docs/architecture.md`, `docs/implementation-roadmap.de.md`, `docs/testing.de.md`, `docs/dependencies-and-licenses.de.md` anlegen.
3. Solution und Grundgerüst (M1) erstellen, `build/build.ps1` und `build/test.ps1` schreiben.
4. Build und Tests tatsächlich ausführen, Ergebnis dokumentieren.
5. Abschlussbericht mit: erstellten Dateien, Architektur, tatsächlichem Build-Status, tatsächlichen Testergebnissen, Blockaden, nächstem Schritt (M2).

Danach eigenständig mit M2 fortfahren, solange keine zwingende Entscheidung des Nutzers nötig ist.
