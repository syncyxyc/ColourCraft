using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Domain.Tests;

public class ImageDocumentTests
{
    [Theory]
    [InlineData(0, 10, SizeValidationResult.NonPositive)]
    [InlineData(10, 0, SizeValidationResult.NonPositive)]
    [InlineData(-1, 10, SizeValidationResult.NonPositive)]
    [InlineData(65_536, 1, SizeValidationResult.ExceedsMaxDimension)]
    [InlineData(1, 65_536, SizeValidationResult.ExceedsMaxDimension)]
    [InlineData(20_000, 20_000, SizeValidationResult.ExceedsMaxPixelCount)]
    [InlineData(1, 1, SizeValidationResult.Valid)]
    [InlineData(16_384, 16_384, SizeValidationResult.Valid)]
    [InlineData(65_535, 4_096, SizeValidationResult.Valid)]
    public void ValidateSize_ClassifiesDimensions(int width, int height, SizeValidationResult expected)
    {
        DocumentLimits.ValidateSize(width, height).ShouldBe(expected);
    }

    [Fact]
    public void Create_WithValidSize_SetsProperties()
    {
        var doc = ImageDocument.Create("Test", 800, 600, 300, DocumentBackground.Transparent);

        doc.Name.ShouldBe("Test");
        doc.Width.ShouldBe(800);
        doc.Height.ShouldBe(600);
        doc.ResolutionDpi.ShouldBe(300);
        doc.Background.ShouldBe(DocumentBackground.Transparent);
        doc.ColorMode.ShouldBe(ColorMode.Bgra8);
        doc.FormatVersion.ShouldBe(ImageDocument.CurrentFormatVersion);
        doc.IsDirty.ShouldBeFalse();
        doc.FilePath.ShouldBeNull();
        doc.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_WithInvalidSize_ThrowsWithReason()
    {
        var ex = Should.Throw<DocumentValidationException>(() => ImageDocument.Create("X", 0, 10));
        ex.Reason.ShouldBe(SizeValidationResult.NonPositive);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(20_000.0)]
    public void Create_WithInvalidResolution_Throws(double dpi)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ImageDocument.Create("X", 10, 10, dpi));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_Throws(string name)
    {
        Should.Throw<ArgumentException>(() => ImageDocument.Create(name, 10, 10));
    }

    [Fact]
    public void Documents_HaveIndependentIdentity()
    {
        var a = ImageDocument.Create("A", 10, 10);
        var b = ImageDocument.Create("A", 10, 10);

        a.Id.ShouldNotBe(b.Id);
        a.MarkDirty();
        b.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void MarkDirty_And_MarkSaved_UpdateStateAndRaiseEvent()
    {
        var doc = ImageDocument.Create("A", 10, 10);
        var events = 0;
        doc.StateChanged += (_, _) => events++;

        doc.MarkDirty();
        doc.MarkDirty(); // zweiter Aufruf ändert nichts
        doc.IsDirty.ShouldBeTrue();
        events.ShouldBe(1);

        doc.MarkSaved(@"C:\temp\a.orbit");
        doc.IsDirty.ShouldBeFalse();
        doc.FilePath.ShouldBe(@"C:\temp\a.orbit");
        events.ShouldBe(2);
    }

    [Fact]
    public void Rename_RejectsEmptyName()
    {
        var doc = ImageDocument.Create("A", 10, 10);
        Should.Throw<ArgumentException>(() => doc.Name = " ");
        doc.Name.ShouldBe("A");
    }
}
