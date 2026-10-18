using System.IO;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Files;
using OrbitStudio.Imaging.Codecs;
using OrbitStudio.Presentation.Localization;
using OrbitStudio.Presentation.Services;

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
                    string.Equals(d.Document.SourcePath, path, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    ActiveDocument = existing;
                    opened++;
                    continue;
                }

                var result = await _files.OpenAsync(path);
                if (result.Success)
                {
                    _workspace.Add(result.Document!);
                    _settings.AddRecentFile(path);
                    finalStatus = Loc.Format("Status.Opened", Path.GetFileName(path));
                    opened++;
                    continue;
                }

                if (result.Error == FileError.NotFound)
                {
                    _settings.RemoveRecentFile(path);
                }

                if (showErrors)
                {
                    _dialogs.ShowError(Loc.Get("Msg.File.OpenFailed.Title"), FileErrorMessage(result.Error, path));
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

    /// <summary>Exportiert das aktive Dokument ohne Dialog (für Tests und spätere Automatisierung).</summary>
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
            var result = await _files.ExportAsync(vm.Document.Pixels, path, options);
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

    private async Task OpenWithDialogAsync()
    {
        var readers = _files.Codecs.Readers.Select(c => c.Format).ToList();
        var allPatterns = readers.SelectMany(f => f.Extensions).Select(e => "*" + e).ToList();
        var filters = new List<FileFilter> { new(Loc.Get("Format.AllImages"), allPatterns) };
        filters.AddRange(readers.Select(ToFilter));

        var paths = _dialogs.ShowOpenFiles(Loc.Get("Dlg.Open.Title"), filters);
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

        if (format.CanWrite && !format.SupportsAlpha && ImageFileService.HasTransparency(vm.Document.Pixels)
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
