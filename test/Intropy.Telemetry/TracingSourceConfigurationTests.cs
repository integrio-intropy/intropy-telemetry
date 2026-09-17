using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.Test;

public class TracingSourceConfigurationTests
{
    /// <summary>Concrete stand-in for the abstract shared configuration.</summary>
    private sealed class TestConfiguration : TelemetryConfiguration;

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
        Dictionary<string, string?>? settings,
        Action<TelemetryConfiguration>? extraConfiguration = null)
    {
        var processor = new RecordingProcessor();
        var services = new ServiceCollection();

        if (settings is not null)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            services.AddSingleton<IConfiguration>(configuration);
        }

        var config = new TestConfiguration
        {
            ServiceName = "TestService",
            Environment = "Test",
            ConfigureTracing = tracing => tracing.AddProcessor(processor)
        };

        extraConfiguration?.Invoke(config);
        services.AddTelemetry(config);

        // Resolving the provider is what builds it and applies the resolved source list.
        var provider = services.BuildServiceProvider().GetRequiredService<TracerProvider>();

        return (provider, processor);
    }

    [Fact]
    public void SourceAddedThroughConfiguration_ProducesSpans()
    {
        var sourceName = $"Custom.{Guid.NewGuid():N}";
        var (provider, processor) = BuildProvider(new Dictionary<string, string?>
        {
            ["Tracing:Sources:0"] = sourceName
        });

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        Assert.Contains(sourceName, processor.Spans);
    }

    [Fact]
    public void SourceNotRegisteredAnywhere_ProducesNoSpans()
    {
        var sourceName = $"Unregistered.{Guid.NewGuid():N}";
        var (provider, processor) = BuildProvider(settings: null);

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        Assert.DoesNotContain(sourceName, processor.Spans);
    }

    [Fact]
    public void DefaultSourceDisabledThroughConfiguration_ProducesNoSpans()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";
        var (provider, processor) = BuildProvider(new Dictionary<string, string?>
        {
            ["Tracing:DisabledSources:0"] = "Intropy.*"
        });

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        Assert.DoesNotContain(sourceName, processor.Spans);
    }

    [Fact]
    public void DefaultSource_ProducesSpansWhenNotDisabled()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";
        var (provider, processor) = BuildProvider(new Dictionary<string, string?>());

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        Assert.Contains(sourceName, processor.Spans);
    }

    [Fact]
    public void WithoutAnyConfigurationRegistered_DefaultsStillApply()
    {
        // The console-app path: no IConfiguration in the container at all.
        var sourceName = $"Intropy.{Guid.NewGuid():N}";
        var (provider, processor) = BuildProvider(settings: null);

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        Assert.Contains(sourceName, processor.Spans);
    }

    [Fact]
    public void SourceRemovedInCode_ProducesNoSpans()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";
        var (provider, processor) = BuildProvider(
            settings: null,
            config => config.Sources.Remove("Intropy.*"));

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        Assert.DoesNotContain(sourceName, processor.Spans);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Warnings { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel != LogLevel.Warning)
                {
                    return;
                }

                lock (provider.Warnings)
                {
                    provider.Warnings.Add(formatter(state, exception));
                }
            }
        }
    }

    [Fact]
    public void DisabledSourceMatchingNothing_LogsAWarning()
    {
        // Intropy.* is registered as a wildcard and cannot be narrowed, so this entry has no effect.
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddProvider(loggerProvider));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tracing:DisabledSources:0"] = "Intropy.Storage"
            })
            .Build());

        services.AddTelemetry(new TestConfiguration { ServiceName = "TestService", Environment = "Test" });

        using var provider = services.BuildServiceProvider().GetRequiredService<TracerProvider>();

        var warning = Assert.Single(loggerProvider.Warnings);
        Assert.Contains("Intropy.Storage", warning, StringComparison.Ordinal);
    }
}
