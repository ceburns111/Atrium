using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;

namespace Atrium.ServiceDefaults;

/// <summary>
/// Shared, Aspire-idiomatic observability wiring — Serilog structured logging plus OpenTelemetry
/// distributed tracing — so the four runtime hosts (Catalog, Storefront, Gateway, Portal) opt in with
/// a single call instead of copy-pasting telemetry into each Program.cs. Deliberately does NOT touch
/// service discovery or health checks; those stay hand-wired per host.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    /// Replaces the default logging provider with Serilog (structured, console sink, log-context
    /// enrichment) and registers OpenTelemetry tracing for ASP.NET Core + outbound HttpClient calls,
    /// optionally SqlClient. Traces are exported over OTLP to the endpoint Aspire injects
    /// (<c>OTEL_EXPORTER_OTLP_ENDPOINT</c>), which lights up the Aspire dashboard with no extra config;
    /// when that endpoint is absent (e.g. tests) the exporter is skipped to avoid connection noise.
    /// </summary>
    public static TBuilder AddAtriumTelemetry<TBuilder>(
        this TBuilder builder,
        bool instrumentSqlClient = false
    )
        where TBuilder : IHostApplicationBuilder
    {
        // Serilog as THE logging provider (replaces the host's default ILoggerFactory). App code keeps
        // using ILogger<T>. Because Serilog replaces the MEL factory, appsettings "Logging:LogLevel"
        // sections (MEL-format) no longer apply — so the defaults are stated here in Serilog's own
        // format: Information overall, with Microsoft.AspNetCore at Warning so the framework's
        // "Request starting/finished" Information pair doesn't print alongside the
        // UseSerilogRequestLogging() summary (double request noise). ReadFrom.Configuration runs LAST
        // so a "Serilog" section in a host's appsettings/env can override these defaults — later
        // MinimumLevel/Override calls win, so defaults-then-config is the order that makes config
        // authoritative.
        builder.Services.AddSerilog(
            (services, loggerConfiguration) =>
                loggerConfiguration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .ReadFrom.Configuration(builder.Configuration)
                    .ReadFrom.Services(services)
        );

        var otel = builder
            .Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();

                // Only the two data-owning services talk to SQL (Dapper over Microsoft.Data.SqlClient).
                if (instrumentSqlClient)
                {
                    tracing.AddSqlClientInstrumentation();
                }
            })
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
            );

        // Aspire injects OTEL_EXPORTER_OTLP_ENDPOINT into every launched resource; export there so a
        // single trace spans Portal → Gateway → Service → SQL in the dashboard. Guarded so tests and
        // standalone runs (no dashboard) don't spew exporter connection errors.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Condenses each request into a single structured Serilog event (method, path, status, elapsed).
    /// Call early in the pipeline so it wraps the downstream handlers.
    /// </summary>
    public static WebApplication UseAtriumRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        return app;
    }
}
