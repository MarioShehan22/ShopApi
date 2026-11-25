using Microsoft.EntityFrameworkCore;
using ShopApi.Exceptions;

namespace ShopApi.Middleware;

/// <summary>Turns any unhandled exception into an RFC 7807 ProblemDetails response.</summary>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, title, detail) = ex switch
            {
                AppException app => (app.StatusCode, app.Title, app.Message),

                // Must come before DbUpdateException (it is a subclass)
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict",
                    "The record was changed by someone else. Reload it and try again."),

                DbUpdateException => (StatusCodes.Status409Conflict, "Database update failed",
                    "The change violates a database rule (duplicate value or related data exists)."),

                BadHttpRequestException bad => (StatusCodes.Status400BadRequest, "Bad request", bad.Message),

                _ => (StatusCodes.Status500InternalServerError, "Unexpected error",
                    env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.")
            };

            if (status >= 500) logger.LogError(ex, "Unhandled exception");
            else logger.LogWarning("Handled {Type}: {Message}", ex.GetType().Name, ex.Message);

            await Results.Problem(
                detail: detail,
                instance: context.Request.Path,
                statusCode: status,
                title: title,
                extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }
            ).ExecuteAsync(context);
        }
    }
}
