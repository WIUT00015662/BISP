namespace Bisp.Api.Models;

public sealed class Game
{
    public Guid Id { get; set; }
    public long IgdbId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Genres { get; set; }
    public string? SummaryUrl { get; set; }

    public ICollection<ExternalGameId> ExternalGameIds { get; set; } = new List<ExternalGameId>();
    public ICollection<GameStorePrice> StorePrices { get; set; } = new List<GameStorePrice>();
}
