using OrbitStudio.Tools.Drawing;
using OrbitStudio.Tools.Fill;
using OrbitStudio.Tools.Selection;
using OrbitStudio.Tools.Transform;

namespace OrbitStudio.Tools;

/// <summary>Standardsatz der Werkzeuge in Anzeigereihenfolge (Plugins können ab M10 weitere ergänzen).</summary>
public static class ToolSet
{
    public static IReadOnlyList<ITool> CreateDefault() =>
    [
        // Auswahl
        new RectangleSelectTool(),
        new EllipseSelectTool(),
        new LassoSelectTool(),
        new PolygonSelectTool(),
        new MagicWandTool(contiguous: true),
        new MagicWandTool(contiguous: false),

        // Transformation
        new MoveTool(),
        new CropTool(),

        // Zeichnen
        new BrushTool(),
        new PencilTool(),
        new EraserTool(),
        new FreehandTool(),
        new LineTool(),
        new DashPatternTool(),
        new RectangleTool(filled: false),
        new RectangleTool(filled: true),
        new EllipseTool(filled: false),
        new EllipseTool(filled: true),

        // Füllung und Farbe
        new BucketTool(),
        new GradientTool(),
        new PipetteTool(),

        // Ansicht
        new HandTool(),
        new ZoomTool(),
    ];
}
