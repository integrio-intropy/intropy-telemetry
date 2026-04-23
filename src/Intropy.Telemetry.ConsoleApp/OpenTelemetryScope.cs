using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.ConsoleApp;

/// <summary>
///     Manages the lifetime of OpenTelemetry providers for console applications.
/// </summary>
/// <example>
///     <code>
/// var serviceProvider = services.BuildServiceProvider();
/// using var telemetryScope = new OpenTelemetryScope(serviceProvider);
/// // Your application code here - telemetry is active
/// // Providers are automatically disposed when the scope ends
/// </code>
/// </example>
/// <param name="serviceProvider">
///     The service provider containing the registered OpenTelemetry providers.
///     Must have LoggerProvider, TracerProvider, and MeterProvider registered.
/// </param>
/// <exception cref="InvalidOperationException">
///     Thrown when any required OpenTelemetry provider is not registered in the service provider.
/// </exception>
public sealed class OpenTelemetryScope(IServiceProvider serviceProvider) : IDisposable
{
    private readonly LoggerProvider _loggerProvider = serviceProvider.GetRequiredService<LoggerProvider>();
    private readonly MeterProvider _meterProvider = serviceProvider.GetRequiredService<MeterProvider>();
    private readonly TracerProvider _tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

    /// <inheritdoc />
    public void Dispose()
    {
        _loggerProvider.Dispose();
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
    }
}
