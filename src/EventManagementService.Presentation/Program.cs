using EventManagementService.Presentation.BackgroundServices;
using EventManagementService.Infrastructure.Configuration;
using EventManagementService.Presentation.Middleware;
using EventManagementService.Application;
using EventManagementService.Infrastructure;
using EventManagementService.Infrastructure.DataAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ========== Services Configuration ==========
// Enables OpenAPI/Swagger support for interactive API documentation and testing.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Event Management Service API",
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

// Registers controllers for API endpoints.
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
builder.Services.AddHostedService<BookingProcessingBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(user => user.Id == EventManagementService.Domain.Models.User.SystemUserId))
    {
        db.Users.Add(EventManagementService.Domain.Models.User.Create(
            "system",
            "system-hash",
            EventManagementService.Domain.Models.UserRole.User,
            EventManagementService.Domain.Models.User.SystemUserId));
        db.SaveChanges();
    }
}

// ========== HTTP Request Pipeline ==========
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Enables OpenAPI endpoint and interactive Swagger UI for development testing.
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Event Management Service API v1.0");
    });
    app.MapOpenApi();
}

// Enforces HTTPS: redirects all HTTP requests to HTTPS for security.
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Maps controller routes for API endpoints.
app.MapControllers();

// Starts the application and listens for incoming HTTP requests.
app.Run();
