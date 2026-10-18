# Entscheidungen

## E-001 – .NET 10 SDK 10.0.402, `global.json` mit `rollForward: latestFeature` (18.10.2026)
- Kontext: Spezifikation fordert die aktuelle LTS-Version. Auf dem Rechner war kein SDK installiert.
- Entscheidung: .NET SDK 10.0.402 per winget installiert. `global.json` verlangt mindestens 10.0.400
  und erlaubt neuere Feature-Bänder.
- Alternative: Version fest pinnen – verworfen, da Sicherheitsupdates sonst manuell nötig wären.

## E-002 – Zentrale Paketverwaltung und gemeinsame Build-Einstellungen (18.10.2026)
- `Directory.Packages.props` (Versionen) und `Directory.Build.props` (Nullable, TreatWarningsAsErrors,
  Sprache, Version) gelten für alle Projekte. Verhindert Versionsdrift zwischen Projekten.

## E-003 – Projekte erst anlegen, wenn sie Code erhalten (18.10.2026)
- Kontext: Spezifikation listet 17 Quellprojekte und verlangt gleichzeitig „keine leeren Projekte“.
- Entscheidung: M1 enthält Domain, Configuration, Diagnostics, Application, Presentation, App.
  Weitere Projekte werden im jeweiligen Meilenstein angelegt und in den Architekturtest aufgenommen.
- Zusätzliche Testprojekte gegenüber der Liste: `Configuration.Tests`, `Application.Tests`,
  `Architecture.Tests` – je Kernprojekt ein Testprojekt, Architekturregeln getrennt.

## E-004 – Pixel: BGRA 8 Bit, premultiplied Alpha (18.10.2026)
- Alternativen: straight Alpha (einfachere Farbbearbeitung, aber falsche Interpolation beim
  Skalieren/Weichzeichnen ohne Umrechnung).
- Entscheidung: premultiplied, weil SkiaSharp und WPF `Pbgra32` nativ damit arbeiten und
  Komposition/Filter ohne Zusatzschritte korrekt sind. Farbanpassungen rechnen intern um.

## E-005 – Sprachressourcen als XAML-ResourceDictionary statt .resx (18.10.2026)
- Kontext: Die `.resx`-Codegenerierung (`PublicResXFileCodeGenerator`) läuft nur in Visual Studio,
  nicht zuverlässig mit `dotnet build`.
- Entscheidung: `Strings.de.xaml` mit `sys:String`-Einträgen; Zugriff in XAML per
  `StaticResource`, in Code per `Loc`. Weitere Sprachen über zusätzliche Dateien gleicher Schlüssel.
- Nachteil: kein Compile-Zeit-Check der Schlüssel; fehlende Schlüssel erscheinen sichtbar als `[Schlüssel]`.

## E-006 – Befehle späterer Meilensteine sichtbar, aber deaktiviert (18.10.2026)
- Kontext: Spezifikation verlangt die vollständige Menüstruktur, verbietet aber Befehle ohne Funktion.
- Entscheidung: Geplante Befehle stehen im Menü, sind deaktiviert und nennen per Tooltip den
  Meilenstein. Sie führen nie etwas aus und stehen in `bekannte-einschraenkungen.md`.
- Alternative: Menüeinträge erst später einfügen – verworfen, da Menüaufbau und Shortcut-Konflikte
  so schon jetzt vollständig geprüft werden.

## E-007 – Eigener Datei-Logger statt Fremdbibliothek (18.10.2026)
- `Microsoft.Extensions.Logging` hat keinen Datei-Sink. Statt Serilog o. Ä. ein kleiner eigener
  `FileLoggerProvider` (Tagesdateien, 14 Tage Aufbewahrung). Weniger Abhängigkeiten, volle Kontrolle
  darüber, dass keine Bildinhalte geloggt werden.

## E-008 – Eingebauter UI-Smoke-Test statt UI-Automatisierungsframework (18.10.2026)
- Alternativen: FlaUI/WinAppDriver (zusätzliche Abhängigkeit, fragil in CI).
- Entscheidung: `--smoke-test` startet das echte Fenster, führt Befehle über die Registry aus und
  prüft den visuellen Baum. Isolierte Einstellungen in `%TEMP%`, Ergebnis per Exitcode und Datei.
  Klick-Interaktionen (z. B. Ziehen) werden bei Bedarf manuell protokolliert.

## E-009 – Basis-Theme hell mit dunkler Arbeitsfläche (18.10.2026)
- Die Standard-WPF-Steuerelemente (Menü, Tabs) sind hell; ein vollständig dunkles Theme erfordert
  eigene Control-Templates (M9). Bis dahin helle Oberfläche, dunkle Arbeitsfläche für neutralen
  Bildkontrast. Alle Farben bereits als Tokens.

