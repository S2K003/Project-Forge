using ForgeOps.AI;
using ForgeOps.Api.Endpoints;
using ForgeOps.Api.Health;
using ForgeOps.Api.Observability;
using ForgeOps.Forge;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Structured logs to stdout (ProjectForge.md §7.6).
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddForgeOpsAi(builder.Configuration);
builder.Services.AddForgeOpsForge(builder.Configuration);
builder.Services.AddSingleton<AiBridgeStatusCache>();
builder.Services.AddHostedService<AiBridgeStatusPoller>();

// The Blazor WASM frontend is a fully independent deployable on another origin (§6, §7.1).
const string WebCorsPolicy = "forgeops-web";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(WebCorsPolicy, policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else
    {
        // Dev fallback: any localhost port.
        policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback)
            .AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("forgeops-api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporterIfConfigured(builder.Configuration))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter(AiTelemetry.MeterName)
        .AddOtlpExporterIfConfigured(builder.Configuration));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors(WebCorsPolicy);

app.MapHealthEndpoints();
app.MapDemoEndpoints();
app.MapRequirementEndpoints();
app.MapForgeEndpoints();

app.Run();

public partial class Program;
