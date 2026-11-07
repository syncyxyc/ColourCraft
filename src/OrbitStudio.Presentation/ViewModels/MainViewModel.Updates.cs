using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using OrbitStudio.Application.Updates;
using OrbitStudio.Configuration;
using OrbitStudio.Presentation.Localization;

namespace OrbitStudio.Presentation.ViewModels;

/// <summary>
/// Hilfe › Nach Updates suchen. Nur auf Anforderung des Nutzers: Es gibt keine automatische Prüfung. Die neue EXE wird
/// geladen und per SHA-256 geprüft, bevor das Programm beendet wird und ein Skript die Datei ersetzt.
/// </summary>
public sealed partial class MainViewModel
{
    private string UpdateTitle => Loc.Get("Msg.Update.Title");

    /// <summary>Sucht nach einer neueren Version und bietet nach Bestätigung die Installation an.</summary>
    public async Task CheckForUpdatesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        UpdateCheckResult result;
        IsBusy = true;
        try
        {
            StatusMessage = Loc.Get("Status.Update.Checking");
            result = await _updates.CheckAsync(AppInfo.Version);
        }
        finally
        {
            IsBusy = false;
            StatusMessage = Loc.Get("Status.Ready");
        }

        switch (result.Status)
        {
            case UpdateStatus.Failed:
                _dialogs.ShowWarning(UpdateTitle, result.Error!);
                break;
            case UpdateStatus.UpToDate:
                _dialogs.ShowInformation(UpdateTitle, Loc.Format("Msg.Update.UpToDate", AppInfo.ProductName, AppInfo.Version));
                break;
            case UpdateStatus.Available when result.Release is { } release:
                if (_dialogs.Confirm(UpdateTitle, Loc.Format("Msg.Update.Available", release.Version, AppInfo.Version)))
                {
                    await InstallUpdateAsync(release);
                }

                break;
        }
    }

    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        if (Documents.Any(d => d.IsDirty))
        {
            _dialogs.ShowWarning(UpdateTitle, Loc.Get("Msg.Update.Unsaved"));
            return;
        }

        var target = Environment.ProcessPath;
        if (target is null || !string.Equals(Path.GetFileName(target), UpdateChecker.AssetName, StringComparison.OrdinalIgnoreCase))
        {
            _dialogs.ShowWarning(UpdateTitle, Loc.Get("Msg.Update.NotExe"));
            return;
        }

        if (release.ChecksumUrl is null)
        {
            _dialogs.ShowError(UpdateTitle, Loc.Get("Msg.Update.NoChecksum"));
            return;
        }

        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppInfo.DataFolderName, "updates");
        var downloaded = Path.Combine(folder, $"OrbitStudio-{release.Version}.exe");
        IsBusy = true;
        try
        {
            StatusMessage = Loc.Format("Status.Update.Downloading", release.Version);
            Directory.CreateDirectory(folder);
            await _updates.DownloadAsync(release.ExeUrl, downloaded);
            var expected = UpdateChecker.ParseSha256(await _updates.GetTextAsync(release.ChecksumUrl));
            if (expected is null || !await UpdateChecker.VerifyAsync(downloaded, expected))
            {
                File.Delete(downloaded);
                _dialogs.ShowError(UpdateTitle, Loc.Get("Msg.Update.ChecksumFailed"));
                return;
            }

            var script = Path.Combine(folder, UpdateInstaller.ScriptName);
            UpdateInstaller.WriteScript(script, Environment.ProcessId, downloaded, target);
            UpdateInstaller.Start(script);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or ArgumentException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Update fehlgeschlagen.");
            _dialogs.ShowError(UpdateTitle, Loc.Format("Msg.Update.Failed", ex.Message));
            return;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = Loc.Get("Status.Ready");
        }

        // Das Skript wartet auf das Ende dieses Prozesses und ersetzt danach die Datei.
        _shell.RequestExit();
    }
}
