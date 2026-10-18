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
| **Effekte: Referenzwerte, Engine, Registry, Vorschau, Abbruch** | `tests/OrbitStudio.Effects.Tests` | aktiv |
| Integration je Werkzeuggruppe | `tests/OrbitStudio.Integration.Tests/ToolGroupWorkflowTests.cs` | aktiv |
| Integration Dateien (echte Dateien, atomares Schreiben, Fehler) | `tests/OrbitStudio.Integration.Tests` | aktiv |
| Architekturtest | `tests/OrbitStudio.Architecture.Tests` | aktiv |
| UI-Smoke-Test | `OrbitStudio.exe --smoke-test <Datei>` (über `build/test.ps1`) | aktiv |
| UI-Eingabetest (echte Maus/Tastatur) | `build/ui-input-test.ps1` bzw. `test.ps1 -WithInput` | optional, braucht ungenutzte Sitzung |
| **Integrationstest Pflicht-Workflow (Abschnitt 13)** | `tests/OrbitStudio.Integration.Tests/MandatoryWorkflowTests.cs` | aktiv |
| **Projektformat: Rundreise, beschädigte/manipulierte/übergroße Dateien, Migration** | `tests/OrbitStudio.Storage.Tests` | aktiv |
| Projektdateien mit echten Dateien (sichere Ersetzung, Abbruch, Schreibschutz) | `tests/OrbitStudio.Integration.Tests/MandatoryWorkflowTests.cs` | aktiv |
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

### Effekt-Referenzwerte (Auszug)

Opakes Pixel (100, 150, 200): Helligkeit +20 % → (151, 201, 251); Kontrast +50 % (Faktor 2,25) → (66, 178, 255);
Kontrast −100 % → 128; Invertieren → (155, 105, 55); Schwarzweiß → 141 (140,75); Kanäle 50/200/0 % → (50, 255, 0);
Tonwert Eingang 50–200 → (85, 170, 255), mit Gamma 2 auf 100 → 147, Ausgabe 20–220 → 87; Helligkeit/Kontrast
+20/+50 auf 100 → 180. (10, 20, 30) Sepia → (25, 22, 17), 50 % → (17, 21, 24). Rot Farbton ±120° → Grün/Blau, 180° →
Cyan. (200, 100, 50) Sättigung −100 % → 125, +100 % → (250, 83, 0). Halbtransparent (50, 50, 50, 128) invertiert →
(78, 78, 78, 128). Gauß Radius 2 auf einem weißen Punkt: 0, 1, 14, 62, 102, 62, 14, 1, 0. Einfacher Weichzeichner
r = 1: 28 (255/9), r = 2: 10. Schärfen 50 %, Radius 2 an der Kante 100|150: 93 und 158. Sobel an einer harten Kante:
0, 255, 255, 0. Median entfernt einen Störpixel vollständig, mit 50 %: 178. Verpixeln 2 × 2: 75, 70, Randzelle 9.
Schlagschatten (3, 3): Deckkraft 100 % → Schwarz, 50 % → Alpha 128, Weichheit 2 → Alpha 41. Rauschen σ = 20:
Mittelwert 128 ± 0,5, Standardabweichung 20 ± 0,6 (40.000 Pixel).

| Prüfung | Toleranz | Begründung |
|---|---|---|
| Farbeffekte, einfacher Weichzeichner, Median, Verpixeln, Kanten, Schatten | exakt | Rechenweg festgelegt |
| Gauß gegen naive 2D-Faltung | ±1 | andere Summationsreihenfolge (separierbar) |
| Rauschen (Statistik) | Mittelwert ±0,5, σ ±0,6 | Zufallsstichprobe mit festem Startwert |

## Ergebnisse

### 18.10.2026 – M7 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

