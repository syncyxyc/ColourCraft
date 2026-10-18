using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;

namespace OrbitStudio.History;

/// <summary>
/// Delta eines rechteckigen Pixelbereichs einer Ebene: nur der geänderte Bereich wird gespeichert (vorher/nachher).
/// </summary>
public sealed class PixelRegionEntry : IHistoryEntry
{
    private readonly PixelLayer _layer;
    private readonly IntRect _region;
    private readonly byte[] _before;
    private readonly byte[] _after;

    private PixelRegionEntry(string nameKey, PixelLayer layer, IntRect region, byte[] before, byte[] after)
    {
        NameKey = nameKey;
        _layer = layer;
        _region = region;
        _before = before;
        _after = after;
    }

    public string NameKey { get; }

    public IntRect Region => _region;

    public long SizeBytes => _before.Length + _after.Length;

    /// <summary>
    /// Erzeugt den Eintrag aus einer Sicherung vor der Änderung und dem aktuellen Ebeneninhalt.
    /// Liefert null, wenn der Bereich leer ist oder sich nichts geändert hat.
    /// </summary>
    public static PixelRegionEntry? FromBackup(string nameKey, PixelLayer layer, PixelBuffer backup, IntRect region)
    {
        var r = region.Intersect(layer.Pixels.Bounds);
        if (r.IsEmpty)
        {
            return null;
        }

        var before = Copy(backup, r);
        var after = Copy(layer.Pixels, r);
        return before.AsSpan().SequenceEqual(after) ? null : new PixelRegionEntry(nameKey, layer, r, before, after);
    }

    public void Undo(ImageDocument document) => Write(document, _before);

    public void Redo(ImageDocument document) => Write(document, _after);

    private void Write(ImageDocument document, byte[] data)
    {
        var rowBytes = _region.Width * PixelBuffer.BytesPerPixel;
        for (var y = 0; y < _region.Height; y++)
        {
            data.AsSpan(y * rowBytes, rowBytes)
                .CopyTo(_layer.Pixels.GetRow(_region.Y + y).Slice(_region.X * PixelBuffer.BytesPerPixel, rowBytes));
        }

        document.CommitPixelChange(_region);
    }

    internal static byte[] Copy(PixelBuffer buffer, IntRect r)
    {
        var rowBytes = r.Width * PixelBuffer.BytesPerPixel;
        var data = new byte[rowBytes * r.Height];
        for (var y = 0; y < r.Height; y++)
        {
            buffer.GetRow(r.Y + y).Slice(r.X * PixelBuffer.BytesPerPixel, rowBytes).CopyTo(data.AsSpan(y * rowBytes));
        }

        return data;
    }
}

/// <summary>Delta eines rechteckigen Bereichs einer Ebenenmaske.</summary>
public sealed class MaskRegionEntry : IHistoryEntry
{
    private readonly LayerMask _mask;
    private readonly IntRect _region;
    private readonly byte[] _before;
    private readonly byte[] _after;

    private MaskRegionEntry(string nameKey, LayerMask mask, IntRect region, byte[] before, byte[] after)
    {
        NameKey = nameKey;
        _mask = mask;
        _region = region;
        _before = before;
        _after = after;
    }

    public string NameKey { get; }

    public long SizeBytes => _before.Length + _after.Length;

    public static MaskRegionEntry? FromBackup(string nameKey, LayerMask mask, byte[] backup, IntRect region)
    {
        var r = region.Intersect(new IntRect(0, 0, mask.Width, mask.Height));
        if (r.IsEmpty)
        {
            return null;
        }

        var before = new byte[r.Width * r.Height];
        var after = new byte[before.Length];
        for (var y = 0; y < r.Height; y++)
        {
            backup.AsSpan((r.Y + y) * mask.Width + r.X, r.Width).CopyTo(before.AsSpan(y * r.Width));
            mask.GetRow(r.Y + y).Slice(r.X, r.Width).CopyTo(after.AsSpan(y * r.Width));
        }

        return before.AsSpan().SequenceEqual(after) ? null : new MaskRegionEntry(nameKey, mask, r, before, after);
    }

    public void Undo(ImageDocument document) => Write(document, _before);

    public void Redo(ImageDocument document) => Write(document, _after);

    private void Write(ImageDocument document, byte[] data)
    {
        for (var y = 0; y < _region.Height; y++)
        {
            data.AsSpan(y * _region.Width, _region.Width).CopyTo(_mask.GetWritableRow(_region.Y + y).Slice(_region.X));
        }

        document.CommitPixelChange(_region);
    }
}

/// <summary>Auswahl vorher/nachher (Masken werden nicht verändert, nur ausgetauscht).</summary>
public sealed class SelectionEntry(string nameKey, SelectionMask? before, SelectionMask? after) : IHistoryEntry
{
    public string NameKey { get; } = nameKey;

    public long SizeBytes => (before?.Data.Length ?? 0) + (after?.Data.Length ?? 0);

    public void Undo(ImageDocument document) => document.SetSelection(before);

