using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Application.Documents;
using OrbitStudio.Application.Settings;
using OrbitStudio.Configuration;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Domain.Layers;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Presentation.Commands;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Presentation.Viewport;
using OrbitStudio.Presentation.Views;
using OrbitStudio.Rendering;

namespace OrbitStudio.App;

/// <summary>
/// Automatisierter UI-Starttest: <c>OrbitStudio.exe --smoke-test &lt;Ergebnisdatei&gt;</c>.
/// Startet das echte Hauptfenster mit isolierten Einstellungen und Testdateien in %TEMP%, bedient es über
/// Befehle der Registry und ViewModels und prüft visuellen Baum, Pixel und Dateien. Dialoge beantwortet
/// <see cref="SmokeDialogService"/>. Exitcode 0 = bestanden, 1 = fehlgeschlagen.
/// Bildschirmfotos: &lt;Ergebnisdatei&gt;.png, -zoom.png (Pixelraster), -ebenen.png (Ebenen-Panel).
/// </summary>
internal static class SmokeTest
{
    private const string Argument = "--smoke-test";

    public static string? ParseArguments(string[] args)
    {
        var index = Array.IndexOf(args, Argument);
        if (index < 0)
        {
            return null;
        }

        return index + 1 < args.Length
            ? Path.GetFullPath(args[index + 1])
            : Path.Combine(Path.GetTempPath(), "orbit-smoke-result.txt");
    }

