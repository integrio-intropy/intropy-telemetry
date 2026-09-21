using System.Diagnostics;
using OpenTelemetry;

namespace Intropy.Telemetry;

/// <summary>
/// Drops every span whose <see cref="ActivitySource" /> is not on the resolved source list, used in
/// <see cref="TracingMode.Strict" />.
/// </summary>
/// <remarks>
/// <para>
/// The built-in instrumentation registers its own sources directly on the tracer provider builder,
/// and the OpenTelemetry SDK offers no way to remove them again. Filtering on the way out is
/// therefore the only way to hold those spans to the same allowlist as everything else, and it has
/// the advantage of leaving trace context propagation to downstream services intact.
/// </para>
/// <para>
/// Clearing <see cref="ActivityTraceFlags.Recorded" /> is what actually drops the span: the export
/// processors ignore activities that are not recorded. This only works for processors added after
/// this one, which is why it is registered before the exporters — see
/// <see cref="TelemetryServiceCollectionExtensions" />.
/// </para>
/// </remarks>
internal sealed class StrictSourceFilterProcessor : BaseProcessor<Activity>
{
    private readonly TracingSourceMatcher _allowed;

    /// <param name="allowedSources">
    /// The resolved source list. Passing the resolved list keeps this allowlist and the registered
    /// sources in step.
    /// </param>
    public StrictSourceFilterProcessor(IEnumerable<string> allowedSources)
    {
        // Compiled: this runs on every span, and by design most of them are about to be discarded.
        _allowed = TracingSourceMatcher.Create(allowedSources, compiled: true);
    }

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (!_allowed.IsMatch(data.Source.Name))
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
