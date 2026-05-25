namespace Bisp.Api.Models;

public sealed class GameStorePrice
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public int StoreId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? RegularPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime LastUpdatedUtc { get; set; }

    public Game? Game { get; set; }
    public Store? Store { get; set; }
}
