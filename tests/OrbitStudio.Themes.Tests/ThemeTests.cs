namespace OrbitStudio.Themes.Tests;

public class ThemeTests
{
    [Theory]
    [InlineData(Theme.LightId)]
    [InlineData(Theme.DarkId)]
    public void BuiltInThemes_AreValid_AndDefineEveryToken(string id)
    {
        var theme = Theme.BuiltIn.Single(t => t.Id == id);
        ThemeValidator.Validate(theme).ShouldBeEmpty();
        theme.Colors.Keys.OrderBy(k => k).ShouldBe(ThemeTokens.All.OrderBy(k => k));
    }

    [Fact]
    public void ContrastRatio_MatchesWcagReferenceValues()
    {
        ThemeValidator.ContrastRatio("#000000", "#FFFFFF").ShouldBe(21, 1e-9);
        ThemeValidator.ContrastRatio("#FFFFFF", "#FFFFFF").ShouldBe(1, 1e-9);
        ThemeValidator.ContrastRatio("#FFFFFF", "#000000").ShouldBe(21, 1e-9); // symmetrisch
        // #777777 auf Weiß: bekannter Wert ≈ 4,48
        ThemeValidator.ContrastRatio("#777777", "#FFFFFF").ShouldBe(4.48, 0.01);
        Should.Throw<FormatException>(() => ThemeValidator.ContrastRatio("rot", "#FFFFFF"));
    }

    [Fact]
    public void Validate_ReportsMissingInvalidAndUnknownColors()
    {
        var colors = Theme.Light.Colors.ToDictionary(kv => kv.Key, kv => kv.Value);
        colors.Remove(ThemeTokens.Border);
        colors[ThemeTokens.Accent] = "blau";
        colors["extra"] = "#000000";
        var issues = ThemeValidator.Validate(Theme.Light with { Colors = colors });
        issues.Select(i => i.Token).ShouldContain(ThemeTokens.Border);
        issues.Select(i => i.Token).ShouldContain(ThemeTokens.Accent);
        issues.Select(i => i.Token).ShouldContain("extra");
    }

    [Fact]
    public void Validate_RejectsUnreadableTextAndMeasuresOutOfRange()
    {
        var lowContrast = Theme.Light.WithColor(ThemeTokens.Text, "#EEEEEE");
        ThemeValidator.Validate(lowContrast).ShouldContain(i => i.Token == ThemeTokens.Text && i.Message.Contains("Kontrast"));

        foreach (var broken in new[]
                 {
                     Theme.Light with { Spacing = 1 }, Theme.Light with { Spacing = double.NaN },
                     Theme.Light with { CornerRadius = 99 }, Theme.Light with { UiScale = 0.1 },
                     Theme.Light with { UiScale = 5 }, Theme.Light with { Id = "Hat Leerzeichen" },
                     Theme.Light with { Id = "" }, Theme.Light with { Name = " " },
                     Theme.Light with { Name = new string('x', 61) }, Theme.Light with { Kind = (ThemeKind)9 },
                 })
        {
            ThemeValidator.Validate(broken).ShouldNotBeEmpty();
        }

        ThemeValidator.Validate(null).ShouldNotBeEmpty();
    }

    [Fact]
    public void Validate_DoesNotThrow_WhenColorsAreMissingEntirely()
    {
        ThemeValidator.Validate(Theme.Light with { Colors = new Dictionary<string, string>() }).Count
            .ShouldBe(ThemeTokens.All.Count);
    }

    [Fact]
    public void Export_ThenImport_RoundTripsEverything()
    {
        var custom = Theme.Dark.WithColor(ThemeTokens.Accent, "#FFAA00") with
        {
            Id = "bernstein", Name = "Bernstein", Spacing = 10, CornerRadius = 6, UiScale = 1.25,
        };
        ThemeValidator.Validate(custom).ShouldBeEmpty();
        var result = ThemeFile.Import(ThemeFile.Export(custom));
        result.Success.ShouldBeTrue(string.Join("; ", result.Errors));
        var loaded = result.Theme!;
        loaded.Id.ShouldBe("bernstein");
        loaded.Name.ShouldBe("Bernstein");
        loaded.Kind.ShouldBe(ThemeKind.Dark);
        loaded.Spacing.ShouldBe(10);
        loaded.CornerRadius.ShouldBe(6);
        loaded.UiScale.ShouldBe(1.25);
        loaded.Colors.OrderBy(kv => kv.Key).ShouldBe(custom.Colors.OrderBy(kv => kv.Key));
    }

