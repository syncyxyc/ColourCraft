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
    public void AppInfo_ProvidesProductNameAndVersion()
    {
        AppInfo.ProductName.ShouldBe("ORBIT Studio");
        AppInfo.Version.ShouldNotBeNullOrWhiteSpace();
        AppInfo.ProjectFileExtension.ShouldBe(".orbit");
    }
}
