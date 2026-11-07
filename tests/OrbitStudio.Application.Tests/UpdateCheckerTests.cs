using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OrbitStudio.Application.Updates;

namespace OrbitStudio.Application.Tests;

/// <summary>Antwortet immer mit derselben Antwort (kein echtes Netzwerk).</summary>
internal sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}

internal sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(exception);
}

public class UpdateCheckerTests
{
    private const string ExeUrl = "https://github.com/syncyxyc/ColourCraft/releases/download/v0.9.0/OrbitStudio.exe";
    private const string ShaUrl = "https://github.com/syncyxyc/ColourCraft/releases/download/v0.9.0/OrbitStudio.exe.sha256";

    private static string ReleaseJson(string tag, bool withExe = true) => $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "https://github.com/syncyxyc/ColourCraft/releases/tag/{{tag}}",
          "body": "Neue Funktionen",
          "assets": [
            {{(withExe ? $$"""{ "name": "OrbitStudio.exe", "browser_download_url": "{{ExeUrl}}" },""" : "")}}
            { "name": "OrbitStudio.exe.sha256", "browser_download_url": "{{ShaUrl}}" },
            { "name": "andere.zip", "browser_download_url": "https://example.invalid/andere.zip" }
          ]
        }
        """;

    private static UpdateChecker Checker(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Theory]
    [InlineData("v0.9.0", "0.8.0", true)]
    [InlineData("0.8.1", "0.8.0", true)]
    [InlineData("v1.0", "0.9.9", true)]
    [InlineData("v0.8.0", "0.8.0", false)]
    [InlineData("v0.7.0", "0.8.0", false)]
    [InlineData("kaputt", "0.8.0", false)]
    public void IsNewer_ComparesNumerically_NotAsText(string candidate, string current, bool expected) =>
        UpdateChecker.IsNewer(candidate, current).ShouldBe(expected);

    [Fact]
    public void NormalizeVersion_AcceptsPrefixAndShortForms()
    {
        UpdateChecker.NormalizeVersion("v0.8").ShouldBe("0.8.0");
        UpdateChecker.NormalizeVersion("V1.2.3").ShouldBe("1.2.3");
        UpdateChecker.NormalizeVersion("main").ShouldBeNull();
    }

    [Fact]
    public void ParseRelease_FindsExeAndChecksum()
    {
        var release = UpdateChecker.ParseRelease(JsonNode.Parse(ReleaseJson("v0.9.0"))!.AsObject());

        release.Version.ShouldBe("0.9.0");
        release.ExeUrl.ShouldBe(ExeUrl);
        release.ChecksumUrl.ShouldBe(ShaUrl);
        release.Notes.ShouldBe("Neue Funktionen");
    }

    [Fact]
    public void ParseRelease_WithoutExe_IsRejected() =>
        Should.Throw<InvalidDataException>(() =>
            UpdateChecker.ParseRelease(JsonNode.Parse(ReleaseJson("v0.9.0", withExe: false))!.AsObject()));

    [Fact]
    public void ParseSha256_ReadsHashFromSha256sumFormat()
    {
        var hash = new string('a', 64);
        UpdateChecker.ParseSha256($"{hash}  OrbitStudio.exe\n").ShouldBe(hash);
        UpdateChecker.ParseSha256("zu kurz").ShouldBeNull();
        UpdateChecker.ParseSha256(new string('z', 64)).ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_NewerRelease_IsAvailable_AndUsesGitHubApi()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, ReleaseJson("v0.9.0"));

        var result = await Checker(handler).CheckAsync("0.8.0");

        result.Status.ShouldBe(UpdateStatus.Available);
        result.Release!.Version.ShouldBe("0.9.0");
        handler.LastRequest!.RequestUri!.ToString().ShouldBe("https://api.github.com/repos/syncyxyc/ColourCraft/releases/latest");
        handler.LastRequest.Headers.UserAgent.ToString().ShouldStartWith("ORBIT-Studio/");
    }

    [Fact]
    public async Task CheckAsync_SameVersion_IsUpToDate()
    {
        var result = await Checker(new FakeHandler(HttpStatusCode.OK, ReleaseJson("v0.8.0"))).CheckAsync("0.8.0");

        result.Status.ShouldBe(UpdateStatus.UpToDate);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_NoReleaseYet_ReportsReadableError()
    {
        var result = await Checker(new FakeHandler(HttpStatusCode.NotFound, "{}")).CheckAsync("0.8.0");

        result.Status.ShouldBe(UpdateStatus.Failed);
        result.Error!.ShouldContain("noch kein Release");
    }

    [Fact]
    public async Task CheckAsync_ServerError_ReportsStatusCode()
    {
        var result = await Checker(new FakeHandler(HttpStatusCode.Forbidden, "{}")).CheckAsync("0.8.0");

        result.Status.ShouldBe(UpdateStatus.Failed);
        result.Error!.ShouldContain("403");
    }

    [Fact]
    public async Task CheckAsync_BrokenJson_IsFailedNotException()
    {
        var result = await Checker(new FakeHandler(HttpStatusCode.OK, "{ kaputt")).CheckAsync("0.8.0");

        result.Status.ShouldBe(UpdateStatus.Failed);
        result.Error!.ShouldContain("nicht gelesen");
    }

    [Fact]
    public async Task CheckAsync_NoConnection_IsFailedWithGermanMessage()
    {
        var result = await Checker(new ThrowingHandler(new HttpRequestException("offline"))).CheckAsync("0.8.0");

        result.Status.ShouldBe(UpdateStatus.Failed);
        result.Error!.ShouldContain("Keine Verbindung");
    }

    [Fact]
    public async Task CheckAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            Checker(new ThrowingHandler(new OperationCanceledException())).CheckAsync("0.8.0", cts.Token));
    }

    [Fact]
    public async Task Verify_AcceptsMatchingHash_AndRejectsOtherContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orbit-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "neu.exe");
            var bytes = Encoding.UTF8.GetBytes("programm");
            await File.WriteAllBytesAsync(file, bytes);
            var expected = Convert.ToHexStringLower(SHA256.HashData(bytes));

            (await UpdateChecker.VerifyAsync(file, expected)).ShouldBeTrue();
            (await UpdateChecker.VerifyAsync(file, new string('0', 64))).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class UpdateInstallerTests
{
    [Fact]
    public void WriteScript_WaitsForProcess_MovesWithRetries_AndRestarts()
    {
        var dir = Path.Combine(Path.GetTempPath(), "orbit-script-" + Guid.NewGuid().ToString("N"));
        try
        {
            var script = Path.Combine(dir, UpdateInstaller.ScriptName);
            UpdateInstaller.WriteScript(script, 4242, @"C:\Temp\neu.exe", @"C:\Programme\OrbitStudio\OrbitStudio.exe");

            var text = File.ReadAllText(script);
            text.ShouldContain("set \"PID=4242\"");
            text.ShouldContain("set \"NEW=C:\\Temp\\neu.exe\"");
            text.ShouldContain("set \"TARGET=C:\\Programme\\OrbitStudio\\OrbitStudio.exe\"");
            text.ShouldContain("tasklist /FI \"PID eq %PID%\"");
            text.ShouldContain("move /Y \"%NEW%\" \"%TARGET%\"");
            text.ShouldContain("GEQ 30");
            text.ShouldContain("start \"\" \"%TARGET%\"");
            text.ShouldContain("del \"%~f0\"");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("C:\\Benutzer\\Lukas Müller\\neu.exe")]
    [InlineData("C:\\a\"b\\neu.exe")]
    [InlineData("C:\\100%\\neu.exe")]
    public void WriteScript_RejectsPathsTheScriptCannotHandle(string newFile) =>
        Should.Throw<ArgumentException>(() =>
            UpdateInstaller.WriteScript(Path.Combine(Path.GetTempPath(), "x.cmd"), 1, newFile, @"C:\OrbitStudio.exe"));
}
