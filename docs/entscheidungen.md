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

## E-033 – Strichmuster-Vorschau als Raster-Überlagerung (18.10.2026)
- Spezifikation 1.1 verlangt eine Vorschau in einem temporären Overlay-Layer, das Original bleibt unverändert.
  Alternativen: (a) Vorschau direkt in die Ebene mit Sicherung (wie die Formwerkzeuge, E-027) – widerspricht der
  Vorgabe; (b) Vektorvorschau mit WPF-Stiften – sieht anders aus als das Ergebnis (andere Kantenglättung, Enden).
  Entscheidung: `OverlayRaster` mit exakt derselben Abdeckungs- und Rundungsrechnung wie beim Anwenden (Test:
  Vorschau = Ergebnis auf leerer Ebene, byteweise). Begrenzung auf den sichtbaren Ausschnitt und unter 100 % Zoom
  reduzierte Auflösung (Spezifikation 6), damit auch bei großen Bildern höchstens etwa Fenstergröße berechnet wird.
- Folge: Die Vorschau zeigt die Musterfarbe „normal“ über dem Gesamtbild; Deckkraft, Mischmodus und Maske der Ebene
  wirken erst nach dem Anwenden (bekannte-einschraenkungen.md).

## E-034 – Strichmuster: flache Enden, Abstand 0 = ein Segment (18.10.2026)
- Runde oder überstehende Enden würden jeden Strich um die Linienstärke verlängern und die verbindliche Regel
  „10 px Strich, 5 px Lücke“ verletzen. Daher flache Enden: Ein Strich ist exakt Strichlänge lang (Tests mit exakten
  Pixeln). Bei Abstand 0 entsteht ein einziges Segment statt aneinanderstoßender Striche, weil sich an den Nahtstellen
  sonst zwei halb deckende Kanten zu einer sichtbaren Naht addieren könnten.
- Identische Punkte ergeben kein Segment (es gibt keine Richtung); Anwenden ändert dann nichts und legt keinen
  Historieneintrag an.

## E-035 – Strichmuster: eigener Farbparameter, Standard Schwarz (18.10.2026)
- Spezifikation 1.1 legt Farbe inkl. Alpha mit Farbwähler, Hex-Feld und Standard Schwarz fest. Das Werkzeug nutzt
  deshalb eine eigene `ColorOption` statt der Vordergrundfarbe; die rechte Maustaste hat keine eigene Farbe.

## E-036 – Strichmuster: Lebenszyklus der Bearbeitung (18.10.2026)
- Die Bearbeitung bleibt nach dem Loslassen offen (Kontrollpunkte, Felder) und endet nur durch Anwenden oder
  Abbrechen. Wie bei allen Werkzeugen übernimmt `CommitActive` (Werkzeug-/Dokumentwechsel, Rückgängig, Schließen)
  die laufende Linie – keine stillen Datenverluste. Rückgängig direkt danach entfernt sie wieder (und Wiederholen
  stellt sie her). Ein Klick neben Linie und Punkte übernimmt und beginnt eine neue Linie (gängige Bedienung).
- Verlorener Mausfang beendet nur das Ziehen (`ITool.PointerLost`); bei den übrigen Werkzeugen bleibt das bisherige
  Verhalten (übernehmen) als Standard der Basisklasse erhalten.
- Gegriffene Kontrollpunkte behalten ihren Versatz zum Mauszeiger und springen nicht (gefunden durch einen Test).

## E-037 – Strichmuster: Koordinatenbereich und Trefferradius (18.10.2026)
- Punkte dürfen außerhalb des Bildes liegen (Linien, die über den Rand laufen); Grenze ±100.000 px, damit
  Eingaben endlich bleiben und die Segmentzahl beherrschbar ist. Werte werden als `double` in `PointD` gespeichert.
