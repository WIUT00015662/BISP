using Bisp.Api.Options;
using Microsoft.Extensions.Options;

namespace Bisp.Api.Services;

public sealed class AggregationWorker : BackgroundService
{
    private readonly AggregationService _aggregationService;
    private readonly AggregationOptions _options;
    private readonly ILogger<AggregationWorker> _logger;

    public AggregationWorker(
        AggregationService aggregationService,
        IOptions<AggregationOptions> options,
        ILogger<AggregationWorker> logger)
    {
        _aggregationService = aggregationService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = Math.Max(1, _options.IntervalHours);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _aggregationService.RunAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Aggregation run failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
