# Benutzerhandbuch – ORBIT Studio

Stand: Meilenstein M7. Dieses Handbuch beschreibt nur Funktionen, die bereits umgesetzt sind.
Ausgegraute Menüeinträge nennen im Tooltip den Meilenstein, in dem sie folgen.

## Bilder anlegen und öffnen

- **Datei › Neu** (Strg+N): Name, Breite, Höhe (1–65.535 px, höchstens 268 Megapixel) und Hintergrund
  (Weiß oder Transparent) wählen.
- **Datei › Öffnen** (Strg+O): eine oder mehrere Dateien wählen. Unterstützt: ORBIT-Projekte (`.orbit`), PNG, JPEG,
  BMP, WebP, GIF (nur erstes Einzelbild). Jede Datei wird ein eigener Tab. Eine bereits geöffnete Datei wird nur nach
  vorn geholt.
- **Ziehen und Ablegen:** Bild- und Projektdateien aus dem Explorer in das Fenster ziehen.
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

Zoom und Ausschnitt merkt sich jedes Dokument einzeln. Die Statusleiste zeigt Werkzeug, Bildgröße,
Pixelposition unter dem Mauszeiger, Zoom und Auswahlgröße. Zoomen verändert niemals das Bild.

## Werkzeuge

Links stehen die Werkzeuge, darunter Vorder- und Hintergrundfarbe. Oben zeigt die **Optionsleiste** die
Einstellungen des aktiven Werkzeugs (Werkzeuge › Werkzeugoptionen blendet sie aus und ein). Linke Maustaste malt mit
der Vordergrundfarbe, rechte mit der Hintergrundfarbe. **Esc** bricht eine laufende Bearbeitung ab, **Enter**
übernimmt sie (Polygonauswahl, Zuschneiden).

| Werkzeug | Taste | Hinweise |
|---|---|---|
| Rechteckauswahl / Ellipsenauswahl | M / Umschalt+M | Klick ohne Ziehen hebt die Auswahl auf |
| Freihandauswahl (Lasso) / Polygonauswahl | S / Umschalt+S | Polygon: Klicks setzen Ecken, Doppelklick oder Enter schließt |
| Zauberstab / Farbbereich | W / Umschalt+W | Toleranz in %, Quelle Ebene oder Bild |
| Ausgewählte Pixel verschieben | V | ohne Auswahl die ganze Ebene; Pfeiltasten 1 px, mit Umschalt 10 px |
| Zuschneiden | C | Rechteck ziehen, Enter oder Doppelklick schneidet zu |
| Pinsel | B | Größe, Härte, Deckkraft, Kantenglättung |
| Bleistift | P | harte, nicht geglättete Pixel |
| Radierer | E | macht Pixel transparent |
| Freihandlinie / Linie | F / L | Linie: Umschalt rastet in 15°-Schritten ein |
| Strichmuster | Umschalt+L | gestrichelte Linie mit ziehbaren Endpunkten, siehe unten |
| Rechteck / gefülltes Rechteck | R / Umschalt+R | Umschalt beim Ziehen: Quadrat |
| Ellipse / gefüllte Ellipse | O / Umschalt+O | Umschalt beim Ziehen: Kreis |
| Farbeimer | G | Toleranz, zusammenhängend oder ganzes Bild |
| Verlauf | Umschalt+G | linear oder radial, von Vorder- zu Hintergrundfarbe |
| Pipette | I | links: Vordergrund-, rechts: Hintergrundfarbe |
| Hand / Zoom | H / Z | Zoom: Klick vergrößert, Rechtsklick oder Alt+Klick verkleinert |

**Farben:** Klick auf ein Farbfeld öffnet den Farbdialog (Rot, Grün, Blau, Deckkraft, Hex wie `#FF8800` oder
`#FF880080`). **X** tauscht die Farben, **D** setzt Schwarz/Weiß.

Gesperrte Ebenen und Gruppen werden nicht bemalt; die Statusleiste zeigt dann einen Hinweis.

### Strichmuster

Zeichnet eine gestrichelte Linie, die sich vor dem Übernehmen beliebig nachbearbeiten lässt.

1. **Linie aufziehen:** Maustaste am Startpunkt drücken und zum Endpunkt ziehen. Die Linie erscheint als Vorschau
   über dem Bild; die Ebene selbst bleibt unverändert, bis Sie „Anwenden“ wählen.
