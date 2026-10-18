using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Files;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;
using OrbitStudio.Rendering;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>Öffnen, Exportieren und „Zuletzt verwendet".</summary>
public sealed partial class MainViewModel
{
    private IRelayCommand<string>? _openRecentCommand;

    /// <summary>
    /// Öffnet Bilddateien als neue Dokumente. Bereits geöffnete Dateien werden nur aktiviert.
    /// Fehler werden gesammelt als deutsche Meldung angezeigt. Liefert die Anzahl erfolgreich geöffneter Dateien.
    /// </summary>
    public async Task<int> OpenFilesAsync(IEnumerable<string> paths, bool showErrors = true)
    {
        var opened = 0;
        string? finalStatus = null;
        IsBusy = true;
        try
        {
            foreach (var path in paths)
            {
                var existing = Documents.FirstOrDefault(d =>
                    string.Equals(d.Document.SourcePath, path, StringComparison.OrdinalIgnoreCase)
                    || (d.Document.FilePath is { } saved && string.Equals(Path.GetFullPath(saved), Path.GetFullPath(path),
                        StringComparison.OrdinalIgnoreCase)));
                if (existing is not null)
                {
                    ActiveDocument = existing;
                    opened++;
                    continue;
                }

                string? error;
                bool notFound;
                if (ProjectFileService.IsProjectPath(path))
                {
                    var project = await _projects.OpenAsync(path);
                    if (project.Success)
                    {
                        _workspace.Add(project.Document!);
                    }

                    error = project.Success ? null : ProjectErrorMessage(project.Error, path);
                    notFound = project.Error == Storage.ProjectError.NotFound;
                }
                else
                {
                    var result = await _files.OpenAsync(path, Loc.Get("Layer.Background"));
                    if (result.Success)
                    {
                        _workspace.Add(result.Document!);
                    }

                    error = result.Success ? null : FileErrorMessage(result.Error, path);
                    notFound = result.Error == FileError.NotFound;
                }

                if (error is null)
                {
                    _settings.AddRecentFile(path);
                    finalStatus = Loc.Format("Status.Opened", Path.GetFileName(path));
                    opened++;
                    continue;
                }

                if (notFound)
                {
                    _settings.RemoveRecentFile(path);
                }

                if (showErrors)
                {
                    _dialogs.ShowError(Loc.Get("Msg.File.OpenFailed.Title"), error);
                }
            }
        }
        finally
        {
            IsBusy = false;
            StatusMessage = finalStatus ?? Loc.Get("Status.Ready");
        }

        return opened;
    }

    /// <summary>
    /// Exportiert das aktive Dokument ohne Dialog (für Tests und spätere Automatisierung).
    /// Alle sichtbaren Ebenen werden zu einem Bild zusammengesetzt; die Ebenen im Dokument bleiben unverändert.
    /// </summary>
    public async Task<ExportResult?> ExportActiveAsync(string path)
    {
        if (ActiveDocument is not { } vm)
        {
            return null;
        }

        IsBusy = true;
        try
        {
            var options = new EncodeOptions(_settings.Global.JpegQuality, vm.Document.ResolutionDpi);
            var composite = LayerCompositor.Composite(vm.Document);
            var result = await _files.ExportAsync(composite, path, options, isSnapshot: true);
            if (result.Success)
            {
                _settings.AddRecentFile(path);
            }

            return result;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = Loc.Get("Status.Ready");
        }
    }

    private static FileFilter ProjectFilter => new(Loc.Get("Format.Orbit"), ["*" + Storage.ProjectFormat.Extension]);

    /// <summary>Öffnen: alle unterstützten Dateien, ORBIT-Projekte, alle Bilder, dann je Bildformat.</summary>
    private List<FileFilter> OpenFilters()
    {
        var readers = _files.Codecs.Readers.Select(c => c.Format).ToList();
        var imagePatterns = readers.SelectMany(f => f.Extensions).Select(e => "*" + e).ToList();
        var filters = new List<FileFilter>
        {
            new(Loc.Get("Format.AllSupported"), ["*" + Storage.ProjectFormat.Extension, .. imagePatterns]),
            ProjectFilter,
            new(Loc.Get("Format.AllImages"), imagePatterns),
        };
        filters.AddRange(readers.Select(ToFilter));
        return filters;
    }

    /// <summary>Nur Bilder (Importieren als Ebene).</summary>
    private List<FileFilter> ImageOpenFilters()
    {
        var readers = _files.Codecs.Readers.Select(c => c.Format).ToList();
        var allPatterns = readers.SelectMany(f => f.Extensions).Select(e => "*" + e).ToList();
        var filters = new List<FileFilter> { new(Loc.Get("Format.AllImages"), allPatterns) };
        filters.AddRange(readers.Select(ToFilter));
        return filters;
    }

