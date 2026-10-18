# Build und Release

## Voraussetzungen

- Windows 10/11 x64
- .NET SDK 10.0.400 oder neuer (`winget install Microsoft.DotNet.SDK.10`)
- PowerShell 5.1 oder 7

## Skripte

| Skript | Zweck |
|---|---|
| `build/build.ps1 [-Configuration Debug\|Release\|All]` | Restore und Build (Standard: beide Konfigurationen) |
| `build/test.ps1 [-Configuration Debug\|Release] [-SkipSmoke] [-WithInput]` | Alle Tests + UI-Smoke-Test (+ optional Eingabetest), Ergebnisse in `artifacts/testresults/` |
| `build/ui-input-test.ps1` | Eingabetest mit echter Maus/Tastatur (nur in ungenutzter Sitzung) |
| `build/publish.ps1` | Single-File-EXE nach `artifacts/publish/OrbitStudio.exe` |
| `build/clean.ps1` | Entfernt `bin/`, `obj/`, `artifacts/` |

Aufruf, falls Skriptausführung gesperrt ist:

```
powershell -ExecutionPolicy Bypass -File build/build.ps1
```

## Publish

`publish.ps1` führt aus:

```
dotnet publish src/OrbitStudio.App/OrbitStudio.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/publish
```

Die Publish-Einstellungen (SelfContained, PublishSingleFile, IncludeNativeLibrariesForSelfExtract,
ReadyToRun) stehen in `OrbitStudio.App.csproj` und gelten nur beim Veröffentlichen.

## Startprüfungen

| Datum | Build | Ergebnis |
|---|---|---|
| 18.10.2026 (M1) | `artifacts/publish/OrbitStudio.exe`, 142,3 MB, Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (18/18, Exitcode 0) |
| 18.10.2026 (M2) | `artifacts/publish/OrbitStudio.exe`, 156,6 MB (inkl. SkiaSharp-Native), Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (42/42, Exitcode 0) – inkl. Öffnen/Export über SkiaSharp aus der Single-File-EXE |

Hinweis: Auf dem Prüfrechner ist das .NET SDK installiert. Ein Start auf einem Rechner ganz ohne
.NET wurde noch nicht geprüft (→ M12, Release-Checkliste).

## Release-Checkliste

Wird in M12 vervollständigt (portable ZIP, Installer, Handbuch, Lizenzen).
