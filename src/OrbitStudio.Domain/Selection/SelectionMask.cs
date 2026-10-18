using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Domain.Selection;

/// <summary>Verknüpfung einer neuen Auswahl mit der bestehenden.</summary>
public enum SelectionCombineMode
{
    Replace,
    Add,
    Subtract,
    Intersect,
}

/// <summary>
/// Auswahlmaske in Dokumentgröße: 8 Bit je Pixel, 255 = ausgewählt, 0 = nicht ausgewählt, Zwischenwerte = teilweise.
/// Grenzregel: Ein Pixel gehört zur Form, wenn sein Mittelpunkt (x + 0,5; y + 0,5) innerhalb der Form liegt.
/// Bearbeitungen wirken auf jeden Pixel mit dem Faktor Maske/255.
/// </summary>
public sealed class SelectionMask
{
    private readonly byte[] _data;

    private SelectionMask(int width, int height, byte[] data)
    {
        Width = width;
        Height = height;
        _data = data;
    }

    public int Width { get; }

    public int Height { get; }

    public Span<byte> Data => _data;

    public static SelectionMask CreateEmpty(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return new SelectionMask(width, height, new byte[(long)width * height]);
    }

    public static SelectionMask CreateFull(int width, int height)
    {
        var mask = CreateEmpty(width, height);
        Array.Fill(mask._data, (byte)255);
        return mask;
    }

    public byte Get(int x, int y) => _data[y * Width + x];

    public void Set(int x, int y, byte value) => _data[y * Width + x] = value;

    public Span<byte> GetRow(int y) => _data.AsSpan(y * Width, Width);

    public void Fill(IntRect region, byte value)
    {
        var r = region.Intersect(new IntRect(0, 0, Width, Height));
        for (var y = r.Y; y < r.Bottom; y++)
        {
            _data.AsSpan(y * Width + r.X, r.Width).Fill(value);
        }
    }

    /// <summary>True, wenn kein Pixel ausgewählt ist.</summary>
    public bool IsEmpty => !_data.AsSpan().ContainsAnyExcept((byte)0);

    /// <summary>Kleinstes Rechteck, das alle (auch teilweise) ausgewählten Pixel enthält; leer bei leerer Auswahl.</summary>
    public IntRect Bounds()
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (var y = 0; y < Height; y++)
        {
            var row = GetRow(y);
            var first = row.IndexOfAnyExcept((byte)0);
            if (first < 0)
            {
                continue;
            }

            var last = row.LastIndexOfAnyExcept((byte)0);
            minX = Math.Min(minX, first);
            maxX = Math.Max(maxX, last);
            minY = Math.Min(minY, y);
            maxY = y;
        }

        return maxX < 0 ? IntRect.Empty : new IntRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public SelectionMask Clone() => new(Width, Height, (byte[])_data.Clone());

    public bool ContentEquals(SelectionMask other) =>
        Width == other.Width && Height == other.Height && _data.AsSpan().SequenceEqual(other._data);

    public void Invert()
    {
        for (var i = 0; i < _data.Length; i++)
        {
            _data[i] = (byte)(255 - _data[i]);
        }
    }

    /// <summary>Verknüpft eine neue Form mit dieser Auswahl und liefert das Ergebnis als neue Maske.</summary>
    public static SelectionMask Combine(SelectionMask? existing, SelectionMask shape, SelectionCombineMode mode)
    {
        if (existing is null || mode == SelectionCombineMode.Replace)
        {
            return mode == SelectionCombineMode.Subtract || mode == SelectionCombineMode.Intersect
                ? (existing is null ? CreateEmpty(shape.Width, shape.Height) : existing.Clone())
                : shape.Clone();
        }

        if (existing.Width != shape.Width || existing.Height != shape.Height)
        {
            throw new ArgumentException("Auswahlmasken müssen gleich groß sein.", nameof(shape));
        }

        var result = existing.Clone();
        var a = result._data;
        var b = shape._data;
        for (var i = 0; i < a.Length; i++)
        {
            a[i] = mode switch
            {
                SelectionCombineMode.Add => Math.Max(a[i], b[i]),
                SelectionCombineMode.Subtract => (byte)Math.Max(0, a[i] - b[i]),
                SelectionCombineMode.Intersect => Math.Min(a[i], b[i]),
                _ => b[i],
            };
        }

        return result;
    }
}
