namespace Bisp.Api.Models;

public sealed class Store
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<GameStorePrice> GamePrices { get; set; } = new List<GameStorePrice>();
}
