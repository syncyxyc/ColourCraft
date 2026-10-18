# Teststrategie und Ergebnisse

## Teststufen

| Stufe | Projekt / Werkzeug | Stand |
|---|---|---|
| Unit-Tests Domäne (Dokument, Pixelpuffer, Farbe, Transformationen) | `tests/OrbitStudio.Domain.Tests` | aktiv |
| Unit-Tests Konfiguration | `tests/OrbitStudio.Configuration.Tests` | aktiv |
| Unit-Tests Anwendung (Arbeitsbereich, Befehle) | `tests/OrbitStudio.Application.Tests` | aktiv |
| Codec-Tests (Referenzbytes, Rundreisen, beschädigte/übergroße Dateien) | `tests/OrbitStudio.Imaging.Tests` | aktiv |
| Ansichtstransformation und **Rendering-Referenztests** (Komposition, Mischmodi, Masken, Gruppen) | `tests/OrbitStudio.Rendering.Tests` | aktiv |
| Ebenenoperationen | `tests/OrbitStudio.Layers.Tests` | aktiv |
| Historie (Deltas, Schnappschüsse, Grenzen) | `tests/OrbitStudio.History.Tests` | aktiv |
| Werkzeuge (Zeichnen, Auswahl, Füllung, Transformation, Ansicht) | `tests/OrbitStudio.Tools.Tests` | aktiv |
| **Segmentberechnung und Werkzeug Strichmuster** | `tests/OrbitStudio.Tools.Tests/DashPatternTests.cs` | aktiv |
| Integration je Werkzeuggruppe | `tests/OrbitStudio.Integration.Tests/ToolGroupWorkflowTests.cs` | aktiv |
| Integration Dateien (echte Dateien, atomares Schreiben, Fehler) | `tests/OrbitStudio.Integration.Tests` | aktiv |
| Architekturtest | `tests/OrbitStudio.Architecture.Tests` | aktiv |
| UI-Smoke-Test | `OrbitStudio.exe --smoke-test <Datei>` (über `build/test.ps1`) | aktiv |
| UI-Eingabetest (echte Maus/Tastatur) | `build/ui-input-test.ps1` bzw. `test.ps1 -WithInput` | optional, braucht ungenutzte Sitzung |
| Integrationstest Pflicht-Workflow (Abschnitt 13) | folgt in M7 | nicht ausgeführt |
| Performance (BenchmarkDotNet) | folgt in M11 | nicht gemessen |

Ausführen: `powershell -ExecutionPolicy Bypass -File build/test.ps1` (optional `-WithInput`).
Ergebnisse: `artifacts/testresults/*.trx`, `smoke-test.txt`, `smoke-test.png`, `smoke-test-zoom.png`,
`ui-input-test.txt`, `ui-input-test.png`.

### Toleranzen

| Prüfung | Toleranz | Begründung |
|---|---|---|
| PNG, BMP mit deckenden Pixeln | exakt (byteweise) | verlustfreie Formate |
| PNG, BMP mit Transparenz | Alpha exakt, Farbkanal ±1 | Rundung vormultipliert ↔ gerade (Formate speichern gerades Alpha) |
| JPEG (einfarbig, Qualität 95) | ±3 je Kanal | verlustbehaftet |
| WebP (einfarbig, Qualität 95) | ±6 je Kanal | verlustbehaftet |
| JPEG/WebP-Export im Smoke-Test (Verlauf mit harten Kanten) | mittlere Abweichung ≤ 3 | an Farbkanten weichen einzelne Pixel stark ab (Farbunterabtastung) |
| Ansichtstransformation | 1e-9 (Rundreise 1e-6) | Fließkomma |
| Komposition (Rendering-Referenztests) | **exakt** (byteweise) | Rechenweg festgelegt (double, kaufmännische Rundung); Erwartungswerte von Hand berechnet |
| Maske anwenden / Zusammenführen | exakt bzw. Aussehen byteweise gleich | gleiche Rechenvorschrift wie die Komposition |

### UI-Smoke-Test

Startet das echte Hauptfenster mit isolierten Einstellungen und Testdateien in `%TEMP%` (die
Nutzereinstellungen bleiben unberührt) und prüft automatisch: Fenster, Menüs aus der Registry, keine
Shortcut-Konflikte, Befehlszustände, Dokumente anlegen (weiße/transparente Pixel), Tabwechsel inkl.
Zoom je Dokument, Öffnen einer PNG mit byteweisem Pixelvergleich, Bitmap = Pixelpuffer, „Zuletzt verwendet“,
Zoomstufen bis 800 %, Verschieben, Pixelraster an/aus, Bild anpassen, **Pixel unverändert nach Zoom und
Verschieben**, Export PNG/BMP/WebP/JPEG mit erneutem Einlesen, beschädigte und fehlende Dateien,
Statusleiste, Vollbild. Exitcode 0 = bestanden.

