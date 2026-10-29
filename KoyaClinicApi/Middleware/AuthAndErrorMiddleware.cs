using System.Text.Json;
using KoyaClinicApi.Middleware; // for ApiException

namespace KoyaClinicApi.Middleware
{
    public class AuthAndErrorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _apiKey;

        public AuthAndErrorMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _apiKey = config["ApiKey"] ?? "test-key-123"; // Default fallback for local dev
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();

            // BYPASS: Allow UI, Docs, and static files to load without an API key
            if (path != null && (
                path.StartsWith("/scalar") ||
                path.StartsWith("/openapi") ||
                path.StartsWith("/index.html") ||
                path == "/" ||
                path.EndsWith(".js") ||
                path.EndsWith(".css") ||
                path.StartsWith("/favicon.ico")))
            {
                await _next(context);
                return;
            }

            // 1. API Key Check (Level 2)
            if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) || extractedApiKey != _apiKey)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\": \"Unauthorized\"}");
                return;
            }

            // 2. Global Error Handling
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";

                if (ex is ApiException apiEx)
                {
                    context.Response.StatusCode = apiEx.StatusCode;
                }
                else
                {
                    context.Response.StatusCode = 400;
                }

                var errorObj = new { error = ex.Message };
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorObj));
            }
        }
    }

    // Helper exception class to throw specific HTTP codes from anywhere in our code
    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public ApiException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}