using EventManagementService.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ========== Services Configuration ==========
// Enables OpenAPI/Swagger support for interactive API documentation and testing.
builder.Services.AddOpenApi();

// Registers controllers for API endpoints.
builder.Services.AddControllers();

// Registers IEventService as Singleton: single instance shared across all requests.
// Suitable for in-memory storage since the same data collection is used for the app lifetime.
builder.Services.AddSingleton<IEventService, EventService>();

var app = builder.Build();

// ========== HTTP Request Pipeline ==========
if (app.Environment.IsDevelopment())
{
    // Enables OpenAPI endpoint and interactive Swagger UI for development testing.
    app.MapOpenApi();
}

// Enforces HTTPS: redirects all HTTP requests to HTTPS for security.
app.UseHttpsRedirection();

// Maps controller routes for API endpoints.
app.MapControllers();

// Starts the application and listens for incoming HTTP requests.
app.Run();


