using System.Runtime.CompilerServices;
using OrbitStudio.Application.Settings;
using OrbitStudio.Domain.Documents;
using OrbitStudio.History;

namespace OrbitStudio.Application.Documents;

/// <summary>
/// Liefert die Historie je Dokument (jedes Dokument hat seine eigene, nichts wird zwischen Tabs geteilt).
/// Grenzen kommen aus den globalen Einstellungen.
/// </summary>
public sealed class HistoryService
{
    private readonly ConditionalWeakTable<ImageDocument, DocumentHistory> _histories = new();
    private readonly SettingsService _settings;

    public HistoryService(SettingsService settings, DocumentWorkspace workspace)
    {
        _settings = settings;
        workspace.DocumentClosed += (_, document) => _histories.Remove(document);
    }

    public DocumentHistory For(ImageDocument document) =>
        _histories.GetValue(document, d => new DocumentHistory(d,
            _settings.Global.HistoryMaxEntries,
            (long)_settings.Global.HistoryMaxMegabytes * 1024 * 1024));
}
