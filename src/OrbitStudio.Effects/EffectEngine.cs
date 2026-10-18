using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;

namespace OrbitStudio.Effects;

/// <summary>Ergebnis einer Effektberechnung: Pixel für <see cref="Region"/> (Koordinaten der Ebene).</summary>
public sealed record EffectResult(IntRect Region, PixelBuffer Pixels);

/// <summary>Ungültige Effektparameter (mit den Fehlern der Validierung).</summary>
public sealed class EffectValidationException(IReadOnlyList<EffectValidationError> errors)
    : ArgumentException("Ungültige Effektparameter: " + string.Join(", ", errors.Select(e => e.MessageKey)))
{
    public IReadOnlyList<EffectValidationError> Errors { get; } = errors;
}

/// <summary>
/// Führt Effekte kontrolliert aus (Spezifikation 9): Validierung, Berechnung im Zielbereich, Begrenzung auf die Auswahl
/// (weiche Auswahlwerte mischen anteilig), Abbruch über <see cref="CancellationToken"/>, Übernahme als ein
/// Rückgängig-Schritt. Die Berechnung braucht keinen UI-Thread; nur <see cref="Commit"/> verändert das Dokument.
/// </summary>
public static class EffectEngine
{
    public static IReadOnlyList<EffectValidationError> Validate(IEffect effect, EffectSettings settings)
    {
        var errors = new List<EffectValidationError>();
        foreach (var parameter in effect.Parameters)
        {
            settings.Values.TryGetValue(parameter.Id, out var value);
            if (parameter.Validate(value) is { } error)
            {
                errors.Add(new EffectValidationError(parameter.Id, error));
            }
        }

        if (errors.Count == 0)
        {
            errors.AddRange(effect.ValidateRules(settings));
        }

        return errors;
    }

    /// <summary>Zu berechnender Bereich: Umriss der Auswahl (sonst die ganze Ebene), begrenzt auf die Ebene.</summary>
    public static IntRect TargetRegion(PixelBuffer layer, SelectionMask? selection) =>
        selection is null ? layer.Bounds : selection.Bounds().Intersect(layer.Bounds);

    /// <summary>
    /// Berechnet den Effekt. Mit Auswahl: Ergebnis = Original + (Effekt − Original) · Auswahl/255 (vormultipliziert,
    /// gerundet); außerhalb bleibt das Original. Wirft <see cref="EffectValidationException"/> bei ungültigen Werten und
    /// <see cref="OperationCanceledException"/> bei Abbruch.
    /// </summary>
    public static EffectResult Render(IEffect effect, EffectSettings settings, PixelBuffer source, SelectionMask? selection,
        CancellationToken cancellation, IntRect? region = null)
    {
        var errors = Validate(effect, settings);
        if (errors.Count > 0)
        {
            throw new EffectValidationException(errors);
        }

        var r = (region ?? TargetRegion(source, selection)).Intersect(source.Bounds);
        if (r.IsEmpty)
        {
            return new EffectResult(IntRect.Empty, PixelBuffer.Create(1, 1));
        }

        var output = PixelBuffer.Create(r.Width, r.Height);
        effect.Render(new EffectContext(source, r, output, settings, cancellation));
        cancellation.ThrowIfCancellationRequested();
        if (selection is not null)
        {
            BlendWithSelection(source, selection, r, output);
        }

        return new EffectResult(r, output);
    }

    /// <summary>Überträgt ein Ergebnis in die Ebene und legt einen Rückgängig-Schritt an (null, wenn sich nichts ändert).</summary>
    public static IHistoryEntry? Commit(ImageDocument document, DocumentHistory history, PixelLayer layer, PixelBuffer backup,
        EffectResult result, string nameKey)
    {
        var r = result.Region.Intersect(layer.Pixels.Bounds);
        if (r.IsEmpty)
        {
            return null;
        }

        for (var y = 0; y < r.Height; y++)
        {
            result.Pixels.GetRow(y).Slice(0, r.Width * 4).CopyTo(layer.Pixels.GetRow(r.Y + y).Slice(r.X * 4));
        }

        document.CommitPixelChange(r);
        var entry = PixelRegionEntry.FromBackup(nameKey, layer, backup, r);
        if (entry is not null)
        {
            history.Push(entry);
        }

        return entry;
    }

