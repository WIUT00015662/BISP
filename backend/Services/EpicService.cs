using System.Text;
using System.Text.Json;

namespace Bisp.Api.Services;

public sealed class EpicService
{
    private readonly HttpClient _http;
    private readonly ILogger<EpicService> _logger;

    private const string GraphQlUrl = "https://store-site-backend-static.ak.epicgames.com/graphql";

    public EpicService(HttpClient http, ILogger<EpicService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(decimal CurrentPrice, decimal? RegularPrice)?> GetPriceAsync(
        string epicSlug,
        CancellationToken cancellationToken = default)
    {
        var query = new
        {
            query = """
                query searchStoreQuery($slug: String, $country: String!, $locale: String!) {
                  Catalog {
                    searchStore(
                      keywords: $slug
                      country: $country
                      locale: $locale
                      category: "games/edition/base"
                      count: 5
                    ) {
                      elements {
                        title
                        productSlug
                        urlSlug
                        price(country: $country) {
                          totalPrice {
                            discountPrice
                            originalPrice
                          }
                        }
                      }
                    }
                  }
                }
                """,
            variables = new { slug = epicSlug, country = "US", locale = "en-US" }
        };

        try
        {
            var body = JsonSerializer.Serialize(query);
            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.Add("User-Agent", "Mozilla/5.0");

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            var elements = doc.RootElement
                .GetProperty("data")
                .GetProperty("Catalog")
                .GetProperty("searchStore")
                .GetProperty("elements");

            // Find the best matching element by slug
            JsonElement? match = null;
            foreach (var el in elements.EnumerateArray())
            {
                var productSlug = el.TryGetProperty("productSlug", out var ps) ? ps.GetString() : null;
                var urlSlug = el.TryGetProperty("urlSlug", out var us) ? us.GetString() : null;

                if ((productSlug is not null && productSlug.StartsWith(epicSlug, StringComparison.OrdinalIgnoreCase)) ||
                    (urlSlug is not null && urlSlug.Equals(epicSlug, StringComparison.OrdinalIgnoreCase)))
                {
                    match = el;
                    break;
                }
            }

            // Fall back to first result if no exact slug match
            if (match is null && elements.GetArrayLength() > 0)
                match = elements.EnumerateArray().First();

            if (match is null) return null;

            var totalPrice = match.Value.GetProperty("price").GetProperty("totalPrice");
            var discountPriceCents = totalPrice.GetProperty("discountPrice").GetInt32();
            var originalPriceCents = totalPrice.GetProperty("originalPrice").GetInt32();

            if (discountPriceCents == 0 && originalPriceCents == 0) return null;

            var current = discountPriceCents / 100m;
            var regular = originalPriceCents / 100m;

            return (current, regular != current ? regular : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get Epic price for slug {EpicSlug}.", epicSlug);
            return null;
        }
    }
}
