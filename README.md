# Intropy.Telemetry

Simple OpenTelemetry configuration for .NET applications. Hook up tracing, metrics and logging with a single method call.

## Why?

Setting up OpenTelemetry in .NET involves a fair amount of boilerplate: configuring resource attributes, wiring up instrumentations, adding OTLP exporters, enriching exceptions, filtering health checks, etc. This library packages all of that into a simple `AddOpenTelemetry` extension method so you don't have to repeat it across every project.

## Packages

| Package | Description                                     |
|---------|-------------------------------------------------|
| [Intropy.Telemetry.AspNetCore](https://www.nuget.org/packages/Intropy.Telemetry.AspNetCore) | For ASP.NET Core web applications               |
| [Intropy.Telemetry.ConsoleApp](https://www.nuget.org/packages/Intropy.Telemetry.ConsoleApp) | For console applications and background workers |
| [Intropy.Telemetry](https://www.nuget.org/packages/Intropy.Telemetry) | Shared implementation, pulled in automatically  |

## Getting Started

### ASP.NET Core

```bash
dotnet add package Intropy.Telemetry.AspNetCore
```

```csharp
using Intropy.Telemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry(config =>
{
    config.ServiceName = "MyService";
    config.Environment = builder.Environment.EnvironmentName;
});

var app = builder.Build();
app.Run();
```

### Console Application

```bash
dotnet add package Intropy.Telemetry.ConsoleApp
```

```csharp
using Intropy.Telemetry.ConsoleApp;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddOpenTelemetry(config =>
{
    config.ServiceName = "MyWorker";
    config.Environment = "Production";
});

var serviceProvider = services.BuildServiceProvider();
using var telemetryScope = new OpenTelemetryScope(serviceProvider);

// Your application code here
```

## What You Get Out of the Box

### Tracing

- ASP.NET Core requests (ASP.NET Core package only)
- Outbound HTTP calls
- SQL Client database operations
- gRPC client calls
- Automatic exception enrichment with type, message, and stack trace

### Metrics

- ASP.NET Core HTTP request metrics (ASP.NET Core package only)
- HTTP client metrics

### Logging

- OTLP export of structured logs

All telemetry is exported via [OTLP](https://opentelemetry.io/docs/specs/otel/protocol/), configured through standard OpenTelemetry environment variables:

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

## Configuration

Both packages expose the same configuration pattern via `OpenTelemetryConfiguration`:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `ServiceName` | `string` | Yes | Name of the service, e.g. `"Orders.API"` |
| `Environment` | `string` | Yes | Environment name, e.g. `"Production"` |
| `ServiceNamespace` | `string` | No | Namespace/organization |
| `ServiceVersion` | `string` | No | Defaults to the entry assembly version |
| `Sources` | `IList<string>` | No | Trace sources to listen to. Defaults to `Intropy.*` and `Azure.*`, plus `ServiceName`. Overridable from config |
| `ConfigureTracing` | `Action<TracerProviderBuilder>?` | No | Add custom instrumentations, processors or samplers |
| `ConfigureMetrics` | `Action<MeterProviderBuilder>?` | No | Add custom meters |
| `ConfigureLogging` | `Action<LoggerProviderBuilder>?` | No | Additional logging configuration |
| `ConfigureResource` | `Action<ResourceBuilder>?` | No | Add custom resource attributes |

The ASP.NET Core package also exposes:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `FilterHttpRequest` | `Func<HttpContext, bool>` | No | Filter which requests are traced. Defaults to excluding `/healthz` |

### Full Example

```csharp
builder.Services.AddOpenTelemetry(config =>
{
    config.ServiceName = "Orders.API";
    config.ServiceNamespace = "mycompany";
    config.Environment = "Production";
    config.ServiceVersion = "2.1.0";

    // Trace sources — also overridable from the "Tracing" config section
    config.Sources.Add("MyCustomActivitySource");

    config.ConfigureMetrics = metrics =>
    {
        metrics.AddMeter("MyCustomMeter");
    };

    config.ConfigureResource = resource =>
    {
        resource.AddAttributes([
            new KeyValuePair<string, object>("team", "backend")
        ]);
    };
});
```

## Controlling Trace Sources

By default, both packages listen to the configured `ServiceName` plus the `Intropy.*` and `Azure.*`
wildcards. That list lives in `config.Sources` and can be changed from `appsettings.json` without a code
change, which is the point: when a deployed service turns out to be missing a source, or one of them
floods your backend, you change configuration and restart rather than cutting a new release.

```json
{
  "Tracing": {
    "Sources": [ "MyCustomActivitySource", "SomeVendor.Sdk" ],
    "DisabledSources": [ "Azure.*" ]
  }
}
```

`Sources` adds to the list; `DisabledSources` removes from it and is applied last, so configuration always
wins over code. The same list is available in code:

```csharp
config.Sources.Add("MyCustomActivitySource");
config.Sources.Remove("Azure.*");
```

Overrides work as environment variables too:

```bash
Tracing__DisabledSources__0=Azure.*
```

### Limitations worth knowing about

**`DisabledSources` matches entries exactly, not as wildcards.** `"Azure.*"` removes the default because
that is the literal string registered by default. But with `Azure.*` registered, disabling
`Azure.Storage` does nothing — the OpenTelemetry SDK matches wildcards when deciding which sources to
listen to and offers no way to carve out an exception. Entries that match nothing are logged as a warning
at start-up rather than failing silently.

**Sources added via `ConfigureTracing` cannot be controlled from configuration.** `TracerProviderBuilder`
has no API to remove a source once added, so anything registered as:

```csharp
config.ConfigureTracing = tracing => tracing.AddSource("MyCustomActivitySource"); // not configurable
```

is fixed at compile time. Declare it in `config.Sources` instead. `ConfigureTracing` remains the right
place for processors, samplers and extra instrumentations.


## Requirements

- .NET 10.0+

## Contributing

To build and test locally:

```bash
dotnet build
dotnet test
```

## License

This project is licensed under the [MIT License](LICENSE).
