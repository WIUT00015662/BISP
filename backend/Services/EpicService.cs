using System.Text;
using System.Text.Json;

namespace Bisp.Api.Services;

public sealed class EpicService
{
    private const string GraphQlEndpoint = "https://store.epicgames.com/api/graphql";
    private const string DefaultCountry = "US";
    private const string DefaultLocale = "en-US";

    private readonly HttpClient _http;
    private readonly ILogger<EpicService> _logger;

    public EpicService(HttpClient http, ILogger<EpicService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(decimal CurrentPrice, decimal? RegularPrice)?> GetPriceAsync(
        string epicSlug,
        CancellationToken cancellationToken = default)
    {
        var slug = NormalizeSlug(epicSlug);
        if (string.IsNullOrWhiteSpace(slug))
        {
            _logger.LogWarning("Epic slug missing or invalid: '{Slug}'.", epicSlug);
            return null;
        }

        _logger.LogDebug("Fetching Epic price for slug '{Slug}'.", slug);

        var payload = new
        {
            query = "query CatalogOffer($productSlug: String!, $locale: String!, $country: String!) { Catalog { catalogOffer(productSlug: $productSlug, locale: $locale, country: $country) { price { totalPrice { discountPrice originalPrice } } } } }",
            variables = new
            {
                productSlug = slug,
                locale = DefaultLocale,
                country = DefaultCountry
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Epic GraphQL returned {StatusCode} for slug '{Slug}'. Body: {Body}",
                    response.StatusCode, slug, json[..Math.Min(500, json.Length)]);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                _logger.LogWarning("Epic GraphQL errors for slug '{Slug}': {Errors}", slug, errors.ToString());
                return null;
            }

            if (!TryReadPrice(doc.RootElement, out var current, out var regular))
            {
                _logger.LogDebug("Epic price not found for slug '{Slug}'.", slug);
                return null;
            }

            return (current, regular != current ? regular : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Epic price for slug '{Slug}'.", slug);
            return null;
        }
    }

    private static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return string.Empty;
        var trimmed = slug.Trim();

        if (trimmed.Contains('/'))
        {
            var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[^1] : trimmed;
        }

        return trimmed;
    }

    private static bool TryReadPrice(JsonElement root, out decimal current, out decimal regular)
    {
        current = 0m;
        regular = 0m;

        if (!root.TryGetProperty("data", out var data)) return false;
        if (!data.TryGetProperty("Catalog", out var catalog)) return false;
        if (!catalog.TryGetProperty("catalogOffer", out var offer)) return false;
        if (!offer.TryGetProperty("price", out var price)) return false;
        if (!price.TryGetProperty("totalPrice", out var totalPrice)) return false;

        if (!totalPrice.TryGetProperty("discountPrice", out var discountEl)) return false;
        if (!totalPrice.TryGetProperty("originalPrice", out var originalEl)) return false;

        if (!discountEl.TryGetInt32(out var discountCents)) return false;
        if (!originalEl.TryGetInt32(out var originalCents)) return false;

        current = discountCents / 100m;
        regular = originalCents / 100m;
        return true;
    }
}