| Testprojekt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|
| OrbitStudio.Domain.Tests | 74 | 0 | 0 |
| OrbitStudio.Configuration.Tests | 19 | 0 | 0 |
| OrbitStudio.Application.Tests | 24 | 0 | 0 |
| OrbitStudio.Imaging.Tests | 27 | 0 | 0 |
| OrbitStudio.Rendering.Tests | 60 | 0 | 0 |
| OrbitStudio.Layers.Tests | 23 | 0 | 0 |
| OrbitStudio.History.Tests | 16 | 0 | 0 |
| OrbitStudio.Tools.Tests | 106 | 0 | 0 |
| OrbitStudio.Effects.Tests | 42 | 0 | 0 |
| **OrbitStudio.Storage.Tests** | **38** | 0 | 0 |
| OrbitStudio.Integration.Tests | 29 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 30 | 0 | 0 |
| **Summe** | **488** | **0** | **0** |

Neu:
- **Projektformat (38):** Rundreise mit Gruppen (verschachtelt), Masken (deaktiviert, Einzelwerte), Deckkraft,
  Mischmodi, Sperre, Sichtbarkeit, teiltransparenten vormultiplizierten Pixeln (byteweise), Metadaten, aktiver Ebene;
  Containerinhalt; Prüfung ohne Pixeldaten. Abgelehnt werden: keine ZIP-Datei, fehlendes Manifest, fremde
  Formatkennung, neuere Version, unlesbares Manifest (3 Fälle), fehlender Datenblock, falsche Länge, **manipulierte
  Pixel (Prüfsumme)**, abgeschnittene Datei, übergroße Abmessungen (2 Fälle, vor dem Anlegen der Puffer),
  überschrittene Speichergrenze, ungültige Werte (Breite, Auflösung, Deckkraft, Mischmodus auch als Zahl, Typ, leerer
  Name), doppelte IDs, unbekannte aktive Ebene, doppelt verwendeter Datenblock, zu tiefe Verschachtelung, leeres Projekt;
  unbekannte Zusatzfelder werden ignoriert; Abbruch beim Schreiben. **Migration:** Kette 1 → 2 → 3 mit Test-Migrationen,
  fehlender Schritt, ungültige/fehlende Versionsnummer.
- **Speicherpunkt (4):** neues Dokument, nach Speichern, verworfener Zweig, aus der Historie gefallener Punkt.
- **Pflicht-Workflow Abschnitt 13 (1, alle 14 Schritte):** Dokument erstellen, Ebene hinzufügen, zeichnen, Auswahl,
  Effekt (Invertieren in der Auswahl), rückgängig, wiederholen, Ebene umbenennen, als `.orbit` speichern, schließen,
  erneut öffnen, Pixel/Ebenen/Zustand prüfen (byteweise, inkl. Komposit), PNG exportieren, wieder einlesen und Pixel
  prüfen.
- **Projektdateien (6):** Ersetzen ohne Reste temporärer Dateien, Abbruch lässt die vorhandene Datei byteweise
  unverändert, fehlender Ordner, schreibgeschützte Zieldatei (unverändert), Öffnen-Fehler, Endungserkennung.
- 3 Architekturfälle für `OrbitStudio.Storage`.

Alle Unit-/Integrationstests bestanden im ersten Lauf. Der erste Smoke-Lauf prüfte versehentlich eine veraltete EXE
(`dotnet test` hatte die App nicht neu gebaut) – `test.ps1` baut die App seitdem vor dem Smoke-Test. Der folgende Lauf
fand einen **Fehler im Produktcode**: „Alle schließen“/Beenden schloss unveränderte Dokumente bereits, bevor bei einem
späteren Dokument „Abbrechen“ gewählt wurde. Behoben (E-049); danach alles bestanden.

UI-Smoke-Test: Debug 166 von 166, veröffentlichte EXE (158,1 MB) 166 von 166 bestanden, Exitcode 0. Neu sind 13
Projektprüfungen über die echten Befehle: Befehle aktiv; Strg+S ohne Pfad fragt nach dem Ziel und ergänzt `.orbit`
(Name, Pfad, Sternchen, Statusmeldung, Zuletzt verwendet); keine temporären Dateien; Rückgängig bis zum Speicherpunkt
entfernt das Sternchen; Strg+S mit Pfad ohne Dialog; Kopie speichern lässt Pfad/Name/Zustand; Schließen-Rückfrage
Abbrechen bzw. Speichern; Öffnen über Datei › Öffnen mit Ebenen, Namen, Pixeln, aktiver Ebene; kein zweiter Tab; Kopie
enthält den kopierten Stand; fremde Datei → deutsche Meldung; **Fenster schließen** mit Änderungen und „Abbrechen“
lässt Fenster und alle Dokumente offen.

