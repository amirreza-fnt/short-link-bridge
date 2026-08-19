using Microsoft.Data.SqlClient;
using ShortLinkBridge.Api.Models;

namespace ShortLinkBridge.Api.Services;

public sealed class ShortLinkQueueProcessor(
    IConfiguration configuration,
    ShortLinksApiClient shortLinksApi,
    ILogger<ShortLinkQueueProcessor> logger)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("QueueDatabase")
        ?? throw new InvalidOperationException("ConnectionStrings:QueueDatabase is required.");

    private readonly string? _defaultGroupName = configuration["ShortLinks:GroupName"];

    public async Task<ProcessQueueResult> ProcessAsync(int batchSize, CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 200);
        await DropAlreadyDoneAsync(ct);
        var items = await ClaimPendingItemsAsync(batchSize, ct);

        if (items.Count == 0)
        {
            return new ProcessQueueResult { RequestedBatchSize = batchSize };
        }

        var createItems = items.Select(i => new BatchCreateLinkItem
        {
            Url = i.LongUrl,
            GroupName = _defaultGroupName
        }).ToList();

        IReadOnlyList<BatchCreateLinkResult> createResults;
        try
        {
            createResults = await shortLinksApi.CreateBatchAsync(createItems, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Short-links API failed for {Count} points; will retry next cycle", items.Count);
            return new ProcessQueueResult
            {
                RequestedBatchSize = batchSize,
                ProcessedCount = items.Count,
                FailedCount = items.Count,
                Errors = new[] { ex.Message }
            };
        }

        var succeeded = 0;
        var failed = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var result = i < createResults.Count ? createResults[i] : null;
            if (result?.ShortUrl is { Length: > 0 })
            {
                await SaveAndRemoveAsync(item.PointId, result.ShortUrl, ct);
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        return new ProcessQueueResult
        {
            RequestedBatchSize = batchSize,
            ProcessedCount = items.Count,
            SucceededCount = succeeded,
            FailedCount = failed
        };
    }

    private async Task DropAlreadyDoneAsync(CancellationToken ct)
    {
        const string sql = @"
DELETE q
FROM dbo.ShortLinkQueue AS q
INNER JOIN dbo.MapPoints AS mp ON mp.Id = q.PointId
WHERE mp.ShortVisitLink IS NOT NULL;";
        await ExecuteNonQueryAsync(sql, ct);
    }

    private async Task<List<ShortLinkQueueItem>> ClaimPendingItemsAsync(int batchSize, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP (@BatchSize)
    q.PointId,
    mp.VisitLink
FROM dbo.ShortLinkQueue AS q WITH (UPDLOCK, READPAST, ROWLOCK)
INNER JOIN dbo.MapPoints AS mp ON mp.Id = q.PointId
WHERE mp.ShortVisitLink IS NULL
ORDER BY q.PointId;";

        var items = new List<ShortLinkQueueItem>();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@BatchSize", batchSize);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new ShortLinkQueueItem
            {
                PointId = reader.GetGuid(0),
                LongUrl = reader.GetString(1)
            });
        }

        return items;
    }

    private async Task SaveAndRemoveAsync(Guid pointId, string shortUrl, CancellationToken ct)
    {
        const string sql = @"
UPDATE dbo.MapPoints
SET ShortVisitLink = @ShortUrl
WHERE Id = @PointId
  AND ShortVisitLink IS NULL;

DELETE FROM dbo.ShortLinkQueue
WHERE PointId = @PointId;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ShortUrl", shortUrl);
        cmd.Parameters.AddWithValue("@PointId", pointId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task ExecuteNonQueryAsync(string sql, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
