using EventManagementService.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Presentation.Middleware;

/// <summary>
/// Handles unhandled exceptions and converts them to Problem Details responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IProblemDetailsService problemDetailsService)
{
    private static readonly Uri ValidationType = new("https://tools.ietf.org/html/rfc9110#section-15.5.1");
    private static readonly Uri UnauthorizedType = new("https://tools.ietf.org/html/rfc9110#section-15.5.2");
    private static readonly Uri ConflictType = new("https://tools.ietf.org/html/rfc9110#section-15.5.10");
    private static readonly Uri ForbiddenType = new("https://tools.ietf.org/html/rfc9110#section-15.5.4");
    private static readonly Uri NotFoundType = new("https://tools.ietf.org/html/rfc9110#section-15.5.5");
    private static readonly Uri ServerErrorType = new("https://tools.ietf.org/html/rfc9110#section-15.6.1");

    /// <summary>
    /// Invokes the middleware for the current HTTP request.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing {Method} {Path}. TraceId={TraceId}, RequestId={RequestId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier,
                context.Request.Headers["x-request-id"].ToString());
            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            logger.LogWarning(
                "The response has already started, the exception middleware will not modify the response. TraceId={TraceId}",
                context.TraceIdentifier);
            return;
        }

        var (statusCode, title, type) = MapException(exception);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Type = type.ToString(),
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        var wasWritten = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });

        if (!wasWritten)
        {
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }

    private static (int StatusCode, string Title, Uri Type) MapException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", UnauthorizedType),
            ForbiddenOperationException => (StatusCodes.Status403Forbidden, "Forbidden", ForbiddenType),
            TooManyActiveBookingsException => (StatusCodes.Status409Conflict, "Conflict", ConflictType),
            BookingAlreadyProcessedException => (StatusCodes.Status409Conflict, "Conflict", ConflictType),
            BusinessValidationException => (StatusCodes.Status400BadRequest, "Validation error", ValidationType),
            NoAvailableSeatsException => (StatusCodes.Status409Conflict, "Conflict", ConflictType),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", NotFoundType),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error", ServerErrorType)
        };
    }
}
