using OrbitStudio.Application.Commands;

namespace OrbitStudio.Application.Tests;

public class GestureProfileTests
{
    private static CommandRegistry Registry()
    {
        var registry = new CommandRegistry();
        registry.Register(AppCommand.Create("file.save", "K", () => { }, gesture: "ctrl+s"));
        registry.Register(AppCommand.Create("file.open", "K", () => { }, gesture: "ctrl+o"));
        registry.Register(AppCommand.Create("edit.undo", "K", () => { }, gesture: "ctrl+z"));
        registry.Register(AppCommand.Create("view.zoom", "K", () => { }));
        return registry;
    }

    [Fact]
    public void Export_ContainsOnlyDeviationsFromDefault()
    {
        var registry = Registry();
        GestureProfile.Export(registry).ShouldNotContain("file.save");

        registry.SetGesture("file.save", "ctrl+shift+s");
        registry.SetGesture("edit.undo", null);
        var json = GestureProfile.Export(registry);
        json.ShouldContain("Ctrl+Shift+S");
        json.ShouldContain("edit.undo");
        json.ShouldNotContain("file.open");
    }

    [Fact]
    public void ExportThenImport_RestoresTheSameAssignments_ToFreshRegistry()
    {
        var source = Registry();
        source.SetGesture("file.save", "ctrl+shift+s");
        source.SetGesture("edit.undo", null);
        source.SetGesture("view.zoom", "F5");

        var target = Registry();
        var result = GestureProfile.Import(target, GestureProfile.Export(source));
        result.Rejected.ShouldBeFalse();
        result.Skipped.ShouldBeEmpty();
        result.Applied.ShouldBe(3);
        foreach (var c in source.All)
        {
            target.Get(c.Id).Gesture.ShouldBe(c.Gesture, c.Id);
        }
    }

    [Fact]
    public void Import_ResetsOtherCommandsToDefault_BeforeApplying()
    {
        var registry = Registry();
        registry.SetGesture("file.open", "F9");
        var result = GestureProfile.Import(registry, "{\"schemaVersion\":1,\"gestures\":{\"view.zoom\":\"F5\"}}");
        registry.Get("file.open").Gesture.ShouldBe("Ctrl+O");
        registry.Get("view.zoom").Gesture.ShouldBe("F5");
        result.Applied.ShouldBe(2);
    }

    [Fact]
    public void Import_SkipsUnknownCommandsAndInvalidGestures_ButAppliesTheRest()
    {
        var registry = Registry();
        var result = GestureProfile.Import(registry,
            "{\"schemaVersion\":1,\"gestures\":{\"gibt.es.nicht\":\"F1\",\"file.save\":\"Ctrl+Foo+S\",\"view.zoom\":\"F5\",\"file.open\":\"Ctrl+Umschalt+O\"}}");
        result.Rejected.ShouldBeFalse();
        result.Skipped.Count.ShouldBe(2);
        result.Skipped.ShouldContain(s => s.Contains("gibt.es.nicht"));
        registry.Get("file.save").Gesture.ShouldBe("Ctrl+S");          // ungültig → unverändert (Standard)
        registry.Get("view.zoom").Gesture.ShouldBe("F5");
        registry.Get("file.open").Gesture.ShouldBe("Ctrl+Shift+O");    // „Umschalt“ wird erkannt
    }

    [Fact]
    public void Import_ResolvesConflicts_FileAssignmentWinsOverDefault()
    {
        var registry = Registry();
        var result = GestureProfile.Import(registry, "{\"schemaVersion\":1,\"gestures\":{\"view.zoom\":\"ctrl+s\"}}");
        registry.Get("view.zoom").Gesture.ShouldBe("Ctrl+S");
        registry.Get("file.save").Gesture.ShouldBeNull();
        result.Skipped.ShouldContain(s => s.StartsWith("file.save"));
        registry.FindGestureConflicts().ShouldBeEmpty();
    }

    [Fact]
    public void Import_TwoFileAssignmentsOnSameGesture_AlphabeticallyFirstWins()
    {
        var registry = Registry();
        GestureProfile.Import(registry, "{\"schemaVersion\":1,\"gestures\":{\"view.zoom\":\"F5\",\"edit.undo\":\"F5\"}}");
        registry.Get("edit.undo").Gesture.ShouldBe("F5");
        registry.Get("view.zoom").Gesture.ShouldBeNull();
        registry.FindGestureConflicts().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":99,\"gestures\":{}}")]
    public void Import_BrokenOrNewerFiles_AreRejected_AndLeaveRegistryUntouched(string? json)
    {
        var registry = Registry();
        registry.SetGesture("view.zoom", "F5");
        var result = GestureProfile.Import(registry, json);
        result.Rejected.ShouldBeTrue();
        result.Applied.ShouldBe(0);
        registry.Get("view.zoom").Gesture.ShouldBe("F5");
    }

    [Fact]
    public void AssignExclusive_TakesGestureAwayFromOthers_AndReportsThem()
    {
        var registry = Registry();
        var displaced = GestureProfile.AssignExclusive(registry, "view.zoom", "CTRL+S");
        displaced.ShouldBe(["file.save"]);
        registry.Get("view.zoom").Gesture.ShouldBe("Ctrl+S");
        registry.Get("file.save").Gesture.ShouldBeNull();
        registry.FindGestureConflicts().ShouldBeEmpty();
    }

    [Fact]
    public void AssignExclusive_NullClearsOnlyThatCommand_AndInvalidInputThrows()
    {
        var registry = Registry();
        GestureProfile.AssignExclusive(registry, "file.save", null).ShouldBeEmpty();
        registry.Get("file.save").Gesture.ShouldBeNull();
        registry.Get("file.open").Gesture.ShouldBe("Ctrl+O");
        Should.Throw<FormatException>(() => GestureProfile.AssignExclusive(registry, "file.save", "Ctrl+Foo+S"));
        Should.Throw<KeyNotFoundException>(() => GestureProfile.AssignExclusive(registry, "gibt.es.nicht", "F1"));
        GestureProfile.AssignExclusive(registry, "file.save", "Ctrl+S").ShouldBeEmpty(); // nichts verdrängt
    }
}
