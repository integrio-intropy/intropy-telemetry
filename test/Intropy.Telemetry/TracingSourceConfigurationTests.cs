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
        /// Only the spans still flagged as recorded, which is what the exporters act on. Strict mode
        /// drops spans by clearing that flag, so this is the list that reflects what leaves the process.
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

    private static (IReadOnlyList<string> All, IReadOnlyList<string> Exported) RecordSpanFrom(
        string sourceName,
        Dictionary<string, string?>? settings,
        Action<TelemetryConfiguration>? extraConfiguration = null)
    {
        var (provider, processor) = BuildProvider(settings, extraConfiguration);

        using (provider)
        {
            using var source = new ActivitySource(sourceName);
            using (source.StartActivity("work"))
            {
            }
        }

        return (processor.Spans, processor.ExportedSpans);
    }

    [Fact]
    public void StrictModeFromCode_ExportsIntropySpans()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(sourceName, settings: null, config => config.Mode = TracingMode.Strict);

        Assert.Contains(sourceName, spans.Exported);
    }

    [Fact]
    public void StrictModeFromCode_ExportsTheServiceNameSource()
    {
        var spans = RecordSpanFrom("TestService", settings: null, config => config.Mode = TracingMode.Strict);

        Assert.Contains("TestService", spans.Exported);
    }

    [Fact]
    public void StrictMode_DropsInstrumentationSpans()
    {
        // The decisive case. System.Net.Http is registered by AddHttpClientInstrumentation, not by our
        // source list, and the SDK offers no way to unregister it - so the span must reach the
        // processor chain and be dropped there rather than never being listened to.
        var spans = RecordSpanFrom(
            "System.Net.Http",
            settings: null,
            config => config.Mode = TracingMode.Strict);

        Assert.Contains("System.Net.Http", spans.All);
        Assert.DoesNotContain("System.Net.Http", spans.Exported);
    }

    [Fact]
    public void OpenMode_ExportsInstrumentationSpans()
    {
        var spans = RecordSpanFrom("System.Net.Http", settings: null);

        Assert.Contains("System.Net.Http", spans.Exported);
    }

    [Fact]
    public void StrictMode_DropsSourcesAddedThroughConfiguration()
    {
        var sourceName = $"Custom.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(sourceName, new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict",
            ["Tracing:Sources:0"] = sourceName
        });

        Assert.DoesNotContain(sourceName, spans.Exported);
    }

    [Fact]
    public void StrictModeFromConfiguration_BindsCaseInsensitivelyAndOverridesCode()
    {
        var sourceName = $"Custom.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(
            sourceName,
            new Dictionary<string, string?> { ["Tracing:Mode"] = "strict" },
            config =>
            {
                config.Mode = TracingMode.Open;
                config.Sources.Add(sourceName);
            });

        Assert.DoesNotContain(sourceName, spans.Exported);
    }

    [Fact]
    public void ModeAbsentFromConfiguration_LeavesTheModeFromCodeInPlace()
    {
        var sourceName = $"Custom.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(
            sourceName,
            new Dictionary<string, string?> { ["Tracing:Sources:0"] = sourceName },
            config => config.Mode = TracingMode.Strict);

        Assert.DoesNotContain(sourceName, spans.Exported);
    }

    [Fact]
    public void StrictMode_StillHonorsDisabledSources()
    {
        var sourceName = $"Intropy.{Guid.NewGuid():N}";

        var spans = RecordSpanFrom(
            sourceName,
            new Dictionary<string, string?> { ["Tracing:DisabledSources:0"] = "Intropy.*" },
            config => config.Mode = TracingMode.Strict);

        Assert.DoesNotContain(sourceName, spans.All);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Warnings { get; } = [];

        public List<string> Information { get; } = [];

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
                var messages = logLevel switch
                {
                    LogLevel.Warning => provider.Warnings,
                    LogLevel.Information => provider.Information,
                    _ => null
                };

                if (messages is null)
                {
                    return;
                }

                lock (messages)
                {
                    messages.Add(formatter(state, exception));
                }
            }
        }
    }

    private static CapturingLoggerProvider BuildProviderWithLogging(Dictionary<string, string?> settings)
    {
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddProvider(loggerProvider));
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        services.AddTelemetry(new TestConfiguration { ServiceName = "TestService", Environment = "Test" });

        // Resolving the provider is what runs the start-up diagnostics.
        using var provider = services.BuildServiceProvider().GetRequiredService<TracerProvider>();

        return loggerProvider;
    }

    [Fact]
    public void DisabledSourceMatchingNothing_LogsAWarning()
    {
        // Intropy.* is registered as a wildcard and cannot be narrowed, so this entry has no effect.
        var loggerProvider = BuildProviderWithLogging(new Dictionary<string, string?>
        {
            ["Tracing:DisabledSources:0"] = "Intropy.Storage"
        });

        var warning = Assert.Single(loggerProvider.Warnings);
        Assert.Contains("Intropy.Storage", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void StartUp_LogsTheModeAndTheRegisteredSources()
    {
        var loggerProvider = BuildProviderWithLogging(new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict"
        });

        var message = Assert.Single(
            loggerProvider.Information,
            entry => entry.StartsWith("Tracing mode is", StringComparison.Ordinal));

        Assert.Contains("Strict", message, StringComparison.Ordinal);
        Assert.Contains("Intropy.*", message, StringComparison.Ordinal);
        Assert.Contains("TestService", message, StringComparison.Ordinal);
    }

    [Fact]
    public void StrictMode_LogsWhatItDrops()
    {
        var loggerProvider = BuildProviderWithLogging(new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict"
        });

        Assert.Contains(
            loggerProvider.Information,
            entry => entry.Contains("dropped before export", StringComparison.Ordinal));
    }

    [Fact]
    public void OpenMode_LogsTheModeWithoutTheStrictExplanation()
    {
        // Every consumer that never touched this feature boots in Open mode, so it must not be told
        // about behaviour that only applies to Strict.
        var loggerProvider = BuildProviderWithLogging([]);

        var message = Assert.Single(loggerProvider.Information);
        Assert.Contains("Open", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Strict", message, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredSourceCoveredByTheStrictAllowlist_IsExportedAndLogsNoWarning()
    {
        // End to end: Intropy.Foo is listened to via the Intropy.* wildcard, so warning that the entry
        // had no effect - and telling the operator to switch to Open mode - would be plainly wrong.
        var settings = new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict",
            ["Tracing:Sources:0"] = "Intropy.Foo"
        };

        var spans = RecordSpanFrom("Intropy.Foo", settings);

        Assert.Contains("Intropy.Foo", spans.Exported);
        Assert.Empty(BuildProviderWithLogging(settings).Warnings);
    }

    [Fact]
    public void ConfiguredSourcesIgnoredByStrictMode_LogAWarning()
    {
        var loggerProvider = BuildProviderWithLogging(new Dictionary<string, string?>
        {
            ["Tracing:Mode"] = "Strict",
            ["Tracing:Sources:0"] = "SomeVendor.Sdk"
        });

        var warning = Assert.Single(loggerProvider.Warnings);
        Assert.Contains("SomeVendor.Sdk", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfiguredSourcesInOpenMode_LogNoWarning()
    {
        var loggerProvider = BuildProviderWithLogging(new Dictionary<string, string?>
        {
            ["Tracing:Sources:0"] = "SomeVendor.Sdk"
        });

        Assert.Empty(loggerProvider.Warnings);
    }
}
