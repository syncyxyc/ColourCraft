using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.Tools;

/// <summary>Handwerkzeug: verschiebt die Ansicht (verändert keine Pixel).</summary>
public sealed class HandTool : ToolBase
{
    private PointD? _last;

    public override string Id => "hand";

    public override ToolCategory Category => ToolCategory.View;

    public override string Glyph => "✋";

    public override string? Shortcut => "H";

    public override ToolCursor Cursor => ToolCursor.Hand;

    public override bool IsActiveOperation => _last is not null;

    public override void PointerDown(ToolContext context, ToolPointerEvent e) => _last = e.ViewportPosition;

    public override void PointerMove(ToolContext context, ToolPointerEvent e)
    {
        if (_last is { } last && context.View is { } view)
        {
            view.PanBy(e.ViewportPosition.X - last.X, e.ViewportPosition.Y - last.Y);
            _last = e.ViewportPosition;
        }
    }

    public override void PointerUp(ToolContext context, ToolPointerEvent e) => _last = null;

    public override void Cancel(ToolContext context) => _last = null;

    public override void Commit(ToolContext context) => _last = null;
}

/// <summary>Zoomwerkzeug: Klick vergrößert am Zeiger, Rechtsklick oder Alt+Klick verkleinert.</summary>
public sealed class ZoomTool : ToolBase
{
    public override string Id => "zoom";

    public override ToolCategory Category => ToolCategory.View;

    public override string Glyph => "🔍";

    public override string? Shortcut => "Z";

    public override ToolCursor Cursor => ToolCursor.ZoomIn;

    public override void PointerDown(ToolContext context, ToolPointerEvent e)
    {
        if (context.View is not { } view)
        {
            return;
        }

        if (e.Button == PointerButton.Secondary || e.Alt)
        {
            view.ZoomOut(e.ViewportPosition);
        }
        else
        {
            view.ZoomIn(e.ViewportPosition);
        }
    }
}
