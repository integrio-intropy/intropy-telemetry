using Microsoft.Extensions.Logging;

namespace Intropy.Telemetry;

/// <summary>
/// Start-up diagnostics for trace source configuration.
/// </summary>
internal static partial class TracingLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Warning,
        Message = "Tracing:DisabledSources contains {UnmatchedCount} entry/entries matching no registered " +
                  "trace source, which therefore had no effect: {UnmatchedSources}. Registered sources: " +
                  "{RegisteredSources}. Disabled entries are matched exactly, so a wildcard source such as " +
                  "'Intropy.*' cannot be narrowed by disabling 'Intropy.Something'.")]
    public static partial void UnmatchedDisabledSources(
        ILogger logger,
        int unmatchedCount,
        string unmatchedSources,
        string registeredSources);
}
