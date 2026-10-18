using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Layers;
using OrbitStudio.History;
using OrbitStudio.Layers;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Tools.Selection;
using OrbitStudio.Tools.Transform;
using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Ebenenbefehle (Menü „Ebenen", Ebenen-Panel) und Importieren – alle mit Rückgängig.</summary>
public sealed partial class MainViewModel : ILayerEditExecutor
{
    private ImageDocument? Doc => _workspace.ActiveDocument;

    private LayerNode? ActiveNode => Doc?.ActiveLayer;

    // Befehle für die Schaltflächen im Ebenen-Panel (gleiche Registry-Befehle wie im Menü).
    public ICommand NewLayerCommand => Adapter(LayerNew);

    public ICommand DuplicateLayerCommand => Adapter(LayerDuplicate);

    public ICommand DeleteLayerCommand => Adapter(LayerDelete);

    public ICommand MoveLayerUpCommand => Adapter(LayerMoveUp);

    public ICommand MoveLayerDownCommand => Adapter(LayerMoveDown);

    public ICommand LayerPropertiesCommand => Adapter(LayerRename);

    private readonly Dictionary<string, CommandAdapter> _adapters = [];

    private CommandAdapter Adapter(string id) =>
        _adapters.TryGetValue(id, out var adapter)
            ? adapter
            : _adapters[id] = new CommandAdapter(_registry.Get(id), _registry);

    private void RegisterLayerCommands()
    {
        bool HasNode() => !IsBusy && ActiveNode is not null;
        bool Unlocked() => HasNode() && !ActiveNode!.IsEffectivelyLocked;
        bool IsPixelLayer() => HasNode() && ActiveNode is PixelLayer;
        bool UnlockedPixelLayer() => IsPixelLayer() && !ActiveNode!.IsEffectivelyLocked;
        bool HasMask() => HasNode() && ActiveNode is PixelLayer { Mask: not null };

        void Add(AppCommand c) => _registry.Register(c);

        Add(AppCommand.Create(LayerNew, "Cmd.Layer.New",
            () => Run("History.LayerNew", d => LayerOperations.AddLayer(d, LayerOperations.NextName(d, Loc.Get("Layer.NewName")))),
            () => !IsBusy && Doc is not null, "Ctrl+Shift+N"));
        Add(AppCommand.Create(LayerDuplicate, "Cmd.Layer.Duplicate",
            () => Run("History.LayerDuplicate",
                d => LayerOperations.Duplicate(d, d.ActiveLayer!, Loc.Format("Layer.CopyName", d.ActiveLayer!.Name))),
            HasNode, "Ctrl+J"));
        Add(AppCommand.Create(LayerDelete, "Cmd.Layer.Delete",
            () => Run("History.LayerDelete", d => LayerOperations.Delete(d, d.ActiveLayer!)),
            () => HasNode() && LayerOperations.CanDelete(Doc!, ActiveNode!)));
        Add(AppCommand.Create(LayerRename, "Cmd.Layer.Rename", () => EditLayerProperties(LayerPropertyFocus.Name), HasNode,
            "F2"));
        Add(AppCommand.Create(LayerMoveUp, "Cmd.Layer.MoveUp",
            () => Run("History.LayerMoveUp", d => LayerOperations.MoveUp(d, d.ActiveLayer!)),
            () => HasNode() && LayerOperations.CanMoveUp(ActiveNode!), "Ctrl+Shift+Up"));
        Add(AppCommand.Create(LayerMoveDown, "Cmd.Layer.MoveDown",
            () => Run("History.LayerMoveDown", d => LayerOperations.MoveDown(d, d.ActiveLayer!)),
            () => HasNode() && LayerOperations.CanMoveDown(ActiveNode!), "Ctrl+Shift+Down"));
        Add(AppCommand.Create(LayerGroupCreate, "Cmd.Layer.GroupCreate",
            () => Run("History.LayerGroup",
                d => LayerOperations.CreateGroup(d, d.ActiveLayer!, LayerOperations.NextName(d, Loc.Get("Layer.GroupName")))),
            Unlocked, "Ctrl+G"));
        Add(AppCommand.Create(LayerGroupDissolve, "Cmd.Layer.GroupDissolve",
            () => Run("History.LayerUngroup", d => LayerOperations.DissolveGroup(d, d.ActiveLayer!)),
            () => Unlocked() && ActiveNode is LayerGroup, "Ctrl+Shift+G"));
        Add(AppCommand.Toggle(LayerVisibility, "Cmd.Layer.Visibility",
            () => Run("History.LayerVisibility", d => LayerOperations.SetVisibility(d, d.ActiveLayer!, !d.ActiveLayer!.IsVisible)),
            () => ActiveNode?.IsVisible == true, HasNode));
        Add(AppCommand.Toggle(LayerLock, "Cmd.Layer.Lock",
            () => Run("History.LayerLock", d => LayerOperations.SetLocked(d, d.ActiveLayer!, !d.ActiveLayer!.IsLocked)),
            () => ActiveNode?.IsLocked == true, HasNode));
        Add(AppCommand.Create(LayerOpacity, "Cmd.Layer.Opacity", () => EditLayerProperties(LayerPropertyFocus.Opacity), HasNode));
        Add(AppCommand.Create(LayerBlendMode, "Cmd.Layer.BlendMode", () => EditLayerProperties(LayerPropertyFocus.BlendMode),
            HasNode));
        Add(AppCommand.Create(LayerAddMask, "Cmd.Layer.AddMask",
            () => Run("History.MaskAdd", d => LayerOperations.AddMask(d, d.ActiveLayer!)),
            () => IsPixelLayer() && ActiveNode is PixelLayer { Mask: null }));
        Add(AppCommand.Toggle(LayerEditMask, "Cmd.Layer.EditMask", () => Tools.EditMask = !Tools.EditMask,
            () => Tools.EditMask, () => !IsBusy));
        Add(AppCommand.Toggle(LayerMaskEnabled, "Cmd.Layer.MaskEnabled",
            () => Run("History.MaskEnabled",
                d => LayerOperations.SetMaskEnabled(d, d.ActiveLayer!, !((PixelLayer)d.ActiveLayer!).Mask!.IsEnabled)),
            () => ActiveNode is PixelLayer { Mask.IsEnabled: true }, HasMask));
        Add(AppCommand.Create(LayerMaskInvert, "Cmd.Layer.MaskInvert",
            () => Run("History.MaskInvert", d => LayerOperations.InvertMask(d, d.ActiveLayer!),
                masks: d => [((PixelLayer)d.ActiveLayer!).Mask!]),
            HasMask));
        Add(AppCommand.Create(LayerMaskApply, "Cmd.Layer.MaskApply",
            () => Run("History.MaskApply", d => LayerOperations.ApplyMask(d, d.ActiveLayer!),
                pixels: d => [(PixelLayer)d.ActiveLayer!]),
            () => HasMask() && Unlocked()));
        Add(AppCommand.Create(LayerMaskDelete, "Cmd.Layer.MaskDelete",
            () => Run("History.MaskDelete", d => LayerOperations.RemoveMask(d, d.ActiveLayer!)),
            HasMask));
        Add(AppCommand.Create(LayerSelectionFromAlpha, "Cmd.Layer.SelectionFromAlpha",
            () => WithHistory((d, h) => SelectionEditing.FromLayerAlpha(d, h)), IsPixelLayer));
        Add(AppCommand.Create(LayerFlipH, "Cmd.Layer.FlipH",
            () => WithHistory((d, h) => DocumentTransforms.FlipLayer(d, h, (PixelLayer)d.ActiveLayer!, horizontal: true)),
            UnlockedPixelLayer));
        Add(AppCommand.Create(LayerFlipV, "Cmd.Layer.FlipV",
            () => WithHistory((d, h) => DocumentTransforms.FlipLayer(d, h, (PixelLayer)d.ActiveLayer!, horizontal: false)),
            UnlockedPixelLayer));
        Add(AppCommand.Create(LayerTransform, "Cmd.Layer.Transform", TransformActiveLayer, UnlockedPixelLayer));
        Add(AppCommand.Create(LayerMerge, "Cmd.Layer.Merge",
            () => Run("History.LayerMerge", d => LayerOperations.MergeDown(d, d.ActiveLayer!), pixels: LayerBelowActive),
            () => HasNode() && LayerOperations.CanMergeDown(Doc!, ActiveNode!), "Ctrl+E"));
        Add(AppCommand.Create(LayerFlatten, "Cmd.Layer.Flatten",
            () => Run("History.LayerFlatten", d => LayerOperations.Flatten(d, Loc.Get("Layer.Background"))),
            () => !IsBusy && Doc is { LayerCount: > 1 }, "Ctrl+Shift+F"));
    }

    private static IEnumerable<PixelLayer> LayerBelowActive(ImageDocument d) =>
        d.ActiveLayer is { Parent: { } parent } node && parent.IndexOf(node) > 0
        && parent.Children[parent.IndexOf(node) - 1] is PixelLayer below
            ? [below]
            : [];

    /// <summary>
    /// Führt eine Ebenenoperation mit Historie aus und zeigt Ablehnungen als deutsche Meldung an.
    /// </summary>
    /// <param name="pixels">Ebenen, deren Pixel die Operation ändert (werden für Rückgängig gesichert).</param>
    /// <param name="masks">Masken, deren Inhalt die Operation ändert.</param>
    private void Run(string nameKey, Action<ImageDocument> operation,
        Func<ImageDocument, IEnumerable<PixelLayer>>? pixels = null,
        Func<ImageDocument, IEnumerable<LayerMask>>? masks = null)
    {
        if (Doc is not { } document)
        {
            return;
        }

        Execute(document, nameKey, () => operation(document), null, pixels?.Invoke(document), masks?.Invoke(document));
    }

    /// <inheritdoc />
    public void Execute(ImageDocument document, string nameKey, Action operation, string? coalesceKey = null,
        IEnumerable<PixelLayer>? changedPixels = null, IEnumerable<LayerMask>? changedMasks = null)
    {
        Tools.CommitActive();
        try
        {
            var entry = LayerTreeEntry.Record(nameKey, document, operation, changedPixels, changedMasks, coalesceKey);
            var history = _histories.For(document);
            if (coalesceKey is null)
            {
                history.Push(entry);
            }
            else
            {
                // Viele kleine Schritte eines Reglerzugs (innerhalb von 2 s) ergeben einen Rückgängig-Schritt.
                history.PushOrMerge(entry, (last, next) =>
                    last is LayerTreeEntry previous && next is LayerTreeEntry current
                    && previous.CoalesceKey == coalesceKey
                    && current.CreatedUtc - previous.CreatedUtc < TimeSpan.FromSeconds(2)
                        ? LayerTreeEntry.Merge(previous, current)
                        : null);
            }
        }
        catch (LayerOperationException ex)
        {
            _dialogs.ShowWarning(Loc.Get("Msg.Layer.Error.Title"), Loc.Get("Msg.Layer.Error." + ex.Error));
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Nicht genug Speicher für Ebenenoperation.");
            _dialogs.ShowError(Loc.Get("Msg.Error.Title"), Loc.Format("Msg.File.OutOfMemory", document.Name));
        }
        finally
        {
            _registry.NotifyStateChanged();
        }
    }

    /// <summary>Werkzeug- und Bildbefehle mit Historie des aktiven Dokuments ausführen.</summary>
    private void WithHistory(Action<ImageDocument, DocumentHistory> action)
    {
        if (Doc is not { } document)
        {
            return;
        }

        Tools.CommitActive();
        try
        {
            action(document, _histories.For(document));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Befehl abgelehnt.");
            _dialogs.ShowWarning(Loc.Get("Msg.Layer.Error.Title"), Loc.Get("Msg.Layer.Error.Locked"));
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Nicht genug Speicher.");
            _dialogs.ShowError(Loc.Get("Msg.Error.Title"), Loc.Format("Msg.File.OutOfMemory", document.Name));
        }
        finally
        {
            _registry.NotifyStateChanged();
        }
    }

    private void TransformActiveLayer()
    {
        if (ActiveNode is not PixelLayer layer)
        {
            return;
        }

        var values = _dialogs.ShowNumbers("Dlg.Transform.Title",
        [
            new NumberField("Dlg.Transform.Angle", 0, -360, 360, 2),
            new NumberField("Dlg.Transform.Scale", 100, 1, 1000, 2),
        ]);
        if (values is { } v && (v[0] != 0 || v[1] != 100))
        {
            WithHistory((d, h) => DocumentTransforms.RotateScaleLayer(d, h, layer, v[0], v[1] / 100));
        }
    }

    /// <summary>Dialog „Ebeneneigenschaften" für die aktive Ebene; übernimmt nur geänderte Werte (ein Schritt).</summary>
    private void EditLayerProperties(LayerPropertyFocus focus)
    {
        if (ActiveNode is not { } node)
        {
            return;
        }

        var initial = new LayerProperties(node.Name, node.IsVisible, node.IsLocked, Math.Round(node.Opacity * 100, 1),
            node.BlendMode);
        var result = _dialogs.ShowLayerProperties(initial, focus);
        if (result is null || result == initial)
        {
            return;
        }

        Run("History.LayerProperties", d =>
        {
            LayerOperations.Rename(d, node, result.Name);
            LayerOperations.SetVisibility(d, node, result.IsVisible);
            LayerOperations.SetOpacity(d, node, result.OpacityPercent / 100.0);
            LayerOperations.SetBlendMode(d, node, result.BlendMode);
            LayerOperations.SetLocked(d, node, result.IsLocked);
        });
    }

    /// <summary>Importiert Bilddateien als neue Ebenen in das aktive Dokument (je Datei ein Rückgängig-Schritt).</summary>
    public async Task<int> ImportFilesAsLayersAsync(IEnumerable<string> paths, bool showMessages = true)
    {
        if (Doc is not { } document)
        {
            return 0;
        }

        var imported = 0;
        IsBusy = true;
        try
        {
            foreach (var path in paths)
            {
                var loaded = await _files.LoadPixelsAsync(path);
                if (!loaded.Success)
                {
                    if (showMessages)
                    {
                        _dialogs.ShowError(Loc.Get("Msg.File.OpenFailed.Title"), FileErrorMessage(loaded.Error, path));
                    }

                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(path);
                var cropped = false;
                Execute(document, "History.Import",
                    () => cropped = LayerOperations.ImportAsLayer(document, loaded.Pixels!, name).Cropped);
                imported++;
                if (cropped && showMessages)
                {
                    _dialogs.ShowInformation(Loc.Get("Msg.Layer.Imported.Title"),
                        Loc.Format("Msg.Layer.Imported.Cropped", Path.GetFileName(path)));
                }
            }
        }
        finally
        {
            IsBusy = false;
            StatusMessage = Loc.Get("Status.Ready");
        }

        return imported;
    }

    private async Task ImportWithDialogAsync()
    {
        var paths = _dialogs.ShowOpenFiles(Loc.WithoutAccessKey(Loc.Get("Cmd.File.Import")).TrimEnd(' ', '…'),
            ImageOpenFilters());
        if (paths is { Count: > 0 })
        {
            await ImportFilesAsLayersAsync(paths);
        }
    }
}
