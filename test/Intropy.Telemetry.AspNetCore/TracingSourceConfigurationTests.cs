using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.AspNetCore.Test;

/// <summary>
/// Regression guard that this package's own AddOpenTelemetry honors the Tracing configuration
/// section. The full matrix lives in Intropy.Telemetry.Test against the shared wiring.
/// </summary>
public class TracingSourceConfigurationTests
{
    private sealed class RecordingProcessor : BaseProcessor<Activity>
    {
        private readonly List<string> _spans = [];

        public IReadOnlyList<string> Spans
        {
            get
            {
                lock (_spans)
                {
                    return [.. _spans];
                }
            }
        }

        public override void OnEnd(Activity data)
        {
            lock (_spans)
            {
                _spans.Add(data.Source.Name);
            }
        }
    }

    private static (TracerProvider Provider, RecordingProcessor Processor) BuildProvider(
        Dictionary<string, string?> settings)
    {
        var processor = new RecordingProcessor();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "TestService";
            config.Environment = "Test";
            config.ConfigureTracing = tracing => tracing.AddProcessor(processor);
        });

        return (services.BuildServiceProvider().GetRequiredService<TracerProvider>(), processor);
    }

    private static IReadOnlyList<string> RecordSpanFrom(
        string sourceName,
        Dictionary<string, string?> settings)
    {
        var (provider, processor) = BuildProvider(settings);

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        return processor.Spans;
    }

    [Fact]
    public void SourceAddedThroughConfiguration_ProducesSpans()
    {
        var sourceName = $"Custom.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(sourceName, new Dictionary<string, string?>
        {
            ["Tracing:Sources:0"] = sourceName
        });

        Assert.Contains(sourceName, spans);
    }

    [Fact]
    public void DefaultSourceDisabledThroughConfiguration_ProducesNoSpans()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(sourceName, new Dictionary<string, string?>
        {
            ["Tracing:DisabledSources:0"] = "Intropy.*"
        });

        Assert.DoesNotContain(sourceName, spans);
    }
}
