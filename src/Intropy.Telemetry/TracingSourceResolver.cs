namespace Intropy.Telemetry;

/// <summary>
/// The outcome of resolving the trace source list from code and configuration.
/// </summary>
/// <param name="Sources">The de-duplicated source names to register with the tracer provider.</param>
/// <param name="UnmatchedDisabledSources">
/// Entries from <see cref="TracingOptions.DisabledSources" /> that matched no registered source and
/// therefore had no effect.
/// </param>
internal sealed record TracingSourceResolution(
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> UnmatchedDisabledSources);

/// <summary>
/// Combines the trace sources declared in code with those added and removed through configuration.
/// </summary>
internal static class TracingSourceResolver
{
    /// <summary>
    /// Resolves the final set of trace sources to listen to.
    /// </summary>
    /// <param name="configuredSources">Sources declared in code, including the built-in defaults.</param>
    /// <param name="serviceName">The service name, registered as a source when not already present.</param>
    /// <param name="options">Trace source settings bound from configuration.</param>
    public static TracingSourceResolution Resolve(
        IEnumerable<string> configuredSources,
        string serviceName,
        TracingOptions options)
    {
        ArgumentNullException.ThrowIfNull(configuredSources);
        ArgumentNullException.ThrowIfNull(options);

        var sources = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in configuredSources.Append(serviceName).Concat(options.Sources))
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var name = source.Trim();

            if (seen.Add(name))
            {
                sources.Add(name);
            }
        }

        var unmatched = new List<string>();

        foreach (var disabled in options.DisabledSources)
        {
            if (string.IsNullOrWhiteSpace(disabled))
            {
                continue;
            }

            var name = disabled.Trim();

            if (sources.RemoveAll(source => string.Equals(source, name, StringComparison.OrdinalIgnoreCase)) == 0)
            {
                unmatched.Add(name);
            }
        }

        return new TracingSourceResolution(sources, unmatched);
    }
}
