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
| `Mode` | `TracingMode` | No | `Open` emits everything configured; `Strict` emits only `Intropy.*` and `ServiceName`. Overridable from config |
| `Sources` | `IList<string>` | No | Trace sources to listen to. Defaults to `Intropy.*` and `Azure.*`, plus `ServiceName`. Overridable from config. Ignored in `Strict` mode |
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
    "Mode": "Open",
    "Sources": [ "MyCustomActivitySource", "SomeVendor.Sdk" ],
    "DisabledSources": [ "Azure.*" ]
  }
}
```

`Sources` adds to the list; `DisabledSources` removes from it and is applied last, so configuration always
wins over code. `Mode` is covered below. The same list is available in code:

```csharp
config.Sources.Add("MyCustomActivitySource");
config.Sources.Remove("Azure.*");
```

Overrides work as environment variables too:

```bash
Tracing__DisabledSources__0=Azure.*
```

### Tracing modes

`Mode` decides how much of that configuration actually leaves the process. It defaults to `Open`, so
upgrading changes nothing for an existing service.

| Mode | What is emitted |
|------|-----------------|
| `Open` | Everything configured: `config.Sources`, `Tracing:Sources`, and the built-in ASP.NET Core, HttpClient, SqlClient and gRPC instrumentation |
| `Strict` | Only `Intropy.*` and the exact `ServiceName`. Everything else is dropped, including all built-in instrumentation |

`Strict` is the switch to reach for when a service should report its own work and nothing else —
no vendor SDK chatter, no per-query SQL spans, no cost for traces nobody reads. It is an allowlist, so
it needs no knowledge of what a dependency might start emitting after its next upgrade.

```json
{
  "Tracing": {
    "Mode": "Strict"
  }
}
```

In code, and as an environment variable:

```csharp
config.Mode = TracingMode.Strict;
```

```bash
Tracing__Mode=Strict
```

Configuration wins over code, as it does for `Sources`. The value is matched case-insensitively, so
`"strict"` and `"Strict"` both work, and an unrecognised value fails at start-up with a
`Failed to convert configuration value … at 'Tracing:Mode'` error rather than quietly falling back to
`Open`. The mode and the resolved source list are logged at `Information` on start-up, which is the
first place to look when spans go missing.

The instrumentation stays registered in `Strict` mode and its spans are dropped on the way to the
exporter, rather than never being created. That is deliberate: it keeps `traceparent` propagation to
downstream services working, so a strict service does not break the traces of the services it calls.

### Limitations worth knowing about

**`Strict` matches `ServiceName` exactly.** With `ServiceName = "Orders.API"`, a source named
`Orders.API.Repository` is dropped — only `Intropy.*` is treated as a wildcard. There is no way to add
it back, because `Tracing:Sources` is ignored in this mode. Either name the `ActivitySource` exactly
`ServiceName`, prefix it with `Intropy.`, or stay on `Open`. Entries in `Tracing:Sources` that this
affects are logged as a warning at start-up; sources added in code are not, so check the registered
source list in the start-up log if spans are missing.

**`Strict` drops the spans that surround yours.** With the ASP.NET Core and HttpClient spans gone, the
spans that survive can appear in your backend as roots with no parent. This is the cost of the mode,
not a bug.

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
