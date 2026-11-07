# Bekannte Einschränkungen

Stand: 18.10.2026, nach M7. Jeder Punkt nennt den Meilenstein, in dem er behoben wird.

## Funktionen, die noch fehlen

- **Werkzeuggruppe „Erweitert“:** Text, Klonen und Retusche fehlen (Hand und Zoom sind umgesetzt). Sie sind
  keinem Meilenstein fest zugeordnet; Entscheidung über Umfang spätestens in M11.
- **Farbfelder (Palette):** Modell (`SwatchPalette`) und Speicherung (`WorkspaceSettings.Swatches`) sind getestet, ein Panel
  zum Auswählen und Bearbeiten fehlt noch; Farben werden weiter über Farbdialog oder Pipette gewählt. → M8 (Windows)
- **Ebene transformieren:** Drehen und Skalieren nur über einen Zahlendialog, ohne Anfasser auf der Leinwand und ohne
  Live-Vorschau; Ergebnis bilinear in der Bildgröße (über den Rand ragende Teile werden abgeschnitten). → M8
- **Verschieben-Werkzeug:** verschiebt nur ganze Pixel; keine Unterpixel-Positionen, kein Drehen/Skalieren der Auswahl.
- **Auswahl:** harte Kanten (keine Weiche Auswahlkante/Glätten); Zauberstab vergleicht nur 4er-Nachbarn.
- **Zwischenablage:** Einfügen fügt immer als neue Ebene ein (nicht in die aktive Ebene); „Kopieren aus allen Ebenen“
  fehlt.
- **Pinsel:** nur rund; keine Druckempfindlichkeit für Grafiktabletts.
- **Farbkurven:** Kurvenmodell und Effekt `curves` sind getestet, aber **nicht im Menü und nicht in der Standard-Effektliste**,
  weil der Kurveneditor (WPF-Steuerelement) fehlt; `EffectDialog` kennt `CurveParameter` noch nicht. → M8 (Windows)
- **Effekte allgemein:** wirken destruktiv auf die Ebenenpixel (mit Rückgängig), nicht als nachträglich änderbare
  Ebeneneffekte (E-039). Kein „Letzten Effekt wiederholen“, kein Effektbrowser (→ M8). Im Modus „Maske bearbeiten“
  wirken Effekte trotzdem auf die Pixel, nicht auf die Maske.
- **Effektvorschau:** zeigt nur die aktive Ebene (ohne andere Ebenen, Ebenendeckkraft und Mischmodus) und bei großen
  Bildern verkleinert; Effekte mit Radius sehen in der verkleinerten Vorschau daher nur ungefähr so aus wie das
  Ergebnis. Rauschen ergibt in der verkleinerten Vorschau ein anderes Muster als im Ergebnis.
- **Einzel-Effekte Helligkeit, Kontrast, Farbton, Sättigung:** im Effektmodell vorhanden und getestet, im Menü aber nur
  über die kombinierten Dialoge erreichbar (Effektbrowser → M8).
- **Kombinierte Effekte** runden nach jedem Teilschritt auf 8 Bit (wie zwei getrennte Anwendungen).
- **Verzerrungen:** nur „Verpixeln“; Wellen, Verwirbeln u. Ä. fehlen. Erweiterungen (Plugin-Effekte) → M10.
- **Fortschritt:** Die Statusleiste meldet nur „wird berechnet“, keinen Prozentwert; Abbrechen nur mit Esc. → M8
- **Projektformat `.orbit`:** speichert keine Auswahl, keine Ansicht (Zoom, Verschiebung) und keine Historie; nach
  dem Öffnen beginnt Rückgängig neu. Kein Vorschaubild im Container, keine automatische Sicherung oder
  Wiederherstellung nach Absturz. Während des Speicherns sind Bearbeitungen gesperrt (kein Speichern „im
  Hintergrund weiterarbeiten“). Projekte mit nicht destruktiven Effekten gibt es noch nicht (E-039).
- **Speichern beim Schließen über „Speichern“** schreibt an den bisherigen Projektpfad; Bilder ohne Projektpfad
  (z. B. geöffnete PNG) werden als `.orbit` gespeichert, nicht zurück ins Bildformat (dafür „Exportieren“).
- **Ebenen-Panel:** fest rechts, nicht andockbar, ohne Vorschaubilder; Umsortieren nur mit Nach oben/unten
  innerhalb einer Gruppe (nicht per Ziehen, nicht in/aus Gruppen); Auswahlfarbe ohne Fokus ist das Windows-Grau. → M8
- **Panels allgemein:** Werkzeugleiste, Optionsleiste und Ebenen-Panel sind fest angeordnet; kein Historie-Panel
  (Rückgängig nur schrittweise), kein Navigator; Werkzeugsymbole sind Unicode-Zeichen statt eigener Vektor-Icons. → M8
