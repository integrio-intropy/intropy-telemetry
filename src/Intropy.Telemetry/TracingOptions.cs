namespace Intropy.Telemetry;

/// <summary>
/// Trace source settings bound from the <c>Tracing</c> configuration section.
/// </summary>
/// <remarks>
/// These settings are applied on top of the sources declared in code via
/// <see cref="TelemetryConfiguration.Sources" />, and are applied last so that configuration
/// always wins. They are read once during start-up; changing them requires a restart.
/// </remarks>
/// <example>
/// <code>
/// {
///   "Tracing": {
///     "Sources": [ "MyCustomActivitySource" ],
///     "DisabledSources": [ "Azure.*" ]
///   }
/// }
/// </code>
/// </example>
public sealed class TracingOptions
{
    /// <summary>
    /// The name of the configuration section these options are bound from.
    /// </summary>
    public const string SectionName = "Tracing";

    /// <summary>
    /// Additional trace sources to listen to, appended to the sources declared in code.
    /// </summary>
    public IList<string> Sources { get; } = [];

    /// <summary>
    /// Trace sources to remove from the resolved source list, including the built-in defaults.
    /// </summary>
    public IList<string> DisabledSources { get; } = [];
}
