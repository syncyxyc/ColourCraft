using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Presentation.Viewport;

/// <summary>
/// Überträgt den Pixelpuffer in eine WPF-Bitmap. Das interne Format (BGRA, vormultipliert) entspricht
/// exakt <see cref="PixelFormats.Pbgra32"/>, daher ist keine Umrechnung nötig.
/// </summary>
public static class BitmapBridge
{
    public static WriteableBitmap Create(PixelBuffer pixels)
    {
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height, 96, 96, PixelFormats.Pbgra32, null);
        Update(bitmap, pixels, pixels.Bounds);
        return bitmap;
    }

    /// <summary>Kopiert nur den geänderten Bereich (Dirty-Rectangle).</summary>
    public static unsafe void Update(WriteableBitmap bitmap, PixelBuffer pixels, IntRect region)
    {
        var r = region.Intersect(pixels.Bounds);
        if (r.IsEmpty)
        {
            return;
        }

        var offset = r.Y * pixels.Stride + r.X * PixelBuffer.BytesPerPixel;
        var data = pixels.Data[offset..];
        fixed (byte* ptr = data)
        {
            bitmap.WritePixels(new Int32Rect(r.X, r.Y, r.Width, r.Height), (IntPtr)ptr, data.Length, pixels.Stride);
        }
    }
}
