using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry;

/// <summary>
/// The OpenTelemetry wiring shared by every Intropy.Telemetry package.
/// </summary>
internal static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers resources, tracing, metrics and logging with the defaults shared by all packages.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="config">The resolved telemetry configuration.</param>
    /// <param name="platformTracing">
    /// Instrumentation specific to the calling package, applied after the shared instrumentation and
    /// before <see cref="TelemetryConfiguration.ConfigureTracing" />.
    /// </param>
    /// <param name="platformMetrics">
    /// Metrics instrumentation specific to the calling package, applied after the shared
    /// instrumentation and before <see cref="TelemetryConfiguration.ConfigureMetrics" />.
    /// </param>
    public static void AddTelemetry(
        this IServiceCollection services,
        TelemetryConfiguration config,
        Action<TracerProviderBuilder>? platformTracing = null,
        Action<MeterProviderBuilder>? platformMetrics = null)
    {
        services.AddTracingSources(config);

        services.AddOpenTelemetry()
            .ConfigureResources(config)
            .ConfigureTracing(config, platformTracing)
            .ConfigureMetrics(config, platformMetrics)
            .ConfigureLogging(config);
    }

    extension(OpenTelemetryBuilder builder)
    {
        private OpenTelemetryBuilder ConfigureResources(TelemetryConfiguration config)
        {
            return builder.ConfigureResource(resource =>
            {
                resource.AddService(config.ServiceName,
                        config.ServiceNamespace,
                        Assembly.GetExecutingAssembly().GetName().Version!.ToString()
                    )
                    .AddAttributes([
                        new KeyValuePair<string, object>("environment", config.Environment),
                        new KeyValuePair<string, object>("service.version", config.ServiceVersion)
                    ]);

                // Apply user configuration
                config.ConfigureResource?.Invoke(resource);
            });
        }

        private OpenTelemetryBuilder ConfigureTracing(
            TelemetryConfiguration config,
            Action<TracerProviderBuilder>? platformTracing)
        {
            return builder.WithTracing(tracing =>
            {
                // Trace sources are registered separately, see AddTracingSources.
                tracing.SetSampler(new AlwaysOnSampler())
                    .AddSqlClientInstrumentation(options => { options.RecordException = true; })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.FilterHttpRequestMessage =
                            _ => Activity.Current?.Parent?.Source.Name != "Azure.Core.Http";
                        options.RecordException = true;
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("error.type", exception.GetType().FullName);
                            activity.SetTag("error.msg", exception.Message);
                        };
                    })
                    .AddGrpcClientInstrumentation(options => { options.SuppressDownstreamInstrumentation = false; });

                // Apply platform specific configuration
                platformTracing?.Invoke(tracing);

                tracing.AddOtlpExporter();

                // Apply user configuration
                config.ConfigureTracing?.Invoke(tracing);
            });
        }

        private OpenTelemetryBuilder ConfigureMetrics(
            TelemetryConfiguration config,
            Action<MeterProviderBuilder>? platformMetrics)
        {
            return builder.WithMetrics(metrics =>
            {
                metrics.AddHttpClientInstrumentation();

                // Apply platform specific configuration
                platformMetrics?.Invoke(metrics);

                metrics.AddOtlpExporter();

                // Apply user configuration
                config.ConfigureMetrics?.Invoke(metrics);
            });
        }

        private void ConfigureLogging(TelemetryConfiguration config)
        {
            builder.WithLogging(logging =>
            {
                logging.AddOtlpExporter();

                // Apply user configuration
                config.ConfigureLogging?.Invoke(logging);
            });
        }
    }

    /// <summary>
    /// Registers the configuration-bound trace source settings and applies the resolved source list
    /// to the tracer provider.
    /// </summary>
    private static void AddTracingSources(this IServiceCollection services, TelemetryConfiguration config)
    {
        services.AddOptions<TracingOptions>();

        services.AddSingleton<IConfigureOptions<TracingOptions>>(serviceProvider =>
            new ConfigureOptions<TracingOptions>(options =>
                serviceProvider.GetService<IConfiguration>()
                    ?.GetSection(TracingOptions.SectionName)
                    .Bind(options)));

        services.ConfigureOpenTelemetryTracerProvider((serviceProvider, tracing) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TracingOptions>>().Value;
            var resolution = TracingSourceResolver.Resolve(config.Sources, config.ServiceName, options);

            tracing.AddSource([.. resolution.Sources]);

            if (resolution.UnmatchedDisabledSources.Count == 0)
            {
                return;
            }

            var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("Intropy.Telemetry");

            if (logger is not null)
            {
                TracingLog.UnmatchedDisabledSources(
                    logger,
                    resolution.UnmatchedDisabledSources.Count,
                    string.Join(", ", resolution.UnmatchedDisabledSources),
                    string.Join(", ", resolution.Sources));
            }
        });
    }
}
