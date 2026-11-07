using OrbitStudio.Domain.Imaging;
using OrbitStudio.Effects;
using OrbitStudio.Plugin.Abstractions;
using OrbitStudio.Plugin.Runtime;

namespace OrbitStudio.Plugin.Tests;

// --- Plugin-Klassen, die über die Test-Assembly selbst als „fremde“ Plugins geladen werden ---------

public sealed class GoodTestPlugin : IOrbitPlugin
{
    public void Register(IPluginHost host) => host.RegisterEffect(new TestEffect("good-plugin.invert"));
}

public sealed class ThrowingTestPlugin : IOrbitPlugin
{
    public void Register(IPluginHost host)
    {
        host.RegisterEffect(new TestEffect("throwing-plugin.first"));
        throw new InvalidOperationException("Absichtlicher Fehler im Plugin");
    }
}

public sealed class WrongPrefixTestPlugin : IOrbitPlugin
{
    public void Register(IPluginHost host) => host.RegisterEffect(new TestEffect("anderes.invert"));
}

public sealed class DuplicateIdTestPlugin : IOrbitPlugin
{
    public void Register(IPluginHost host) => host.RegisterEffect(new TestEffect("duplicate-plugin.brightness"));
}

public sealed class BadDefaultTestPlugin : IOrbitPlugin
{
    public void Register(IPluginHost host) => host.RegisterEffect(new TestEffect("bad-default-plugin.x", badDefault: true));
}

public sealed class NoDefaultConstructorTestPlugin(int unused) : IOrbitPlugin
{
    public int Unused { get; } = unused;

    public void Register(IPluginHost host)
    {
    }
}

public sealed class NotAPlugin;

public sealed class TestEffect(string id, bool badDefault = false) : PointEffect
{
    public override string Id => id;

    public override IReadOnlyList<EffectParameter> Parameters =>
        badDefault ? [new NumberParameter("x", "P", 0, 10, 99)] : [];

    protected override ColorMap Prepare(EffectSettings settings) => (r, g, b, _, _) => (255 - r, 255 - g, 255 - b);
}

