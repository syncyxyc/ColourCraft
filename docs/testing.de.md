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

## Ergebnisse

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

Nicht ausgeführt – das Werkzeug wird in M5 umgesetzt.