    public enum SaveMode
    {
        /// <summary>In den bisherigen Projektpfad; ohne Pfad wie „Speichern unter“.</summary>
        Save,

        /// <summary>Neuen Pfad wählen; das Dokument heißt danach wie die Datei.</summary>
        SaveAs,

        /// <summary>Kopie schreiben; Pfad, Name und Änderungsmarkierung des Dokuments bleiben unverändert.</summary>
        Copy,
    }

    public Task<bool> SaveActiveAsync(SaveMode mode) =>
        ActiveDocument is { } document ? SaveDocumentAsync(document, mode) : Task.FromResult(false);

    /// <summary>
    /// Speichert ein Dokument als „.orbit“-Projekt im Hintergrund. Während des Schreibens sind Befehle und Werkzeuge
    /// gesperrt. Liefert true bei Erfolg (false bei Abbruch im Dialog oder Fehler; Fehler werden gemeldet).
    /// </summary>
    public async Task<bool> SaveDocumentAsync(DocumentViewModel vm, SaveMode mode)
    {
        var document = vm.Document;
        var path = mode == SaveMode.Save ? document.FilePath : null;
        if (path is null)
        {
            var defaultName = Path.GetFileNameWithoutExtension(document.FilePath ?? document.Name);
            var title = Loc.Get(mode == SaveMode.Copy ? "Dlg.SaveCopy.Title" : "Dlg.SaveAs.Title");
            if (_dialogs.ShowSaveFile(title, [ProjectFilter], defaultName, 0) is not { } choice)
            {
                return false;
            }

            path = ProjectFileService.IsProjectPath(choice.Path) ? choice.Path : choice.Path + Storage.ProjectFormat.Extension;
        }

        Tools.CommitActive();
        IsBusy = true;
        Tools.InputBlocked = true;
        StatusMessage = Loc.Format("Status.Saving", Path.GetFileName(path));
        Storage.ProjectError error;
        try
        {
            error = (await _projects.SaveAsync(document, path)).Error;
        }
        finally
        {
            Tools.InputBlocked = false;
            IsBusy = false;
        }

        if (error != Storage.ProjectError.None)
        {
            StatusMessage = Loc.Get("Status.Ready");
            if (error != Storage.ProjectError.Cancelled)
            {
                _dialogs.ShowError(Loc.Get("Msg.Project.SaveFailed.Title"), ProjectErrorMessage(error, path));
            }

            return false;
        }

        var full = Path.GetFullPath(path);
        if (mode != SaveMode.Copy)
        {
            document.Name = Path.GetFileName(full);
            document.MarkSaved(full);
            _histories.For(document).MarkSavePoint();
        }

        _settings.AddRecentFile(full);
        StatusMessage = Loc.Format(mode == SaveMode.Copy ? "Status.SavedCopy" : "Status.Saved", Path.GetFileName(full));
        _registry.NotifyStateChanged();
        return true;
    }

    public static string ProjectErrorMessage(Storage.ProjectError error, string path)
    {
        var key = error switch
        {
            Storage.ProjectError.NotFound => "Msg.File.NotFound",
            Storage.ProjectError.AccessDenied => "Msg.File.AccessDenied",
            Storage.ProjectError.NotAProject => "Msg.Project.NotAProject",
            Storage.ProjectError.Corrupt => "Msg.Project.Corrupt",
            Storage.ProjectError.UnsupportedVersion => "Msg.Project.UnsupportedVersion",
            Storage.ProjectError.TooLarge => "Msg.Project.TooLarge",
            Storage.ProjectError.OutOfMemory => "Msg.File.OutOfMemory",
            Storage.ProjectError.Cancelled => "Msg.File.Cancelled",
            _ => "Msg.File.IoError",
        };
        return Loc.Format(key, Path.GetFileName(path));
    }

    private async Task OpenWithDialogAsync()
    {
        var paths = _dialogs.ShowOpenFiles(Loc.Get("Dlg.Open.Title"), OpenFilters());
        if (paths is { Count: > 0 })
        {
            await OpenFilesAsync(paths);
        }
    }

