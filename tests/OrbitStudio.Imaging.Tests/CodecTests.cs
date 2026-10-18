using System.Buffers.Binary;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Imaging.Codecs;

namespace OrbitStudio.Imaging.Tests;

public class CodecTests
{
    private readonly ImageCodecRegistry _registry = ImageCodecRegistry.CreateDefault();

    /// <summary>Deckendes 7×5-Testbild mit eindeutigen Werten je Pixel (ungerade Breite prüft das BMP-Zeilenpadding).</summary>
    private static PixelBuffer OpaquePattern(int width = 7, int height = 5)
    {
        var buffer = PixelBuffer.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                buffer.SetPixel(x, y, new Bgra32((byte)(x * 30), (byte)(y * 50), (byte)(x * 10 + y * 20), 255));
            }
        }

        return buffer;
    }

    /// <summary>Testbild mit Transparenzstufen (vormultipliert, gültige Werte).</summary>
    private static PixelBuffer AlphaPattern()
    {
        var buffer = PixelBuffer.Create(4, 2);
        buffer.SetPixel(0, 0, Bgra32.FromStraight(255, 0, 0, 255));
        buffer.SetPixel(1, 0, Bgra32.FromStraight(255, 0, 0, 128));
        buffer.SetPixel(2, 0, Bgra32.FromStraight(0, 255, 0, 64));
        buffer.SetPixel(3, 0, Bgra32.Transparent);
        buffer.SetPixel(0, 1, Bgra32.FromStraight(10, 20, 30, 200));
        buffer.SetPixel(1, 1, Bgra32.FromStraight(255, 255, 255, 1));
        buffer.SetPixel(2, 1, Bgra32.Black);
        buffer.SetPixel(3, 1, Bgra32.White);
        return buffer;
    }

    private PixelBuffer RoundTrip(string formatId, PixelBuffer source, int quality = 100)
    {
        var codec = _registry.FindById(formatId)!;
        using var stream = new MemoryStream();
        codec.Encode(source, stream, new EncodeOptions(quality));
        stream.Position = 0;
        return codec.Decode(stream).Pixels;
    }

    private static int MaxDifference(PixelBuffer a, PixelBuffer b)
    {
        a.Width.ShouldBe(b.Width);
        a.Height.ShouldBe(b.Height);
        var max = 0;
        for (var i = 0; i < a.Data.Length; i++)
        {
            max = Math.Max(max, Math.Abs(a.Data[i] - b.Data[i]));
        }

        return max;
    }

    [Theory]
    [InlineData("png")]
    [InlineData("bmp")]
    public void LosslessFormats_RoundTripOpaquePixelsExactly(string format)
    {
        var source = OpaquePattern();
        RoundTrip(format, source).ContentEquals(source).ShouldBeTrue();
    }

    [Theory]
    [InlineData("png")]
    [InlineData("bmp")]
    public void LosslessFormats_PreserveAlpha_WithinPremultiplyRounding(string format)
    {
        var source = AlphaPattern();
        var result = RoundTrip(format, source);

        // Umrechnung vormultipliert → gerade → vormultipliert: Alpha exakt, Farbe höchstens ±1.
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var expected = source.GetPixel(x, y);
                var actual = result.GetPixel(x, y);
                actual.A.ShouldBe(expected.A, $"Alpha bei ({x},{y})");
                Math.Abs(actual.R - expected.R).ShouldBeLessThanOrEqualTo(1, $"Rot bei ({x},{y})");
                Math.Abs(actual.G - expected.G).ShouldBeLessThanOrEqualTo(1, $"Grün bei ({x},{y})");
                Math.Abs(actual.B - expected.B).ShouldBeLessThanOrEqualTo(1, $"Blau bei ({x},{y})");
                actual.IsValidPremultiplied.ShouldBeTrue();
            }
        }
    }

    [Fact]
    public void Jpeg_FlatColor_RoundTripsWithinTolerance()
    {
        var source = PixelBuffer.Create(32, 32);
        source.Fill(Bgra32.FromStraight(200, 100, 50, 255));

        var result = RoundTrip("jpeg", source, quality: 95);

        MaxDifference(result, source).ShouldBeLessThanOrEqualTo(3);
    }

    [Fact]
    public void Jpeg_FlattensTransparencyOntoWhite()
    {
        var source = PixelBuffer.Create(16, 16); // vollständig transparent

        var result = RoundTrip("jpeg", source, quality: 95);

        var p = result.GetPixel(8, 8);
        p.A.ShouldBe((byte)255);
        p.R.ShouldBeGreaterThanOrEqualTo((byte)252);
        p.G.ShouldBeGreaterThanOrEqualTo((byte)252);
        p.B.ShouldBeGreaterThanOrEqualTo((byte)252);
    }

    [Fact]
    public void WebP_Lossy_RoundTripsWithinTolerance()
    {
        var source = PixelBuffer.Create(32, 32);
        source.Fill(Bgra32.FromStraight(40, 160, 220, 255));

        var result = RoundTrip("webp", source, quality: 95);

        MaxDifference(result, source).ShouldBeLessThanOrEqualTo(6);
    }

    [Fact]
    public void Gif_IsReadOnly()
    {
        var gif = _registry.FindById("gif")!;
        gif.Format.CanWrite.ShouldBeFalse();
        var ex = Should.Throw<ImageCodecException>(() =>
            gif.Encode(OpaquePattern(), new MemoryStream(), new EncodeOptions()));
        ex.Error.ShouldBe(ImageCodecError.Unsupported);
    }

    [Fact]
    public void Bmp_Opaque_Uses24BitWithPaddedRows()
    {
        var bytes = Encode("bmp", OpaquePattern(width: 7, height: 5));

        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28)).ShouldBe((ushort)24);
        var rowSize = (7 * 3 + 3) & ~3; // 24 Byte
        bytes.Length.ShouldBe(14 + 40 + rowSize * 5);
    }

    [Fact]
    public void Bmp_WithAlpha_Uses32BitV4Header()
    {
        var bytes = Encode("bmp", AlphaPattern());

        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(14)).ShouldBe(108u); // BITMAPV4HEADER
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28)).ShouldBe((ushort)32);
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(30)).ShouldBe(3u); // BI_BITFIELDS
    }

    [Fact]
    public void Bmp_HandMadeFile_DecodesToKnownPixels()
    {
        // Unabhängig vom eigenen Encoder: 2×2, 24 Bit, Zeilen von unten nach oben, je Zeile 8 Byte (6 + 2 Padding).
        var file = new List<byte>();
        file.AddRange("BM"u8.ToArray());
        file.AddRange(BitConverter.GetBytes(14 + 40 + 16));
        file.AddRange(new byte[4]);
        file.AddRange(BitConverter.GetBytes(54));
        file.AddRange(BitConverter.GetBytes(40));
        file.AddRange(BitConverter.GetBytes(2));
        file.AddRange(BitConverter.GetBytes(2));
        file.AddRange(BitConverter.GetBytes((short)1));
        file.AddRange(BitConverter.GetBytes((short)24));
        file.AddRange(new byte[24]);
        // untere Zeile (y = 1): Blau, Weiß
        file.AddRange(new byte[] { 255, 0, 0, 255, 255, 255, 0, 0 });
        // obere Zeile (y = 0): Rot, Grün
        file.AddRange(new byte[] { 0, 0, 255, 0, 255, 0, 0, 0 });

        var pixels = _registry.FindById("bmp")!.Decode(new MemoryStream(file.ToArray())).Pixels;

        pixels.GetPixel(0, 0).ShouldBe(new Bgra32(0, 0, 255, 255)); // Rot
        pixels.GetPixel(1, 0).ShouldBe(new Bgra32(0, 255, 0, 255)); // Grün
        pixels.GetPixel(0, 1).ShouldBe(new Bgra32(255, 0, 0, 255)); // Blau
        pixels.GetPixel(1, 1).ShouldBe(Bgra32.White);
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0 })] // PNG-Signatur, dann Ende
    public void Decode_GarbageOrEmpty_ThrowsCorrupt(byte[] data)
    {
        var ex = Should.Throw<ImageCodecException>(() => _registry.FindById("png")!.Decode(new MemoryStream(data)));
        ex.Error.ShouldBe(ImageCodecError.Corrupt);
    }

    [Fact]
    public void Decode_TruncatedPng_ThrowsCorrupt()
    {
        var full = Encode("png", OpaquePattern(64, 64));
        var truncated = full.AsSpan(0, full.Length / 2).ToArray();

        var ex = Should.Throw<ImageCodecException>(() => _registry.FindById("png")!.Decode(new MemoryStream(truncated)));
        ex.Error.ShouldBe(ImageCodecError.Corrupt);
    }

    [Theory]
    [InlineData(70_000, 1)] // Kantenlänge über 65.535
    [InlineData(20_000, 20_000)] // 400 MP über der Pixelgrenze
    public void Decode_OversizedDimensions_ThrowsTooLargeWithoutDecoding(int width, int height)
    {
        // Gültiger PNG-Kopf mit großen Abmessungen; die Pixeldaten fehlen bewusst.
        // Würde der Decoder trotzdem dekodieren, wäre das Ergebnis "Corrupt" statt "TooLarge".
        var png = PngWithHeaderOnly(width, height);

        var ex = Should.Throw<ImageCodecException>(() => _registry.FindById("png")!.Decode(new MemoryStream(png)));
        ex.Error.ShouldBe(ImageCodecError.TooLarge);
    }

    private static byte[] PngWithHeaderOnly(int width, int height)
    {
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; // Bittiefe
        ihdr[9] = 2; // RGB
        var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(stream, "IHDR", ihdr);
        WriteChunk(stream, "IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);
        var typeAndData = System.Text.Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        stream.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32(typeAndData));
        stream.Write(buffer);
    }

    /// <summary>CRC-32 nach PNG-Spezifikation (Polynom 0xEDB88320).</summary>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }

    [Theory]
    [InlineData(@"C:\a\bild.PNG", "png")]
    [InlineData("foto.jpeg", "jpeg")]
    [InlineData("foto.JPG", "jpeg")]
    [InlineData("x.bmp", "bmp")]
    [InlineData("x.webp", "webp")]
    [InlineData("x.gif", "gif")]
    public void Registry_FindsCodecByExtension(string path, string expectedId)
    {
        _registry.FindByPath(path)!.Format.Id.ShouldBe(expectedId);
    }

    [Theory]
    [InlineData("x.tif")]
    [InlineData("ohne_endung")]
    public void Registry_UnknownExtension_ReturnsNull(string path)
    {
        _registry.FindByPath(path).ShouldBeNull();
    }

    [Fact]
    public void Registry_RejectsDuplicateFormat()
    {
        Should.Throw<InvalidOperationException>(() => _registry.Register(new BmpCodec()));
    }

    [Fact]
    public void AlphaFlattening_IsExact()
    {
        var source = PixelBuffer.Create(1, 1);
        source.SetPixel(0, 0, Bgra32.FromStraight(255, 0, 0, 128)); // vormultipliert: (0,0,128,128)

        var flat = AlphaFlattening.OntoWhite(source).GetPixel(0, 0);

        // R = 128 + 127 = 255, G = B = 0 + 127 = 127
        flat.ShouldBe(new Bgra32(127, 127, 255, 255));
    }

    private byte[] Encode(string formatId, PixelBuffer pixels)
    {
        using var stream = new MemoryStream();
        _registry.FindById(formatId)!.Encode(pixels, stream, new EncodeOptions());
        return stream.ToArray();
    }
}
