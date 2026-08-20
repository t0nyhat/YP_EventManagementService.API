using EventManagementService.Events.Infrastructure;
using EventManagementService.Events.Application;
using EventManagementService.Events.Presentation.Middleware;
using EventManagementService.Events.Infrastructure.DataAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========== Валидация OpenTelemetry ==========
var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"];
if (string.IsNullOrWhiteSpace(otelServiceName))
    throw new InvalidOperationException(
        "OpenTelemetry:ServiceName is not configured. Set it in appsettings.json or via OpenTelemetry__ServiceName environment variable.");

var otlpEndpoint = builder.Configuration["Otlp:Endpoint"];
if (string.IsNullOrWhiteSpace(otlpEndpoint))
    throw new InvalidOperationException(
        "Otlp:Endpoint is not configured. Set it in appsettings.json or via Otlp__Endpoint environment variable.");

if (!Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var otlpUri) ||
    (otlpUri.Scheme != "http" && otlpUri.Scheme != "https"))
    throw new InvalidOperationException(
        $"Otlp:Endpoint must be an absolute HTTP or HTTPS URI. Current value: '{otlpEndpoint}'.");

// ========== Serilog ==========
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service.name", otelServiceName)
    .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// ========== Конвейер OpenTelemetry ==========
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(otelServiceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/metrics"))
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = otlpUri;
            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

// ========== Конфигурация сервисов ==========
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Events Service API",
        Version = "v1.0"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Введите JWT токен в формате: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT"
    };

    var securitySchemeReference = new OpenApiSecuritySchemeReference(
        JwtBearerDefaults.AuthenticationScheme,
        hostDocument: null,
        externalResource: null);

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [securitySchemeReference] = []
    });
});

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation error",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Detail = "One or more validation errors occurred.",
            Instance = context.HttpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

// Конфигурация JWT обязательна — падаем сразу, если она отсутствует или неполная.
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];
var jwtSigningKey = jwtSection["SigningKey"];

if (string.IsNullOrEmpty(jwtIssuer))
    throw new InvalidOperationException("JWT Issuer is not configured. Set Jwt__Issuer environment variable or Jwt:Issuer in appsettings.");
if (string.IsNullOrEmpty(jwtAudience))
    throw new InvalidOperationException("JWT Audience is not configured. Set Jwt__Audience environment variable or Jwt:Audience in appsettings.");
if (string.IsNullOrEmpty(jwtSigningKey) || jwtSigningKey.Length < 32)
    throw new InvalidOperationException("JWT SigningKey is not configured or is too short (minimum 32 characters). Set Jwt__SigningKey environment variable or Jwt:SigningKey in appsettings.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!builder.Configuration.GetValue<bool>("SkipDatabaseMigration"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
    db.Database.Migrate();
}

// ========== Конвейер обработки HTTP-запросов ==========
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/metrics"),
    branch => branch.UseSerilogRequestLogging());
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Events Service API v1.0");
    });
}

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/metrics"),
    branch => branch.UseHttpsRedirection());

app.UseAuthentication();
app.UseAuthorization();

// ========== Endpoint метрик ==========
app.MapPrometheusScrapingEndpoint()
    .AllowAnonymous()
    .DisableHttpMetrics();

app.MapControllers();

app.Run();

public partial class Program;
