using static OrbitStudio.Presentation.Commands.CommandIds;

namespace OrbitStudio.Presentation.Commands;

/// <summary>
/// Standardaufbau der Menüleiste. Einträge verweisen nur auf Befehls-IDs; "-" ist eine Trennlinie.
/// Die Anpassung durch den Nutzer folgt in M9 und überschreibt diese Vorgabe.
/// </summary>
public static class MenuLayout
{
    public const string Separator = "-";

    /// <summary>Platzhalter für das dynamische Untermenü „Zuletzt verwendet".</summary>
    public const string RecentFiles = "@recent";

    /// <summary>Platzhalter: an dieser Stelle werden alle Werkzeuge als Menüeinträge eingefügt.</summary>
    public const string ToolsTokens = "@tools";

    public static IReadOnlyList<(string TextKey, string[] Items)> Default { get; } =
    [
        ("Menu.File", [FileNew, FileOpen, RecentFiles, Separator, FileSave, FileSaveAs, FileSaveCopy, Separator,
            FileImport, FileExport, Separator, FileDocumentInfo, Separator, FileClose, FileCloseAll, Separator, FileExit]),
        ("Menu.Edit", [EditUndo, EditRedo, Separator, EditCut, EditCopy, EditPaste, EditDelete, Separator,
            EditSelectAll, EditDeselect, EditInvertSelection, EditGrowSelection, EditShrinkSelection, Separator,
            EditPreferences]),
        ("Menu.View", [ViewZoomIn, ViewZoomOut, ViewActualSize, ViewFitToWindow, Separator, ViewRulers, ViewGrid,
            ViewGuides, ViewPixelGrid, Separator, ViewFullScreen, Separator, ViewPanels, ViewWorkspaces]),
        ("Menu.Image", [ImageResize, ImageCanvasSize, Separator, ImageRotateCw, ImageRotateCcw, ImageRotate180, Separator,
            ImageFlipH, ImageFlipV, Separator, ImageCrop, Separator, ImageInfo]),
        ("Menu.Layers", [LayerNew, LayerDuplicate, LayerDelete, LayerRename, Separator, LayerMoveUp, LayerMoveDown,
            Separator, LayerGroupCreate, LayerGroupDissolve, Separator, LayerVisibility, LayerLock, LayerOpacity,
            LayerBlendMode, Separator, LayerAddMask, LayerEditMask, LayerMaskEnabled, LayerMaskInvert, LayerMaskApply,
            LayerMaskDelete, Separator, LayerSelectionFromAlpha, LayerFlipH, LayerFlipV, LayerTransform, Separator, LayerMerge, LayerFlatten]),
        ("Menu.Adjustments", [AdjustBrightnessContrast, AdjustHueSaturation, AdjustLevels, AdjustCurves, AdjustChannels, Separator,
            AdjustBlackWhite, AdjustInvert, AdjustSepia]),
        ("Menu.Effects", [EffectBlur, EffectBoxBlur, EffectSharpen, EffectNoise, EffectDenoise, EffectEdges, EffectShadow,
            EffectDistort, Separator, EffectExtensions]),
        ("Menu.Tools", [ToolsTokens, Separator, ColorsSwap, ColorsReset, Separator, ToolsOptions, ToolsShortcuts,
            EditPreferences]),
        ("Menu.Window", [WindowTools, WindowLayers, WindowColors, WindowHistory, WindowNavigator, WindowProperties,
            WindowEffectBrowser, WindowPlugins, WindowDiagnostics]),
        ("Menu.Help", [HelpUserGuide, HelpShortcuts, Separator, HelpCheckUpdates, HelpDiagnostics, HelpLicenses, Separator, HelpAbout]),
    ];
}
