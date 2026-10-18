using OrbitStudio.Domain.Imaging;
using OrbitStudio.Presentation.Services;

namespace OrbitStudio.App;

/// <summary>Zwischenablage im Speicher für den Smoke-Test – die echte Windows-Zwischenablage bleibt unberührt.</summary>
internal sealed class SmokeClipboardService : IClipboardService
{
    private PixelBuffer? _image;

    public bool ContainsImage => _image is not null;

    public ClipboardStatus SetImage(PixelBuffer pixels)
    {
        _image = pixels.Clone();
        return ClipboardStatus.Ok;
    }

    public (PixelBuffer? Pixels, ClipboardStatus Status) GetImage() =>
        _image is null ? (null, ClipboardStatus.Empty) : (_image.Clone(), ClipboardStatus.Ok);
}
