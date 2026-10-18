# Benutzerhandbuch – ORBIT Studio

Stand: Meilenstein M2. Dieses Handbuch beschreibt nur Funktionen, die bereits umgesetzt sind.
Ausgegraute Menüeinträge nennen im Tooltip den Meilenstein, in dem sie folgen.

## Bilder anlegen und öffnen

- **Datei › Neu** (Strg+N): Name, Breite, Höhe (1–65.535 px, höchstens 268 Megapixel) und Hintergrund
  (Weiß oder Transparent) wählen.
- **Datei › Öffnen** (Strg+O): eine oder mehrere Dateien wählen. Unterstützt: PNG, JPEG, BMP, WebP, GIF
  (nur erstes Einzelbild). Jede Datei wird ein eigener Tab. Eine bereits geöffnete Datei wird nur nach vorn geholt.
- **Ziehen und Ablegen:** Bilddateien aus dem Explorer in das Fenster ziehen.
- **Datei › Zuletzt verwendet:** die letzten zehn Dateien. Nicht mehr vorhandene Dateien werden beim
  Öffnen gemeldet und aus der Liste entfernt.

Fotos mit EXIF-Ausrichtung erscheinen richtig gedreht. Farbprofile werden nach sRGB umgerechnet.

## Ansicht

| Aktion | Bedienung |
|---|---|
| Vergrößern / Verkleinern | Mausrad (am Mauszeiger), Strg++ / Strg+- |
| 100 % (1 Bildpixel = 1 Bildschirmpixel) | Strg+0 |
| Bild anpassen | Strg+B |
| Verschieben | mittlere Maustaste ziehen, Leertaste + linke Maustaste ziehen, Scrollleisten |
| Pixelraster ab 800 % | Ansicht › Pixelraster (wird gespeichert) |
| Vollbild | F11 |

Zoom und Ausschnitt merkt sich jedes Dokument einzeln. Die Statusleiste zeigt Bildgröße,
Pixelposition unter dem Mauszeiger und Zoom. Zoomen verändert niemals das Bild.

## Exportieren

**Datei › Exportieren** (Strg+Umschalt+E): Format über die Dateiendung bzw. den Dateityp wählen –
PNG, JPEG, BMP oder WebP.

- PNG und BMP sind verlustfrei und behalten Transparenz.
- JPEG kann keine Transparenz speichern: transparente Bereiche werden weiß gefüllt (mit Rückfrage).
- JPEG/WebP werden mit Qualität 92 gespeichert.
- Eine vorhandene Datei wird erst ersetzt, wenn die neue vollständig geschrieben ist.

## Dokumente schließen

Tab-Schaltfläche ✕, **Datei › Schließen** (Strg+W) oder **Alle schließen** (Strg+Umschalt+W).

## Tastenkürzel (umgesetzte Befehle)

| Befehl | Kürzel |
|---|---|
| Neu | Strg+N |
| Öffnen | Strg+O |
| Exportieren | Strg+Umschalt+E |
| Schließen / Alle schließen | Strg+W / Strg+Umschalt+W |
| Vergrößern / Verkleinern | Strg++ / Strg+- |
| 100 % / Bild anpassen | Strg+0 / Strg+B |
| Vollbild | F11 |

## Startparameter

```
OrbitStudio.exe [Bilddateien …] [--data-dir <Ordner>]
```

`--data-dir` speichert Einstellungen und Logs im angegebenen Ordner statt im Benutzerprofil.

## Datenschutz

ORBIT Studio sendet keine Daten ins Netz. Logs (`%LOCALAPPDATA%\OrbitStudio\logs`, 14 Tage aufbewahrt)
enthalten keine Bildinhalte und keine Dateiinhalte. Bei Datei-Fehlern werden Dateiname bzw. -pfad
protokolliert.
