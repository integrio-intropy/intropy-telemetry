# Intropy.Telemetry

Simple OpenTelemetry configuration for .NET applications. Hook up tracing, metrics and logging with a single method call.

## Overview

This library provides two NuGet packages that wrap the OpenTelemetry SDK with sensible defaults:

- **Intropy.Telemetry.AspNetCore** — for ASP.NET Core web applications
- **Intropy.Telemetry.ConsoleApp** — for console applications and background workers

Both are thin platform-specific layers over a third package, **Intropy.Telemetry**, which holds the shared
configuration and wiring. It is pulled in automatically as a dependency, you never reference it directly.

Both packages configure tracing, metrics, and logging with OTLP export out of the box. The ASP.NET Core package additionally includes HTTP request instrumentation, health check filtering, and ASP.NET Core metrics.

## Installation

### ASP.NET Core

```bash
dotnet add package Intropy.Telemetry.AspNetCore
```

### Console Application

```bash
dotnet add package Intropy.Telemetry.ConsoleApp
```

## Quick Start

### ASP.NET Core

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

// Your application code here — telemetry is active
// Providers are automatically flushed and disposed when the scope ends
```

`OpenTelemetryScope` manages the lifetime of the `TracerProvider`, `MeterProvider`, and `LoggerProvider`. Disposing it ensures all buffered telemetry is flushed before the process exits.

## Configuration Reference

### Shared Properties

Both packages use `OpenTelemetryConfiguration` with the same base properties:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `ServiceName` | `string` | Yes | `""` | Name of the service, e.g. `"orders-b2b-api"`. Recommended to match your `ActivitySource` name. |
| `Environment` | `string` | Yes | `""` | Environment name, e.g. `"Production"`, `"Staging"` |
| `ServiceNamespace` | `string` | No | `""` | Namespace or organization, e.g. `"mycompany"` |
| `ServiceVersion` | `string` | No | Assembly version | Service version. Defaults to the executing assembly's version. |
| `Sources` | `IList<string>` | No | `Intropy.*`, `Azure.*` | Trace sources to listen to, plus `ServiceName`. Extendable and trimmable from the `Tracing` config section |
| `ConfigureTracing` | `Action<TracerProviderBuilder>?` | No | `null` | Callback to add custom instrumentations, processors or samplers |
| `ConfigureMetrics` | `Action<MeterProviderBuilder>?` | No | `null` | Callback to add custom meters |
| `ConfigureLogging` | `Action<LoggerProviderBuilder>?` | No | `null` | Callback for additional logging configuration |
| `ConfigureResource` | `Action<ResourceBuilder>?` | No | `null` | Callback to add custom resource attributes |

### ASP.NET Core Only

The ASP.NET Core package adds one additional property:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `FilterHttpRequest` | `Func<HttpContext, bool>` | No | Excludes `/healthz` | Predicate that controls which HTTP requests are traced. Return `true` to trace, `false` to skip. |

## Built-in Instrumentation

### Intropy.Telemetry.AspNetCore

| Category | Instrumentation |
|----------|----------------|
| Tracing | ASP.NET Core requests with exception enrichment |
| Tracing | Outbound HTTP calls with exception enrichment |
| Tracing | SQL Client database operations |
| Tracing | gRPC client calls |
| Metrics | ASP.NET Core HTTP request metrics |
| Metrics | HTTP client metrics |
| Logging | OTLP export of structured logs |

### Intropy.Telemetry.ConsoleApp

| Category | Instrumentation |
|----------|----------------|
| Tracing | Outbound HTTP calls with exception enrichment |
| Tracing | SQL Client database operations |
| Tracing | gRPC client calls |
| Metrics | HTTP client metrics |
| Logging | OTLP export of structured logs |

Both packages register trace sources for the `Azure.*` and `Intropy.*` patterns by default, along with the
configured `ServiceName`. See [Controlling Trace Sources](#controlling-trace-sources) to change that list
from configuration.

## Examples

### Full Configuration (ASP.NET Core)

```csharp
using Intropy.Telemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry(config =>
{
    config.ServiceName = "Orders.API";
    config.ServiceNamespace = "mycompany";
    config.Environment = "Production";
    config.ServiceVersion = "2.1.0";

    // Add custom trace sources — also overridable from the "Tracing" config section
    config.Sources.Add("MyCustomActivitySource");

    // Add custom meters
    config.ConfigureMetrics = metrics =>
    {
        metrics.AddMeter("MyCustomMeter");
    };

    // Add custom resource attributes
    config.ConfigureResource = resource =>
    {
        resource.AddAttributes([
            new KeyValuePair<string, object>("deployment.region", "eu-west-1"),
            new KeyValuePair<string, object>("team", "backend")
        ]);
    };

    // Custom request filter — trace everything except health checks and metrics
    config.FilterHttpRequest = context =>
        !context.Request.Path.StartsWithSegments("/healthz") &&
        !context.Request.Path.StartsWithSegments("/metrics");
});

var app = builder.Build();
app.Run();
```

### Custom Activity Spans

Create manual spans using the standard `System.Diagnostics` API. Make sure the `ActivitySource` name matches your `ServiceName`, one of the default patterns, or is added via `config.Sources`:

```csharp
using System.Diagnostics;

// Create an ActivitySource matching your service name
private static readonly ActivitySource ActivitySource = new("Orders.API");

public async Task<Order> PlaceOrder(OrderRequest request)
{
    using var activity = ActivitySource.StartActivity("PlaceOrder");
    activity?.SetTag("order.customer_id", request.CustomerId);
    activity?.SetTag("order.item_count", request.Items.Count);

    // Your business logic here
    var order = await _orderService.Create(request);

    activity?.SetTag("order.id", order.Id);
    return order;
}
```

### Custom Metrics

```csharp
using System.Diagnostics.Metrics;

// Register the meter via configuration
builder.Services.AddOpenTelemetry(config =>
{
    config.ServiceName = "Orders.API";
    config.Environment = "Production";

    config.ConfigureMetrics = metrics =>
    {
        metrics.AddMeter("Orders.Metrics");
    };
});

// Use the meter in your application
private static readonly Meter Meter = new("Orders.Metrics");
private static readonly Counter<long> OrdersPlaced = Meter.CreateCounter<long>("orders.placed");

public void OnOrderPlaced(Order order)
{
    OrdersPlaced.Add(1, new KeyValuePair<string, object?>("order.type", order.Type));
}
```

## Controlling Trace Sources

By default both packages listen to the configured `ServiceName` plus the `Intropy.*` and `Azure.*`
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


## Environment Variables

The OTLP exporter is configured through standard [OpenTelemetry environment variables](https://opentelemetry.io/docs/specs/otel/protocol/exporter/). Common variables:

| Variable | Example | Description |
|----------|---------|-------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://localhost:4317` | OTLP collector endpoint |
| `OTEL_EXPORTER_OTLP_HEADERS` | `authorization=Bearer token123` | Headers sent with each export request |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` | Transport protocol (`grpc` or `http/protobuf`) |

Resource attributes can also be set via environment variables as an alternative to code configuration:

| Variable | Example | Description |
|----------|---------|-------------|
| `OTEL_SERVICE_NAME` | `MyService` | Overrides the service name |
| `OTEL_RESOURCE_ATTRIBUTES` | `team=backend,region=eu` | Additional resource attributes |

