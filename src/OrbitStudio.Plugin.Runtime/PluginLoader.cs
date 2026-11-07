using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using OrbitStudio.Effects;
using OrbitStudio.Plugin.Abstractions;

namespace OrbitStudio.Plugin.Runtime;

public enum PluginStatus
{
    /// <summary>Geladen und angemeldet.</summary>
    Loaded,

    /// <summary>Vom Nutzer deaktiviert; Assembly wurde nicht geladen.</summary>
    Disabled,

    /// <summary>API-Version passt nicht zum Programm.</summary>
    Incompatible,

    /// <summary>Manifest, Assembly oder Einstiegspunkt ungültig, oder das Plugin hat beim Anmelden einen Fehler geworfen.</summary>
    Failed,
}

/// <summary>Zeile der Plugin-Verwaltung: ein Plugin-Ordner und was daraus geworden ist.</summary>
public sealed record PluginInfo(
    string FolderName,
    PluginManifest? Manifest,
    PluginStatus Status,
    string? Error,
    IReadOnlyList<string> EffectIds);

/// <summary>
/// Findet und lädt Plugins aus genau einem Ordner. Jedes Plugin liegt in einem Unterordner mit <c>plugin.json</c>
/// (id, name, version, vendor, apiVersion, assembly, entryType) und seiner Assembly. Es werden nur Dateien innerhalb des
/// Plugin-Ordners geladen. Fehler eines Plugins werden abgefangen und gemeldet; sie verhindern weder den Start noch das
/// Laden anderer Plugins. <b>Keine Sandbox:</b> Plugins laufen im selben Prozess mit den Rechten des Programms.
/// </summary>
public static class PluginLoader
{
    public const string ManifestFileName = "plugin.json";
    public const int MaxPlugins = 64;
    public const int MaxManifestBytes = 16 * 1024;

    // Diese Assemblies teilen sich Programm und Plugin (gleiche Typen), alles andere lädt jedes Plugin für sich.
    private static readonly string[] SharedAssemblies =
    [
        "OrbitStudio.Plugin.Abstractions", "OrbitStudio.Effects", "OrbitStudio.Domain", "OrbitStudio.History",
    ];

    private sealed record ManifestDto(string? Id, string? Name, string? Version, string? Vendor, int? ApiVersion,
        string? Assembly, string? EntryType);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Durchsucht <paramref name="pluginDirectory"/> und meldet die Effekte erfolgreich geladener Plugins in
    /// <paramref name="effects"/> an. <paramref name="disabledIds"/> sind vom Nutzer deaktivierte Plugin-IDs.
    /// </summary>
    public static IReadOnlyList<PluginInfo> LoadAll(string pluginDirectory, EffectRegistry effects,
        IReadOnlySet<string>? disabledIds = null)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var infos = new List<PluginInfo>();
        if (!Directory.Exists(pluginDirectory))
        {
            return infos;
        }

        IEnumerable<string> folders;
        try
        {
            folders = Directory.EnumerateDirectories(pluginDirectory).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return infos;
        }

