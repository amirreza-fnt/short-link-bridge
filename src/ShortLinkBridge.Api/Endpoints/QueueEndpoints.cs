using ShortLinkBridge.Api.Models;
using ShortLinkBridge.Api.Services;

namespace ShortLinkBridge.Api.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this WebApplication app)
    {
        app.MapPost("/api/queue/process", async (
            int? batchSize,
            ShortLinkQueueProcessor processor,
            IConfiguration configuration,
            HttpContext http,
            CancellationToken ct) =>
        {
            if (!IsAuthorized(http, configuration))
                return Results.Unauthorized();

            var size = batchSize ?? configuration.GetValue("Queue:BatchSize", 50);
            var result = await processor.ProcessAsync(size, ct);
            return Results.Ok(new { success = true, data = result });
        })
        .WithTags("Queue")
        .WithDescription("پردازش دسته‌ای صف لینک کوتاه — توسط SQL Agent Job فراخوانی می‌شود.");

        app.MapGet("/api/queue/health", () => Results.Ok(new
        {
            status = "healthy",
            service = "ShortLinkBridge",
            utc = DateTimeOffset.UtcNow
        }));
    }

    private static bool IsAuthorized(HttpContext http, IConfiguration configuration)
    {
        var expectedKey = configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey))
            return true;

        return http.Request.Headers.TryGetValue("X-Api-Key", out var provided)
               && string.Equals(provided.ToString(), expectedKey, StringComparison.Ordinal);
    }
}
