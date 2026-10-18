# Architektur – ORBIT Studio

Stand: 18.10.2026, Meilenstein M3

## Schichten und Projekte

```
OrbitStudio.App            WPF-Startprojekt: Kompositionswurzel (DI), Theme-Ressourcen, Manifest, Smoke-Test
  └─ OrbitStudio.Presentation   ViewModels, Views, Dialoge, Menüaufbau, Leinwand, Ebenen-Panel, Sprachressourcen
       ├─ OrbitStudio.Layers       Ebenenoperationen (Neu, Duplizieren, Gruppen, Masken, Zusammenführen, Import)
       │    └─ OrbitStudio.Rendering  Ebenenkomposition (CPU, Mischmodi), Ansichtstransformation
       └─ OrbitStudio.Application  Arbeitsbereich, Command-Registry, Einstellungen, Datei-Dienst (Öffnen/Export)
            ├─ OrbitStudio.Imaging       Codec-Vertrag, Codec-Registry, PNG/JPEG/WebP/GIF (SkiaSharp), BMP
            │    └─ OrbitStudio.Domain   Dokument, Pixelpuffer, Farbe, Geometrie (ohne WPF, ohne Fremdbibliotheken)
            └─ OrbitStudio.Configuration AppInfo, Pfade, versionierte Einstellungen, atomares Speichern
  └─ OrbitStudio.Diagnostics    Datei-Logger (Microsoft.Extensions.Logging), keine Telemetrie
```

Weitere Projekte der Spezifikation (Tools, Effects, History, Storage, Themes, Docking, Plugin.*)
werden in dem Meilenstein angelegt, in dem sie Code erhalten ([entscheidungen.md](entscheidungen.md), E-003).

| Projekt | Zielframework | Darf referenzieren |
|---|---|---|
| Domain | net10.0 | – |
| Configuration | net10.0 | – |
| Diagnostics | net10.0 | Microsoft.Extensions.Logging.Abstractions |
| Imaging | net10.0 | Domain, SkiaSharp |
| Rendering | net10.0 | Domain |
| Layers | net10.0 | Domain, Rendering |
| Application | net10.0 | Domain, Configuration, Imaging, Logging.Abstractions |
| Presentation | net10.0-windows | Application, Rendering, Layers, CommunityToolkit.Mvvm |
| App | net10.0-windows | alle, DI, Logging |

## Abhängigkeitsregeln

Verbindlich nach Spezifikation Abschnitt 3.1. Automatisch geprüft durch
`tests/OrbitStudio.Architecture.Tests/DependencyRuleTests.cs`:

- Domain, Configuration, Diagnostics, Imaging, Rendering und Application referenzieren **kein**
  UI-Framework (PresentationFramework, PresentationCore, WindowsBase, System.Xaml, WinForms).
- Jedes Kernprojekt referenziert nur die in der Tabelle erlaubten `OrbitStudio.*`-Assemblies.
- Domain referenziert ausschließlich `System.*`-Assemblies.
- SkiaSharp (Codecs) ist auf `OrbitStudio.Imaging` beschränkt; Domain, Rendering und Application
  referenzieren es nicht.

## Pixelrepräsentation (verbindlich)

- Format: **BGRA, 8 Bit je Kanal**, 4 Byte je Pixel (`Bgra32`).
- Alpha: **vormultipliert** (premultiplied). Invariante: B, G, R ≤ A. Begründung: SkiaSharp und WPF
  (`Pbgra32`) arbeiten nativ damit, die Anzeige braucht keine Umrechnung (siehe E-004).
- Umrechnung (`ColorMath`): vormultiplizieren `c·a/255`, zurückrechnen `c·255/a`, jeweils kaufmännisch
  gerundet; bei a = 0 ist die Farbe 0. Gerade → vormultipliert → gerade ist für deckende Pixel exakt.
- Speicherlayout (`PixelBuffer`): zeilenweise ab oben links, Reihenfolge B, G, R, A, Stride = Breite·4
  ohne Auffüllung. Pixel (x, y) liegt bei Byte `y·Stride + x·4`.
- Koordinaten: Ursprung oben links, x nach rechts, y nach unten, ganzzahlige Pixelindizes;
  Pixel (x, y) überdeckt [x, x+1) × [y, y+1). Rechtecke (`IntRect`): rechte/untere Kante exklusiv.
