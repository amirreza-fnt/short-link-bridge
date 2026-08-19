namespace ShortLinkBridge.Api.Services;

/// <summary>
/// جاب داخلی: هر چند ثانیه صف لینک کوتاه را پردازش می‌کند.
/// جایگزین SQL Agent است تا سرویس بدون دسترسی به Agent هم کار کند.
/// </summary>
public sealed class QueueProcessorHostedService(
    ShortLinkQueueProcessor processor,
    IConfiguration configuration,
    ILogger<QueueProcessorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(configuration.GetValue("Queue:PollIntervalSeconds", 10), 5, 300);
        var batchSize = configuration.GetValue("Queue:BatchSize", 50);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        logger.LogInformation(
            "Short-link queue job started. Interval={Interval}s BatchSize={BatchSize}",
            intervalSeconds,
            batchSize);

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await processor.ProcessAsync(batchSize, stoppingToken);
                if (result.ProcessedCount > 0)
                {
                    logger.LogInformation(
                        "Queue cycle: processed={Processed} succeeded={Succeeded} failed={Failed}",
                        result.ProcessedCount,
                        result.SucceededCount,
                        result.FailedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Queue processing cycle failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
