using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Intropy.Telemetry.ConsoleApp.Test;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOpenTelemetry_RegistersOpenTelemetryServices()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "TestService";
            config.Environment = "Test";
        });

        var serviceProvider = services.BuildServiceProvider();

        var tracerProvider = serviceProvider.GetService<TracerProvider>();
        var meterProvider = serviceProvider.GetService<MeterProvider>();

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddOpenTelemetry_InvokesConfigurationCallback()
    {
        var services = new ServiceCollection();
        var configurationCalled = false;
        OpenTelemetryConfiguration? capturedConfig = null;

        services.AddOpenTelemetry(config =>
        {
            configurationCalled = true;
            capturedConfig = config;
            config.ServiceName = "TestService";
            config.Environment = "Test";
        });

        Assert.True(configurationCalled);
        Assert.NotNull(capturedConfig);
        Assert.Equal("TestService", capturedConfig.ServiceName);
        Assert.Equal("Test", capturedConfig.Environment);
    }

    [Fact]
    public void AddOpenTelemetry_WithValidConfiguration_DoesNotThrow()
    {
        var services = new ServiceCollection();

        var exception = Record.Exception(() =>
        {
            services.AddOpenTelemetry(config =>
            {
                config.ServiceName = "TestService";
                config.ServiceNamespace = "TestNamespace";
                config.Environment = "Production";
                config.ServiceVersion = "1.0.0";
            });

            services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddOpenTelemetry_WithUserResourceConfiguration_InvokesCallback()
    {
        var services = new ServiceCollection();
        var userResourceConfigCalled = false;

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "TestService";
            config.Environment = "Test";
            config.ConfigureResource = resource =>
            {
                userResourceConfigCalled = true;
                resource.AddAttributes([
                    new KeyValuePair<string, object>("custom.attribute", "test-value")
                ]);
            };
        });

        var serviceProvider = services.BuildServiceProvider();
        var tracerProvider = serviceProvider.GetService<TracerProvider>();

        Assert.NotNull(tracerProvider);
        Assert.True(userResourceConfigCalled);
    }

    [Fact]
    public void AddOpenTelemetry_WithUserTracingConfiguration_InvokesCallback()
    {
        var services = new ServiceCollection();
        var userTracingConfigCalled = false;

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "TestService";
            config.Environment = "Test";
            config.ConfigureTracing = tracing =>
            {
                userTracingConfigCalled = true;
                tracing.AddSource("CustomSource");
            };
        });

        var serviceProvider = services.BuildServiceProvider();
        var tracerProvider = serviceProvider.GetService<TracerProvider>();

        Assert.NotNull(tracerProvider);
        Assert.True(userTracingConfigCalled);
    }

    [Fact]
    public void AddOpenTelemetry_WithUserMetricsConfiguration_InvokesCallback()
    {
        var services = new ServiceCollection();
        var userMetricsConfigCalled = false;

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "TestService";
            config.Environment = "Test";
            config.ConfigureMetrics = _ => { userMetricsConfigCalled = true; };
        });

        var serviceProvider = services.BuildServiceProvider();
        var meterProvider = serviceProvider.GetService<MeterProvider>();

        Assert.NotNull(meterProvider);
        Assert.True(userMetricsConfigCalled);
    }

    [Fact]
    public void AddOpenTelemetry_WithUserLoggingConfiguration_InvokesCallback()
    {
        var services = new ServiceCollection();
        var userLoggingConfigCalled = false;

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "TestService";
            config.Environment = "Test";
            config.ConfigureLogging = _ => { userLoggingConfigCalled = true; };
        });

        services.BuildServiceProvider();

        Assert.True(userLoggingConfigCalled);
    }

    [Fact]
    public void AddOpenTelemetry_WithMinimalConfiguration_Works()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "MinimalService";
            config.Environment = "Test";
        });

        var serviceProvider = services.BuildServiceProvider();
        var tracerProvider = serviceProvider.GetService<TracerProvider>();
        var meterProvider = serviceProvider.GetService<MeterProvider>();

        Assert.NotNull(tracerProvider);
        Assert.NotNull(meterProvider);
    }

    [Fact]
    public void AddOpenTelemetry_WithAllOptionalFields_Works()
    {
        var services = new ServiceCollection();

        services.AddOpenTelemetry(config =>
        {
            config.ServiceName = "FullService";
            config.ServiceNamespace = "TestNamespace";
            config.Environment = "Production";
            config.ServiceVersion = "2.1.0";

            config.ConfigureResource = resource =>
            {
                resource.AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", "prod")
                ]);
            };

            config.ConfigureTracing = tracing => { tracing.AddSource("CustomTracing"); };

            config.ConfigureMetrics = metrics => { metrics.AddMeter("TestMeter"); };

            config.ConfigureLogging = _ =>
            {
                //
            };
        });

        var exception = Record.Exception(() =>
        {
            var serviceProvider = services.BuildServiceProvider();
            var tracerProvider = serviceProvider.GetService<TracerProvider>();
            var meterProvider = serviceProvider.GetService<MeterProvider>();

            Assert.NotNull(tracerProvider);
            Assert.NotNull(meterProvider);
        });

        Assert.Null(exception);
    }
}
