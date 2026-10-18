namespace OrbitStudio.Presentation.Commands;

/// <summary>Stabile IDs aller Befehle. Werden in Tastenkürzel- und Layoutdateien gespeichert – nicht umbenennen.</summary>
public static class CommandIds
{
    public const string FileNew = "file.new";
    public const string FileOpen = "file.open";
    public const string FileRecent = "file.recent";
    public const string FileSave = "file.save";
    public const string FileSaveAs = "file.saveAs";
    public const string FileSaveCopy = "file.saveCopy";
    public const string FileImport = "file.import";
    public const string FileExport = "file.export";
    public const string FileDocumentInfo = "file.documentInfo";
    public const string FileClose = "file.close";
    public const string FileCloseAll = "file.closeAll";
    public const string FileExit = "file.exit";

    public const string EditUndo = "edit.undo";
    public const string EditRedo = "edit.redo";
    public const string EditCut = "edit.cut";
    public const string EditCopy = "edit.copy";
    public const string EditPaste = "edit.paste";
    public const string EditDelete = "edit.delete";
    public const string EditSelectAll = "edit.selectAll";
    public const string EditDeselect = "edit.deselect";
    public const string EditInvertSelection = "edit.invertSelection";
    public const string EditPreferences = "edit.preferences";

    public const string ViewZoomIn = "view.zoomIn";
    public const string ViewZoomOut = "view.zoomOut";
    public const string ViewActualSize = "view.actualSize";
    public const string ViewFitToWindow = "view.fitToWindow";
    public const string ViewRulers = "view.rulers";
    public const string ViewGrid = "view.grid";
    public const string ViewGuides = "view.guides";
    public const string ViewPixelGrid = "view.pixelGrid";
    public const string ViewFullScreen = "view.fullScreen";
    public const string ViewPanels = "view.panels";
    public const string ViewWorkspaces = "view.workspaces";

    public const string ImageResize = "image.resize";
    public const string ImageCanvasSize = "image.canvasSize";
    public const string ImageRotate = "image.rotate";
    public const string ImageFlip = "image.flip";
    public const string ImageCrop = "image.crop";
    public const string ImageInfo = "image.info";

    public const string LayerNew = "layer.new";
    public const string LayerDuplicate = "layer.duplicate";
    public const string LayerDelete = "layer.delete";
    public const string LayerRename = "layer.rename";
    public const string LayerMoveUp = "layer.moveUp";
    public const string LayerMoveDown = "layer.moveDown";
    public const string LayerGroupCreate = "layer.groupCreate";
    public const string LayerGroupDissolve = "layer.groupDissolve";
    public const string LayerVisibility = "layer.visibility";
    public const string LayerOpacity = "layer.opacity";
    public const string LayerBlendMode = "layer.blendMode";
    public const string LayerAddMask = "layer.addMask";
    public const string LayerMerge = "layer.merge";

    public const string AdjustBrightnessContrast = "adjust.brightnessContrast";
    public const string AdjustHueSaturation = "adjust.hueSaturation";
    public const string AdjustLevels = "adjust.levels";
    public const string AdjustCurves = "adjust.curves";
    public const string AdjustBlackWhite = "adjust.blackWhite";
    public const string AdjustInvert = "adjust.invert";
    public const string AdjustSepia = "adjust.sepia";

    public const string EffectBlur = "effect.blur";
    public const string EffectSharpen = "effect.sharpen";
    public const string EffectNoise = "effect.noise";
    public const string EffectDenoise = "effect.denoise";
    public const string EffectEdges = "effect.edges";
    public const string EffectShadow = "effect.shadow";
    public const string EffectDistort = "effect.distort";
    public const string EffectExtensions = "effect.extensions";

    public const string ToolsOptions = "tools.options";
    public const string ToolsShortcuts = "tools.shortcuts";

    public const string WindowTools = "window.tools";
    public const string WindowLayers = "window.layers";
    public const string WindowColors = "window.colors";
    public const string WindowHistory = "window.history";
    public const string WindowNavigator = "window.navigator";
    public const string WindowProperties = "window.properties";
    public const string WindowEffectBrowser = "window.effectBrowser";
    public const string WindowPlugins = "window.plugins";
    public const string WindowDiagnostics = "window.diagnostics";

    public const string HelpUserGuide = "help.userGuide";
    public const string HelpShortcuts = "help.shortcuts";
    public const string HelpAbout = "help.about";
    public const string HelpDiagnostics = "help.diagnostics";
    public const string HelpLicenses = "help.licenses";
}
