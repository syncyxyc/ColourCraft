using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrbitStudio.Application.Updates;

/// <summary>Veröffentlichtes Release mit den Dateien, die zur Aktualisierung gebraucht werden.</summary>
public sealed record ReleaseInfo(string TagName, string Version, string PageUrl, string? Notes, string ExeUrl, string? ChecksumUrl);

public enum UpdateStatus
{
    UpToDate,
    Available,
    Failed,
}

/// <summary>Ergebnis der Suche. Bei <see cref="UpdateStatus.Failed"/> steht der Grund in <see cref="Error"/>.</summary>
public sealed record UpdateCheckResult(UpdateStatus Status, ReleaseInfo? Release, string? Error);

/// <summary>
/// Sucht auf GitHub nach einem neueren Release (nur auf Anforderung des Nutzers, ohne Anmeldung und ohne Telemetrie).
/// Erwartet im Release die Datei <see cref="AssetName"/> und optional <see cref="ChecksumAssetName"/>.
/// </summary>
public sealed class UpdateChecker(HttpClient http)
{
    public const string Repository = "syncyxyc/ColourCraft";

    /// <summary>Name der Programmdatei im Release (muss dem Namen der EXE entsprechen).</summary>
    public const string AssetName = "OrbitStudio.exe";

    public const string ChecksumAssetName = "OrbitStudio.exe.sha256";

    public static Uri LatestReleaseUri { get; } = new($"https://api.github.com/repos/{Repository}/releases/latest");

    /// <summary>Wie lange die Suche höchstens dauern darf (der Download der EXE hat kein Zeitlimit).</summary>
    public static TimeSpan CheckTimeout { get; } = TimeSpan.FromSeconds(30);

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellation = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ORBIT-Studio", currentVersion));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(CheckTimeout);
        try
        {
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Failed("Es ist noch kein Release veröffentlicht.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failed($"Der Server antwortete mit Status {(int)response.StatusCode}.");
            }

            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            var release = ParseRelease(JsonNode.Parse(text) as JsonObject
                ?? throw new InvalidDataException("Antwort ist kein Objekt."));
            return IsNewer(release.Version, currentVersion)
                ? new UpdateCheckResult(UpdateStatus.Available, release, null)
                : new UpdateCheckResult(UpdateStatus.UpToDate, release, null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return Failed("Die Anfrage an GitHub hat zu lange gedauert.");
        }
        catch (HttpRequestException)
        {
            return Failed("Keine Verbindung zu GitHub. Bitte Internetverbindung prüfen.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or FormatException)
        {
            return Failed("Die Antwort von GitHub konnte nicht gelesen werden.");
        }
    }

    /// <summary>Liest tag_name, html_url, body und die Dateien aus der Release-Antwort der GitHub-API.</summary>
    public static ReleaseInfo ParseRelease(JsonObject json)
    {
        var tag = json["tag_name"]?.GetValue<string>() ?? throw new InvalidDataException("tag_name fehlt.");
        var version = NormalizeVersion(tag) ?? throw new InvalidDataException($"Ungültige Versionsnummer „{tag}“.");
        var page = json["html_url"]?.GetValue<string>() ?? throw new InvalidDataException("html_url fehlt.");
        var notes = json["body"]?.GetValue<string>();
        var assets = json["assets"]?.AsArray() ?? throw new InvalidDataException("assets fehlen.");

        string? exe = null;
        string? checksum = null;
        foreach (var asset in assets.OfType<JsonObject>())
        {
            var name = asset["name"]?.GetValue<string>();
            var url = asset["browser_download_url"]?.GetValue<string>();
            if (url is null)
            {
                continue;
            }

            if (name == AssetName)
            {
                exe = url;
            }
            else if (name == ChecksumAssetName)
            {
                checksum = url;
            }
        }

        if (exe is null)
        {
            throw new InvalidDataException($"Das Release enthält keine Datei „{AssetName}“.");
        }

        return new ReleaseInfo(tag, version, page, notes, exe, checksum);
    }

    /// <summary>True, wenn <paramref name="candidate"/> eine höhere Versionsnummer hat als <paramref name="current"/>.</summary>
    public static bool IsNewer(string candidate, string current)
    {
        var a = NormalizeVersion(candidate);
        var b = NormalizeVersion(current);
        return a is not null && b is not null && Version.Parse(a) > Version.Parse(b);
    }

    /// <summary>„v1.2.3“ oder „1.2“ als Versionsnummer mit mindestens drei Teilen; sonst null.</summary>
    public static string? NormalizeVersion(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(text, out var version))
        {
            return null;
        }

        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }

    /// <summary>Liest die SHA-256-Summe aus einer Datei im Format „&lt;hex&gt;  OrbitStudio.exe“ (sha256sum).</summary>
    public static string? ParseSha256(string text)
    {
        var token = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return token is { Length: 64 } && token.All(Uri.IsHexDigit) ? token.ToLowerInvariant() : null;
    }

    /// <summary>Liest eine kleine Textdatei (z. B. die Prüfsumme) von der angegebenen Adresse.</summary>
    public async Task<string> GetTextAsync(string url, CancellationToken cancellation = default)
    {
        using var response = await http.GetAsync(url, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);
    }

    /// <summary>Lädt eine Datei herunter. Die Zieldatei entsteht erst, wenn der Download vollständig ist.</summary>
    public async Task DownloadAsync(string url, string targetPath, CancellationToken cancellation = default)
    {
        var temp = targetPath + ".part";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false))
        await using (var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(target, cancellation).ConfigureAwait(false);
        }

        File.Move(temp, targetPath, overwrite: true);
    }

    /// <summary>Vergleicht die SHA-256-Summe einer Datei mit dem erwarteten Wert.</summary>
    public static async Task<bool> VerifyAsync(string path, string expectedHex, CancellationToken cancellation = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var hash = await SHA256.HashDataAsync(stream, cancellation).ConfigureAwait(false);
        return string.Equals(Convert.ToHexStringLower(hash), expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    private static UpdateCheckResult Failed(string message) => new(UpdateStatus.Failed, null, message);
}
