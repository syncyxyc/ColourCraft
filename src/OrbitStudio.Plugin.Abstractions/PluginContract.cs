using OrbitStudio.Effects;

namespace OrbitStudio.Plugin.Abstractions;

/// <summary>Version des Plugin-Vertrags. Ein Plugin läuft nur, wenn seine API-Version mit dieser übereinstimmt.</summary>
public static class PluginApi
{
    public const int Version = 1;
}

/// <summary>Beschreibung eines Plugins (steht auch in der Datei <c>plugin.json</c>).</summary>
/// <param name="Id">Kleinbuchstaben, Ziffern und „-“; eindeutig. Effekt-IDs des Plugins beginnen mit „&lt;Id&gt;.“.</param>
public sealed record PluginManifest(string Id, string Name, string Version, string Vendor, int ApiVersion);

/// <summary>Einstiegspunkt eines Plugins: eine öffentliche Klasse mit parameterlosem Konstruktor.</summary>
public interface IOrbitPlugin
{
    /// <summary>Wird einmal beim Laden aufgerufen. Das Plugin meldet hier alles an, was es bereitstellt.</summary>
    void Register(IPluginHost host);
}

/// <summary>
/// Was ein Plugin beim Programm anmelden kann. Der Vertrag Version 1 enthält nur Effekte; Werkzeuge, Panels, Importer und
/// Exporter, Menübefehle, Themes und Dokumentaktionen folgen in einer späteren API-Version.
/// </summary>
public interface IPluginHost
{
    /// <summary>
    /// Meldet einen Effekt an. Seine ID muss mit „&lt;Plugin-ID&gt;.“ beginnen, die Standardwerte aller Parameter müssen gültig sein.
    /// Verstöße werfen <see cref="InvalidOperationException"/> und lassen das Laden des Plugins fehlschlagen.
    /// </summary>
    void RegisterEffect(IEffect effect);
}
