# Bekannte Einschränkungen

Stand: 18.10.2026, nach M4. Jeder Punkt nennt den Meilenstein, in dem er behoben wird.

## Funktionen, die noch fehlen

- **Strichmuster-Werkzeug:** fehlt. → M5
- **Werkzeuggruppe „Erweitert“:** Text, Klonen und Retusche fehlen (Hand und Zoom sind umgesetzt). Sie sind
  keinem Meilenstein fest zugeordnet; Entscheidung über Umfang spätestens in M11.
- **Farbfelder (Palette):** fehlen; Farben werden über Farbdialog oder Pipette gewählt. → M8
- **Ebene transformieren:** Drehen und Skalieren nur über einen Zahlendialog, ohne Anfasser auf der Leinwand und ohne
  Live-Vorschau; Ergebnis bilinear in der Bildgröße (über den Rand ragende Teile werden abgeschnitten). → M8
- **Verschieben-Werkzeug:** verschiebt nur ganze Pixel; keine Unterpixel-Positionen, kein Drehen/Skalieren der Auswahl.
- **Auswahl:** harte Kanten (keine Weiche Auswahlkante/Glätten); Zauberstab vergleicht nur 4er-Nachbarn.
- **Zwischenablage:** Einfügen fügt immer als neue Ebene ein (nicht in die aktive Ebene); „Kopieren aus allen Ebenen“
  fehlt.
- **Pinsel:** nur rund; keine Druckempfindlichkeit für Grafiktabletts.
- **Farben und Effekte:** deaktiviert. → M6
- **Speichern, Speichern unter, Kopie speichern (.orbit):** deaktiviert. Ebenen gehen beim Schließen verloren;
  der Export schreibt nur das zusammengesetzte Bild (mit Warnung). Die Rückfrage bei ungespeicherten Änderungen
  bietet nur „Verwerfen“ (OK) oder „Abbrechen“. → M7
- **Ebenen-Panel:** fest rechts, nicht andockbar, ohne Vorschaubilder; Umsortieren nur mit Nach oben/unten
  innerhalb einer Gruppe (nicht per Ziehen, nicht in/aus Gruppen); Auswahlfarbe ohne Fokus ist das Windows-Grau. → M8
- **Panels allgemein:** Werkzeugleiste, Optionsleiste und Ebenen-Panel sind fest angeordnet; kein Historie-Panel
  (Rückgängig nur schrittweise), kein Navigator; Werkzeugsymbole sind Unicode-Zeichen statt eigener Vektor-Icons. → M8
- **Symbolleisten, andockbare Panels, Fenster-Menü, Lineale, Raster, Hilfslinien:** fehlen bzw. deaktiviert. → M8
- **Werkzeugoptionen und Farben** werden nicht über einen Neustart hinweg gespeichert. → M9
- **Layout zurücksetzen:** es gibt nur Fensterposition und Pixelraster-Einstellung. → M8
- **Einstellungen-Dialog, Tastenkürzel-Editor, Theme-Wechsel, Menüanpassung, Arbeitsbereiche:** deaktiviert.
  Die JPEG/WebP-Qualität (Standard 92) ist nur über `settings.json` änderbar. → M9
- **Plugins, Erweiterungen:** deaktiviert. → M10
- **Diagnosebericht, Diagnose-Panel:** deaktiviert. Logs liegen unter `%LOCALAPPDATA%\OrbitStudio\logs`. → M11
- **Benutzerhandbuch (F1), Lizenzinformationen im Programm:** deaktiviert; Handbuch liegt als
  `docs/user-guide.de.md` vor. → M12
- **Drucken / Druckvorschau:** nicht geplant innerhalb M0–M12, späterer Ausbau.

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

- **Ungespeicherte Änderungen:** Rückgängig bis zum Ausgangszustand entfernt die Änderungsmarkierung (*) nicht.
  → M7 (mit dem Speicherpunkt des Projektformats)
- **Speicher:** Grenzen 100 Schritte / 1024 MB je Dokument (nur über `settings.json` änderbar, → M9). Ein einzelner
  sehr großer Schritt bleibt auch über der Grenze erhalten.

## Leistung (noch nicht gemessen, → M11)

- Jeder Malzug kopiert zu Beginn die ganze Ebene und legt einen Abdeckungspuffer an (zusammen 8 Byte/Pixel);
  bei sehr großen Bildern spürbar. Verlauf und Formvorschau berechnen bei jeder Mausbewegung den betroffenen Bereich neu.
- Zauberstab mit Quelle „Bild“, Farbeimer mit Quelle „Bild“ und Bildtransformationen laufen im UI-Thread.

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
- **UI-Eingabetest:** braucht eine unbenutzte Sitzung im Vordergrund; deshalb nicht im Standard-Testlauf. Der in M4
  ergänzte Schritt (Pinselstrich mit echter Maus + Strg+Z) wurde noch nicht ausgeführt.
