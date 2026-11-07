using System.Text.Json;

namespace OrbitStudio.Application.Commands;

/// <summary>Ergebnis von <see cref="GestureProfile.Import"/>: was übernommen wurde und was nicht (mit Grund).</summary>
public sealed record GestureImportResult(int Applied, IReadOnlyList<string> Skipped, bool Rejected)
{
    public static GestureImportResult Reject(string reason) => new(0, [reason], true);
}

/// <summary>
/// Tastenkürzel-Profile (Spezifikation 11.6): Export und Import der vom Standard abweichenden Belegungen,
/// Neubelegung mit automatischer Konfliktauflösung. Es werden Befehls-IDs gespeichert, keine Texte.
/// </summary>
public static class GestureProfile
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxFileChars = 256 * 1024;

    private sealed record Dto
    {
        public int SchemaVersion { get; init; }

        /// <summary>Befehls-ID → Kürzel; null = Belegung entfernt.</summary>
        public Dictionary<string, string?>? Gestures { get; init; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

        // Lesbare Datei: „Ctrl+S“ statt „Ctrl\u002BS“.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Nur Abweichungen vom Standard: neue Belegungen und bewusst entfernte (null).</summary>
    public static string Export(CommandRegistry registry) => JsonSerializer.Serialize(new Dto
    {
        SchemaVersion = CurrentSchemaVersion,
        Gestures = registry.All.Where(c => c.Gesture != c.DefaultGesture)
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToDictionary(c => c.Id, c => c.Gesture),
    }, Options);

    /// <summary>
    /// Wendet ein Profil an: erst alle Belegungen auf den Standard, dann die Abweichungen der Datei. Unbekannte Befehle und
    /// ungültige Kürzel werden übersprungen und gemeldet. Doppelte Belegungen innerhalb der Datei lösen sich so auf, dass
    /// der alphabetisch erste Befehl das Kürzel behält und die anderen leer bleiben (gemeldet). Wird die Datei als Ganzes
    /// abgelehnt, bleibt die Registry unverändert.
    /// </summary>
    public static GestureImportResult Import(CommandRegistry registry, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return GestureImportResult.Reject("Die Datei ist leer.");
        }

        if (json.Length > MaxFileChars)
        {
            return GestureImportResult.Reject("Die Datei ist zu groß.");
        }

        Dto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, Options);
        }
        catch (JsonException)
        {
            return GestureImportResult.Reject("Die Datei ist kein gültiges Tastenkürzel-Profil.");
        }

        if (dto is null || dto.SchemaVersion < 1)
        {
            return GestureImportResult.Reject("Die Formatversion des Profils fehlt oder ist ungültig.");
        }

        if (dto.SchemaVersion > CurrentSchemaVersion)
        {
            return GestureImportResult.Reject("Das Profil stammt aus einer neueren Programmversion.");
        }

        // Erst vollständig prüfen, dann anwenden.
        var skipped = new List<string>();
        var planned = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (id, gesture) in (dto.Gestures ?? []).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!registry.TryGet(id, out _))
            {
                skipped.Add($"Unbekannter Befehl „{id}“.");
                continue;
            }

            string? normalized;
            try
            {
                normalized = KeyGestureText.Normalize(gesture);
            }
            catch (FormatException ex)
            {
                skipped.Add($"{id}: {ex.Message}");
                continue;
            }

            planned[id] = normalized;
        }

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var c in registry.All)
        {
            result[c.Id] = planned.TryGetValue(c.Id, out var g) ? g : c.DefaultGesture;
        }

        // Konflikte auflösen: Wer eine Abweichung aus der Datei hat, verdrängt Standardbelegungen anderer Befehle;
        // zwei Abweichungen auf demselben Kürzel: der alphabetisch erste gewinnt.
        foreach (var group in result.Where(kv => kv.Value is not null).GroupBy(kv => kv.Value!, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1).ToList())
        {
            var winner = group.Select(kv => kv.Key).OrderBy(id => planned.ContainsKey(id) ? 0 : 1)
                .ThenBy(id => id, StringComparer.Ordinal).First();
            foreach (var loser in group.Select(kv => kv.Key).Where(id => id != winner))
            {
                result[loser] = null;
                skipped.Add($"{loser}: Kürzel {group.Key} ist schon durch {winner} belegt, Belegung entfernt.");
            }
        }

        var applied = 0;
        foreach (var command in registry.All)
        {
            if (command.Gesture != result[command.Id])
            {
                registry.SetGesture(command.Id, result[command.Id]);
                applied++;
            }
        }

        return new GestureImportResult(applied, skipped, false);
    }

    /// <summary>
    /// Belegt einen Befehl neu und nimmt das Kürzel allen anderen Befehlen weg. Liefert die IDs der Befehle, denen die
    /// Belegung entzogen wurde (für die Rückfrage im Editor). Ungültige Kürzel werfen <see cref="FormatException"/>.
    /// </summary>
    public static IReadOnlyList<string> AssignExclusive(CommandRegistry registry, string id, string? gesture)
    {
        var normalized = KeyGestureText.Normalize(gesture);
        registry.Get(id);
        var displaced = normalized is null
            ? []
            : registry.All.Where(c => c.Id != id && c.Gesture == normalized).Select(c => c.Id).ToList();
        foreach (var other in displaced)
        {
            registry.SetGesture(other, null);
        }

        registry.SetGesture(id, normalized);
        return displaced;
    }
}
