namespace Intropy.Telemetry;

/// <summary>
/// Trace source settings bound from the <c>Tracing</c> configuration section.
/// </summary>
/// <remarks>
/// These settings are applied on top of the tracing declared in code via
/// <see cref="TelemetryConfiguration.Sources" /> and <see cref="TelemetryConfiguration.Mode" />, and
/// are applied last so that configuration always wins. They are read once during start-up; changing
/// them requires a restart.
/// </remarks>
/// <example>
/// <code>
/// {
///   "Tracing": {
///     "Mode": "Strict",
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
    /// Overrides <see cref="TelemetryConfiguration.Mode" /> when set.
    /// </summary>
    /// <remarks>
    /// Left <see langword="null" /> when the section has no <c>Mode</c> key, so that an absent
    /// setting leaves the mode chosen in code alone rather than resetting it to
    /// <see cref="TracingMode.Open" />. Values bind case-insensitively, so both <c>"Strict"</c> and
    /// <c>"strict"</c> work.
    /// </remarks>
    public TracingMode? Mode { get; set; }

    /// <summary>
    /// Additional trace sources to listen to, appended to the sources declared in code.
    /// </summary>
    /// <remarks>
    /// Ignored in <see cref="TracingMode.Strict" />, which registers a fixed allowlist. Entries left
    /// out for that reason are logged as a warning at start-up.
    /// </remarks>
    public IList<string> Sources { get; } = [];

    /// <summary>
    /// Trace sources to remove from the resolved source list, including the built-in defaults.
    /// </summary>
    /// <remarks>
    /// Applies in both modes, so the strict allowlist can be narrowed further from configuration.
    /// </remarks>
    public IList<string> DisabledSources { get; } = [];
}
