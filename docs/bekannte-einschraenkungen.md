# Bekannte Einschränkungen

Stand: 18.10.2026, nach M1. Jeder Punkt nennt den Meilenstein, in dem er behoben wird.

## Funktionen, die noch fehlen

- **Bilddaten:** Dokumente haben noch keinen Pixelpuffer. Die Leinwand zeigt Weiß oder das
  Transparenz-Schachbrett. → M2
- **Öffnen, Importieren, Exportieren, Zuletzt verwendet:** im Menü deaktiviert. → M2
- **Zoom, Schwenken, Anpassen, Pixelraster:** deaktiviert. Die Leinwand zeigt 1 Dokumentpixel pro
  geräteunabhängiger Einheit; bei Windows-Skalierung ≠ 100 % ist „100 %“ daher noch nicht
  pixelgenau. → M2
- **Ebenen, Mischmodi, Masken:** Menü „Ebenen“ deaktiviert. → M3
- **Werkzeuge, Auswahl, Bearbeiten (Rückgängig, Zwischenablage), Bild-Transformationen:**
  deaktiviert. Die Statusleiste zeigt „Werkzeug: keines“. → M4
- **Strichmuster-Werkzeug:** fehlt. → M5
- **Farben und Effekte:** deaktiviert. → M6
- **Speichern, Speichern unter, Kopie speichern (.orbit):** deaktiviert. Die Rückfrage bei
  ungespeicherten Änderungen bietet deshalb nur „Verwerfen“ (OK) oder „Abbrechen“; die Option
  „Speichern“ folgt. In M1 entstehen noch keine ungespeicherten Änderungen. → M7
- **Symbolleisten, andockbare Panels, Fenster-Menü, Lineale, Raster, Hilfslinien, Vorder-/
  Hintergrundfarbe in der Oberfläche:** fehlen bzw. deaktiviert. → M8
- **Layout zurücksetzen:** es gibt in M1 nur die Fensterposition; Panel-Layouts folgen. → M8
- **Einstellungen-Dialog, Tastenkürzel-Editor, Theme-Wechsel (hell/dunkel/System),
  Menüanpassung, Arbeitsbereiche:** deaktiviert. → M9
- **Plugins, Erweiterungen:** deaktiviert. → M10
- **Diagnosebericht, Diagnose-Panel:** deaktiviert. Logs liegen unter
  `%LOCALAPPDATA%\OrbitStudio\logs`. → M11
- **Benutzerhandbuch (F1), Lizenzinformationen:** deaktiviert. → M12
- **Drucken / Druckvorschau:** nicht geplant innerhalb M0–M12, späterer Ausbau.

## Technische Einschränkungen

- **Kein App-Icon:** Die EXE verwendet das Standard-Symbol. → M8
- **EXE-Größe:** Die Single-File-EXE ist 142,3 MB groß (Laufzeit + WPF + ReadyToRun, unkomprimiert).
  Komprimierung (`EnableCompressionInSingleFile`) wird in M12 geprüft (Abwägung Startzeit).
- **Nur Deutsch:** Die Architektur erlaubt weitere Sprachen, es gibt aber nur `Strings.de.xaml`.
- **Theme:** Nur das Basis-Theme; Menü und Tabs nutzen die Windows-Standardoptik.
- **DPI:** PerMonitorV2 ist aktiv; die Darstellung bei 150 %/200 % und Monitorwechsel ist noch
  nicht manuell geprüft.
- **Fensterposition:** Wird verworfen, wenn sie nicht mehr auf dem virtuellen Bildschirm liegt
  (z. B. abgesteckter Monitor); das Fenster startet dann zentriert.
