namespace Bisp.Api.Models;

public sealed class UserSubscription
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? CurrentPeriodEndUtc { get; set; }

    public ApplicationUser? User { get; set; }
}