- **Symbolleisten, andockbare Panels, Fenster-Menü, Lineale, Raster, Hilfslinien:** fehlen bzw. deaktiviert. Rechenmodell für
  Hilfslinien, Raster und Lineal-Teilung (`ViewAids`) ist getestet, wird aber nirgends gezeichnet oder gespeichert (Hilfslinien
  sind noch nicht Teil des `.orbit`-Formats). → M8 (Windows)
- **Werkzeugoptionen und Farben** werden nicht über einen Neustart hinweg gespeichert. → M9
- **Layout zurücksetzen / Arbeitsbereiche:** Das Modell (`PanelLayout`, `WorkspaceCatalog` in `OrbitStudio.Docking`) ist getestet,
  aber nicht an die Oberfläche angebunden: Es gibt weiterhin nur Fensterposition und Pixelraster-Einstellung. → M8 (Windows)
- **Einstellungen-Dialog, Tastenkürzel-Editor, Theme-Wechsel, Menüanpassung, Arbeitsbereiche:** deaktiviert. Die Modelle
  `OrbitStudio.Themes` (Prüfung, Import/Export) und `GestureProfile` (Import/Export, Konfliktauflösung) sind getestet, aber ohne
  Oberfläche. Ein Theme-Wechsel zur Laufzeit (WPF-Ressourcen aus den Tokens) fehlt.
  Die JPEG/WebP-Qualität (Standard 92) ist nur über `settings.json` änderbar. → M9
- **Plugins, Erweiterungen:** Laufzeit, Vertrag und Beispielplugin sind getestet (siehe `plugin-development.de.md`), aber nicht
  angebunden: kein Verwaltungsdialog, kein Laden beim Programmstart, Menü „Erweiterungen“ deaktiviert. API 1 kennt nur Effekte;
  Plugin-Effekte haben noch keine eigenen Texte. → M10 (Windows)
- **Diagnosebericht, Diagnose-Panel:** deaktiviert. Logs liegen unter `%LOCALAPPDATA%\OrbitStudio\logs`. → M11
- **Benutzerhandbuch (F1), Lizenzinformationen im Programm:** deaktiviert; Handbuch liegt als
  `docs/user-guide.de.md` vor. → M12
- **Drucken / Druckvorschau:** nicht geplant innerhalb M0–M12, späterer Ausbau.

## Strichmuster-Werkzeug

- **Vorschau:** zeigt die Musterfarbe normal über dem Gesamtbild. Deckkraft, Mischmodus und Maske der aktiven Ebene
  sowie darüberliegende Ebenen wirken erst nach dem Anwenden (E-033). Im Modus „Maske bearbeiten“ zeigt die Vorschau
  ebenfalls die Farbe, nicht die Wirkung auf die Maske. Unter 100 % Zoom wird die Vorschau in reduzierter Auflösung
  berechnet; das angewendete Ergebnis hat immer volle Auflösung.
- **Linien** sind Kurven durch bis zu 16 Punkte (Catmull-Rom-Spline, E-050). Enden und Striche sind flach; keine
  runden Enden, keine Muster aus mehreren Strichlängen, keine Tangenten-Griffe pro Punkt (Kurvenform nur über die
  Punkte).
- **Punkte entfernen** nur per Rechtsklick auf einen inneren Punkt; keine Taste dafür (ToolKey kennt kein Entf).
- **Länge und Winkel** werden angezeigt, aber nicht als Eingabefelder angeboten (Eingabe über X1/Y1/X2/Y2).
- **Optionsleiste:** Das Werkzeug hat 14 Optionen; die Leiste bricht in mehrere Zeilen um und nimmt bei schmalem
  Fenster viel Höhe ein. Eine andockbare Werkzeugoptionen-Palette folgt. → M8
- **Werkzeugparameter** (Stärke, Länge, Abstand, Farbe …) werden nicht über einen Neustart gespeichert. → M9

## Dateiformate

- **TIFF:** nicht unterstützt (SkiaSharp hat keinen TIFF-Codec).
- **GIF:** nur Lesen, nur das erste Einzelbild; Animationen gehen verloren. Kein GIF-Export.
- **Metadaten:** EXIF, IPTC, XMP und eingebettete Farbprofile werden beim Öffnen ausgewertet
  (Ausrichtung, Umrechnung nach sRGB), aber nicht übernommen. Exportierte Dateien sind sRGB ohne Metadaten.
- **Auflösung (DPI):** wird beim Öffnen nicht aus der Datei gelesen (immer 96 dpi). BMP schreibt die
  Dokumentauflösung, PNG/JPEG/WebP derzeit nicht.
- **16-Bit-Bilder** werden beim Öffnen auf 8 Bit je Kanal reduziert.
- **Dateigröße:** Dateien über 1 GiB werden nicht geöffnet; Bilder über 65.535 px Kantenlänge oder
  268 Megapixel werden abgewiesen.

## Historie

- **Speicher:** Grenzen 100 Schritte / 1024 MB je Dokument (nur über `settings.json` änderbar, → M9). Ein einzelner
  sehr großer Schritt bleibt auch über der Grenze erhalten.
