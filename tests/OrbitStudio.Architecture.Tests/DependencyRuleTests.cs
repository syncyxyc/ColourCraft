using System.Reflection;

namespace OrbitStudio.Architecture.Tests;

/// <summary>
/// Prüft die Abhängigkeitsregeln aus docs/architecture.md anhand der tatsächlich referenzierten Assemblies.
/// Neue Projekte werden hier mit ihren erlaubten OrbitStudio-Abhängigkeiten ergänzt.
/// </summary>
public class DependencyRuleTests
{
    private static readonly string[] UiAssemblies =
        ["PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "System.Windows.Forms"];

    public static TheoryData<string, string[]> AllowedProjectReferences => new()
    {
        { "OrbitStudio.Domain", [] },
        { "OrbitStudio.Configuration", [] },
        { "OrbitStudio.Diagnostics", [] },
        { "OrbitStudio.Imaging", ["OrbitStudio.Domain"] },
        { "OrbitStudio.Rendering", ["OrbitStudio.Domain"] },
        { "OrbitStudio.Layers", ["OrbitStudio.Domain", "OrbitStudio.Rendering"] },
        { "OrbitStudio.History", ["OrbitStudio.Domain"] },
        { "OrbitStudio.Tools", ["OrbitStudio.Domain", "OrbitStudio.History", "OrbitStudio.Rendering", "OrbitStudio.Layers"] },
        {
            "OrbitStudio.Application",
            ["OrbitStudio.Domain", "OrbitStudio.Configuration", "OrbitStudio.Imaging", "OrbitStudio.History"]
        },
    };

    [Theory]
    [MemberData(nameof(AllowedProjectReferences))]
    public void Project_ReferencesOnlyAllowedOrbitAssemblies(string assemblyName, string[] allowed)
    {
        var references = Load(assemblyName).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("OrbitStudio.", StringComparison.Ordinal));

        references.Except(allowed).ShouldBeEmpty($"{assemblyName} darf nur {string.Join(", ", allowed)} referenzieren");
    }

    [Theory]
    [InlineData("OrbitStudio.Domain")]
    [InlineData("OrbitStudio.Rendering")]
    [InlineData("OrbitStudio.History")]
    [InlineData("OrbitStudio.Tools")]
    [InlineData("OrbitStudio.Application")]
    public void Project_DoesNotReferenceCodecLibrary(string assemblyName)
    {
        // Codecs (SkiaSharp) sind auf OrbitStudio.Imaging beschränkt.
        Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!).ShouldNotContain("SkiaSharp");
    }

    [Theory]
    [InlineData("OrbitStudio.Domain")]
    [InlineData("OrbitStudio.Configuration")]
    [InlineData("OrbitStudio.Diagnostics")]
    [InlineData("OrbitStudio.Imaging")]
    [InlineData("OrbitStudio.Rendering")]
    [InlineData("OrbitStudio.Layers")]
    [InlineData("OrbitStudio.History")]
    [InlineData("OrbitStudio.Tools")]
    [InlineData("OrbitStudio.Application")]
    public void CoreProject_HasNoUiDependency(string assemblyName)
    {
        var references = Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);
        references.Intersect(UiAssemblies).ShouldBeEmpty($"{assemblyName} darf kein UI-Framework referenzieren");
    }

    [Fact]
    public void Domain_HasNoThirdPartyDependency()
    {
        var references = Load("OrbitStudio.Domain").GetReferencedAssemblies().Select(a => a.Name!);
        references.ShouldAllBe(n => n == "System.Runtime" || n.StartsWith("System.", StringComparison.Ordinal)
                                    || n == "netstandard");
    }

    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));
}
