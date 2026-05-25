namespace Bisp.Api.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    public int IntervalHours { get; set; } = 10;
}
