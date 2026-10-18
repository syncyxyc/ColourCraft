using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;

namespace OrbitStudio.Tools.Transform;

/// <summary>Ausrichtung des alten Bildes beim Ändern der Leinwandgröße.</summary>
public enum CanvasAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

/// <summary>
/// Bildweite Transformationen (alle Ebenen, Masken und die Auswahl gemeinsam) mit Historie.
/// Die alten Puffer bleiben im Historieneintrag erhalten (keine Kopie nötig, da nur neue Puffer erzeugt werden).
/// </summary>
public static class DocumentTransforms
{
    public static void ResizeImage(ImageDocument document, DocumentHistory history, int width, int height) =>
        Apply(document, history, "History.ResizeImage", width, height,
            p => Resampler.Resize(p, width, height),
            m => Resampler.Resize(m, width, height),
            s => Resampler.Resize(s, width, height));

    public static void ResizeCanvas(ImageDocument document, DocumentHistory history, int width, int height,
        CanvasAnchor anchor)
    {
        var (ox, oy) = AnchorOffset(document.Width, document.Height, width, height, anchor);
        Apply(document, history, "History.CanvasSize", width, height,
            p => Place(p, width, height, ox, oy),
            m => PlaceMask(m, width, height, ox, oy),
            s => PlaceSelection(s, width, height, ox, oy));
    }

    public static void Crop(ImageDocument document, DocumentHistory history, IntRect rect)
    {
        var r = rect.Intersect(document.Bounds);
        if (r.IsEmpty || r == document.Bounds)
        {
            return;
        }

        Apply(document, history, "History.Crop", r.Width, r.Height,
            p => Place(p, r.Width, r.Height, -r.X, -r.Y),
            m => PlaceMask(m, r.Width, r.Height, -r.X, -r.Y),
            s => PlaceSelection(s, r.Width, r.Height, -r.X, -r.Y));
    }

    /// <summary>Dreht das ganze Bild um 90°, 180° oder 270° im Uhrzeigersinn.</summary>
    public static void Rotate(ImageDocument document, DocumentHistory history, int degreesClockwise)
    {
        var degrees = ((degreesClockwise % 360) + 360) % 360;
        Func<PixelBuffer, PixelBuffer> transform = degrees switch
        {
            90 => PixelTransforms.Rotate90Clockwise,
            180 => PixelTransforms.Rotate180,
            270 => PixelTransforms.Rotate90CounterClockwise,
            _ => throw new ArgumentOutOfRangeException(nameof(degreesClockwise), "Nur 90, 180 oder 270 Grad."),
        };
        var swap = degrees != 180;
        ApplyPixelTransform(document, history, "History.Rotate" + degrees,
            swap ? document.Height : document.Width, swap ? document.Width : document.Height, transform);
    }

    public static void Flip(ImageDocument document, DocumentHistory history, bool horizontal) =>
        ApplyPixelTransform(document, history, horizontal ? "History.FlipHorizontal" : "History.FlipVertical",
            document.Width, document.Height,
            horizontal ? PixelTransforms.FlipHorizontal : PixelTransforms.FlipVertical);

    /// <summary>Spiegelt nur eine Ebene (samt Maske).</summary>
    public static void FlipLayer(ImageDocument document, DocumentHistory history, PixelLayer layer, bool horizontal)
    {
        Func<PixelBuffer, PixelBuffer> transform = horizontal ? PixelTransforms.FlipHorizontal : PixelTransforms.FlipVertical;
        TransformLayer(document, history, layer, horizontal ? "History.FlipLayerHorizontal" : "History.FlipLayerVertical",
            transform);
    }

    /// <summary>Dreht (beliebiger Winkel, Uhrzeigersinn) und skaliert eine Ebene um die Bildmitte.</summary>
    public static void RotateScaleLayer(ImageDocument document, DocumentHistory history, PixelLayer layer,
        double angleDegrees, double scale) =>
        TransformLayer(document, history, layer, "History.TransformLayer",
            p => Resampler.RotateScale(p, angleDegrees, scale));

    public static (int X, int Y) AnchorOffset(int oldWidth, int oldHeight, int width, int height, CanvasAnchor anchor)
    {
        var column = (int)anchor % 3;
        var row = (int)anchor / 3;
        var x = column switch { 0 => 0, 1 => (width - oldWidth) / 2, _ => width - oldWidth };
        var y = row switch { 0 => 0, 1 => (height - oldHeight) / 2, _ => height - oldHeight };
        return (x, y);
    }

