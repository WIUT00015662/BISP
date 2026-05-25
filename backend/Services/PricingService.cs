namespace Bisp.Api.Services;

public sealed class PricingService
{
    public static decimal? CalculateDiscountPercent(decimal? regularPrice, decimal? currentPrice)
    {
        if (regularPrice is null || currentPrice is null || regularPrice <= 0)
        {
            return null;
        }

        var discount = (regularPrice.Value - currentPrice.Value) / regularPrice.Value * 100m;
        return Math.Round(discount, 2, MidpointRounding.AwayFromZero);
    }
}