    private async Task ExportWithDialogAsync()
    {
        if (ActiveDocument is not { } vm)
        {
            return;
        }

        var writers = _files.Codecs.Writers.Select(c => c.Format).ToList();
        var sourceFormat = vm.Document.SourcePath is { } source ? _files.Codecs.FindByPath(source)?.Format : null;
        var defaultIndex = Math.Max(0, writers.FindIndex(f => f == (sourceFormat is { CanWrite: true } ? sourceFormat : writers[0])));
        var defaultName = Path.GetFileNameWithoutExtension(vm.Document.Name);

        var choice = _dialogs.ShowSaveFile(Loc.Get("Dlg.Export.Title"), writers.Select(ToFilter).ToList(), defaultName,
            defaultIndex);
        if (choice is not { } selected)
        {
            return;
        }

        var path = selected.Path;
        var format = _files.Codecs.FindByPath(path)?.Format;
        if (format is null)
        {
            // Unbekannte Endung eingetippt: Endung des gewählten Filters anhängen.
            format = writers[Math.Clamp(selected.FilterIndex, 0, writers.Count - 1)];
            path += format.DefaultExtension;
        }

        // Spezifikation 11.1: Export ist kein Projektspeichern – bei mehreren Ebenen ausdrücklich warnen.
        var layerCount = vm.Document.LayerCount;
        if (format.CanWrite && layerCount > 1 && !_dialogs.Confirm(Loc.Get("Msg.Export.Merge.Title"),
                Loc.Format("Msg.Export.Merge.Body", layerCount, Loc.Get("Format." + format.Id))))
        {
            return;
        }

        if (format.CanWrite && !format.SupportsAlpha && ImageFileService.HasTransparency(vm.Composite)
            && !_dialogs.Confirm(Loc.Get("Msg.Export.AlphaLost.Title"),
                Loc.Format("Msg.Export.AlphaLost.Body", Loc.Get("Format." + format.Id))))
        {
            return;
        }

        var result = await ExportActiveAsync(path);
        if (result is null)
        {
            return;
        }

        if (result.Success)
        {
            StatusMessage = Loc.Format("Status.Exported", Path.GetFileName(path));
        }
        else if (result.Error != FileError.Cancelled)
        {
            _dialogs.ShowError(Loc.Get("Msg.File.ExportFailed.Title"), FileErrorMessage(result.Error, path));
        }
    }

    /// <summary>Führt einen asynchronen Befehl aus und fängt unerwartete Fehler ab (statt sie zu verlieren).</summary>
    private async void RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler in einem Dateibefehl.");
            IsBusy = false;
            _dialogs.ShowError(Loc.Get("Msg.Error.Title"), Loc.Format("Msg.Error.Unexpected", ex.Message));
        }
    }

    private void RebuildRecentItems()
    {
        _openRecentCommand ??= new RelayCommand<string>(
            path => { if (path is not null) { RunAsync(() => OpenFilesAsync([path])); } },
            _ => !IsBusy);

        _recentItems.Clear();
        var files = _settings.Global.RecentFiles;
        if (files.Count == 0)
        {
            _recentItems.Add(MenuItemViewModel.Placeholder(Loc.Get("Recent.Empty")));
            return;
        }

        for (var i = 0; i < files.Count; i++)
        {
            // Zugriffstaste 1–9, danach 0; Unterstriche im Dateinamen verdoppeln, damit sie sichtbar bleiben.
            var number = i < 9 ? (i + 1).ToString(Loc.Culture) : "0";
            var name = Path.GetFileName(files[i]).Replace("_", "__", StringComparison.Ordinal);
            _recentItems.Add(MenuItemViewModel.ForAction(Loc.Format("Recent.Item", number, name), _openRecentCommand,
                files[i], files[i]));
        }
    }

    private static FileFilter ToFilter(ImageFileFormat format) =>
        new(Loc.Get("Format." + format.Id), format.Extensions.Select(e => "*" + e).ToList());

    public static string FileErrorMessage(FileError error, string path)
    {
        var key = error switch
        {
            FileError.NotFound => "Msg.File.NotFound",
            FileError.AccessDenied => "Msg.File.AccessDenied",
            FileError.UnknownFormat => "Msg.File.UnknownFormat",
            FileError.FormatNotWritable => "Msg.File.FormatNotWritable",
            FileError.Corrupt => "Msg.File.Corrupt",
            FileError.Unsupported => "Msg.File.Unsupported",
            FileError.TooLarge => "Msg.File.TooLarge",
            FileError.OutOfMemory => "Msg.File.OutOfMemory",
            FileError.EncodeFailed => "Msg.File.EncodeFailed",
            FileError.Cancelled => "Msg.File.Cancelled",
            _ => "Msg.File.IoError",
        };
        return Loc.Format(key, Path.GetFileName(path));
    }
}
