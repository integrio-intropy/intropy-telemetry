using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.Test;

/// <summary>
/// <see cref="TracingMode" /> is an enum but is written as a string in appsettings.json and in
/// environment variables, so these pin down what the configuration binder accepts.
/// </summary>
public class TracingModeBindingTests
{
    /// <summary>Concrete stand-in for the abstract shared configuration.</summary>
    private sealed class TestConfiguration : TelemetryConfiguration;

    private static TracerProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();

        services.AddSingleton(configuration);
        services.AddTelemetry(new TestConfiguration { ServiceName = "TestService", Environment = "Test" });

        return services.BuildServiceProvider().GetRequiredService<TracerProvider>();
    }

    private static TracingMode? BoundMode(IConfiguration configuration)
    {
        var options = new TracingOptions();

        configuration.GetSection(TracingOptions.SectionName).Bind(options);

        return options.Mode;
    }

    private static IConfiguration InMemory(string? mode) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Tracing:Mode"] = mode })
            .Build();

    [Theory]
    [InlineData("Strict", TracingMode.Strict)]
    [InlineData("strict", TracingMode.Strict)]
    [InlineData("STRICT", TracingMode.Strict)]
    [InlineData("Open", TracingMode.Open)]
    [InlineData("open", TracingMode.Open)]
    public void ModeNames_BindCaseInsensitively(string value, TracingMode expected)
    {
        Assert.Equal(expected, BoundMode(InMemory(value)));
    }

    [Fact]
    public void ModeAbsentOrBlank_LeavesTheModeUnset()
    {
        Assert.Null(BoundMode(InMemory(mode: null)));
        Assert.Null(BoundMode(InMemory("")));
        Assert.Null(BoundMode(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void ModeSetThroughAnEnvironmentVariable_IsHonored()
    {
        // The documented Tracing__Mode=Strict form: the provider turns the double underscore into the
        // section separator, and the value binds the same way it does from appsettings.json.
        Environment.SetEnvironmentVariable("Tracing__Mode", "Strict");

        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();

            Assert.Equal(TracingMode.Strict, BoundMode(configuration));
        }
        finally
        {
            Environment.SetEnvironmentVariable("Tracing__Mode", null);
        }
    }

    [Fact]
    public void UnrecognizedMode_FailsLoudlyWhenTheTracerProviderIsBuilt()
    {
        // A typo must not quietly fall back to Open and leave a service emitting more than intended.
        var exception = Assert.Throws<InvalidOperationException>(() => BuildProvider(InMemory("Stirct")));

        Assert.Contains("Tracing:Mode", exception.Message, StringComparison.Ordinal);
    }
}
