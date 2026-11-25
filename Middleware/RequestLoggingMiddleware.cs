using System.Diagnostics;

namespace ShopApi.Middleware;

/// <summary>Adds a correlation id, times each request and logs the result.</summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[HeaderName] = correlationId;

        var sw = Stopwatch.StartNew();
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            try
            {
                await next(context);
            }
            finally
            {
                sw.Stop();
                logger.LogInformation("{Method} {Path} -> {StatusCode} in {Elapsed} ms",
                    context.Request.Method, context.Request.Path,
                    context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
