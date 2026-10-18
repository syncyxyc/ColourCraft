# Abhängigkeiten und Lizenzen

Geprüft am 18.10.2026 über die NuGet-API (Lizenzangabe im `.nuspec` der genannten Version).
Versionen werden zentral in `Directory.Packages.props` gepflegt.

## Laufzeit (im Programm enthalten)

| Paket | Version | Lizenz | Zweck | Projekt |
|---|---|---|---|---|
| .NET 10 Runtime + WPF (Microsoft) | 10.0 | MIT | Laufzeit, Oberfläche | alle |
| CommunityToolkit.Mvvm | 8.4.2 | MIT | MVVM (ObservableObject, RelayCommand) | Presentation |
| Microsoft.Extensions.DependencyInjection | 10.0.13 | MIT | Dependency Injection | App |
| Microsoft.Extensions.Logging | 10.0.13 | MIT | Logging-Infrastruktur | App |
| Microsoft.Extensions.Logging.Abstractions | 10.0.13 | MIT | Logging-Schnittstellen | Diagnostics, Application, Presentation |
| SkiaSharp | 4.153.1 | MIT | Dekodieren PNG/JPEG/BMP/WebP/GIF, Kodieren PNG/JPEG/WebP | Imaging |
| SkiaSharp.NativeAssets.Win32 (transitiv) | 4.153.1 | MIT (Paket) + Drittlizenzen der nativen Bibliothek, siehe unten | native Skia-Bibliothek `libSkiaSharp.dll` | Imaging |

### Native Skia-Bibliothek (in SkiaSharp.NativeAssets.Win32)

Das Paket liefert `THIRD-PARTY-NOTICES.txt` (139.775 Byte) mit den Lizenzen der enthaltenen bzw. im
Skia-Quellbaum verwendeten Komponenten, u. a.: Skia (BSD-3-Clause), libpng (libpng-Lizenz),
libjpeg-turbo (IJG/BSD-3-Clause/zlib), libwebp (BSD-3-Clause), zlib (zlib), expat (MIT), FreeType
(FTL/GPLv2 wahlweise), HarfBuzz (MIT-ähnlich), ICU (Unicode-Lizenz), ANGLE (BSD), Adobe DNG SDK
(eigene Lizenz: lizenzgebührenfrei, auch kommerziell, Hinweise müssen erhalten bleiben), piex (Apache-2.0),
sowie libmicrohttpd (LGPL-2.1).

**Offen bis zum Release (M12):**
- Die vollständige `THIRD-PARTY-NOTICES.txt` muss mit der EXE ausgeliefert werden (Lizenzinformationen).
- Prüfen, ob libmicrohttpd (LGPL) tatsächlich in `libSkiaSharp.dll` enthalten ist. In Skia wird es nur für
  Entwicklerwerkzeuge (skiaserve) verwendet; die Auflistung im Notices-File ist eine Obermenge.
  Ergebnis der Prüfung in diesem Dokument nachtragen.

## Nur für Tests

| Paket | Version | Lizenz | Zweck |
|---|---|---|---|
| xunit | 2.9.3 | Apache-2.0 | Testframework |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | Testausführung über `dotnet test` |
| Microsoft.NET.Test.Sdk | 18.11.0 | MIT | Testinfrastruktur |
| Shouldly | 4.3.0 | BSD-3-Clause | Assertions |

## Geplant (noch nicht eingebunden)

| Paket | Lizenz (vorläufig) | Meilenstein | Hinweis |
|---|---|---|---|
| AvalonDock | Ms-PL (vor Einbindung erneut prüfen) | M8 | Docking |
| BenchmarkDotNet | MIT (vor Einbindung prüfen) | M11 | Performance |

## Bewusst nicht verwendet

| Paket | Grund |
|---|---|
| SixLabors.ImageSharp | Six Labors Split License: kostenpflichtig für bestimmte kommerzielle Nutzung |
| SkiaSharp.Views.WPF | nicht nötig: Anzeige über WPF-`WriteableBitmap` (gleiches Pixelformat) |
| FluentAssertions (ab v8) | Xceed-Lizenz: kostenpflichtig für kommerzielle Nutzung; stattdessen Shouldly |
| SkiaSharp-Bildfilter für Effekte | technisch, nicht lizenzrechtlich: keine exakt prüfbaren Rundungs-/Randregeln; Effekte sind eigene Implementierungen ohne neue Abhängigkeit (E-040) |

## Icons, Schriften

Keine fremden Icons oder Schriften eingebunden. Das Tab-Schließen-Symbol ist das Unicode-Zeichen
„✕“ aus der Systemschrift. Eigene Vektor-Icons folgen in M8.