### UI-Eingabetest

Startet `OrbitStudio.exe --data-dir <Temp> <Testbild>` und sendet echte Eingaben per `SendInput`:
3× Mausrad hoch, Ziehen mit mittlerer Maustaste, Mausrad runter, Strg+0, Strg+B. Zoom und Scrollposition
werden per UI Automation aus der Oberfläche gelesen. **Sicherheitsregel:** Vor jeder Eingabe wird geprüft,
dass ORBIT Studio das Vordergrundfenster ist; sonst bricht der Test ab, ohne Eingaben zu senden.

### Rendering-Referenzwerte (Auszug)

Grau 128 (oben, deckend) über Grau 200 (unten, deckend): Normal 128, Multiplizieren 100 (200·128/255 = 100,39),
Bildschirm 228, Addieren 255, Negativ 182, Differenz 72, Abdunkeln 128, Aufhellen 200, Überlagern 200
(255 − 2·127·55/255 = 200,22). Über Grau 100: Überlagern 100, Negativ 228, Differenz 28.
Grau 128 mit 40 % über Weiß: 204 (128·0,4 + 255·0,6 = 204,2). Multiplizieren über halbtransparentem
Hintergrund (gerade 200, α 128): 114. Gruppe (50 %) mit Multiplizieren-Inhalt über transparent: (64, 64, 64, 128).
Weitere Fälle siehe `tests/OrbitStudio.Rendering.Tests/LayerCompositorTests.cs`.

### Werkzeug-Referenzwerte (Auszug)

Bleistift 1 px: Klick in Pixel (5,7) setzt genau dieses Pixel. Gefülltes Rechteck aus (4,2;5,4)–(9,6;8,7): Ecken auf
(4,5)–(10,9) gerundet, exakt 6 × 4 schwarze Pixel, keine Kantenglättungsreste. Pinsel 50 % schwarz über Weiß, Hin-
und Rückweg im selben Zug: 128 (keine Anhäufung). Farbbereich mit Toleranz 10 %: Abweichung 25 (9,8 %) ausgewählt,
30 (11,8 %) nicht. Erweitern um 2 px: Abstand √2 ausgewählt, √5 nicht. Bildgröße 2 schwarze + 2 weiße Spalten → 1:
exakt Schwarz/Weiß; Rot neben Transparent verkleinert → (R 128, A 128), also unvermindertes Rot bei halber Deckkraft.

### Strichmuster-Referenzwerte (Auszug)

Linie (0; 12) → (30; 12), Stärke 4, Strich 10, Abstand 5: exakt die Pixel x 0–9 und 15–24 in den Zeilen 10–13 schwarz,
alle anderen unverändert (auch mit Kantenglättung, weil alle Kanten auf Pixelgrenzen liegen). Strich 4 / Abstand 2 /
Stärke 2: x mod 6 < 4 in den Zeilen 11–12. Abstand 0: durchgehend ohne Naht. Schwarz 50 % über Weiß: 128.
Rot mit Alpha 128 über Weiß: (R 255, G 127, B 127). Segmente: 0–100 mit 10/5 → [0,10] [15,25] … [90,100];
0–95 → letzter [90,95]; 0–30 → kein Strich bei 30; 37 px bei 4/3 → 5 volle Striche + 2 px, für alle Winkel in
5°-Schritten; 10.000.000 px bei 1/1 → 5.000.000 Striche ohne Speicherliste.

## Ergebnisse

### 18.10.2026 – M5 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

| Testprojekt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|
| OrbitStudio.Domain.Tests | 74 | 0 | 0 |
| OrbitStudio.Configuration.Tests | 19 | 0 | 0 |
| OrbitStudio.Application.Tests | 24 | 0 | 0 |
| OrbitStudio.Imaging.Tests | 27 | 0 | 0 |
| OrbitStudio.Rendering.Tests | 60 | 0 | 0 |
| OrbitStudio.Layers.Tests | 23 | 0 | 0 |
| OrbitStudio.History.Tests | 12 | 0 | 0 |
| OrbitStudio.Tools.Tests | 106 | 0 | 0 |
| OrbitStudio.Integration.Tests | 21 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 24 | 0 | 0 |
| **Summe** | **390** | **0** | **0** |

