using System.Reflection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.ConsoleApp;

/// <summary>
///     Configuration options for OpenTelemetry setup including service identification and customization callbacks.
/// </summary>
/// <remarks>
///     This class provides configuration for all aspects of OpenTelemetry including service resource attributes,
///     tracing, metrics, and logging. The <see cref="ServiceName" /> and <see cref="Environment" /> properties are
///     required, while other properties provide optional customization capabilities.
/// </remarks>
/// <example>
///     <code>
/// var config = new OpenTelemetryConfiguration
/// {
///     ServiceName = "orders-b2b-api",
///     ServiceNamespace = "yourorganization",
///     Environment = "Production",
///     ServiceVersion = "2.1.0",
///     ConfigureTracing = tracing => tracing.AddSource("OrderService.Custom"),
///     ConfigureResource = resource => resource.AddAttributes([
///         new KeyValuePair&lt;string, object&gt;("team", "orders")
///     ])
/// };
/// </code>
/// </example>
public class OpenTelemetryConfiguration
{
    /// <summary>
    ///     The name of the service, e.g. orders-b2b-api
    ///     Recommended to use the same name as the ActivitySource name.
    /// </summary>
    public string ServiceName { get; set; } = "";

    /// <summary>
    ///     The namespace of the service, e.g. yourorganization
    /// </summary>
    public string ServiceNamespace { get; set; } = "";

    /// <summary>
    ///     The version of the service
    /// </summary>
    public string ServiceVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    /// <summary>
    ///     The environment that the service is running in
    /// </summary>
    public string Environment { get; set; } = "";

    /// <summary>
    ///     Optional action to configure additional tracing options
    /// </summary>
    public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }

    /// <summary>
    ///     Optional action to configure additional resource options
    /// </summary>
    public Action<ResourceBuilder>? ConfigureResource { get; set; }

    /// <summary>
    ///     Optional action to configure additional metrics options
    /// </summary>
    public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }

    /// <summary>
    ///     Optional action to configure additional logging options
    /// </summary>
    public Action<LoggerProviderBuilder>? ConfigureLogging { get; set; }
}
