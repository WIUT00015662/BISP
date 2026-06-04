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
        var url = $"https://api.gog.com/products/prices?ids={gogProductId}&countryCode=US";
        _logger.LogDebug("Fetching GOG price for product {GogProductId}.", gogProductId);
        try
        {
            var json = await _http.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(json);

            // Path: _embedded.items[0]._embedded.prices[0]
            if (!doc.RootElement.TryGetProperty("_embedded", out var root)) return null;
            if (!root.TryGetProperty("items", out var items)) return null;

            var item = items.EnumerateArray().FirstOrDefault();
            if (item.ValueKind == JsonValueKind.Undefined) return null;

            if (!item.TryGetProperty("_embedded", out var itemEmbedded)) return null;
            if (!itemEmbedded.TryGetProperty("prices", out var prices)) return null;

            var price = prices.EnumerateArray().FirstOrDefault();
            if (price.ValueKind == JsonValueKind.Undefined) return null;

            if (!price.TryGetProperty("finalPrice", out var finalEl)) return null;
            if (!price.TryGetProperty("basePrice", out var baseEl)) return null;

            var finalRaw = finalEl.GetString();
            var baseRaw = baseEl.GetString();
            var current = ParseGogPrice(finalRaw);
            var regular = ParseGogPrice(baseRaw);

            if (current is null)
            {
                _logger.LogWarning(
                    "Failed to parse GOG price for product {GogProductId}. finalPrice='{Final}', basePrice='{Base}'.",
                    gogProductId, finalRaw, baseRaw);
                return null;
            }

            return (current.Value, regular != current ? regular : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GOG price for product {GogProductId}.", gogProductId);
            return null;
        }
    }

    public async Task<string?> GetSlugAsync(string gogProductId, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.gog.com/products/{gogProductId}";
        try
        {
            var json = await _http.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("slug", out var slugEl))
                return slugEl.GetString();
            _logger.LogWarning("GOG products API returned no slug for product {GogProductId}.", gogProductId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GOG slug for product {GogProductId}.", gogProductId);
            return null;
        }
    }

    private static decimal? ParseGogPrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // GOG returns "3999 USD" — strip currency suffix, parse as cents
        if (long.TryParse(raw.Replace(" USD", "").Trim(), out var cents))
            return cents / 100m;
        if (decimal.TryParse(raw.Replace(" USD", "").Trim(),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var dec))
            return dec;
        return null;
    }
}