2. **Nachbearbeiten:**
   - Die beiden **Kontrollpunkte** (kleine Quadrate) lassen sich mit der Maus ziehen; die Linie selbst verschiebt
     beide Punkte gemeinsam. **Umschalt** beim Ziehen rastet den Winkel in 15°-Schritten ein.
   - **Pfeiltasten** verschieben die Linie um 1 px, mit Umschalt um 10 px.
   - Die Felder **X1, Y1, X2, Y2** zeigen die Punkte in Bildpixeln und lassen sich direkt bearbeiten (Enter übernimmt).
     Punkte dürfen außerhalb des Bildes liegen; gezeichnet wird nur innerhalb.
   - Daneben stehen **Länge, Winkel** (0° = nach rechts, 90° = nach oben), **Richtung** (Einheitsvektor in
     Bildkoordinaten), **Mitte** und die Anzahl der Striche.
3. **Parameter** (Optionsleiste, jeweils Feld und Schieberegler):

   | Parameter | Standard | Bereich |
   |---|---|---|
   | Linienstärke | 4 px | 1–500 px |
   | Strichlänge | 10 px | 1–5.000 px |
   | Strichabstand | 5 px | 0–5.000 px (0 = durchgezogene Linie) |
   | Farbe | Schwarz | Farbfeld (öffnet den Farbdialog) oder Hex-Feld `#RRGGBB` / `#RRGGBBAA` |
   | Deckkraft | 100 % | 0–100 % |
   | Kantenglättung | an | |

   Ein Muster wiederholt sich alle *Strichlänge + Strichabstand* Pixel: Bei 10 px und 5 px entstehen 10 px lange
   Striche mit 5 px Lücke. Der letzte Strich wird am Endpunkt gekürzt. Ungültige Eingaben werden auf den gültigen
   Bereich begrenzt bzw. (Hex) verworfen.
4. **Anwenden** (Schaltfläche oder **Enter**) überträgt das Muster auf die aktive Ebene – begrenzt auf eine aktive
   Auswahl und die Bildgrenzen. Das ist ein Schritt für Rückgängig (Strg+Z) und Wiederholen (Strg+Y).
   **Abbrechen** (Schaltfläche oder **Esc**) verwirft die Linie, **Zurücksetzen** stellt die Standardparameter wieder
   her (die Punkte bleiben).

Ein Klick neben Linie und Kontrollpunkte übernimmt die aktuelle Linie und beginnt eine neue. Auch ein Werkzeug- oder
Dokumentwechsel und Rückgängig übernehmen eine laufende Linie zuerst (wie bei allen Werkzeugen) – es geht nichts
unbemerkt verloren. Im Modus „Maske bearbeiten“ wirkt das Muster auf die Maske.

## Auswahl

Bei Auswahlwerkzeugen gilt: **Umschalt** fügt hinzu, **Alt** zieht ab, **Umschalt+Alt** bildet die Schnittmenge
(oder Modus in der Optionsleiste). Alle Malwerkzeuge wirken nur innerhalb der Auswahl.

| Befehl (Menü Bearbeiten) | Kürzel |
|---|---|
| Alles auswählen / Auswahl aufheben / Auswahl umkehren | Strg+A / Strg+D / Strg+I |
| Auswahl erweitern … / verkleinern … | – (Radius in Pixeln) |
| Auswahl aus Ebenentransparenz | Menü Ebenen |

## Bearbeiten und Rückgängig

| Befehl | Kürzel |
|---|---|
| Rückgängig / Wiederholen | Strg+Z / Strg+Y |
| Ausschneiden / Kopieren / Einfügen | Strg+X / Strg+C / Strg+V |
| Löschen (ausgewählte Pixel transparent) | Entf |

Jede Bearbeitung – Malen, Auswahl, Ebenen, Bildgröße – lässt sich rückgängig machen; die Statusleiste nennt den
Schritt („Rückgängig: Pinsel“). Je Dokument werden bis zu 100 Schritte gespeichert. **Einfügen** legt eine neue Ebene
an (an der ursprünglichen Stelle, wenn das Bild aus ORBIT Studio kopiert wurde), wählt sie aus und aktiviert das
Verschieben-Werkzeug. Kopiert wird mit Transparenz.

## Bild

| Befehl (Menü Bild) | Kürzel |
|---|---|
| Bildgröße … (Seitenverhältnis optional fest) | Strg+R |
| Leinwandgröße … (mit Ausrichtung) | Strg+Umschalt+R |
| Um 90° im / gegen den Uhrzeigersinn drehen | Strg+H / Strg+Umschalt+H |
| Um 180° drehen, waagerecht/senkrecht spiegeln | – |
| Auf Auswahl zuschneiden | Strg+Umschalt+X |

