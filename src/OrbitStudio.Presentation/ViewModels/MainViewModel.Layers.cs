using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Layers;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Ebenenbefehle (Menü „Ebenen", Ebenen-Panel) und Importieren.</summary>
public sealed partial class MainViewModel
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
        bool PixelLayer() => HasNode() && ActiveNode is PixelLayer;
        bool HasMask() => HasNode() && ActiveNode is PixelLayer { Mask: not null };

        void Add(AppCommand c) => _registry.Register(c);

        Add(AppCommand.Create(LayerNew, "Cmd.Layer.New",
            () => Run(d => LayerOperations.AddLayer(d, LayerOperations.NextName(d, Loc.Get("Layer.NewName")))),
            () => !IsBusy && Doc is not null, "Ctrl+Shift+N"));
        Add(AppCommand.Create(LayerDuplicate, "Cmd.Layer.Duplicate",
            () => Run(d => LayerOperations.Duplicate(d, d.ActiveLayer!, Loc.Format("Layer.CopyName", d.ActiveLayer!.Name))),
            HasNode, "Ctrl+J"));
        Add(AppCommand.Create(LayerDelete, "Cmd.Layer.Delete", DeleteActiveLayer,
            () => HasNode() && LayerOperations.CanDelete(Doc!, ActiveNode!)));
        Add(AppCommand.Create(LayerRename, "Cmd.Layer.Rename", () => EditLayerProperties(LayerPropertyFocus.Name), HasNode,
            "F2"));
        Add(AppCommand.Create(LayerMoveUp, "Cmd.Layer.MoveUp", () => Run(d => LayerOperations.MoveUp(d, d.ActiveLayer!)),
            () => HasNode() && LayerOperations.CanMoveUp(ActiveNode!), "Ctrl+Shift+Up"));
        Add(AppCommand.Create(LayerMoveDown, "Cmd.Layer.MoveDown", () => Run(d => LayerOperations.MoveDown(d, d.ActiveLayer!)),
            () => HasNode() && LayerOperations.CanMoveDown(ActiveNode!), "Ctrl+Shift+Down"));
        Add(AppCommand.Create(LayerGroupCreate, "Cmd.Layer.GroupCreate",
            () => Run(d => LayerOperations.CreateGroup(d, d.ActiveLayer!, LayerOperations.NextName(d, Loc.Get("Layer.GroupName")))),
            Unlocked, "Ctrl+G"));
        Add(AppCommand.Create(LayerGroupDissolve, "Cmd.Layer.GroupDissolve", DissolveActiveGroup,
            () => Unlocked() && ActiveNode is LayerGroup, "Ctrl+Shift+G"));
        Add(AppCommand.Toggle(LayerVisibility, "Cmd.Layer.Visibility",
            () => Run(d => LayerOperations.SetVisibility(d, d.ActiveLayer!, !d.ActiveLayer!.IsVisible)),
            () => ActiveNode?.IsVisible == true, HasNode));
        Add(AppCommand.Toggle(LayerLock, "Cmd.Layer.Lock",
            () => Run(d => LayerOperations.SetLocked(d, d.ActiveLayer!, !d.ActiveLayer!.IsLocked)),
            () => ActiveNode?.IsLocked == true, HasNode));
        Add(AppCommand.Create(LayerOpacity, "Cmd.Layer.Opacity", () => EditLayerProperties(LayerPropertyFocus.Opacity), HasNode));
        Add(AppCommand.Create(LayerBlendMode, "Cmd.Layer.BlendMode", () => EditLayerProperties(LayerPropertyFocus.BlendMode),
            HasNode));
        Add(AppCommand.Create(LayerAddMask, "Cmd.Layer.AddMask", () => Run(d => LayerOperations.AddMask(d, d.ActiveLayer!)),
            () => PixelLayer() && ActiveNode is PixelLayer { Mask: null }));
        Add(AppCommand.Toggle(LayerMaskEnabled, "Cmd.Layer.MaskEnabled",
            () => Run(d => LayerOperations.SetMaskEnabled(d, d.ActiveLayer!, !((PixelLayer)d.ActiveLayer!).Mask!.IsEnabled)),
            () => ActiveNode is PixelLayer { Mask.IsEnabled: true }, HasMask));
        Add(AppCommand.Create(LayerMaskInvert, "Cmd.Layer.MaskInvert", () => Run(d => LayerOperations.InvertMask(d, d.ActiveLayer!)),
            HasMask));
        Add(AppCommand.Create(LayerMaskApply, "Cmd.Layer.MaskApply", ApplyActiveMask, () => HasMask() && Unlocked()));
        Add(AppCommand.Create(LayerMaskDelete, "Cmd.Layer.MaskDelete", () => Run(d => LayerOperations.RemoveMask(d, d.ActiveLayer!)),
            HasMask));
        Add(AppCommand.Planned(LayerSelectionFromAlpha, "Cmd.Layer.SelectionFromAlpha", "M4"));
        Add(AppCommand.Planned(LayerTransform, "Cmd.Layer.Transform", "M4"));
        Add(AppCommand.Create(LayerMerge, "Cmd.Layer.Merge", MergeActiveDown,
            () => HasNode() && LayerOperations.CanMergeDown(Doc!, ActiveNode!), "Ctrl+E"));
        Add(AppCommand.Create(LayerFlatten, "Cmd.Layer.Flatten", FlattenActive,
            () => !IsBusy && Doc is { LayerCount: > 1 }, "Ctrl+Shift+F"));
    }

    /// <summary>Führt eine Ebenenoperation aus und zeigt Ablehnungen als deutsche Meldung an.</summary>
    private void Run(Action<ImageDocument> operation)
    {
        if (Doc is not { } document)
        {
            return;
        }

        try
        {
            operation(document);
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

    private void DeleteActiveLayer()
    {
        if (ActiveNode is not { } node)
        {
            return;
        }

        var message = node is LayerGroup
            ? Loc.Format("Msg.Layer.Confirm.DeleteGroup", node.Name)
            : Loc.Format("Msg.Layer.Confirm.Delete", node.Name);
        if (_dialogs.Confirm(Loc.Get("Msg.Layer.Confirm.Title"), message))
        {
            Run(d => LayerOperations.Delete(d, node));
        }
    }

    private void DissolveActiveGroup()
    {
        if (ActiveNode is not LayerGroup group)
        {
            return;
        }

        // Nur nachfragen, wenn sich das Aussehen ändern kann.
        var changesLook = group.Opacity < 1 || group.BlendMode != BlendMode.Normal || !group.IsVisible;
        if (!changesLook || _dialogs.Confirm(Loc.Get("Msg.Layer.Confirm.Title"),
                Loc.Format("Msg.Layer.Confirm.Dissolve", group.Name)))
        {
            Run(d => LayerOperations.DissolveGroup(d, group));
        }
    }

    private void ApplyActiveMask()
    {
        if (ActiveNode is PixelLayer layer && _dialogs.Confirm(Loc.Get("Msg.Layer.Confirm.Title"),
                Loc.Format("Msg.Layer.Confirm.ApplyMask", layer.Name)))
        {
            Run(d => LayerOperations.ApplyMask(d, layer));
        }
    }

    private void MergeActiveDown()
    {
        if (ActiveNode is not { Parent: { } parent } node || parent.IndexOf(node) < 1)
        {
            return;
        }

        var below = parent.Children[parent.IndexOf(node) - 1];
        if (_dialogs.Confirm(Loc.Get("Msg.Layer.Confirm.Title"), Loc.Format("Msg.Layer.Confirm.Merge", node.Name, below.Name)))
        {
            Run(d => LayerOperations.MergeDown(d, node));
        }
    }

    private void FlattenActive()
    {
        if (_dialogs.Confirm(Loc.Get("Msg.Layer.Confirm.Title"), Loc.Get("Msg.Layer.Confirm.Flatten")))
        {
            Run(d => LayerOperations.Flatten(d, Loc.Get("Layer.Background")));
        }
    }

    /// <summary>Dialog „Ebeneneigenschaften" für die aktive Ebene; übernimmt nur geänderte Werte.</summary>
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

        Run(d =>
        {
            LayerOperations.Rename(d, node, result.Name);
            LayerOperations.SetVisibility(d, node, result.IsVisible);
            LayerOperations.SetOpacity(d, node, result.OpacityPercent / 100.0);
            LayerOperations.SetBlendMode(d, node, result.BlendMode);
            LayerOperations.SetLocked(d, node, result.IsLocked);
        });
    }

    /// <summary>Importiert Bilddateien als neue Ebenen in das aktive Dokument.</summary>
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
                var (_, cropped) = LayerOperations.ImportAsLayer(document, loaded.Pixels!, name);
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
        var paths = _dialogs.ShowOpenFiles(Loc.Get("Cmd.File.Import").Replace("_", string.Empty, StringComparison.Ordinal)
            .TrimEnd(' ', '…'), ImageOpenFilters());
        if (paths is { Count: > 0 })
        {
            await ImportFilesAsLayersAsync(paths);
        }
    }
}