Neu: 24 Testfälle der Segmentberechnung (`DashLineTests`, inkl. Theorien je Hauptrichtung und ungültige Parameter),
32 Werkzeugtests (`DashPatternToolTests`), 1 Integrationsablauf (Strichmuster → Rückgängig/Wiederholen → PNG-Export →
Wiedereinlesen). Beim ersten Lauf schlugen 2 Werkzeugtests fehl: Ein knapp neben dem Mittelpunkt gegriffener
Kontrollpunkt sprang auf die Mausposition. Das war ein **Fehler im Produktcode** (Bedienung); behoben, indem der
Greifversatz erhalten bleibt (E-036). Ein weiterer Test erwartete noch 22 statt 23 Werkzeuge (Testanpassung).

UI-Smoke-Test: Debug 135 von 135 bestanden, veröffentlichte EXE (157,7 MB) 135 von 135 bestanden, jeweils Exitcode 0.
Neu sind 18 Strichmuster-Prüfungen (siehe Abnahme unten). Sichtprüfung `smoke-test-strichmuster.png`: rote Striche
mit Hilfslinie und beiden Kontrollpunkten bei 200 %, Optionsleiste mit allen 14 Optionen (umgebrochen).

UI-Eingabetest: um die Strichmuster-Abnahme mit echter Maus und Tastatur erweitert (Linie aufziehen, beide
Kontrollpunkte ziehen, Parameter tippen + Enter, Vorschau-Pixel am Bildschirm, „Anwenden“ klicken, Strg+Z, Strg+Y);
Skript syntaktisch geprüft, **nicht ausgeführt** (Sitzung aktiv benutzt, der Test bewegt den Mauszeiger).

### 18.10.2026 – M4 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

| Testprojekt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|
| OrbitStudio.Domain.Tests | 74 | 0 | 0 |
| OrbitStudio.Configuration.Tests | 19 | 0 | 0 |
| OrbitStudio.Application.Tests | 24 | 0 | 0 |
| OrbitStudio.Imaging.Tests | 27 | 0 | 0 |
| OrbitStudio.Rendering.Tests | 60 | 0 | 0 |
| OrbitStudio.Layers.Tests | 23 | 0 | 0 |
| OrbitStudio.History.Tests | 12 | 0 | 0 |
| OrbitStudio.Tools.Tests | 50 | 0 | 0 |
| OrbitStudio.Integration.Tests | 20 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 24 | 0 | 0 |
| **Summe** | **333** | **0** | **0** |

Beim ersten Lauf der Architekturtests schlugen 3 Fälle fehl, weil die Projektreferenz auf `OrbitStudio.Tools` im
Testprojekt fehlte (Testaufbau, nicht Produktcode). Nach Korrektur: alle bestanden.

Integrationsabläufe je Werkzeuggruppe: Zeichnen (neue Ebene, Rechteck, Linie, Export/Wiedereinlesen, alles rückgängig
und wiederholt), Auswahl (Zauberstab auf importierter Ebene, Löschen, Umkehren, rückgängig), Füllung/Farbe (Pipette aus
dem Bild, Farbeimer auf anderer Ebene, Verlauf), Transformation (Auswahl verschieben, Zuschneiden, Bildgröße, Drehen,
Kette rückgängig), Ansicht (Zoom/Hand ohne Pixel- oder Historienänderung).

UI-Smoke-Test (Debug und veröffentlichte EXE, 157,6 MB): je 117 von 117 bestanden, Exitcode 0. Neu in M4: Werkzeugleiste
vollständig sichtbar (alle 22), Farbfelder mit Inhalt, Werkzeugwahl per Befehl, Bleistift/Pinsel über den
Werkzeug-Controller mit exakten Pixeln, Rückgängig/Wiederholen über die Befehle, Farben tauschen/zurücksetzen,
Rechteckauswahl begrenzt das Malen, Kopieren/Einfügen (Zwischenablage im Speicher), Verschieben, Auswahl verkleinern
(Dialog), Entf, Zuschneiden auf Auswahl, Drehen, Bildgröße, drei Schritte rückgängig, Pipette, Farbeimer, Maske bemalen,
Hinweis bei gesperrter Ebene, Escape bricht Formvorschau exakt ab, Löschen/Rückgängig/Wiederholen von Ebenen.

Bei der Sichtprüfung des ersten M4-Bildschirmfotos fielen zwei Darstellungsfehler auf (nur eine Werkzeugspalte,
untere Werkzeuge abgeschnitten; Farbfelder ohne Inhalt). Beide wurden behoben und sind seither Smoke-Prüfungen.

