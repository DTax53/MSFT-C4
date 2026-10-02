using System.Diagnostics;

internal sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        logger.LogInformation("HTTP {Method} {Path} request received", context.Request.Method, context.Request.Path);

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = exception is BadHttpRequestException
                {
                    StatusCode: StatusCodes.Status400BadRequest
                }
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status500InternalServerError;
            }

            throw;
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}