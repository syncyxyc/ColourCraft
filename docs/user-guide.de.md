# Benutzerhandbuch – ORBIT Studio

Stand: Meilenstein M3. Dieses Handbuch beschreibt nur Funktionen, die bereits umgesetzt sind.
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

## Ebenen

Rechts steht das Ebenen-Panel. Die oberste Zeile ist die oberste Ebene. Ein Klick wählt die **aktive Ebene**,
auf die sich alle Befehle im Menü **Ebenen** beziehen.

| In der Zeile | Bedeutung |
|---|---|
| Kästchen links | Sichtbarkeit ein/aus |
| Name (fett = Gruppe), darunter Deckkraft und Mischmodus | Doppelklick öffnet die Ebeneneigenschaften |
| „M“ | Ebene hat eine Maske (blass = deaktiviert) |
| 🔒-Kästchen | Sperre: nicht löschen, verschieben, zusammenführen |

Unter der Liste: **Deckkraft** (Regler) und **Mischmodus** der aktiven Ebene, darunter Schaltflächen für
Neue Ebene, Duplizieren, Nach oben, Nach unten und Löschen.

| Befehl (Menü Ebenen) | Kürzel |
|---|---|
| Neue Ebene | Strg+Umschalt+N |
| Duplizieren | Strg+J |
| Umbenennen / Eigenschaften | F2 |
| Nach oben / Nach unten | Strg+Umschalt+Pfeil hoch / Strg+Umschalt+Pfeil runter |
| Gruppe erstellen / auflösen | Strg+G / Strg+Umschalt+G |
| Mit darunterliegender Ebene zusammenführen | Strg+E |
| Bild auf eine Ebene reduzieren | Strg+Umschalt+F |

**Mischmodi:** Normal, Multiplizieren, Bildschirm, Addieren, Negativ, Differenz, Abdunkeln, Aufhellen, Überlagern.

**Gruppen** fassen Ebenen zusammen; Sichtbarkeit, Deckkraft und Mischmodus der Gruppe wirken auf ihren ganzen Inhalt.

**Masken:** Ebenen › Maske hinzufügen legt eine weiße Maske an (alles sichtbar). Maske umkehren, aktiv/inaktiv
schalten, anwenden (in die Pixel einrechnen) oder löschen. Masken bemalen ist ab Meilenstein M4 möglich.

**Importieren** (Datei › Importieren, Strg+Umschalt+O) fügt Bilddateien als neue Ebenen ein (oben links ausgerichtet;
zu große Bilder werden beschnitten).

Noch gibt es kein Rückgängig. Löschen, Zusammenführen, Reduzieren und „Maske anwenden“ fragen deshalb nach.
Ebenen bleiben erst mit dem Projektformat `.orbit` (Meilenstein M7) über das Schließen hinaus erhalten.

## Exportieren

**Datei › Exportieren** (Strg+Umschalt+E): Format über die Dateiendung bzw. den Dateityp wählen –
PNG, JPEG, BMP oder WebP. Alle sichtbaren Ebenen werden zu einem Bild zusammengesetzt; bei mehreren Ebenen
erscheint vorher ein Hinweis. Die Ebenen im Dokument bleiben dabei erhalten.

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
| Importieren als Ebene | Strg+Umschalt+O |
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
