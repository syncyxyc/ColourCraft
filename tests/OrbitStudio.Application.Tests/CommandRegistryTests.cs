using OrbitStudio.Application.Commands;

namespace OrbitStudio.Application.Tests;

public class CommandRegistryTests
{
    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = new CommandRegistry();
        registry.Register(AppCommand.Create("a", "Key.A", () => { }));

        Should.Throw<InvalidOperationException>(() => registry.Register(AppCommand.Create("a", "Key.B", () => { })));
    }

    [Fact]
    public void Get_Unknown_Throws()
    {
        Should.Throw<KeyNotFoundException>(() => new CommandRegistry().Get("fehlt"));
    }

    [Fact]
    public void Execute_RespectsCanExecute()
    {
        var enabled = false;
        var executed = 0;
        var command = AppCommand.Create("a", "Key.A", () => executed++, () => enabled);

        command.Execute();
        executed.ShouldBe(0);

        enabled = true;
        command.CanExecute().ShouldBeTrue();
        command.Execute();
        executed.ShouldBe(1);
    }

    [Fact]
    public void PlannedCommand_IsDisabledAndNamesMilestone()
    {
        var command = AppCommand.Planned("edit.undo", "Cmd.Edit.Undo", "M4", "ctrl+z");

        command.IsImplemented.ShouldBeFalse();
        command.CanExecute().ShouldBeFalse();
        command.PlannedMilestone.ShouldBe("M4");
        command.Gesture.ShouldBe("Ctrl+Z");
        command.Execute(); // darf nichts tun und nicht werfen
    }

    [Fact]
    public void FindGestureConflicts_DetectsDifferentSpellings()
    {
        var registry = new CommandRegistry();
        registry.Register(AppCommand.Create("a", "K", () => { }, gesture: "Ctrl+Shift+S"));
        registry.Register(AppCommand.Create("b", "K", () => { }, gesture: "shift+ctrl+s"));
        registry.Register(AppCommand.Create("c", "K", () => { }, gesture: "Ctrl+S"));

        var conflicts = registry.FindGestureConflicts();

        conflicts.Count.ShouldBe(1);
        conflicts[0].Gesture.ShouldBe("Ctrl+Shift+S");
        conflicts[0].CommandIds.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void SetGesture_And_Reset()
    {
        var registry = new CommandRegistry();
        registry.Register(AppCommand.Create("a", "K", () => { }, gesture: "Ctrl+N"));

        registry.SetGesture("a", "alt+n");
        registry.Get("a").Gesture.ShouldBe("Alt+N");

        registry.SetGesture("a", null);
        registry.Get("a").Gesture.ShouldBeNull();

        registry.ResetGestures();
        registry.Get("a").Gesture.ShouldBe("Ctrl+N");
    }

    [Fact]
    public void NotifyStateChanged_RaisesRequery()
    {
        var registry = new CommandRegistry();
        var raised = false;
        registry.RequerySuggested += (_, _) => raised = true;

        registry.NotifyStateChanged();

        raised.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ctrl+n", "Ctrl+N")]
    [InlineData("Strg+Umschalt+s", "Ctrl+Shift+S")]
    [InlineData("alt+ctrl+Delete", "Ctrl+Alt+Delete")]
    [InlineData("F11", "F11")]
    [InlineData("ctrl+OemPlus", "Ctrl+OemPlus")]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void KeyGestureText_Normalizes(string? input, string? expected)
    {
        KeyGestureText.Normalize(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Ctrl+Shift")]
    [InlineData("Hyper+A")]
    public void KeyGestureText_RejectsInvalid(string input)
    {
        Should.Throw<FormatException>(() => KeyGestureText.Normalize(input));
    }
}