- Fangradius 8 geräteunabhängige Pixel am Bildschirm (× DPI-Skalierung, gemessen in Viewport-Pixeln): Punkte sind bei
  jedem Zoom und jeder Windows-Skalierung gleich gut greifbar. Liegen beide Punkte übereinander, gewinnt der Endpunkt.

## E-038 – Optionsleiste: Schaltflächen, Farbfelder, Anzeigen als Werkzeugoptionen (18.10.2026)
- Statt einer Sonderoberfläche für das Strichmuster wurde die Options-API um `ColorOption`, `ActionOption`,
  `InfoOption` und `IsEnabled` erweitert. Die Optionsleiste bleibt generisch (Plugins ab M10 können dieselben Typen
  nutzen) und bricht bei vielen Optionen um. Eine eigene Werkzeugoptionen-Palette folgt in M8.

## E-039 – Effekte destruktiv mit Rückgängig, nicht als Ebeneneffekte (18.10.2026)
- Spezifikation 9 bevorzugt nicht destruktive Effekte, „sofern der Aufwand verhältnismäßig ist“. Nicht destruktiv
  hieße: Effektstapel je Ebene, Neuberechnung bei jeder Pixeländerung, Cache-Verwaltung und Speicherung im
  `.orbit`-Format (M7). Entscheidung: Effekte verändern die Ebenenpixel als ein Rückgängig-Schritt (Delta nur des
  Zielbereichs). Die Effekt-API trennt Berechnung (`Render`) von Übernahme (`Commit`), sodass ein späterer
  Ebeneneffekt-Stapel dieselben Effekte nutzen kann.

## E-040 – Eigenes Projekt `OrbitStudio.Effects`, ohne SkiaSharp (18.10.2026)
- Alternativen: SkiaSharp-Bildfilter (schnell, GPU-fähig) oder eigene CPU-Implementierung. SkiaSharp-Filter haben
  eigene, nicht dokumentierte Rundungs- und Randregeln; exakte Referenztests (Spezifikation 14.1) wären nicht möglich,
  und die Effekte müssten `Imaging` referenzieren. Entscheidung: eigene CPU-Implementierung auf `PixelBuffer`
  (abhängig nur von Domain und History), jede Formel an der Klasse dokumentiert und mit von Hand berechneten
  Referenzwerten getestet. Optimierung nach Messung in M11.

## E-041 – Effektdefinitionen (18.10.2026)
- Gauß: σ = Radius/2, Kern bis ⌈3σ⌉ (Radius = 2σ ist eine übliche, gut vorhersagbare Größe). Einfacher Weichzeichner
  exakt in Ganzzahlen. Kontrast: Faktor ((100 + Wert)/100)², damit −100 % Grau und +100 % deutlich (×4) ergeben.
  Farbton/Sättigung in HSL. Schwarzweiß und Masken nutzen dieselbe Helligkeit (BT.601). Kantenerkennung mit Sobel,
  normiert auf eine harte 0→255-Kante = 255. Rauschen über einen Hash aus Startwert und Position: reproduzierbar und
  unabhängig vom bearbeiteten Ausschnitt (wichtig für Auswahl und spätere Kachelung).
- Rand: Faltungen verlängern den Bildrand (keine dunklen Ränder beim Weichzeichnen). Der Schlagschatten nimmt außerhalb
  der Ebene Alpha 0 an.
- Helligkeit/Kontrast und Farbton/Sättigung sind kombinierte Effekte aus den Einzeleffekten (ein Dialog wie in der
  Menüvorgabe, die Einzeleffekte bleiben für Effektbrowser und Plugins registriert).
- „Verzerrungen“ ist mit „Verpixeln“ belegt (exakt prüfbar); weitere Verzerrungen sind keine Pflicht nach
  Spezifikation 9.

