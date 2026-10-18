using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Application.Documents;
using OrbitStudio.Application.Settings;
using OrbitStudio.Configuration;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Presentation.Commands;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>ViewModel des Hauptfensters: Menüs, Dokument-Tabs, Statusleiste.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DocumentWorkspace _workspace;
    private readonly CommandRegistry _registry;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly SettingsService _settings;
    private readonly ILogger<MainViewModel> _logger;

    public MainViewModel(DocumentWorkspace workspace, CommandRegistry registry, IDialogService dialogs,
        IShellService shell, SettingsService settings, ILogger<MainViewModel> logger)
    {
        _workspace = workspace;
        _registry = registry;
        _dialogs = dialogs;
        _shell = shell;
        _settings = settings;
        _logger = logger;

        _workspace.DocumentAdded += (_, doc) => Documents.Add(new DocumentViewModel(doc));
        _workspace.DocumentClosed += (_, doc) =>
        {
            var vm = Documents.FirstOrDefault(d => d.Document == doc);
            if (vm is not null)
            {
                Documents.Remove(vm);
            }
        };
        _workspace.ActiveDocumentChanged += (_, _) =>
        {
            ActiveDocument = Documents.FirstOrDefault(d => d.Document == _workspace.ActiveDocument);
            _registry.NotifyStateChanged();
        };

        RegisterCommands();
        Menus = BuildMenus();
    }

    public string WindowTitle => ActiveDocument is null
        ? AppInfo.ProductName
        : $"{ActiveDocument.Title} – {AppInfo.ProductName}";

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public IReadOnlyList<MenuItemViewModel> Menus { get; }

    public CommandRegistry Registry => _registry;

    public bool HasDocuments => Documents.Count > 0;

    public string ToolStatusText => Loc.Get("Status.Tool");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(HasDocuments))]
    private DocumentViewModel? _activeDocument;

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnActiveDocumentPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnActiveDocumentPropertyChanged;
        }

        // Tabwechsel durch die Oberfläche an den Arbeitsbereich weitergeben.
        if (!ReferenceEquals(_workspace.ActiveDocument, newValue?.Document))
        {
            _workspace.Activate(newValue?.Document);
        }
    }

    private void OnActiveDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.Title))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    /// <summary>Legt ein neues Dokument an. Wird vom Befehl „Neu" nach dem Dialog aufgerufen.</summary>
    public DocumentViewModel CreateDocument(NewDocumentRequest request)
    {
        var document = ImageDocument.Create(request.Name, request.Width, request.Height, background: request.Background);
        _workspace.Add(document);
        _settings.UpdateGlobal(_settings.Global with
        {
            NewDocumentWidth = request.Width,
            NewDocumentHeight = request.Height,
        });
        return Documents.Single(d => d.Document == document);
    }

    /// <summary>Schließt ein Dokument nach Rückfrage bei ungespeicherten Änderungen. False = abgebrochen.</summary>
    public bool CloseDocument(DocumentViewModel document)
    {
        if (document.IsDirty && !_dialogs.ConfirmDiscardChanges(document.Document.Name))
        {
            return false;
        }

        _workspace.Close(document.Document);
        return true;
    }

    /// <summary>Schließen-Schaltfläche im Tab.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void CloseTab(DocumentViewModel? document)
    {
        if (document is not null)
        {
            CloseDocument(document);
        }
    }

    /// <summary>Schließt alle Dokumente. Bricht ab, sobald der Nutzer eine Rückfrage abbricht.</summary>
    public bool CloseAllDocuments()
    {
        foreach (var document in Documents.ToList())
        {
            if (!CloseDocument(document))
            {
                return false;
            }
        }

        return true;
    }

    private void NewDocument()
    {
        var defaultName = Loc.Format("Dlg.New.Untitled", _workspace.NextUntitledNumber());
        var request = _dialogs.ShowNewDocument(defaultName, _settings.Global.NewDocumentWidth,
            _settings.Global.NewDocumentHeight);
        if (request is not null)
        {
            CreateDocument(request);
        }
    }

    private void ShowDocumentInfo()
    {
        if (ActiveDocument is not { } vm)
        {
            return;
        }

        var doc = vm.Document;
        var background = Loc.Get(doc.Background == DocumentBackground.White
            ? "Dlg.New.Background.White"
            : "Dlg.New.Background.Transparent");
        _dialogs.ShowInformation(Loc.Get("Msg.DocInfo.Title"), Loc.Format("Msg.DocInfo.Body",
            doc.Name, doc.Width, doc.Height, doc.ResolutionDpi, background,
            doc.FilePath ?? Loc.Get("Msg.DocInfo.NotSaved"),
            Loc.Get(doc.IsDirty ? "Msg.Yes" : "Msg.No")));
    }

    private void ShowAbout() =>
        _dialogs.ShowInformation(Loc.Format("Msg.About.Title", AppInfo.ProductName),
            Loc.Format("Msg.About.Body", AppInfo.ProductName, AppInfo.Version));

    private void RegisterCommands()
    {
        bool HasDoc() => _workspace.ActiveDocument is not null;

        void Add(AppCommand c) => _registry.Register(c);
        void Plan(string id, string key, string milestone, string? gesture = null) =>
            Add(AppCommand.Planned(id, key, milestone, gesture));

        // Datei
        Add(AppCommand.Create(FileNew, "Cmd.File.New", NewDocument, gesture: "Ctrl+N"));
        Plan(FileOpen, "Cmd.File.Open", "M2", "Ctrl+O");
        Plan(FileRecent, "Cmd.File.Recent", "M2");
        Plan(FileSave, "Cmd.File.Save", "M7", "Ctrl+S");
        Plan(FileSaveAs, "Cmd.File.SaveAs", "M7", "Ctrl+Shift+S");
        Plan(FileSaveCopy, "Cmd.File.SaveCopy", "M7");
        Plan(FileImport, "Cmd.File.Import", "M2");
        Plan(FileExport, "Cmd.File.Export", "M2", "Ctrl+Shift+E");
        Add(AppCommand.Create(FileDocumentInfo, "Cmd.File.DocumentInfo", ShowDocumentInfo, HasDoc));
        Add(AppCommand.Create(FileClose, "Cmd.File.Close",
            () => { if (ActiveDocument is { } d) { CloseDocument(d); } }, HasDoc, "Ctrl+W"));
        Add(AppCommand.Create(FileCloseAll, "Cmd.File.CloseAll", () => CloseAllDocuments(), HasDoc, "Ctrl+Shift+W"));
        Add(AppCommand.Create(FileExit, "Cmd.File.Exit", _shell.RequestExit));

        // Bearbeiten
        Plan(EditUndo, "Cmd.Edit.Undo", "M4", "Ctrl+Z");
        Plan(EditRedo, "Cmd.Edit.Redo", "M4", "Ctrl+Y");
        Plan(EditCut, "Cmd.Edit.Cut", "M4", "Ctrl+X");
        Plan(EditCopy, "Cmd.Edit.Copy", "M4", "Ctrl+C");
        Plan(EditPaste, "Cmd.Edit.Paste", "M4", "Ctrl+V");
        Plan(EditDelete, "Cmd.Edit.Delete", "M4", "Delete");
        Plan(EditSelectAll, "Cmd.Edit.SelectAll", "M4", "Ctrl+A");
        Plan(EditDeselect, "Cmd.Edit.Deselect", "M4", "Ctrl+D");
        Plan(EditInvertSelection, "Cmd.Edit.InvertSelection", "M4", "Ctrl+I");
        Plan(EditPreferences, "Cmd.Edit.Preferences", "M9");

        // Ansicht
        Plan(ViewZoomIn, "Cmd.View.ZoomIn", "M2", "Ctrl+OemPlus");
        Plan(ViewZoomOut, "Cmd.View.ZoomOut", "M2", "Ctrl+OemMinus");
        Plan(ViewActualSize, "Cmd.View.ActualSize", "M2", "Ctrl+D0");
        Plan(ViewFitToWindow, "Cmd.View.FitToWindow", "M2", "Ctrl+B");
        Plan(ViewRulers, "Cmd.View.Rulers", "M8");
        Plan(ViewGrid, "Cmd.View.Grid", "M8");
        Plan(ViewGuides, "Cmd.View.Guides", "M8");
        Plan(ViewPixelGrid, "Cmd.View.PixelGrid", "M2");
        Add(AppCommand.Create(ViewFullScreen, "Cmd.View.FullScreen", _shell.ToggleFullScreen, gesture: "F11"));
        Plan(ViewPanels, "Cmd.View.Panels", "M8");
        Plan(ViewWorkspaces, "Cmd.View.Workspaces", "M9");

        // Bild
        Plan(ImageResize, "Cmd.Image.Resize", "M4", "Ctrl+R");
        Plan(ImageCanvasSize, "Cmd.Image.CanvasSize", "M4", "Ctrl+Shift+R");
        Plan(ImageRotate, "Cmd.Image.Rotate", "M4");
        Plan(ImageFlip, "Cmd.Image.Flip", "M4");
        Plan(ImageCrop, "Cmd.Image.Crop", "M4");
        Add(AppCommand.Create(ImageInfo, "Cmd.Image.Info", ShowDocumentInfo, HasDoc));

        // Ebenen
        Plan(LayerNew, "Cmd.Layer.New", "M3", "Ctrl+Shift+N");
        Plan(LayerDuplicate, "Cmd.Layer.Duplicate", "M3", "Ctrl+J");
        Plan(LayerDelete, "Cmd.Layer.Delete", "M3");
        Plan(LayerRename, "Cmd.Layer.Rename", "M3");
        Plan(LayerMoveUp, "Cmd.Layer.MoveUp", "M3");
        Plan(LayerMoveDown, "Cmd.Layer.MoveDown", "M3");
        Plan(LayerGroupCreate, "Cmd.Layer.GroupCreate", "M3", "Ctrl+G");
        Plan(LayerGroupDissolve, "Cmd.Layer.GroupDissolve", "M3", "Ctrl+Shift+G");
        Plan(LayerVisibility, "Cmd.Layer.Visibility", "M3");
        Plan(LayerOpacity, "Cmd.Layer.Opacity", "M3");
        Plan(LayerBlendMode, "Cmd.Layer.BlendMode", "M3");
        Plan(LayerAddMask, "Cmd.Layer.AddMask", "M3");
        Plan(LayerMerge, "Cmd.Layer.Merge", "M3", "Ctrl+E");

        // Farben
        Plan(AdjustBrightnessContrast, "Cmd.Adjust.BrightnessContrast", "M6");
        Plan(AdjustHueSaturation, "Cmd.Adjust.HueSaturation", "M6", "Ctrl+U");
        Plan(AdjustLevels, "Cmd.Adjust.Levels", "M6", "Ctrl+L");
        Plan(AdjustCurves, "Cmd.Adjust.Curves", "M6", "Ctrl+M");
        Plan(AdjustBlackWhite, "Cmd.Adjust.BlackWhite", "M6", "Ctrl+Shift+B");
        Plan(AdjustInvert, "Cmd.Adjust.Invert", "M6", "Ctrl+Shift+I");
        Plan(AdjustSepia, "Cmd.Adjust.Sepia", "M6");

        // Effekte
        Plan(EffectBlur, "Cmd.Effect.Blur", "M6");
        Plan(EffectSharpen, "Cmd.Effect.Sharpen", "M6");
        Plan(EffectNoise, "Cmd.Effect.Noise", "M6");
        Plan(EffectDenoise, "Cmd.Effect.Denoise", "M6");
        Plan(EffectEdges, "Cmd.Effect.Edges", "M6");
        Plan(EffectShadow, "Cmd.Effect.Shadow", "M6");
        Plan(EffectDistort, "Cmd.Effect.Distort", "M6");
        Plan(EffectExtensions, "Cmd.Effect.Extensions", "M10");

        // Werkzeuge
        Plan(ToolsOptions, "Cmd.Tools.Options", "M4");
        Plan(ToolsShortcuts, "Cmd.Tools.Shortcuts", "M9");

        // Fenster
        Plan(WindowTools, "Cmd.Window.Tools", "M8");
        Plan(WindowLayers, "Cmd.Window.Layers", "M8");
        Plan(WindowColors, "Cmd.Window.Colors", "M8");
        Plan(WindowHistory, "Cmd.Window.History", "M8");
        Plan(WindowNavigator, "Cmd.Window.Navigator", "M8");
        Plan(WindowProperties, "Cmd.Window.Properties", "M8");
        Plan(WindowEffectBrowser, "Cmd.Window.EffectBrowser", "M8");
        Plan(WindowPlugins, "Cmd.Window.Plugins", "M10");
        Plan(WindowDiagnostics, "Cmd.Window.Diagnostics", "M11");

        // Hilfe
        Plan(HelpUserGuide, "Cmd.Help.UserGuide", "M12", "F1");
        Plan(HelpShortcuts, "Cmd.Help.Shortcuts", "M9");
        Plan(HelpDiagnostics, "Cmd.Help.Diagnostics", "M11");
        Plan(HelpLicenses, "Cmd.Help.Licenses", "M12");
        Add(AppCommand.Create(HelpAbout, "Cmd.Help.About", ShowAbout));

        var conflicts = _registry.FindGestureConflicts();
        foreach (var conflict in conflicts)
        {
            _logger.LogError("Tastenkürzel-Konflikt {Gesture}: {Commands}", conflict.Gesture,
                string.Join(", ", conflict.CommandIds));
        }
    }

    private IReadOnlyList<MenuItemViewModel> BuildMenus() =>
        MenuLayout.Default.Select(menu => MenuItemViewModel.Submenu(
            Loc.Get(menu.TextKey),
            menu.Items.Select(id => id == MenuLayout.Separator
                ? MenuItemViewModel.Separator()
                : MenuItemViewModel.ForCommand(_registry.Get(id), _registry,
                    id == HelpAbout ? Loc.Format("Cmd.Help.About", AppInfo.ProductName) : null)).ToList())).ToList();
}