    /// <summary>Synchron auf die aktive Ebene anwenden (Tests, Plugins). Die Oberfläche rechnet im Hintergrund.</summary>
    public static IHistoryEntry? Apply(ImageDocument document, DocumentHistory history, IEffect effect, EffectSettings settings,
        CancellationToken cancellation = default)
    {
        var layer = document.ActivePixelLayer ?? throw new InvalidOperationException("Keine Pixelebene aktiv.");
        if (layer.IsEffectivelyLocked)
        {
            throw new InvalidOperationException("Die Ebene ist gesperrt.");
        }

        var backup = layer.Pixels.Clone();
        var result = Render(effect, settings, backup, document.Selection, cancellation);
        return Commit(document, history, layer, backup, result, effect.NameKey);
    }

    /// <summary>
    /// Vorschau-Quelle: Ebene (und Auswahl) auf höchstens <paramref name="maxWidth"/> × <paramref name="maxHeight"/>
    /// verkleinert (Flächenmittel bzw. nächster Nachbar). Längenparameter werden beim Rendern mitskaliert.
    /// </summary>
    public static EffectPreview CreatePreview(PixelBuffer source, SelectionMask? selection, int maxWidth, int maxHeight)
    {
        var scale = Math.Min(1, Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height));
        if (scale >= 1)
        {
            return new EffectPreview(source.Clone(), selection?.Clone(), 1);
        }

        var w = Math.Max(1, (int)Math.Round(source.Width * scale));
        var h = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new EffectPreview(Downscale(source, w, h), selection is null ? null : Downscale(selection, w, h),
            (double)w / source.Width);
    }

    private static void BlendWithSelection(PixelBuffer source, SelectionMask selection, IntRect r, PixelBuffer output)
    {
        for (var y = 0; y < r.Height; y++)
        {
            var sel = selection.GetRow(r.Y + y);
            var src = source.GetPixelRow(r.Y + y);
            var dst = output.GetPixelRow(y);
            for (var x = 0; x < r.Width; x++)
            {
                var amount = sel[r.X + x];
                if (amount == 255)
                {
                    continue;
                }

                var s = src[r.X + x];
                if (amount == 0)
                {
                    dst[x] = s;
                    continue;
                }

                var e = dst[x];
                var t = amount / 255.0;
                dst[x] = PixelMath.FromPremultiplied(s.B + (e.B - s.B) * t, s.G + (e.G - s.G) * t, s.R + (e.R - s.R) * t,
                    s.A + (e.A - s.A) * t);
            }
        }
    }

    private static PixelBuffer Downscale(PixelBuffer source, int width, int height)
    {
        var result = PixelBuffer.Create(width, height);
        Parallel.For(0, height, ty =>
        {
            var y0 = (int)((long)ty * source.Height / height);
            var y1 = Math.Max(y0 + 1, (int)((long)(ty + 1) * source.Height / height));
            var target = result.GetPixelRow(ty);
            for (var tx = 0; tx < width; tx++)
            {
                var x0 = (int)((long)tx * source.Width / width);
                var x1 = Math.Max(x0 + 1, (int)((long)(tx + 1) * source.Width / width));
                double b = 0, g = 0, r = 0, a = 0;
                for (var y = y0; y < y1; y++)
                {
                    var row = source.GetPixelRow(y);
                    for (var x = x0; x < x1; x++)
                    {
                        b += row[x].B;
                        g += row[x].G;
                        r += row[x].R;
                        a += row[x].A;
                    }
                }

                var n = (double)(x1 - x0) * (y1 - y0);
                target[tx] = PixelMath.FromPremultiplied(b / n, g / n, r / n, a / n);
            }
        });
        return result;
    }

    private static SelectionMask Downscale(SelectionMask selection, int width, int height)
    {
        var result = SelectionMask.CreateEmpty(width, height);
        for (var y = 0; y < height; y++)
        {
            var sy = (int)(((long)y * 2 + 1) * selection.Height / (2L * height));
            for (var x = 0; x < width; x++)
            {
                result.Set(x, y, selection.Get((int)(((long)x * 2 + 1) * selection.Width / (2L * width)), sy));
            }
        }

        return result;
    }
}

/// <summary>Verkleinerte Vorschau-Quelle mit Maßstab (Vorschaupixel je Bildpixel).</summary>
public sealed record EffectPreview(PixelBuffer Source, SelectionMask? Selection, double Scale)
{
    /// <summary>Ganze Vorschau berechnen (außerhalb der Auswahl unverändert); Längenparameter mit dem Maßstab skaliert.</summary>
    public PixelBuffer Render(IEffect effect, EffectSettings settings, CancellationToken cancellation) =>
        EffectEngine.Render(effect, settings.Scaled(effect, Scale), Source, Selection, cancellation, Source.Bounds).Pixels;
}
