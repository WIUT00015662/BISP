namespace Bisp.Api.Dtos;

public sealed record SubscriptionStatusDto(
    bool HasActiveSubscription,
    string? Status,
    DateTime? CurrentPeriodEndUtc);

public sealed record CheckoutSessionResponse(string Url);
