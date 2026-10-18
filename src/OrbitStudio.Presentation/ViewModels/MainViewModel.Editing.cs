using OrbitStudio.Application.Commands;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Domain.Selection;
using OrbitStudio.History;
using OrbitStudio.Layers;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Tools;
using OrbitStudio.Tools.Editing;
using OrbitStudio.Tools.Selection;
using OrbitStudio.Tools.Transform;
using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Bearbeiten (Rückgängig, Zwischenablage, Auswahl), Bild-Transformationen, Werkzeuge und Farben.</summary>
public sealed partial class MainViewModel
{
    private (PixelBuffer Pixels, IntRect Origin)? _lastCopy;

    private DocumentHistory? ActiveHistory => Doc is { } d ? _histories.For(d) : null;

    private void RegisterEditingCommands()
    {
        bool HasDoc() => !IsBusy && Doc is not null;
        bool HasSelection() => HasDoc() && Doc!.HasSelection;
        bool HasPixelLayer() => HasDoc() && Doc!.ActivePixelLayer is not null;
        bool CanEditPixels() => HasPixelLayer() && !Doc!.ActivePixelLayer!.IsEffectivelyLocked;

        void Add(AppCommand c) => _registry.Register(c);

        // Bearbeiten
        Add(AppCommand.Create(EditUndo, "Cmd.Edit.Undo", Undo, () => HasDoc() && ActiveHistory!.CanUndo, "Ctrl+Z"));
        Add(AppCommand.Create(EditRedo, "Cmd.Edit.Redo", Redo, () => HasDoc() && ActiveHistory!.CanRedo, "Ctrl+Y"));
        Add(AppCommand.Create(EditCut, "Cmd.Edit.Cut", Cut, CanEditPixels, "Ctrl+X"));
        Add(AppCommand.Create(EditCopy, "Cmd.Edit.Copy", () => Copy(), HasPixelLayer, "Ctrl+C"));
        Add(AppCommand.Create(EditPaste, "Cmd.Edit.Paste", Paste, HasDoc, "Ctrl+V"));
        Add(AppCommand.Create(EditDelete, "Cmd.Edit.Delete",
            () => WithHistory((d, h) => SelectionContent.Delete(Tools.ContextFor(ActiveDocument!))), CanEditPixels, "Delete"));
        Add(AppCommand.Create(EditSelectAll, "Cmd.Edit.SelectAll", () => WithHistory(SelectionEditing.SelectAll), HasDoc,
            "Ctrl+A"));
        Add(AppCommand.Create(EditDeselect, "Cmd.Edit.Deselect", () => WithHistory(SelectionEditing.Deselect), HasSelection,
            "Ctrl+D"));
        Add(AppCommand.Create(EditInvertSelection, "Cmd.Edit.InvertSelection", () => WithHistory(SelectionEditing.Invert),
            HasDoc, "Ctrl+I"));
        Add(AppCommand.Create(EditGrowSelection, "Cmd.Edit.GrowSelection", () => GrowOrShrink(grow: true), HasSelection));
        Add(AppCommand.Create(EditShrinkSelection, "Cmd.Edit.ShrinkSelection", () => GrowOrShrink(grow: false), HasSelection));

        // Bild
        Add(AppCommand.Create(ImageResize, "Cmd.Image.Resize", ResizeImage, HasDoc, "Ctrl+R"));
        Add(AppCommand.Create(ImageCanvasSize, "Cmd.Image.CanvasSize", ResizeCanvas, HasDoc, "Ctrl+Shift+R"));
        Add(AppCommand.Create(ImageRotateCw, "Cmd.Image.RotateCw", () => WithHistory((d, h) => DocumentTransforms.Rotate(d, h, 90)),
            HasDoc, "Ctrl+H"));
        Add(AppCommand.Create(ImageRotateCcw, "Cmd.Image.RotateCcw",
            () => WithHistory((d, h) => DocumentTransforms.Rotate(d, h, 270)), HasDoc, "Ctrl+Shift+H"));
        Add(AppCommand.Create(ImageRotate180, "Cmd.Image.Rotate180",
            () => WithHistory((d, h) => DocumentTransforms.Rotate(d, h, 180)), HasDoc));
        Add(AppCommand.Create(ImageFlipH, "Cmd.Image.FlipH",
            () => WithHistory((d, h) => DocumentTransforms.Flip(d, h, horizontal: true)), HasDoc));
        Add(AppCommand.Create(ImageFlipV, "Cmd.Image.FlipV",
            () => WithHistory((d, h) => DocumentTransforms.Flip(d, h, horizontal: false)), HasDoc));
        Add(AppCommand.Create(ImageCrop, "Cmd.Image.Crop",
            () => WithHistory((d, h) => DocumentTransforms.Crop(d, h, d.Selection!.Bounds())), HasSelection, "Ctrl+Shift+X"));

        // Werkzeuge (einfache Tastenkürzel ohne Strg werden im Hauptfenster ausgewertet)
        foreach (var tool in Tools.Tools)
        {
            var captured = tool;
            Add(AppCommand.Toggle(ToolPrefix + tool.Id, tool.NameKey, () => Tools.ActiveTool = captured,
                () => Tools.ActiveTool == captured, gesture: tool.Shortcut));
        }

        Add(AppCommand.Create(ColorsSwap, "Cmd.Colors.Swap", () => Tools.Colors.Swap(), gesture: "X"));
        Add(AppCommand.Create(ColorsReset, "Cmd.Colors.Reset", () => Tools.Colors.Reset(), gesture: "D"));
        Add(AppCommand.Toggle(ToolsOptions, "Cmd.Tools.Options",
            () => _settings.UpdateWorkspace(_settings.Workspace with { ShowToolOptions = !_settings.Workspace.ShowToolOptions }),
            () => _settings.Workspace.ShowToolOptions));
    }