## E-042 – Effektausführung im Hintergrund mit Sperre (18.10.2026)
- Gerechnet wird auf einer Kopie der Ebene (`Task.Run`, `CancellationToken`); währenddessen sind alle Befehle gesperrt
  (`IsBusy`) und Werkzeugeingaben werden ignoriert, damit die Ebene nicht parallel verändert und das Ergebnis nicht
  über neuere Änderungen geschrieben wird. Abbruch mit Esc; ein abgebrochener Effekt ändert nichts und legt keinen
  Historieneintrag an. Vorschauen laufen ebenfalls im Hintergrund, jede Parameteränderung bricht die vorige ab.

## E-043 – Projektformat: ZIP mit rohen, vormultiplizierten Pixeln (18.10.2026)
- Spezifikation 11.2 erlaubt PNG-kodierte oder rohe Pixel je Ebene. PNG speichert gerades Alpha; die Umrechnung aus
  dem internen vormultiplizierten Format verliert bei teiltransparenten Pixeln Information (Rundung), und das Projekt
  müsste `OrbitStudio.Imaging`/SkiaSharp kennen. Entscheidung: rohes BGRA wie im Speicher (byteweise verlustfrei,
  Rundreise exakt getestet), komprimiert durch das ZIP-Deflate. `OrbitStudio.Storage` hängt nur von `Domain` ab.
- `System.IO.Compression` und `System.Text.Json` (Teil von .NET, MIT) – keine neue Abhängigkeit.

## E-044 – Deflate „Fastest“ für Pixeldaten (18.10.2026)
- Pixeldaten sind groß (12 MP = 48 MB je Ebene). „Optimal“ verlangsamt das Speichern deutlich für meist wenige Prozent
  kleinere Dateien. Entscheidung: „Fastest“ für Pixel/Masken, „Optimal“ für das kleine Manifest. Messung in M11.

## E-045 – Prüfsummen und Prüfung vor dem Laden (18.10.2026)
- ZIP enthält CRC-32, das aber nicht vor absichtlich veränderten Dateien schützt und je nach .NET-Version nicht in
  jedem Lesepfad geprüft wird. Entscheidung: SHA-256 je Datenblock im Manifest, Prüfung beim Lesen; zusätzlich
  vollständige Prüfung des Manifests und aller Datenlängen **bevor** Puffer angelegt werden (Schutz vor übergroßen
  oder manipulierten Angaben, „Zip-Bomben“ werden durch die feste erwartete Länge begrenzt).

## E-046 – Migrationen auf JSON-Ebene (18.10.2026)
- Migrationen arbeiten auf dem rohen JSON-Objekt (vor der Abbildung auf Klassen), damit alte Felder umbenannt oder
  umgerechnet werden können, ohne alte Klassen weiter zu pflegen. Version 1 ist die erste Version; die Kette ist leer
  und wird durch Tests mit eigenen Migrationen (1 → 2 → 3, fehlender Schritt, ungültige Version) geprüft – es wird
  keine erfundene Vorgängerversion behauptet.

## E-047 – Speicherpunkt in der Historie (18.10.2026)
- Der Speicherpunkt ist der oberste Rückgängig-Eintrag beim Speichern (Referenz). Damit gilt ein Dokument nach
  Rückgängig/Wiederholen bis genau dorthin wieder als ungeändert. Neue Dokumente und geöffnete Bilder haben den
  Speicherpunkt „leerer Stapel“. Fällt der Punkt aus der begrenzten Historie, bleibt das Dokument als geändert
  markiert (lieber einmal zu oft fragen als Änderungen verlieren).

## E-048 – Speichern sperrt Bearbeitungen statt das Dokument zu kopieren (18.10.2026)
- Alternativen: vor dem Speichern alle Ebenen kopieren (doppelter Speicher) oder während des Schreibens Bearbeitungen
  sperren. Entscheidung: sperren (wie bei Effekten, E-042) – kein zusätzlicher Speicher, kein inkonsistenter Stand.

