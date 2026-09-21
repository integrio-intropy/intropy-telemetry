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

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Tracing mode is {Mode}. Registered trace sources: {RegisteredSources}.")]
    public static partial void ResolvedTracingMode(ILogger logger, TracingMode mode, string registeredSources);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Strict tracing mode: spans from every other source are dropped before export, " +
                  "including the built-in ASP.NET Core, HttpClient, SqlClient and gRPC instrumentation. " +
                  "Spans that do survive can appear in the backend as roots with no parent.")]
    public static partial void StrictModeDropsOtherSources(ILogger logger);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Tracing:Sources contains {IgnoredCount} entry/entries that had no effect because " +
                  "Tracing mode is Strict: {IgnoredSources}. Strict mode registers a fixed allowlist of " +
                  "'Intropy.*' plus the service name. Switch to Open mode to listen to these sources.")]
    public static partial void IgnoredConfiguredSources(ILogger logger, int ignoredCount, string ignoredSources);
}
