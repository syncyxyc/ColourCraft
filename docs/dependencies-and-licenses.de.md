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
| SkiaSharp / SkiaSharp.Views.WPF | MIT (4.153.1 geprüft) | M2 | Bildpuffer, Codecs |
| AvalonDock | Ms-PL (vor Einbindung erneut prüfen) | M8 | Docking |
| BenchmarkDotNet | MIT (vor Einbindung prüfen) | M11 | Performance |

## Bewusst nicht verwendet

| Paket | Grund |
|---|---|
| SixLabors.ImageSharp | Six Labors Split License: kostenpflichtig für bestimmte kommerzielle Nutzung |
| FluentAssertions (ab v8) | Xceed-Lizenz: kostenpflichtig für kommerzielle Nutzung; stattdessen Shouldly |

## Icons, Schriften

Keine fremden Icons oder Schriften eingebunden. Das Tab-Schließen-Symbol ist das Unicode-Zeichen
„✕“ aus der Systemschrift. Eigene Vektor-Icons folgen in M8.