    [Fact]
    public void Import_NormalizesCaseOfHexValues_AndFillsMissingMeasures()
    {
        var json = ThemeFile.Export(Theme.Light with { Id = "klein", Name = "Klein" })
            .Replace("#0F5FC2", "#0f5fc2");
        ThemeFile.Import(json).Theme!.Color(ThemeTokens.Accent).ShouldBe("#0F5FC2");

        var withoutMeasures = """
            { "schemaVersion": 1, "id": "ohne-masse", "name": "Ohne Maße", "kind": "light", "colors": { }
            """;
        // Ohne Farben ungültig – aber die Maßangaben dürfen fehlen, ohne dass der Import abstürzt.
        ThemeFile.Import(withoutMeasures + "}").Success.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("kein json")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":0}")]
    [InlineData("{\"schemaVersion\":1,\"id\":\"x\",\"kind\":\"purpurn\"}")]
    public void Import_BrokenFiles_FailWithMessage_WithoutThrowing(string? json)
    {
        var result = ThemeFile.Import(json);
        result.Success.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Import_NewerVersionAndOversizeFiles_AreRejected()
    {
        ThemeFile.Import("{\"schemaVersion\":99}").Errors.Single().ShouldContain("neueren");
        ThemeFile.Import(new string(' ', ThemeFile.MaxFileBytes) + "{}").Errors.Single().ShouldContain("zu groß");
    }

    [Fact]
    public void Catalog_ContainsBuiltInThemes_AndProtectsTheirIds()
    {
        var catalog = new ThemeCatalog();
        catalog.All.Select(t => t.Id).ShouldBe([Theme.LightId, Theme.DarkId]);
        var result = catalog.Add(Theme.Dark with { Name = "Anders" });
        result.Success.ShouldBeFalse();
        catalog.All.Count.ShouldBe(2);
    }

    [Fact]
    public void Catalog_AddReplaceRemoveCustomThemes()
    {
        var catalog = new ThemeCatalog();
        var one = Theme.Dark with { Id = "eigen", Name = "Eins" };
        catalog.Add(one).Success.ShouldBeTrue();
        catalog.Add(one with { Name = "Zwei" }).Success.ShouldBeTrue();
        catalog.All.Count(t => t.Id == "eigen").ShouldBe(1);
        catalog.All.Single(t => t.Id == "eigen").Name.ShouldBe("Zwei");
        catalog.Remove("eigen").ShouldBeTrue();
        catalog.Remove("eigen").ShouldBeFalse();
        catalog.Remove(Theme.LightId).ShouldBeFalse(); // eingebaut bleibt
    }

    [Fact]
    public void Catalog_RejectsInvalidThemes()
    {
        var catalog = new ThemeCatalog();
        var invalid = Theme.Light.WithColor(ThemeTokens.Text, "#FFFFFF") with { Id = "unlesbar", Name = "Unlesbar" };
        catalog.Add(invalid).Success.ShouldBeFalse();
        catalog.All.Count.ShouldBe(2);
    }

    [Fact]
    public void Resolve_FallsBackToLight_WhenUnknownOrMissing()
    {
        var catalog = new ThemeCatalog();
        catalog.Resolve(Theme.DarkId, out var fell).Id.ShouldBe(Theme.DarkId);
        fell.ShouldBeFalse();
        catalog.Resolve("gibtEsNicht", out fell).Id.ShouldBe(Theme.LightId);
        fell.ShouldBeTrue();
        catalog.Resolve(null, out fell).Id.ShouldBe(Theme.LightId);
        fell.ShouldBeTrue();
    }
}
