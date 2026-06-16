namespace LabelVerification.Api.Middleware;

/// <summary>
/// Serves Next.js static export HTML for nested routes (e.g. /verify/ → verify/index.html)
/// before falling back to the root index.html.
/// </summary>
internal sealed class StaticExportFallbackMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StaticExportFallbackMiddleware> _logger;

    public StaticExportFallbackMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment,
        ILogger<StaticExportFallbackMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (ShouldAppendTrailingSlash(path))
            {
                var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
                context.Response.Redirect($"{path}/{query}", permanent: false);
                return;
            }

            if (await TryServeStaticExportPageAsync(context, path))
            {
                return;
            }
        }

        await _next(context);
    }

    private static bool ShouldAppendTrailingSlash(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return false;
        }

        if (path.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !path.EndsWith('/');
    }

    private async Task<bool> TryServeStaticExportPageAsync(HttpContext context, string path)
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return false;
        }

        var normalizedPath = path.TrimEnd('/');
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        var relativeSegments = normalizedPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var nestedIndexPath = Path.Combine(webRoot, Path.Combine(relativeSegments), "index.html");

        if (!File.Exists(nestedIndexPath))
        {
            return false;
        }

        if (context.Response.HasStarted)
        {
            return false;
        }

        _logger.LogDebug("Serving static export page {Path} for request {RequestPath}", nestedIndexPath, path);
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(nestedIndexPath);
        return true;
    }
}
