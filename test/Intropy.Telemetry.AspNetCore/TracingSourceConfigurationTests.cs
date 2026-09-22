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

        private readonly List<string> _exported = [];

        /// <summary>Every span that reached this processor, whether or not it will be exported.</summary>
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

        /// <summary>
        /// Only the spans still flagged as recorded. Strict mode drops spans by clearing that flag.
        /// </summary>
        public IReadOnlyList<string> ExportedSpans
        {
            get
            {
                lock (_spans)
                {
                    return [.. _exported];
                }
            }
        }

        public override void OnEnd(Activity data)
        {
            lock (_spans)
            {
                _spans.Add(data.Source.Name);

                if (data.Recorded)
                {
                    _exported.Add(data.Source.Name);
                }
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

    private static RecordingProcessor RecordSpanFrom(
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

        return processor;
    }

    [Fact]
    public void SourceAddedThroughConfiguration_ProducesSpans()
    {
        var sourceName = $"Custom.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(sourceName, new Dictionary<string, string?>
        {
            ["Tracing:Sources:0"] = sourceName
        });

        Assert.Contains(sourceName, spans.Spans);
    }

    [Fact]
    public void DefaultSourceDisabledThroughConfiguration_ProducesNoSpans()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(sourceName, new Dictionary<string, string?>
        {
            ["Tracing:DisabledSources:0"] = "Intropy.*"
        });

        Assert.DoesNotContain(sourceName, spans.Spans);
    }

    [Fact]
    public void StrictModeThroughConfiguration_DropsInstrumentationSpans()
    {
        var recorded = RecordSpanFrom("System.Net.Http", new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict"
        });

        Assert.Contains("System.Net.Http", recorded.Spans);
        Assert.DoesNotContain("System.Net.Http", recorded.ExportedSpans);
    }

    [Fact]
    public void StrictModeThroughConfiguration_StillExportsIntropySpans()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";

        var recorded = RecordSpanFrom(sourceName, new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict"
        });

        Assert.Contains(sourceName, recorded.ExportedSpans);
    }
}