- **Speicherpunkt:** Fällt der gespeicherte Stand wegen der Grenzen aus der Historie, kann er durch Rückgängig nicht
  mehr erreicht werden; das Dokument bleibt dann als geändert markiert (sicherer Fall).

## Leistung (noch nicht gemessen, → M11)

- Jeder Malzug kopiert zu Beginn die ganze Ebene und legt einen Abdeckungspuffer an (zusammen 8 Byte/Pixel);
  bei sehr großen Bildern spürbar. Verlauf und Formvorschau berechnen bei jeder Mausbewegung den betroffenen Bereich neu.
- Zauberstab mit Quelle „Bild“, Farbeimer mit Quelle „Bild“ und Bildtransformationen laufen im UI-Thread.
- Projekte: Pixeldaten werden roh mit Deflate („Fastest“) gespeichert und mit SHA-256 geprüft; Dateien sind daher
  größer als PNG-Ebenen und Speichern/Öffnen sehr großer Projekte dauert entsprechend (nicht gemessen).
- Effekte: Gaußscher Weichzeichner, Schärfen und Schlagschatten kosten O(Radius) je Pixel, Rauschreduktion O(Radius) je
  Pixel und Kanal plus Median-Suche; bei großen Radien auf großen Bildern spürbar (läuft im Hintergrund, abbrechbar).
  Im Smoke-Test protokolliert, keine Messung nach Spezifikation 14.2: Gauß Radius 4 auf 3000 × 2000 in 752 ms
  (Debug) bzw. 169 ms (veröffentlichte EXE), je ein Lauf auf dem Entwicklungsrechner.
  Für die Effektberechnung wird die Ebene kopiert (4 Byte/Pixel) plus Ergebnis für den Zielbereich.

## Ebenen und Komposition

- **Zusammenführen** erhält das Aussehen exakt nur, wenn die untere Ebene deckend und „Normal“ ist. Bei
  transparenter unterer Ebene und anderen Mischmodi kann das Ergebnis abweichen (siehe architecture.md).
- **Gruppe auflösen** verwirft Deckkraft und Mischmodus der Gruppe (mit Rückfrage).
- **Leistung:** Jede Eigenschaftsänderung (z. B. Deckkraft-Regler) setzt das ganze Bild neu zusammen (CPU,
  parallel). Bei großen Bildern mit vielen Ebenen kann das spürbar dauern; Messungen und Optimierung in M11.
  Die Komposition läuft derzeit im UI-Thread. → M11
- **Speicher:** jede Ebene belegt 4 Byte je Bildpixel (z. B. 12 MP → 48 MB je Ebene), dazu das Komposit.
- **Import:** größere Bilder werden oben links ausgerichtet und beschnitten (mit Meldung); kein automatisches
  Vergrößern der Leinwand.

## Technische Einschränkungen

- **Sehr große Bilder:** Die Anzeige erfolgt als eine WPF-Bitmap. Bilder über der GPU-Texturgrenze
  (typisch 16.384 px) zeichnet WPF in Software – langsamer. Gekachelte Darstellung erst bei Bedarf (M11).
- **Kein App-Icon:** Die EXE verwendet das Standard-Symbol. → M8
- **EXE-Größe:** 157,6 MB (Laufzeit, WPF, SkiaSharp-Native, ReadyToRun, unkomprimiert).
  Komprimierung wird in M12 geprüft.
- **Lizenzhinweise:** `THIRD-PARTY-NOTICES.txt` von SkiaSharp wird noch nicht mit ausgeliefert; Prüfung
  des LGPL-Eintrags (libmicrohttpd) offen. → M12 (siehe dependencies-and-licenses.de.md)
- **Nur Deutsch:** Architektur erlaubt weitere Sprachen, es gibt nur `Strings.de.xaml`.
- **Theme:** Nur das Basis-Theme; Menü, Tabs und Scrollleisten nutzen die Windows-Standardoptik. → M9
- **DPI:** PerMonitorV2 ist aktiv und die Transformation rechnet mit der DPI-Skalierung (Unit-Tests),
  die Darstellung bei 150 %/200 % und beim Monitorwechsel wurde aber noch nicht am Bildschirm geprüft.
- **Fensterposition:** wird verworfen, wenn sie nicht mehr auf dem virtuellen Bildschirm liegt.
- **UI-Eingabetest:** braucht eine unbenutzte Sitzung im Vordergrund; deshalb nicht im Standard-Testlauf. Die in M4
  (Pinselstrich + Strg+Z) und M5 (Strichmuster mit echter Maus: Kontrollpunkte ziehen, Werte tippen, Anwenden,
  Strg+Z/Strg+Y) ergänzten Schritte wurden noch nicht ausgeführt. Die Abnahme des Strichmusters ist über den
  Smoke-Test im echten Fenster belegt (Eingaben über den Werkzeug-Controller, nicht über Windows-Mausereignisse).
