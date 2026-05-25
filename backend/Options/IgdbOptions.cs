namespace Bisp.Api.Options;

public sealed class IgdbOptions
{
    public const string SectionName = "Igdb";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
