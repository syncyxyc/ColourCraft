using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Domain.Tests;

public class PixelBufferTests
{
    [Fact]
    public void Create_IsTransparentWithDocumentedLayout()
    {
        var buffer = PixelBuffer.Create(3, 2);

        buffer.Stride.ShouldBe(12);
        buffer.Data.Length.ShouldBe(24);
        buffer.Data.ToArray().ShouldAllBe(b => b == 0);
    }

    [Fact]
    public void SetPixel_WritesBgraAtDocumentedOffset()
    {
        var buffer = PixelBuffer.Create(3, 2);

        buffer.SetPixel(2, 1, new Bgra32(1, 2, 3, 4));

        var offset = 1 * buffer.Stride + 2 * 4; // y · Stride + x · 4
        buffer.Data.Slice(offset, 4).ToArray().ShouldBe(new byte[] { 1, 2, 3, 4 });
        buffer.GetPixel(2, 1).ShouldBe(new Bgra32(1, 2, 3, 4));
        buffer.GetPixelRow(1)[2].ShouldBe(new Bgra32(1, 2, 3, 4));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    public void GetPixel_OutsideBounds_Throws(int x, int y)
    {
        var buffer = PixelBuffer.Create(3, 2);
        Should.Throw<ArgumentOutOfRangeException>(() => buffer.GetPixel(x, y));
        buffer.Contains(x, y).ShouldBeFalse();
    }

    [Fact]
    public void Create_InvalidSize_IsRejectedBeforeAllocation()
    {
        Should.Throw<DocumentValidationException>(() => PixelBuffer.Create(0, 5)).Reason
            .ShouldBe(SizeValidationResult.NonPositive);
        Should.Throw<DocumentValidationException>(() => PixelBuffer.Create(30_000, 30_000)).Reason
            .ShouldBe(SizeValidationResult.ExceedsMaxPixelCount);
    }

    [Fact]
    public void Fill_Region_IsClippedToBounds()
    {
        var buffer = PixelBuffer.Create(4, 4);

        buffer.Fill(new IntRect(2, 2, 10, 10), Bgra32.White);

        buffer.GetPixel(1, 1).ShouldBe(Bgra32.Transparent);
        buffer.GetPixel(2, 2).ShouldBe(Bgra32.White);
        buffer.GetPixel(3, 3).ShouldBe(Bgra32.White);
        buffer.GetPixel(3, 1).ShouldBe(Bgra32.Transparent);
    }

    [Fact]
    public void Clone_IsIndependentCopy()
    {
        var original = PixelBuffer.Create(2, 2);
        original.Fill(Bgra32.White);

        var clone = original.Clone();
        clone.SetPixel(0, 0, Bgra32.Black);

        original.GetPixel(0, 0).ShouldBe(Bgra32.White);
        original.ContentEquals(clone).ShouldBeFalse();
    }

    [Fact]
    public void FromBytes_RequiresExactLength()
    {
        Should.Throw<ArgumentException>(() => PixelBuffer.FromBytes(2, 2, new byte[15]));
        PixelBuffer.FromBytes(2, 2, new byte[16]).Width.ShouldBe(2);
    }
}

public class ColorMathTests
{
    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(255, 128, 128)]
    [InlineData(255, 0, 0)]
    [InlineData(100, 255, 100)]
    [InlineData(200, 128, 100)] // 200·128/255 = 100,39 → 100
    [InlineData(1, 128, 1)] // 0,50 → 1 (kaufmännisch)
    [InlineData(0, 200, 0)]
    public void Premultiply_RoundsHalfUp(byte c, byte a, byte expected)
    {
        ColorMath.Premultiply(c, a).ShouldBe(expected);
    }

    [Theory]
    [InlineData(128, 128, 255)]
    [InlineData(100, 128, 199)] // 100·255/128 = 199,2 → 199
    [InlineData(50, 0, 0)]
    [InlineData(200, 100, 255)] // ungültig (Farbe > Alpha) → auf 255 begrenzt
    public void Unpremultiply_RoundsAndClamps(byte c, byte a, byte expected)
    {
        ColorMath.Unpremultiply(c, a).ShouldBe(expected);
    }

    [Fact]
    public void RoundTrip_StraightToPremultipliedAndBack_IsExactForOpaque()
    {
        for (var c = 0; c <= 255; c++)
        {
            ColorMath.Unpremultiply(ColorMath.Premultiply((byte)c, 255), 255).ShouldBe((byte)c);
        }
    }

    [Fact]
    public void Premultiplied_AlwaysSatisfiesInvariant()
    {
        for (var a = 0; a <= 255; a += 5)
        {
            for (var c = 0; c <= 255; c += 5)
            {
                Bgra32.FromStraight((byte)c, (byte)c, (byte)c, (byte)a).IsValidPremultiplied.ShouldBeTrue();
            }
        }
    }

    [Fact]
    public void FromStraight_ProducesExpectedPremultipliedValues()
    {
        // Rot 50 % deckend: R = 128, B = G = 0, A = 128
        Bgra32.FromStraight(255, 0, 0, 128).ShouldBe(new Bgra32(0, 0, 128, 128));
    }
}

public class PixelTransformTests
{
    /// <summary>3×2-Bild, jedes Pixel kodiert seine Position im Blaukanal als y·10 + x.</summary>
    private static PixelBuffer Numbered()
    {
        var buffer = PixelBuffer.Create(3, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                buffer.SetPixel(x, y, new Bgra32((byte)(y * 10 + x), 0, 0, 255));
            }
        }

