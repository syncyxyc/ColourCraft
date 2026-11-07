# Build und Release

## Voraussetzungen

- Windows 10/11 x64
- .NET SDK 10.0.400 oder neuer (`winget install Microsoft.DotNet.SDK.10`)
- PowerShell 5.1 oder 7

## Skripte

| Skript | Zweck |
|---|---|
| `build/build.ps1 [-Configuration Debug\|Release\|All]` | Restore und Build (Standard: beide Konfigurationen) |
| `build/test.ps1 [-Configuration Debug\|Release] [-SkipSmoke] [-WithInput]` | Alle Tests + UI-Smoke-Test (baut die App vorher neu; + optional Eingabetest), Ergebnisse in `artifacts/testresults/` |
| `build/ui-input-test.ps1` | Eingabetest mit echter Maus/Tastatur (nur in ungenutzter Sitzung) |
| `build/publish.ps1` | Single-File-EXE nach `artifacts/publish/OrbitStudio.exe` |
| `build/release.ps1` | Publish plus SHA-256-Prüfsumme für ein GitHub-Release nach `artifacts/release/` (E-059) |
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
| 18.10.2026 (M7) | `artifacts/publish/OrbitStudio.exe`, 158,1 MB, Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (166/166, Exitcode 0) – inkl. Speichern/Öffnen von `.orbit` |
| 18.10.2026 (M6) | `artifacts/publish/OrbitStudio.exe`, 157,9 MB, Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (153/153, Exitcode 0) |
| 18.10.2026 (M5) | `artifacts/publish/OrbitStudio.exe`, 157,7 MB, Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (135/135, Exitcode 0) |
| 18.10.2026 (M4) | `artifacts/publish/OrbitStudio.exe`, 157,6 MB, Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (117/117, Exitcode 0) |
| 18.10.2026 (M3) | `artifacts/publish/OrbitStudio.exe`, 156,9 MB, Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (79/79, Exitcode 0) |
| 18.10.2026 (M2) | `artifacts/publish/OrbitStudio.exe`, 156,6 MB (inkl. SkiaSharp-Native), Windows 11 Pro 10.0.26200 | Start erfolgreich, UI-Smoke-Test bestanden (42/42, Exitcode 0) – inkl. Öffnen/Export über SkiaSharp aus der Single-File-EXE |

Hinweis: Auf dem Prüfrechner ist das .NET SDK installiert. Ein Start auf einem Rechner ganz ohne
.NET wurde noch nicht geprüft (→ M12, Release-Checkliste).

## Release-Checkliste

Wird in M12 vervollständigt (portable ZIP, Installer, Handbuch, Lizenzen).

## Veröffentlichen (GitHub Release)

Das Programm holt Updates aus den GitHub-Releases von `syncyxyc/ColourCraft` (E-058). Ein Release braucht genau
zwei Dateien mit diesen Namen:

- `OrbitStudio.exe` – die Programmdatei
- `OrbitStudio.exe.sha256` – die SHA-256-Summe im Format `<hex>  OrbitStudio.exe`

Ablauf für eine neue Version:

1. Versionsnummer in `Directory.Build.props` (`<Version>`) erhöhen und `CHANGELOG.md` ergänzen.
2. Alle Tests grün: `build/test.ps1`.
3. `build/release.ps1` ausführen. Es erstellt `artifacts/release/OrbitStudio.exe` und die Prüfsummendatei.
4. Committen, Tag `v<Version>` setzen und pushen.
5. Release anlegen (GitHub CLI):

```
gh release create v0.9.0 artifacts/release/OrbitStudio.exe artifacts/release/OrbitStudio.exe.sha256 --title "ORBIT Studio 0.9.0" --notes-file artifacts/release/notes.md
```

   `--notes-file` soll nur den Abschnitt dieser Version aus `CHANGELOG.md` enthalten (den Text zwischen `## <Version>` und dem nächsten `## `).
6. Prüfen: `gh release view v0.9.0`. Das Release muss nicht als Vorabversion markiert sein, sonst findet
   die Updateprüfung es nicht (sie nutzt „latest“).

Kollegen bekommen den Link zur Release-Seite. Dort steht die EXE unter „Assets“.