UI-Eingabetest: um einen Pinselstrich mit echter Maus + Strg+Z erweitert; in M4 **nicht ausgeführt** (Sitzung aktiv
benutzt, der Test bewegt den Mauszeiger).

### 18.10.2026 – M3 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

| Testprojekt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|
| OrbitStudio.Domain.Tests | 74 | 0 | 0 |
| OrbitStudio.Configuration.Tests | 19 | 0 | 0 |
| OrbitStudio.Application.Tests | 24 | 0 | 0 |
| OrbitStudio.Imaging.Tests | 27 | 0 | 0 |
| OrbitStudio.Rendering.Tests | 60 | 0 | 0 |
| OrbitStudio.Layers.Tests | 23 | 0 | 0 |
| OrbitStudio.Integration.Tests | 15 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 18 | 0 | 0 |
| **Summe** | **260** | **0** | **0** |

Beim ersten Lauf schlugen 2 Tests fehl – beides Fehler in den Tests selbst (eine Ebene wurde zweimal in einen
Stapel eingefügt; fehlendes `using`), nicht im Produktcode. Nach Korrektur: alle bestanden.

UI-Smoke-Test (Debug und veröffentlichte EXE, 156,9 MB): je 79 von 79 bestanden, Exitcode 0.
Neu in M3: Neu/Öffnen/Importieren/Exportieren laufen über die echten Registry-Befehle mit geskripteten Dialogen
(`SmokeDialogService`); Ebenen anlegen, Mischmodi und Deckkraft über das Panel mit exakten Pixelwerten,
Sichtbarkeit, Masken, Eigenschaftsdialog, Duplizieren, Verschieben, Löschen mit Rückfrage, Sperre, Gruppen,
Zusammenführen und Reduzieren mit unverändertem Aussehen, Import mit Beschnittmeldung, Export-Warnung bei
mehreren Ebenen, Panel-Markierung = aktive Ebene, Rückfrage bei ungespeicherten Änderungen.

UI-Eingabetest: in M3 nicht erneut ausgeführt (Sitzung wurde aktiv benutzt). Die Leinwand-Eingabe ist
gegenüber M2 unverändert.

### 18.10.2026 – M2 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

| Testprojekt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|
| OrbitStudio.Domain.Tests | 59 | 0 | 0 |
| OrbitStudio.Configuration.Tests | 19 | 0 | 0 |
| OrbitStudio.Application.Tests | 24 | 0 | 0 |
| OrbitStudio.Imaging.Tests | 27 | 0 | 0 |
| OrbitStudio.Rendering.Tests | 21 | 0 | 0 |
| OrbitStudio.Integration.Tests | 15 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 16 | 0 | 0 |
| **Summe** | **181** | **0** | **0** |

UI-Smoke-Test (Debug): 42 von 42 Prüfungen bestanden, Exitcode 0. Gemessene mittlere Abweichung nach
Export/Einlesen: WebP 0,61, JPEG 0,39 (Qualität 92).
UI-Smoke-Test (veröffentlichte EXE, 156,6 MB): 42 von 42 bestanden, Exitcode 0.

UI-Eingabetest:
- 2 Läufe vollständig bestanden: Zoom beim Öffnen 62,1 % (Bild 1600×1000 eingepasst), 3× Mausrad → 150 %,
  Ziehen um 200 Gerätepixel → horizontale Scrollposition 619 → 819, Mausrad runter → 100 %,
  Strg+0 von 200 % → 100 %, Strg+B → 62,1 %.
- Ein früherer Lauf (vor Einführung der Vordergrundprüfung) schlug fehl: Das Fenster war nicht im
  Vordergrund, Eingaben gingen verloren bzw. an ein anderes Fenster. Daraufhin wurde die
  Sicherheitsregel eingebaut.
- 3 weitere Läufe brachen danach korrekt mit „Fenster nicht im Vordergrund“ ab, ohne Eingaben zu senden
  (der Rechner wurde zu dem Zeitpunkt aktiv benutzt). Der Test ist deshalb optional und nicht Teil
  des Standardlaufs.

Sichtprüfung der Bildschirmfotos: Geöffnetes Testbild eingepasst und zentriert; bei 800 % scharfe Pixel
mit sichtbarem 1-px-Pixelraster; Statusleiste mit Größe, Cursorposition und Zoom.

Nicht geprüft: Darstellung bei Windows-Skalierung 150 %/200 % und Monitorwechsel am echten Bildschirm
(nur Unit-Tests der Transformation mit DpiScale 1,25/1,5/2,0).

### 18.10.2026 – M1

64 Unit-/Architekturtests und 18 Smoke-Prüfungen bestanden (Details im Git-Verlauf dieser Datei).

