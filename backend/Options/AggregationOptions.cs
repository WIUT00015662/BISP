namespace Bisp.Api.Options;

public sealed class AggregationOptions
{
    public const string SectionName = "Aggregation";

    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 10;
}
