using Microsoft.AspNetCore.Http;

namespace Intropy.Telemetry.AspNetCore;

/// <inheritdoc />
/// <remarks>
/// Adds the ASP.NET Core specific <see cref="FilterHttpRequest" /> on top of the shared options in
/// <see cref="TelemetryConfiguration" />.
/// </remarks>
public sealed class OpenTelemetryConfiguration : TelemetryConfiguration
{
    /// <summary>
    ///     Filter to determine which HTTP requests should be traced.
    ///     By default, excludes requests to /healthz endpoints.
    /// </summary>
    public Func<HttpContext, bool> FilterHttpRequest { get; set; } =
        context => !context.Request.Path.StartsWithSegments("/healthz");
}
