using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.History;

namespace OrbitStudio.Tools.Painting;

public enum PaintMode
{
    /// <summary>Farbe mit Source-over auftragen.</summary>
    Paint,

    /// <summary>Deckkraft verringern (Radierer).</summary>
    Erase,
}

/// <summary>
/// Eine laufende Malbearbeitung auf einer Ebene (oder ihrer Maske). Arbeitet nach dem Prinzip
/// „Sicherung + Abdeckung": Der Zustand vor Beginn bleibt in einer Sicherung erhalten; jede Aktualisierung berechnet
/// die betroffenen Pixel neu aus Sicherung und Abdeckung. Dadurch addiert sich die Deckkraft innerhalb eines Zuges
/// nicht auf, Formvorschauen lassen sich beliebig oft neu berechnen und Abbrechen stellt exakt den Ausgangszustand her.
/// Wirkung je Pixel = Abdeckung · Deckkraft · Auswahl/255. Bildgrenzen begrenzen immer.
/// </summary>
public sealed class PaintSession
{
    private readonly ImageDocument _document;
    private readonly PixelLayer _layer;
    private readonly LayerMask? _mask;
    private readonly PixelBuffer? _pixelBackup;
    private readonly byte[]? _maskBackup;
    private readonly float[] _coverage;
    private readonly PaintMode _mode;
    private readonly double _opacity;
    private readonly double _b;
    private readonly double _g;
    private readonly double _r;
    private readonly double _a;
    private readonly byte _maskTarget;
    private readonly int _width;
    private Func<int, int, (double B, double G, double R, double A)>? _colorAt;

    /// <param name="color">Farbe (gerades Alpha). Bei Masken zählt nur die Helligkeit als Maskenwert.</param>
    /// <param name="maskTarget">True: die Maske der Ebene bearbeiten (muss vorhanden sein).</param>
    public PaintSession(ImageDocument document, PixelLayer layer, PaintMode mode, ColorRgba color, double opacity,
        bool maskTarget = false)
    {
        _document = document;
        _layer = layer;
        _mode = mode;
        _opacity = Math.Clamp(opacity, 0, 1);
        _width = document.Width;
        _coverage = new float[(long)document.Width * document.Height];

        if (maskTarget)
        {
            _mask = layer.Mask ?? throw new InvalidOperationException("Die Ebene hat keine Maske.");
            _maskBackup = _mask.Data.ToArray();
            // Malen: Helligkeit der Farbe (Rec. 601) als Maskenwert; Radieren: Maske wieder öffnen (255).
            _maskTarget = mode == PaintMode.Erase
                ? (byte)255
                : (byte)Math.Round(0.299 * color.R + 0.587 * color.G + 0.114 * color.B);
        }
        else
        {
            _pixelBackup = layer.Pixels.Clone();
            var premultiplied = color.ToPremultiplied();
            _b = premultiplied.B;
            _g = premultiplied.G;
            _r = premultiplied.R;
            _a = premultiplied.A;
        }
    }

    public PixelLayer Layer => _layer;

    /// <summary>Alle bisher veränderten Pixel (für Historie und Abbrechen).</summary>
    public IntRect Touched { get; private set; } = IntRect.Empty;

    /// <summary>
    /// Farbe je Pixel statt einer festen Farbe (z. B. Verlauf). Werte vormultipliziert, 0–255, Reihenfolge B, G, R, A.
    /// Auf Masken zählt die Helligkeit der (nicht vormultiplizierten) Farbe.
    /// </summary>
    public void SetColorFunction(Func<int, int, (double B, double G, double R, double A)>? colorAt) => _colorAt = colorAt;

    /// <summary>Erhöht die Abdeckung eines Pixels (Maximum, keine Summe).</summary>
    public void AddCoverage(int x, int y, double value)
    {
        if (value <= 0 || (uint)x >= (uint)_width || (uint)y >= (uint)_document.Height)
        {
            return;
        }

        ref var c = ref _coverage[y * _width + x];
        if (value > c)
        {
            c = (float)Math.Min(1, value);
        }
    }

    /// <summary>Setzt die Abdeckung im Bereich auf 0 (für neu zu berechnende Formvorschauen).</summary>
    public void ClearCoverage(IntRect region)
    {
        var r = region.Intersect(_document.Bounds);
        for (var y = r.Y; y < r.Bottom; y++)
        {
            Array.Clear(_coverage, y * _width + r.X, r.Width);
        }
    }