        var knownIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var folder in folders)
        {
            var name = Path.GetFileName(folder);
            if (infos.Count >= MaxPlugins)
            {
                infos.Add(new PluginInfo(name, null, PluginStatus.Failed, $"Es werden höchstens {MaxPlugins} Plugins geladen.", []));
                continue;
            }

            infos.Add(LoadOne(folder, name, effects, disabledIds ?? new HashSet<string>(), knownIds));
        }

        return infos;
    }

    private static PluginInfo LoadOne(string folder, string folderName, EffectRegistry effects,
        IReadOnlySet<string> disabledIds, HashSet<string> knownIds)
    {
        PluginManifest? manifest = null;
        try
        {
            var manifestPath = Path.Combine(folder, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                return Fail(folderName, null, $"Die Datei {ManifestFileName} fehlt.");
            }

            if (new FileInfo(manifestPath).Length > MaxManifestBytes)
            {
                return Fail(folderName, null, $"{ManifestFileName} ist zu groß.");
            }

            var dto = JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath), JsonOptions)
                      ?? throw new JsonException();
            var error = ValidateManifest(dto);
            if (error is not null)
            {
                return Fail(folderName, null, error);
            }

            manifest = new PluginManifest(dto.Id!, dto.Name!.Trim(), dto.Version!.Trim(), dto.Vendor!.Trim(), dto.ApiVersion!.Value);
            if (!knownIds.Add(manifest.Id))
            {
                return Fail(folderName, manifest, $"Die Plugin-ID „{manifest.Id}“ wird schon von einem anderen Plugin verwendet.");
            }

            if (disabledIds.Contains(manifest.Id))
            {
                return new PluginInfo(folderName, manifest, PluginStatus.Disabled, null, []);
            }

            if (manifest.ApiVersion != PluginApi.Version)
            {
                return new PluginInfo(folderName, manifest, PluginStatus.Incompatible,
                    $"Das Plugin benötigt API-Version {manifest.ApiVersion}, das Programm bietet Version {PluginApi.Version}.", []);
            }

            var assemblyPath = ResolveInside(folder, dto.Assembly!);
            if (assemblyPath is null || !File.Exists(assemblyPath))
            {
                return Fail(folderName, manifest, $"Die Assembly „{dto.Assembly}“ liegt nicht im Plugin-Ordner.");
            }

            var assembly = new PluginLoadContext(assemblyPath).LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(dto.EntryType!, throwOnError: false)
                       ?? throw new InvalidOperationException($"Der Einstiegspunkt „{dto.EntryType}“ wurde nicht gefunden.");
            if (!typeof(IOrbitPlugin).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"„{dto.EntryType}“ ist keine öffentliche Klasse mit parameterlosem Konstruktor, die IOrbitPlugin umsetzt.");
            }

            var host = new StagingHost(manifest.Id, effects);
            ((IOrbitPlugin)Activator.CreateInstance(type)!).Register(host);

            // Erst jetzt, nach fehlerfreiem Register, werden die Effekte wirklich angemeldet.
            host.Commit();
            return new PluginInfo(folderName, manifest, PluginStatus.Loaded, null, host.EffectIds);
        }
        catch (Exception ex)
        {
            return Fail(folderName, manifest, Describe(ex));
        }
    }

    private static PluginInfo Fail(string folderName, PluginManifest? manifest, string message) =>
        new(folderName, manifest, PluginStatus.Failed, message, []);

    private static string Describe(Exception ex) => ex switch
    {
        JsonException => $"{ManifestFileName} ist kein gültiges JSON.",
        TargetInvocationException { InnerException: { } inner } => Describe(inner),
        BadImageFormatException => "Die Datei ist keine gültige .NET-Assembly.",
        FileLoadException or FileNotFoundException => $"Die Assembly oder eine ihrer Abhängigkeiten konnte nicht geladen werden: {ex.Message}",
        _ => $"{ex.GetType().Name}: {ex.Message}",
    };

    private static string? ValidateManifest(ManifestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id) || !dto.Id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
        {
            return "Die Plugin-ID darf nur Kleinbuchstaben, Ziffern und „-“ enthalten.";
        }

        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Version) || string.IsNullOrWhiteSpace(dto.Vendor))
        {
            return "Name, Version und Hersteller müssen angegeben sein.";
        }

        if (dto.ApiVersion is null)
        {
            return "Die API-Version fehlt.";
        }

        return string.IsNullOrWhiteSpace(dto.Assembly) || string.IsNullOrWhiteSpace(dto.EntryType)
            ? "Assembly und Einstiegspunkt müssen angegeben sein."
            : null;
    }

    /// <summary>Pfad innerhalb des Plugin-Ordners; null bei Pfaden, die hinausführen (z. B. „..“) oder absolut sind.</summary>
    private static string? ResolveInside(string folder, string relative)
    {
        if (Path.IsPathRooted(relative))
        {
            return null;
        }

        var root = Path.GetFullPath(folder + Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, relative));
        return full.StartsWith(root, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>Sammelt die Anmeldungen eines Plugins und prüft sie; angemeldet wird erst bei <see cref="Commit"/>.</summary>
    private sealed class StagingHost(string pluginId, EffectRegistry registry) : IPluginHost
    {
        private readonly List<IEffect> _effects = [];

        public IReadOnlyList<string> EffectIds => _effects.Select(e => e.Id).ToList();

        public void RegisterEffect(IEffect effect)
        {
            ArgumentNullException.ThrowIfNull(effect);
            if (!effect.Id.StartsWith(pluginId + ".", StringComparison.Ordinal) || effect.Id.Length == pluginId.Length + 1)
            {
                throw new InvalidOperationException($"Die Effekt-ID „{effect.Id}“ muss mit „{pluginId}.“ beginnen.");
            }

            if (registry.Find(effect.Id) is not null || _effects.Any(e => e.Id == effect.Id))
            {
                throw new InvalidOperationException($"Der Effekt „{effect.Id}“ ist schon angemeldet.");
            }

            foreach (var parameter in effect.Parameters)
            {
                var error = parameter.Validate(parameter.DefaultValue);
                if (error is not null)
                {
                    throw new InvalidOperationException(
                        $"Der Standardwert des Parameters „{parameter.Id}“ von „{effect.Id}“ ist ungültig ({error}).");
                }
            }

            _effects.Add(effect);
        }

        public void Commit()
        {
            foreach (var effect in _effects)
            {
                registry.Register(effect);
            }
        }
    }

    private sealed class PluginLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: false)
    {
        private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (SharedAssemblies.Contains(assemblyName.Name, StringComparer.Ordinal))
            {
                return null; // aus dem Standardkontext → gleiche Typen wie im Programm
            }

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
