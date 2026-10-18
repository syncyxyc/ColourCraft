using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Application.Documents;
using OrbitStudio.Application.Files;
using OrbitStudio.Application.Settings;
using OrbitStudio.Configuration;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Presentation.Commands;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Presentation.Viewport;
using OrbitStudio.Presentation.Views;

namespace OrbitStudio.App;

/// <summary>
/// Automatisierter UI-Starttest: <c>OrbitStudio.exe --smoke-test &lt;Ergebnisdatei&gt;</c>.
/// Startet das echte Hauptfenster mit isolierten Einstellungen und Testdateien in %TEMP%, bedient es über
/// Befehle und ViewModels und prüft visuellen Baum, Pixel und Dateien. Exitcode 0 = bestanden, 1 = fehlgeschlagen.
/// Bildschirmfotos: &lt;Ergebnisdatei&gt;.png (Übersicht) und &lt;Ergebnisdatei&gt;-zoom.png (Pixelraster).
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

        void Check(string name, bool condition)
        {
            results.Add($"{(condition ? "PASS" : "FAIL")} {name}");
            failed |= !condition;
        }

        try
        {
            var vm = window.ViewModel;
            var registry = services.GetRequiredService<CommandRegistry>();
            var workspace = services.GetRequiredService<DocumentWorkspace>();
            var settings = services.GetRequiredService<SettingsService>();
            var codecs = services.GetRequiredService<ImageCodecRegistry>();

            // --- M1: Grundgerüst ---------------------------------------------------------------
            Check("Fenster sichtbar", window.IsVisible && window.ActualWidth > 0);
            Check("10 Hauptmenüs aus der Registry", vm.Menus.Count == 10 && vm.Menus.All(m => m.Items.Count > 0));
            Check("Keine Tastenkürzel-Konflikte", registry.FindGestureConflicts().Count == 0);
            Check("Tastenkürzel gebunden", window.InputBindings.Count >= 10);
            Check("Ohne Dokument: Schließen und Zoom deaktiviert",
                !registry.Get(CommandIds.FileClose).CanExecute() && !registry.Get(CommandIds.ViewZoomIn).CanExecute());
            Check("Geplante Befehle deaktiviert", !registry.Get(CommandIds.EditUndo).CanExecute());

            var first = vm.CreateDocument(new NewDocumentRequest("Smoke A", 320, 240, DocumentBackground.White));
            var second = vm.CreateDocument(new NewDocumentRequest("Smoke B", 200, 100, DocumentBackground.Transparent));
            Flush(window);

            Check("Zwei Tabs offen", vm.Documents.Count == 2 && workspace.Documents.Count == 2);
            Check("Neues Dokument ist aktiv", vm.ActiveDocument == second && workspace.ActiveDocument == second.Document);
            Check("Leinwand gerendert", FindCanvas(window) is { ActualWidth: > 0, ViewModel: var cvm } && cvm == second);
            Check("Kleines Bild startet mit 100 %", Math.Abs(second.View.Zoom - 1.0) < 1e-9);
            Check("Neues weißes Dokument hat weiße Pixel", first.Document.Pixels.GetPixel(10, 10) == Bgra32.White);
            Check("Neues transparentes Dokument hat transparente Pixel",
                second.Document.Pixels.GetPixel(10, 10) == Bgra32.Transparent);
            Check("Mit Dokument: Schließen aktiv", registry.Get(CommandIds.FileClose).CanExecute());

            vm.ActiveDocument = first;
            Flush(window);
            Check("Tabwechsel aktualisiert Arbeitsbereich", workspace.ActiveDocument == first.Document);
            Check("Leinwand zeigt nach Tabwechsel Dokument A", FindCanvas(window)?.ViewModel == first);
            Check("Fenstertitel zeigt aktives Dokument", window.Title.StartsWith("Smoke A", StringComparison.Ordinal));
            Check("Statusleiste zeigt Größe",
                window.FindName("SizeStatus") is System.Windows.Controls.TextBlock status
                && status.Text.Contains("320", StringComparison.Ordinal));

            // --- M2: Zoom je Dokument getrennt ---------------------------------------------------
            registry.Get(CommandIds.ViewZoomIn).Execute();
            Flush(window);
            var zoomA = first.View.Zoom;
            vm.ActiveDocument = second;
            Flush(window);
            Check("Zoom gilt nur für ein Dokument", zoomA > 1.0 && Math.Abs(second.View.Zoom - 1.0) < 1e-9);
            vm.ActiveDocument = first;
            Flush(window);
            Check("Zoom bleibt beim Tabwechsel erhalten", Math.Abs(first.View.Zoom - zoomA) < 1e-9);

            registry.Get(CommandIds.FileCloseAll).Execute();
            Flush(window);
            Check("Alle schließen", vm.Documents.Count == 0 && workspace.ActiveDocument is null);

            // --- M2: Bild öffnen ---------------------------------------------------------------
            var reference = CreateReferenceImage(640, 400);
            var pngPath = Path.Combine(tempDir, "referenz.png");
            await using (var stream = File.Create(pngPath))
            {
                codecs.FindById("png")!.Encode(reference, stream, new EncodeOptions());
            }

            var opened = await vm.OpenFilesAsync([pngPath], showErrors: false);
            Flush(window);
            var image = vm.ActiveDocument;
            Check("PNG geöffnet", opened == 1 && image is not null && image.Document.SourcePath == pngPath);
            Check("Geöffnete Pixel entsprechen der Datei", image?.Document.Pixels.ContentEquals(reference) == true);
            Check("Datei in „Zuletzt verwendet“", settings.Global.RecentFiles.FirstOrDefault() == pngPath);
            Check("Gleiche Datei erneut öffnen erzeugt keinen zweiten Tab",
                await vm.OpenFilesAsync([pngPath], showErrors: false) == 1 && vm.Documents.Count == 1);
            Check("Bitmap entspricht Pixelpuffer", image is not null && BitmapMatches(image.Bitmap, image.Document.Pixels));

            // --- M2: Zoom und Verschieben verändern keine Pixel ------------------------------------
            var before = image!.Document.Pixels.Clone();
            var canvas = FindCanvas(window)!;
            registry.Get(CommandIds.ViewActualSize).Execute();
            Flush(window);
            Check("100 %", Math.Abs(image.View.Zoom - 1.0) < 1e-9 && image.ZoomText.Contains("100", StringComparison.Ordinal));
            // Stufen ab 100 %: 150, 200, 300, 400, 500, 600, 800 → sieben Schritte.
            for (var i = 0; i < 7; i++)
            {
                registry.Get(CommandIds.ViewZoomIn).Execute();
            }

            Flush(window);
            Check("Vergrößern über Stufen bis 800 %", Math.Abs(image.View.Zoom - 8.0) < 1e-9);
            var offsetBefore = (image.View.OffsetX, image.View.OffsetY);
            image.View.PanBy(-150, -90);
            Flush(window);
            Check("Verschieben ändert die Ansicht",
                image.View.OffsetX != offsetBefore.OffsetX && image.View.OffsetY != offsetBefore.OffsetY);
            Check("Pixelraster ab 800 % aktiv", canvas.ShowPixelGrid);
            SaveScreenshot(window, Path.Combine(Path.GetDirectoryName(resultFile)!,
                Path.GetFileNameWithoutExtension(resultFile) + "-zoom.png"));

            registry.Get(CommandIds.ViewPixelGrid).Execute();
            Flush(window);
            Check("Pixelraster abschaltbar (Einstellung)", !settings.Workspace.ShowPixelGrid && !canvas.ShowPixelGrid);
            registry.Get(CommandIds.ViewPixelGrid).Execute();

            registry.Get(CommandIds.ViewFitToWindow).Execute();
            Flush(window);
            var visible = image.View.VisibleDocumentRect();
            Check("Bild anpassen zeigt das ganze Bild", visible == image.Document.Pixels.Bounds);
            registry.Get(CommandIds.ViewZoomOut).Execute();
            Flush(window);
            Check("Zoom und Verschieben verändern keine Pixel", image.Document.Pixels.ContentEquals(before));
            registry.Get(CommandIds.ViewFitToWindow).Execute();
            Flush(window);
            SaveScreenshot(window, Path.ChangeExtension(resultFile, ".png"));

            // --- M2: Exportieren und wieder einlesen ------------------------------------------------
            foreach (var (extension, exact) in new[] { (".png", true), (".bmp", true), (".webp", false), (".jpg", false) })
            {
                var target = Path.Combine(tempDir, "export" + extension);
                var export = await vm.ExportActiveAsync(target);
                var reread = codecs.FindByPath(target)!.Decode(File.OpenRead(target)).Pixels;
                var sameSize = reread.Width == reference.Width && reread.Height == reference.Height;
                if (exact)
                {
                    Check($"Export {extension} und erneutes Einlesen (exakt)",
                        export?.Success == true && sameSize && reread.ContentEquals(reference));
                }
                else
                {
                    // Verlustbehaftet: an harten Farbkanten weichen einzelne Pixel stark ab, daher mittlere Abweichung.
                    var mean = sameSize ? MeanChannelDifference(reread, reference) : double.MaxValue;
                    Check($"Export {extension} und erneutes Einlesen (mittlere Abweichung {mean:0.00} ≤ 3)",
                        export?.Success == true && sameSize && mean <= 3);
                }
            }

            // --- M2: Fehlerfälle ----------------------------------------------------------------
            var corrupt = Path.Combine(tempDir, "kaputt.png");
            await File.WriteAllBytesAsync(corrupt, [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4, 5]);
            var countBefore = vm.Documents.Count;
            Check("Beschädigte Datei wird abgewiesen",
                await vm.OpenFilesAsync([corrupt], showErrors: false) == 0 && vm.Documents.Count == countBefore);
            var missing = Path.Combine(tempDir, "fehlt.png");
            settings.AddRecentFile(missing);
            await vm.OpenFilesAsync([missing], showErrors: false);
            Check("Fehlende Datei wird aus „Zuletzt verwendet“ entfernt", !settings.Global.RecentFiles.Contains(missing));
            Check("Nach Dateivorgängen nicht mehr beschäftigt", !vm.IsBusy && registry.Get(CommandIds.FileOpen).CanExecute());
            Check("Statusleiste zeigt danach keinen Wartehinweis",
                !vm.StatusMessage.Equals(OrbitStudio.Presentation.Localization.Loc.Get("Status.Busy"), StringComparison.Ordinal));

            registry.Get(CommandIds.FileCloseAll).Execute();
            Flush(window);

            // --- M1: Vollbild ---------------------------------------------------------------------
            var shell = services.GetRequiredService<ShellService>();
            registry.Get(CommandIds.ViewFullScreen).Execute();
            Flush(window);
            Check("Vollbild an", shell.IsFullScreen && window.WindowStyle == WindowStyle.None);
            registry.Get(CommandIds.ViewFullScreen).Execute();
            Flush(window);
            Check("Vollbild aus", !shell.IsFullScreen && window.WindowStyle != WindowStyle.None);
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

    private static CanvasView? FindCanvas(DependencyObject root) => FindDescendant<CanvasView>(root);

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && (child is not UIElement ui || ui.IsVisible))
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
