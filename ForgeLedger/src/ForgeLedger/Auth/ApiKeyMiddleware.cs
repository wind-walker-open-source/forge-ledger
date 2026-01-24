namespace ForgeLedger.Auth;

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-API-KEY";

    private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/health",
        "/swagger",
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApiKeyProvider apiKeyProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip auth for excluded paths (health, swagger, root)
        if (IsExcludedPath(path))
        {
            await _next(context);
            return;
        }

        // Check for API key header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("API key missing for request to {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                title = "Unauthorized",
                status = 401,
                detail = $"Missing {ApiKeyHeaderName} header."
            });
            return;
        }

        var expectedApiKey = await apiKeyProvider.GetApiKeyAsync();

        if (string.IsNullOrEmpty(expectedApiKey))
        {
            _logger.LogError("API key not configured. Set it in Parameter Store at /ForgeLedger/API/Key or in appsettings.");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "Internal Server Error",
                status = 500,
                detail = "API key not configured on server."
            });
            return;
        }

        if (!string.Equals(providedApiKey, expectedApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid API key provided for request to {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                title = "Unauthorized",
                status = 401,
                detail = "Invalid API key."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsExcludedPath(string path)
    {
        if (ExcludedPaths.Contains(path))
            return true;

        // Allow all swagger sub-paths
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}