using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

internal sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var isInvalidRequest = exception is BadHttpRequestException
        {
            StatusCode: StatusCodes.Status400BadRequest
        };
        var statusCode = isInvalidRequest
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        var title = isInvalidRequest ? "Bad Request" : "Internal Server Error";
        var detail = isInvalidRequest
            ? "The request body is invalid."
            : "An unexpected error occurred.";

        if (isInvalidRequest)
        {
            logger.LogWarning(exception, "Invalid request body for {Path}", httpContext.Request.Path);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        });

        return true;
    }
}