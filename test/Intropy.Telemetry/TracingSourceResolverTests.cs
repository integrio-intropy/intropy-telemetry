namespace Intropy.Telemetry.Test;

public class TracingSourceResolverTests
{
    /// <summary>Concrete stand-in for the abstract shared configuration.</summary>
    private sealed class TestConfiguration : TelemetryConfiguration;

    private static TracingOptions Options(
        string[]? sources = null,
        string[]? disabled = null,
        TracingMode? mode = null)
    {
        var options = new TracingOptions { Mode = mode };

        foreach (var source in sources ?? [])
        {
            options.Sources.Add(source);
        }

        foreach (var source in disabled ?? [])
        {
            options.DisabledSources.Add(source);
        }

        return options;
    }

    [Fact]
    public void Resolve_WithNoConfiguration_ReturnsDefaultsAndServiceName()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(config, Options());

        Assert.Equal(TracingMode.Open, resolution.Mode);
        Assert.Equal(["Intropy.*", "Azure.*", "Orders.API"], resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
        Assert.Empty(resolution.IgnoredConfiguredSources);
    }

    [Fact]
    public void Resolve_WithDisabledDefault_RemovesOnlyThatDefault()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(config, Options(disabled: ["Intropy.*"]));

        Assert.Equal(["Azure.*", "Orders.API"], resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_CanDisableEveryDefaultIncludingServiceName()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(
            config,
            Options(disabled: ["Intropy.*", "Azure.*", "Orders.API"]));

        Assert.Empty(resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_WithConfiguredSources_AppendsThem()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(config, Options(["SomeVendor.Sdk"]));

        Assert.Contains("SomeVendor.Sdk", resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenSourceIsBothAddedAndDisabled_DisablingWins()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };
        config.Sources.Add("MyCustomActivitySource");

        var resolution = TracingSourceResolver.Resolve(
            config,
            Options(["MyCustomActivitySource"], ["MyCustomActivitySource"]));

        Assert.DoesNotContain("MyCustomActivitySource", resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenCodeRemovesDefault_DefaultIsNotRegistered()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };
        config.Sources.Remove("Azure.*");

        var resolution = TracingSourceResolver.Resolve(config, Options());

        Assert.Equal(["Intropy.*", "Orders.API"], resolution.Sources);
    }

    [Fact]
    public void Resolve_MatchesDisabledSourcesCaseInsensitively()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(config, Options(disabled: ["intropy.*"]));

        Assert.DoesNotContain("Intropy.*", resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_CollapsesDuplicatesAndIgnoresBlankEntries()
    {
        var config = new TestConfiguration { ServiceName = "Intropy.*" };

        var resolution = TracingSourceResolver.Resolve(config, Options(["  Azure.*  ", "", "   "]));

        Assert.Equal(["Intropy.*", "Azure.*"], resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenDisabledEntryMatchesNothing_ReportsItAndChangesNothing()
    {
        // A wildcard entry cannot be narrowed: Intropy.* stays registered and the attempt is reported.
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(config, Options(disabled: ["Intropy.Storage"]));

        Assert.Contains("Intropy.*", resolution.Sources);
        Assert.Equal(["Intropy.Storage"], resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_WithEmptyServiceName_DoesNotRegisterABlankSource()
    {
        var config = new TestConfiguration();

        var resolution = TracingSourceResolver.Resolve(config, Options());

        Assert.Equal(["Intropy.*", "Azure.*"], resolution.Sources);
    }

    [Fact]
    public void Resolve_InStrictMode_RegistersOnlyTheAllowlist()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options());

        Assert.Equal(TracingMode.Strict, resolution.Mode);
        Assert.Equal(["Intropy.*", "Orders.API"], resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenConfigurationSetsStrict_ItOverridesTheModeFromCode()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Open };

        var resolution = TracingSourceResolver.Resolve(config, Options(mode: TracingMode.Strict));

        Assert.Equal(TracingMode.Strict, resolution.Mode);
        Assert.Equal(["Intropy.*", "Orders.API"], resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenConfigurationSetsOpen_ItOverridesStrictFromCode()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options(mode: TracingMode.Open));

        Assert.Equal(TracingMode.Open, resolution.Mode);
        Assert.Contains("Azure.*", resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenConfigurationOmitsTheMode_TheModeFromCodeStands()
    {
        // The nullable binding guarantee: an absent Tracing:Mode must not reset the mode to Open.
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options(mode: null));

        Assert.Equal(TracingMode.Strict, resolution.Mode);
    }

    [Fact]
    public void Resolve_InStrictMode_IgnoresConfiguredSourcesAndReportsThem()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options(["SomeVendor.Sdk", "SomeVendor.Sdk"]));

        Assert.DoesNotContain("SomeVendor.Sdk", resolution.Sources);
        Assert.Equal(["SomeVendor.Sdk"], resolution.IgnoredConfiguredSources);
    }

    [Theory]
    [InlineData("Intropy.*")]
    [InlineData("Orders.API")]
    [InlineData("Intropy.Foo")]
    [InlineData("Intropy.Deeply.Nested")]
    [InlineData("intropy.foo")]
    public void Resolve_InStrictMode_DoesNotReportConfiguredSourcesCoveredByTheAllowlist(string configured)
    {
        // Intropy.Foo is registered by way of the Intropy.* wildcard rather than under its own name,
        // so reporting it would tell an operator to switch modes to fix something that already works.
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options([configured]));

        Assert.Empty(resolution.IgnoredConfiguredSources);
    }

    [Fact]
    public void Resolve_InStrictMode_ReportsConfiguredSourcesLeftUncoveredByADisabledWildcard()
    {
        // Intropy.* would have covered Intropy.Foo, but it was disabled, so the entry is now dead.
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(
            config,
            Options(["Intropy.Foo"], disabled: ["Intropy.*"]));

        Assert.Equal(["Intropy.Foo"], resolution.IgnoredConfiguredSources);
    }

    [Fact]
    public void Resolve_InStrictMode_ReportsSourcesNestedUnderTheServiceName()
    {
        // Strict matches the service name exactly, so anything sub-named below it is dropped. That is
        // a documented limitation, and this is the start-up warning that keeps it from being silent.
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options(["Orders.API.Repository"]));

        Assert.DoesNotContain("Orders.API.Repository", resolution.Sources);
        Assert.Equal(["Orders.API.Repository"], resolution.IgnoredConfiguredSources);
    }

    [Fact]
    public void Resolve_InStrictMode_IgnoresSourcesDeclaredInCodeWithoutReportingThem()
    {
        // Dropping these is the documented point of strict mode, so it is not worth a warning.
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };
        config.Sources.Add("MyCustomActivitySource");

        var resolution = TracingSourceResolver.Resolve(config, Options());

        Assert.Equal(["Intropy.*", "Orders.API"], resolution.Sources);
        Assert.Empty(resolution.IgnoredConfiguredSources);
    }

    [Fact]
    public void Resolve_InStrictMode_StillAppliesDisabledSources()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API", Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options(disabled: ["Intropy.*"]));

        Assert.Equal(["Orders.API"], resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_InStrictMode_WithEmptyServiceName_RegistersOnlyTheIntropyPattern()
    {
        var config = new TestConfiguration { Mode = TracingMode.Strict };

        var resolution = TracingSourceResolver.Resolve(config, Options());

        Assert.Equal(["Intropy.*"], resolution.Sources);
    }
}
