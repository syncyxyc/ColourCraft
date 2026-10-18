using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.History;
using OrbitStudio.Rendering;

namespace OrbitStudio.Tools.Tests;

/// <summary>Hilfen, um Werkzeuge ohne Oberfläche mit Zeigerereignissen zu bedienen.</summary>
public abstract class ToolTestBase
{
    protected ToolTestBase(int width = 20, int height = 20, DocumentBackground background = DocumentBackground.White)
    {
        Document = ImageDocument.Create("T", width, height, "HG", background: background);
        History = new DocumentHistory(Document);
        View = new ViewTransform(width, height);
        View.SetViewport(400, 400, 1.0);
        Context = new ToolContext(Document, History, Colors, View);
        Context.Problem += (_, p) => Problems.Add(p);
    }

    protected ImageDocument Document { get; }

    protected DocumentHistory History { get; }

    protected ToolColors Colors { get; } = new();

    protected ViewTransform View { get; }

    protected ToolContext Context { get; set; }

    protected List<ToolProblem> Problems { get; } = [];

    protected PixelLayer Layer => Document.PixelLayers.First();

    protected PixelBuffer Pixels => Layer.Pixels;

    protected static ToolPointerEvent At(double x, double y, PointerButton button = PointerButton.Primary,
        ToolModifiers modifiers = ToolModifiers.None, int clicks = 1) =>
        new(new PointD(x, y), new PointD(x, y), button, modifiers, clicks);

    /// <summary>Ziehen von a nach b über Zwischenpunkte.</summary>
    protected void Drag(ITool tool, double x0, double y0, double x1, double y1, int steps = 4,
        PointerButton button = PointerButton.Primary, ToolModifiers modifiers = ToolModifiers.None)
    {
        tool.PointerDown(Context, At(x0, y0, button, modifiers));
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            tool.PointerMove(Context, At(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, button, modifiers));
        }

        tool.PointerUp(Context, At(x1, y1, button, modifiers));
    }

    protected void Click(ITool tool, double x, double y, PointerButton button = PointerButton.Primary,
        ToolModifiers modifiers = ToolModifiers.None, int clicks = 1)
    {
        tool.PointerDown(Context, At(x, y, button, modifiers, clicks));
        tool.PointerUp(Context, At(x, y, button, modifiers, clicks));
    }

    protected int CountPixels(Func<Bgra32, bool> predicate)
    {
        var count = 0;
        for (var y = 0; y < Pixels.Height; y++)
        {
            foreach (var p in Pixels.GetPixelRow(y))
            {
                if (predicate(p))
                {
                    count++;
                }
            }
        }

        return count;
    }
}
