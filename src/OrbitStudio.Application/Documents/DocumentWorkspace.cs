using Microsoft.Extensions.Logging;
using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Application.Documents;

/// <summary>
/// Verwaltet die geöffneten Dokumente und das aktive Dokument.
/// Jedes Dokument behält seinen eigenen Zustand; der Arbeitsbereich teilt nichts zwischen Dokumenten.
/// </summary>
public sealed class DocumentWorkspace(ILogger<DocumentWorkspace> logger)
{
    private readonly List<ImageDocument> _documents = [];
    private int _untitledCounter;

    public IReadOnlyList<ImageDocument> Documents => _documents;

    public ImageDocument? ActiveDocument { get; private set; }

    public bool HasDocuments => _documents.Count > 0;

    public event EventHandler<ImageDocument>? DocumentAdded;

    public event EventHandler<ImageDocument>? DocumentClosed;

    public event EventHandler? ActiveDocumentChanged;

    /// <summary>Liefert die nächste fortlaufende Nummer für unbenannte Dokumente (1, 2, 3, ...).</summary>
    public int NextUntitledNumber() => ++_untitledCounter;

    public void Add(ImageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_documents.Any(d => d.Id == document.Id))
        {
            throw new InvalidOperationException("Das Dokument ist bereits geöffnet.");
        }

        _documents.Add(document);
        logger.LogInformation("Dokument geöffnet: {Id} ({Width}x{Height})", document.Id, document.Width, document.Height);
        DocumentAdded?.Invoke(this, document);
        Activate(document);
    }

    public void Activate(ImageDocument? document)
    {
        if (document is not null && !_documents.Contains(document))
        {
            throw new InvalidOperationException("Nur geöffnete Dokumente können aktiviert werden.");
        }

        if (!ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = document;
            ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Schließt ein Dokument ohne Rückfrage. Die Prüfung auf ungespeicherte Änderungen erfolgt vorher im Aufrufer.
    /// </summary>
    public void Close(ImageDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var index = _documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }

        _documents.RemoveAt(index);
        logger.LogInformation("Dokument geschlossen: {Id}", document.Id);
        DocumentClosed?.Invoke(this, document);

        if (ReferenceEquals(ActiveDocument, document))
        {
            // Wie in gängigen Editoren: der rechte Nachbar, sonst der linke, wird aktiv.
            var next = _documents.Count == 0 ? null : _documents[Math.Min(index, _documents.Count - 1)];
            Activate(next);
        }
    }
}
