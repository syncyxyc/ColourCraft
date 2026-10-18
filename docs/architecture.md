# Architektur – ORBIT Studio

Stand: 18.10.2026, Meilenstein M1

## Schichten und Projekte

```
OrbitStudio.App            WPF-Startprojekt: Kompositionswurzel (DI), Theme-Ressourcen, Manifest, Smoke-Test
  └─ OrbitStudio.Presentation   ViewModels, Views, Dialoge, Menüaufbau, Sprachressourcen (WPF)
       └─ OrbitStudio.Application  Arbeitsbereich (offene Dokumente), Command-Registry, Einstellungsdienst
            ├─ OrbitStudio.Domain        Dokumentmodell, Größengrenzen (ohne WPF, ohne Fremdbibliotheken)
            └─ OrbitStudio.Configuration AppInfo, Pfade, versionierte Einstellungen, atomares Speichern
  └─ OrbitStudio.Diagnostics    Datei-Logger (Microsoft.Extensions.Logging), keine Telemetrie
```

In M1 existieren nur Projekte mit echtem Inhalt. Die übrigen Projekte aus der Spezifikation
(Imaging, Rendering, Tools, Layers, Effects, History, Storage, Themes, Docking, Plugin.*) werden in dem
Meilenstein angelegt, in dem sie Code erhalten (siehe [entscheidungen.md](entscheidungen.md), E-003).

| Projekt | Zielframework | Darf referenzieren |
|---|---|---|
| Domain | net10.0 | – |
| Configuration | net10.0 | – |
| Diagnostics | net10.0 | Microsoft.Extensions.Logging.Abstractions |
| Application | net10.0 | Domain, Configuration, Logging.Abstractions |
| Presentation | net10.0-windows | Application (+ transitiv Domain, Configuration), CommunityToolkit.Mvvm |
| App | net10.0-windows | alle, DI, Logging |

## Abhängigkeitsregeln

Verbindlich nach Spezifikation Abschnitt 3.1. Automatisch geprüft durch
`tests/OrbitStudio.Architecture.Tests/DependencyRuleTests.cs`:

- Domain, Configuration, Diagnostics und Application referenzieren **kein** UI-Framework
  (PresentationFramework, PresentationCore, WindowsBase, System.Xaml, WinForms).
- Jedes Kernprojekt referenziert nur die in der Tabelle erlaubten `OrbitStudio.*`-Assemblies.
- Domain referenziert ausschließlich `System.*`-Assemblies.

Neue Projekte werden beim Anlegen in die Tabelle des Tests eingetragen.

## Datenfluss (M1)

1. `App.OnStartup` erzeugt Logger, DI-Container und lädt die Einstellungen (`SettingsService`).
2. `MainViewModel` registriert alle Befehle in der `CommandRegistry` und baut daraus die Menüs.
3. `MainWindow` bindet Tastenkürzel ausschließlich aus der Registry (keine Shortcuts in einzelnen Controls).
4. Befehl „Neu“ → Dialog → `ImageDocument.Create` (Validierung) → `DocumentWorkspace.Add` →
   Ereignis → `DocumentViewModel` → Tab.
5. Tabwechsel in der Oberfläche → `MainViewModel.ActiveDocument` → `DocumentWorkspace.Activate`
   → `CommandRegistry.NotifyStateChanged` → Menüeinträge werden neu ausgewertet.
6. Beim Beenden: Rückfrage bei ungespeicherten Dokumenten, dann Fensterzustand in `workspace.json`.

## Command-Registry

- Jeder Befehl hat eine stabile ID (`file.new`, `edit.undo`, …), einen Textschlüssel und ein
  optionales Standard-Tastenkürzel im Textformat (`Ctrl+Shift+S`).
- Befehle späterer Meilensteine sind als `AppCommand.Planned(..., "M4")` registriert: sichtbar,
  deaktiviert, mit Tooltip „wird in Meilenstein M4 umgesetzt“. Sie führen nie etwas aus.
- Tastenkürzel werden normalisiert (`shift+ctrl+s` = `Ctrl+Shift+S`), Konflikte erkennt
  `CommandRegistry.FindGestureConflicts()`.

## Pixelrepräsentation (verbindlich ab M2)

- Format: **BGRA, 8 Bit je Kanal**, 4 Byte je Pixel.
- Alpha: **premultiplied** (vormultipliert). Begründung: SkiaSharp und WPF (`Pbgra32`) arbeiten
  nativ damit, Komposition und Filter sind ohne ständige Umrechnung korrekt. Import/Export rechnet
  an der Dateigrenze um (siehe E-004).
- Speicherlayout: zeilenweise, oben links beginnend, Stride = Breite × 4 Byte (ohne Auffüllung).
- Koordinaten: Ursprung oben links, x nach rechts, y nach unten, ganzzahlige Pixelindizes;
  Pixel (x, y) überdeckt die Fläche [x, x+1) × [y, y+1).
- Fließkomma-Positionen (Werkzeuge) als `PointD` (double) in Dokumentkoordinaten.

## Zustandstrennung

- Pro Dokument (`ImageDocument`, `DocumentViewModel`): Name, Größe, Speicherzustand, Ansicht
  (Zoom, Cursor). Ab M3/M4 zusätzlich Ebenen, Auswahl, Historie.
- Global (`SettingsService`): `settings.json` (Sprache, zuletzt verwendet, Vorgaben) und
  `workspace.json` (Fenster). Getrennte Dateien, je mit `schemaVersion`.

## Einstellungsdateien

Ort: `%APPDATA%\OrbitStudio\`. Logs: `%LOCALAPPDATA%\OrbitStudio\logs\orbit-JJJJMMTT.log` (14 Tage).
Laden fällt bei Fehlern auf Standardwerte zurück und meldet dies (Status `Invalid`, `Corrected`,
`NewerVersion`). Beschädigte Dateien werden vorher als `*.defekt-<Zeitstempel>` gesichert.
Speichern schreibt erst `*.tmp` und ersetzt dann die Zieldatei.

## Lokalisierung

Alle sichtbaren Texte stehen in `src/OrbitStudio.Presentation/Resources/Strings.de.xaml`.
XAML nutzt `{StaticResource Schlüssel}`, Code nutzt `Loc.Get`/`Loc.Format`. Eine weitere Sprache
entsteht durch eine Kopie der Datei mit denselben Schlüsseln.

## Theme

`src/OrbitStudio.App/Themes/Base.xaml` definiert Design-Tokens (`Color.*`, `Brush.*`). Views
verwenden sie nur über `DynamicResource`, damit das Theme-System (M9) sie zur Laufzeit tauschen kann.
