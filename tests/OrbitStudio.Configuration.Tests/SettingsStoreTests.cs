using OrbitStudio.Configuration;

namespace OrbitStudio.Configuration.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "orbit-settings-tests-" + Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public SettingsStoreTests()
    {
        _paths = new AppPaths(Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var result = SettingsStores.Global(_paths).Load();

        result.Status.ShouldBe(SettingsLoadStatus.Missing);
        result.Value.ShouldBe(new GlobalSettings());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var store = SettingsStores.Workspace(_paths);
        var original = new WorkspaceSettings
        {
            WindowLeft = 100, WindowTop = 50, WindowWidth = 1500, WindowHeight = 900, IsMaximized = true,
        };

        store.Save(original);
        var result = store.Load();

        result.Status.ShouldBe(SettingsLoadStatus.Loaded);
        result.Value.ShouldBe(original);
        File.Exists(store.FilePath + ".tmp").ShouldBeFalse("die temporäre Datei muss ersetzt worden sein");
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaultsAndCreatesBackup()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.GlobalSettingsFile, "{ das ist kein JSON");

        var result = SettingsStores.Global(_paths).Load();

        result.Status.ShouldBe(SettingsLoadStatus.Invalid);
        result.Value.ShouldBe(new GlobalSettings());
        result.BackupPath.ShouldNotBeNull();
        File.ReadAllText(result.BackupPath).ShouldBe("{ das ist kein JSON");
    }

    [Fact]
    public void Load_JsonNull_IsInvalid()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.GlobalSettingsFile, "null");

        SettingsStores.Global(_paths).Load().Status.ShouldBe(SettingsLoadStatus.Invalid);
    }

    [Fact]
    public void Load_NewerSchemaVersion_LeavesFileUntouched()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        const string json = """{ "schemaVersion": 99, "language": "de-DE" }""";
        File.WriteAllText(_paths.GlobalSettingsFile, json);

        var result = SettingsStores.Global(_paths).Load();

        result.Status.ShouldBe(SettingsLoadStatus.NewerVersion);
        result.Value.ShouldBe(new GlobalSettings());
        File.ReadAllText(_paths.GlobalSettingsFile).ShouldBe(json);
    }

    [Fact]
    public void Load_SchemaVersionZero_IsInvalid()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.GlobalSettingsFile, """{ "schemaVersion": 0 }""");

        SettingsStores.Global(_paths).Load().Status.ShouldBe(SettingsLoadStatus.Invalid);
    }

    [Fact]
    public void Load_InvalidValues_AreCorrected()
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        File.WriteAllText(_paths.WorkspaceSettingsFile,
            """{ "schemaVersion": 1, "windowWidth": -5, "windowHeight": 100000, "windowLeft": 10, "windowTop": 20 }""");

        var result = SettingsStores.Workspace(_paths).Load();

        result.Status.ShouldBe(SettingsLoadStatus.Corrected);
        result.Value.WindowWidth.ShouldBe(new WorkspaceSettings().WindowWidth);
        result.Value.WindowHeight.ShouldBe(new WorkspaceSettings().WindowHeight);
        result.Value.WindowLeft.ShouldBe(10);
        result.Value.WindowTop.ShouldBe(20);
    }

    [Fact]
    public void GlobalNormalize_DeduplicatesAndLimitsRecentFiles()
    {
        var files = Enumerable.Range(0, 15).Select(i => $@"C:\bilder\{i}.png").Prepend(@"C:\BILDER\0.png").Append(" ");
        var normalized = GlobalSettings.Normalize(new GlobalSettings { RecentFiles = files.ToList() }, out var changed);

        changed.ShouldBeTrue();
        normalized.RecentFiles.Count.ShouldBe(GlobalSettings.MaxRecentFiles);
        normalized.RecentFiles[0].ShouldBe(@"C:\BILDER\0.png");
        normalized.RecentFiles.ShouldNotContain(@"C:\bilder\0.png");
    }

    [Fact]
    public void GlobalNormalize_UnsupportedLanguage_FallsBackToGerman()
    {
        var normalized = GlobalSettings.Normalize(new GlobalSettings { Language = "xx-XX" }, out var changed);

        changed.ShouldBeTrue();
        normalized.Language.ShouldBe("de-DE");
    }

    [Fact]
    public void GlobalNormalize_ValidSettings_AreUnchanged()
    {
        var settings = new GlobalSettings { RecentFiles = [@"C:\a.png"] };
        GlobalSettings.Normalize(settings, out var changed);
        changed.ShouldBeFalse();
    }

    [Fact]
    public void WithRecentFile_MovesExistingEntryToFrontCaseInsensitive()
    {
        var settings = new GlobalSettings { RecentFiles = [@"C:\a.png", @"C:\b.png"] };

        var result = settings.WithRecentFile(@"C:\B.PNG");

        result.RecentFiles.ShouldBe([@"C:\B.PNG", @"C:\a.png"]);
    }

    [Fact]
    public void WithRecentFile_KeepsAtMostTenEntries()
    {
        var settings = new GlobalSettings();
        for (var i = 0; i < 15; i++)
        {
            settings = settings.WithRecentFile($@"C:\{i}.png");
        }

        settings.RecentFiles.Count.ShouldBe(GlobalSettings.MaxRecentFiles);
        settings.RecentFiles[0].ShouldBe(@"C:\14.png");
    }

    [Fact]
    public void WithoutRecentFile_RemovesEntry()
    {
        new GlobalSettings { RecentFiles = [@"C:\a.png", @"C:\b.png"] }
            .WithoutRecentFile(@"c:\A.png").RecentFiles.ShouldBe([@"C:\b.png"]);
    }

    [Theory]
    [InlineData(0, 92)]
    [InlineData(101, 92)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    public void JpegQuality_IsNormalized(int input, int expected)
    {
        GlobalSettings.Normalize(new GlobalSettings { JpegQuality = input }, out _).JpegQuality.ShouldBe(expected);
    }

    [Fact]
    public void Workspace_PixelGridDefaultsOn_AndRoundTrips()
    {
        new WorkspaceSettings().ShowPixelGrid.ShouldBeTrue();
        var store = SettingsStores.Workspace(_paths);
        store.Save(new WorkspaceSettings { ShowPixelGrid = false });
        store.Load().Value.ShowPixelGrid.ShouldBeFalse();
    }

    [Fact]
    public void Workspace_Swatches_DefaultToNull_AndRoundTrip_IncludingEmptyPalette()
    {
        new WorkspaceSettings().Swatches.ShouldBeNull();
        var store = SettingsStores.Workspace(_paths);

        store.Save(new WorkspaceSettings { Swatches = ["#FF0000", "#00FF0080"] });
        var loaded = store.Load();
        loaded.Status.ShouldBe(SettingsLoadStatus.Loaded);
        loaded.Value.Swatches.ShouldBe(["#FF0000", "#00FF0080"]);

        // Absichtlich geleerte Palette bleibt leer (nicht wieder die Standardpalette).
        store.Save(new WorkspaceSettings { Swatches = [] });
        store.Load().Value.Swatches.ShouldBe([]);
    }

    [Fact]
    public void Workspace_Swatches_InvalidAndDuplicateEntriesAreDropped_AndReportedAsCorrected()
    {
        var normalized = WorkspaceSettings.Normalize(
            new WorkspaceSettings { Swatches = ["#ff0000", "#FF0000", "rot", "#12345", "#GG0000", "#0000FF"] }, out var changed);
        changed.ShouldBeTrue();
        normalized.Swatches.ShouldBe(["#FF0000", "#0000FF"]);

        WorkspaceSettings.Normalize(new WorkspaceSettings { Swatches = ["#FF0000", "#0000FF"] }, out var unchanged);
        unchanged.ShouldBeFalse();
    }

    [Fact]
    public void Workspace_Swatches_AreCappedAtMaximum()
    {
        var many = Enumerable.Range(0, WorkspaceSettings.MaxSwatches + 20).Select(i => $"#{i:X6}").ToList();
        var normalized = WorkspaceSettings.Normalize(new WorkspaceSettings { Swatches = many }, out var changed);
        changed.ShouldBeTrue();
        normalized.Swatches!.Count.ShouldBe(WorkspaceSettings.MaxSwatches);
    }

    [Fact]
    public void AppInfo_ProvidesProductNameAndVersion()
    {
        AppInfo.ProductName.ShouldBe("ORBIT Studio");
        AppInfo.Version.ShouldNotBeNullOrWhiteSpace();
        AppInfo.ProjectFileExtension.ShouldBe(".orbit");
    }
}
