using System.Text.RegularExpressions;

namespace Intropy.Telemetry;

/// <summary>
/// Matches trace source names against a list of source patterns, using the same <c>*</c> and
/// <c>?</c> wildcards as the OpenTelemetry SDK.
/// </summary>
/// <remarks>
/// The SDK applies these wildcards when deciding which sources to listen to but does not expose the
/// matching itself, so we reproduce it wherever we need to reason about the resolved source list -
/// dropping spans in <see cref="StrictSourceFilterProcessor" />, and working out which configured
/// entries a mode left with nothing to do in <see cref="TracingSourceResolver" />.
/// </remarks>
internal sealed class TracingSourceMatcher
{
    /// <summary>All patterns as one alternation, or <see langword="null" /> when there are none.</summary>
    private readonly Regex? _pattern;

    private TracingSourceMatcher(Regex? pattern)
    {
        _pattern = pattern;
    }

    /// <summary>
    /// Builds a matcher from a list of source patterns.
    /// </summary>
    /// <param name="patterns">Source patterns, which may contain the <c>*</c> and <c>?</c> wildcards.</param>
    /// <param name="compiled">
    /// Whether to compile the expression. Worth it for a matcher kept for the lifetime of the process
    /// and run once per span; not worth the JIT cost for one built during start-up and discarded.
    /// </param>
    public static TracingSourceMatcher Create(IEnumerable<string> patterns, bool compiled = false)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        var translated = patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => Translate(pattern.Trim()))
            .ToArray();

        if (translated.Length == 0)
        {
            return new TracingSourceMatcher(pattern: null);
        }

        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        // One alternation rather than one expression per pattern, so that matching a span costs a
        // single pass regardless of how many sources are registered.
        return new TracingSourceMatcher(new Regex($"^(?:{string.Join('|', translated)})$", options));
    }

    /// <summary>
    /// Whether <paramref name="sourceName" /> is matched by any of the patterns. Always
    /// <see langword="false" /> when there are no patterns, so an empty list matches nothing.
    /// </summary>
    public bool IsMatch(string sourceName) => _pattern?.IsMatch(sourceName) ?? false;

    /// <summary>
    /// Turns a source pattern into a regular expression fragment, leaving every character other than
    /// the two wildcards to match literally.
    /// </summary>
    private static string Translate(string pattern) =>
        Regex.Escape(pattern)
            .Replace("\\*", ".*", StringComparison.Ordinal)
            .Replace("\\?", ".", StringComparison.Ordinal);
}
