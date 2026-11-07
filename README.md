# ORBIT Studio

Windows-Bildbearbeitung (WPF, .NET 10) mit Ebenen, Werkzeugen, Effekten und eigenem Projektformat `.orbit`.
Vollständig deutsche Oberfläche, keine Telemetrie.

> Status: **Version 0.9.0** (öffentliches Release, siehe [CHANGELOG.md](CHANGELOG.md)). Bis M4: Bilder öffnen und exportieren, Ebenen mit Gruppen, Masken und 9 Mischmodi,
> 22 Werkzeuge (Auswahl, Malen, Formen, Füllen, Verlauf, Pipette, Verschieben, Zuschneiden, Hand, Zoom),
> Rückgängig/Wiederholen, Zwischenablage, Bildgröße/Leinwand/Drehen/Spiegeln. Seit M5–M7: Strichmuster-Werkzeug,
> 19 Effekte, Projektformat `.orbit`; noch nicht verfügbare Menübefehle sind deaktiviert und nennen ihren Meilenstein. Details: [docs/implementation-roadmap.de.md](docs/implementation-roadmap.de.md)

## Schnellstart

```
powershell -ExecutionPolicy Bypass -File build/build.ps1
powershell -ExecutionPolicy Bypass -File build/test.ps1
```

Voraussetzung: .NET SDK 10 (`winget install Microsoft.DotNet.SDK.10`).

## Dokumentation

- [Benutzerhandbuch](docs/user-guide.de.md)
- [Architektur](docs/architecture.md)
- [Umsetzungsplan](docs/implementation-roadmap.de.md)
- [Tests](docs/testing.de.md)
- [Build und Release](docs/build-and-release.de.md)
- [Entwicklerhandbuch](docs/developer-guide.de.md)
- [Abhängigkeiten und Lizenzen](docs/dependencies-and-licenses.de.md)
- [Entscheidungen](docs/entscheidungen.md)
- [Bekannte Einschränkungen](docs/bekannte-einschraenkungen.md)

Die vollständige Projektspezifikation steht in [CLAUDE.md](CLAUDE.md).

## Herunterladen und aktualisieren

Die aktuelle Programmdatei gibt es auf der Seite [Releases](https://github.com/syncyxyc/ColourCraft/releases).
`OrbitStudio.exe` ist eine einzelne Datei ohne Installation. Mit **Hilfe › Nach Updates suchen** prüft das Programm
auf Knopfdruck, ob eine neuere Version veröffentlicht ist, und installiert sie nach Bestätigung.

Hinweis: Die Datei ist nicht signiert. Windows kann beim ersten Start warnen (siehe Benutzerhandbuch, „Updates“).
