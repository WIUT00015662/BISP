namespace Bisp.Api.Models;

public sealed class WishlistItem
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid GameId { get; set; }
    public decimal MinDiscountPercent { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? LastNotifiedUtc { get; set; }

    public ApplicationUser? User { get; set; }
    public Game? Game { get; set; }
}
