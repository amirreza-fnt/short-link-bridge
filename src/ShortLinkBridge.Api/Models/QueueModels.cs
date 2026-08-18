namespace ShortLinkBridge.Api.Models;

public enum QueueItemStatus : byte
{
    Pending = 0,
    Processing = 1,
    Done = 2,
    Failed = 3
}

public sealed class ShortLinkQueueItem
{
    public long Id { get; init; }
    public string SourceSchema { get; init; } = "dbo";
    public string SourceTable { get; init; } = string.Empty;
    public string SourceKeyColumn { get; init; } = string.Empty;
    public string SourceKeyValue { get; init; } = string.Empty;
    public string LongUrl { get; init; } = string.Empty;
    public string TargetColumn { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
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
