using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.AspNetCore;

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
    ///     Tracing sources default to the configured service name plus the Azure.* and Intropy.* patterns.
    ///     That list can be extended and trimmed from the <c>Tracing</c> configuration section without a
    ///     code change - see <see cref="TracingOptions" />.
    ///     <para>
    ///     Set <see cref="TelemetryConfiguration.Mode" /> to <see cref="TracingMode.Strict" /> to emit only
    ///     Intropy.* and the service name, dropping the instrumentation above along with every other source.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// services.AddOpenTelemetry(config =>
    /// {
    ///     config.ServiceName = "MyService.API";
    ///     config.Environment = "Production";
    ///     config.Sources.Add("CustomSource");
    /// });
    /// </code>
    /// </example>
    public static void AddOpenTelemetry(this IServiceCollection services, Action<OpenTelemetryConfiguration> configure)
    {
        // Create and configure the options
        var telemetryConfig = new OpenTelemetryConfiguration();
        ArgumentNullException.ThrowIfNull(configure);
        configure(telemetryConfig);

        services.AddTelemetry(
            telemetryConfig,
            tracing => tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = telemetryConfig.FilterHttpRequest;
                    options.EnrichWithException = (activity, exception) =>
                    {
                        activity.SetTag("exception.type", exception.GetType().FullName);
                        activity.SetTag("exception.message", exception.Message);
                        activity.SetTag("exception.stacktrace", exception.StackTrace);
                    };
                }
            ),
            metrics => metrics.AddAspNetCoreInstrumentation());
    }
}
