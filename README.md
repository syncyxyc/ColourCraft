# ORBIT Studio

Windows-Bildbearbeitung (WPF, .NET 10) mit Ebenen, Werkzeugen, Effekten und eigenem Projektformat `.orbit`.
Vollständig deutsche Oberfläche, keine Telemetrie.

> Status: **Meilenstein M2** – Bilder öffnen (PNG, JPEG, BMP, WebP, GIF), anzeigen mit Zoom und Verschieben,
> exportieren (PNG, JPEG, BMP, WebP). Bearbeitungsfunktionen folgen ab M3; noch nicht verfügbare Menübefehle
> sind deaktiviert und nennen ihren Meilenstein. Details: [docs/implementation-roadmap.de.md](docs/implementation-roadmap.de.md)

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