UI-Eingabetest: für M7 nicht erweitert und nicht ausgeführt.

### 18.10.2026 – M6 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

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
| **OrbitStudio.Effects.Tests** | **42** | 0 | 0 |
| OrbitStudio.Integration.Tests | 22 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 27 | 0 | 0 |
| **Summe** | **436** | **0** | **0** |

Alle Tests bestanden im ersten Lauf. Neu: 42 Effekttests, 1 Integrationsablauf (Zeichnen, Auswahl, Effekt,
Rückgängig/Wiederholen, PNG-Export und Wiedereinlesen = Effekt-Teil des Pflicht-Workflows), 3 Architekturfälle für
`OrbitStudio.Effects` (erlaubte Referenzen, kein SkiaSharp, keine UI).

UI-Smoke-Test: Debug 153 von 153, veröffentlichte EXE (157,9 MB) 153 von 153 bestanden, Exitcode 0. Neu sind 18
Effektprüfungen: Befehle aktiv (Farbkurven noch geplant), Invertieren über den Menübefehl mit exakten Pixeln,
Bitmap = Komposit, Rückgängig/Wiederholen exakt, Helligkeit/Kontrast mit Dialogwerten exakt, Vorschau = Ergebnis bei
voller Auflösung, gemerkte Einstellungen, Dialog abbrechen ändert nichts, Weichzeichner nur in der Auswahl, **echter
Effektdialog** (Live-Vorschau folgt dem Schieberegler, Zahlenfeld begrenzt 9999 auf 200, Regelverstoß sperrt OK),
verkleinerte Vorschau bei 3000 × 2000 (Maßstab 0,12), Sperre während der Berechnung, Abbruch ohne Änderung und ohne
Historieneintrag, vollständige Berechnung auf 6 MP im Hintergrund, gesperrte Ebene deaktiviert die Befehle.
Bei der Sichtprüfung des ersten Dialog-Fotos war das Bild abgeschnitten: Fehler im Foto-Helfer des Smoke-Tests
(Außenabstand nicht berücksichtigt), nicht im Dialog; behoben.

UI-Eingabetest: für M6 nicht erweitert und nicht ausgeführt (siehe M5).

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
Skript syntaktisch geprüft. Ausführungsversuche am 18.10.2026 (4 Läufe), **nicht abgeschlossen**:
1. Abbruch vor der ersten Eingabe: Windows verweigerte die Aktivierung (Vordergrundsperre). → Aktivierung über
   Minimieren/Wiederherstellen und `SwitchToThisWindow` ergänzt (ohne Eingaben).
2. Aktivierung, Mausrad (51,6 % → 150 %), Ziehen mit der mittleren Maustaste, Mausrad runter bestanden; danach übernahm
   ein anderes Programm den Fokus. → Vor jeder Eingabe wird der Vordergrund geprüft und ohne Eingaben zurückgeholt; das
   Fenster, das den Fokus hatte, wird protokolliert.
3./4. Ein Programm im Vollbild (Videoanruf mit Bildschirmübertragung) holte sich wiederholt den Fokus; Schritte danach
   schlugen fehl oder brachen ab. Der Test wurde nicht weiter wiederholt, weil Eingaben in einer laufenden Übertragung
   nicht vertretbar sind. Ergebnis: Das Strichmuster mit echter Windows-Maus bleibt **nicht ausgeführt**.

Voraussetzung für einen Lauf: keine Programme, die sich in den Vordergrund holen (Anrufe, Bildschirmübertragung,
Overlays), Maus und Tastatur unberührt.

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
