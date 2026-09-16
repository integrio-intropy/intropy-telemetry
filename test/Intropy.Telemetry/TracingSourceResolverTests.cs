namespace Intropy.Telemetry.Test;

public class TracingSourceResolverTests
{
    /// <summary>Concrete stand-in for the abstract shared configuration.</summary>
    private sealed class TestConfiguration : TelemetryConfiguration;

    private static TracingOptions Options(string[]? sources = null, string[]? disabled = null)
    {
        var options = new TracingOptions();

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

        var resolution = TracingSourceResolver.Resolve(config.Sources, config.ServiceName, Options());

        Assert.Equal(["Intropy.*", "Azure.*", "Orders.API"], resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_WithDisabledDefault_RemovesOnlyThatDefault()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(
            config.Sources, config.ServiceName, Options(disabled: ["Intropy.*"]));

        Assert.Equal(["Azure.*", "Orders.API"], resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_CanDisableEveryDefaultIncludingServiceName()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(
            config.Sources,
            config.ServiceName,
            Options(disabled: ["Intropy.*", "Azure.*", "Orders.API"]));

        Assert.Empty(resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_WithConfiguredSources_AppendsThem()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(
            config.Sources, config.ServiceName, Options(["SomeVendor.Sdk"]));

        Assert.Contains("SomeVendor.Sdk", resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenSourceIsBothAddedAndDisabled_DisablingWins()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };
        config.Sources.Add("MyCustomActivitySource");

        var resolution = TracingSourceResolver.Resolve(
            config.Sources,
            config.ServiceName,
            Options(["MyCustomActivitySource"], ["MyCustomActivitySource"]));

        Assert.DoesNotContain("MyCustomActivitySource", resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenCodeRemovesDefault_DefaultIsNotRegistered()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };
        config.Sources.Remove("Azure.*");

        var resolution = TracingSourceResolver.Resolve(config.Sources, config.ServiceName, Options());

        Assert.Equal(["Intropy.*", "Orders.API"], resolution.Sources);
    }

    [Fact]
    public void Resolve_MatchesDisabledSourcesCaseInsensitively()
    {
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(
            config.Sources, config.ServiceName, Options(disabled: ["intropy.*"]));

        Assert.DoesNotContain("Intropy.*", resolution.Sources);
        Assert.Empty(resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_CollapsesDuplicatesAndIgnoresBlankEntries()
    {
        var config = new TestConfiguration { ServiceName = "Intropy.*" };

        var resolution = TracingSourceResolver.Resolve(
            config.Sources, config.ServiceName, Options(["  Azure.*  ", "", "   "]));

        Assert.Equal(["Intropy.*", "Azure.*"], resolution.Sources);
    }

    [Fact]
    public void Resolve_WhenDisabledEntryMatchesNothing_ReportsItAndChangesNothing()
    {
        // A wildcard entry cannot be narrowed: Intropy.* stays registered and the attempt is reported.
        var config = new TestConfiguration { ServiceName = "Orders.API" };

        var resolution = TracingSourceResolver.Resolve(
            config.Sources, config.ServiceName, Options(disabled: ["Intropy.Storage"]));

        Assert.Contains("Intropy.*", resolution.Sources);
        Assert.Equal(["Intropy.Storage"], resolution.UnmatchedDisabledSources);
    }

    [Fact]
    public void Resolve_WithEmptyServiceName_DoesNotRegisterABlankSource()
    {
        var config = new TestConfiguration();

        var resolution = TracingSourceResolver.Resolve(config.Sources, config.ServiceName, Options());

        Assert.Equal(["Intropy.*", "Azure.*"], resolution.Sources);
    }
}
