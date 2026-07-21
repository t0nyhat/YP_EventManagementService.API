using EventManagementService.Users.Infrastructure.Configuration;
using EventManagementService.Users.Presentation.Middleware;
using EventManagementService.Users.Application;
using EventManagementService.Users.Infrastructure;
using EventManagementService.Users.Infrastructure.DataAccess;
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

// ========== Services Configuration ==========
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Users Service API",
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT settings are not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    db.Database.Migrate();
}

// ========== HTTP Request Pipeline ==========
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/metrics"),
    branch => branch.UseSerilogRequestLogging());
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Users Service API v1.0");
    });
    app.MapOpenApi();
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
