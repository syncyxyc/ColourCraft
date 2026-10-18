using Microsoft.Extensions.Logging.Abstractions;
using OrbitStudio.Application.Documents;
using OrbitStudio.Domain.Documents;

namespace OrbitStudio.Application.Tests;

public class DocumentWorkspaceTests
{
    private readonly DocumentWorkspace _workspace = new(NullLogger<DocumentWorkspace>.Instance);

    private static ImageDocument Doc(string name) => ImageDocument.Create(name, 10, 10, "HG");

    [Fact]
    public void Add_MakesDocumentActive()
    {
        var a = Doc("A");
        var b = Doc("B");

        _workspace.Add(a);
        _workspace.Add(b);

        _workspace.Documents.ShouldBe([a, b]);
        _workspace.ActiveDocument.ShouldBe(b);
    }

    [Fact]
    public void Add_SameDocumentTwice_Throws()
    {
        var a = Doc("A");
        _workspace.Add(a);
        Should.Throw<InvalidOperationException>(() => _workspace.Add(a));
    }

    [Fact]
    public void Close_Active_ActivatesRightNeighbourThenLeft()
    {
        var a = Doc("A");
        var b = Doc("B");
        var c = Doc("C");
        _workspace.Add(a);
        _workspace.Add(b);
        _workspace.Add(c);

        _workspace.Activate(b);
        _workspace.Close(b);
        _workspace.ActiveDocument.ShouldBe(c);

        _workspace.Close(c);
        _workspace.ActiveDocument.ShouldBe(a);

        _workspace.Close(a);
        _workspace.ActiveDocument.ShouldBeNull();
        _workspace.HasDocuments.ShouldBeFalse();
    }

    [Fact]
    public void Close_Inactive_KeepsActiveDocument()
    {
        var a = Doc("A");
        var b = Doc("B");
        _workspace.Add(a);
        _workspace.Add(b);

        _workspace.Close(a);

        _workspace.ActiveDocument.ShouldBe(b);
    }

    [Fact]
    public void Activate_UnknownDocument_Throws()
    {
        Should.Throw<InvalidOperationException>(() => _workspace.Activate(Doc("X")));
    }

    [Fact]
    public void Events_AreRaisedInOrder()
    {
        var log = new List<string>();
        _workspace.DocumentAdded += (_, d) => log.Add("add " + d.Name);
        _workspace.DocumentClosed += (_, d) => log.Add("close " + d.Name);
        _workspace.ActiveDocumentChanged += (_, _) => log.Add("active " + (_workspace.ActiveDocument?.Name ?? "-"));

        var a = Doc("A");
        _workspace.Add(a);
        _workspace.Close(a);

        log.ShouldBe(["add A", "active A", "close A", "active -"]);
    }

    [Fact]
    public void DocumentState_IsNotSharedBetweenDocuments()
    {
        var a = Doc("A");
        var b = Doc("B");
        _workspace.Add(a);
        _workspace.Add(b);

        a.MarkDirty();
        _workspace.Activate(a);
        _workspace.Activate(b);

        a.IsDirty.ShouldBeTrue();
        b.IsDirty.ShouldBeFalse();
    }

    [Fact]
    public void NextUntitledNumber_Increments()
    {
        _workspace.NextUntitledNumber().ShouldBe(1);
        _workspace.NextUntitledNumber().ShouldBe(2);
    }
}
