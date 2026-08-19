namespace ShortLinkBridge.Api.Models;

public sealed class ShortLinkQueueItem
{
    public Guid PointId { get; init; }
    public string LongUrl { get; init; } = string.Empty;
}

public sealed class ProcessQueueResult
{
    public int RequestedBatchSize { get; init; }
    public int ProcessedCount { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class BatchCreateLinkItem
{
    public string Url { get; set; } = string.Empty;
    public string? GroupName { get; set; }
}

public sealed class BatchCreateLinksRequest
{
    public List<BatchCreateLinkItem> Items { get; set; } = new();
}

public sealed class BatchCreateLinkResult
{
    public string Url { get; set; } = string.Empty;
    public string? ShortUrl { get; set; }
    public string? Error { get; set; }
}

public sealed class BatchCreateLinksResponse
{
    public List<BatchCreateLinkResult> Results { get; set; } = new();
}