        return buffer;
    }

    private static int[,] Read(PixelBuffer b)
    {
        var result = new int[b.Height, b.Width];
        for (var y = 0; y < b.Height; y++)
        {
            for (var x = 0; x < b.Width; x++)
            {
                result[y, x] = b.GetPixel(x, y).B;
            }
        }

        return result;
    }

    // Ausgangsbild:
    //  0  1  2
    // 10 11 12
    [Fact]
    public void FlipHorizontal() => Read(PixelTransforms.FlipHorizontal(Numbered())).ShouldBe(new[,] { { 2, 1, 0 }, { 12, 11, 10 } });

    [Fact]
    public void FlipVertical() => Read(PixelTransforms.FlipVertical(Numbered())).ShouldBe(new[,] { { 10, 11, 12 }, { 0, 1, 2 } });

    [Fact]
    public void Rotate180() => Read(PixelTransforms.Rotate180(Numbered())).ShouldBe(new[,] { { 12, 11, 10 }, { 2, 1, 0 } });

    [Fact]
    public void Rotate90Clockwise() =>
        Read(PixelTransforms.Rotate90Clockwise(Numbered())).ShouldBe(new[,] { { 10, 0 }, { 11, 1 }, { 12, 2 } });

    [Fact]
    public void Rotate90CounterClockwise() =>
        Read(PixelTransforms.Rotate90CounterClockwise(Numbered())).ShouldBe(new[,] { { 2, 12 }, { 1, 11 }, { 0, 10 } });

    [Fact]
    public void Transpose() => Read(PixelTransforms.Transpose(Numbered())).ShouldBe(new[,] { { 0, 10 }, { 1, 11 }, { 2, 12 } });

    [Fact]
    public void Transverse() => Read(PixelTransforms.Transverse(Numbered())).ShouldBe(new[,] { { 12, 2 }, { 11, 1 }, { 10, 0 } });

    [Fact]
    public void FourClockwiseRotations_RestoreOriginal()
    {
        var original = Numbered();
        var result = original;
        for (var i = 0; i < 4; i++)
        {
            result = PixelTransforms.Rotate90Clockwise(result);
        }

        result.ContentEquals(original).ShouldBeTrue();
    }
}

public class GeometryTests
{
    [Fact]
    public void IntRect_Intersect_And_Union()
    {
        var a = new IntRect(0, 0, 10, 10);
        var b = new IntRect(5, 5, 10, 10);

        a.Intersect(b).ShouldBe(new IntRect(5, 5, 5, 5));
        a.Union(b).ShouldBe(new IntRect(0, 0, 15, 15));
        a.Intersect(new IntRect(20, 20, 1, 1)).IsEmpty.ShouldBeTrue();
        IntRect.Empty.Union(b).ShouldBe(b);
    }

    [Fact]
    public void IntRect_RightAndBottomAreExclusive()
    {
        var r = new IntRect(2, 3, 4, 5);
        r.Contains(5, 7).ShouldBeTrue();
        r.Contains(6, 7).ShouldBeFalse();
        r.Contains(5, 8).ShouldBeFalse();
    }

    [Fact]
    public void PointD_Arithmetic()
    {
        var p = new PointD(3, 4);
        p.Length.ShouldBe(5);
        (p + new PointD(1, 1)).ShouldBe(new PointD(4, 5));
        (p - new PointD(1, 1)).ShouldBe(new PointD(2, 3));
        (p * 2).ShouldBe(new PointD(6, 8));
    }
}

public class ImageDocumentPixelTests
{
    [Fact]
    public void Create_White_FillsWhite_Transparent_StaysTransparent()
    {
        ImageDocument.Create("W", 4, 4).Pixels.GetPixel(3, 3).ShouldBe(Bgra32.White);
        ImageDocument.Create("T", 4, 4, background: DocumentBackground.Transparent).Pixels.GetPixel(3, 3)
            .ShouldBe(Bgra32.Transparent);
    }

    [Fact]
    public void CommitPixelChange_RaisesClippedRegionAndMarksDirty()
    {
        var doc = ImageDocument.Create("A", 10, 10);
        IntRect? reported = null;
        doc.PixelsChanged += (_, r) => reported = r;

        doc.CommitPixelChange(new IntRect(8, 8, 5, 5));

        reported.ShouldBe(new IntRect(8, 8, 2, 2));
        doc.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void CommitPixelChange_OutsideImage_DoesNothing()
    {
        var doc = ImageDocument.Create("A", 10, 10);
        var raised = false;
        doc.PixelsChanged += (_, _) => raised = true;

        doc.CommitPixelChange(new IntRect(20, 20, 5, 5));

        raised.ShouldBeFalse();
        doc.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void FromPixels_KeepsSourcePathAndBuffer()
    {
        var pixels = PixelBuffer.Create(5, 6);
        var doc = ImageDocument.FromPixels("foto.png", pixels, @"C:\foto.png");

        doc.Pixels.ShouldBeSameAs(pixels);
        doc.Width.ShouldBe(5);
        doc.Height.ShouldBe(6);
        doc.SourcePath.ShouldBe(@"C:\foto.png");
        doc.FilePath.ShouldBeNull();
        doc.IsDirty.ShouldBeFalse();
    }
}
