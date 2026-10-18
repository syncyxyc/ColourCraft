using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Application.Documents;
using OrbitStudio.Configuration;
using OrbitStudio.Domain.Documents;
using OrbitStudio.Presentation.Commands;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.Views;

namespace OrbitStudio.App;

/// <summary>
/// Automatisierter UI-Starttest: <c>OrbitStudio.exe --smoke-test &lt;Ergebnisdatei&gt;</c>.
/// Startet das echte Hauptfenster mit isolierten Einstellungen, legt Dokumente an, wechselt Tabs,
/// prüft die Darstellung und schließt alles wieder. Exitcode 0 = bestanden, 1 = fehlgeschlagen.
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
        window.Dispatcher.InvokeAsync(() => Execute(window, services, resultFile, logger), DispatcherPriority.ApplicationIdle);
    }

    private static void Execute(MainWindow window, IServiceProvider services, string resultFile, ILogger logger)
    {
        var results = new List<string>();
        var failed = false;

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

            Check("Fenster sichtbar", window.IsVisible && window.ActualWidth > 0);
            Check("10 Hauptmenüs aus der Registry", vm.Menus.Count == 10 && vm.Menus.All(m => m.Items.Count > 0));
            Check("Keine Tastenkürzel-Konflikte", registry.FindGestureConflicts().Count == 0);
            Check("Tastenkürzel gebunden", window.InputBindings.Count >= 10);
            Check("Ohne Dokument: Schließen deaktiviert", !registry.Get(CommandIds.FileClose).CanExecute());
            Check("Geplante Befehle deaktiviert", !registry.Get(CommandIds.EditUndo).CanExecute());

            var first = vm.CreateDocument(new NewDocumentRequest("Smoke A", 320, 240, DocumentBackground.White));
            var second = vm.CreateDocument(new NewDocumentRequest("Smoke B", 200, 100, DocumentBackground.Transparent));
            Flush(window);

            Check("Zwei Tabs offen", vm.Documents.Count == 2 && workspace.Documents.Count == 2);
            Check("Neues Dokument ist aktiv", vm.ActiveDocument == second && workspace.ActiveDocument == second.Document);
            Check("Leinwand mit Dokumentgröße gerendert (200×100)", FindPageSize(window) == new Size(200, 100));
            Check("Mit Dokument: Schließen aktiv", registry.Get(CommandIds.FileClose).CanExecute());

            vm.ActiveDocument = first;
            Flush(window);
            Check("Tabwechsel aktualisiert Arbeitsbereich", workspace.ActiveDocument == first.Document);
            Check("Leinwand nach Tabwechsel (320×240)", FindPageSize(window) == new Size(320, 240));
            Check("Fenstertitel zeigt aktives Dokument", window.Title.StartsWith("Smoke A", StringComparison.Ordinal));
            Check("Statusleiste zeigt Größe",
                window.FindName("SizeStatus") is System.Windows.Controls.TextBlock status
                && status.Text.Contains("320", StringComparison.Ordinal));
            SaveScreenshot(window, Path.ChangeExtension(resultFile, ".png"));

            registry.Get(CommandIds.FileClose).Execute();
            Flush(window);
            Check("Schließen entfernt aktives Dokument", vm.Documents.Count == 1 && vm.ActiveDocument == second);

            registry.Get(CommandIds.FileCloseAll).Execute();
            Flush(window);
            Check("Alle schließen", vm.Documents.Count == 0 && workspace.ActiveDocument is null);

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

        results.Add(failed ? "ERGEBNIS: FEHLGESCHLAGEN" : "ERGEBNIS: BESTANDEN");
        File.WriteAllLines(resultFile, results);
        logger.LogInformation("Smoke-Test beendet: {Result}", failed ? "fehlgeschlagen" : "bestanden");

        // Shutdown schließt auch das Hauptfenster; ein vorheriges Close würde den Exitcode auf 0 setzen.
        System.Windows.Application.Current.Shutdown(failed ? 1 : 0);
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
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static Size? FindPageSize(DependencyObject root)
    {
        var view = FindDescendant<DocumentView>(root);
        if (view?.FindName("Page") is not FrameworkElement page)
        {
            return null;
        }

        return new Size(page.ActualWidth, page.ActualHeight);
    }

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
