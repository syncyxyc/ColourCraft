# Plugin-Entwicklung

Stand: 23.10.2026 (Plugin-API Version 1). Die Plugin-Laufzeit (`OrbitStudio.Plugin.Runtime`) ist umgesetzt und getestet;
die **Anbindung an die Oberfläche (Plugin-Verwaltung, Menü „Erweiterungen“) fehlt noch** und steht in
`bekannte-einschraenkungen.md`.

## Vertrauensmodell (verbindlich)

Plugins laufen **im selben Prozess** wie ORBIT Studio und haben dieselben Rechte wie das Programm (Dateien, Netzwerk,
Speicher). Es gibt **keine Sandbox**. Ein Plugin darf nur installiert werden, wenn man seinem Hersteller vertraut.
Die Laufzeit schützt vor *Fehlern* (ein defektes Plugin verhindert weder den Start noch andere Plugins), nicht vor
*Absicht* (ein bösartiges Plugin kann alles, was das Programm kann).

## Was ein Plugin bereitstellen kann (API 1)

Nur **Effekte** (`IPluginHost.RegisterEffect`). Werkzeuge, Panels, Importer/Exporter, Menübefehle, Themes und
Dokumentaktionen sind für eine spätere API-Version vorgesehen und heute nicht möglich.

## Aufbau eines Plugins

Ein Plugin ist ein Unterordner im Plugin-Ordner mit mindestens zwei Dateien:

```
plugins/
  mein-plugin/
    plugin.json
    MeinPlugin.dll
```

`plugin.json`:

```json
{
  "id": "mein-plugin",
  "name": "Mein Plugin",
  "version": "1.0.0",
  "vendor": "Ich",
  "apiVersion": 1,
  "assembly": "MeinPlugin.dll",
  "entryType": "MeinPlugin.Einstieg"
}
```

- `id`: Kleinbuchstaben, Ziffern und „-“; eindeutig. Effekt-IDs müssen mit `<id>.` beginnen.
- `apiVersion` muss genau `1` sein, sonst wird das Plugin als „inkompatibel“ gemeldet und nicht geladen.
- `assembly` liegt **im Plugin-Ordner**; Pfade, die hinausführen (`..`) oder absolut sind, werden abgelehnt.
- `entryType`: öffentliche Klasse mit parameterlosem Konstruktor, die `IOrbitPlugin` umsetzt.

## Schritt für Schritt: das Beispielplugin

1. **Erstellen:** `plugins/OrbitStudio.SamplePlugin` ist ein Klassenbibliotheks-Projekt, das nur
   `OrbitStudio.Plugin.Abstractions` referenziert (`<Private>false</Private>`: die Programm-DLLs werden nicht mitkopiert).
   `SamplePlugin.Register` meldet den Effekt `orbit-sample.posterize` („Tonwertstufen“) an.
2. **Bauen:** `dotnet build plugins/OrbitStudio.SamplePlugin -c Release`. Ergebnis in `bin/Release/net10.0/`:
   `OrbitStudio.SamplePlugin.dll` und `plugin.json`.
3. **Installieren:** beide Dateien in einen Unterordner des Plugin-Ordners kopieren, z. B.
   `%APPDATA%\OrbitStudio\plugins\orbit-sample\`.
4. **Nutzen:** `PluginLoader.LoadAll(pluginOrdner, effektRegistry)` meldet den Effekt in der `EffectRegistry` an; er läuft
   dann über die `EffectEngine` wie jeder eingebaute Effekt.

Schritte 2–4 werden vom Test `SamplePlugin_IsBuiltInstalledAndUsed_EndToEnd` tatsächlich ausgeführt (Bauen über die
Projektreferenz, Kopieren in einen Temp-Ordner, Laden, Effekt auf ein Testbild anwenden, Pixelwerte prüfen). **Nicht
ausgeführt** ist Schritt 3 im installierten Programm, weil es die Oberfläche dafür noch nicht gibt.

## Fehlerverhalten

Jedes Plugin wird einzeln geladen; Fehler werden pro Plugin gemeldet (`PluginInfo.Status`/`Error`, deutsche Texte):

| Status | Bedeutung |
|---|---|
| `Loaded` | geladen, Effekte angemeldet |
| `Disabled` | vom Nutzer deaktiviert (ID in der Liste `disabledIds`); Assembly wird nicht geladen |
| `Incompatible` | `apiVersion` passt nicht |
| `Failed` | `plugin.json` fehlt oder ist ungültig, Assembly fehlt/ist keine .NET-Assembly, Einstiegspunkt falsch, doppelte Plugin-ID, falsches Effekt-Präfix, ungültige Standardwerte, oder `Register` hat eine Ausnahme geworfen |

Wirft ein Plugin in `Register`, wird **keiner** seiner Effekte angemeldet (erst nach fehlerfreiem Durchlauf wird übernommen).

## Bekannte Grenzen

- Fehler, die ein Plugin-Effekt erst beim Rechnen (`Render`) wirft, fängt die `EffectEngine` nicht gesondert ab; sie
  behandelt der aufrufende Hintergrundjob wie jeden Effektfehler.
- Plugin-Assemblies werden geladen und nicht wieder entladen (kein Aktualisieren ohne Neustart).
- Texte des Effekts (`Effect.<id>`, `EffectParam.<…>`) liegen in den Programm-Ressourcen; für Plugin-Effekte gibt es noch
  keinen Weg, eigene Texte mitzubringen (die Oberfläche zeigt bis dahin den Textschlüssel).
