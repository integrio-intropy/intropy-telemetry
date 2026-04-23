using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.ConsoleApp;

/// <summary>
/// <see cref="ServiceCollection"/> extension methods for configuring open telemetry
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds OpenTelemetry services to the dependency injection container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="configure">A delegate to configure the <see cref="OpenTelemetryConfiguration" /> options.</param>
    /// <remarks>
    ///     This extension method configures OpenTelemetry with the following default instrumentations:
    ///     <list type="bullet">
    ///         <item>
    ///             <description>ASP.NET Core instrumentation with exception recording</description>
    ///         </item>
    ///         <item>
    ///             <description>HTTP client instrumentation with exception recording</description>
    ///         </item>
    ///         <item>
    ///             <description>SQL Client instrumentation with exception recording</description>
    ///         </item>
    ///         <item>
    ///             <description>gRPC client instrumentation</description>
    ///         </item>
    ///         <item>
    ///             <description>OTLP exporters for traces, metrics, and logs</description>
    ///         </item>
    ///     </list>
    ///     The method automatically adds tracing sources for the configured service name, Azure.*, and Intropy.* patterns.
    /// </remarks>
    /// <example>
    ///     <code>
    /// services.AddOpenTelemetry(config =>
    /// {
    ///     config.ServiceName = "MyService.API";
    ///     config.Environment = "Production";
    ///     config.ConfigureTracing = tracing => tracing.AddSource("CustomSource");
    /// });
    /// </code>
    /// </example>
    public static void AddOpenTelemetry(this IServiceCollection services, Action<OpenTelemetryConfiguration> configure)
    {
        // Create and configure the options
        var telemetryConfig = new OpenTelemetryConfiguration();
        ArgumentNullException.ThrowIfNull(configure);
        configure(telemetryConfig);

        services.AddOpenTelemetry()
            .ConfigureResources(telemetryConfig)
            .ConfigureTracing(telemetryConfig)
            .ConfigureMetrics(telemetryConfig)
            .ConfigureLogging(telemetryConfig);
    }

    extension(OpenTelemetryBuilder builder)
    {
        private OpenTelemetryBuilder ConfigureResources(OpenTelemetryConfiguration config)
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

        private OpenTelemetryBuilder ConfigureTracing(OpenTelemetryConfiguration config)
        {
            return builder.WithTracing(tracing =>
            {
                // Add the main service source
                tracing.AddSource(config.ServiceName);

                // Add default sources
                tracing.AddSource("Azure.*")
                    .AddSource("Intropy.*")
                    .SetSampler(new AlwaysOnSampler())
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

                tracing.AddOtlpExporter();

                // Apply user configuration
                config.ConfigureTracing?.Invoke(tracing);
            });
        }

        private OpenTelemetryBuilder ConfigureMetrics(OpenTelemetryConfiguration config)
        {
            return builder.WithMetrics(metrics =>
            {
                metrics.AddHttpClientInstrumentation()
                    .AddOtlpExporter();

                config.ConfigureMetrics?.Invoke(metrics);
            });
        }

        private void ConfigureLogging(OpenTelemetryConfiguration config)
        {
            builder.WithLogging(logging =>
            {
                logging.AddOtlpExporter();

                config.ConfigureLogging?.Invoke(logging);
            });
        }
    }
}
