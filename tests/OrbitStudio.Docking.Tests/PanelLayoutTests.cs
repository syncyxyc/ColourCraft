namespace OrbitStudio.Docking.Tests;

public class PanelLayoutTests
{
    private static IEnumerable<string> Ids(PanelLayout l, DockSide side) => l.OnSide(side).Select(p => p.Id);

    [Fact]
    public void Default_IsValid_AndContainsEveryPanelOnce()
    {
        PanelLayout.Validate(PanelLayout.Default.Panels).ShouldBeNull();
        PanelLayout.Default.Panels.Select(p => p.Id).OrderBy(x => x).ShouldBe(PanelIds.All.OrderBy(x => x));
        PanelLayout.Default.Get(PanelIds.Layers).Visible.ShouldBeTrue();
        PanelLayout.Default.Get(PanelIds.EffectBrowser).Visible.ShouldBeFalse();
    }

    [Fact]
    public void SetVisible_ChangesOnlyThatPanel_AndKeepsOriginalUntouched()
    {
        var hidden = PanelLayout.Default.SetVisible(PanelIds.Layers, false);
        hidden.Get(PanelIds.Layers).Visible.ShouldBeFalse();
        PanelLayout.Default.Get(PanelIds.Layers).Visible.ShouldBeTrue();
        hidden.Panels.Count(p => p.Visible).ShouldBe(PanelLayout.Default.Panels.Count(p => p.Visible) - 1);
        Should.Throw<KeyNotFoundException>(() => PanelLayout.Default.SetVisible("gibtEsNicht", true));
    }

    [Fact]
    public void Resize_ClampsToRange_AndIgnoresNonFiniteValues()
    {
        PanelLayout.Default.Resize(PanelIds.Layers, 10).Get(PanelIds.Layers).Size.ShouldBe(PanelLayout.MinSize);
        PanelLayout.Default.Resize(PanelIds.Layers, 99999).Get(PanelIds.Layers).Size.ShouldBe(PanelLayout.MaxSize);
        PanelLayout.Default.Resize(PanelIds.Layers, 333).Get(PanelIds.Layers).Size.ShouldBe(333);
        PanelLayout.Default.Resize(PanelIds.Layers, double.NaN).ShouldBeSameAs(PanelLayout.Default);
        PanelLayout.Default.Resize(PanelIds.Layers, double.PositiveInfinity).ShouldBeSameAs(PanelLayout.Default);
    }

    [Fact]
    public void Move_ToOtherSide_PlacesPanelAtRequestedPosition_AndKeepsSize()
    {
        var moved = PanelLayout.Default.Resize(PanelIds.History, 350).Move(PanelIds.History, DockSide.Left, 0);
        Ids(moved, DockSide.Left).ShouldBe([PanelIds.History, PanelIds.Tools]);
        Ids(moved, DockSide.Right).ShouldBe([PanelIds.Layers, PanelIds.Colors, PanelIds.Navigator, PanelIds.Properties, PanelIds.EffectBrowser]);
        moved.Get(PanelIds.History).Size.ShouldBe(350);
        PanelLayout.Validate(moved.Panels).ShouldBeNull();
    }

    [Fact]
    public void Move_WithinSide_ReordersPanels()
    {
        var moved = PanelLayout.Default.Move(PanelIds.Navigator, DockSide.Right, 0);
        Ids(moved, DockSide.Right).Take(4).ShouldBe([PanelIds.Navigator, PanelIds.Layers, PanelIds.Colors, PanelIds.History]);
        var last = PanelLayout.Default.Move(PanelIds.Layers, DockSide.Right, 99);
        Ids(last, DockSide.Right).Last().ShouldBe(PanelIds.Layers);
        Ids(last, DockSide.Right).Count().ShouldBe(6);
    }

    [Fact]
    public void Move_ToEmptySide_Works()
    {
        var moved = PanelLayout.Default.Move(PanelIds.Colors, DockSide.Bottom, 5);
        Ids(moved, DockSide.Bottom).ShouldBe([PanelIds.Colors]);
        moved.Panels.Count.ShouldBe(PanelIds.All.Count);
    }

    [Fact]
    public void Validate_RejectsUnknownDuplicateMissingAndInvalidPanels()
    {
        var all = PanelLayout.Default.Panels.ToList();
        PanelLayout.Validate(null).ShouldNotBeNull();
        PanelLayout.Validate(all.Skip(1).ToList()).ShouldNotBeNull();
        PanelLayout.Validate([.. all, all[0]]).ShouldNotBeNull();
        PanelLayout.Validate([.. all.Skip(1), new PanelState("fremd", DockSide.Left, true, 200)]).ShouldNotBeNull();
        PanelLayout.Validate([.. all.Skip(1), all[0] with { Size = 5 }]).ShouldNotBeNull();
        PanelLayout.Validate([.. all.Skip(1), all[0] with { Size = double.NaN }]).ShouldNotBeNull();
        PanelLayout.Validate([.. all.Skip(1), all[0] with { Side = (DockSide)42 }]).ShouldNotBeNull();
        Should.Throw<ArgumentException>(() => PanelLayout.Create(all.Skip(1)));
    }

    [Fact]
    public void Normalize_RepairsEverything_AndReportsChange()
    {
        var input = new PanelState?[]
        {
            new(PanelIds.Layers, DockSide.Left, true, 5000),       // Größe zu groß
            new(PanelIds.Layers, DockSide.Right, true, 200),       // doppelt
            new("fremd", DockSide.Left, true, 200),                // unbekannt
            null,
            new(PanelIds.Colors, (DockSide)99, false, double.NaN), // Seite und Größe ungültig
        };
        var layout = PanelLayout.Normalize(input, out var changed);
        changed.ShouldBeTrue();
        PanelLayout.Validate(layout.Panels).ShouldBeNull();
        layout.Get(PanelIds.Layers).ShouldBe(new PanelState(PanelIds.Layers, DockSide.Left, true, PanelLayout.MaxSize));
        var colors = layout.Get(PanelIds.Colors);
        colors.Side.ShouldBe(PanelLayout.Default.Get(PanelIds.Colors).Side);
        colors.Size.ShouldBe(PanelLayout.Default.Get(PanelIds.Colors).Size);
        colors.Visible.ShouldBeFalse(); // sichtbarer Zustand bleibt erhalten
    }

    [Fact]
    public void Normalize_OfValidLayout_ChangesNothing()
    {
        var layout = PanelLayout.Normalize(PanelLayout.Default.Panels, out var changed);
        changed.ShouldBeFalse();
        layout.ShouldBe(PanelLayout.Default);
        PanelLayout.Normalize(null, out var changedNull).ShouldBe(PanelLayout.Default);
        changedNull.ShouldBeTrue();
    }

    [Fact]
    public void Layouts_AreComparedByValue()
    {
        PanelLayout.Default.Resize(PanelIds.Tools, 80).ShouldNotBe(PanelLayout.Default);
        PanelLayout.Default.SetVisible(PanelIds.Tools, true).ShouldBe(PanelLayout.Default);
    }
}
