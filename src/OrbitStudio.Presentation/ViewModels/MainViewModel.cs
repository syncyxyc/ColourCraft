using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Application.Documents;
using OrbitStudio.Application.Files;
using OrbitStudio.Application.Settings;
using OrbitStudio.Configuration;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Presentation.Commands;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.Tooling;
using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>ViewModel des Hauptfensters: Menüs, Dokument-Tabs, Statusleiste. Dateioperationen: MainViewModel.Files.cs.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly DocumentWorkspace _workspace;
    private readonly CommandRegistry _registry;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly SettingsService _settings;
    private readonly ImageFileService _files;
    private readonly ILogger<MainViewModel> _logger;
    private readonly HistoryService _histories;
    private readonly IClipboardService _clipboard;
    private readonly ObservableCollection<MenuItemViewModel> _recentItems = [];

    public MainViewModel(DocumentWorkspace workspace, CommandRegistry registry, IDialogService dialogs,
        IShellService shell, SettingsService settings, ImageFileService files, ToolController tools,
        HistoryService histories, IClipboardService clipboard, ILogger<MainViewModel> logger)
    {
        _workspace = workspace;
        _registry = registry;
        _dialogs = dialogs;
        _shell = shell;
        _settings = settings;
        _files = files;
        Tools = tools;
        _histories = histories;
        _clipboard = clipboard;
        _logger = logger;

        Tools.ProblemReported += OnToolProblem;
        Tools.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ToolController.ActiveTool) or nameof(ToolController.EditMask))
            {
                OnPropertyChanged(nameof(ToolStatusText));
                _registry.NotifyStateChanged();
            }
        };

        _workspace.DocumentAdded += (_, doc) =>
        {
            Documents.Add(new DocumentViewModel(doc) { LayerEdits = this });
            _histories.For(doc).Changed += (_, _) => _registry.NotifyStateChanged();
            doc.SelectionChanged += (_, _) => _registry.NotifyStateChanged();
            // Ebenenbefehle hängen von der aktiven Ebene ab.
            doc.ActiveLayerChanged += (_, _) => _registry.NotifyStateChanged();
            doc.LayersChanged += (_, _) => _registry.NotifyStateChanged();
        };
        _workspace.DocumentClosed += (_, doc) =>
        {
            var vm = Documents.FirstOrDefault(d => d.Document == doc);
            if (vm is not null)
            {
                Tools.ForgetDocument(vm);
                Documents.Remove(vm);
            }
        };
        _workspace.ActiveDocumentChanged += (_, _) =>
        {
            Tools.CommitActive();
            ActiveDocument = Documents.FirstOrDefault(d => d.Document == _workspace.ActiveDocument);
            _registry.NotifyStateChanged();
        };
        _settings.RecentFilesChanged += (_, _) => RebuildRecentItems();
        _settings.WorkspaceChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowPixelGrid));
            OnPropertyChanged(nameof(ShowToolOptions));
            _registry.NotifyStateChanged();
        };

        RegisterCommands();
        Menus = BuildMenus();
        RebuildRecentItems();
    }

    public string WindowTitle => ActiveDocument is null
        ? AppInfo.ProductName
        : $"{ActiveDocument.Title} – {AppInfo.ProductName}";

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    public IReadOnlyList<MenuItemViewModel> Menus { get; }

    public CommandRegistry Registry => _registry;

    /// <summary>Werkzeuge, Farben und Masken-Bearbeitungsmodus.</summary>
    public ToolController Tools { get; }

    public bool HasDocuments => Documents.Count > 0;

    public string ToolStatusText => Loc.Format("Status.ToolName", Tools.ActiveToolName)
                                    + (Tools.EditMask ? " · " + Loc.Get("Status.MaskEditing") : string.Empty);

    /// <summary>Pixelraster ab 800 % (Arbeitsbereichs-Einstellung, gilt für alle Dokumente).</summary>
    public bool ShowPixelGrid => _settings.Workspace.ShowPixelGrid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(HasDocuments))]
    private DocumentViewModel? _activeDocument;

    /// <summary>Kurze Meldung in der Statusleiste (z. B. „Exportiert: bild.png").</summary>
    [ObservableProperty]
    private string _statusMessage = Loc.Get("Status.Ready");

    /// <summary>True während Datei-Vorgängen. Dateibefehle sind dann gesperrt.</summary>
    [ObservableProperty]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        _shell.SetBusy(value);
        if (value)
        {
            StatusMessage = Loc.Get("Status.Busy");
        }

        _registry.NotifyStateChanged();
    }

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
        var document = ImageDocument.Create(request.Name, request.Width, request.Height, Loc.Get("Layer.Background"),
            background: request.Background);
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
        if (request is null)
        {
            return;
        }

        try
        {
            CreateDocument(request);
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Nicht genug Speicher für {Width}x{Height}", request.Width, request.Height);
            _dialogs.ShowError(Loc.Get("Msg.Error.Title"), Loc.Format("Msg.File.OutOfMemory", request.Name));
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
            doc.FilePath ?? doc.SourcePath ?? Loc.Get("Msg.DocInfo.NotSaved"),
            Loc.Get(doc.IsDirty ? "Msg.Yes" : "Msg.No")));
    }

    private void ShowAbout() =>
        _dialogs.ShowInformation(Loc.Format("Msg.About.Title", AppInfo.ProductName),
            Loc.Format("Msg.About.Body", AppInfo.ProductName, AppInfo.Version));

    private void TogglePixelGrid() =>
        _settings.UpdateWorkspace(_settings.Workspace with { ShowPixelGrid = !_settings.Workspace.ShowPixelGrid });

    private void RegisterCommands()
    {
        bool HasDoc() => _workspace.ActiveDocument is not null;
        bool HasDocAndIdle() => HasDoc() && !IsBusy;

        void Add(AppCommand c) => _registry.Register(c);
        void Plan(string id, string key, string milestone, string? gesture = null) =>
            Add(AppCommand.Planned(id, key, milestone, gesture));

        // Datei
        Add(AppCommand.Create(FileNew, "Cmd.File.New", NewDocument, () => !IsBusy, "Ctrl+N"));
        Add(AppCommand.Create(FileOpen, "Cmd.File.Open", () => RunAsync(OpenWithDialogAsync), () => !IsBusy, "Ctrl+O"));
        Plan(FileSave, "Cmd.File.Save", "M7", "Ctrl+S");
        Plan(FileSaveAs, "Cmd.File.SaveAs", "M7", "Ctrl+Shift+S");
        Plan(FileSaveCopy, "Cmd.File.SaveCopy", "M7");
        Add(AppCommand.Create(FileImport, "Cmd.File.Import", () => RunAsync(ImportWithDialogAsync), HasDocAndIdle,
            "Ctrl+Shift+O"));
        Add(AppCommand.Create(FileExport, "Cmd.File.Export", () => RunAsync(ExportWithDialogAsync), HasDocAndIdle,
            "Ctrl+Shift+E"));
        Add(AppCommand.Create(FileDocumentInfo, "Cmd.File.DocumentInfo", ShowDocumentInfo, HasDoc));
        Add(AppCommand.Create(FileClose, "Cmd.File.Close",
            () => { if (ActiveDocument is { } d) { CloseDocument(d); } }, HasDocAndIdle, "Ctrl+W"));
        Add(AppCommand.Create(FileCloseAll, "Cmd.File.CloseAll", () => CloseAllDocuments(), HasDocAndIdle, "Ctrl+Shift+W"));
        Add(AppCommand.Create(FileExit, "Cmd.File.Exit", _shell.RequestExit));

        // Bearbeiten
        RegisterEditingCommands();
        Plan(EditPreferences, "Cmd.Edit.Preferences", "M9");

        // Ansicht
        Add(AppCommand.Create(ViewZoomIn, "Cmd.View.ZoomIn", () => ActiveDocument?.ZoomIn(), HasDoc, "Ctrl+OemPlus"));
        Add(AppCommand.Create(ViewZoomOut, "Cmd.View.ZoomOut", () => ActiveDocument?.ZoomOut(), HasDoc, "Ctrl+OemMinus"));
        Add(AppCommand.Create(ViewActualSize, "Cmd.View.ActualSize", () => ActiveDocument?.ActualSize(), HasDoc, "Ctrl+D0"));
        Add(AppCommand.Create(ViewFitToWindow, "Cmd.View.FitToWindow", () => ActiveDocument?.FitToWindow(), HasDoc, "Ctrl+B"));
        Plan(ViewRulers, "Cmd.View.Rulers", "M8");
        Plan(ViewGrid, "Cmd.View.Grid", "M8");
        Plan(ViewGuides, "Cmd.View.Guides", "M8");
        Add(AppCommand.Toggle(ViewPixelGrid, "Cmd.View.PixelGrid", TogglePixelGrid, () => ShowPixelGrid));
        Add(AppCommand.Create(ViewFullScreen, "Cmd.View.FullScreen", _shell.ToggleFullScreen, gesture: "F11"));
        Plan(ViewPanels, "Cmd.View.Panels", "M8");
        Plan(ViewWorkspaces, "Cmd.View.Workspaces", "M9");

        // Bild (Größe, Drehen, Spiegeln, Zuschneiden: MainViewModel.Editing.cs)
        Add(AppCommand.Create(ImageInfo, "Cmd.Image.Info", ShowDocumentInfo, HasDoc));

        // Ebenen (MainViewModel.Layers.cs)
        RegisterLayerCommands();

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

        foreach (var conflict in _registry.FindGestureConflicts())
        {
            _logger.LogError("Tastenkürzel-Konflikt {Gesture}: {Commands}", conflict.Gesture,
                string.Join(", ", conflict.CommandIds));
        }
    }

    private IReadOnlyList<MenuItemViewModel> BuildMenus() =>
        MenuLayout.Default.Select(menu => MenuItemViewModel.Submenu(
            Loc.Get(menu.TextKey),
            menu.Items.SelectMany(id => id == MenuLayout.ToolsTokens
                ? Tools.Tools.Select(t => MenuItemViewModel.ForCommand(_registry.Get(ToolPrefix + t.Id), _registry))
                : [BuildMenuItem(id)]).ToList())).ToList();

    private MenuItemViewModel BuildMenuItem(string id) => id switch
    {
        MenuLayout.Separator => MenuItemViewModel.Separator(),
        MenuLayout.RecentFiles => MenuItemViewModel.Submenu(Loc.Get("Cmd.File.Recent"), _recentItems),
        HelpAbout => MenuItemViewModel.ForCommand(_registry.Get(id), _registry,
            Loc.Format("Cmd.Help.About", AppInfo.ProductName)),
        _ => MenuItemViewModel.ForCommand(_registry.Get(id), _registry),
    };
}
