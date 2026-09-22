namespace Intropy.Telemetry;

/// <summary>
/// Controls how much of the configured tracing reaches the exporters.
/// </summary>
/// <remarks>
/// The mode is set in code via <see cref="TelemetryConfiguration.Mode" /> and can be overridden from
/// the <c>Tracing</c> configuration section via <see cref="TracingOptions.Mode" />, so that
/// configuration always wins.
/// </remarks>
public enum TracingMode
{
    /// <summary>
    /// Everything that is configured is emitted: the sources declared in
    /// <see cref="TelemetryConfiguration.Sources" />, those added through
    /// <see cref="TracingOptions.Sources" />, and the spans produced by the built-in instrumentation
    /// (ASP.NET Core, HttpClient, SqlClient, gRPC). This is the default.
    /// </summary>
    Open = 0,

    /// <summary>
    /// Only spans from <c>Intropy.*</c> and from the configured
    /// <see cref="TelemetryConfiguration.ServiceName" /> are emitted. Every other source is dropped,
    /// including the built-in instrumentation and anything added through
    /// <see cref="TelemetryConfiguration.Sources" /> or <see cref="TracingOptions.Sources" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The instrumentation stays registered so that trace context still propagates to downstream
    /// services; its spans are dropped on the way to the exporter rather than never being created.
    /// </para>
    /// <para>
    /// Because the surrounding request and client spans are dropped, the spans that do survive can
    /// appear in the backend as roots with no parent.
    /// </para>
    /// <para>
    /// <see cref="TracingOptions.DisabledSources" /> still applies, so the allowlist can be narrowed
    /// further from configuration.
    /// </para>
    /// </remarks>
    Strict = 1
}
