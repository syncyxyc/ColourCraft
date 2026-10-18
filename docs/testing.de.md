# Teststrategie und Ergebnisse

## Teststufen

| Stufe | Projekt / Werkzeug | Stand |
|---|---|---|
| Unit-Tests Domäne | `tests/OrbitStudio.Domain.Tests` | aktiv |
| Unit-Tests Konfiguration | `tests/OrbitStudio.Configuration.Tests` | aktiv |
| Unit-Tests Anwendung | `tests/OrbitStudio.Application.Tests` | aktiv |
| Architekturtest | `tests/OrbitStudio.Architecture.Tests` | aktiv |
| UI-Smoke-Test | `OrbitStudio.exe --smoke-test <Datei>` (über `build/test.ps1`) | aktiv |
| Rendering-Referenztests | folgt ab M2/M3 | nicht ausgeführt |
| Integrationstest Pflicht-Workflow (Abschnitt 13) | folgt in M7 | nicht ausgeführt |
| Performance (BenchmarkDotNet) | folgt in M11 | nicht gemessen |

Ausführen: `powershell -ExecutionPolicy Bypass -File build/test.ps1`
Ergebnisse: `artifacts/testresults/*.trx`, `smoke-test.txt`, `smoke-test.png`.

### UI-Smoke-Test

Der Smoke-Test startet das echte Hauptfenster mit isolierten Einstellungen in `%TEMP%`
(die Nutzereinstellungen bleiben unberührt) und prüft automatisch: Fenster sichtbar, Menüs aus
der Registry, keine Shortcut-Konflikte, Tastenkürzel gebunden, Befehlszustände mit/ohne Dokument,
zwei Dokumente anlegen, gerenderte Leinwandgröße, Tabwechsel inkl. Arbeitsbereich, Fenstertitel,
Statusleiste, Schließen, Alle schließen, Vollbild an/aus. Exitcode 0 = bestanden.

## Ergebnisse

### 18.10.2026 – M1 (Debug, Windows 11 Pro 10.0.26200, .NET SDK 10.0.402)

| Testprojekt | Bestanden | Fehlgeschlagen | Übersprungen |
|---|---|---|---|
| OrbitStudio.Domain.Tests | 20 | 0 | 0 |
| OrbitStudio.Configuration.Tests | 11 | 0 | 0 |
| OrbitStudio.Application.Tests | 24 | 0 | 0 |
| OrbitStudio.Architecture.Tests | 9 | 0 | 0 |
| **Summe** | **64** | **0** | **0** |

UI-Smoke-Test (Debug-Build): 18 von 18 Prüfungen bestanden, Exitcode 0.
UI-Smoke-Test (veröffentlichte Single-File-EXE `artifacts/publish/OrbitStudio.exe`): bestanden, Exitcode 0.

Sichtprüfung: Das vom Smoke-Test erzeugte Bildschirmfoto zeigt Menüleiste, zwei Tabs, eine
320 × 240 px große weiße Leinwand auf dunkler Arbeitsfläche und die Statusleiste
„320 × 240 px / Zoom: 100 %“.

Nicht automatisiert geprüft (manuell offen): Dialog „Neues Bild“ per Tastatur bedienen,
Fensterposition nach Neustart, Verhalten bei 150 %/200 % Skalierung und auf einem zweiten Monitor.

## Abnahme Strichmuster-Werkzeug (Abschnitt 1.1)

Nicht ausgeführt – das Werkzeug wird in M5 umgesetzt.