## E-011 – SkiaSharp für Codecs, Anzeige über WriteableBitmap (18.10.2026)
- SkiaSharp 4.153.1 (MIT, aktiv gepflegt, .NET 10, Windows-Native-Assets) dekodiert PNG/JPEG/BMP/WebP/GIF und
  kodiert PNG/JPEG/WebP. Für die Anzeige wird kein `SkiaSharp.Views.WPF` gebraucht: Das interne Format ist
  identisch mit WPF `Pbgra32`, eine `WriteableBitmap` genügt (weniger Abhängigkeiten, keine Umrechnung).

## E-012 – Eigener BMP-Encoder (18.10.2026)
- Skia kann BMP lesen, aber nicht schreiben. Eigener Encoder: 24 Bit (BITMAPINFOHEADER) für deckende Bilder
  wegen größter Kompatibilität, 32 Bit BGRA mit BITMAPV4HEADER/BI_BITFIELDS bei Transparenz.

## E-013 – Zusätzliche Formate WebP (lesen/schreiben) und GIF (nur lesen) (18.10.2026)
- Ohne Mehraufwand über SkiaSharp verfügbar und durch Tests abgedeckt. GIF: nur das erste Einzelbild.
  TIFF wird nicht angeboten, da SkiaSharp es nicht unterstützt (Spezifikation: „soweit zuverlässig“).

## E-014 – „Öffnen“ = neues Dokument, „Importieren“ = Ebene (18.10.2026)
- Öffnen legt je Bilddatei ein eigenes Dokument an. Importieren fügt künftig ein Bild als Ebene in das
  aktive Dokument ein und folgt daher mit dem Ebenenmodell (M3). Eine bereits geöffnete Datei wird nur aktiviert.

## E-015 – Mausrad zoomt, mittlere Maustaste/Leertaste verschiebt (18.10.2026)
- Spezifikation verlangt „Zoom (Mausrad, Zoom um den Mauszeiger)“. Deshalb zoomt das Mausrad ohne
  Modifizierer; Verschieben mit mittlerer Maustaste oder Leertaste + linker Maustaste sowie über die
  Scrollleisten. Das Handwerkzeug folgt in M4.

## E-016 – 100 % = 1 Bildpixel pro Gerätepixel (18.10.2026)
- Alternativen: 1 Bildpixel pro DIP (bei 150 % Skalierung unscharf/vergrößert).
- Entscheidung: Zoom bezieht sich auf physische Pixel; die `ViewTransform` rechnet mit der aktuellen
  DPI-Skalierung um. So ist „100 %“ auf jedem Monitor pixelgenau.

## E-017 – Export-Qualität aus den Einstellungen, noch ohne Dialog (18.10.2026)
- JPEG/WebP-Qualität kommt aus `GlobalSettings.JpegQuality` (Standard 92). Ein Optionsdialog beim Export
  bzw. in den Einstellungen folgt mit dem Einstellungsdialog (M9).

## E-018 – Startparameter `--data-dir` (18.10.2026)
- Einstellungen und Logs in einem frei wählbaren Ordner. Nötig für den Eingabetest (keine Veränderung
  der echten Nutzereinstellungen) und Grundlage der portablen Version (M12).

## E-019 – Eingabetest nur mit Vordergrundprüfung (18.10.2026)
- Ein erster Lauf hat Tastenkürzel an ein anderes Fenster gesendet, weil ORBIT Studio nicht im
  Vordergrund war. Seitdem prüft der Test vor jeder Eingabe das Vordergrundfenster und bricht sonst ab.
  Er ist optional (`-WithInput`), da er eine unbenutzte Sitzung braucht.

## E-020 – „Negativ“ = Negation (18.10.2026)
- Die Spezifikation nennt „Negativ“ neben „Bildschirm“. Da „Negativ multiplizieren“ (Photoshop-Begriff für
  Bildschirm) ausscheidet, wird „Negativ“ als Negation umgesetzt: B = 1 − |1 − Cb − Cs|. Zusätzlich gibt es
  Differenz, Abdunkeln, Aufhellen und Überlagern (gleicher Aufwand, gleiche Referenztests).

## E-021 – Ebenen in Dokumentgröße (18.10.2026)
- Alternativen: Ebenen mit eigener Größe/Position (spart Speicher bei kleinen Inhalten, macht aber jede
  Operation komplexer). Entscheidung: alle Pixelebenen haben die Dokumentgröße; „Position“ einer Ebene ist ihr
  Platz im Stapel. Verschieben von Inhalten erfolgt in M4 als Pixeloperation. Speicherbedarf: 4 Byte/Pixel je Ebene.

## E-022 – Isolierte Gruppen, W3C-Formeln, Rechnen in double (18.10.2026)
- Gruppen werden isoliert zusammengesetzt (wie in gängigen Editoren, kein „Durchreichen“). Die Komposition folgt
  W3C Compositing Level 1 und rechnet in `double`; so sind Referenzwerte exakt vorhersagbar und Zusammenführen
  ergibt byteweise dasselbe wie die Anzeige. Optimierungen (Ganzzahl/SIMD/GPU) erst nach Messungen in M11 und nur,
  wenn die Referenztests weiter bestehen.