    private static void TransformLayer(ImageDocument document, DocumentHistory history, PixelLayer layer, string nameKey,
        Func<PixelBuffer, PixelBuffer> transform)
    {
        if (layer.IsEffectivelyLocked)
        {
            throw new InvalidOperationException("Die Ebene ist gesperrt.");
        }

        var backup = layer.Pixels.Clone();
        transform(layer.Pixels).Data.CopyTo(layer.Pixels.Data);
        var maskBackup = layer.Mask?.Data.ToArray();
        if (layer.Mask is { } mask)
        {
            var transformed = Resampler.MaskFromGray(transform(Resampler.GrayFromMask(mask.Width, mask.Height, mask.GetRow)));
            transformed.Data.CopyTo(mask.Data);
        }

        var entries = new List<IHistoryEntry>();
        if (PixelRegionEntry.FromBackup(nameKey, layer, backup, document.Bounds) is { } pixelEntry)
        {
            entries.Add(pixelEntry);
        }

        if (layer.Mask is { } m && maskBackup is not null
            && MaskRegionEntry.FromBackup(nameKey, m, maskBackup, document.Bounds) is { } maskEntry)
        {
            entries.Add(maskEntry);
        }

        document.CommitPixelChange(document.Bounds);
        if (entries.Count > 0)
        {
            history.Push(new CompositeEntry(nameKey, entries));
        }
    }

    private static void ApplyPixelTransform(ImageDocument document, DocumentHistory history, string nameKey, int width,
        int height, Func<PixelBuffer, PixelBuffer> transform) =>
        Apply(document, history, nameKey, width, height, transform,
            m => Resampler.MaskFromGray(transform(Resampler.GrayFromMask(m.Width, m.Height, m.GetRow))),
            s =>
            {
                var gray = transform(Resampler.GrayFromMask(s.Width, s.Height, y => s.GetRow(y)));
                return SelectionFromGray(gray);
            });

    private static void Apply(ImageDocument document, DocumentHistory history, string nameKey, int width, int height,
        Func<PixelBuffer, PixelBuffer> pixels, Func<LayerMask, LayerMask> mask, Func<SelectionMask, SelectionMask> selection)
    {
        var before = document.CaptureContent();
        var layers = new Dictionary<PixelLayer, (PixelBuffer, LayerMask?)>();
        foreach (var (layer, (layerPixels, layerMask)) in before.Layers)
        {
            var newMask = layerMask is null ? null : mask(layerMask);
            if (newMask is not null && layerMask is not null)
            {
                newMask.IsEnabled = layerMask.IsEnabled;
            }

            layers[layer] = (pixels(layerPixels), newMask);
        }

        var after = new DocumentContent(width, height, layers, before.Selection is null ? null : selection(before.Selection));
        document.ReplaceContent(after);
        history.Push(new ContentEntry(nameKey, before, after));
    }

    private static PixelBuffer Place(PixelBuffer source, int width, int height, int offsetX, int offsetY)
    {
        var result = PixelBuffer.Create(width, height);
        var target = new IntRect(offsetX, offsetY, source.Width, source.Height).Intersect(result.Bounds);
        for (var y = target.Y; y < target.Bottom; y++)
        {
            source.GetRow(y - offsetY).Slice((target.X - offsetX) * 4, target.Width * 4)
                .CopyTo(result.GetRow(y).Slice(target.X * 4));
        }

        return result;
    }

    private static LayerMask PlaceMask(LayerMask source, int width, int height, int offsetX, int offsetY)
    {
        var mask = LayerMask.CreateRevealAll(width, height); // neue Fläche: sichtbar
        var target = new IntRect(offsetX, offsetY, source.Width, source.Height).Intersect(new IntRect(0, 0, width, height));
        for (var y = target.Y; y < target.Bottom; y++)
        {
            source.GetRow(y - offsetY).Slice(target.X - offsetX, target.Width).CopyTo(mask.GetWritableRow(y).Slice(target.X));
        }

        return mask;
    }

    private static SelectionMask PlaceSelection(SelectionMask source, int width, int height, int offsetX, int offsetY)
    {
        var mask = SelectionMask.CreateEmpty(width, height);
        var target = new IntRect(offsetX, offsetY, source.Width, source.Height).Intersect(new IntRect(0, 0, width, height));
        for (var y = target.Y; y < target.Bottom; y++)
        {
            source.GetRow(y - offsetY).Slice(target.X - offsetX, target.Width).CopyTo(mask.GetRow(y).Slice(target.X));
        }

        return mask;
    }

    private static SelectionMask SelectionFromGray(PixelBuffer gray)
    {
        var mask = SelectionMask.CreateEmpty(gray.Width, gray.Height);
        for (var y = 0; y < gray.Height; y++)
        {
            var row = gray.GetPixelRow(y);
            var target = mask.GetRow(y);
            for (var x = 0; x < gray.Width; x++)
            {
                target[x] = row[x].A;
            }
        }

        return mask;
    }
}
