using EventManagementService.API.BackgroundServices;
using EventManagementService.API.DataAccess;
using EventManagementService.API.Middleware;
using EventManagementService.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddHostedService<BookingProcessingBackgroundService>();

var app = builder.Build();

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

// Maps controller routes for API endpoints.
app.MapControllers();

// Starts the application and listens for incoming HTTP requests.
app.Run();
