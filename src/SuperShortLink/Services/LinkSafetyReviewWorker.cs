using Microsoft.Extensions.Options;

namespace SuperShortLink;

public sealed class LinkSafetyReviewWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ShortLinkOptions> _options;
    private readonly ILogger<LinkSafetyReviewWorker> _logger;

    public LinkSafetyReviewWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ShortLinkOptions> options,
        ILogger<LinkSafetyReviewWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue.UrlSafety ?? new UrlSafetyOptions();
            var interval = TimeSpan.FromMinutes(Math.Clamp(options.ReviewIntervalMinutes, 1, 10080));
            await Task.Delay(interval, stoppingToken);

            if (!options.Enabled || !options.PeriodicReviewEnabled)
            {
                continue;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IShortLinkService>();
                var result = await service.ReviewDueLinksAsync(
                    Math.Clamp(options.ReviewBatchSize, 1, 1000), stoppingToken);
                _logger.LogInformation(
                    "Periodic URL safety review checked {CheckedCount} links and disabled {DisabledCount}",
                    result.checked_count, result.disabled_count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Periodic URL safety review failed");
            }
        }
    }
}