## E-049 – Rückfrage „Speichern / Nicht speichern / Abbrechen“ (18.10.2026)
- Eigene Schaltflächenbeschriftungen statt „Ja/Nein“ (eindeutig, Spezifikation 5.3). „Alle schließen“ und Beenden
  stellen zuerst alle Rückfragen und schließen erst danach, damit „Abbrechen“ wirklich nichts schließt (gefunden
  durch einen Smoke-Test: vorher wurden unveränderte Dokumente schon vor der abgebrochenen Rückfrage geschlossen).
- „Speichern“ in der Rückfrage für Bilder ohne Projektpfad fragt nach einer `.orbit`-Datei; das Originalbild (PNG …)
  wird nie stillschweigend überschrieben (sonst gingen Ebenen verloren).

## E-010 – Repository-Name ColourCraft, Produktname ORBIT Studio (18.10.2026)
- Das GitHub-Repository heißt `syncyxyc/ColourCraft`. Der Produktname laut Spezifikation ist
  „ORBIT Studio“ und zentral in `AppInfo.cs` austauschbar.

## E-050 – Farbkurven: monotone kubische Interpolation, Effekt noch nicht im Menü (22.10.2026)
- Alternativen: natürlicher kubischer Spline (überschwingt bei steilen Sprüngen, Werte außerhalb von 0–255 oder
  nicht-monotone Tonwerte), lineare Segmente (sichtbare Knicke). Entscheidung: monotoner kubischer Hermite-Spline nach
  Fritsch–Carlson; die Kurve läuft exakt durch alle Punkte und bleibt zwischen zwei Punkten monoton.
- Reihenfolge der Anwendung: erst Gesamtkurve, dann Kanalkurve; Eingang auf ganze Zahlen gerundet, Ausgabe aus einer
  256er-Tabelle (kaufmännisch gerundet, wie alle Effekte).
- Der Effekt ist absichtlich nicht in `StandardEffects` registriert: Ohne Kurveneditor wäre ein Menüpunkt eine Demo-
  Oberfläche ohne Funktion (Grundregel 0). Der Test `Parameter_ValidatesType_AndIsNotRegisteredInDefaultSet` hält das fest
  und muss beim Anschluss des Editors angepasst werden.

## E-051 – Farbfelder: Modell in Domain, Speicherung als Hex-Liste im Arbeitsbereich (22.10.2026)
- `Configuration` darf nach den Architekturregeln keine Projekte referenzieren; deshalb speichert `WorkspaceSettings`
  die Palette als Textliste („#RRGGBB“/„#RRGGBBAA“) und normalisiert sie selbst (kanonische Großschreibung, ungültige und
  doppelte Einträge entfallen). Die Umwandlung in Farben macht `SwatchPalette.FromHex` in der Domain.
- `Swatches = null` bedeutet Standardpalette, eine leere Liste eine absichtlich geleerte Palette.

## E-052 – Docking-Modell unabhängig von AvalonDock (23.10.2026)
- Das Layout (welches Panel auf welcher Seite, Reihenfolge, Größe, Sichtbarkeit) ist ein eigenes, unveränderliches Modell
  in `OrbitStudio.Docking` ohne UI-Abhängigkeit. Alternative: AvalonDock-XML-Layouts direkt speichern – wäre an die
  Bibliothek und an Windows gebunden, nicht testbar und bei Bibliotheksupdates fragil. Die spätere Oberfläche bildet das
  Modell auf AvalonDock ab (und zurück).
- Gelesene Layouts werden nie abgelehnt, sondern repariert (`Normalize`): unbekannte/doppelte Panels entfallen, fehlende
  (z. B. nach einem Update neu hinzugekommene) kommen mit Standardwerten dazu. „Standard“ ist als Arbeitsbereichsname
  reserviert, damit „Layout zurücksetzen“ immer eindeutig ist.
- Mindestgröße 48 statt 120: Die schmale Werkzeugleiste (Standard 64) ist ein gültiges Layout; das haben die Tests
  beim ersten Lauf aufgedeckt.