public sealed class PluginLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "orbit-plugin-tests-" + Guid.NewGuid().ToString("N"));

    public PluginLoaderTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Geladene Assemblies können unter Windows gesperrt bleiben; Temp-Ordner räumt das System auf.
        }
    }

    private static string TestAssemblyPath => typeof(PluginLoaderTests).Assembly.Location;

    private string MakePlugin(string folder, string id, string entryType, string? assemblySource = null,
        int apiVersion = 1, string? assemblyName = null)
    {
        var dir = Path.Combine(_root, folder);
        Directory.CreateDirectory(dir);
        var source = assemblySource ?? TestAssemblyPath;
        var fileName = assemblyName ?? Path.GetFileName(source);
        File.Copy(source, Path.Combine(dir, fileName), overwrite: true);
        File.WriteAllText(Path.Combine(dir, PluginLoader.ManifestFileName), $$"""
            { "id": "{{id}}", "name": "Test {{id}}", "version": "1.0", "vendor": "Test", "apiVersion": {{apiVersion}},
              "assembly": "{{fileName}}", "entryType": "{{entryType}}" }
            """);
        return dir;
    }

    private static PluginInfo Single(IReadOnlyList<PluginInfo> infos, string folder) => infos.Single(i => i.FolderName == folder);

    [Fact]
    public void MissingPluginDirectory_YieldsNoPlugins()
    {
        PluginLoader.LoadAll(Path.Combine(_root, "gibtEsNicht"), new EffectRegistry()).ShouldBeEmpty();
    }

    [Fact]
    public void GoodPlugin_IsLoaded_AndItsEffectIsRegisteredAndRuns()
    {
        MakePlugin("gut", "good-plugin", typeof(GoodTestPlugin).FullName!);
        var registry = new EffectRegistry();
        var info = Single(PluginLoader.LoadAll(_root, registry), "gut");

        info.Status.ShouldBe(PluginStatus.Loaded, info.Error);
        info.Manifest!.Id.ShouldBe("good-plugin");
        info.EffectIds.ShouldBe(["good-plugin.invert"]);
        var effect = registry.Get("good-plugin.invert");
        effect.Category.ShouldBe(EffectCategory.Adjustment);

        var source = PixelBuffer.Create(1, 1);
        source.SetPixel(0, 0, Bgra32.FromStraight(100, 150, 200, 255));
        var result = EffectEngine.Render(effect, EffectSettings.Defaults(effect), source, null, CancellationToken.None).Pixels;
        result.GetPixel(0, 0).ShouldBe(Bgra32.FromStraight(155, 105, 55, 255));
    }

    [Fact]
    public void SamplePlugin_IsBuiltInstalledAndUsed_EndToEnd()
    {
        // Das Beispielplugin wird wie ein Nutzer installieren würde: Assembly + plugin.json in einen Plugin-Unterordner.
        var sampleDll = Path.Combine(AppContext.BaseDirectory, "OrbitStudio.SamplePlugin.dll");
        File.Exists(sampleDll).ShouldBeTrue("Das Beispielplugin muss mit dem Testprojekt gebaut werden.");
        var folder = Path.Combine(_root, "beispiel");
        Directory.CreateDirectory(folder);
        File.Copy(sampleDll, Path.Combine(folder, "OrbitStudio.SamplePlugin.dll"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "plugin.json"), Path.Combine(folder, "plugin.json"));

        var registry = EffectRegistry.CreateDefault();
        var info = Single(PluginLoader.LoadAll(_root, registry), "beispiel");
        info.Status.ShouldBe(PluginStatus.Loaded, info.Error);
        info.Manifest!.Id.ShouldBe("orbit-sample");

        var effect = registry.Get("orbit-sample.posterize");
        // 4 Stufen (Standard): 0, 85, 170, 255. 100 → 100/255·3 = 1,18 → 1 → 85; 150 → 1,76 → 2 → 170; 200 → 2,35 → 2 → 170.
        var source = PixelBuffer.Create(2, 1);
        source.SetPixel(0, 0, Bgra32.FromStraight(100, 150, 200, 255));
        source.SetPixel(1, 0, Bgra32.FromStraight(10, 250, 128, 255));
        var result = EffectEngine.Render(effect, EffectSettings.Defaults(effect), source, null, CancellationToken.None).Pixels;
        result.GetPixel(0, 0).ShouldBe(Bgra32.FromStraight(85, 170, 170, 255));
        // 10 → 0,12 → 0 → 0; 250 → 2,94 → 3 → 255; 128 → 1,51 → 2 → 170
        result.GetPixel(1, 0).ShouldBe(Bgra32.FromStraight(0, 255, 170, 255));
        // 2 Stufen: nur 0 oder 255
        var two = EffectSettings.Defaults(effect).With(effect, "levels", 2.0);
        var binary = EffectEngine.Render(effect, two, source, null, CancellationToken.None).Pixels;
        binary.GetPixel(0, 0).ShouldBe(Bgra32.FromStraight(0, 255, 255, 255));

        // Das Standard-Effektset bleibt erhalten.
        registry.All.Count.ShouldBe(EffectRegistry.CreateDefault().All.Count + 1);
    }

    [Fact]
    public void ThrowingPlugin_IsFailed_RegistersNothing_AndDoesNotAffectOthers()
    {
        MakePlugin("a-kaputt", "throwing-plugin", typeof(ThrowingTestPlugin).FullName!);
        MakePlugin("b-gut", "good-plugin", typeof(GoodTestPlugin).FullName!);
        var registry = new EffectRegistry();
        var infos = PluginLoader.LoadAll(_root, registry);

        var broken = Single(infos, "a-kaputt");
        broken.Status.ShouldBe(PluginStatus.Failed);
        broken.Error!.ShouldContain("Absichtlicher Fehler");
        registry.Find("throwing-plugin.first").ShouldBeNull(); // halb angemeldete Effekte bleiben draußen
        Single(infos, "b-gut").Status.ShouldBe(PluginStatus.Loaded);
        registry.Find("good-plugin.invert").ShouldNotBeNull();
    }

    [Fact]
    public void WrongPrefix_DuplicateId_AndInvalidDefaults_AreRejected()
    {
        MakePlugin("prefix", "wrong-prefix-plugin", typeof(WrongPrefixTestPlugin).FullName!);
        MakePlugin("dup", "duplicate-plugin", typeof(DuplicateIdTestPlugin).FullName!);
        MakePlugin("default", "bad-default-plugin", typeof(BadDefaultTestPlugin).FullName!);
        var registry = EffectRegistry.CreateDefault();
        registry.Register(new TestEffect("duplicate-plugin.brightness"));
        var infos = PluginLoader.LoadAll(_root, registry);

        Single(infos, "prefix").Error!.ShouldContain("beginnen");
        Single(infos, "dup").Error!.ShouldContain("schon angemeldet");
        Single(infos, "default").Error!.ShouldContain("Standardwert");
        infos.ShouldAllBe(i => i.Status == PluginStatus.Failed);
    }

    [Fact]
    public void WrongEntryPoints_AreReportedPrecisely()
    {
        MakePlugin("fehlt", "missing-type-plugin", "Gibt.Es.Nicht");
        MakePlugin("keinplugin", "not-plugin", typeof(NotAPlugin).FullName!);
        MakePlugin("ctor", "ctor-plugin", typeof(NoDefaultConstructorTestPlugin).FullName!);
        var infos = PluginLoader.LoadAll(_root, new EffectRegistry());
        Single(infos, "fehlt").Error!.ShouldContain("nicht gefunden");
        Single(infos, "keinplugin").Error!.ShouldContain("IOrbitPlugin");
        Single(infos, "ctor").Error!.ShouldContain("parameterlosem Konstruktor");
    }

    [Fact]
    public void IncompatibleApiVersion_IsReported_WithoutLoadingAssembly()
    {
        MakePlugin("alt", "old-plugin", typeof(GoodTestPlugin).FullName!, apiVersion: 7);
        var registry = new EffectRegistry();
        var info = Single(PluginLoader.LoadAll(_root, registry), "alt");
        info.Status.ShouldBe(PluginStatus.Incompatible);
        info.Error!.ShouldContain("7");
        registry.All.ShouldBeEmpty();
    }

    [Fact]
    public void DisabledPlugin_IsListed_ButNotLoaded()
    {
        MakePlugin("gut", "good-plugin", typeof(GoodTestPlugin).FullName!);
        var registry = new EffectRegistry();
        var info = Single(PluginLoader.LoadAll(_root, registry, new HashSet<string> { "good-plugin" }), "gut");
        info.Status.ShouldBe(PluginStatus.Disabled);
        info.Manifest!.Id.ShouldBe("good-plugin");
        registry.All.ShouldBeEmpty();
    }

    [Fact]
    public void TwoPluginsWithSameId_OnlyFirstFolderLoads()
    {
        MakePlugin("a", "good-plugin", typeof(GoodTestPlugin).FullName!);
        MakePlugin("b", "good-plugin", typeof(GoodTestPlugin).FullName!);
        var infos = PluginLoader.LoadAll(_root, new EffectRegistry());
        Single(infos, "a").Status.ShouldBe(PluginStatus.Loaded);
        Single(infos, "b").Status.ShouldBe(PluginStatus.Failed);
        Single(infos, "b").Error!.ShouldContain("schon");
    }

    [Theory]
    [InlineData("{ kaputt")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{ \"id\": \"Gross\", \"name\": \"n\", \"version\": \"1\", \"vendor\": \"v\", \"apiVersion\": 1, \"assembly\": \"a.dll\", \"entryType\": \"T\" }")]
    [InlineData("{ \"id\": \"ok\", \"name\": \"n\", \"version\": \"1\", \"vendor\": \"v\", \"assembly\": \"a.dll\", \"entryType\": \"T\" }")]
    [InlineData("{ \"id\": \"ok\", \"name\": \"n\", \"version\": \"1\", \"vendor\": \"v\", \"apiVersion\": 1 }")]
    public void InvalidManifests_AreFailed_WithoutThrowing(string manifest)
    {
        var dir = Path.Combine(_root, "m");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, PluginLoader.ManifestFileName), manifest);
        var info = PluginLoader.LoadAll(_root, new EffectRegistry()).Single();
        info.Status.ShouldBe(PluginStatus.Failed);
        info.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MissingManifest_AndMissingOrEscapingAssembly_AreFailed()
    {
        Directory.CreateDirectory(Path.Combine(_root, "ohne"));
        MakePlugin("fort", "gone-plugin", typeof(GoodTestPlugin).FullName!);
        File.Delete(Path.Combine(_root, "fort", Path.GetFileName(TestAssemblyPath)));

        // Assembly-Pfad, der aus dem Plugin-Ordner hinausführt: darf nie geladen werden.
        var escape = Path.Combine(_root, "ausbruch");
        Directory.CreateDirectory(escape);
        File.Copy(TestAssemblyPath, Path.Combine(_root, "draussen.dll"));
        File.WriteAllText(Path.Combine(escape, PluginLoader.ManifestFileName), $$"""
            { "id": "escape-plugin", "name": "n", "version": "1", "vendor": "v", "apiVersion": 1,
              "assembly": "..{{Path.DirectorySeparatorChar}}draussen.dll", "entryType": "{{typeof(GoodTestPlugin).FullName}}" }
            """);

        var infos = PluginLoader.LoadAll(_root, new EffectRegistry());
        Single(infos, "ohne").Error!.ShouldContain(PluginLoader.ManifestFileName);
        Single(infos, "fort").Error!.ShouldContain("Plugin-Ordner");
        Single(infos, "ausbruch").Error!.ShouldContain("Plugin-Ordner");
        Single(infos, "ausbruch").Status.ShouldBe(PluginStatus.Failed);
    }

    [Fact]
    public void NotAnAssembly_IsFailed()
    {
        var dir = Path.Combine(_root, "text");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "x.dll"), "das ist keine Assembly");
        File.WriteAllText(Path.Combine(dir, PluginLoader.ManifestFileName),
            "{ \"id\": \"text-plugin\", \"name\": \"n\", \"version\": \"1\", \"vendor\": \"v\", \"apiVersion\": 1, \"assembly\": \"x.dll\", \"entryType\": \"T\" }");
        PluginLoader.LoadAll(_root, new EffectRegistry()).Single().Status.ShouldBe(PluginStatus.Failed);
    }
}