## Ebenen

Rechts steht das Ebenen-Panel. Die oberste Zeile ist die oberste Ebene. Ein Klick wählt die **aktive Ebene**,
auf die sich alle Befehle im Menü **Ebenen** beziehen.

| In der Zeile | Bedeutung |
|---|---|
| Kästchen links | Sichtbarkeit ein/aus |
| Name (fett = Gruppe), darunter Deckkraft und Mischmodus | Doppelklick öffnet die Ebeneneigenschaften |
| „M“ | Ebene hat eine Maske (blass = deaktiviert) |
| 🔒-Kästchen | Sperre: nicht bemalen, löschen, verschieben, zusammenführen |

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

**Masken:** Ebenen › Maske hinzufügen legt eine weiße Maske an (alles sichtbar). Mit **Ebenen › Maske bearbeiten**
malen alle Malwerkzeuge in die Maske statt in die Pixel: Schwarz verbirgt, Weiß zeigt, Grau macht teilweise
durchsichtig; der Radierer öffnet die Maske wieder. Außerdem: Maske umkehren, aktiv/inaktiv schalten, anwenden
(in die Pixel einrechnen) oder löschen.

**Ebene spiegeln / transformieren:** Ebenen › Ebene waagerecht/senkrecht spiegeln; „Ebene transformieren …“ dreht
(Grad) und skaliert (%) die aktive Ebene um die Bildmitte.

**Importieren** (Datei › Importieren, Strg+Umschalt+O) fügt Bilddateien als neue Ebenen ein (oben links ausgerichtet;
zu große Bilder werden beschnitten).

Alle Ebenenänderungen lassen sich mit Strg+Z rückgängig machen. Damit Ebenen über das Schließen hinaus erhalten
bleiben, das Bild als Projekt speichern (siehe „Speichern als Projekt“).

## Speichern als Projekt (.orbit)

Ein **ORBIT-Projekt** enthält alles, was zum Weiterarbeiten nötig ist: Bildgröße und Auflösung, alle Ebenen und
Gruppen mit Namen, Sichtbarkeit, Deckkraft, Mischmodus und Sperre, Masken (auch deaktivierte), die aktive Ebene und
die Pixel – verlustfrei. Nicht gespeichert werden die Auswahl, Zoom und Ansicht sowie die Rückgängig-Liste.

- **Datei › Speichern** (Strg+S): speichert in die Projektdatei. Hat das Bild noch keine (neues Bild oder geöffnete
  PNG/JPEG …), wird wie bei „Speichern unter“ nach dem Ziel gefragt.
- **Datei › Speichern unter** (Strg+Umschalt+S): neue Projektdatei wählen; der Tab heißt danach wie die Datei.
- **Datei › Kopie speichern**: schreibt eine Kopie; das geöffnete Bild bleibt mit seiner bisherigen Datei verbunden
  und behält seine Änderungsmarkierung.
- Ein Sternchen (*) im Tab und in der Titelzeile zeigt ungespeicherte Änderungen. Wer mit Rückgängig/Wiederholen
  genau zum gespeicherten Stand zurückkehrt, sieht das Sternchen verschwinden.
- Gespeichert wird im Hintergrund; währenddessen sind Bearbeitungen kurz gesperrt. Es wird zuerst eine temporäre
  Datei geschrieben und geprüft und erst dann die alte Datei ersetzt – bricht das Speichern ab (Fehler, voller
  Datenträger, schreibgeschützte Datei), bleibt die vorhandene Datei unverändert.
- **Exportieren** (PNG, JPEG …) ist kein Speichern: Dabei werden die Ebenen zu einem Bild zusammengeführt.

Beim **Schließen** eines geänderten Bildes – auch über „Alle schließen“ oder beim Beenden – fragt ORBIT Studio:
**Speichern** (speichert und schließt), **Nicht speichern** (verwirft die Änderungen) oder **Abbrechen** (nichts wird
geschlossen). Bei mehreren geänderten Bildern werden zuerst alle Fragen gestellt; ein „Abbrechen“ lässt alle offen.

Beschädigte oder veränderte Projektdateien, Dateien, die kein ORBIT-Projekt sind, zu große Projekte und Projekte aus
einer neueren Programmversion werden mit einer Meldung abgelehnt; die Datei selbst wird dabei nicht verändert.

## Farben und Effekte

