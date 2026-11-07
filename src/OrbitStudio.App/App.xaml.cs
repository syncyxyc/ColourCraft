using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Commands;
using OrbitStudio.Application.Documents;
using OrbitStudio.Application.Files;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Application.Settings;
using OrbitStudio.Application.Updates;
using OrbitStudio.Configuration;
using OrbitStudio.Diagnostics;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Presentation.Tooling;
using OrbitStudio.Presentation.ViewModels;
using OrbitStudio.Presentation.Views;

namespace OrbitStudio.App;

/// <summary>Kompositionswurzel: Logging, Einstellungen, Dienste, Hauptfenster.</summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private ILogger<App>? _logger;
    private FileLoggerProvider? _fileLogger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var smokeResultFile = SmokeTest.ParseArguments(e.Args);
        var dataDir = ParseDataDir(e.Args);
        var paths = smokeResultFile is not null ? SmokeTest.CreateIsolatedPaths()
            : dataDir is not null ? new AppPaths(Path.Combine(dataDir, "settings"), dataDir)
            : AppPaths.CreateDefault();

        _fileLogger = new FileLoggerProvider(paths.LogDirectory);
        _services = ConfigureServices(paths, _fileLogger, useSmokeDialogs: smokeResultFile is not null);
        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("{Product} {Version} startet.", AppInfo.ProductName, AppInfo.Version);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger.LogCritical(args.ExceptionObject as Exception, "Unbehandelte Ausnahme (Domäne).");

        var settings = _services.GetRequiredService<SettingsService>();
        var issues = settings.Load();

        var window = _services.GetRequiredService<MainWindow>();
        _services.GetRequiredService<ShellService>().Attach(window);
        ApplyWindowState(window, settings.Workspace);
        window.Closed += (_, _) => SaveSettings(window);
        MainWindow = window;
        window.Show();

        if (smokeResultFile is not null)
        {
            SmokeTest.Run(window, _services, smokeResultFile, _logger);
            return;
        }

        ReportSettingsIssues(issues);

        // Beim Start übergebene Dateien öffnen (z. B. „Öffnen mit" oder Ziehen auf die EXE).
        var files = e.Args.Where((a, i) => !a.StartsWith("--", StringComparison.Ordinal)
                                            && (i == 0 || e.Args[i - 1] != DataDirArgument)).ToList();
        if (files.Count > 0)
        {
            _ = window.ViewModel.OpenFilesAsync(files);
        }
    }

    private const string DataDirArgument = "--data-dir";

    /// <summary>
    /// <c>--data-dir &lt;Ordner&gt;</c>: Einstellungen und Logs in diesem Ordner statt im Benutzerprofil
    /// (für Tests und portable Nutzung).
    /// </summary>
    private static string? ParseDataDir(string[] args)
    {
        var index = Array.IndexOf(args, DataDirArgument);
        return index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) : null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Programmende (Code {Code}).", e.ApplicationExitCode);
        _services?.Dispose();
        _fileLogger?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices(AppPaths paths, FileLoggerProvider fileLogger, bool useSmokeDialogs)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(fileLogger);
        });
        services.AddSingleton(paths);
        services.AddSingleton<SettingsService>();
        services.AddSingleton<DocumentWorkspace>();
        services.AddSingleton(_ => ImageCodecRegistry.CreateDefault());
        services.AddSingleton<ImageFileService>();
        services.AddSingleton<ProjectFileService>();
        services.AddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<ShellService>();
        services.AddSingleton<IShellService>(sp => sp.GetRequiredService<ShellService>());
        services.AddSingleton<HistoryService>();
        services.AddSingleton<ToolController>();
        if (useSmokeDialogs)
        {
            // Smoke-Test: keine modalen Dialoge, keine Veränderung der echten Windows-Zwischenablage.
            services.AddSingleton<SmokeDialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<SmokeDialogService>());
            services.AddSingleton<SmokeClipboardService>();
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<SmokeClipboardService>());
        }
        else
        {
            services.AddSingleton<IDialogService, WpfDialogService>();
            services.AddSingleton<IClipboardService, WpfClipboardService>();
        }
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static void ApplyWindowState(Window window, WorkspaceSettings workspace)
    {
        window.Width = workspace.WindowWidth;
        window.Height = workspace.WindowHeight;

        var hasPosition = workspace.WindowLeft is { } left && workspace.WindowTop is { } top
                          && IsOnScreen(left, top, workspace.WindowWidth, workspace.WindowHeight);
        if (hasPosition)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = workspace.WindowLeft!.Value;
            window.Top = workspace.WindowTop!.Value;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (workspace.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Mindestens ein 100×100-Bereich der Titelleiste muss auf dem virtuellen Bildschirm liegen.</summary>
    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var titleBar = new Rect(left, top, Math.Min(width, 100), Math.Min(height, 100));
        return screen.IntersectsWith(titleBar);
    }

    private void SaveSettings(Window window)
    {
        if (_services is null)
        {
            return;
        }

        var settings = _services.GetRequiredService<SettingsService>();
        var shell = _services.GetRequiredService<ShellService>();
        var bounds = window.WindowState == WindowState.Normal && !shell.IsFullScreen
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        settings.UpdateWorkspace(settings.Workspace with
        {
            WindowLeft = bounds.Left,
            WindowTop = bounds.Top,
            WindowWidth = bounds.Width,
            WindowHeight = bounds.Height,
            IsMaximized = shell.StateOutsideFullScreen == WindowState.Maximized,
        });

        try
        {
            settings.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger?.LogError(ex, "Einstellungen konnten nicht gespeichert werden.");
            _services.GetRequiredService<IDialogService>().ShowError(Loc.Get("Msg.Error.Title"),
                Loc.Format("Msg.Settings.SaveFailed", ex.Message));
        }
    }

    private void ReportSettingsIssues(IReadOnlyList<SettingsIssue> issues)
    {
        if (issues.Count == 0 || _services is null)
        {
            return;
        }

        var dialogs = _services.GetRequiredService<IDialogService>();
        foreach (var issue in issues)
        {
            var file = Path.GetFileName(issue.FilePath);
            var message = issue.Status switch
            {
                SettingsLoadStatus.Invalid => Loc.Format("Msg.Settings.Invalid", file,
                    issue.BackupPath ?? Loc.Get("Msg.Settings.NoBackup")),
                SettingsLoadStatus.NewerVersion => Loc.Format("Msg.Settings.NewerVersion", file),
                SettingsLoadStatus.Corrected => Loc.Format("Msg.Settings.Corrected", file),
                _ => null,
            };
            if (message is not null)
            {
                dialogs.ShowWarning(Loc.Get("Msg.Settings.Title"), message);
            }
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unbehandelte Ausnahme im UI-Thread.");
        var logFile = _fileLogger?.CurrentLogFile ?? "-";
        _services?.GetService<IDialogService>()?.ShowError(Loc.Get("Msg.Error.Title"),
            Loc.Format("Msg.Error.Unexpected", logFile));
        e.Handled = true;
    }
}
