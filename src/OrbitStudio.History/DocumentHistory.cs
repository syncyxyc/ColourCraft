using OrbitStudio.Domain.Documents;

namespace OrbitStudio.History;

/// <summary>
/// Ein rückgängig machbarer Bearbeitungsschritt. <see cref="NameKey"/> ist ein Textschlüssel der Oberfläche
/// (z. B. „History.Brush"), damit die Historie sprachneutral bleibt.
/// </summary>
public interface IHistoryEntry
{
    string NameKey { get; }

    /// <summary>Ungefährer Speicherbedarf (für die Speichergrenze der Historie).</summary>
    long SizeBytes { get; }

    void Undo(ImageDocument document);

    void Redo(ImageDocument document);
}

/// <summary>
/// Historie eines Dokuments: Rückgängig-Stapel und Wiederholen-Zweig. Eine neue Aktion verwirft den
/// Wiederholen-Zweig. Älteste Einträge fallen weg, wenn Anzahl oder Speicher die Grenzen überschreiten.
/// </summary>
public sealed class DocumentHistory
{
    private readonly ImageDocument _document;
    private readonly LinkedList<IHistoryEntry> _undo = new();
    private readonly Stack<IHistoryEntry> _redo = new();

    // Speicherpunkt: oberster Rückgängig-Eintrag beim letzten Speichern (null = leerer Stapel, z. B. neues Dokument).
    private IHistoryEntry? _savedTop;
    private bool _savePointReachable = true;

    public DocumentHistory(ImageDocument document, int maxEntries = 100, long maxBytes = 1024L * 1024 * 1024)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        MaxEntries = maxEntries;
        MaxBytes = maxBytes;
    }

    public int MaxEntries { get; private set; }

    public long MaxBytes { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? UndoNameKey => _undo.Last?.Value.NameKey;

    public string? RedoNameKey => _redo.TryPeek(out var entry) ? entry.NameKey : null;

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    /// <summary>Älteste zuerst.</summary>
    public IEnumerable<IHistoryEntry> UndoEntries => _undo;

    public long TotalBytes => _undo.Sum(e => e.SizeBytes) + _redo.Sum(e => e.SizeBytes);

    /// <summary>Wird bei jeder Änderung an der Historie ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <summary>Nimmt einen bereits ausgeführten Schritt auf.</summary>
    public void Push(IHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _redo.Clear();
        _undo.AddLast(entry);
        Trim();
        OnChanged();
    }

    /// <summary>
    /// Fasst einen neuen Schritt mit dem letzten zusammen, wenn <paramref name="merge"/> dafür einen kombinierten
    /// Eintrag liefert (z. B. viele Deckkraft-Schritte eines Reglerzugs); sonst normales <see cref="Push"/>.
    /// </summary>
    public void PushOrMerge(IHistoryEntry entry, Func<IHistoryEntry, IHistoryEntry, IHistoryEntry?> merge)
    {
        if (_redo.Count == 0 && _undo.Last is { } last && merge(last.Value, entry) is { } combined)
        {
            last.Value = combined;
            Trim();
            OnChanged();
            return;
        }

        Push(entry);
    }

    public bool Undo()
    {
        if (_undo.Last is not { } node)
        {
            return false;
        }

        _undo.RemoveLast();
        node.Value.Undo(_document);
        _redo.Push(node.Value);
        OnChanged();
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var entry))
        {
            return false;
        }

        entry.Redo(_document);
        _undo.AddLast(entry);
        OnChanged();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        _savedTop = null;
        _savePointReachable = !_document.IsDirty;
        OnChanged();
    }

    /// <summary>
    /// Merkt den aktuellen Stand als gespeichert. Wird dieser Stand später durch Rückgängig oder Wiederholen wieder
    /// erreicht, verliert das Dokument seine Änderungsmarkierung.
    /// </summary>
    public void MarkSavePoint()
    {
        _savedTop = _undo.Last?.Value;
        _savePointReachable = true;
    }

    /// <summary>True, wenn der aktuelle Stand dem gespeicherten entspricht.</summary>
    public bool IsAtSavePoint => _savePointReachable && ReferenceEquals(_undo.Last?.Value, _savedTop);

    private void OnChanged()
    {
        if (IsAtSavePoint)
        {
            _document.MarkClean();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetLimits(int maxEntries, long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        MaxEntries = maxEntries;
        MaxBytes = maxBytes;
        Trim();
        OnChanged();
    }

    /// <summary>Entfernt die ältesten Einträge. Der jüngste Eintrag bleibt immer erhalten.</summary>
    private void Trim()
    {
        while (_undo.Count > MaxEntries)
        {
            DropOldest();
        }

        var bytes = TotalBytes;
        while (bytes > MaxBytes && _undo.Count > 1)
        {
            bytes -= _undo.First!.Value.SizeBytes;
            DropOldest();
        }
    }

    /// <summary>Ältesten Eintrag verwerfen; lag der Speicherpunkt davor oder darauf, ist er nicht mehr erreichbar.</summary>
    private void DropOldest()
    {
        var oldest = _undo.First!.Value;
        if (_savedTop is null || ReferenceEquals(oldest, _savedTop))
        {
            _savePointReachable = false;
        }

        _undo.RemoveFirst();
    }
}

/// <summary>Mehrere Schritte als ein Eintrag (Rückgängig in umgekehrter Reihenfolge).</summary>
public sealed class CompositeEntry(string nameKey, IReadOnlyList<IHistoryEntry> entries) : IHistoryEntry
{
    public string NameKey { get; } = nameKey;

    public IReadOnlyList<IHistoryEntry> Entries { get; } = entries;

    public long SizeBytes => Entries.Sum(e => e.SizeBytes);

    public void Undo(ImageDocument document)
    {
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            Entries[i].Undo(document);
        }
    }

    public void Redo(ImageDocument document)
    {
        foreach (var entry in Entries)
        {
            entry.Redo(document);
        }
    }
}

/// <summary>Schritt, der sich selbst umkehrt bzw. mit zwei Aktionen beschrieben ist (z. B. Maske umkehren).</summary>
public sealed class ActionEntry(string nameKey, Action<ImageDocument> undo, Action<ImageDocument> redo, long sizeBytes = 0)
    : IHistoryEntry
{
    public string NameKey { get; } = nameKey;

    public long SizeBytes { get; } = sizeBytes;

    public void Undo(ImageDocument document) => undo(document);

    public void Redo(ImageDocument document) => redo(document);
}
