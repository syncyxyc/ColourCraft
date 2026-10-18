# Entscheidungen

## E-001 – .NET 10 SDK 10.0.402, `global.json` mit `rollForward: latestFeature` (18.10.2026)
- Kontext: Spezifikation fordert die aktuelle LTS-Version. Auf dem Rechner war kein SDK installiert.
- Entscheidung: .NET SDK 10.0.402 per winget installiert. `global.json` verlangt mindestens 10.0.400
  und erlaubt neuere Feature-Bänder.
- Alternative: Version fest pinnen – verworfen, da Sicherheitsupdates sonst manuell nötig wären.

## E-002 – Zentrale Paketverwaltung und gemeinsame Build-Einstellungen (18.10.2026)
- `Directory.Packages.props` (Versionen) und `Directory.Build.props` (Nullable, TreatWarningsAsErrors,
  Sprache, Version) gelten für alle Projekte. Verhindert Versionsdrift zwischen Projekten.

## E-003 – Projekte erst anlegen, wenn sie Code erhalten (18.10.2026)
- Kontext: Spezifikation listet 17 Quellprojekte und verlangt gleichzeitig „keine leeren Projekte“.
- Entscheidung: M1 enthält Domain, Configuration, Diagnostics, Application, Presentation, App.
  Weitere Projekte werden im jeweiligen Meilenstein angelegt und in den Architekturtest aufgenommen.
- Zusätzliche Testprojekte gegenüber der Liste: `Configuration.Tests`, `Application.Tests`,
  `Architecture.Tests` – je Kernprojekt ein Testprojekt, Architekturregeln getrennt.

## E-004 – Pixel: BGRA 8 Bit, premultiplied Alpha (18.10.2026)
- Alternativen: straight Alpha (einfachere Farbbearbeitung, aber falsche Interpolation beim
  Skalieren/Weichzeichnen ohne Umrechnung).
- Entscheidung: premultiplied, weil SkiaSharp und WPF `Pbgra32` nativ damit arbeiten und
  Komposition/Filter ohne Zusatzschritte korrekt sind. Farbanpassungen rechnen intern um.

## E-005 – Sprachressourcen als XAML-ResourceDictionary statt .resx (18.10.2026)
- Kontext: Die `.resx`-Codegenerierung (`PublicResXFileCodeGenerator`) läuft nur in Visual Studio,
  nicht zuverlässig mit `dotnet build`.
- Entscheidung: `Strings.de.xaml` mit `sys:String`-Einträgen; Zugriff in XAML per
  `StaticResource`, in Code per `Loc`. Weitere Sprachen über zusätzliche Dateien gleicher Schlüssel.
- Nachteil: kein Compile-Zeit-Check der Schlüssel; fehlende Schlüssel erscheinen sichtbar als `[Schlüssel]`.

## E-006 – Befehle späterer Meilensteine sichtbar, aber deaktiviert (18.10.2026)
- Kontext: Spezifikation verlangt die vollständige Menüstruktur, verbietet aber Befehle ohne Funktion.
- Entscheidung: Geplante Befehle stehen im Menü, sind deaktiviert und nennen per Tooltip den
  Meilenstein. Sie führen nie etwas aus und stehen in `bekannte-einschraenkungen.md`.
- Alternative: Menüeinträge erst später einfügen – verworfen, da Menüaufbau und Shortcut-Konflikte
  so schon jetzt vollständig geprüft werden.

## E-007 – Eigener Datei-Logger statt Fremdbibliothek (18.10.2026)
- `Microsoft.Extensions.Logging` hat keinen Datei-Sink. Statt Serilog o. Ä. ein kleiner eigener
  `FileLoggerProvider` (Tagesdateien, 14 Tage Aufbewahrung). Weniger Abhängigkeiten, volle Kontrolle
  darüber, dass keine Bildinhalte geloggt werden.

## E-008 – Eingebauter UI-Smoke-Test statt UI-Automatisierungsframework (18.10.2026)
- Alternativen: FlaUI/WinAppDriver (zusätzliche Abhängigkeit, fragil in CI).
- Entscheidung: `--smoke-test` startet das echte Fenster, führt Befehle über die Registry aus und
  prüft den visuellen Baum. Isolierte Einstellungen in `%TEMP%`, Ergebnis per Exitcode und Datei.
  Klick-Interaktionen (z. B. Ziehen) werden bei Bedarf manuell protokolliert.

## E-009 – Basis-Theme hell mit dunkler Arbeitsfläche (18.10.2026)
- Die Standard-WPF-Steuerelemente (Menü, Tabs) sind hell; ein vollständig dunkles Theme erfordert
  eigene Control-Templates (M9). Bis dahin helle Oberfläche, dunkle Arbeitsfläche für neutralen
  Bildkontrast. Alle Farben bereits als Tokens.

## E-010 – Repository-Name ColourCraft, Produktname ORBIT Studio (18.10.2026)
- Das GitHub-Repository heißt `syncyxyc/ColourCraft`. Der Produktname laut Spezifikation ist
  „ORBIT Studio“ und zentral in `AppInfo.cs` austauschbar.
