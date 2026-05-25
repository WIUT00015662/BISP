using Bisp.Api.Data;

namespace Bisp.Api.Services;

public sealed class AggregationService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AggregationService> _logger;

    public AggregationService(AppDbContext dbContext, ILogger<AggregationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aggregation run requested at {UtcNow}.", DateTime.UtcNow);
        _ = _dbContext.Database;
        // Placeholder: integrate Steam/GOG/Epic ingestion and IGDB mapping here.
        return Task.CompletedTask;
    }
}
