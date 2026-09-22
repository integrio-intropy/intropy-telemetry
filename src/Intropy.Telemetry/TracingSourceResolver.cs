namespace Intropy.Telemetry;

/// <summary>
/// The outcome of resolving the trace source list from code and configuration.
/// </summary>
/// <param name="Mode">The effective tracing mode, with configuration taking precedence over code.</param>
/// <param name="Sources">The de-duplicated source names to register with the tracer provider.</param>
/// <param name="UnmatchedDisabledSources">
/// Entries from <see cref="TracingOptions.DisabledSources" /> that matched no registered source and
/// therefore had no effect.
/// </param>
/// <param name="IgnoredConfiguredSources">
/// Entries from <see cref="TracingOptions.Sources" /> that <see cref="TracingMode.Strict" /> left out
/// of the allowlist. Sources declared in code are not reported, as dropping those is the documented
/// point of the mode rather than a surprise.
/// </param>
internal sealed record TracingSourceResolution(
    TracingMode Mode,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> UnmatchedDisabledSources,
    IReadOnlyList<string> IgnoredConfiguredSources);

/// <summary>
/// Combines the trace sources declared in code with those added and removed through configuration.
/// </summary>
internal static class TracingSourceResolver
{
    /// <summary>
    /// The only source pattern registered in <see cref="TracingMode.Strict" />, besides the service name.
    /// </summary>
    public const string StrictSourcePattern = "Intropy.*";

    /// <summary>
    /// Resolves the final set of trace sources to listen to.
    /// </summary>
    /// <param name="config">The telemetry configuration declared in code, including the built-in defaults.</param>
    /// <param name="options">Trace source settings bound from configuration.</param>
    public static TracingSourceResolution Resolve(TelemetryConfiguration config, TracingOptions options)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(options);

        var mode = options.Mode ?? config.Mode;

        // Strict ignores every declared source in favour of a fixed allowlist; open keeps the
        // established order of code sources, then the service name, then configured sources.
        IEnumerable<string> declared = mode is TracingMode.Strict
            ? [StrictSourcePattern, config.ServiceName]
            : config.Sources.Append(config.ServiceName).Concat(options.Sources);

        var sources = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in declared)
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

        // Computed last, against the list DisabledSources has already been applied to: an entry that
        // only the removed source would have covered is genuinely dead and should be reported.
        var ignored = mode is TracingMode.Strict
            ? IgnoredFrom(options.Sources, sources)
            : [];

        return new TracingSourceResolution(mode, sources, unmatched, ignored);
    }

    /// <summary>
    /// The configured sources that no registered source covers, de-duplicated.
    /// </summary>
    /// <remarks>
    /// Matched as patterns rather than compared as strings: the allowlist holds the wildcard
    /// <c>Intropy.*</c>, so a configured <c>Intropy.Foo</c> is listened to and exported even though it
    /// is not registered under that name. Reporting it would tell an operator to switch modes to fix
    /// something that already works.
    /// </remarks>
    private static List<string> IgnoredFrom(IEnumerable<string> configuredSources, IEnumerable<string> registered)
    {
        var matcher = TracingSourceMatcher.Create(registered);
        var ignored = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in configuredSources)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var name = source.Trim();

            if (!matcher.IsMatch(name) && seen.Add(name))
            {
                ignored.Add(name);
            }
        }

        return ignored;
    }
}