    public bool ShowToolOptions => _settings.Workspace.ShowToolOptions;

    public System.Windows.Input.ICommand SwapColorsCommand => Adapter(ColorsSwap);

    public System.Windows.Input.ICommand ResetColorsCommand => Adapter(ColorsReset);

    public void Undo()
    {
        Tools.CommitActive();
        if (ActiveHistory is { } history && history.UndoNameKey is { } name && history.Undo())
        {
            StatusMessage = Loc.Format("Status.Undone", Loc.Get(name));
        }

        _registry.NotifyStateChanged();
    }

    public void Redo()
    {
        Tools.CommitActive();
        if (ActiveHistory is { } history && history.RedoNameKey is { } name && history.Redo())
        {
            StatusMessage = Loc.Format("Status.Redone", Loc.Get(name));
        }

        _registry.NotifyStateChanged();
    }

    /// <summary>Kopiert die ausgewählten Pixel der aktiven Ebene (ohne Auswahl: die ganze Ebene).</summary>
    public bool Copy()
    {
        Tools.CommitActive();
        if (Doc is not { ActivePixelLayer: { } layer } document || SelectionContent.Copy(document, layer) is not { } copy)
        {
            return false;
        }

        var status = _clipboard.SetImage(copy.Pixels);
        if (status != ClipboardStatus.Ok)
        {
            _dialogs.ShowWarning(Loc.Get("Msg.Clipboard.Title"), Loc.Get("Msg.Clipboard.Failed"));
            return false;
        }

        _lastCopy = copy;
        StatusMessage = Loc.Format("Status.Copied", copy.Origin.Width, copy.Origin.Height);
        return true;
    }

    private void Cut()
    {
        if (Copy())
        {
            WithHistory((d, h) => SelectionContent.Delete(Tools.ContextFor(ActiveDocument!), "History.Cut"));
        }
    }