## E-023 – Fest angeordnetes Ebenen-Panel schon in M3 (18.10.2026)
- Ohne Panel ließe sich die aktive Ebene nicht sinnvoll wählen. Ein einfaches Panel rechts (Liste, Sichtbarkeit,
  Sperre, Deckkraft, Mischmodus, Schaltflächen) ist daher Teil von M3. Andocken, Vorschaubilder und Ziehen
  zum Umsortieren folgen mit dem Panel-System in M8.

## E-024 – Rückfragen vor nicht umkehrbaren Ebenenänderungen (18.10.2026, durch E-028 abgelöst)
- Bis Rückgängig/Wiederholen (M4) existiert, fragen Löschen, Zusammenführen, Reduzieren und Maske anwenden nach.
  Die Rückfragen werden in M4 überprüft und entfallen, wo Rückgängig sie überflüssig macht.

## E-025 – Geskripteter Dialogdienst im Smoke-Test (18.10.2026)
- Damit der Smoke-Test Befehle mit Rückfragen und Dateidialogen über die echte Befehlskette ausführen kann,
  ersetzt `SmokeDialogService` im Testmodus den WPF-Dialogdienst: vorbereitete Antworten, automatisch bestätigte
  Rückfragen, Protokoll aller Meldungen. Der normale Programmstart verwendet ausschließlich echte Dialoge.

## E-026 – Historie: Deltas für Pixel, Referenzen für Struktur und Größe (18.10.2026)
- Spezifikation 10 verlangt Command-Pattern, Deltas und gezielte Snapshots statt Ebenenkopien nach jeder Aktion.
- Malen/Verschieben speichern nur das geänderte Rechteck (vorher/nachher). Ebenenoperationen speichern Struktur und
  Eigenschaften (Ebenen per Referenz) und kopieren nur Pixel, die die Operation verändert. Bildtransformationen
  erzeugen neue Puffer; die alten werden nur referenziert. Grenzen: 100 Schritte / 1024 MB je Dokument.

## E-027 – „Sicherung + Abdeckung“ statt direktem Stempeln (18.10.2026)
- Alternativen: Pinselabdrücke direkt in die Ebene stempeln (Deckkraft addiert sich bei Überlappung, Vorschauen
  hinterlassen Spuren). Entscheidung: Abdeckungspuffer mit Maximum und Neuberechnung aus der Sicherung. Kostet je
  Zug eine Kopie der Ebene und einen float-Puffer (8 Byte/Pixel), liefert aber exakte Ergebnisse, saubere Vorschauen
  und exaktes Abbrechen. Optimierung (Kacheln) in M11 nach Messung.

## E-028 – Keine Rückfragen mehr bei Ebenenoperationen (18.10.2026)
- Mit Rückgängig sind Löschen, Zusammenführen, Reduzieren und Maske anwenden umkehrbar; die Rückfragen aus E-024
  entfallen. Rückfragen bleiben dort, wo Daten das Programm verlassen (Export) oder verloren gehen (Schließen).

## E-029 – Farben und Werkzeugoptionen global (18.10.2026)
- Spezifikation 4 trennt Dokumentzustand (Ebenen, Auswahl, Historie, Ansicht) und globale Einstellungen; Farben werden
  dort nicht eindeutig zugeordnet. Wie in gängigen Editoren gelten Vorder-/Hintergrundfarbe, Werkzeugoptionen und
  „Maske bearbeiten“ für alle Dokumente. Pixel, Ebenen, Auswahl und Historie werden nie zwischen Tabs geteilt (Tests).

## E-030 – Einfache Tastenkürzel über die Registry (18.10.2026)
- WPF-KeyBindings erlauben keine Buchstaben ohne Strg/Alt. Werkzeugkürzel (B, P, E, M, Umschalt+M …) sowie X/D stehen
  trotzdem in der Command-Registry (Konflikterkennung, später anpassbar); das Hauptfenster wertet sie selbst aus und
  ignoriert sie, solange ein Textfeld den Fokus hat.

## E-031 – Zwischenablage zweifach (18.10.2026)
- „PNG“ erhält Transparenz für Bildprogramme, die Standard-Bitmap sorgt für Kompatibilität mit allen anderen.
  Eingefügt wird bevorzugt aus „PNG“. Der Smoke-Test nutzt eine Zwischenablage im Speicher, damit die echte
  Windows-Zwischenablage des Nutzers nie verändert wird.

## E-032 – Verlauf und Malen im vormultiplizierten Raum (18.10.2026)
- Interpolation und Auftrag vormultipliziert, damit transparente Farben keine dunklen Säume erzeugen; dieselbe Regel
  gilt für das Resampling (Bildgröße, Drehen).

## E-010 – Repository-Name ColourCraft, Produktname ORBIT Studio (18.10.2026)
- Das GitHub-Repository heißt `syncyxyc/ColourCraft`. Der Produktname laut Spezifikation ist
  „ORBIT Studio“ und zentral in `AppInfo.cs` austauschbar.
