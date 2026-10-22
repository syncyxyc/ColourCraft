using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Domain.Tests;

public class SwatchPaletteTests
{
    private static readonly ColorRgba Red = new(255, 0, 0);
    private static readonly ColorRgba Green = new(0, 255, 0);
    private static readonly ColorRgba Blue = new(0, 0, 255);

    [Fact]
    public void Default_HasNoDuplicates_AndContainsBlackAndWhite()
    {
        var palette = SwatchPalette.Default;
        palette.Count.ShouldBeGreaterThan(10);
        palette.Colors.Distinct().Count().ShouldBe(palette.Count);
        palette.Contains(ColorRgba.Black).ShouldBeTrue();
        palette.Contains(ColorRgba.White).ShouldBeTrue();
    }

    [Fact]
    public void Add_AppendsNewColor_AndIgnoresDuplicates()
    {
        var palette = SwatchPalette.Empty.Add(Red).Add(Green);
        palette.Colors.ShouldBe([Red, Green]);
        palette.Add(Red).ShouldBeSameAs(palette);
        // Gleiche RGB-Werte mit anderem Alpha sind eine andere Farbe.
        palette.Add(Red with { A = 128 }).Count.ShouldBe(3);
    }

    [Fact]
    public void Add_WhenFull_ChangesNothing()
    {
        var palette = SwatchPalette.FromColors(Enumerable.Range(0, SwatchPalette.MaximumColors)
            .Select(i => new ColorRgba((byte)i, (byte)(i / 2), 7)));
        palette.Count.ShouldBe(SwatchPalette.MaximumColors);
        palette.IsFull.ShouldBeTrue();
        palette.Add(new ColorRgba(1, 2, 3, 4)).ShouldBeSameAs(palette);
    }

    [Fact]
    public void FromColors_DropsDuplicates_KeepsFirst_AndCapsCount()
    {
        SwatchPalette.FromColors([Red, Green, Red, Blue, Green]).Colors.ShouldBe([Red, Green, Blue]);
        var many = Enumerable.Range(0, 1000).Select(i => new ColorRgba((byte)(i % 256), (byte)(i / 256), 0));
        SwatchPalette.FromColors(many).Count.ShouldBe(SwatchPalette.MaximumColors);
    }

    [Fact]
    public void RemoveAt_Replace_Move_WorkOnIndices()
    {
        var palette = SwatchPalette.FromColors([Red, Green, Blue]);
        palette.RemoveAt(1).Colors.ShouldBe([Red, Blue]);
        palette.Replace(1, ColorRgba.White).Colors.ShouldBe([Red, ColorRgba.White, Blue]);
        // Ersetzen durch eine Farbe, die es schon gibt, würde ein Duplikat erzeugen → unverändert.
        palette.Replace(1, Blue).ShouldBeSameAs(palette);
        // Gleiche Farbe an gleicher Stelle: inhaltlich unverändert.
        palette.Replace(1, Green).ShouldBe(palette);
        palette.Move(0, 2).Colors.ShouldBe([Green, Blue, Red]);
        palette.Move(2, 0).Colors.ShouldBe([Blue, Red, Green]);
        palette.Move(1, 99).Colors.ShouldBe([Red, Blue, Green]); // Ziel wird begrenzt
        palette.Move(1, 1).ShouldBeSameAs(palette);
        palette.IndexOf(Blue).ShouldBe(2);
        palette.IndexOf(ColorRgba.White).ShouldBe(-1);
    }

    [Fact]
    public void IndexedOperations_RejectInvalidIndex()
    {
        var palette = SwatchPalette.FromColors([Red]);
        Should.Throw<ArgumentOutOfRangeException>(() => palette.RemoveAt(1));
        Should.Throw<ArgumentOutOfRangeException>(() => palette.RemoveAt(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => palette.Replace(3, Green));
        Should.Throw<ArgumentOutOfRangeException>(() => palette.Move(2, 0));
    }

    [Fact]
    public void Hex_RoundTrips_IncludingAlpha()
    {
        var palette = SwatchPalette.FromColors([Red, new ColorRgba(1, 2, 3, 4), ColorRgba.Black]);
        var hex = palette.ToHex();
        hex.ShouldBe(["#FF0000", "#01020304", "#000000"]);
        var loaded = SwatchPalette.FromHex(hex, out var skipped);
        skipped.ShouldBe(0);
        loaded.ShouldBe(palette);
    }

    [Fact]
    public void FromHex_SkipsInvalidDuplicateAndMissingEntries_AndCountsThem()
    {
        var loaded = SwatchPalette.FromHex(["#FF0000", "kein-hex", null, "#ff0000", "#GG0000", "#00F", ""], out var skipped);
        loaded.Colors.ShouldBe([Red, new ColorRgba(0, 0, 255)]);
        skipped.ShouldBe(5);
    }

    [Fact]
    public void Palettes_AreImmutable_AndComparedByValue()
    {
        var a = SwatchPalette.FromColors([Red, Green]);
        var b = SwatchPalette.Empty.Add(Red).Add(Green);
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.Add(Blue).ShouldNotBe(a);
        a.Count.ShouldBe(2);
    }
}
