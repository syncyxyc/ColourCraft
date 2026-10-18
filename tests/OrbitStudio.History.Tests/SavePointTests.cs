using OrbitStudio.Domain.Documents;
using OrbitStudio.Domain.Imaging;

namespace OrbitStudio.History.Tests;

/// <summary>Speicherpunkt: Rückgängig/Wiederholen bis zum gespeicherten Stand entfernt die Änderungsmarkierung.</summary>
public class SavePointTests
{
    private readonly ImageDocument _doc = ImageDocument.Create("T", 6, 4, "HG");
    private readonly DocumentHistory _history;

    public SavePointTests() => _history = new DocumentHistory(_doc, maxEntries: 3);

    private void Paint(byte value)
    {
        var layer = _doc.PixelLayers.First();
        var backup = layer.Pixels.Clone();
        layer.Pixels.SetPixel(0, 0, new Bgra32(value, value, value, 255));
        _doc.CommitPixelChange(new IntRect(0, 0, 1, 1));
        _history.Push(PixelRegionEntry.FromBackup("Test", layer, backup, new IntRect(0, 0, 1, 1))!);
    }

    [Fact]
    public void NewDocument_UndoToStart_IsCleanAgain()
    {
        Paint(1);
        _doc.IsDirty.ShouldBeTrue();
        _history.Undo();
        _doc.IsDirty.ShouldBeFalse();
        _history.Redo();
        _doc.IsDirty.ShouldBeTrue();
    }

    [Fact]
    public void AfterSave_UndoMarksDirty_RedoBackToSavePointIsClean()
    {
        Paint(1);
        Paint(2);
        _doc.MarkSaved("x.orbit");
        _history.MarkSavePoint();

        _history.Undo();
        _doc.IsDirty.ShouldBeTrue();
        _history.Redo();
        _doc.IsDirty.ShouldBeFalse();
        _history.IsAtSavePoint.ShouldBeTrue();
    }

    [Fact]
    public void NewActionAfterUndo_DiscardsSavePointBranch()
    {
        Paint(1);
        _doc.MarkSaved("x.orbit");
        _history.MarkSavePoint();
        _history.Undo();
        Paint(9); // verwirft den Wiederholen-Zweig mit dem gespeicherten Stand

        _history.Undo();
        _doc.IsDirty.ShouldBeTrue(); // Ausgangszustand ≠ gespeicherter Stand
        _history.IsAtSavePoint.ShouldBeFalse();
    }

    [Fact]
    public void TrimmedSavePoint_IsNoLongerReachable()
    {
        // Gespeichert mit leerem Stapel, dann mehr Schritte als erlaubt (3): der Ausgangszustand ist nicht mehr erreichbar.
        Paint(1);
        Paint(2);
        Paint(3);
        Paint(4);
        while (_history.Undo())
        {
        }

        _doc.IsDirty.ShouldBeTrue();
    }
}