- Fließkomma-Positionen als `PointD` (double) in Dokumentkoordinaten.
- Grenzen (`DocumentLimits`): Kantenlänge 1…65.535, höchstens 16.384² ≈ 268 MP (1 GiB je Puffer).
  Wird **vor** jeder Speicherreservierung geprüft.

## Ebenenmodell (`OrbitStudio.Domain.Layers`)

- `ImageDocument.Root` ist eine unsichtbare Wurzelgruppe. Kinder sind **von unten nach oben** geordnet
  (Index 0 = unterste Ebene). Das Panel zeigt sie umgekehrt (oberste zuerst, Gruppe über ihren Kindern).
- `LayerNode` (gemeinsam): ID, Name, Sichtbarkeit, Deckkraft 0–1, Mischmodus, Sperre, Elterngruppe.
  Sichtbarkeit und Sperre wirken auf alle Kinder einer Gruppe (`IsEffectivelyVisible/Locked`).
- `PixelLayer`: Pixelpuffer **in Dokumentgröße** und optional `LayerMask` (8 Bit, 255 = sichtbar, abschaltbar).
- `LayerGroup`: Kinderliste; Zyklen und Mehrfach-Eltern werden abgewiesen.
- `ImageDocument.ActiveLayer`: Ziel der Ebenenbefehle (Ebene oder Gruppe).
- Änderungen meldet das Dokument über `LayersChanged` (Struktur/Eigenschaften) und `PixelsChanged(IntRect)`.
- Sperre bedeutet: nicht löschen, nicht verschieben, nicht zusammenführen, keine Maske anwenden, (ab M4) nicht bemalen.
  Sichtbarkeit, Deckkraft und Mischmodus bleiben änderbar.

## Ebenenoperationen (`OrbitStudio.Layers.LayerOperations`)

Neu (über der aktiven Ebene), Importieren als Ebene (oben links, beschnitten), Duplizieren (tiefe Kopie, nie gesperrt),
Löschen (mindestens eine Pixelebene bleibt), Umbenennen, Nach oben/unten (innerhalb der Gruppe), Sichtbarkeit,
Deckkraft, Mischmodus, Sperre, Gruppe erstellen/auflösen, Maske hinzufügen/aktivieren/umkehren/anwenden/löschen,
Mit darunterliegender Ebene zusammenführen, Auf eine Ebene reduzieren. Vorbedingungen werden geprüft;
Ablehnungen als `LayerOperationException(LayerOperationError)` – die Oberfläche zeigt eine deutsche Meldung.
Ab M4 werden die Operationen als rückgängig machbare Historieneinträge gekapselt.

- **Zusammenführen:** Die obere Ebene wird mit Deckkraft, Maske und Mischmodus auf die Pixel der unteren gemischt
  (`LayerCompositor.BlendInto`). Eine Maske der unteren Ebene wird vorher eingerechnet. Ist die untere Ebene deckend
  und „Normal“, bleibt das Aussehen exakt gleich (Test). Bei transparenter unterer Ebene und anderen Mischmodi kann es
  abweichen, weil die obere Ebene im Gesamtbild auch mit tieferen Ebenen gemischt wird.
- **Gruppe auflösen:** Deckkraft und Mischmodus der Gruppe entfallen (Rückfrage, falls nicht neutral).

## Komposition (`OrbitStudio.Rendering.LayerCompositor`)

CPU-Referenzpfad, zeilenweise (große Bereiche parallel über Zeilen), in `double` mit vormultipliertem Alpha nach
W3C „Compositing and Blending Level 1“, Source-over:

```
co = cs·(1 − αb) + cb·(1 − αs) + αs·αb·B(Cb, Cs)       αo = αs + αb − αs·αb
cs = Quelle·Deckkraft·Maske/255 (vormultipliert), Cs = cs/αs, Cb = cb/αb
```

