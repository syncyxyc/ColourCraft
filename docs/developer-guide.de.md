# Entwicklerhandbuch

## Einstieg

1. .NET SDK 10 installieren.
2. `powershell -ExecutionPolicy Bypass -File build/build.ps1`
3. `powershell -ExecutionPolicy Bypass -File build/test.ps1`
4. Starten: `src/OrbitStudio.App/bin/Debug/net10.0-windows/OrbitStudio.exe`

Architektur und Abhängigkeitsregeln: [architecture.md](architecture.md).

## Konventionen

- Sprache im Code: Bezeichner Englisch, Kommentare und Dokumentation Deutsch.
- `Nullable` aktiv, Warnungen sind Fehler.
- **Keine sichtbaren Texte im Code oder XAML.** Neue Texte in
  `src/OrbitStudio.Presentation/Resources/Strings.de.xaml` eintragen.
- **Keine Farben in Views.** Nur Tokens aus `Themes/Base.xaml` per `DynamicResource`.
- **Neuer Befehl:** ID in `CommandIds`, Text in `Strings.de.xaml`, Registrierung in
  `MainViewModel.RegisterCommands`, Platz im Menü in `MenuLayout`. Tastenkürzel nur dort angeben.
  Noch nicht umgesetzte Befehle mit `AppCommand.Planned(…, "Mx")`.
- **Neues Projekt:** in `OrbitStudio.sln` aufnehmen und die erlaubten Abhängigkeiten in
  `tests/OrbitStudio.Architecture.Tests/DependencyRuleTests.cs` eintragen.
- Nach jedem Meilenstein: Build, Tests, Doku (`implementation-roadmap.de.md`, `testing.de.md`,
  `bekannte-einschraenkungen.md`), Commit.

## Logs

`%LOCALAPPDATA%\OrbitStudio\logs\orbit-JJJJMMTT.log`. Niemals Bildinhalte oder Dateiinhalte loggen.
