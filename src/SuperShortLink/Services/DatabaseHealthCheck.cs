using Microsoft.Extensions.Diagnostics.HealthChecks;
using SuperShortLink.Repository;

namespace SuperShortLink;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly IShortLinkRepository _repository;

    public DatabaseHealthCheck(IShortLinkRepository repository)
    {
        _repository = repository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var today = DateTime.Today;
            await _repository.GetCountAsync(today, today.AddDays(1));
            return HealthCheckResult.Healthy("Database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unavailable.", ex);
        }
    }
}