| Mischmodus | B(Cb, Cs) | Name in der Oberfläche |
|---|---|---|
| Normal | Cs | Normal |
| Multiply | Cb·Cs | Multiplizieren |
| Screen | Cb + Cs − Cb·Cs | Bildschirm |
| Add | min(1, Cb + Cs) | Addieren |
| Negation | 1 − \|1 − Cb − Cs\| | Negativ |
| Difference | \|Cb − Cs\| | Differenz |
| Darken | min(Cb, Cs) | Abdunkeln |
| Lighten | max(Cb, Cs) | Aufhellen |
| Overlay | Cb ≤ 0,5: 2·Cs·Cb, sonst 1 − 2·(1 − Cs)·(1 − Cb) | Überlagern |

- Ist der Hintergrund leer (αb = 0), reduziert sich jeder Modus auf Normal.
- Gruppen werden **isoliert** zusammengesetzt (eigener transparenter Puffer) und dann mit Gruppendeckkraft und
  -mischmodus eingefügt.
- Ergebnis: je Kanal ×255, kaufmännisch gerundet, auf 0–255 begrenzt, Farbe ≤ Alpha (Invariante).
- Eine einzelne Ebene „Normal, 100 %“ wird byteweise exakt wiedergegeben (Referenztest).
- Darstellung: `DocumentViewModel` hält das Komposit als Cache; `PixelsChanged(Bereich)` setzt nur diesen Bereich
  neu zusammen und überträgt ihn in die Bitmap; `LayersChanged` setzt das ganze Bild neu zusammen.
- Export setzt das Bild frisch zusammen (sichtbare Ebenen) und warnt vorher, wenn mehr als eine Ebene existiert.

## Koordinatensysteme und Ansicht (`OrbitStudio.Rendering.ViewTransform`)

| System | Einheit | Ursprung |
|---|---|---|
| Dokument | Bildpixel (double) | Bild oben links |
| Viewport | physische Gerätepixel | Arbeitsfläche oben links |
| DIP | WPF-Einheiten = Viewport / DpiScale | Arbeitsfläche oben links |

- `viewport = dokument · Zoom + Offset`. **Zoom 1,0 = 1 Bildpixel pro Gerätepixel**, unabhängig von der
  Windows-Skalierung (bei 150 % ist ein Bildpixel 0,667 DIP groß).
- Zoomstufen: 1 % … 3.200 % (`ZoomPresets`). Mausrad und Vergrößern/Verkleinern springen zur nächsten Stufe;
  der Punkt unter dem Mauszeiger bleibt dabei fest.
- Passt das Bild samt Rand (32 DIP) in den Viewport, wird es zentriert; sonst ist Verschieben auf
  „Bildkante bis Rand“ begrenzt. Scrollleisten werden aus derselben Transformation abgeleitet.
- Erste Ansicht eines Dokuments: ganzes Bild sichtbar, höchstens 100 %.
- Zoom und Verschiebung gehören zum Dokument (`DocumentViewModel.View`) und bleiben beim Tabwechsel erhalten.
- Die Transformation liest keine Pixel; Zoomwechsel verändern nie Bilddaten (Test im Smoke-Test).

## Darstellung (`Presentation.Viewport`)

- `BitmapBridge`: Pixelpuffer → `WriteableBitmap` (Pbgra32, identisches Layout, keine Umrechnung).
  Änderungen kommen über `ImageDocument.PixelsChanged(IntRect)` und werden nur im geänderten Bereich
  übertragen (Dirty-Rectangle).
- `CanvasView` zeichnet Hintergrund, Schachbrett (Transparenz), Bild und – ab 800 % und wenn
  eingeschaltet – das Pixelraster (1 Gerätepixel breite Linien). Ab 100 % wird auf ganze Gerätepixel
  ausgerichtet und mit „nächster Nachbar“ skaliert, darunter hochwertig verkleinert.
- Eingaben: Mausrad = Zoom um den Mauszeiger, mittlere Maustaste oder Leertaste + linke Maustaste =
  Verschieben, Mausbewegung = Pixelkoordinate in der Statusleiste.

## Dateien: Codecs und Datei-Dienst

- Vertrag `IImageCodec` (Imaging): `Format` (Id, Endungen, CanRead, CanWrite, SupportsAlpha,
  SupportsQuality), `Decode(Stream)`, `Encode(PixelBuffer, Stream, EncodeOptions)`. Fehler als
  `ImageCodecException` mit `ImageCodecError` (Corrupt, Unsupported, TooLarge, OutOfMemory, EncodeFailed).
