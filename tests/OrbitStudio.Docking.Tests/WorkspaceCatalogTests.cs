namespace OrbitStudio.Docking.Tests;

public class WorkspaceCatalogTests
{
    private static readonly PanelLayout Custom = PanelLayout.Default.SetVisible(PanelIds.Layers, false)
        .Move(PanelIds.History, DockSide.Left, 0).Resize(PanelIds.Colors, 400);

    [Fact]
    public void Empty_ShowsDefaultLayout_WithDefaultActive()
    {
        WorkspaceCatalog.Empty.Current.ShouldBe(PanelLayout.Default);
        WorkspaceCatalog.Empty.ActiveName.ShouldBe(WorkspaceCatalog.DefaultName);
        WorkspaceCatalog.Empty.Names.ShouldBeEmpty();
    }

    [Fact]
    public void WithCurrent_ClearsActiveName_ButNotWhenUnchanged()
    {
        var changed = WorkspaceCatalog.Empty.WithCurrent(Custom);
        changed.Current.ShouldBe(Custom);
        changed.ActiveName.ShouldBeNull();
        WorkspaceCatalog.Empty.WithCurrent(PanelLayout.Default).ShouldBeSameAs(WorkspaceCatalog.Empty);
    }

    [Fact]
    public void SaveLoadReset_WorkTogether()
    {
        var saved = WorkspaceCatalog.Empty.WithCurrent(Custom).SaveCurrentAs("  Malen ");
        saved.Names.ShouldBe(["Malen"]);
        saved.ActiveName.ShouldBe("Malen");

        var reset = saved.ResetCurrent();
        reset.Current.ShouldBe(PanelLayout.Default);
        reset.ActiveName.ShouldBe(WorkspaceCatalog.DefaultName);

        var loaded = reset.Load("malen"); // Groß-/Kleinschreibung egal
        loaded.Current.ShouldBe(Custom);
        loaded.ActiveName.ShouldBe("Malen");
        loaded.Load("standard").Current.ShouldBe(PanelLayout.Default);
        Should.Throw<KeyNotFoundException>(() => reset.Load("gibtEsNicht"));
    }

