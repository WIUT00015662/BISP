namespace Bisp.Api.Models;

public sealed class NotificationLog
{
    public Guid Id { get; set; }
    public Guid WishlistItemId { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateTime SentUtc { get; set; }

    public WishlistItem? WishlistItem { get; set; }
}
