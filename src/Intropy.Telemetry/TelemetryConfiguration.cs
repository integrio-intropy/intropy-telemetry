using System.Reflection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry;

/// <summary>
/// Shared configuration options for OpenTelemetry setup, including service identification and
/// customization callbacks. Each package exposes its own <c>OpenTelemetryConfiguration</c> deriving
/// from this type.
/// </summary>
/// <remarks>
/// This class provides configuration for all aspects of OpenTelemetry including service resource attributes,
/// tracing, metrics, and logging. The <see cref="ServiceName" /> and <see cref="Environment" /> properties are
/// required, while other properties provide optional customization capabilities.
/// </remarks>
/// <example>
/// <code>
/// var config = new OpenTelemetryConfiguration // your package's type
/// {
///     ServiceName = "orders-b2b-api",
///     ServiceNamespace = "yourorganization",
///     Environment = "Production",
///     ServiceVersion = "2.1.0",
///     Sources = { "OrderService.Custom" },
///     ConfigureResource = resource => resource.AddAttributes([
///         new KeyValuePair&lt;string, object&gt;("team", "orders")
///     ])
/// };
/// </code>
/// </example>
public abstract class TelemetryConfiguration
{
    /// <summary>
    /// The name of the service, e.g. orders-b2b-api
    /// Recommended to use the same name as the ActivitySource name.
    /// </summary>
    public string ServiceName { get; set; } = "";

    /// <summary>
    /// The namespace of the service, e.g. yourorganization
    /// </summary>
    public string ServiceNamespace { get; set; } = "";

    /// <summary>
    /// The version of the service
    /// </summary>
    public string ServiceVersion { get; set; } = Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    /// <summary>
    /// The environment that the service is running in
    /// </summary>
    public string Environment { get; set; } = "";

    /// <summary>
    /// How much of the configured tracing is actually emitted. Defaults to
    /// <see cref="TracingMode.Open" />.
    /// </summary>
    /// <remarks>
    /// <see cref="TracingMode.Strict" /> replaces <see cref="Sources" /> with a fixed allowlist of
    /// <c>Intropy.*</c> plus <see cref="ServiceName" />, and drops the spans produced by the built-in
    /// instrumentation. Overridable from the <c>Tracing</c> configuration section - see
    /// <see cref="TracingOptions.Mode" />.
    /// </remarks>
    public TracingMode Mode { get; set; } = TracingMode.Open;

    /// <summary>
    /// The trace sources to listen to, pre-seeded with the built-in defaults.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add an entry to listen to another source, or remove one to opt out of a default. The
    /// configured <see cref="ServiceName" /> is appended automatically. Entries may contain the
    /// <c>*</c> and <c>?</c> wildcards supported by the OpenTelemetry SDK.
    /// </para>
    /// <para>
    /// This list is ignored entirely in <see cref="TracingMode.Strict" />, which registers a fixed
    /// allowlist instead.
    /// </para>
    /// <para>
    /// This list can be extended and trimmed from the <c>Tracing</c> configuration section without
    /// a code change — see <see cref="TracingOptions" />. Sources added directly to the
    /// <see cref="TracerProviderBuilder" /> inside <see cref="ConfigureTracing" /> cannot be, as
    /// the OpenTelemetry SDK offers no way to remove a source from the builder. Prefer this list
    /// for anything that should stay configurable after deployment.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// config.Sources.Add("MyCustomActivitySource");
    /// config.Sources.Remove("Azure.*");
    /// </code>
    /// </example>
    public IList<string> Sources { get; } = ["Intropy.*", "Azure.*"];

    /// <summary>
    /// Optional action to configure additional tracing options
    /// </summary>
    public Action<TracerProviderBuilder>? ConfigureTracing { get; set; }

    /// <summary>
    /// Optional action to configure additional resource options
    /// </summary>
    public Action<ResourceBuilder>? ConfigureResource { get; set; }

    /// <summary>
    /// Optional action to configure additional metrics options
    /// </summary>
    public Action<MeterProviderBuilder>? ConfigureMetrics { get; set; }

    /// <summary>
    /// Optional action to configure additional logging options
    /// </summary>
    public Action<LoggerProviderBuilder>? ConfigureLogging { get; set; }
}
