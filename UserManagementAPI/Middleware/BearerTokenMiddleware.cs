using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

internal sealed class BearerTokenMiddleware(
    RequestDelegate next,
    string apiToken,
    ILogger<BearerTokenMiddleware> logger)
{
    private readonly byte[] _apiTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(apiToken));

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(authorization, out var header)
            && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(header.Parameter)
            && IsValidToken(header.Parameter))
        {
            await next(context);
            return;
        }

        logger.LogInformation("HTTP {Method} {Path} request received", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = "Bearer";
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode);
    }

    private bool IsValidToken(string providedToken)
    {
        var providedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedToken));
        return CryptographicOperations.FixedTimeEquals(_apiTokenHash, providedTokenHash);
    }
}