    public static AppPaths CreateIsolatedPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "OrbitStudio-smoke-" + Guid.NewGuid().ToString("N"));
        return new AppPaths(Path.Combine(root, "settings"), Path.Combine(root, "local"));
    }

    public static void Run(MainWindow window, IServiceProvider services, string resultFile, ILogger logger)
    {
        // Erst nach vollständigem Rendern prüfen.
        window.Dispatcher.InvokeAsync(() => ExecuteAsync(window, services, resultFile, logger),
            DispatcherPriority.ApplicationIdle);
    }

    private static async Task ExecuteAsync(MainWindow window, IServiceProvider services, string resultFile, ILogger logger)
    {
        var results = new List<string>();
        var failed = false;
        var tempDir = Path.Combine(Path.GetTempPath(), "OrbitStudio-smoke-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var shotBase = Path.Combine(Path.GetDirectoryName(resultFile)!, Path.GetFileNameWithoutExtension(resultFile));

        void Check(string name, bool condition)
        {
            results.Add($"{(condition ? "PASS" : "FAIL")} {name}");
            failed |= !condition;
        }

        try
        {
            var context = new Context(window, services, tempDir, Check);
            CheckBasics(context);
            await CheckFilesAndViewAsync(context, shotBase);
            await CheckLayersAsync(context, shotBase);
            CheckTools(context, shotBase);
            CheckFullScreen(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Smoke-Test abgebrochen.");
            results.Add("FAIL Ausnahme: " + ex.GetType().Name + ": " + ex.Message);
            failed = true;
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        results.Add(failed ? "ERGEBNIS: FEHLGESCHLAGEN" : "ERGEBNIS: BESTANDEN");
        await File.WriteAllLinesAsync(resultFile, results);
        logger.LogInformation("Smoke-Test beendet: {Result}", failed ? "fehlgeschlagen" : "bestanden");

        // Shutdown schließt auch das Hauptfenster; ein vorheriges Close würde den Exitcode auf 0 setzen.
        System.Windows.Application.Current.Shutdown(failed ? 1 : 0);
    }

    private sealed class Context(MainWindow window, IServiceProvider services, string tempDir, Action<string, bool> check)
    {
        public MainWindow Window { get; } = window;

        public MainViewModel Vm => Window.ViewModel;

        public CommandRegistry Registry { get; } = services.GetRequiredService<CommandRegistry>();

        public DocumentWorkspace Workspace { get; } = services.GetRequiredService<DocumentWorkspace>();

        public SettingsService Settings { get; } = services.GetRequiredService<SettingsService>();

        public ImageCodecRegistry Codecs { get; } = services.GetRequiredService<ImageCodecRegistry>();

        public SmokeDialogService Dialogs { get; } = services.GetRequiredService<SmokeDialogService>();

        public ShellService Shell { get; } = services.GetRequiredService<ShellService>();

        public string TempDir { get; } = tempDir;

        public void Check(string name, bool condition) => check(name, condition);

        public void Execute(string commandId)
        {
            Registry.Get(commandId).Execute();
            Flush(Window);
        }

        public bool CanExecute(string commandId) => Registry.Get(commandId).CanExecute();
    }

    // --- M1: Grundgerüst ------------------------------------------------------------------------

    private static void CheckBasics(Context c)
    {
        var vm = c.Vm;
        c.Check("Fenster sichtbar", c.Window.IsVisible && c.Window.ActualWidth > 0);
        c.Check("10 Hauptmenüs aus der Registry", vm.Menus.Count == 10 && vm.Menus.All(m => m.Items.Count > 0));
        c.Check("Keine Tastenkürzel-Konflikte", c.Registry.FindGestureConflicts().Count == 0);
        c.Check("Tastenkürzel gebunden", c.Window.InputBindings.Count >= 10);
        c.Check("Ohne Dokument: Schließen, Zoom und Ebenenbefehle deaktiviert",
            !c.CanExecute(CommandIds.FileClose) && !c.CanExecute(CommandIds.ViewZoomIn)
            && !c.CanExecute(CommandIds.LayerNew));
        c.Check("Geplante Befehle deaktiviert", !c.CanExecute(CommandIds.EditUndo));

        // Neu über den echten Befehl (Dialog liefert die Eingaben)
        c.Dialogs.NewDocumentAnswers.Enqueue(new NewDocumentRequest("Smoke A", 320, 240, DocumentBackground.White));
        c.Execute(CommandIds.FileNew);
        var first = vm.ActiveDocument!;
        var second = vm.CreateDocument(new NewDocumentRequest("Smoke B", 200, 100, DocumentBackground.Transparent));
        Flush(c.Window);

        c.Check("Datei › Neu über Befehl", first.Document.Name == "Smoke A");
        c.Check("Zwei Tabs offen", vm.Documents.Count == 2 && c.Workspace.Documents.Count == 2);
        c.Check("Neues Dokument ist aktiv", vm.ActiveDocument == second && c.Workspace.ActiveDocument == second.Document);
        c.Check("Leinwand gerendert", FindDescendant<CanvasView>(c.Window) is { ActualWidth: > 0, ViewModel: var cvm } && cvm == second);
        c.Check("Kleines Bild startet mit 100 %", Math.Abs(second.View.Zoom - 1.0) < 1e-9);
        c.Check("Neues weißes Dokument: eine Ebene „Hintergrund“, weiße Pixel",
            first.Document.LayerCount == 1 && first.Document.ActiveLayer?.Name == Loc.Get("Layer.Background")
            && first.Composite.GetPixel(10, 10) == Bgra32.White);
        c.Check("Neues transparentes Dokument hat transparente Pixel", second.Composite.GetPixel(10, 10) == Bgra32.Transparent);

        vm.ActiveDocument = first;
        Flush(c.Window);
        c.Check("Tabwechsel aktualisiert Arbeitsbereich", c.Workspace.ActiveDocument == first.Document);
        c.Check("Leinwand zeigt nach Tabwechsel Dokument A", FindDescendant<CanvasView>(c.Window)?.ViewModel == first);
        c.Check("Fenstertitel zeigt aktives Dokument", c.Window.Title.StartsWith("Smoke A", StringComparison.Ordinal));
        c.Check("Statusleiste zeigt Größe",
            c.Window.FindName("SizeStatus") is System.Windows.Controls.TextBlock status
            && status.Text.Contains("320", StringComparison.Ordinal));

        c.Execute(CommandIds.ViewZoomIn);
        var zoomA = first.View.Zoom;
        vm.ActiveDocument = second;
        Flush(c.Window);
        c.Check("Zoom gilt nur für ein Dokument", zoomA > 1.0 && Math.Abs(second.View.Zoom - 1.0) < 1e-9);
        vm.ActiveDocument = first;
        Flush(c.Window);
        c.Check("Zoom bleibt beim Tabwechsel erhalten", Math.Abs(first.View.Zoom - zoomA) < 1e-9);

        c.Execute(CommandIds.FileCloseAll);
        c.Check("Alle schließen", vm.Documents.Count == 0 && c.Workspace.ActiveDocument is null);
    }

    // --- M2: Dateien und Ansicht -----------------------------------------------------------------

    private static async Task CheckFilesAndViewAsync(Context c, string shotBase)
    {
        var vm = c.Vm;
        var reference = CreateReferenceImage(640, 400);
        var pngPath = Path.Combine(c.TempDir, "referenz.png");
        await using (var stream = File.Create(pngPath))
        {
            c.Codecs.FindById("png")!.Encode(reference, stream, new EncodeOptions());
        }

        c.Dialogs.OpenFilesAnswers.Enqueue([pngPath]);
        c.Execute(CommandIds.FileOpen);
        await WaitUntilAsync(() => vm.Documents.Count == 1 && !vm.IsBusy);
        Flush(c.Window);
        var image = vm.ActiveDocument;
        c.Check("Datei › Öffnen über Befehl", image is not null && image.Document.SourcePath == pngPath);
        c.Check("Geöffnete Pixel entsprechen der Datei", image?.Composite.ContentEquals(reference) == true);
        c.Check("Datei in „Zuletzt verwendet“", c.Settings.Global.RecentFiles.FirstOrDefault() == pngPath);
        c.Check("Gleiche Datei erneut öffnen erzeugt keinen zweiten Tab",
            await vm.OpenFilesAsync([pngPath], showErrors: false) == 1 && vm.Documents.Count == 1);
        c.Check("Bitmap entspricht Komposit", image is not null && BitmapMatches(image.Bitmap, image.Composite));

        var before = image!.Composite.Clone();
        var canvas = FindDescendant<CanvasView>(c.Window)!;
        c.Execute(CommandIds.ViewActualSize);
        c.Check("100 %", Math.Abs(image.View.Zoom - 1.0) < 1e-9 && image.ZoomText.Contains("100", StringComparison.Ordinal));
        for (var i = 0; i < 7; i++) // 150, 200, 300, 400, 500, 600, 800 %
        {
            c.Registry.Get(CommandIds.ViewZoomIn).Execute();
        }

        Flush(c.Window);
        c.Check("Vergrößern über Stufen bis 800 %", Math.Abs(image.View.Zoom - 8.0) < 1e-9);
        var offsetBefore = (image.View.OffsetX, image.View.OffsetY);
        image.View.PanBy(-150, -90);
        Flush(c.Window);
        c.Check("Verschieben ändert die Ansicht",
            image.View.OffsetX != offsetBefore.OffsetX && image.View.OffsetY != offsetBefore.OffsetY);
        c.Check("Pixelraster ab 800 % aktiv", canvas.ShowPixelGrid);
        SaveScreenshot(c.Window, shotBase + "-zoom.png");

        c.Execute(CommandIds.ViewPixelGrid);
        c.Check("Pixelraster abschaltbar (Einstellung)", !c.Settings.Workspace.ShowPixelGrid && !canvas.ShowPixelGrid);
        c.Execute(CommandIds.ViewPixelGrid);

        c.Execute(CommandIds.ViewFitToWindow);
        c.Check("Bild anpassen zeigt das ganze Bild", image.View.VisibleDocumentRect() == image.Document.Bounds);
        c.Execute(CommandIds.ViewZoomOut);
        c.Check("Zoom und Verschieben verändern keine Pixel",
            image.Composite.ContentEquals(before) && image.Document.PixelLayers.Single().Pixels.ContentEquals(before));
        c.Execute(CommandIds.ViewFitToWindow);
        SaveScreenshot(c.Window, shotBase + ".png");

        foreach (var (extension, exact) in new[] { (".png", true), (".bmp", true), (".webp", false), (".jpg", false) })
        {
            var target = Path.Combine(c.TempDir, "export" + extension);
            var export = await vm.ExportActiveAsync(target);
            var reread = Decode(c, target);
            var sameSize = reread.Width == reference.Width && reread.Height == reference.Height;
            if (exact)
            {
                c.Check($"Export {extension} und erneutes Einlesen (exakt)",
                    export?.Success == true && sameSize && reread.ContentEquals(reference));
            }
            else
            {
                // Verlustbehaftet: an harten Farbkanten weichen einzelne Pixel stark ab, daher mittlere Abweichung.
                var mean = sameSize ? MeanChannelDifference(reread, reference) : double.MaxValue;
                c.Check($"Export {extension} und erneutes Einlesen (mittlere Abweichung {mean:0.00} ≤ 3)",
                    export?.Success == true && sameSize && mean <= 3);
            }
        }

        var corrupt = Path.Combine(c.TempDir, "kaputt.png");
        await File.WriteAllBytesAsync(corrupt, [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5]);
        var countBefore = vm.Documents.Count;
        c.Dialogs.Messages.Clear();
        c.Check("Beschädigte Datei wird mit deutscher Meldung abgewiesen",
            await vm.OpenFilesAsync([corrupt]) == 0 && vm.Documents.Count == countBefore
            && c.Dialogs.Messages.Any(m => m.Kind == "error" && m.Message.Contains("beschädigt", StringComparison.Ordinal)));
        var missing = Path.Combine(c.TempDir, "fehlt.png");
        c.Settings.AddRecentFile(missing);
        await vm.OpenFilesAsync([missing], showErrors: false);
        c.Check("Fehlende Datei wird aus „Zuletzt verwendet“ entfernt", !c.Settings.Global.RecentFiles.Contains(missing));
        c.Check("Nach Dateivorgängen nicht mehr beschäftigt", !vm.IsBusy && c.CanExecute(CommandIds.FileOpen));
        c.Check("Statusleiste zeigt danach keinen Wartehinweis",
            !vm.StatusMessage.Equals(Loc.Get("Status.Busy"), StringComparison.Ordinal));

        c.Execute(CommandIds.FileCloseAll);
    }

    // --- M3: Ebenen ----------------------------------------------------------------------------

    private static async Task CheckLayersAsync(Context c, string shotBase)
    {
        var vm = c.Vm;
        var doc = vm.CreateDocument(new NewDocumentRequest("Ebenen", 64, 48, DocumentBackground.White));
        Flush(c.Window);
        var panel = FindDescendant<LayersPanel>(c.Window);
        c.Check("Ebenen-Panel sichtbar mit einer Ebene", panel is { IsVisible: true } && doc.Layers.Count == 1);

        // Neue Ebene über den Menübefehl, grau gefüllt (vormultipliert, deckend)
        c.Execute(CommandIds.LayerNew);
        var gray = (PixelLayer)doc.Document.ActiveLayer!;
        c.Check("Neue Ebene ist aktiv, oben in der Liste, transparent",
            doc.Document.LayerCount == 2 && doc.Layers[0].Node == gray && doc.SelectedLayer?.Node == gray
            && gray.Pixels.GetPixel(0, 0) == Bgra32.Transparent);
        gray.Pixels.Fill(new IntRect(0, 0, 32, 48), new Bgra32(128, 128, 128, 255));
        doc.Document.CommitPixelChange(new IntRect(0, 0, 32, 48));
        Flush(c.Window);
        c.Check("Komposit: graue Ebene links, Hintergrund rechts",
            doc.Composite.GetPixel(10, 10) == new Bgra32(128, 128, 128, 255) && doc.Composite.GetPixel(40, 10) == Bgra32.White);
        c.Check("Bitmap entspricht Komposit nach Pixeländerung", BitmapMatches(doc.Bitmap, doc.Composite));

        // Mischmodi über das Panel (exakte Referenzwerte, Grau 128 über Weiß 255)
        foreach (var (mode, expected) in new[]
                 {
                     (BlendMode.Multiply, (byte)128), (BlendMode.Screen, (byte)255), (BlendMode.Difference, (byte)127),
                     (BlendMode.Darken, (byte)128), (BlendMode.Lighten, (byte)255), (BlendMode.Normal, (byte)128),
                 })
        {
            doc.SelectedBlendMode = doc.BlendModes.First(o => o.Mode == mode);
            Flush(c.Window);
            c.Check($"Mischmodus {mode} über Panel: Kanal {expected}", doc.Composite.GetPixel(10, 10).R == expected);
        }

        // Deckkraft 40 % über Panel: 128·0,4 + 255·0,6 = 204,2 → 204
        doc.SelectedOpacityPercent = 40;
        Flush(c.Window);
        c.Check("Deckkraft 40 % über Panel ergibt 204", doc.Composite.GetPixel(10, 10).R == 204 && gray.Opacity == 0.4);

        c.Execute(CommandIds.LayerVisibility);
        c.Check("Ebene ausblenden zeigt Hintergrund", doc.Composite.GetPixel(10, 10) == Bgra32.White && !gray.IsVisible);
        c.Execute(CommandIds.LayerVisibility);
        c.Check("Ebene einblenden", doc.Composite.GetPixel(10, 10).R == 204);

        // Masken
        c.Execute(CommandIds.LayerAddMask);
        c.Check("Maske hinzugefügt (alles sichtbar, Aussehen unverändert)",
            gray.Mask is not null && doc.Composite.GetPixel(10, 10).R == 204 && doc.Layers[0].HasMask);
        c.Execute(CommandIds.LayerMaskInvert);
        c.Check("Maske umkehren verbirgt die Ebene", doc.Composite.GetPixel(10, 10) == Bgra32.White);
        c.Execute(CommandIds.LayerMaskEnabled);
        c.Check("Maske deaktivieren zeigt die Ebene wieder",
            doc.Composite.GetPixel(10, 10).R == 204 && gray.Mask is { IsEnabled: false });
        c.Execute(CommandIds.LayerMaskDelete);
        c.Check("Maske löschen", gray.Mask is null && !doc.Layers[0].HasMask);

        // Eigenschaften-Dialog (F2) mit vorbereiteter Eingabe
        c.Dialogs.LayerPropertiesAnswers.Enqueue(p => p with { Name = "Grau", OpacityPercent = 100 });
        c.Execute(CommandIds.LayerRename);
        c.Check("Ebeneneigenschaften: Name und Deckkraft übernommen",
            gray.Name == "Grau" && gray.Opacity == 1.0 && doc.Layers[0].Name == "Grau");

        // Duplizieren, Reihenfolge, Löschen (ohne Rückfrage – rückgängig machbar)
        c.Execute(CommandIds.LayerDuplicate);
        var copy = doc.Document.ActiveLayer!;
        c.Check("Duplizieren legt Kopie darüber an", doc.Document.LayerCount == 3 && doc.Document.Root.Children[2] == copy
                                                      && copy.Name == Loc.Format("Layer.CopyName", "Grau"));
        c.Check("Oberste Ebene kann nicht nach oben", !c.CanExecute(CommandIds.LayerMoveUp) && c.CanExecute(CommandIds.LayerMoveDown));
        c.Execute(CommandIds.LayerMoveDown);
        c.Check("Nach unten verschiebt in der Liste", doc.Document.Root.Children[1] == copy && doc.Layers[1].Node == copy);
        c.Dialogs.Messages.Clear();
        c.Execute(CommandIds.LayerDelete);
        c.Check("Löschen ohne Rückfrage", doc.Document.LayerCount == 2 && !doc.Document.Contains(copy)
                                           && !c.Dialogs.Messages.Any(m => m.Kind == "confirm"));
        c.Execute(CommandIds.EditUndo);
        c.Check("Rückgängig stellt gelöschte Ebene an derselben Stelle wieder her",
            doc.Document.LayerCount == 3 && doc.Document.Root.Children[1] == copy && doc.Layers[1].Node == copy);
        c.Execute(CommandIds.EditRedo);
        c.Check("Wiederholen löscht sie erneut", !doc.Document.Contains(copy));

        // Sperre
        doc.Document.ActiveLayer = gray;
        c.Execute(CommandIds.LayerLock);
        c.Check("Gesperrte Ebene: Löschen und Verschieben deaktiviert",
            gray.IsLocked && !c.CanExecute(CommandIds.LayerDelete) && !c.CanExecute(CommandIds.LayerMoveDown));
        c.Execute(CommandIds.LayerLock);

        // Gruppen
        c.Execute(CommandIds.LayerGroupCreate);
        var group = doc.Document.ActiveLayer as LayerGroup;
        c.Check("Gruppe erstellen", group is not null && gray.Parent == group && doc.Layers.Count == 3
                                     && doc.Layers[0].Node == group && doc.Layers[1].Node == gray && doc.Layers[1].Indent.Left > 0);
        c.Check("Gruppe ändert das Aussehen nicht", doc.Composite.GetPixel(10, 10).R == 128);
        doc.SelectedOpacityPercent = 40;
        Flush(c.Window);
        c.Check("Gruppendeckkraft wirkt auf Inhalt", doc.Composite.GetPixel(10, 10).R == 204);
        doc.SelectedOpacityPercent = 100;
        c.Execute(CommandIds.LayerGroupDissolve);
        c.Check("Gruppe auflösen", gray.Parent == doc.Document.Root && doc.Document.LayerCount == 2);

        // Zusammenführen erhält das Aussehen (untere Ebene deckend und normal)
        doc.Document.ActiveLayer = gray;
        doc.SelectedOpacityPercent = 40;
        Flush(c.Window);
        var beforeMerge = doc.Composite.Clone();
        c.Execute(CommandIds.LayerMerge);
        c.Check("Zusammenführen: eine Ebene, gleiches Aussehen",
            doc.Document.LayerCount == 1 && doc.Composite.ContentEquals(beforeMerge));

        // Importieren als Ebene (kleiner und größer als das Dokument)
        var small = Path.Combine(c.TempDir, "gruen.png");
        var large = Path.Combine(c.TempDir, "gross.png");
        WriteSolidPng(c, small, 20, 10, Bgra32.FromStraight(0, 200, 0, 255));
        WriteSolidPng(c, large, 200, 200, Bgra32.FromStraight(0, 0, 200, 255));
        c.Dialogs.Messages.Clear();
        c.Dialogs.OpenFilesAnswers.Enqueue([small, large]);
        c.Execute(CommandIds.FileImport);
        await WaitUntilAsync(() => doc.Document.LayerCount == 3 && !vm.IsBusy);
        Flush(c.Window);
        c.Check("Import als Ebenen über Befehl (Name = Dateiname)",
            doc.Document.LayerCount == 3 && doc.Layers.Any(l => l.Name == "gruen") && doc.Layers[0].Name == "gross");
        c.Check("Großes Bild wird beschnitten und gemeldet",
            c.Dialogs.Messages.Any(m => m.Kind == "info" && m.Message.Contains("gross.png", StringComparison.Ordinal)));
        var list = (System.Windows.Controls.ListBox)panel!.FindName("LayerList");
        var selectedContainers = Enumerable.Range(0, list.Items.Count)
            .Select(i => list.ItemContainerGenerator.ContainerFromIndex(i) as System.Windows.Controls.ListBoxItem)
            .Where(item => item?.IsSelected == true)
            .Select(item => ((LayerItemViewModel)item!.DataContext).Name)
            .ToList();
        c.Check($"Panel markiert genau die aktive Ebene (markiert: {string.Join(", ", selectedContainers)})",
            selectedContainers.Count == 1 && selectedContainers[0] == doc.Document.ActiveLayer?.Name
            && list.SelectedItems.Count == 1);
        SaveScreenshot(c.Window, shotBase + "-ebenen.png");

        // Export mit mehreren Ebenen: Warnung + zusammengesetztes Ergebnis
        var exportPath = Path.Combine(c.TempDir, "ebenen.png");
        c.Dialogs.Messages.Clear();
        c.Dialogs.SaveFileAnswers.Enqueue((exportPath, 0));
        c.Execute(CommandIds.FileExport);
        await WaitUntilAsync(() => File.Exists(exportPath) && !vm.IsBusy);
        c.Check("Export warnt vor dem Zusammenführen der Ebenen",
            c.Dialogs.Messages.Any(m => m.Title == Loc.Get("Msg.Export.Merge.Title")));
        c.Check("Exportiertes PNG = Komposit, Ebenen bleiben erhalten",
            Decode(c, exportPath).ContentEquals(doc.Composite) && doc.Document.LayerCount == 3);

        // Reduzieren
        var beforeFlatten = doc.Composite.Clone();
        c.Execute(CommandIds.LayerFlatten);
        c.Check("Auf eine Ebene reduzieren erhält das Aussehen",
            doc.Document.LayerCount == 1 && doc.Composite.ContentEquals(beforeFlatten));
        c.Check("Letzte Ebene kann nicht gelöscht werden", !c.CanExecute(CommandIds.LayerDelete));
        c.Check("Ebenenänderungen markieren das Dokument als geändert", doc.IsDirty && doc.Title.EndsWith('*'));

        c.Dialogs.ConfirmAnswer = true; // ungespeicherte Änderungen verwerfen
        c.Execute(CommandIds.FileCloseAll);
        c.Check("Schließen mit ungespeicherten Änderungen fragt nach",
            vm.Documents.Count == 0 && c.Dialogs.Messages.Any(m => m.Title == "discard"));
    }

    // --- M4: Werkzeuge, Auswahl, Historie ---------------------------------------------------------

    private static void CheckTools(Context c, string shotBase)
    {
        var vm = c.Vm;
        var tools = vm.Tools;
        var doc = vm.CreateDocument(new NewDocumentRequest("Werkzeuge", 120, 80, DocumentBackground.White));
        Flush(c.Window);
        var layer = doc.Document.PixelLayers.Single();
        var canvas = FindDescendant<CanvasView>(c.Window)!;

        c.Check("Werkzeugleiste mit 22 Werkzeugen sichtbar",
            FindDescendant<System.Windows.Controls.ItemsControl>(c.Window, "ToolPalette") is { Items.Count: 22, IsVisible: true });
        c.Check("Leinwand ist mit dem Werkzeug-Controller verbunden", canvas.Tools == tools);
        var palette = FindDescendant<System.Windows.Controls.ItemsControl>(c.Window, "ToolPalette")!;
        var lastTool = (FrameworkElement)palette.ItemContainerGenerator.ContainerFromIndex(palette.Items.Count - 1);
        var paletteBottom = lastTool.TranslatePoint(new Point(0, lastTool.ActualHeight), palette).Y;
        c.Check($"Alle Werkzeuge in der Leiste sichtbar (unterstes endet bei {paletteBottom:0} von {palette.ActualHeight:0})",
            paletteBottom <= palette.ActualHeight + 0.5);
        var primaryButton = (System.Windows.Controls.Button)c.Window.FindName("PrimaryColorButton");
        c.Check("Farbfeld zeigt die Farbe (Inhalt hat Größe)",
            primaryButton.Content is FrameworkElement { ActualWidth: > 20, ActualHeight: > 20 });
        c.Check("Ohne Bearbeitung: Rückgängig deaktiviert", !c.CanExecute(CommandIds.EditUndo));

        void Stroke(double x0, double y0, double x1, double y1, OrbitStudio.Tools.PointerButton button = OrbitStudio.Tools.PointerButton.Primary)
        {
            tools.PointerDown(doc, new PointD(x0, y0), default, button, OrbitStudio.Tools.ToolModifiers.None, 1);
            for (var i = 1; i <= 8; i++)
            {
                var t = i / 8.0;
                tools.PointerMove(doc, new PointD(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t), default, button,
                    OrbitStudio.Tools.ToolModifiers.None);
            }

            tools.PointerUp(doc, new PointD(x1, y1), default, button, OrbitStudio.Tools.ToolModifiers.None);
            Flush(c.Window);
        }

        // Werkzeugwahl über das Tastenkürzel-Kommando (wie Taste „P")
        c.Execute(CommandIds.ToolPrefix + "pencil");
        c.Check("Werkzeugwahl per Befehl (Bleistift)", tools.ActiveTool.Id == "pencil"
                                                        && vm.ToolStatusText.Contains(Loc.Get("Tool.pencil"), StringComparison.Ordinal));
        c.Check("Optionsleiste zeigt Optionen des Werkzeugs", tools.Options.Count == 1);
        var original = layer.Pixels.Clone();
        Stroke(10.5, 10.5, 50.5, 10.5);
        c.Check("Bleistiftstrich setzt exakte Pixel", layer.Pixels.GetPixel(10, 10) == Bgra32.Black
                                                       && layer.Pixels.GetPixel(50, 10) == Bgra32.Black
                                                       && layer.Pixels.GetPixel(30, 11) == Bgra32.White);
        c.Check("Komposit und Bitmap zeigen den Strich", doc.Composite.GetPixel(30, 10) == Bgra32.Black
                                                          && BitmapMatches(doc.Bitmap, doc.Composite));
        c.Check("Bearbeitung markiert Dokument als geändert", doc.IsDirty);
        var painted = layer.Pixels.Clone();
        c.Execute(CommandIds.EditUndo);
        c.Check("Rückgängig (Strg+Z-Befehl) stellt Pixel exakt wieder her", layer.Pixels.ContentEquals(original)
                                                                         && vm.StatusMessage.Contains(Loc.Get("Tool.pencil"), StringComparison.Ordinal));
        c.Execute(CommandIds.EditRedo);
        c.Check("Wiederholen stellt den Strich exakt wieder her", layer.Pixels.ContentEquals(painted));

        // Farben: Tauschen und Pinsel mit rechter Maustaste
        tools.Colors.Primary = new ColorRgba(255, 0, 0);
        c.Execute(CommandIds.ColorsSwap);
        c.Check("Farben tauschen", tools.Colors.Primary == ColorRgba.White && tools.Colors.Secondary == new ColorRgba(255, 0, 0));
        c.Execute(CommandIds.ToolPrefix + "brush");
        ((OrbitStudio.Tools.Drawing.BrushTool)tools.ActiveTool).HardnessOption.Value = 100;
        Stroke(10, 40, 50, 40, OrbitStudio.Tools.PointerButton.Secondary);
        c.Check("Pinsel mit rechter Maustaste malt Hintergrundfarbe", layer.Pixels.GetPixel(30, 40) == new Bgra32(0, 0, 255, 255));
        c.Execute(CommandIds.ColorsReset);
        c.Check("Farben zurücksetzen", tools.Colors.Primary == ColorRgba.Black && tools.Colors.Secondary == ColorRgba.White);

        // Auswahl: Rechteck, dann Pinsel nur innerhalb
        c.Execute(CommandIds.ToolPrefix + "selectRectangle");
        Stroke(60, 0, 90, 80);
        c.Check("Rechteckauswahl", doc.Document.Selection?.Bounds() == new IntRect(60, 0, 30, 80)
                                    && doc.SelectionText.Contains("30", StringComparison.Ordinal)
                                    && doc.SelectionOutline.Count >= 4);
        c.Execute(CommandIds.ToolPrefix + "brush");
        Stroke(40, 60, 110, 60);
        c.Check("Malen bleibt innerhalb der Auswahl", layer.Pixels.GetPixel(70, 60) == Bgra32.Black
                                                       && layer.Pixels.GetPixel(50, 60) == Bgra32.White
                                                       && layer.Pixels.GetPixel(100, 60) == Bgra32.White);
        SaveScreenshot(c.Window, shotBase + "-werkzeuge.png");

        // Kopieren / Einfügen (Zwischenablage im Speicher) und Verschieben
        c.Execute(CommandIds.EditCopy);
        c.Execute(CommandIds.EditPaste);
        var pasted = doc.Document.ActivePixelLayer!;
        c.Check("Einfügen: neue Ebene an Originalstelle, Auswahl, Verschieben-Werkzeug",
            doc.Document.LayerCount == 2 && pasted.Name == Loc.Get("Layer.Pasted")
            && pasted.Pixels.GetPixel(70, 60) == Bgra32.Black && pasted.Pixels.GetPixel(10, 10) == Bgra32.Transparent
            && tools.ActiveTool.Id == "move");
        Stroke(75, 40, 65, 40); // 10 px nach links
        c.Check("Verschieben bewegt eingefügte Pixel und Auswahl", pasted.Pixels.GetPixel(60, 60) == Bgra32.Black
                                                                   && doc.Document.Selection?.Bounds().X == 50);
        c.Execute(CommandIds.EditUndo);
        c.Execute(CommandIds.EditUndo);
        c.Check("Rückgängig: Verschieben und Einfügen", doc.Document.LayerCount == 1 && doc.Document.ActivePixelLayer == layer);

        // Auswahl umkehren, löschen, aufheben – über die Menübefehle
        c.Execute(CommandIds.EditDeselect);
        c.Check("Auswahl aufheben", !doc.Document.HasSelection && !c.CanExecute(CommandIds.EditDeselect));
        c.Execute(CommandIds.EditSelectAll);
        c.Dialogs.NumberAnswers.Enqueue([10]);
        c.Execute(CommandIds.EditShrinkSelection);
        c.Check("Auswahl verkleinern (Dialog 10 px)", doc.Document.Selection?.Bounds() == new IntRect(10, 10, 100, 60));
        c.Execute(CommandIds.EditDelete);
        c.Check("Entf löscht ausgewählte Pixel", layer.Pixels.GetPixel(30, 40) == Bgra32.Transparent
                                                  && layer.Pixels.GetPixel(5, 5) == Bgra32.White);
        c.Execute(CommandIds.EditUndo);
        c.Check("Löschen rückgängig", layer.Pixels.GetPixel(30, 40) == new Bgra32(0, 0, 255, 255));

        // Bild: Zuschneiden auf Auswahl, Drehen, Bildgröße
        c.Execute(CommandIds.ImageCrop);
        c.Check("Auf Auswahl zuschneiden", doc.Document.Width == 100 && doc.Document.Height == 60
                                            && doc.Width == 100 && doc.View.DocumentWidth == 100);
        Flush(c.Window);
        c.Check("Bitmap nach Größenänderung neu aufgebaut", BitmapMatches(doc.Bitmap, doc.Composite));
        c.Execute(CommandIds.ImageRotateCw);
        c.Check("Um 90° drehen", doc.Document.Width == 60 && doc.Document.Height == 100);
        c.Dialogs.NumberAnswers.Enqueue([30, 50]);
        c.Execute(CommandIds.ImageResize);
        c.Check("Bildgröße (Dialog)", doc.Document.Width == 30 && doc.Document.Height == 50);
        c.Execute(CommandIds.EditUndo);
        c.Execute(CommandIds.EditUndo);
        c.Execute(CommandIds.EditUndo);
        c.Check("Drei Bildtransformationen rückgängig", doc.Document.Width == 120 && doc.Document.Height == 80
                                                         && layer.Pixels.GetPixel(30, 40) == new Bgra32(0, 0, 255, 255));

        // Farbeimer, Pipette, Maske bemalen
        c.Execute(CommandIds.EditDeselect);
        c.Execute(CommandIds.ToolPrefix + "pipette");
        tools.PointerDown(doc, new PointD(30.5, 40.5), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None, 1);
        tools.PointerUp(doc, new PointD(30.5, 40.5), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None);
        c.Check("Pipette übernimmt Farbe", tools.Colors.Primary == new ColorRgba(255, 0, 0));
        c.Execute(CommandIds.ToolPrefix + "bucket");
        tools.PointerDown(doc, new PointD(115, 75), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None, 1);
        tools.PointerUp(doc, new PointD(115, 75), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None);
        c.Check("Farbeimer füllt zusammenhängende Fläche", layer.Pixels.GetPixel(115, 75) == new Bgra32(0, 0, 255, 255)
                                                             && layer.Pixels.GetPixel(10, 10) == Bgra32.Black);
        c.Execute(CommandIds.LayerAddMask);
        c.Execute(CommandIds.LayerEditMask);
        c.Check("Maske bearbeiten aktiv", tools.EditMask && vm.ToolStatusText.Contains(Loc.Get("Status.MaskEditing"), StringComparison.Ordinal));
        c.Execute(CommandIds.ToolPrefix + "pencil");
        tools.Colors.Primary = ColorRgba.Black;
        var beforeMask = layer.Pixels.Clone();
        Stroke(5.5, 70.5, 20.5, 70.5);
        c.Check("Malen auf der Maske verbirgt Pixel, Ebenenpixel unverändert",
            layer.Mask!.Get(10, 70) == 0 && layer.Pixels.ContentEquals(beforeMask)
            && doc.Composite.GetPixel(10, 70) == Bgra32.Transparent);
        c.Execute(CommandIds.LayerEditMask);

        // Problemmeldung bei gesperrter Ebene
        c.Execute(CommandIds.LayerLock);
        Stroke(1.5, 1.5, 3.5, 1.5);
        c.Check("Gesperrte Ebene: Hinweis statt Malen", vm.StatusMessage == Loc.Get("ToolProblem.LayerLocked"));
        c.Execute(CommandIds.LayerLock);

        // Escape bricht eine laufende Form ab
        c.Execute(CommandIds.ToolPrefix + "rectangleFilled");
        var beforeShape = layer.Pixels.Clone();
        tools.PointerDown(doc, new PointD(40, 20), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None, 1);
        tools.PointerMove(doc, new PointD(80, 50), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None);
        c.Check("Formvorschau sichtbar", !layer.Pixels.ContentEquals(beforeShape));
        tools.Key(doc, OrbitStudio.Tools.ToolKey.Escape, OrbitStudio.Tools.ToolModifiers.None);
        tools.PointerUp(doc, new PointD(80, 50), default, OrbitStudio.Tools.PointerButton.Primary, OrbitStudio.Tools.ToolModifiers.None);
        Flush(c.Window);
        c.Check("Escape stellt Original exakt wieder her", layer.Pixels.ContentEquals(beforeShape));

        c.Dialogs.ConfirmAnswer = true;
        c.Execute(CommandIds.FileCloseAll);
    }

    // --- M1: Vollbild -----------------------------------------------------------------------------

    private static void CheckFullScreen(Context c)
    {
        c.Execute(CommandIds.ViewFullScreen);
        c.Check("Vollbild an", c.Shell.IsFullScreen && c.Window.WindowStyle == WindowStyle.None);
        c.Execute(CommandIds.ViewFullScreen);
        c.Check("Vollbild aus", !c.Shell.IsFullScreen && c.Window.WindowStyle != WindowStyle.None);
    }

    // --- Hilfsfunktionen ---------------------------------------------------------------------------

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 15000)
    {
        var start = Environment.TickCount64;
        while (!condition() && Environment.TickCount64 - start < timeoutMs)
        {
            await Task.Delay(25);
        }
    }

    private static PixelBuffer Decode(Context c, string path)
    {
        using var stream = File.OpenRead(path);
        return c.Codecs.FindByPath(path)!.Decode(stream).Pixels;
    }

    private static void WriteSolidPng(Context c, string path, int width, int height, Bgra32 color)
    {
        var buffer = PixelBuffer.Create(width, height);
        buffer.Fill(color);
        using var stream = File.Create(path);
        c.Codecs.FindById("png")!.Encode(buffer, stream, new EncodeOptions());
    }

    /// <summary>Deckendes Testbild mit Farbverlauf und Farbfeldern (exakt verlustfrei prüfbar).</summary>
    private static PixelBuffer CreateReferenceImage(int width, int height)
    {
        var buffer = PixelBuffer.Create(width, height);
        for (var y = 0; y < height; y++)
        {
            var row = buffer.GetPixelRow(y);
            for (var x = 0; x < width; x++)
            {
                row[x] = new Bgra32((byte)(x * 255 / (width - 1)), (byte)(y * 255 / (height - 1)), 128, 255);
            }
        }

        buffer.Fill(new IntRect(40, 40, 120, 80), Bgra32.FromStraight(220, 30, 30, 255));
        buffer.Fill(new IntRect(200, 60, 80, 160), Bgra32.White);
        buffer.Fill(new IntRect(320, 200, 160, 120), Bgra32.Black);
        return buffer;
    }

    private static double MeanChannelDifference(PixelBuffer a, PixelBuffer b)
    {
        long sum = 0;
        var da = a.Data;
        var db = b.Data;
        for (var i = 0; i < da.Length; i++)
        {
            sum += Math.Abs(da[i] - db[i]);
        }

        return (double)sum / da.Length;
    }

    private static bool BitmapMatches(WriteableBitmap bitmap, PixelBuffer pixels)
    {
        if (bitmap.PixelWidth != pixels.Width || bitmap.PixelHeight != pixels.Height
            || bitmap.Format != PixelFormats.Pbgra32)
        {
            return false;
        }

        var copy = new byte[pixels.Stride * pixels.Height];
        bitmap.CopyPixels(copy, pixels.Stride, 0);
        return copy.AsSpan().SequenceEqual(pixels.Data);
    }

    private static void Flush(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    }

    /// <summary>Rendert den Fensterinhalt als PNG (zur Sichtprüfung, ohne Fensterrahmen).</summary>
    private static void SaveScreenshot(Window window, string path)
    {
        if (window.Content is not FrameworkElement content)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static T? FindDescendant<T>(DependencyObject root, string? name = null) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && (child is not UIElement ui || ui.IsVisible)
                && (name is null || (child is FrameworkElement fe && fe.Name == name)))
            {
                return match;
            }

            if (FindDescendant<T>(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