## Abnahme Strichmuster-Werkzeug (Abschnitt 1.1)

**Kriterium:** Der Nutzer verschiebt zwei Kontrollpunkte auf der Leinwand, die Vorschau passt sich an, alle Parameter
wirken, das Ergebnis wird auf die aktive Ebene übernommen, und die Übernahme lässt sich mit Rückgängig zurücknehmen und
mit Wiederholen wiederherstellen.

**Ausgeführt am 18.10.2026** im echten Hauptfenster (`OrbitStudio.exe --smoke-test`, Abschnitt `CheckDashPattern`),
mit dem Debug-Build und mit der veröffentlichten Single-File-EXE – beide bestanden. Dokument 200 × 120 weiß, Zoom 200 %.
Zeigereingaben gehen über den `ToolController` mit den Dokument- **und** den dazu passenden Viewport-Koordinaten der
aktuellen Ansicht (gleicher Weg wie die Leinwand; die Trefferprüfung der Kontrollpunkte rechnet in Viewport-Pixeln).
Bedienelemente der Optionsleiste werden im visuellen Baum gesucht und bedient (Textfelder, Schaltflächen).

| Schritt | Ergebnis |
|---|---|
| Werkzeug per Befehl (Umschalt+L) wählen; Optionsleiste zeigt 14 Optionen; ohne Linie sind X1–Y2 und „Anwenden“ gesperrt | bestanden |
| Linie (20; 30) → (100; 30) aufziehen, **Startpunkt** nach (10; 60), **Endpunkt** nach (130; 60) ziehen | bestanden (Punkte exakt) |
| Felder X1/Y1/X2/Y2 zeigen 10 / 60 / 130 / 60 | bestanden |
| Anzeige „Länge 120 px · Winkel 0° · Richtung (1; 0) · Mitte (70; 60) · 8 Striche“ | bestanden |
| Ebene während der Vorschau unverändert, kein Historieneintrag | bestanden |
| Gerenderte Leinwand: Strichmitte schwarz (#FF000000), Lückenmitte weiß (#FFFFFFFF) → Vorschau sichtbar und an der neuen Position | bestanden |
| Feld X2 = 150 (Eingabe) → Endpunkt (150; 60) | bestanden |
| Linienstärke 0 → auf 1 begrenzt, Feld zeigt 1 | bestanden |
| Hex-Feld #00FF00 übernommen, „kein Hex“ abgewiesen | bestanden |
| Farbfeld → Farbdialog (geskriptet: Rot) → Farbe Rot | bestanden |
| Stärke 6, Strich 12, Abstand 8, „Anwenden“ (Schaltfläche): Pixel (15; 57) und (15; 62) rot, (15; 56) weiß, (25; 60) weiß (Lücke), (35; 60) rot, (150; 60) weiß; Überlagerung entfernt | bestanden |
| Komposit und Bildschirm-Bitmap zeigen das Muster | bestanden |
| Bearbeiten › Rückgängig → Ebene byteweise wie vorher; Statusleiste „Rückgängig: Strichmuster“ | bestanden |
| Bearbeiten › Wiederholen → Ebene byteweise wie nach dem Anwenden | bestanden |
| Neue Linie, Esc → verworfen, Ebene unverändert | bestanden |
| „Zurücksetzen“ → 4 / 10 / 5 / Schwarz / 100 % (auch im Feld) | bestanden |
| Werkzeugwechsel mit offener Linie → Linie übernommen | bestanden |

Zusätzlich ohne Oberfläche: Integrationstest `DashPattern_MoveBothControlPoints_Apply_UndoRedo_ExportReopen`
(bei 200 % Zoom beide Punkte ziehen, Parameter, Anwenden, Rückgängig/Wiederholen, PNG-Export und byteweises
Wiedereinlesen) und Werkzeugtests für Zoom-unabhängigen Fangradius (400 %), reduzierte Vorschau (50 %), Auswahl- und
Bildgrenzen, Maske, Pfeiltasten, Umschalt-Einrasten, gesperrte Ebene, „Vorschau = Ergebnis“ (byteweise).

**Nicht ausgeführt:** Dieselbe Abnahme mit echten Windows-Mausereignissen (`build/ui-input-test.ps1`, Schritt 7).
Das Skript ist vorbereitet; es bewegt den Mauszeiger und darf nur in einer unbenutzten Sitzung laufen.
Darstellung bei Windows-Skalierung 150 %/200 % nicht am Bildschirm geprüft (Trefferprüfung rechnet mit der
DPI-Skalierung, siehe E-037).
