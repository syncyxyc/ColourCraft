# Bekannte Einschränkungen

Stand: 18.10.2026, nach M3. Jeder Punkt nennt den Meilenstein, in dem er behoben wird.

## Funktionen, die noch fehlen

- **Rückgängig/Wiederholen:** fehlt, auch für Ebenenoperationen. Löschen, Zusammenführen, Reduzieren und
  „Maske anwenden“ fragen deshalb vorher nach. → M4
- **Werkzeuge, Auswahl, Bearbeiten (Zwischenablage), Bild-Transformationen, Handwerkzeug:** deaktiviert.
  Pixel und Masken können noch nicht bemalt werden; Masken lassen sich nur hinzufügen, umkehren, (de)aktivieren,
  anwenden und löschen. → M4
- **Auswahl aus Ebenentransparenz, Ebene transformieren:** deaktiviert. → M4
- **Strichmuster-Werkzeug:** fehlt. → M5
- **Farben und Effekte:** deaktiviert. → M6
- **Speichern, Speichern unter, Kopie speichern (.orbit):** deaktiviert. Ebenen gehen beim Schließen verloren;
  der Export schreibt nur das zusammengesetzte Bild (mit Warnung). Die Rückfrage bei ungespeicherten Änderungen
  bietet nur „Verwerfen“ (OK) oder „Abbrechen“. → M7
- **Ebenen-Panel:** fest rechts, nicht andockbar, ohne Vorschaubilder; Umsortieren nur mit Nach oben/unten
  innerhalb einer Gruppe (nicht per Ziehen, nicht in/aus Gruppen); Auswahlfarbe ohne Fokus ist das Windows-Grau. → M8
- **Symbolleisten, andockbare Panels, Fenster-Menü, Lineale, Raster, Hilfslinien, Navigator,
  Vorder-/Hintergrundfarbe:** fehlen bzw. deaktiviert. → M8
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
- **EXE-Größe:** 156,9 MB (Laufzeit, WPF, SkiaSharp-Native, ReadyToRun, unkomprimiert).
  Komprimierung wird in M12 geprüft.
- **Lizenzhinweise:** `THIRD-PARTY-NOTICES.txt` von SkiaSharp wird noch nicht mit ausgeliefert; Prüfung
  des LGPL-Eintrags (libmicrohttpd) offen. → M12 (siehe dependencies-and-licenses.de.md)
- **Nur Deutsch:** Architektur erlaubt weitere Sprachen, es gibt nur `Strings.de.xaml`.
- **Theme:** Nur das Basis-Theme; Menü, Tabs und Scrollleisten nutzen die Windows-Standardoptik. → M9
- **DPI:** PerMonitorV2 ist aktiv und die Transformation rechnet mit der DPI-Skalierung (Unit-Tests),
  die Darstellung bei 150 %/200 % und beim Monitorwechsel wurde aber noch nicht am Bildschirm geprüft.
- **Fensterposition:** wird verworfen, wenn sie nicht mehr auf dem virtuellen Bildschirm liegt.
- **UI-Eingabetest:** braucht eine unbenutzte Sitzung im Vordergrund; deshalb nicht im Standard-Testlauf.