    /// <summary>Wertet eine Abdeckungsfunktion für alle Pixel im Bereich aus (Maximum mit dem bisherigen Wert).</summary>
    public void Rasterize(IntRect region, Func<PointD, double> coverage)
    {
        var r = region.Intersect(_document.Bounds);
        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                AddCoverage(x, y, coverage(new PointD(x + 0.5, y + 0.5)));
            }
        }
    }

    /// <summary>Berechnet die Pixel im Bereich neu aus Sicherung und Abdeckung und meldet die Änderung.</summary>
    public void Apply(IntRect region)
    {
        var r = region.Intersect(_document.Bounds);
        if (r.IsEmpty)
        {
            return;
        }

        var selection = _document.Selection;
        for (var y = r.Y; y < r.Bottom; y++)
        {
            var selectionRow = selection is null ? default : selection.GetRow(y);
            if (_mask is not null)
            {
                ApplyMaskRow(y, r, selectionRow);
            }
            else
            {
                ApplyPixelRow(y, r, selectionRow);
            }
        }

        Touched = Touched.Union(r);
        _document.CommitPixelChange(r);
    }

    private void ApplyPixelRow(int y, IntRect r, ReadOnlySpan<byte> selectionRow)
    {
        var target = _layer.Pixels.GetRow(y);
        var backup = _pixelBackup!.GetRow(y);
        for (var x = r.X; x < r.Right; x++)
        {
            var i = x * 4;
            var amount = _coverage[y * _width + x] * _opacity;
            if (!selectionRow.IsEmpty)
            {
                amount *= selectionRow[x] / 255.0;
            }

            if (amount <= 0)
            {
                backup.Slice(i, 4).CopyTo(target.Slice(i, 4));
                continue;
            }

            double b = backup[i], g = backup[i + 1], rr = backup[i + 2], a = backup[i + 3];
            if (_mode == PaintMode.Erase)
            {
                var keep = 1 - amount;
                b *= keep;
                g *= keep;
                rr *= keep;
                a *= keep;
            }
            else
            {
                // Source-over, vormultipliert: Quelle = Farbe · Wirkung
                var (cb, cg, cr, ca) = _colorAt is null ? (_b, _g, _r, _a) : _colorAt(x, y);
                var sa = ca * amount;
                var keep = 1 - sa / 255.0;
                b = cb * amount + b * keep;
                g = cg * amount + g * keep;
                rr = cr * amount + rr * keep;
                a = sa + a * keep;
            }

            var alpha = ToByte(a);
            target[i] = Math.Min(ToByte(b), alpha);
            target[i + 1] = Math.Min(ToByte(g), alpha);
            target[i + 2] = Math.Min(ToByte(rr), alpha);
            target[i + 3] = alpha;
        }
    }

    private void ApplyMaskRow(int y, IntRect r, ReadOnlySpan<byte> selectionRow)
    {
        var target = _mask!.GetWritableRow(y);
        for (var x = r.X; x < r.Right; x++)
        {
            var backup = _maskBackup![y * _width + x];
            var amount = _coverage[y * _width + x] * _opacity;
            if (!selectionRow.IsEmpty)
            {
                amount *= selectionRow[x] / 255.0;
            }

            var maskTarget = (double)_maskTarget;
            if (_colorAt is not null && _mode == PaintMode.Paint)
            {
                var (cb, cg, cr, ca) = _colorAt(x, y);
                maskTarget = ca <= 0 ? 0 : (0.299 * cr + 0.587 * cg + 0.114 * cb) * 255 / ca;
            }

            target[x] = amount <= 0 ? backup : ToByte(backup + (maskTarget - backup) * amount);
        }
    }

    /// <summary>Schließt die Bearbeitung ab und legt einen Historieneintrag an (null, wenn nichts geändert wurde).</summary>
    public IHistoryEntry? Commit(ToolContext context, string nameKey)
    {
        IHistoryEntry? entry = _mask is not null
            ? MaskRegionEntry.FromBackup(nameKey, _mask, _maskBackup!, Touched)
            : PixelRegionEntry.FromBackup(nameKey, _layer, _pixelBackup!, Touched);
        if (entry is not null)
        {
            context.History.Push(entry);
        }

        return entry;
    }

    /// <summary>Stellt den Zustand vor Beginn exakt wieder her.</summary>
    public void Cancel()
    {
        var r = Touched;
        if (r.IsEmpty)
        {
            return;
        }

        for (var y = r.Y; y < r.Bottom; y++)
        {
            if (_mask is not null)
            {
                _maskBackup.AsSpan(y * _width + r.X, r.Width).CopyTo(_mask.GetWritableRow(y).Slice(r.X));
            }
            else
            {
                _pixelBackup!.GetRow(y).Slice(r.X * 4, r.Width * 4).CopyTo(_layer.Pixels.GetRow(y).Slice(r.X * 4));
            }
        }

        Touched = IntRect.Empty;
        _document.CommitPixelChange(r);
    }

    private static byte ToByte(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded <= 0 ? (byte)0 : rounded >= 255 ? (byte)255 : (byte)rounded;
    }
}
