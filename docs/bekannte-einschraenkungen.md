# Bekannte Einschränkungen

Stand: 18.10.2026, nach M2. Jeder Punkt nennt den Meilenstein, in dem er behoben wird.

## Funktionen, die noch fehlen

- **Ebenen, Mischmodi, Masken:** Ein Dokument hat genau eine Pixelfläche. Menü „Ebenen“ deaktiviert. → M3
- **Importieren (Bild als Ebene):** deaktiviert. Bilder lassen sich über „Öffnen“ als neues Dokument laden. → M3
- **Warnung „Ebenen werden zusammengeführt“ beim PNG-Export:** entfällt, solange es nur eine Ebene gibt. → M3
- **Werkzeuge, Auswahl, Bearbeiten (Rückgängig, Zwischenablage), Bild-Transformationen, Handwerkzeug:**
  deaktiviert. Pixel können noch nicht bearbeitet werden. → M4
- **Strichmuster-Werkzeug:** fehlt. → M5
- **Farben und Effekte:** deaktiviert. → M6
- **Speichern, Speichern unter, Kopie speichern (.orbit):** deaktiviert. Die Rückfrage bei ungespeicherten
  Änderungen bietet nur „Verwerfen“ (OK) oder „Abbrechen“. In M2 entstehen noch keine ungespeicherten
  Änderungen, weil es keine Bearbeitung gibt. → M7
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

## Technische Einschränkungen

- **Sehr große Bilder:** Die Anzeige erfolgt als eine WPF-Bitmap. Bilder über der GPU-Texturgrenze
  (typisch 16.384 px) zeichnet WPF in Software – langsamer. Gekachelte Darstellung erst bei Bedarf (M11).
- **Kein App-Icon:** Die EXE verwendet das Standard-Symbol. → M8
- **EXE-Größe:** 156,6 MB (Laufzeit, WPF, SkiaSharp-Native, ReadyToRun, unkomprimiert).
  Komprimierung wird in M12 geprüft.
- **Lizenzhinweise:** `THIRD-PARTY-NOTICES.txt` von SkiaSharp wird noch nicht mit ausgeliefert; Prüfung
  des LGPL-Eintrags (libmicrohttpd) offen. → M12 (siehe dependencies-and-licenses.de.md)
- **Nur Deutsch:** Architektur erlaubt weitere Sprachen, es gibt nur `Strings.de.xaml`.
- **Theme:** Nur das Basis-Theme; Menü, Tabs und Scrollleisten nutzen die Windows-Standardoptik. → M9
- **DPI:** PerMonitorV2 ist aktiv und die Transformation rechnet mit der DPI-Skalierung (Unit-Tests),
  die Darstellung bei 150 %/200 % und beim Monitorwechsel wurde aber noch nicht am Bildschirm geprüft.
- **Fensterposition:** wird verworfen, wenn sie nicht mehr auf dem virtuellen Bildschirm liegt.
- **UI-Eingabetest:** braucht eine unbenutzte Sitzung im Vordergrund; deshalb nicht im Standard-Testlauf.