    public void Redo(ImageDocument document) => document.SetSelection(after);
}

/// <summary>Bildgröße und alle Puffer vorher/nachher (Bildgröße, Leinwand, Drehen, Spiegeln, Zuschneiden).</summary>
public sealed class ContentEntry(string nameKey, DocumentContent before, DocumentContent after) : IHistoryEntry
{
    public string NameKey { get; } = nameKey;

    public long SizeBytes => before.SizeBytes + after.SizeBytes;

    public void Undo(ImageDocument document) => document.ReplaceContent(before);

    public void Redo(ImageDocument document) => document.ReplaceContent(after);
}

/// <summary>
/// Ebenenstruktur und -eigenschaften vorher/nachher; optional Pixel- und Maskeninhalte der Ebenen, die die
/// Operation verändert (z. B. Zusammenführen, Maske anwenden). Diese werden als vollständige Kopien gesichert.
/// </summary>
public sealed class LayerTreeEntry : IHistoryEntry
{
    private readonly LayerTreeSnapshot _before;
    private readonly LayerTreeSnapshot _after;
    private readonly List<(PixelLayer Layer, PixelBuffer Before, PixelBuffer After)> _pixels;
    private readonly List<(LayerMask Mask, byte[] Before, byte[] After)> _masks;

    private LayerTreeEntry(string nameKey, LayerTreeSnapshot before, LayerTreeSnapshot after,
        List<(PixelLayer, PixelBuffer, PixelBuffer)> pixels, List<(LayerMask, byte[], byte[])> masks)
    {
        NameKey = nameKey;
        _before = before;
        _after = after;
        _pixels = pixels;
        _masks = masks;
    }

    public string NameKey { get; }

    /// <summary>Schlüssel zum Zusammenfassen gleichartiger Folgeschritte (z. B. „opacity:&lt;Ebenen-ID&gt;"), sonst null.</summary>
    public string? CoalesceKey { get; init; }

    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    public long SizeBytes => _pixels.Sum(p => (long)p.Before.Data.Length + p.After.Data.Length)
                             + _masks.Sum(m => (long)m.Before.Length + m.After.Length);

    /// <summary>
    /// Kombiniert zwei aufeinanderfolgende reine Struktur-/Eigenschaftsschritte zu einem (Zustand vor dem ersten,
    /// Zustand nach dem zweiten). Liefert null, wenn einer der beiden Pixel oder Masken sichert.
    /// </summary>
    public static LayerTreeEntry? Merge(LayerTreeEntry first, LayerTreeEntry second) =>
        first._pixels.Count > 0 || first._masks.Count > 0 || second._pixels.Count > 0 || second._masks.Count > 0
            ? null
            : new LayerTreeEntry(second.NameKey, first._before, second._after, [], [])
            {
                CoalesceKey = second.CoalesceKey,
                CreatedUtc = second.CreatedUtc,
            };

    /// <summary>
    /// Führt eine Ebenenoperation aus und zeichnet sie auf. Wirft die Operation eine Ausnahme, wird nichts
    /// aufgezeichnet und der Ausgangszustand bleibt erhalten (Operationen prüfen ihre Vorbedingungen vor Änderungen).
    /// </summary>
    /// <param name="changedPixels">Ebenen, deren Pixel die Operation verändert.</param>
    /// <param name="changedMasks">Masken, deren Inhalt die Operation verändert.</param>
    public static LayerTreeEntry Record(string nameKey, ImageDocument document, Action operation,
        IEnumerable<PixelLayer>? changedPixels = null, IEnumerable<LayerMask>? changedMasks = null,
        string? coalesceKey = null)
    {
        var before = LayerTreeSnapshot.Capture(document);
        var pixelLayers = changedPixels?.Distinct().ToList() ?? [];
        var masks = changedMasks?.Distinct().ToList() ?? [];
        var pixelBackups = pixelLayers.Select(l => l.Pixels.Clone()).ToList();
        var maskBackups = masks.Select(m => m.Data.ToArray()).ToList();

        operation();

        var after = LayerTreeSnapshot.Capture(document);
        var pixels = pixelLayers.Select((l, i) => (l, pixelBackups[i], l.Pixels.Clone())).ToList();
        var maskData = masks.Select((m, i) => (m, maskBackups[i], m.Data.ToArray())).ToList();
        return new LayerTreeEntry(nameKey, before, after, pixels, maskData) { CoalesceKey = coalesceKey };
    }

    public void Undo(ImageDocument document)
    {
        foreach (var (layer, before, _) in _pixels)
        {
            before.Data.CopyTo(layer.Pixels.Data);
        }

        foreach (var (mask, before, _) in _masks)
        {
            before.CopyTo(mask.Data);
        }

        _before.Restore(document);
    }

    public void Redo(ImageDocument document)
    {
        foreach (var (layer, _, after) in _pixels)
        {
            after.Data.CopyTo(layer.Pixels.Data);
        }

        foreach (var (mask, _, after) in _masks)
        {
            after.CopyTo(mask.Data);
        }

        _after.Restore(document);
    }
}
