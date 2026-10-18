# ORBIT Studio

Windows-Bildbearbeitung (WPF, .NET 10) mit Ebenen, Werkzeugen, Effekten und eigenem Projektformat `.orbit`.
Vollständig deutsche Oberfläche, keine Telemetrie.

> Status: **Meilenstein M4** – Bilder öffnen und exportieren, Ebenen mit Gruppen, Masken und 9 Mischmodi,
> 22 Werkzeuge (Auswahl, Malen, Formen, Füllen, Verlauf, Pipette, Verschieben, Zuschneiden, Hand, Zoom),
> Rückgängig/Wiederholen, Zwischenablage, Bildgröße/Leinwand/Drehen/Spiegeln. Effekte und das Projektformat
> folgen; noch nicht verfügbare Menübefehle sind deaktiviert und nennen ihren Meilenstein. Details: [docs/implementation-roadmap.de.md](docs/implementation-roadmap.de.md)

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
