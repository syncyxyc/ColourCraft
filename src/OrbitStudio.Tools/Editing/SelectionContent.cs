using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.History;
using OrbitStudio.Tools.Painting;

namespace OrbitStudio.Tools.Editing;

/// <summary>Inhalte der Auswahl kopieren und löschen (Grundlage für Ausschneiden, Kopieren, Löschen).</summary>
public static class SelectionContent
{
    /// <summary>
    /// Kopiert die ausgewählten Pixel einer Ebene (ohne Auswahl: die ganze Ebene), zugeschnitten auf die
    /// Auswahlgrenzen. Teilweise ausgewählte Pixel werden anteilig übernommen.
    /// </summary>
    public static (PixelBuffer Pixels, IntRect Origin)? Copy(ImageDocument document, PixelLayer layer)
    {
        var bounds = document.Selection?.Bounds() ?? document.Bounds;
        if (bounds.IsEmpty)
        {
            return null;
        }

        var result = PixelBuffer.Create(bounds.Width, bounds.Height);
        for (var y = 0; y < bounds.Height; y++)
        {
            var source = layer.Pixels.GetPixelRow(bounds.Y + y);
            var target = result.GetPixelRow(y);
            for (var x = 0; x < bounds.Width; x++)
            {
                var amount = document.EditableAmount(bounds.X + x, bounds.Y + y);
                var p = source[bounds.X + x];
                target[x] = amount == 255
                    ? p
                    : new Bgra32(ColorMath.Premultiply(p.B, amount), ColorMath.Premultiply(p.G, amount),
                        ColorMath.Premultiply(p.R, amount), ColorMath.Premultiply(p.A, amount));
            }
        }

        return (result, bounds);
    }

    /// <summary>Löscht die ausgewählten Pixel (macht sie transparent) mit Historieneintrag.</summary>
    public static bool Delete(ToolContext context, string nameKey = "History.Delete")
    {
        var layer = context.Document.ActivePixelLayer;
        if (layer is null)
        {
            context.Report(ToolProblem.NoPixelLayer);
            return false;
        }

        if (layer.IsEffectivelyLocked)
        {
            context.Report(ToolProblem.LayerLocked);
            return false;
        }

        var bounds = context.Document.Selection?.Bounds() ?? context.Document.Bounds;
        var session = new PaintSession(context.Document, layer, PaintMode.Erase, ColorRgba.Black, 1.0);
        session.Rasterize(bounds, _ => 1);
        session.Apply(bounds);
        return session.Commit(context, nameKey) is not null;
    }
}