- `ImageCodecRegistry.CreateDefault()`: PNG, JPEG, BMP, WebP (lesen/schreiben), GIF (nur lesen, erstes Bild).
- Lesen (SkiaSharp): Größe aus dem Dateikopf wird geprüft, bevor dekodiert wird; Farbprofile werden
  nach sRGB umgerechnet; EXIF-Ausrichtung (1–8) wird angewendet; unvollständige Daten gelten als
  beschädigt (kein stilles Teilbild). Dateien über 1 GiB werden nicht gelesen.
- Schreiben: PNG/WebP aus dem vormultiplizierten Puffer; JPEG wird vorher exakt auf Weiß reduziert
  (`Kanal + 255 − A`); BMP mit eigenem Encoder (24 Bit ohne, 32 Bit BGRA mit V4-Header bei Transparenz).
- `ImageFileService` (Application): Lesen/Schreiben im Hintergrund (`Task.Run`, `CancellationToken`),
  Export aus einer Kopie des Puffers, Schreiben in `.<name>.<guid>.tmp` im Zielordner und danach
  Ersetzen der Zieldatei – ein Abbruch zerstört nie eine vorhandene Datei. Fehler werden als `FileError`
  zurückgegeben und in der Oberfläche als deutsche Meldung angezeigt.
- „Öffnen“ legt je Bilddatei ein neues Dokument mit einer Ebene an (`SourcePath` = Bilddatei).
  „Importieren“ liest nur die Pixel (`LoadPixelsAsync`) und fügt sie als Ebene ein. „Speichern“ als
  `.orbit`-Projekt folgt in M7.

## Datenfluss (Auszug)

1. `App.OnStartup` erzeugt Logger, DI-Container und lädt die Einstellungen (`SettingsService`).
2. `MainViewModel` registriert alle Befehle in der `CommandRegistry` und baut daraus die Menüs
   (inkl. dynamischem Untermenü „Zuletzt verwendet“).
3. `MainWindow` bindet Tastenkürzel ausschließlich aus der Registry.
4. Öffnen: Dialog/Drag-and-drop/Kommandozeile → `MainViewModel.OpenFilesAsync` → `ImageFileService.OpenAsync`
   (Hintergrund) → `ImageDocument.FromPixels` → `DocumentWorkspace.Add` → Tab.
5. Tabwechsel → `DocumentWorkspace.Activate` → `CommandRegistry.NotifyStateChanged` → Menüs aktualisieren.
6. Beim Beenden: Rückfrage bei ungespeicherten Dokumenten, dann Einstellungen speichern.

## Zustandstrennung

- Pro Dokument (`ImageDocument`, `DocumentViewModel`): Ebenenbaum, aktive Ebene, Name, Speicherzustand,
  Ansicht (Zoom, Verschiebung, Cursor), Komposit-Cache. Ab M4 zusätzlich Auswahl und Historie.
- Global (`SettingsService`): `settings.json` (Sprache, zuletzt verwendet, Vorgaben, JPEG-Qualität) und
  `workspace.json` (Fenster, Pixelraster). Getrennte Dateien, je mit `schemaVersion`.

## Einstellungsdateien

Ort: `%APPDATA%\OrbitStudio\` (oder `<Ordner>\settings` bei `--data-dir <Ordner>`).
Logs: `%LOCALAPPDATA%\OrbitStudio\logs\orbit-JJJJMMTT.log` (14 Tage).
Laden fällt bei Fehlern auf Standardwerte zurück und meldet dies (`Invalid`, `Corrected`, `NewerVersion`).
Beschädigte Dateien werden vorher als `*.defekt-<Zeitstempel>` gesichert. Speichern: erst `*.tmp`, dann ersetzen.

## Lokalisierung

Alle sichtbaren Texte stehen in `src/OrbitStudio.Presentation/Resources/Strings.de.xaml`.
XAML nutzt `{StaticResource Schlüssel}`, Code nutzt `Loc.Get`/`Loc.Format`. Formatnamen: `Format.<Id>`.

## Theme

`src/OrbitStudio.App/Themes/Base.xaml` definiert Design-Tokens (`Color.*`, `Brush.*`, u. a. `Brush.PixelGrid`).
Views verwenden sie nur über `DynamicResource`/`TryFindResource`.