Farbanpassungen und Effekte wirken auf die **aktive Ebene**, bei einer Auswahl nur auf den ausgewählten Bereich
(weiche Auswahlränder anteilig). Gesperrte Ebenen und Gruppen können nicht bearbeitet werden; die Befehle sind dann
ausgegraut.

Befehle mit „…“ öffnen einen Dialog: je Wert ein Schieberegler und ein Zahlenfeld (Enter übernimmt; zu große oder zu
kleine Werte werden auf den erlaubten Bereich begrenzt, ungültige Eingaben verworfen), rechts eine **Vorschau** der
aktiven Ebene, die sich bei jeder Änderung neu berechnet. Bei großen Bildern ist die Vorschau verkleinert; Radien
und Abstände werden dafür mitskaliert. **Original zeigen** blendet zum Vergleich den Zustand vorher ein,
**Zurücksetzen** stellt die Standardwerte her. Der Dialog merkt sich die zuletzt bestätigten Werte.

Nach „OK“ wird der Effekt im Hintergrund auf die ganze Auflösung angewendet; die Statusleiste zeigt den Fortschritt.
**Esc** bricht die Berechnung ab – das Bild bleibt dann unverändert. Jede Anwendung ist ein Schritt für Rückgängig
(Strg+Z) und Wiederholen (Strg+Y).

| Menü | Befehl | Taste | Werte |
|---|---|---|---|
| Farben | Helligkeit/Kontrast … | | Helligkeit −100–100 %, Kontrast −100–100 % |
| Farben | Farbton/Sättigung … | Strg+U | Farbton −180–180°, Sättigung −100–100 % |
| Farben | Tonwertkorrektur … | Strg+L | Eingabe-Schwarz/-Weiß, Gamma 0,1–10, Ausgabe-Schwarz/-Weiß |
| Farben | Farbkanäle anpassen … | | Rot, Grün, Blau je 0–200 % |
| Farben | Schwarzweiß | Strg+Umschalt+B | – |
| Farben | Invertieren | Strg+Umschalt+I | – |
| Farben | Sepia … | | Intensität 0–100 % |
| Effekte | Gaußscher Weichzeichner … | | Radius 0–200 px |
| Effekte | Einfacher Weichzeichner … | | Radius 0–200 px (Mittelwert eines Quadrats) |
| Effekte | Schärfen … | | Stärke 0–500 %, Radius 0,5–50 px |
| Effekte | Rauschen hinzufügen … | | Stärke 0–100, Farbanteil 0–100 %, Startwert |
| Effekte | Rauschreduktion … | | Radius 1–10 px, Stärke 0–100 % |
| Effekte | Kantenerkennung … | | Stärke 10–500 %, dunkle Kanten auf Weiß |
| Effekte | Schlagschatten … | | Versatz X/Y, Weichheit, Deckkraft, Schattenfarbe |
| Effekte | Verpixeln … | | Zellgröße 2–200 px |

Tonwertkorrektur: Eingabe-Schwarz muss kleiner als Eingabe-Weiß sein, sonst ist „OK“ gesperrt und der Dialog nennt
den Grund. Farbkurven folgen mit dem Kurveneditor (M8).

## Exportieren

**Datei › Exportieren** (Strg+Umschalt+E): Format über die Dateiendung bzw. den Dateityp wählen –
PNG, JPEG, BMP oder WebP. Alle sichtbaren Ebenen werden zu einem Bild zusammengesetzt; bei mehreren Ebenen
erscheint vorher ein Hinweis. Die Ebenen im Dokument bleiben dabei erhalten.

- PNG und BMP sind verlustfrei und behalten Transparenz.
- JPEG kann keine Transparenz speichern: transparente Bereiche werden weiß gefüllt (mit Rückfrage).
- JPEG/WebP werden mit Qualität 92 gespeichert.
- Eine vorhandene Datei wird erst ersetzt, wenn die neue vollständig geschrieben ist.

## Dokumente schließen

Tab-Schaltfläche ✕, **Datei › Schließen** (Strg+W) oder **Alle schließen** (Strg+Umschalt+W). Bei ungespeicherten
Änderungen folgt die Rückfrage „Speichern / Nicht speichern / Abbrechen“ (siehe „Speichern als Projekt“).

## Tastenkürzel (Datei und Ansicht)

Werkzeug-, Bearbeiten-, Bild- und Ebenenkürzel stehen in den jeweiligen Abschnitten oben.

| Befehl | Kürzel |
|---|---|
| Neu | Strg+N |
| Öffnen | Strg+O |
| Speichern / Speichern unter | Strg+S / Strg+Umschalt+S |
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
