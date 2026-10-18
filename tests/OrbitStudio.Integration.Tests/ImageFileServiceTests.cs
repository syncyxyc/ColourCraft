using Microsoft.Extensions.Logging.Abstractions;
using OrbitStudio.Application.Files;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Imaging.Codecs;

namespace OrbitStudio.Integration.Tests;

/// <summary>Öffnen und Exportieren über echte Dateien im Temp-Ordner.</summary>
public sealed class ImageFileServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbit-files-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ImageFileService _service =
        new(ImageCodecRegistry.CreateDefault(), NullLogger<ImageFileService>.Instance);

    public ImageFileServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string PathOf(string name) => Path.Combine(_dir, name);

    private static PixelBuffer Pattern()
    {
        var buffer = PixelBuffer.Create(33, 17);
        for (var y = 0; y < buffer.Height; y++)
        {
            for (var x = 0; x < buffer.Width; x++)
            {
                buffer.SetPixel(x, y, new Bgra32((byte)(x * 7), (byte)(y * 15), (byte)(x + y), 255));
            }
        }

        return buffer;
    }

    [Theory]
    [InlineData("bild.png")]
    [InlineData("bild.bmp")]
    public async Task ExportThenOpen_ReproducesPixelsExactly(string fileName)
    {
        var pixels = Pattern();

        var export = await _service.ExportAsync(pixels, PathOf(fileName), new EncodeOptions());
        var open = await _service.OpenAsync(PathOf(fileName), "HG");

        export.Success.ShouldBeTrue();
        open.Success.ShouldBeTrue();
        open.Document!.PixelLayers.Single().Pixels.ContentEquals(pixels).ShouldBeTrue();
        open.Document.Name.ShouldBe(fileName);
        open.Document.SourcePath.ShouldBe(PathOf(fileName));
        open.Document.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public async Task ExportJpeg_ThenOpen_IsCloseToOriginal()
    {
        var pixels = PixelBuffer.Create(64, 64);
        pixels.Fill(Bgra32.FromStraight(90, 150, 210, 255));

        (await _service.ExportAsync(pixels, PathOf("bild.jpg"), new EncodeOptions(95))).Success.ShouldBeTrue();
        var open = await _service.OpenAsync(PathOf("bild.jpg"), "HG");

        var p = open.Document!.PixelLayers.Single().Pixels.GetPixel(32, 32);
        Math.Abs(p.R - 90).ShouldBeLessThanOrEqualTo(3);
        Math.Abs(p.G - 150).ShouldBeLessThanOrEqualTo(3);
        Math.Abs(p.B - 210).ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task Export_UsesSnapshot_LaterChangesDoNotAffectFile()
    {
        var pixels = Pattern();
        var expected = pixels.Clone();

        var task = _service.ExportAsync(pixels, PathOf("snap.png"), new EncodeOptions());
        pixels.Fill(Bgra32.Black); // Änderung nach dem Start des Exports
        (await task).Success.ShouldBeTrue();

        (await _service.OpenAsync(PathOf("snap.png"), "HG")).Document!.PixelLayers.Single().Pixels.ContentEquals(expected).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_OverwritesExistingFileAtomically_AndLeavesNoTempFiles()
    {
        await File.WriteAllTextAsync(PathOf("ziel.png"), "alt");

        (await _service.ExportAsync(Pattern(), PathOf("ziel.png"), new EncodeOptions())).Success.ShouldBeTrue();

        (await _service.OpenAsync(PathOf("ziel.png"), "HG")).Success.ShouldBeTrue();
        Directory.GetFiles(_dir).ShouldBe([PathOf("ziel.png")]);
    }

    [Fact]
    public async Task Export_Cancelled_KeepsExistingFileUnchanged()
    {
        await File.WriteAllTextAsync(PathOf("bleibt.png"), "alt");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await _service.ExportAsync(Pattern(), PathOf("bleibt.png"), new EncodeOptions(), cts.Token);

        result.Error.ShouldBe(FileError.Cancelled);
        (await File.ReadAllTextAsync(PathOf("bleibt.png"))).ShouldBe("alt");
        Directory.GetFiles(_dir).ShouldBe([PathOf("bleibt.png")]);
    }

    [Fact]
    public async Task Export_ToReadOnlyFormat_IsRejected()
    {
        var result = await _service.ExportAsync(Pattern(), PathOf("x.gif"), new EncodeOptions());
        result.Error.ShouldBe(FileError.FormatNotWritable);
        File.Exists(PathOf("x.gif")).ShouldBeFalse();
    }

    [Fact]
    public async Task Export_UnknownExtension_IsRejected()
    {
        (await _service.ExportAsync(Pattern(), PathOf("x.tif"), new EncodeOptions())).Error.ShouldBe(FileError.UnknownFormat);
    }

    [Fact]
    public async Task Export_ToMissingDirectory_ReportsNotFound()
    {
        var result = await _service.ExportAsync(Pattern(), Path.Combine(_dir, "fehlt", "x.png"), new EncodeOptions());
        result.Error.ShouldBe(FileError.NotFound);
    }

    [Fact]
    public async Task Open_MissingFile_ReportsNotFound()
    {
        (await _service.OpenAsync(PathOf("gibtsnicht.png"), "HG")).Error.ShouldBe(FileError.NotFound);
    }

    [Fact]
    public async Task Open_CorruptFile_ReportsCorrupt()
    {
        await File.WriteAllBytesAsync(PathOf("kaputt.png"), [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        (await _service.OpenAsync(PathOf("kaputt.png"), "HG")).Error.ShouldBe(FileError.Corrupt);
    }

    [Fact]
    public async Task Open_WrongExtension_IsDetectedByContent()
    {
        // PNG-Inhalt mit .jpg-Endung: Der Decoder erkennt das Format am Inhalt.
        (await _service.ExportAsync(Pattern(), PathOf("echt.png"), new EncodeOptions())).Success.ShouldBeTrue();
        File.Move(PathOf("echt.png"), PathOf("falsch.jpg"));

        var open = await _service.OpenAsync(PathOf("falsch.jpg"), "HG");

        open.Success.ShouldBeTrue();
        open.Document!.PixelLayers.Single().Pixels.ContentEquals(Pattern()).ShouldBeTrue();
    }

    [Fact]
    public async Task Open_UnsupportedExtension_ReportsUnknownFormat()
    {
        await File.WriteAllTextAsync(PathOf("text.txt"), "hallo");
        (await _service.OpenAsync(PathOf("text.txt"), "HG")).Error.ShouldBe(FileError.UnknownFormat);
    }

    [Fact]
    public async Task Open_LockedFile_ReportsIoError()
    {
        (await _service.ExportAsync(Pattern(), PathOf("gesperrt.png"), new EncodeOptions())).Success.ShouldBeTrue();
        await using var lockStream = new FileStream(PathOf("gesperrt.png"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        (await _service.OpenAsync(PathOf("gesperrt.png"), "HG")).Error.ShouldBe(FileError.IoError);
    }

    [Fact]
    public void HasTransparency_DetectsAlpha()
    {
        var opaque = Pattern();
        ImageFileService.HasTransparency(opaque).ShouldBeFalse();
        opaque.SetPixel(5, 5, Bgra32.FromStraight(1, 2, 3, 254));
        ImageFileService.HasTransparency(opaque).ShouldBeTrue();
    }
}
