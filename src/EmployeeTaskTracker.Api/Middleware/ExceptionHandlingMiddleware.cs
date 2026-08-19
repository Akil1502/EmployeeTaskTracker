using System.Text.Json;
using EmployeeTaskTracker.Shared;
using Microsoft.Data.SqlClient;

namespace EmployeeTaskTracker.Api.Middleware;

/// <summary>
/// Catches anything that escapes a controller, logs it with the request's trace
/// identifier, and returns a consistent JSON error body. This satisfies the
/// "proper exception handling" and "logging support" requirements and makes
/// sure a stack trace is never returned to the caller.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away mid-request. Not an error worth reporting.
            _logger.LogInformation("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (statusCode, message) = exception switch
        {
            // A RAISERROR raised by one of our stored procedures. Severity 16
            // means "the caller did something wrong", so surface it as a 400.
            SqlException { Class: 16 } sql => (StatusCodes.Status400BadRequest, sql.Message),

            // Connection-level SQL failures: the database is unreachable or the
            // setup script has not been run.
            SqlException => (StatusCodes.Status503ServiceUnavailable,
                "The database is unavailable. Confirm SQL Server is running and that database/setup.sql has been executed."),

            UnauthorizedAccessException => (StatusCodes.Status403Forbidden,
                "You do not have permission to perform this action."),

            _ => (StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while processing your request.")
        };

        _logger.LogError(exception,
            "Unhandled {ExceptionType} on {Method} {Path}. TraceId={TraceId}",
            exception.GetType().Name, context.Request.Method, context.Request.Path, traceId);

        if (context.Response.HasStarted)
        {
            // Too late to change the response; the log entry above is all we can do.
            _logger.LogWarning("Response for TraceId={TraceId} had already started; error body not written.", traceId);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var error = new ApiError
        {
            // Only echo raw exception detail outside production.
            Message = _environment.IsDevelopment() && statusCode == StatusCodes.Status500InternalServerError
                ? $"{message} ({exception.Message})"
                : message,
            TraceId = traceId
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(error,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