    /// <summary>
    /// Fügt das Bild der Zwischenablage als neue Ebene ein. Stammt es aus der letzten eigenen Kopie, landet es an
    /// der ursprünglichen Stelle, sonst oben links. Die eingefügten Pixel werden ausgewählt und das Verschieben-
    /// Werkzeug aktiviert. Ein Rückgängig-Schritt.
    /// </summary>
    public void Paste()
    {
        if (Doc is not { } document)
        {
            return;
        }

        Tools.CommitActive();
        var (pixels, status) = _clipboard.GetImage();
        if (pixels is null)
        {
            _dialogs.ShowInformation(Loc.Get("Msg.Clipboard.Title"), Loc.Get(status switch
            {
                ClipboardStatus.Empty => "Msg.Clipboard.Empty",
                ClipboardStatus.TooLarge => "Msg.Clipboard.TooLarge",
                _ => "Msg.Clipboard.Failed",
            }));
            return;
        }

        var origin = _lastCopy is { } last && last.Pixels.ContentEquals(pixels) ? last.Origin : new IntRect(0, 0, pixels.Width, pixels.Height);
        var history = _histories.For(document);
        var tree = LayerTreeEntry.Record("History.Paste", document,
            () => LayerOperations.ImportAsLayer(document, pixels, Loc.Get("Layer.Pasted"), origin.X, origin.Y));
        var before = document.Selection;
        var placed = new IntRect(origin.X, origin.Y, pixels.Width, pixels.Height).Intersect(document.Bounds);
        var selection = SelectionMask.CreateEmpty(document.Width, document.Height);
        selection.Fill(placed, 255);
        document.SetSelection(selection);
        history.Push(new CompositeEntry("History.Paste", [tree, new SelectionEntry("History.Paste", before, document.Selection)]));
        Tools.ActiveTool = Tools.FindTool("move")!;
        _registry.NotifyStateChanged();
    }

    private void GrowOrShrink(bool grow)
    {
        var values = _dialogs.ShowNumbers(grow ? "Dlg.Grow.Title" : "Dlg.Shrink.Title",
            [new NumberField("Dlg.Radius", 2, 1, 500)]);
        if (values is { } v)
        {
            WithHistory((d, h) =>
            {
                if (grow)
                {
                    SelectionEditing.Grow(d, h, v[0]);
                }
                else
                {
                    SelectionEditing.Shrink(d, h, v[0]);
                }
            });
        }
    }

    private void ResizeImage()
    {
        if (Doc is not { } d)
        {
            return;
        }

        var values = _dialogs.ShowNumbers("Dlg.ImageSize.Title",
        [
            new NumberField("Dlg.Size.Width", d.Width, 1, DocumentLimits.MaxDimension),
            new NumberField("Dlg.Size.Height", d.Height, 1, DocumentLimits.MaxDimension),
        ], linkFirstTwo: true);
        if (values is not { } v || (v[0] == d.Width && v[1] == d.Height))
        {
            return;
        }

        if (DocumentLimits.ValidateSize((int)v[0], (int)v[1]) is var check && check != SizeValidationResult.Valid)
        {
            _dialogs.ShowWarning(Loc.Get("Dlg.ImageSize.Title"), NewDocumentValidator.SizeMessage(check)!);
            return;
        }

        WithHistory((doc, h) => DocumentTransforms.ResizeImage(doc, h, (int)v[0], (int)v[1]));
    }

    private void ResizeCanvas()
    {
        if (Doc is not { } d || _dialogs.ShowCanvasSize(d.Width, d.Height) is not { } result)
        {
            return;
        }

        if (DocumentLimits.ValidateSize(result.Width, result.Height) is var check && check != SizeValidationResult.Valid)
        {
            _dialogs.ShowWarning(Loc.Get("Dlg.CanvasSize.Title"), NewDocumentValidator.SizeMessage(check)!);
            return;
        }

        if (result.Width != d.Width || result.Height != d.Height)
        {
            WithHistory((doc, h) => DocumentTransforms.ResizeCanvas(doc, h, result.Width, result.Height, result.Anchor));
        }
    }

    /// <summary>Vorder- oder Hintergrundfarbe über den Farbdialog wählen.</summary>
    public void PickColor(bool primary)
    {
        var current = primary ? Tools.Colors.Primary : Tools.Colors.Secondary;
        if (_dialogs.ShowColorPicker(current) is { } chosen)
        {
            if (primary)
            {
                Tools.Colors.Primary = chosen;
            }
            else
            {
                Tools.Colors.Secondary = chosen;
            }
        }
    }

    private void OnToolProblem(object? sender, ToolProblem problem) =>
        StatusMessage = Loc.Get("ToolProblem." + problem);
}
