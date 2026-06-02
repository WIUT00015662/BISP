using System.Text.Json;

namespace Bisp.Api.Services;

public sealed class GogService
{
    private readonly HttpClient _http;
    private readonly ILogger<GogService> _logger;

    public GogService(HttpClient http, ILogger<GogService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(decimal CurrentPrice, decimal? RegularPrice)?> GetPriceAsync(
        string gogProductId,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://api.gog.com/products/{gogProductId}?expand=prices";
        _logger.LogDebug("Fetching GOG price for product {GogProductId}.", gogProductId);
        try
        {
            var json = await _http.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("prices", out var prices)) return null;
            if (!prices.TryGetProperty("items", out var items)) return null;

            var firstItem = items.EnumerateArray().FirstOrDefault();
            if (firstItem.ValueKind == JsonValueKind.Undefined) return null;

            if (!firstItem.TryGetProperty("finalPrice", out var finalPriceEl)) return null;
            if (!firstItem.TryGetProperty("basePrice", out var basePriceEl)) return null;

            // GOG returns prices as strings like "1999" (in cents) or "19.99"
            var current = ParseGogPrice(finalPriceEl.GetString());
            var regular = ParseGogPrice(basePriceEl.GetString());

            if (current is null) return null;

            return (current.Value, regular != current ? regular : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GOG price for product {GogProductId}.", gogProductId);
            return null;
        }
    }

    private static decimal? ParseGogPrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // GOG API returns prices as integer strings in cents (e.g., "1999" = $19.99)
        if (long.TryParse(raw.Replace(" USD", "").Trim(), out var cents))
            return cents / 100m;
        if (decimal.TryParse(raw.Replace(" USD", "").Trim(),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var dec))
            return dec;
        return null;
    }
}
