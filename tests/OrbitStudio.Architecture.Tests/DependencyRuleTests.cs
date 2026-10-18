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
        { "OrbitStudio.Application", ["OrbitStudio.Domain", "OrbitStudio.Configuration"] },
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
    [InlineData("OrbitStudio.Configuration")]
    [InlineData("OrbitStudio.Diagnostics")]
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
