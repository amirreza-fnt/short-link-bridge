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

    private readonly int _maxAttempts = configuration.GetValue("Queue:MaxAttempts", 5);
    private readonly string? _defaultGroupName = configuration["ShortLinks:GroupName"];

    public async Task<ProcessQueueResult> ProcessAsync(int batchSize, CancellationToken ct = default)
    {
        batchSize = Math.Clamp(batchSize, 1, 200);
        var items = await ClaimPendingItemsAsync(batchSize, ct);

        if (items.Count == 0)
        {
            return new ProcessQueueResult
            {
                RequestedBatchSize = batchSize,
                ProcessedCount = 0
            };
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
            logger.LogError(ex, "Short-links batch API failed for {Count} items", items.Count);
            await MarkItemsFailedAsync(items, ex.Message, ct);
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
        var errors = new List<string>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var result = i < createResults.Count ? createResults[i] : null;

            if (result?.ShortUrl is { Length: > 0 })
            {
                try
                {
                    await SaveShortUrlAsync(item, result.ShortUrl, ct);
                    await MarkItemDoneAsync(item.Id, result.ShortUrl, ct);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save short URL for queue item {QueueId}", item.Id);
                    await MarkItemFailedAsync(item, ex.Message, ct);
                    failed++;
                    errors.Add($"Queue {item.Id}: {ex.Message}");
                }
            }
            else
            {
                var error = result?.Error ?? "Short link was not returned.";
                await MarkItemFailedAsync(item, error, ct);
                failed++;
                errors.Add($"Queue {item.Id}: {error}");
            }
        }

        return new ProcessQueueResult
        {
            RequestedBatchSize = batchSize,
            ProcessedCount = items.Count,
            SucceededCount = succeeded,
            FailedCount = failed,
            Errors = errors
        };
    }

    private async Task<List<ShortLinkQueueItem>> ClaimPendingItemsAsync(int batchSize, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        var selectSql = $@"
SELECT TOP (@BatchSize)
    Id, SourceSchema, SourceTable, SourceKeyColumn, SourceKeyValue, LongUrl, TargetColumn, AttemptCount
FROM dbo.ShortLinkQueue WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE Status = 0 AND AttemptCount < @MaxAttempts
ORDER BY CreatedAt;";

        var items = new List<ShortLinkQueueItem>();
        await using (var selectCmd = new SqlCommand(selectSql, connection, transaction))
        {
            selectCmd.Parameters.AddWithValue("@BatchSize", batchSize);
            selectCmd.Parameters.AddWithValue("@MaxAttempts", _maxAttempts);

            await using var reader = await selectCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new ShortLinkQueueItem
                {
                    Id = reader.GetInt64(0),
                    SourceSchema = reader.GetString(1),
                    SourceTable = reader.GetString(2),
                    SourceKeyColumn = reader.GetString(3),
                    SourceKeyValue = reader.GetString(4),
                    LongUrl = reader.GetString(5),
                    TargetColumn = reader.GetString(6),
                    AttemptCount = reader.GetInt32(7)
                });
            }
        }

        if (items.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return items;
        }

        var ids = string.Join(",", items.Select(i => i.Id));
        var updateSql = $@"
UPDATE dbo.ShortLinkQueue
SET Status = 1,
    AttemptCount = AttemptCount + 1
WHERE Id IN ({ids});";

        await using (var updateCmd = new SqlCommand(updateSql, connection, transaction))
        {
            await updateCmd.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return items;
    }

    private async Task SaveShortUrlAsync(ShortLinkQueueItem item, string shortUrl, CancellationToken ct)
    {
        if (!IsSafeIdentifier(item.SourceSchema) ||
            !IsSafeIdentifier(item.SourceTable) ||
            !IsSafeIdentifier(item.SourceKeyColumn) ||
            !IsSafeIdentifier(item.TargetColumn))
        {
            throw new InvalidOperationException("Queue item contains unsafe SQL identifiers.");
        }

        var sql = $@"
UPDATE [{item.SourceSchema}].[{item.SourceTable}]
SET [{item.TargetColumn}] = @ShortUrl
WHERE [{item.SourceKeyColumn}] = @SourceKeyValue;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ShortUrl", shortUrl);
        cmd.Parameters.AddWithValue("@SourceKeyValue", item.SourceKeyValue);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected == 0)
            throw new InvalidOperationException($"Target row not found: {item.SourceTable}.{item.SourceKeyColumn}={item.SourceKeyValue}");
    }

    private async Task MarkItemDoneAsync(long queueId, string shortUrl, CancellationToken ct)
    {
        const string sql = @"
UPDATE dbo.ShortLinkQueue
SET Status = 2,
    ShortUrl = @ShortUrl,
    ProcessedAt = SYSUTCDATETIME(),
    LastError = NULL
WHERE Id = @Id;";

        await ExecuteNonQueryAsync(sql, ct,
            new SqlParameter("@ShortUrl", shortUrl),
            new SqlParameter("@Id", queueId));
    }

    private Task MarkItemFailedAsync(ShortLinkQueueItem item, string error, CancellationToken ct)
        => MarkItemsFailedAsync(new[] { item }, error, ct);

    private async Task MarkItemsFailedAsync(IReadOnlyList<ShortLinkQueueItem> items, string error, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var status = item.AttemptCount + 1 >= _maxAttempts
                ? QueueItemStatus.Failed
                : QueueItemStatus.Pending;

            const string sql = @"
UPDATE dbo.ShortLinkQueue
SET Status = @Status,
    LastError = @LastError,
    ProcessedAt = CASE WHEN @Status = 3 THEN SYSUTCDATETIME() ELSE NULL END
WHERE Id = @Id;";

            await ExecuteNonQueryAsync(sql, ct,
                new SqlParameter("@Status", (byte)status),
                new SqlParameter("@LastError", error),
                new SqlParameter("@Id", item.Id));
        }
    }

    private async Task ExecuteNonQueryAsync(string sql, CancellationToken ct, params SqlParameter[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddRange(parameters);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static bool IsSafeIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value) && value.All(c => char.IsLetterOrDigit(c) || c == '_');
}