    [Fact]
    public void SaveCurrentAs_OverwritesSameNameIgnoringCase_AndRespectsLimit()
    {
        var one = WorkspaceCatalog.Empty.WithCurrent(Custom).SaveCurrentAs("Malen");
        var again = one.WithCurrent(PanelLayout.Default).SaveCurrentAs("MALEN");
        again.Names.ShouldBe(["MALEN"]);
        again.Load("malen").Current.ShouldBe(PanelLayout.Default);

        var full = WorkspaceCatalog.Empty;
        for (var i = 0; i < WorkspaceCatalog.MaxWorkspaces; i++)
        {
            full = full.SaveCurrentAs($"Bereich {i}");
        }

        Should.Throw<InvalidOperationException>(() => full.SaveCurrentAs("Einer zu viel"));
        full.SaveCurrentAs("bereich 3").Names.Count.ShouldBe(WorkspaceCatalog.MaxWorkspaces); // Überschreiben geht trotzdem
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Standard")]
    [InlineData("standard")]
    [InlineData("Zeilen\numbruch")]
    public void InvalidNames_AreRejected(string? name)
    {
        WorkspaceCatalog.ValidateName(name).ShouldNotBeNull();
        Should.Throw<ArgumentException>(() => WorkspaceCatalog.Empty.SaveCurrentAs(name!));
    }

    [Fact]
    public void TooLongName_IsRejected()
    {
        WorkspaceCatalog.ValidateName(new string('a', WorkspaceCatalog.MaxNameLength)).ShouldBeNull();
        WorkspaceCatalog.ValidateName(new string('a', WorkspaceCatalog.MaxNameLength + 1)).ShouldNotBeNull();
    }

    [Fact]
    public void Rename_KeepsLayout_UpdatesActiveName_AndRejectsConflicts()
    {
        var catalog = WorkspaceCatalog.Empty.WithCurrent(Custom).SaveCurrentAs("A")
            .WithCurrent(PanelLayout.Default).SaveCurrentAs("B");
        var renamed = catalog.Rename("B", "Bild");
        renamed.Names.ShouldBe(["A", "Bild"]);
        renamed.ActiveName.ShouldBe("Bild");
        renamed.Load("Bild").Current.ShouldBe(PanelLayout.Default);
        renamed.Rename("Bild", "BILD").Names.ShouldBe(["A", "BILD"]); // nur Schreibweise ändern ist erlaubt
        Should.Throw<InvalidOperationException>(() => catalog.Rename("B", "a"));
        Should.Throw<ArgumentException>(() => catalog.Rename("B", "Standard"));
        Should.Throw<KeyNotFoundException>(() => catalog.Rename("X", "Y"));
    }

    [Fact]
    public void Delete_RemovesWorkspace_AndProtectsDefault()
    {
        var catalog = WorkspaceCatalog.Empty.SaveCurrentAs("A").SaveCurrentAs("B");
        var deleted = catalog.Delete("b");
        deleted.Names.ShouldBe(["A"]);
        deleted.ActiveName.ShouldBeNull(); // B war aktiv
        deleted.Current.ShouldBe(PanelLayout.Default);      // Layout bleibt stehen
        catalog.Delete("A").ActiveName.ShouldBe("B");
        Should.Throw<InvalidOperationException>(() => catalog.Delete("Standard"));
        Should.Throw<KeyNotFoundException>(() => catalog.Delete("X"));
    }

    [Fact]
    public void Json_RoundTrips_Everything()
    {
        var catalog = WorkspaceCatalog.Empty.WithCurrent(Custom).SaveCurrentAs("Malen")
            .WithCurrent(PanelLayout.Default.Resize(PanelIds.Layers, 321)).SaveCurrentAs("Retusche")
            .WithCurrent(Custom.SetVisible(PanelIds.Navigator, false));
        var (loaded, status) = WorkspaceCatalog.FromJson(catalog.ToJson());
        status.ShouldBe(WorkspaceLoadStatus.Loaded);
        loaded.Current.ShouldBe(catalog.Current);
        loaded.ActiveName.ShouldBeNull();
        loaded.Names.ShouldBe(["Malen", "Retusche"]);
        loaded.Load("Malen").Current.ShouldBe(Custom);
        loaded.Load("Retusche").Current.Get(PanelIds.Layers).Size.ShouldBe(321);
    }

    [Fact]
    public void Json_Active_Name_Survives()
    {
        var (loaded, _) = WorkspaceCatalog.FromJson(WorkspaceCatalog.Empty.WithCurrent(Custom).SaveCurrentAs("Malen").ToJson());
        loaded.ActiveName.ShouldBe("Malen");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("[1,2,3]")]
    [InlineData("{\"schemaVersion\":0}")]
    public void Json_Broken_FallsBackToDefault(string? json)
    {
        var (catalog, status) = WorkspaceCatalog.FromJson(json);
        status.ShouldBe(WorkspaceLoadStatus.Invalid);
        catalog.Current.ShouldBe(PanelLayout.Default);
        catalog.Names.ShouldBeEmpty();
    }

    [Fact]
    public void Json_NewerVersion_IsNotInterpreted()
    {
        var (catalog, status) = WorkspaceCatalog.FromJson("{\"schemaVersion\":99,\"workspaces\":[]}");
        status.ShouldBe(WorkspaceLoadStatus.NewerVersion);
        catalog.Current.ShouldBe(PanelLayout.Default);
    }

    [Fact]
    public void Json_PartiallyBroken_IsRepaired_AndReported()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "current": [ { "id": "layers", "side": "left", "visible": true, "size": 99999 } ],
              "activeName": "Gibt es nicht",
              "workspaces": [
                { "name": "Gut", "panels": [ { "id": "colors", "side": "bottom", "visible": true, "size": 200 } ] },
                { "name": "gut", "panels": [] },
                { "name": "Standard", "panels": [] },
                { "name": "", "panels": [] },
                null
              ]
            }
            """;
        var (catalog, status) = WorkspaceCatalog.FromJson(json);
        status.ShouldBe(WorkspaceLoadStatus.Corrected);
        catalog.Names.ShouldBe(["Gut"]);
        catalog.ActiveName.ShouldBeNull();
        PanelLayout.Validate(catalog.Current.Panels).ShouldBeNull();
        catalog.Current.Get(PanelIds.Layers).Size.ShouldBe(PanelLayout.MaxSize);
        var good = catalog.Load("Gut").Current;
        PanelLayout.Validate(good.Panels).ShouldBeNull();
        good.Get(PanelIds.Colors).Side.ShouldBe(DockSide.Bottom);
    }

    [Fact]
    public void Json_NewPanelInLaterVersion_IsAddedWithDefaults()
    {
        // Alte Datei kennt „navigator“ und „effectBrowser“ noch nicht.
        const string json = """
            { "schemaVersion": 1, "current": [
              { "id": "tools", "side": "left", "visible": true, "size": 64 },
              { "id": "toolOptions", "side": "top", "visible": true, "size": 120 },
              { "id": "layers", "side": "right", "visible": true, "size": 280 },
              { "id": "colors", "side": "right", "visible": true, "size": 280 },
              { "id": "history", "side": "right", "visible": true, "size": 280 },
              { "id": "properties", "side": "right", "visible": false, "size": 280 } ] }
            """;
        var (catalog, status) = WorkspaceCatalog.FromJson(json);
        status.ShouldBe(WorkspaceLoadStatus.Corrected);
        catalog.Current.Get(PanelIds.Navigator).ShouldBe(PanelLayout.Default.Get(PanelIds.Navigator));
        catalog.Current.Panels.Count.ShouldBe(PanelIds.All.Count);
    }
}
